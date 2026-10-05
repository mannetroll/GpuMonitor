using System.Diagnostics;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using GpuMonitor.Models;
namespace GpuMonitor.Services;
public sealed class SystemTelemetryService : IDisposable
{
 [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
 [StructLayout(LayoutKind.Sequential)] struct MemoryStatus { public uint Length,Load; public ulong Total,Available,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,Extended; }
 [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
 private long idle,kernel,user; private bool primed;
 private readonly Computer computer=new(){IsCpuEnabled=true};
 private IHardware? cpu;
 public SystemTelemetryService()
 {
  try {computer.Open();cpu=computer.Hardware.FirstOrDefault(h=>h.HardwareType==HardwareType.Cpu);Console.WriteLine("CPU: "+cpu?.Name);} catch(Exception e){Console.WriteLine("CPU sensors: "+e.Message);}
 }
 public SystemSample Read()
 {
  double? load=null,used=null,total=null,power=null,temp=null;
  if(GetSystemTimes(out var i,out var k,out var u)) {long delta=k-kernel+u-user;if(primed&&delta>0)load=Math.Clamp(100d*(delta-(i-idle))/delta,0,100);idle=i;kernel=k;user=u;primed=true;}
  var m=new MemoryStatus{Length=(uint)Marshal.SizeOf<MemoryStatus>()};
  if(GlobalMemoryStatusEx(ref m)){total=m.Total/1073741824d;used=(m.Total-m.Available)/1073741824d;}
  try {cpu?.Update();if(cpu!=null)foreach(var s in cpu.Sensors){if(s.Value is not float v || !float.IsFinite(v) || v <= 0)continue;var n=LibreHardwareMonitorTelemetryService.Normalize(s.Name);if(s.SensorType==SensorType.Power&&n.Contains("package"))power=v;if(s.SensorType==SensorType.Temperature&&(n.Contains("tctltdie")||n.Contains("package")))temp=v;}}catch(Exception e){Debug.WriteLine(e.Message);}
  return new(load,used,total,power,temp);
 }
 public void Dispose(){try{computer.Close();}catch(Exception e){Debug.WriteLine(e.Message);}}
}


