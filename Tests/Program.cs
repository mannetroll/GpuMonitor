using GpuMonitor.Infrastructure;
using GpuMonitor.Models;
using GpuMonitor.Services;
using GpuMonitor.ViewModels;
int checks=0;
void Check(bool condition,string name) { if(!condition) throw new Exception(name); checks++; Console.WriteLine("PASS "+name); }
var ring=new RingBuffer<int>(901);
for(int i=0;i<100_000;i++) ring.Add(i);
Check(ring.Count==901,"History bounded after 100,000 samples");
Check(ring[0]==99_099 && ring[900]==99_999,"Wraparound maintains chronological order");
bool rejected=false;try{_ = ring[901];}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"Out-of-range access rejected");
Check(LibreHardwareMonitorTelemetryService.IsMemoryJunction("GPU Memory Junction"),"Exact memory sensor matches");
Check(LibreHardwareMonitorTelemetryService.IsMemoryJunction("memory_junction temperature"),"Variant name matches");
Check(!LibreHardwareMonitorTelemetryService.IsMemoryJunction("GPU Core"),"Core never substitutes for junction");
Check(!LibreHardwareMonitorTelemetryService.IsMemoryJunction("GPU Hot Spot"),"Hotspot never substitutes for junction");
var vm=new MainWindowViewModel();
vm.Apply(new(DateTimeOffset.Now,"RTX 3090"),"missing");
Check(vm.Junction=="N/A" && vm.Power=="N/A" && vm.ThermalState=="UNAVAILABLE","Missing sensors render N/A");
foreach(var (temperature,state) in new[]{(89d,"NORMAL"),(90d,"WARNING"),(100d,"HOT"),(110d,"REFERENCE EXCEEDED")}) {vm.Apply(new(DateTimeOffset.Now,"RTX 3090",MemoryJunctionC:temperature),"test"); Check(vm.ThermalState==state,$"Boundary {temperature}");}
vm.Apply(new(DateTimeOffset.Now,"RTX 3090",MemoryJunctionC:106,MemoryThermalLimitC:105),"test");Check(vm.ThermalState=="REFERENCE EXCEEDED","Driver memory limit preferred");
vm.Warning=105;Check(!vm.ValidThresholds,"Invalid threshold order rejected");
vm.Apply(new(DateTimeOffset.Now,"RTX 3090",Error:"temporary error"),"test");Check(vm.Junction=="N/A","Failed read clears stale measurements");
Console.WriteLine($"{checks} checks passed.");

Check(new SystemSample(RamUsedGb:32,RamTotalGb:64).RamPercent==50,"RAM percentage");
ring.Clear();
Check(ring.Count==0 && ring.Capacity==901,"Clear removes history without changing capacity");
ring.Add(7);ring.Add(8);
Check(ring.Count==2 && ring[0]==7 && ring[1]==8,"Sampling resumes in order after clear");
