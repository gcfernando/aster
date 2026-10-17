# Dependency-free regressions for the release evidence trust boundary.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../tools/ReleaseEvidence.ps1')
$checks = 0
function Assert-Rejected([scriptblock]$Action) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw 'Invalid release evidence was accepted.' }
    $script:checks++
}
function New-EngineReport {
    [xml]'<TestRun><Results><UnitTestResult outcome="Passed" executionId="11111111-1111-1111-1111-111111111111"/><UnitTestResult outcome="Passed" executionId="22222222-2222-2222-2222-222222222222"/></Results><ResultSummary outcome="Completed"><Counters total="2" executed="2" passed="2" failed="0" notExecuted="0"/></ResultSummary></TestRun>'
}
function New-UiReport {
    [pscustomobject]@{ focus = ''; passed = 2; failed = 0; skipped = 0; checks = @(@{Status='PASS'}, @{Status='PASS'}) }
}
Assert-ReleaseEngineReport (New-EngineReport) 2
Assert-ReleaseUiReport (New-UiReport)
$checks += 2
foreach ($counter in @('failed', 'notExecuted', 'aborted', 'inconclusive', 'timeout', 'pending')) {
    $report = New-EngineReport
    $report.TestRun.ResultSummary.Counters.SetAttribute($counter, '1')
    Assert-Rejected { Assert-ReleaseEngineReport $report 2 }
}
foreach ($edit in @(
    { param($r) $r.TestRun.ResultSummary.SetAttribute('outcome', 'Aborted') },
    { param($r) $r.TestRun.ResultSummary.Counters.SetAttribute('total', '3') },
    { param($r) $r.TestRun.ResultSummary.Counters.SetAttribute('executed', '1') },
    { param($r) $r.TestRun.ResultSummary.Counters.SetAttribute('passed', '1') },
    { param($r) $r.TestRun.Results.UnitTestResult[1].SetAttribute('executionId', '11111111-1111-1111-1111-111111111111') },
    { param($r) $r.TestRun.Results.UnitTestResult[0].RemoveAttribute('executionId') },
    { param($r) $r.TestRun.Results.UnitTestResult[0].SetAttribute('outcome', 'Failed') },
    { param($r) [void]$r.TestRun.Results.RemoveChild($r.TestRun.Results.UnitTestResult[0]) }
)) {
    $report = New-EngineReport
    & $edit $report
    Assert-Rejected { Assert-ReleaseEngineReport $report 2 }
}
Assert-Rejected { Assert-ReleaseEngineReport (New-EngineReport) 0 }
Assert-Rejected { Assert-ReleaseEngineReport (New-EngineReport) 3 }
foreach ($edit in @(
    { param($r) $r.focus = 'filtered' },
    { param($r) $r.passed = 1 },
    { param($r) $r.failed = 1 },
    { param($r) $r.skipped = 1 },
    { param($r) $r.checks[0].Status = 'FAIL' },
    { param($r) $r.checks = @() }
)) {
    $report = New-UiReport
    & $edit $report
    Assert-Rejected { Assert-ReleaseUiReport $report }
}
Assert-Rejected { Assert-ReleaseUiReport ([pscustomobject]@{passed=2;failed=0;skipped=0}) }
$fixture = Join-Path $PSScriptRoot ('../artifacts/ReleaseEvidenceTests/' + [Guid]::NewGuid().ToString('N'))
$fixture = [IO.Path]::GetFullPath($fixture)
[void][IO.Directory]::CreateDirectory((Join-Path $fixture 'src'))
[IO.File]::WriteAllText((Join-Path $fixture 'src/Input.cs'), '// input')
[IO.File]::WriteAllText((Join-Path $fixture 'LICENSE'), 'fixture')
$records = @(Get-ReleaseSourceRecords $fixture)
Assert-ReleaseSources $fixture $records
$checks++
[IO.File]::WriteAllText((Join-Path $fixture 'src/Input.cs'), '// changed')
Assert-Rejected { Assert-ReleaseSources $fixture $records }
[IO.File]::WriteAllText((Join-Path $fixture 'src/Input.cs'), '// input')
[IO.File]::WriteAllText((Join-Path $fixture 'src/Added.cs'), '// new input')
Assert-Rejected { Assert-ReleaseSources $fixture $records }
Assert-Rejected { Assert-ReleaseRecords $fixture @() }
Assert-Rejected { Assert-ReleaseRecords $fixture @($records[0], $records[0]) }
Assert-Rejected { Resolve-ReleasePath $fixture '../escape' }
Assert-Rejected { Resolve-ReleasePath $fixture ([IO.Path]::GetFullPath($fixture)) }
Assert-Rejected { Assert-ReleaseRecords $fixture @([pscustomobject]@{path='missing';sha256=('0' * 64)}) }
Assert-Rejected { Assert-ReleaseManifest $fixture ([pscustomobject]@{schemaVersion=999}) }
$payload = Join-Path $fixture 'payload'
[void][IO.Directory]::CreateDirectory($payload)
$payloadRecords = @(foreach ($name in @('Aster.exe', 'Aster.dll', 'Aster.deps.json', 'Aster.runtimeconfig.json', 'ModernCalculator.Core.dll', 'ModernCalculator.Infrastructure.dll')) {
    $path = Join-Path $payload $name
    [IO.File]::WriteAllText($path, $name)
    $record = Get-ReleaseFileRecord $payload $path
    $record.path = 'src/ModernCalculator.WinForms/bin/Release/net10.0-windows/win-x64/' + $record.path
    $record
})
Assert-ReleasePayload $payload $payloadRecords
$checks++
$fullRoot = Join-Path $fixture 'full-manifest'
$fullBinaries = @(foreach ($record in $payloadRecords) {
    $path = Join-Path $fullRoot $record.path
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
    [IO.File]::WriteAllText($path, [IO.Path]::GetFileName($path))
    Get-ReleaseFileRecord $fullRoot $path
    if ($path.EndsWith('.dll')) {
        foreach ($destination in @('tests/ModernCalculator.UiChecks/bin/Release/net10.0-windows/win-x64', 'tests/ModernCalculator.Tests/bin/Release/net10.0/win-x64')) {
            if ($path.EndsWith('Aster.dll') -and $destination.Contains('ModernCalculator.Tests/')) { continue }
            $copy = Join-Path $fullRoot ($destination + '/' + [IO.Path]::GetFileName($path))
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($copy))
            Copy-Item -LiteralPath $path -Destination $copy
            Get-ReleaseFileRecord $fullRoot $copy
        }
    }
})
[IO.File]::WriteAllText((Join-Path $fullRoot 'LICENSE'), 'fixture source')
[void][IO.Directory]::CreateDirectory((Join-Path $fullRoot 'artifacts'))
$enginePath = Join-Path $fullRoot 'artifacts/engine.trx'
(New-EngineReport).Save($enginePath)
$uiPath = Join-Path $fullRoot 'artifacts/ui.json'
New-UiReport | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $uiPath
$fullManifest = [pscustomobject]@{
    schemaVersion=1; runtimeIdentifier='win-x64'; selfContained=$false
    verificationKind='Aster complete local engine and hidden component run'; discoveredEngineTests=2
    sources=@(Get-ReleaseSourceRecords $fullRoot); binaries=$fullBinaries
    reports=@((Get-ReleaseFileRecord $fullRoot $enginePath), (Get-ReleaseFileRecord $fullRoot $uiPath))
}
Assert-ReleaseManifest $fullRoot $fullManifest
$checks++
foreach ($project in @('ModernCalculator.UiChecks', 'ModernCalculator.Tests')) {
    $changed = @($fullManifest.binaries | Where-Object { $_.path.StartsWith('tests/' + $project + '/') -and $_.path.EndsWith('ModernCalculator.Core.dll') })[0]
    $file = Join-Path $fullRoot $changed.path
    [IO.File]::WriteAllText($file, 'different tested DLL')
    $changed.sha256 = (Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant()
    Assert-Rejected { Assert-ReleaseManifest $fullRoot $fullManifest }
    [IO.File]::WriteAllText($file, 'ModernCalculator.Core.dll')
    $changed.sha256 = (Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant()
}
$badUi = New-UiReport; $badUi.focus='filtered'
$badUi | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $uiPath
$fullManifest.reports[1] = Get-ReleaseFileRecord $fullRoot $uiPath
Assert-Rejected { Assert-ReleaseManifest $fullRoot $fullManifest }
foreach ($name in @('Aster.dll', 'Aster.runtimeconfig.json', 'Aster.deps.json')) {
    [IO.File]::WriteAllText((Join-Path $payload $name), 'changed')
    Assert-Rejected { Assert-ReleasePayload $payload $payloadRecords }
    [IO.File]::WriteAllText((Join-Path $payload $name), $name)
}
[IO.File]::WriteAllText((Join-Path $payload 'Unverified.dll'), 'extra')
Assert-Rejected { Assert-ReleasePayload $payload $payloadRecords }
Write-Output "Release evidence checks: $checks passed. Fixtures: $fixture"
