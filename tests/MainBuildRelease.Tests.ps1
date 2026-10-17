# Publication policy tests use a fake GitHub boundary; no network or release writes.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../tools/Publish-MainBuild.ps1')
$checks = 0
function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
}
function Assert-Rejected([scriptblock]$Action) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    Assert-True $rejected 'Unsafe preview publication was accepted.'
}
$fixture = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('../artifacts/MainBuildReleaseTests/' + [Guid]::NewGuid().ToString('N'))))
[void][IO.Directory]::CreateDirectory($fixture)
$archive = Join-Path $fixture 'Aster-win-x64.zip'
$checksum = Join-Path $fixture 'SHA256SUMS.txt'
[IO.File]::WriteAllText($archive, 'verified package fixture')
$hash = (Get-FileHash -LiteralPath $archive).Hash.ToLowerInvariant()
"$hash  Aster-win-x64.zip" | Set-Content -LiteralPath $checksum
Assert-True ((Assert-MainBuildPackage $fixture) -ceq $hash) 'Matching package was rejected.'
foreach ($invalid in @("$('0' * 64)  Aster-win-x64.zip", "$hash  Other.zip", "$hash  Aster-win-x64.zip`n$hash  Aster-win-x64.zip")) {
    $invalid | Set-Content -LiteralPath $checksum
    Assert-Rejected { Assert-MainBuildPackage $fixture }
}
"$hash  Aster-win-x64.zip" | Set-Content -LiteralPath $checksum
$owned = @{ tag_name = 'latest'; prerelease = $true; immutable = $false; body = $script:MainBuildMarker }
Assert-MainBuildRelease ([pscustomobject]$owned)
$checks++
foreach ($change in @(@{tag_name='v1.0'}, @{prerelease=$false}, @{immutable=$true}, @{body='Manual release'})) {
    $invalid = $owned.Clone()
    foreach ($key in $change.Keys) { $invalid[$key] = $change[$key] }
    Assert-Rejected { Assert-MainBuildRelease ([pscustomobject]$invalid) }
}

$savedEnvironment = @{}
foreach ($name in @('GITHUB_ACTIONS', 'GITHUB_EVENT_NAME', 'GITHUB_REF', 'GITHUB_SHA', 'GITHUB_REPOSITORY', 'GITHUB_RUN_ID', 'GITHUB_STEP_SUMMARY')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}
$script:calls = [Collections.Generic.List[object]]::new()
$script:head = 'a' * 40
$script:releases = '[[]]'
$script:refs = '[]'
$script:fail = ''
function Invoke-BuildGitHub([string[]]$Arguments) {
    $script:calls.Add($Arguments)
    $command = $Arguments -join ' '
    if ($script:fail -and $command.Contains($script:fail)) { throw 'Simulated GitHub API failure.' }
    if ($command.Contains('/git/ref/heads/main')) { return $script:head }
    if ($command.Contains('/releases?')) { return $script:releases }
    if ($command.Contains('/matching-refs/')) { return $script:refs }
    return ''
}
try {
    $env:GITHUB_ACTIONS = 'true'
    $env:GITHUB_EVENT_NAME = 'push'
    $env:GITHUB_REF = 'refs/heads/main'
    $env:GITHUB_SHA = $script:head
    $env:GITHUB_REPOSITORY = 'example/aster'
    $env:GITHUB_RUN_ID = '123'
    $env:GITHUB_STEP_SUMMARY = Join-Path $fixture 'summary.md'
    foreach ($change in @(@{GITHUB_EVENT_NAME='pull_request'}, @{GITHUB_EVENT_NAME='workflow_dispatch'}, @{GITHUB_REF='refs/heads/feature'}, @{GITHUB_ACTIONS='false'}, @{GITHUB_SHA='invalid'})) {
        foreach ($key in $change.Keys) {
            $previous = [Environment]::GetEnvironmentVariable($key)
            [Environment]::SetEnvironmentVariable($key, $change[$key])
            Assert-Rejected { Publish-MainBuild $fixture }
            [Environment]::SetEnvironmentVariable($key, $previous)
        }
    }
    Assert-True ($script:calls.Count -eq 0) 'Blocked events reached GitHub.'
    $script:head = 'b' * 40
    Publish-MainBuild $fixture
    Assert-True ($script:calls.Count -eq 1) 'An old main rerun wrote to GitHub.'
    $script:head = $env:GITHUB_SHA
    $script:calls.Clear()
    Publish-MainBuild $fixture
    $create = $script:calls[-1] -join ' '
    Assert-True ($create.Contains("release create latest --repo example/aster --target $($env:GITHUB_SHA)")) 'Initial release targets the wrong commit.'
    Assert-True ($create.Contains('--prerelease --latest=false --notes-file')) 'Preview was promoted to stable.'
    Assert-True ($create.EndsWith("Aster-win-x64.zip $(Join-Path $fixture 'SHA256SUMS.txt')")) 'Unexpected release assets.'
    $script:releases = ConvertTo-Json -InputObject @(@($owned)) -Depth 5 -Compress
    $script:calls.Clear()
    Publish-MainBuild $fixture
    Assert-True (($script:calls[-3] -join ' ').Contains('release upload latest')) 'Existing preview was not updated.'
    Assert-True (($script:calls[-2] -join ' ').Contains('/git/refs/tags/latest')) 'The rolling tag was not updated.'
    Assert-True (($script:calls[-1] -join ' ').Contains('release edit latest')) 'Preview notes were not updated.'
    $script:releases = '[[{"tag_name":"latest","prerelease":false,"immutable":false,"body":"Manual release"}]]'
    $script:calls.Clear()
    Assert-Rejected { Publish-MainBuild $fixture }
    Assert-True ($script:calls.Count -eq 2) 'Unowned release was modified.'
    $script:releases = '[[]]'
    $script:refs = '[{"ref":"refs/tags/latest"}]'
    Assert-Rejected { Publish-MainBuild $fixture }
    $script:refs = '[]'
    $script:fail = '/releases?'
    $script:calls.Clear()
    Assert-Rejected { Publish-MainBuild $fixture }
    Assert-True ($script:calls.Count -eq 2) 'API failure was treated as an absent release.'
    $script:fail = 'release upload'
    $script:releases = ConvertTo-Json -InputObject @(@($owned)) -Depth 5 -Compress
    $script:calls.Clear()
    Assert-Rejected { Publish-MainBuild $fixture }
    Assert-True ($script:calls.Count -eq 3) 'Failed asset upload still advanced the tag or notes.'
} finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) }
}
Write-Output "PASS: $checks main-build publication checks."
