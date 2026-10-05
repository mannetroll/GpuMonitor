using LibreHardwareMonitor.Hardware;
using GpuMonitor.Models;
using System.Diagnostics;
namespace GpuMonitor.Services;
public sealed class LibreHardwareMonitorTelemetryService : IGpuTelemetryService
{
 private readonly Computer computer = new() { IsGpuEnabled = true };
 private IHardware? gpu;
 private readonly Dictionary<string, ISensor> selected = new();
 private bool opened; private double? memoryLimit;
 public string GpuName => gpu?.Name ?? "No NVIDIA GPU detected";
 public string MemorySensorDescription => selected.TryGetValue("junction", out var s) ? $"{s.Name} · {s.Identifier}" : "Memory junction sensor unavailable";
 public static string Normalize(string name) => new(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
 public static bool IsMemoryJunction(string name) { var n = Normalize(name); return n.Contains("memory") && (n.Contains("junction") || n.Contains("hotspot")); }
 private void Open()
 {
  computer.Open(); opened = true;
  gpu = computer.Hardware.Where(h => h.HardwareType == HardwareType.GpuNvidia)
   .OrderByDescending(h => h.Name.Contains("3090", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
  Log($"Selected: {GpuName}"); if (gpu != null) memoryLimit = NvmlMemoryLimit.Read(GpuName); Log($"NVML memory slowdown limit: {memoryLimit?.ToString() ?? "N/A"}");
 }
 private static void Log(string text) { Console.WriteLine(text); Debug.WriteLine(text); }
 private void Discover()
 {
  foreach (var s in gpu!.Sensors)
  {
   var n = Normalize(s.Name);
   string? key = s.SensorType switch
   {
    SensorType.Temperature when IsMemoryJunction(s.Name) => "junction",
    SensorType.Temperature when n.Contains("hotspot") => "hotspot",
    SensorType.Temperature when n == "gpucore" => "core",
    SensorType.SmallData when n == "gpumemoryused" => "used",
    SensorType.SmallData when n == "gpumemorytotal" => "total",
    SensorType.Load when n == "gpucore" => "load",
    SensorType.Power when n is "gpupackage" or "gpuboardpower" => "power",
    SensorType.Clock when n == "gpucore" => "clock",
    SensorType.Clock when n == "gpumemory" => "memoryclock",
    SensorType.Control when n.Contains("fan") => "fan",
    _ => null
   };
   if (key != null && !selected.ContainsKey(key)) { selected[key] = s; Log($"Sensor {key}: {s.Name} | {s.SensorType} | {s.Identifier} | {s.Value}"); }
  }
 }
 private double? Value(string key, double divisor = 1) => selected.TryGetValue(key, out var s) && s.Value is float v && float.IsFinite(v) && v >= 0 ? v / divisor : null;
 public GpuSample Read()
 {
  try
  {
   if (!opened) Open();
   if (gpu == null) return new(DateTimeOffset.Now, GpuName, Error: "No NVIDIA GPU detected. Restart after connecting the GPU.");
   gpu.Update(); if (selected.Count < 10) Discover();
   return new(DateTimeOffset.Now, GpuName, Value("used", 1024), Value("total", 1024), Value("junction"), Value("core"), Value("hotspot"), Value("load"), Value("power"), Value("clock"), Value("memoryclock"), Value("fan"), MemoryThermalLimitC: memoryLimit);
  }
  catch (Exception e) { Log($"Telemetry error: {e.Message}"); return new(DateTimeOffset.Now, GpuName, Error: e.Message); }
 }
 public void Dispose() { try { computer.Close(); } catch (Exception e) { Log($"Close: {e.Message}"); } finally { opened = false; } }
}



