using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GpuMonitor.Infrastructure;
using GpuMonitor.Models;
using GpuMonitor.Services;
using GpuMonitor.ViewModels;
using ScottPlot;
using System.Text.Json;
namespace GpuMonitor.Views;
public partial class MainWindow : Window
{
 private readonly MainWindowViewModel vm = new();
 private readonly RingBuffer<GpuSample> history = new(901);
 private readonly double[] times = new double[901], memory = new double[901], temperature = new double[901];
 private readonly RingBuffer<SystemSample> systemHistory=new(901);
 private readonly double[] cpuValues=new double[901],ramValues=new double[901],diskValues=new double[901],fanValues=new double[901];
 private ScottPlot.Avalonia.AvaPlot systemChart=null!;
 private readonly CancellationTokenSource cancellation = new();
 private readonly ScottPlot.Avalonia.AvaPlot chart;
 private readonly ScottPlot.Plottables.HorizontalLine warning, hot, reference;
 private Task? sampling;
 private bool closing, allowClose;
 private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GpuMonitor","settings.json");
 public MainWindow()
 {
  AvaloniaXamlLoader.Load(this); DataContext = vm;
  chart = this.FindControl<ScottPlot.Avalonia.AvaPlot>("Chart")!;
  LoadSettings();
  systemChart=this.FindControl<ScottPlot.Avalonia.AvaPlot>("SystemChart")!;
  SetupSystemChart();
  var plot = chart.Plot;
  Array.Fill(memory, double.NaN); Array.Fill(temperature, double.NaN);
  for (int i=0;i<times.Length;i++) times[i] = DateTime.Now.AddSeconds(i-900).ToOADate();
  var vram = plot.Add.Scatter(times,memory); vram.LegendText="Dedicated VRAM (GB)"; vram.Color=Color.FromHex("6CAEFF"); vram.LineWidth=2; vram.MarkerSize=0;
  var junction = plot.Add.Scatter(times,temperature); junction.LegendText="Memory junction (°C)"; junction.Color=Color.FromHex("58D6B0"); junction.LineWidth=2; junction.MarkerSize=0; junction.Axes.YAxis=plot.Axes.Right;
  warning = AddLevel((double)vm.Warning,"FFC66D"); hot = AddLevel((double)vm.Hot,"FF9972"); reference = AddLevel((double)vm.Reference,"FF727F");
  var timeAxis = plot.Axes.DateTimeTicksBottom(); ((ScottPlot.TickGenerators.DateTimeAutomatic)timeAxis.TickGenerator).LabelFormatter = dt => dt.ToString("HH:mm");
  plot.Axes.Left.Label.Text="Dedicated VRAM · GB"; plot.Axes.Right.Label.Text="Memory junction · °C";
  plot.FigureBackground.Color=Color.FromHex("192332"); plot.DataBackground.Color=Color.FromHex("192332");
  plot.Axes.Color(Color.FromHex("A5B4C9")); plot.Grid.MajorLineColor=Color.FromHex("293548");
  plot.ShowLegend(Alignment.UpperLeft);
  chart.UserInputProcessor.Disable();
  UpdateAxes(DateTimeOffset.Now,24);
  Opened += (_,_) => sampling = Task.Run(SampleLoop);
  Closing += OnClosing;
 }
 private ScottPlot.Plottables.HorizontalLine AddLevel(double value,string color)
 { var line=chart.Plot.Add.HorizontalLine(value); line.Color=Color.FromHex(color); line.LineWidth=1; line.LinePattern=LinePattern.Dashed; line.Axes.YAxis=chart.Plot.Axes.Right; return line; }
 private void UpdateAxes(DateTimeOffset now,double total)
 {
  chart.Plot.Axes.SetLimitsX(now.LocalDateTime.AddMinutes(-15).ToOADate(),now.LocalDateTime.ToOADate());
  chart.Plot.Axes.SetLimitsY(0,Math.Max(24,total));
  chart.Plot.Axes.SetLimitsY(0,Math.Max(120,(double)vm.Reference+10),chart.Plot.Axes.Right);
 }
 private async Task SampleLoop()
 {
  using var systemService=new SystemTelemetryService();
  using IGpuTelemetryService service = new LibreHardwareMonitorTelemetryService();
  try
  {
   while (!cancellation.IsCancellationRequested)
   {
    var started = System.Diagnostics.Stopwatch.GetTimestamp();
    var sample = service.Read(); var system=systemService.Read();
    if (cancellation.IsCancellationRequested) break;
    // Await dispatch: at most one update queued even when the UI is busy.
    try { await Dispatcher.UIThread.InvokeAsync(() => Apply(sample,service.MemorySensorDescription,system)); }
    catch (Exception e) { Console.Error.WriteLine(e); System.Diagnostics.Debug.WriteLine(e); }
    var remaining = TimeSpan.FromSeconds(1) - System.Diagnostics.Stopwatch.GetElapsedTime(started);
    if (remaining > TimeSpan.Zero) await Task.Delay(remaining,cancellation.Token);
   }
  }
  catch (OperationCanceledException) { }
 }
 private void Apply(GpuSample sample,string sensor,SystemSample system)
 {
  if (closing) return;
  history.Add(sample); systemHistory.Add(system); vm.Apply(sample,sensor); vm.ApplySystem(system);
  if (WindowState == WindowState.Minimized) return;
  var offset=times.Length-history.Count;
  for (int i=0;i<offset;i++) { times[i]=history[0].Timestamp.LocalDateTime.AddSeconds(i-offset).ToOADate(); memory[i]=temperature[i]=cpuValues[i]=ramValues[i]=diskValues[i]=fanValues[i]=double.NaN; }
  for (int i=0;i<history.Count;i++) { var s=history[i]; var j=offset+i; times[j]=s.Timestamp.LocalDateTime.ToOADate(); memory[j]=s.MemoryUsedGb??double.NaN; temperature[j]=s.MemoryJunctionC??double.NaN; fanValues[j]=s.FanPercent??double.NaN; var sys=systemHistory[i]; cpuValues[j]=sys.CpuPercent??double.NaN; ramValues[j]=sys.RamPercent??double.NaN; diskValues[j]=(sys.DiskReadMBps+sys.DiskWriteMBps)??double.NaN; }
  if (vm.ValidThresholds) { warning.Y=(double)vm.Warning; hot.Y=(double)vm.Hot; reference.Y=sample.MemoryThermalLimitC ?? (double)vm.Reference; }
  UpdateAxes(sample.Timestamp,sample.MemoryTotalGb??24); chart.Refresh();
  systemChart.Plot.Axes.SetLimitsX(sample.Timestamp.LocalDateTime.AddMinutes(-15).ToOADate(),sample.Timestamp.LocalDateTime.ToOADate());
  systemChart.Plot.Axes.SetLimitsY(0,100);
  double maxDisk=10; foreach(var v in diskValues) if(double.IsFinite(v)) maxDisk=Math.Max(maxDisk,v*1.1);
  systemChart.Plot.Axes.SetLimitsY(0,maxDisk,systemChart.Plot.Axes.Right); systemChart.Refresh();
 }
 private async void OnClosing(object? sender,WindowClosingEventArgs e)
 {
  if (allowClose) return;
  e.Cancel=true;
  if (closing) return;
  closing=true; cancellation.Cancel();
  try { if (sampling != null) await sampling; }
  finally { SaveSettings(); cancellation.Dispose(); allowClose=true; Close(); }
 }
 private void SetupSystemChart()
 {
  Array.Fill(cpuValues,double.NaN);Array.Fill(ramValues,double.NaN);Array.Fill(diskValues,double.NaN);Array.Fill(fanValues,double.NaN);
  var p=systemChart.Plot;
  var c=p.Add.Scatter(times,cpuValues);c.LegendText="CPU %";c.Color=Color.FromHex("C49BFF");c.MarkerSize=0;c.LineWidth=2;
  var r=p.Add.Scatter(times,ramValues);r.LegendText="RAM %";r.Color=Color.FromHex("6CAEFF");r.MarkerSize=0;r.LineWidth=2;
  var d=p.Add.Scatter(times,diskValues);d.LegendText="Disk MB/s";d.Color=Color.FromHex("FFC66D");d.MarkerSize=0;d.LineWidth=1.5f;d.Axes.YAxis=p.Axes.Right;
  var fan=p.Add.Scatter(times,fanValues);fan.LegendText="GPU fan %";fan.Color=Color.FromHex("FF83BE");fan.MarkerSize=0;fan.LineWidth=2;
  var axis=p.Axes.DateTimeTicksBottom();((ScottPlot.TickGenerators.DateTimeAutomatic)axis.TickGenerator).LabelFormatter=dt=>dt.ToString("HH:mm");
  p.Axes.Left.Label.Text="CPU / RAM / fan · %";p.Axes.Right.Label.Text="Disk read + write · MB/s";
  p.FigureBackground.Color=Color.FromHex("192332");p.DataBackground.Color=Color.FromHex("192332");p.Axes.Color(Color.FromHex("A5B4C9"));p.Grid.MajorLineColor=Color.FromHex("293548");p.ShowLegend(Alignment.UpperLeft);systemChart.UserInputProcessor.Disable();
 }
 private void LoadSettings()
 { try { if (File.Exists(settingsPath)) { var a=JsonSerializer.Deserialize<decimal[]>(File.ReadAllText(settingsPath)); if(a is {Length:3} && a[0]>=20 && a[0]<a[1] && a[1]<a[2] && a[2]<=150) { vm.Warning=a[0];vm.Hot=a[1];vm.Reference=a[2]; } } } catch(Exception e) { System.Diagnostics.Debug.WriteLine(e.Message); } }
 private void SaveSettings()
 { try { if(vm.ValidThresholds) { Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!); File.WriteAllText(settingsPath,JsonSerializer.Serialize(new[]{vm.Warning,vm.Hot,vm.Reference})); } } catch(Exception e) { System.Diagnostics.Debug.WriteLine(e.Message); } }
}




