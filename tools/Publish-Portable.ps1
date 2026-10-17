param([Parameter(Mandatory = $true)][string]$VerificationManifest)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleaseEvidence.ps1')
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manifestPath = Resolve-ReleasePath $root $VerificationManifest
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
Assert-ReleaseManifest $root $manifest
$candidate = Join-Path $root ('artifacts/ReleaseCandidates/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$folder = Join-Path $candidate 'Aster-win-x64'
[void][IO.Directory]::CreateDirectory($folder)
Push-Location $root
try {
    & dotnet publish src/ModernCalculator.WinForms -c Release -r win-x64 --self-contained false --no-build --no-restore -o $folder
    if ($LASTEXITCODE -ne 0) { throw 'Portable publishing failed.' }
    Assert-ReleasePayload $folder @($manifest.binaries)
    Assert-ReleaseManifest $root $manifest
    Copy-ReleaseDocumentation $root $folder
    $evidence = Join-Path $folder 'verification'
    [void][IO.Directory]::CreateDirectory($evidence)
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $evidence 'manifest.json')
    Copy-Item -LiteralPath (Resolve-ReleasePath $root $manifest.reports[0].path) -Destination (Join-Path $evidence 'engine.trx')
    Copy-Item -LiteralPath (Resolve-ReleasePath $root $manifest.reports[1].path) -Destination (Join-Path $evidence 'ui-report.json')
    @'
ASTER CALCULATOR — FULLY ENABLED PORTABLE CANDIDATE

Extract the complete ZIP to a writable application folder, then run Aster.exe.
Keep all files together. No account, payment, product key or activation is used.
Windows x64 and the .NET 10 Desktop Runtime are required. Supported Windows
editions: https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md
Runtime: https://dotnet.microsoft.com/en-us/download/dotnet/10.0

This framework-dependent package does not bundle the .NET runtime. Maintain
supported Windows and runtime patches. Calculations work offline.
F1 opens help, including local version/support diagnostics. LICENSE is MIT.
docs/release-guide.md explains install, update, rollback, uninstall and privacy.
verification/ records engine and hidden component checks and their limits.
This unsigned preview candidate is not a certificate of stable-release readiness.
'@ | Set-Content -LiteralPath (Join-Path $folder 'START-HERE.txt') -Encoding utf8
    $package = [ordered]@{
        schemaVersion = 1
        version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $folder 'Aster.dll')).ProductVersion
        runtimeIdentifier = 'win-x64'
        selfContained = $false
        signing = 'Unsigned preview candidate'
        verificationManifestSha256 = (Get-FileHash -LiteralPath $manifestPath).Hash.ToLowerInvariant()
        files = @(Get-ChildItem -LiteralPath $folder -File -Recurse | Sort-Object FullName | ForEach-Object { Get-ReleaseFileRecord $folder $_.FullName })
    }
    $package | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $folder 'package-manifest.json') -Encoding utf8
    $archive = Join-Path $candidate 'Aster-win-x64.zip'
    Write-ReleaseArchive $folder $archive
    $hash = (Get-FileHash -LiteralPath $archive).Hash.ToLowerInvariant()
    "$hash  Aster-win-x64.zip" | Set-Content -LiteralPath (Join-Path $candidate 'SHA256SUMS.txt') -Encoding utf8
    Write-Output "VERIFIED LOCAL CANDIDATE: $archive"
} finally { Pop-Location }
