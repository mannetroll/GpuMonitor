namespace GpuMonitor.Models;
public sealed record GpuSample(DateTimeOffset Timestamp, string GpuName, double? MemoryUsedGb = null,
 double? MemoryTotalGb = null, double? MemoryJunctionC = null, double? CoreC = null,
 double? HotspotC = null, double? LoadPercent = null, double? PowerW = null,
 double? CoreClockMhz = null, double? MemoryClockMhz = null, double? FanPercent = null,
 string? Error = null, double? MemoryThermalLimitC = null);

