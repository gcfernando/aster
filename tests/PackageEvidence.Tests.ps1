# Run with: pwsh -NoProfile -File tests/PackageEvidence.Tests.ps1
# Load only the report validator; the packaging entry point must not create archives during tests.
$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot '../tools/Package-Audited.ps1'
$parseErrors = $null
$tokens = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw ($parseErrors | Out-String) }
$validator = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Assert-CompleteEngineReport' }, $true)
if ($null -eq $validator) { throw 'The package report validator is missing.' }
. ([scriptblock]::Create($validator.Extent.Text))

function New-Report {
    [xml]@'
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results><UnitTestResult outcome="Passed" /><UnitTestResult outcome="Passed" /></Results>
  <ResultSummary outcome="Completed">
    <Counters total="2" executed="2" passed="2" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" />
  </ResultSummary>
</TestRun>
'@
}

$passed = 0
Assert-CompleteEngineReport (New-Report) 2
$passed++
foreach ($counter in @('failed', 'error', 'timeout', 'aborted', 'inconclusive', 'passedButRunAborted', 'notRunnable', 'notExecuted', 'disconnected', 'warning', 'inProgress', 'pending')) {
    $report = New-Report
    $report.TestRun.ResultSummary.Counters.SetAttribute($counter, '1')
    $rejected = $false
    try { Assert-CompleteEngineReport $report 2 } catch { $rejected = $true }
    if (-not $rejected) { throw "Accepted a report with a nonzero $counter counter." }
    $passed++
}
foreach ($change in @(
    { param($r) $r.TestRun.ResultSummary.SetAttribute('outcome', 'Aborted') },
    { param($r) $r.TestRun.ResultSummary.Counters.SetAttribute('total', '3') },
    { param($r) $r.TestRun.ResultSummary.Counters.SetAttribute('executed', '1') },
    { param($r) $r.TestRun.ResultSummary.Counters.SetAttribute('passed', '1') },
    { param($r) $r.TestRun.ResultSummary.Counters.RemoveAttribute('executed') },
    { param($r) $r.TestRun.ResultSummary.Counters.SetAttribute('total', 'invalid') },
    { param($r) $r.TestRun.Results.UnitTestResult[0].SetAttribute('outcome', 'NotExecuted') },
    { param($r) [void]$r.TestRun.Results.RemoveChild($r.TestRun.Results.UnitTestResult[0]) },
    { param($r) [void]$r.TestRun.ResultSummary.RemoveChild($r.TestRun.ResultSummary.Counters) }
)) {
    $report = New-Report
    & $change $report
    $rejected = $false
    try { Assert-CompleteEngineReport $report 2 } catch { $rejected = $true }
    if (-not $rejected) { throw 'Accepted an incomplete or inconsistent report.' }
    $passed++
}
Write-Output "Package evidence checks: $passed passed. No archives created."
