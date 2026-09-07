param(
    [string]$OutputDirectory = "$PSScriptRoot/../../docs/performance/2026-09-07",
    [ValidateRange(1, 10)][int]$Rounds = 3
)
$ErrorActionPreference = 'Stop'
$executable = Join-Path $PSScriptRoot 'bin/Release/SpinnerPerf.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build SpinnerPerf.csproj in Release first.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$environmentPath = Join-Path $OutputDirectory 'environment.json'
[ordered]@{
    started_at = (Get-Date).ToString('o')
    source_commit = (git -C "$PSScriptRoot/../.." rev-parse HEAD)
    source_status = @(git -C "$PSScriptRoot/../.." status --short)
    cpu = @(Get-CimInstance Win32_Processor | Select-Object Name, NumberOfCores, NumberOfLogicalProcessors)
    os = Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, BuildNumber, TotalVisibleMemorySize
    power_plan = @(powercfg /getactivescheme)
    warmup_seconds = 3
    measurement_seconds = 6
    rounds = $Rounds
    order_seed = 20260907
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $environmentPath -Encoding UTF8
$csvPath = Join-Path $OutputDirectory 'raw.csv'
'round,kind,count,state,wall_ms,cpu_ms,one_core_pct,machine_pct,private_median_mib,private_max_mib,working_median_mib,working_max_mib,managed_before_mib,managed_after_mib,ui_alloc_kib_sec,gc0,gc1,gc2,render_tier,logical_cpus,is_64bit,dpi,clr' | Set-Content -LiteralPath $csvPath -Encoding UTF8
$cases = @(@{ Kind = 0; Count = 0; State = 'stopped' })
foreach ($kind in 1..3) {
    foreach ($count in 1, 64) {
        foreach ($state in 'active', 'stopped') { $cases += @{ Kind = $kind; Count = $count; State = $state } }
    }
}
$random = [Random]::new(20260907)
for ($round = 1; $round -le $Rounds; $round++) {
    $ordered = @($cases)
    for ($i = $ordered.Length - 1; $i -gt 0; $i--) {
        $j = $random.Next($i + 1)
        $temporary = $ordered[$i]; $ordered[$i] = $ordered[$j]; $ordered[$j] = $temporary
    }
    foreach ($case in $ordered) {
        Write-Host "Round $round/$Rounds : spinner=$($case.Kind) count=$($case.Count) state=$($case.State)"
        $row = & $executable $case.Kind $case.Count $case.State
        if ($LASTEXITCODE -ne 0) { throw "Benchmark failed: $LASTEXITCODE" }
        "$round,$row" | Add-Content -LiteralPath $csvPath -Encoding UTF8
        Write-Host $row
    }
}
Write-Host "Results: $csvPath"
