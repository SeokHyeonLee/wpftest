param([string]$ResultDirectory = "$PSScriptRoot/../../docs/performance/2026-09-07")
$ErrorActionPreference = 'Stop'
$culture = [Globalization.CultureInfo]::InvariantCulture
function Format-Number([double]$Value) { $Value.ToString('F3', $culture) }
function Get-Median($Values) {
    $sorted = @($Values | Sort-Object)
    ($sorted[[int][Math]::Floor(($sorted.Count - 1) / 2)] + $sorted[[int][Math]::Floor($sorted.Count / 2)]) / 2
}
$rows = @(Import-Csv -LiteralPath (Join-Path $ResultDirectory 'raw.csv'))
$environment = Get-Content -LiteralPath (Join-Path $ResultDirectory 'environment.json') -Raw | ConvertFrom-Json
if ($rows.Count -ne 13 * $environment.rounds) { throw 'Incomplete benchmark: expected 13 scenarios per round.' }
$groups = $rows | Group-Object kind, count, state | Sort-Object Name
if (@($groups).Count -ne 13) { throw 'Unexpected scenario count.' }
$summary = foreach ($group in $groups) {
    if ($group.Count -ne $environment.rounds -or @($group.Group.round | Sort-Object -Unique).Count -ne $environment.rounds) {
        throw "Missing or duplicate rounds in $($group.Name)"
    }
    $first = $group.Group[0]
    $result = [ordered]@{ kind = $first.kind; count = $first.count; state = $first.state; runs = $group.Count }
    foreach ($metric in 'one_core_pct', 'machine_pct', 'private_median_mib', 'working_median_mib', 'managed_after_mib', 'ui_alloc_kib_sec', 'gc0', 'gc1', 'gc2') {
        $values = @($group.Group | ForEach-Object { [double]::Parse($_.$metric, $culture) })
        $result["${metric}_median"] = Format-Number (Get-Median $values)
        $result["${metric}_min"] = Format-Number (($values | Measure-Object -Minimum).Minimum)
        $result["${metric}_max"] = Format-Number (($values | Measure-Object -Maximum).Maximum)
    }
    [pscustomobject]$result
}
$summary | Export-Csv -LiteralPath (Join-Path $ResultDirectory 'summary.csv') -NoTypeInformation -Encoding UTF8
$summary | Select-Object kind, count, state, runs, machine_pct_median, private_median_mib_median, working_median_mib_median, managed_after_mib_median, ui_alloc_kib_sec_median | Format-Table -AutoSize
