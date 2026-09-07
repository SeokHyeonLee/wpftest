# Spinner CPU and memory benchmark

Build and run from the repository root in Visual Studio Developer PowerShell:

```powershell
msbuild .\tools\SpinnerPerf\SpinnerPerf.csproj /t:Build /p:Configuration=Release /m
.\tools\SpinnerPerf\run.ps1 -OutputDirectory .\artifacts\spinner-perf-new
.\tools\SpinnerPerf\summarize.ps1 -ResultDirectory .\artifacts\spinner-perf-new
```

The runner opens an automatic WPF window. Leave it visible and avoid minimizing,
covering, resizing, or interacting with it during measurement. Runs take about six
minutes. Use a new output directory to preserve previously captured results.

Each scenario gets a fresh process. Three rounds shuffle 13 scenarios with a fixed
seed: an empty window, and each of the three spinner types at 1 and 64 instances,
active and stopped. Every instance is 32 DIP in a fixed 8-by-8 grid in a 420-by-450
DIP window. Stopped controls first run for two seconds, then stop for one second;
active controls run for all three warmup seconds. Stopped controls override
Visibility to Visible so the same dots remain on screen without animation clocks.

After warmup, a full GC is performed outside the measured interval. Each scenario
measures at least six seconds of actual elapsed time. A 250 ms dispatcher timer
samples process memory; it does not drive the spinner animations. Clock state and
loaded, visible 32-DIP layout are checked before and after measurement.

`raw.csv` contains all runs, `environment.json` records machine and source context,
and `summary.csv` contains median/minimum/maximum across rounds. Kind 0 denotes the
empty window; kinds 1, 2, and 3 are LoadingSpinner, LoadingSpinner2, and LoadingSpinner3.

| Metric | Meaning |
| --- | --- |
| `one_core_pct` | Process CPU time / elapsed time * 100; 100% equals one logical core |
| `machine_pct` | `one_core_pct` / logical CPU count |
| `private_median_mib`, `private_max_mib` | Median and maximum sampled process Private Bytes |
| `working_median_mib`, `working_max_mib` | Median and maximum sampled process Working Set, including shared pages |
| `managed_before_mib`, `managed_after_mib` | GC.GetTotalMemory after forced GC outside the timed interval |
| `ui_alloc_kib_sec` | UI-thread allocated bytes / elapsed time, including measurement overhead |
| `gc0`, `gc1`, `gc2` | GC collection count differences within the measured interval |

Memory is the whole benchmark process, including WPF, CLR and instrumentation,
not the exclusive memory of one control. The memory samples use Process.Refresh,
which itself allocates. The empty and stopped scenarios quantify that overhead.
Managed before/after differences include diagnostic caches and sample storage and
are not a leak test. Forced collections are excluded from reported GC counts.

This is a short comparison on one machine, not a statistical confidence interval,
long-term leak test, startup benchmark, GPU-memory measurement, or FPS measurement.
WPF visibility and clock checks cannot detect occlusion by another application's
window. Process CPU excludes DWM and other processes, and hardware acceleration
can shift work to the GPU.
