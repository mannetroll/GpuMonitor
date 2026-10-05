namespace GpuMonitor.Models;
public sealed record SystemSample(double? CpuPercent=null,double? RamUsedGb=null,double? RamTotalGb=null,double? DiskReadMBps=null,double? DiskWriteMBps=null,double? CpuPowerW=null,double? CpuTemperatureC=null)
{
 public double? RamPercent => RamTotalGb > 0 ? RamUsedGb/RamTotalGb*100 : null;
}
