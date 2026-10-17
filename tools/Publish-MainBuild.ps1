param([string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# This marker reserves only the rolling preview release, never a versioned release.
$script:MainBuildMarker = '<!-- aster-automated-main-preview -->'

function Assert-MainBuildPackage([string]$Directory) {
    $archive = Join-Path $Directory 'Aster-win-x64.zip'
    $checksum = Join-Path $Directory 'SHA256SUMS.txt'
    $line = (Get-Content -LiteralPath $checksum -Raw).Trim()
    if ($line -cnotmatch '^([a-f0-9]{64})  Aster-win-x64\.zip$') { throw 'Invalid package checksum manifest.' }
    $expected = $Matches[1]
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expected) {
        throw 'Downloaded package does not match its checksum.'
    }
    return $expected
}

function Assert-MainBuildRelease($Release) {
    if ($Release.tag_name -cne 'latest' -or -not $Release.prerelease -or
        $Release.immutable -or $Release.body -notlike "*$script:MainBuildMarker*") {
        throw 'Refusing to replace an unrelated, stable or immutable release.'
    }
}

function Invoke-BuildGitHub([string[]]$Arguments) {
    $output = @(& gh @Arguments)
    if ($LASTEXITCODE -ne 0) { throw "GitHub command failed: $($Arguments[0])" }
    return ($output -join "`n")
}

function Publish-MainBuild([string]$Directory) {
    if ($env:GITHUB_ACTIONS -cne 'true' -or $env:GITHUB_EVENT_NAME -cne 'push' -or $env:GITHUB_REF -cne 'refs/heads/main') {
        throw 'Preview publication is restricted to GitHub Actions pushes to main.'
    }
    if ($env:GITHUB_SHA -cnotmatch '^[a-f0-9]{40}$' -or $env:GITHUB_REPOSITORY -cnotmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$' -or
        $env:GITHUB_RUN_ID -cnotmatch '^[0-9]+$') {
        throw 'Invalid GitHub build identity.'
    }
    $hash = Assert-MainBuildPackage $Directory
    $repo = $env:GITHUB_REPOSITORY
    $commit = $env:GITHUB_SHA
    $head = Invoke-BuildGitHub -Arguments @('api', "repos/$repo/git/ref/heads/main", '--jq', '.object.sha')
    if ($head.Trim() -cne $commit) {
        Write-Output 'A newer main commit exists. Package remains in this run; skipping the rolling download update.'
        return
    }
    # A successful collection request distinguishes a missing release from API/authentication failure.
    $releases = Invoke-BuildGitHub -Arguments @('api', "repos/$repo/releases?per_page=100", '--paginate', '--slurp') | ConvertFrom-Json
    $existing = @($releases | ForEach-Object { $_ } | Where-Object { $_.tag_name -ceq 'latest' })
    if ($existing.Count -gt 1) { throw 'Ambiguous latest release.' }
    if ($existing.Count -eq 1) {
        Assert-MainBuildRelease $existing[0]
    } else {
        $refs = Invoke-BuildGitHub -Arguments @('api', "repos/$repo/git/matching-refs/tags/latest") | ConvertFrom-Json
        if (@($refs | Where-Object { $_.ref -ceq 'refs/tags/latest' }).Count -gt 0) {
            throw 'The latest tag already exists without an owned preview release.'
        }
    }
    $runUrl = "https://github.com/$repo/actions/runs/$($env:GITHUB_RUN_ID)"
    $notes = @"
$script:MainBuildMarker
Automatically built from main commit [$commit](https://github.com/$repo/commit/$commit).
[Passing Windows verification and package evidence]($runUrl).

Download **Aster-win-x64.zip**, extract the entire ZIP, then run **Aster.exe**.
Windows x64 and the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) are required.
All features are enabled; no account, payment or activation key is required. Calculations work offline.

This is a rolling **unsigned preview**, not a stable release. Automated engine and hidden component checks do not cover every supported Windows machine, assistive technology or physical monitor/input combination. See the included release guide for remaining acceptance checks.

SHA-256 of Aster-win-x64.zip: $hash
The latest tag and its two assets are replaced by passing current-main builds. Download updates manually; the app does not contact GitHub. Older run artifacts remain available for 30 days.
"@
    $notesPath = Join-Path $Directory 'release-notes.md'
    $notes | Set-Content -LiteralPath $notesPath -Encoding utf8
    $assets = @((Join-Path $Directory 'Aster-win-x64.zip'), (Join-Path $Directory 'SHA256SUMS.txt'))
    $title = 'Latest main build (Windows x64 preview)'
    if ($existing.Count -eq 0) {
        # gh stages assets on a draft before publishing the initial release.
        [void](Invoke-BuildGitHub -Arguments (@('release', 'create', 'latest', '--repo', $repo, '--target', $commit,
            '--title', $title, '--prerelease', '--latest=false', '--notes-file', $notesPath) + $assets))
    } else {
        [void](Invoke-BuildGitHub -Arguments (@('release', 'upload', 'latest', '--repo', $repo, '--clobber') + $assets))
        # Only this explicitly reserved moving preview tag is updated; main history is untouched.
        [void](Invoke-BuildGitHub -Arguments @('api', '--method', 'PATCH', "repos/$repo/git/refs/tags/latest", '-f', "sha=$commit", '-F', 'force=true'))
        [void](Invoke-BuildGitHub -Arguments @('release', 'edit', 'latest', '--repo', $repo, '--title', $title,
            '--prerelease', '--latest=false', '--draft=false', '--notes-file', $notesPath))
    }
    "[Download latest Windows preview](https://github.com/$repo/releases/download/latest/Aster-win-x64.zip) ([release notes](https://github.com/$repo/releases/tag/latest))." |
        Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY
}

if ($MyInvocation.InvocationName -ne '.') { Publish-MainBuild $PackageDirectory }
