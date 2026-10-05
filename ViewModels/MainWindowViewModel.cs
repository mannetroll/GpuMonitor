using System.ComponentModel;
using System.Runtime.CompilerServices;
using GpuMonitor.Models;
namespace GpuMonitor.ViewModels;
public sealed class MainWindowViewModel : INotifyPropertyChanged
{
 public event PropertyChangedEventHandler? PropertyChanged;
 public string Cpu { get; private set; } = "N/A";
 public string Ram { get; private set; } = "N/A";
 public string CpuPower { get; private set; } = "N/A";
 public string CpuTemperature { get; private set; } = "N/A";
 public void ApplySystem(SystemSample s) { Cpu=F(s.CpuPercent," %"); Ram=$"{F(s.RamUsedGb,"","0.0")} / {F(s.RamTotalGb," GB","0.0")}"; CpuPower=F(s.CpuPowerW," W"); CpuTemperature=F(s.CpuTemperatureC," °C"); Changed(string.Empty); }
 public string GpuName { get; private set; } = "Detecting NVIDIA GPU…";
 public string Vram { get; private set; } = "N/A";
 public string Junction { get; private set; } = "N/A";
 public string Core { get; private set; } = "N/A";
 public string Hotspot { get; private set; } = "N/A";
 public string Load { get; private set; } = "N/A";
 public string Power { get; private set; } = "N/A";
 public string Clocks { get; private set; } = "N/A";
 public string Fan { get; private set; } = "N/A";
 private string liveStatus = "Starting telemetry...";
 private int updateSpeedIndex = 1;
 public string[] UpdateSpeeds { get; } = ["High", "Normal", "Low", "Paused"];
 public int UpdateSpeedIndex
 {
  get => Volatile.Read(ref updateSpeedIndex);
  set { if (value < 0 || value > 3 || value == UpdateSpeedIndex) return; Volatile.Write(ref updateSpeedIndex,value); Changed(); Changed(nameof(Status)); UpdateSpeedChanged?.Invoke(); }
 }
 public event Action? UpdateSpeedChanged;
 public int UpdateIntervalMs => UpdateSpeedIndex switch { 0 => 250, 1 => 1000, 2 => 4000, _ => Timeout.Infinite };
 public string Status => UpdateSpeedIndex == 3 ? "PAUSED | Last sample: " + liveStatus : liveStatus + $" | {UpdateSpeeds[UpdateSpeedIndex]} | {UpdateIntervalMs / 1000d:0.##} s interval";
 public string LimitDescription { get; private set; } = "Driver memory limit: N/A · Reference is configurable, not a verified hardware limit.";
 public string Sensor { get; private set; } = "LibreHardwareMonitor / NVIDIA";
 public string WarningColor { get; private set; } = "#58D6B0";
 public string ThermalState { get; private set; } = "WAITING";
 private decimal warning = 90, hot = 100, reference = 110;
 public decimal Warning { get => warning; set { warning = value; Changed(); } }
 public decimal Hot { get => hot; set { hot = value; Changed(); } }
 public decimal Reference { get => reference; set { reference = value; Changed(); } }
 public bool ValidThresholds => Warning < Hot && Hot < Reference;
 private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
 private static string F(double? v, string suffix, string format = "0") => v.HasValue ? v.Value.ToString(format) + suffix : "N/A";
 public void Apply(GpuSample s, string sensor)
 {
  GpuName = s.GpuName; Vram = $"{F(s.MemoryUsedGb, "", "0.0")} / {F(s.MemoryTotalGb, " GB", "0.0")}";
  Junction = F(s.MemoryJunctionC," °C"); Core = F(s.CoreC," °C"); Hotspot = F(s.HotspotC," °C");
  Load = F(s.LoadPercent," %"); Power = F(s.PowerW," W"); Fan = F(s.FanPercent," %");
  Clocks = $"Core {F(s.CoreClockMhz," MHz")}   /   Memory {F(s.MemoryClockMhz," MHz")}";
  ThermalState = s.MemoryJunctionC is not double t ? "UNAVAILABLE" : t >= (s.MemoryThermalLimitC ?? (double)Reference) ? "REFERENCE EXCEEDED" : t >= (double)Hot ? "HOT" : t >= (double)Warning ? "WARNING" : "NORMAL";
  WarningColor = ThermalState == "NORMAL" ? "#58D6B0" : ThermalState == "WARNING" ? "#FFC66D" : ThermalState == "UNAVAILABLE" ? "#94A3B8" : "#FF727F";
  liveStatus = !ValidThresholds ? "Set Warning < Hot < Reference." : s.Error != null ? $"Read failed · {s.Error}" : $"LIVE · {s.Timestamp:yyyy-MM-dd HH:mm:ss} · sample history · 15-minute rolling history";
  Sensor = sensor; LimitDescription = s.MemoryThermalLimitC is double limit ? $"Driver memory slowdown limit: {limit:0} °C (NVML) · Reference line follows this limit." : "Driver memory limit: N/A · Reference is configurable, not a verified hardware limit.";
  Changed(string.Empty);
 }
}





