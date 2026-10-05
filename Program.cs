using Avalonia;
using GpuMonitor.Services;
using System.Text.Json;
namespace GpuMonitor;
internal static class Program
{
 [STAThread] public static void Main(string[] args)
 {
  if (args.Contains("--probe"))
  {
   using var service = new LibreHardwareMonitorTelemetryService();
   var samples = new List<Models.GpuSample>();
   for (int i=0;i<3;i++) { samples.Add(service.Read()); Thread.Sleep(1000); }
   File.WriteAllText("sensor-report.json", JsonSerializer.Serialize(new { service.MemorySensorDescription, Samples = samples },new JsonSerializerOptions { WriteIndented=true }));
   return;
  }
  BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
 }
 public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect()
  .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] }).LogToTrace();
}
