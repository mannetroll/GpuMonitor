using System.Runtime.InteropServices;
using System.Text;
namespace GpuMonitor.Services;
// Startup-only fallback for the memory thermal limit (not exposed by LHM).
// All live measurements remain LibreHardwareMonitor readings.
internal static class NvmlMemoryLimit
{
 [DllImport("nvml.dll", CallingConvention=CallingConvention.Cdecl)] private static extern int nvmlInit_v2();
 [DllImport("nvml.dll", CallingConvention=CallingConvention.Cdecl)] private static extern int nvmlShutdown();
 [DllImport("nvml.dll", CallingConvention=CallingConvention.Cdecl)] private static extern int nvmlDeviceGetCount_v2(out uint count);
 [DllImport("nvml.dll", CallingConvention=CallingConvention.Cdecl)] private static extern int nvmlDeviceGetHandleByIndex_v2(uint index,out IntPtr device);
 [DllImport("nvml.dll", CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Ansi)] private static extern int nvmlDeviceGetName(IntPtr device,StringBuilder name,uint length);
 [DllImport("nvml.dll", CallingConvention=CallingConvention.Cdecl)] private static extern int nvmlDeviceGetTemperatureThreshold(IntPtr device,int threshold,out uint temperature);
 public static double? Read(string gpuName)
 {
  bool initialized=false;
  try
  {
   if(nvmlInit_v2()!=0) return null; initialized=true;
   if(nvmlDeviceGetCount_v2(out var count)!=0) return null;
   var matches=new List<IntPtr>();
   for(uint i=0;i<count;i++) { if(nvmlDeviceGetHandleByIndex_v2(i,out var device)!=0) continue; var name=new StringBuilder(128); if(nvmlDeviceGetName(device,name,128)==0 && name.ToString()==gpuName) matches.Add(device); }
   // Do not misattribute a limit when multiple identically named adapters exist.
   return matches.Count==1 && nvmlDeviceGetTemperatureThreshold(matches[0],2,out var t)==0 && t is >0 and <200 ? t : null;
  }
  catch(Exception e) when(e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { System.Diagnostics.Debug.WriteLine(e.Message); return null; }
  finally { if(initialized) nvmlShutdown(); }
 }
}
