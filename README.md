# GPU Monitor

![GPU Monitor — one minute idle, five minutes of GPU load, then five minutes of cooldown](https://raw.githubusercontent.com/mannetroll/GpuMonitor/v0.1.0/docs/idle-load-cooldown-dashboard.png)

[Download the portable Windows x64 EXE](https://github.com/mannetroll/GpuMonitor/releases/download/v0.1.0/GpuMonitor.exe) · [Release v0.1.0](https://github.com/mannetroll/GpuMonitor/releases/tag/v0.1.0)

A lightweight Windows 11 desktop monitor for an NVIDIA RTX 3090 CUDA workstation. Built with .NET 10, C#, Avalonia and ScottPlot. All live readings come from hardware; no generated/demo values are displayed.

## Build and run

Prerequisites: Windows 11 x64, .NET 10 SDK (10.0.401 used here), NVIDIA graphics driver, and NuGet access for the first restore. No Unity, web view, browser, or Electron runtime. An administrator account is not required for the verified NVIDIA readings.

```powershell
cd C:\Dev\GpuMonitor
dotnet build -c Release
dotnet run -c Release --project GpuMonitor.csproj
# Or launch the built desktop application:
.\bin\Release\net10.0-windows\GpuMonitor.exe
```

Run the hardware-free checks:

```powershell
dotnet run -c Release --project Tests\GpuMonitor.Tests.csproj
```

Generate a three-sample hardware verification report (the desktop executable does not attach a console):

```powershell
Start-Process .\bin\Release\net10.0-windows\GpuMonitor.exe -ArgumentList '--probe' -Wait
Get-Content sensor-report.json
```

Startup, selected sensor names/identifiers, and read errors go to Console/Debug output. For persistent diagnostic output, launch with PowerShell `Start-Process` and `-RedirectStandardOutput app-out.log -RedirectStandardError app-error.log`. Logs are not written on every successful sample.

## Architecture

- `Models/GpuSample.cs`: immutable timestamped readings; unavailable values are nullable.
- `Services/IGpuTelemetryService.cs`: replaceable hardware interface.
- `Services/LibreHardwareMonitorTelemetryService.cs`: GPU-only computer handle, NVIDIA selection (RTX 3090 preferred), sensor discovery and error handling. Sensor types plus normalized names identify junction temperature. Core temperature is never used as a substitute.
- `Services/NvmlMemoryLimit.cs`: optional startup-only NVIDIA memory slowdown threshold lookup because LibreHardwareMonitor does not expose this limit. No NVML loop or nvidia-smi subprocess is used for sampling. Ambiguous same-name GPU matches produce N/A.
- `Infrastructure/RingBuffer.cs`: fixed 3,601 slots (15 minutes plus inclusive endpoints at the fastest 0.25-second cadence).
- `ViewModels/MainWindowViewModel.cs`: current values, threshold validation and thermal status.
- `Views/MainWindow.axaml(.cs)`: layout, persistent plot series and fixed 3,601-element chart arrays, sample dispatch and lifetime.

A single background task owns the hardware handle. The selected polling interval is 0.25, 1, or 4 seconds; read duration is deducted from the delay. Paused suspends polling. Slow reads do not overlap or cause catch-up polling. UI dispatch is awaited so updates cannot form an unbounded queue. Closing cancels delays and awaits any in-progress driver call before releasing the monitor handle. Native driver calls cannot be forcibly canceled.

Avalonia uses software rendering. ScottPlot is refreshed once per sample, not at display refresh rate; minimized windows continue sampling but skip chart updates. Plot lines and backing arrays are reused. Chart X limits continuously cover the previous 15 minutes, with now at the right edge. The GPU chart uses watts on the left axis and temperatures on the right; the system chart uses percentages. GB here means 1024 MiB (GiB), matching the RTX 3090's advertised 24 GB capacity.

## Sensor backend on this machine

LibreHardwareMonitorLib 0.9.6; Avalonia 12.1.3; ScottPlot.Avalonia 5.1.59.

Detected **NVIDIA GeForce RTX 3090**; **24576 MiB / 24 GB**. The integrated AMD adapter is ignored after discovery.

| Reading | Exact LibreHardwareMonitor sensor | Type / identifier |
|---|---|---|
| VRAM used | GPU Memory Used | SmallData /gpu-nvidia/0/smalldata/1 |
| VRAM total | GPU Memory Total | SmallData /gpu-nvidia/0/smalldata/2 |
| Memory junction | GPU Memory Junction | Temperature /gpu-nvidia/0/temperature/3 |
| GPU core | GPU Core | Temperature /gpu-nvidia/0/temperature/0 |
| Hotspot | GPU Hot Spot | Temperature /gpu-nvidia/0/temperature/2 |
| Load | GPU Core | Load /gpu-nvidia/0/load/0 |
| Power | GPU Package | Power /gpu-nvidia/0/power/0 |
| Core clock | GPU Core | Clock /gpu-nvidia/0/clock/0 |
| Memory clock | GPU Memory | Clock /gpu-nvidia/0/clock/4 |
| Fan percentage | GPU Fan 1 | Control /gpu-nvidia/0/control/1 |

Memory junction and hotspot are available without a separate temperature backend. A startup NVML query for `NVML_TEMPERATURE_THRESHOLD_MEM_MAX` returned unavailable with driver 616.92. NVIDIA's `nvidia-smi -q -d TEMPERATURE` also reported Memory Max Operating Temp N/A. Core thermal limits are deliberately not reused as memory limits.

## Temperature warning levels

Defaults: Warning **90 °C**, Hot **100 °C**, Reference **110 °C**. Dashed horizontal lines use the right temperature axis. The current-value badge changes at warning/hot/reference thresholds; there are no sounds. Warning must be below Hot, and Hot below Reference. Invalid ordering displays a validation message and is not saved. Settings persist on clean exit under `%LOCALAPPDATA%\GpuMonitor\settings.json`.

110 °C is a user-configurable reference, **not a claim about the exact hardware limit**. If NVML exposes a memory slowdown limit, it is displayed explicitly and used for the reference line and exceeded status. Warning/hot levels remain user-configurable.

## Reliability and limitations

Missing fields display N/A and chart missing values as gaps. A failed whole read produces an empty timestamped sample, not a cached successful value. Polling continues after temporary errors. GPU selection currently prefers a name containing 3090; future multi-GPU selection belongs in the service. Hot-plug requires restart. Fan displays the first available fan control percentage; memory clock is the API clock, not marketing effective data rate. NVIDIA GPU Package power may differ from another utility's board-power measurement.

History is in-memory only and starts empty. No 15-minute synthetic prefill. Minimizing preserves history. Full multi-hour leak/driver-disconnect testing has not been completed. Startup and shutdown can wait for a native hardware call; the UI thread is not used for polling. A broken driver that never returns can delay clean shutdown.

## Verification

Release build and desktop launch verified on this Windows 11 machine. 26 deterministic checks cover bounded wraparound (100,000 samples), sensor-name matching, missing values, error clearing, threshold boundaries, and driver-limit precedence. See `VERIFICATION.md` for actual readings and measured process overhead.

## Dashboard

The left chart plots CPU utilization, RAM usage, GPU fan speed and GPU load on a fixed 0-100% axis. The right chart plots GPU power (W), GPU core temperature and memory junction temperature. VRAM used/total remains a live value. The power axis starts at 0-400 W and expands for higher readings. Both charts show 15 minutes of timestamped history.

System CPU uses GetSystemTimes deltas; RAM uses GlobalMemoryStatusEx. CPU package watts and temperature are optional LibreHardwareMonitor readings. On this machine the unprivileged CPU backend returned zero for both; these invalid readings show N/A. GPU watts remain available. Fan percentage is the first available NVIDIA fan control sensor, not RPM. Disk telemetry is omitted.

**Clear** resets both histories and their scales while preserving current values and thresholds. **Update speed** offers High (0.25 seconds), Normal (1 second, default), Low (4 seconds), and Paused. LIVE includes the sample date and time. Paused retains the last values and history.

**Save PNG** exports both charts and live values through a save dialog. A 20-pixel (display-scaled) border matches the dashboard background. The image at the top shows a one-minute idle baseline, five-minute GPU load and five-minute cooldown.

## Portable single-file Windows build

Copy `artifacts/win-x64/GpuMonitor.exe` alone to another Windows x64 machine. No .NET installation is required. NVIDIA drivers must already be installed. Native/runtime files are extracted automatically to the user's temporary .NET bundle cache on first launch; user settings remain under LocalAppData. The EXE is not code-signed. Adjacent PDB debug-symbol files are not needed.

Rebuild with `dotnet publish -p:PublishProfile=Portable`. Trimming is disabled to preserve UI and hardware-library compatibility.

The standalone EXE was copied into an isolated folder, successfully probed the RTX 3090, and launched its desktop window using the bundled runtime. An RTX 5090 has not been available for verification. NVIDIA selection uses the detected hardware; if both a 3090 and 5090 are installed, the existing selection policy prefers the 3090. Missing sensors show N/A and are never replaced with core temperature. Run `GpuMonitor.exe --probe` from a writable working directory to produce sensor-report.json for the new machine.
