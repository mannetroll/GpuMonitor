using GpuMonitor.Models;
namespace GpuMonitor.Services;
public interface IGpuTelemetryService : IDisposable
{
 string GpuName { get; }
 string MemorySensorDescription { get; }
 GpuSample Read();
}
