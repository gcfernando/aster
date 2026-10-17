param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleaseEvidence.ps1')
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($env:VSTEST_TESTCASEFILTER) { throw 'Clear VSTEST_TESTCASEFILTER before complete release verification.' }
$run = Join-Path $root ('artifacts/ReleaseVerification/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
[void][IO.Directory]::CreateDirectory($run)
$sources = @(Get-ReleaseSourceRecords $root)
Push-Location $root
try {
    & dotnet restore ModernCalculator.sln -r win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Release restore failed.' }
    # The SDK rejects RID-specific solution builds. Build each entry project instead.
    foreach ($project in @('src/ModernCalculator.WinForms', 'tests/ModernCalculator.Tests', 'tests/ModernCalculator.UiChecks')) {
        & dotnet build $project -c Release -r win-x64 --self-contained false --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
        if ($LASTEXITCODE -ne 0) { throw "Release build failed: $project" }
    }
    Assert-ReleaseSources $root $sources
    $binaries = @(foreach ($folder in @(
        'src/ModernCalculator.WinForms/bin/Release/net10.0-windows/win-x64',
        'tests/ModernCalculator.Tests/bin/Release/net10.0/win-x64',
        'tests/ModernCalculator.UiChecks/bin/Release/net10.0-windows/win-x64'
    )) {
        Get-ChildItem -LiteralPath (Join-Path $root $folder) -File -Recurse | Where-Object Extension -in @('.dll', '.exe', '.json') |
            ForEach-Object { Get-ReleaseFileRecord $root $_.FullName }
    })
    $discovery = @(& dotnet test tests/ModernCalculator.Tests -c Release -r win-x64 --no-build --no-restore --list-tests 2>&1)
    $discovery | Set-Content -LiteralPath (Join-Path $run 'discovery.txt') -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw 'Engine discovery failed.' }
    $expected = @($discovery | Where-Object { "$_" -match '^\s{4}\S' }).Count
    if ($expected -le 0) { throw 'No engine tests discovered; inspect discovery.txt.' }
    & dotnet test tests/ModernCalculator.Tests -c Release -r win-x64 --no-build --no-restore --logger 'trx;LogFileName=engine.trx' --results-directory $run
    if ($LASTEXITCODE -ne 0) { throw 'Engine verification failed.' }
    & dotnet run --project tests/ModernCalculator.UiChecks -c Release -r win-x64 --no-build --no-restore -- (Join-Path $run 'UiChecks')
    if ($LASTEXITCODE -ne 0) { throw 'Component verification failed.' }
    $uiReports = @(Get-ChildItem -LiteralPath (Join-Path $run 'UiChecks') -Filter report.json -Recurse -File)
    if ($uiReports.Count -ne 1) { throw 'Exactly one fresh component report is required.' }
    $manifest = [ordered]@{
        schemaVersion = 1
        verificationKind = 'Aster complete local engine and hidden component run'
        generatedUtc = [DateTime]::UtcNow.ToString('O')
        sdk = (& dotnet --version | Out-String).Trim()
        runtimeIdentifier = 'win-x64'
        selfContained = $false
        discoveredEngineTests = $expected
        sources = $sources
        binaries = $binaries
        reports = @((Get-ReleaseFileRecord $root (Join-Path $run 'engine.trx')), (Get-ReleaseFileRecord $root $uiReports[0].FullName))
        limits = @('Hidden component checks do not establish physical input, screen-reader, monitor transitions or clean-machine compatibility.', 'Evidence binds local files and commands; it is not a signed or tamper-proof attestation.')
    }
    Assert-ReleaseManifest $root ([pscustomobject]$manifest)
    $manifestPath = Join-Path $run 'manifest.json'
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    Write-Output "VERIFIED MANIFEST: $manifestPath"
} finally { Pop-Location }
