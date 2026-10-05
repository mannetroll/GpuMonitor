# Verification — 2026-10-05

## Hardware

- NVIDIA GeForce RTX 3090 selected over the integrated AMD Radeon adapter.
- Total VRAM: 24576 MiB = 24 GB (binary units).
- Memory junction: `GPU Memory Junction`, Temperature, `/gpu-nvidia/0/temperature/3`; observed 42–48 °C during development.
- Core: `GPU Core`, Temperature, `/gpu-nvidia/0/temperature/0`; observed 34–38 °C.
- Hotspot: `GPU Hot Spot`, Temperature, `/gpu-nvidia/0/temperature/2`; available, about 44–49 °C.
- Power: `GPU Package`, Power, `/gpu-nvidia/0/power/0`; available, about 18–27 W.
- VRAM used, load, clocks and fan percentage all returned real values. Samples varied over time.
- Startup NVML memory slowdown limit returned N/A; nvidia-smi independently returned Memory Max Operating Temp N/A. No core limit or core temperature substituted.

## Application

- Release build: 0 warnings, 0 errors.
- 15 deterministic checks passed, including 100,000-sample ring wraparound and null/error handling.
- Actual desktop window inspected: dual Y axes, a 30-minute scrolling X axis, two distinguishable live lines, legend, dashed thresholds, live values, and sensor identifier.
- UI threshold controls tested against the actual measured temperature: lowering Warning to 40 °C produced WARNING; lowering Hot to 42 °C produced HOT. Restored 90 / 100 / 110 °C afterward.
- Clean close/relaunch exercised during development; sampling task canceled and hardware handle released.
- Software renderer selected explicitly; no continuous frame-animation loop. No synthetic history.

## Process overhead

Visible Release application, Ryzen 7 7800X3D / 16 logical processors, no intentional GPU workload launched by this task. Other desktop programs were active. These are process CPU values, not whole-system GPU load.

- Initial 40-second observation: 0.44% total machine CPU (7.0% of one logical core), 171 MB working set, 98 MB private bytes.
- Final 30-second observation: 0.31% total machine CPU, 180 MB working set, 104 MB private bytes.
- Working set includes .NET, Avalonia, ScottPlot and native libraries. Short observations include warm-up/GC variation and do not establish long-run memory stability. Ring and chart arrays stay bounded; a multi-hour soak has not been performed.

Hardware snapshots can be regenerated using `--probe`. Performance observations are approximate and vary with window size, desktop capture, driver and other applications.
