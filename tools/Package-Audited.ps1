# Developer ::> Gehan Fernando
param(
    [Parameter(Mandatory = $true)][string]$VerificationManifest,
    [Parameter(Mandatory = $true)][string]$UiReport,
    [string]$EngineReport = 'artifacts/TestResults/audit-final.trx',
    [ValidatePattern('^[A-Za-z][A-Za-z0-9-]*$')][string]$Edition = 'Audited',
    [ValidatePattern('^[A-Za-z][A-Za-z0-9-]*$')][string]$BuildOutput = 'AuditAcceptance',
    [ValidateRange(1, [int]::MaxValue)][int]$ExpectedEngineTests = 283,
    [ValidateRange(1, [int]::MaxValue)][int]$MinimumUiChecks = 205
)
$ErrorActionPreference = 'Stop'

function Assert-CompleteEngineReport([xml]$Report, [int]$ExpectedCount) {
    $summary = $Report.TestRun.ResultSummary
    $counters = $summary.Counters
    $message = "The complete $ExpectedCount-test engine report is required; every test must execute and pass."
    if ($ExpectedCount -le 0 -or $null -eq $counters -or $summary.outcome -ne 'Completed') { throw $message }
    foreach ($name in @('total', 'executed', 'passed')) {
        $count = 0
        if (-not [int]::TryParse($counters.GetAttribute($name), [ref]$count) -or $count -ne $ExpectedCount) { throw $message }
    }
    foreach ($counter in $counters.Attributes) {
        if ($counter.Name -in @('total', 'executed', 'passed')) { continue }
        $count = 0
        if (-not [int]::TryParse($counter.Value, [ref]$count) -or $count -ne 0) { throw $message }
    }
    $testResults = @($Report.TestRun.Results.UnitTestResult)
    if ($testResults.Count -ne $ExpectedCount -or @($testResults | Where-Object outcome -ne 'Passed').Count -ne 0) { throw $message }
}

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'ReleaseEvidence.ps1')
$verification = Get-Content -LiteralPath (Resolve-ReleasePath $projectRoot $VerificationManifest) -Raw | ConvertFrom-Json
Assert-ReleaseManifest $projectRoot $verification
$releaseFolder = Join-Path $projectRoot "artifacts/Aster-$Edition"
$editionName = $Edition.ToLowerInvariant()
$runtimeArchive = Join-Path $projectRoot "artifacts/Aster-$editionName-win-x64.zip"
$sourceArchive = Join-Path $projectRoot "artifacts/Aster-$editionName-source.zip"
foreach ($archivePath in @($runtimeArchive, $sourceArchive)) {
    if (Test-Path -LiteralPath $archivePath) { throw "Preserve the existing archive before regenerating: $archivePath" }
}
$uiReportPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $UiReport))
$engineReportPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $EngineReport))
# Check acceptance reports before packaging release files.
$uiResult = Get-Content -LiteralPath $uiReportPath -Raw | ConvertFrom-Json
[xml]$engineResult = Get-Content -LiteralPath $engineReportPath -Raw
if ($uiResult.failed -ne 0 -or $uiResult.skipped -ne 0 -or $uiResult.focus -ne '' -or $uiResult.passed -lt $MinimumUiChecks) { throw 'A complete passing UI report is required.' }
Assert-CompleteEngineReport $engineResult $ExpectedEngineTests
if ($ExpectedEngineTests -ne $verification.discoveredEngineTests -or
    (Get-FileHash -LiteralPath $engineReportPath).Hash -ne $verification.reports[0].sha256 -or
    (Get-FileHash -LiteralPath $uiReportPath).Hash -ne $verification.reports[1].sha256) { throw 'Reports do not match the verified release run.' }
$builtFolder = Join-Path $projectRoot "src/ModernCalculator.WinForms/bin/$BuildOutput"
Assert-ReleasePayload $releaseFolder @($verification.binaries)
foreach ($file in @('Aster.exe', 'Aster.dll', 'ModernCalculator.Core.dll', 'ModernCalculator.Infrastructure.dll')) {
    $verifiedBinary = @($verification.binaries | Where-Object path -eq "src/ModernCalculator.WinForms/bin/Release/net10.0-windows/win-x64/$file")
    if ($verifiedBinary.Count -ne 1 -or (Get-FileHash -LiteralPath (Join-Path $releaseFolder $file)).Hash -ne $verifiedBinary[0].sha256) { throw "Published binary differs from verified release: $file" }
    if ((Get-FileHash -LiteralPath (Join-Path $releaseFolder $file)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $builtFolder $file)).Hash) { throw "Published binary differs from acceptance build: $file" }
}
foreach ($file in @('ModernCalculator.Core.dll', 'ModernCalculator.Infrastructure.dll')) {
    $testedFile = Join-Path $projectRoot "tests/ModernCalculator.Tests/bin/$BuildOutput/$file"
    if ((Get-FileHash -LiteralPath $testedFile).Hash -ne (Get-FileHash -LiteralPath (Join-Path $releaseFolder $file)).Hash) { throw "Published binary differs from tested engine: $file" }
}
$hostBytes = [IO.File]::ReadAllBytes((Join-Path $releaseFolder 'Aster.exe'))
$peOffset = [BitConverter]::ToInt32($hostBytes, 0x3c)
if ([BitConverter]::ToUInt16($hostBytes, $peOffset + 4) -ne 0x8664) { throw 'The published app host is not Windows x64.' }
$evidenceFolder = New-Item -ItemType Directory -Path (Join-Path $releaseFolder 'verification') -Force
Copy-ReleaseDocumentation $projectRoot $releaseFolder
Copy-Item -LiteralPath (Resolve-ReleasePath $projectRoot $VerificationManifest) -Destination (Join-Path $evidenceFolder.FullName 'manifest.json')
Copy-Item -LiteralPath $uiReportPath -Destination (Join-Path $evidenceFolder.FullName 'ui-report.json')
Copy-Item -LiteralPath $engineReportPath -Destination (Join-Path $evidenceFolder.FullName 'engine-tests.trx')
foreach ($mode in @('Standard', 'Scientific', 'Programmer', 'Converter', 'Dates')) {
    $pngPath = Join-Path (Split-Path -Parent $uiReportPath) "main-Dark-100pct-simulated-$mode.png"
    Copy-Item -LiteralPath $pngPath -Destination $evidenceFolder.FullName
}
@'
ASTER CALCULATOR — AUDITED EDITION

Run Aster.exe. Keep the extracted folder together.
Requires Windows x64 and the .NET 10 Desktop Runtime. Works offline, fully enabled.
Supported Windows editions: https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md
LICENSE contains the MIT license. No payment, key or activation is required.

Normal centered startup; drag the brand/header, resize at the edges, and use
the custom minimize, maximize/restore and close buttons. F1 opens Help.
Standard memory is above the keys; Scientific memory is below them.
MA opens all slots and shows their count. MR recalls; MS stores a new slot.

Standard accepts 100 significant input digits and supports Ctrl+Z/Ctrl+Y.
Scientific offers editable operand templates, Ans and visible function banks.
Programmer edits the selected base and shows the other three representations.
Converter searches units by name/symbol/alias and offers Favorites/Recent.
Dates separates live differences and interval adjustment with inline calendars.
Preferences changes are drafts until Save. Cancel, X and Escape discard them.

Ctrl+1 through Ctrl+5 changes mode (alternate preset in Preferences → Window).
There are no dropdowns or scrolling panels. Inline pages expose long values,
all units and all 64 bits. Copy result keeps the complete stored numeric value.

README.md explains numeric precision, controls and persistence.
docs/development.md explains the build and verification workflow and its limits.
docs/numeric-behavior.md describes each engine's precision and input contracts.
verification/ contains the acceptance reports and five component previews.
These are hidden component checks, not physical-input or monitor-transition tests.

Source archive: Aster-audited-source.zip. Open ModernCalculator.sln and set
ModernCalculator.WinForms as Startup Project. Core is a class library.
'@ | ForEach-Object { $_.Replace('AUDITED EDITION', $Edition.ToUpperInvariant() + ' EDITION').Replace('Aster-audited-source.zip', "Aster-$editionName-source.zip") } |
    Set-Content -LiteralPath (Join-Path $releaseFolder 'START-HERE.txt') -Encoding utf8

Add-Type -AssemblyName System.IO.Compression
$sourceFiles = @(& rg --files --hidden -g '!.local/**' -g '!.vs/**' -g '!artifacts/**' -g '!.git/**' -g '!.agents/**' -g '!.codex/**' -g '!.aws/**' -g '!**/bin/**' -g '!**/obj/**' -g '!*.user' -g '!*.suo' $projectRoot)
if ($LASTEXITCODE -ne 0) { throw 'Source enumeration failed.' }
$sourceEntries = @($sourceFiles | ForEach-Object {
    $absolute = [IO.Path]::GetFullPath($_)
    $relative = [IO.Path]::GetRelativePath($projectRoot, $absolute).Replace('\', '/')
    if ($relative.StartsWith('../') -or $relative -match '(^|/)(bin|obj|artifacts|\.vs|\.local|\.git|\.agents|\.codex|\.aws)(/|$)') { throw "Unexpected source path: $relative" }
    [pscustomobject]@{ Source = $absolute; Entry = $relative }
})
$sourceEntries += [pscustomobject]@{ Source = $uiReportPath; Entry = 'verification/ui-report.json' }
$sourceEntries += [pscustomobject]@{ Source = $engineReportPath; Entry = 'verification/engine-tests.trx' }
$sourceEntries = @($sourceEntries | Sort-Object Entry)
$sourceEntries.Entry | Set-Content -LiteralPath (Join-Path $projectRoot "artifacts/$editionName-source-file-manifest.txt") -Encoding utf8

# Verify every archived entry against its source before reporting success.
function Write-VerifiedArchive([string]$ArchivePath, [array]$Entries) {
    if (Test-Path -LiteralPath $ArchivePath) { throw "Archive already exists; preserve it before regenerating: $ArchivePath" }
    $stream = [IO.File]::Open($ArchivePath, [IO.FileMode]::CreateNew)
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($item in $Entries) {
            $entry = $archive.CreateEntry($item.Entry, [IO.Compression.CompressionLevel]::Optimal)
            $inputFile = [IO.File]::OpenRead($item.Source); $outputFile = $entry.Open()
            try { $inputFile.CopyTo($outputFile) } finally { $inputFile.Dispose(); $outputFile.Dispose() }
        }
    } finally { $archive.Dispose(); $stream.Dispose() }
    $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        if ($archive.Entries.Count -ne $Entries.Count) { throw 'Archive entry count differs from manifest.' }
        foreach ($item in $Entries) {
            $entry = $archive.GetEntry($item.Entry)
            if ($null -eq $entry) { throw "Missing archive entry: $($item.Entry)" }
            $entryStream = $entry.Open()
            try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($entryStream)) } finally { $entryStream.Dispose() }
            if ($hash -ne (Get-FileHash -LiteralPath $item.Source -Algorithm SHA256).Hash) { throw "Archive content mismatch: $($item.Entry)" }
        }
    } finally { $archive.Dispose() }
    Write-Output "Verified $($Entries.Count) entries: $ArchivePath"
}
$runtimeEntries = @(Get-ChildItem -LiteralPath $releaseFolder -File -Recurse | Sort-Object FullName | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; Entry = [IO.Path]::GetRelativePath($releaseFolder, $_.FullName).Replace('\', '/') }
})
Write-VerifiedArchive $runtimeArchive $runtimeEntries
Write-VerifiedArchive $sourceArchive $sourceEntries
@($runtimeArchive, $sourceArchive) | ForEach-Object { $file = Get-FileHash -LiteralPath $_ -Algorithm SHA256; "$($file.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($_))" } |
    Set-Content -LiteralPath (Join-Path $projectRoot ("artifacts/" + $Edition.ToUpperInvariant() + '-SHA256SUMS.txt')) -Encoding utf8
Write-Output 'Published/tested binary hashes and both archive contents match.'
