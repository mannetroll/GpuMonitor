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
 private readonly RingBuffer<GpuSample> history = new(3601);
 private readonly double[] times = new double[3601], power = new double[3601], coreTemperature = new double[3601], temperature = new double[3601];
 private readonly RingBuffer<SystemSample> systemHistory=new(3601);
 private readonly double[] cpuValues=new double[3601],ramValues=new double[3601],fanValues=new double[3601],loadValues=new double[3601];
 private ScottPlot.Avalonia.AvaPlot systemChart=null!;
 private readonly CancellationTokenSource cancellation = new();
 private readonly ScottPlot.Avalonia.AvaPlot chart;
 private readonly ScottPlot.Plottables.HorizontalLine warning, hot, reference;
 private readonly SemaphoreSlim speedChanged = new(0,1);
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
  Array.Fill(power, double.NaN); Array.Fill(coreTemperature, double.NaN); Array.Fill(temperature, double.NaN);
  for (int i=0;i<times.Length;i++) times[i] = DateTime.Now.AddSeconds((i-3600)*0.25).ToOADate();
  var watts = plot.Add.Scatter(times,power); watts.LegendText="GPU power (W)"; watts.Color=Color.FromHex("6CAEFF"); watts.LineWidth=2; watts.MarkerSize=0;
  var core = plot.Add.Scatter(times,coreTemperature); core.LegendText="GPU core (°C)"; core.Color=Color.FromHex("C49BFF"); core.LineWidth=2; core.MarkerSize=0; core.Axes.YAxis=plot.Axes.Right;
  var junction = plot.Add.Scatter(times,temperature); junction.LegendText="Memory junction (°C)"; junction.Color=Color.FromHex("58D6B0"); junction.LineWidth=2; junction.MarkerSize=0; junction.Axes.YAxis=plot.Axes.Right;
  warning = AddLevel((double)vm.Warning,"FFC66D"); hot = AddLevel((double)vm.Hot,"FF9972"); reference = AddLevel((double)vm.Reference,"FF727F");
  var timeAxis = plot.Axes.DateTimeTicksBottom(); ((ScottPlot.TickGenerators.DateTimeAutomatic)timeAxis.TickGenerator).LabelFormatter = dt => dt.ToString("HH:mm");
  plot.Axes.Left.Label.Text="GPU power · W"; plot.Axes.Right.Label.Text="Core / memory junction · °C";
  plot.FigureBackground.Color=Color.FromHex("192332"); plot.DataBackground.Color=Color.FromHex("192332");
  plot.Axes.Color(Color.FromHex("A5B4C9")); plot.Grid.MajorLineColor=Color.FromHex("293548");
  plot.ShowLegend(Alignment.UpperLeft);
  chart.UserInputProcessor.Disable();
  UpdateAxes(DateTimeOffset.Now);
  vm.UpdateSpeedChanged += OnUpdateSpeedChanged;
  Opened += (_,_) => sampling = Task.Run(SampleLoop);
  Closing += OnClosing;
 }
 private ScottPlot.Plottables.HorizontalLine AddLevel(double value,string color)
 { var line=chart.Plot.Add.HorizontalLine(value); line.Color=Color.FromHex(color); line.LineWidth=1; line.LinePattern=LinePattern.Dashed; line.Axes.YAxis=chart.Plot.Axes.Right; return line; }
 private void UpdateAxes(DateTimeOffset now)
 {
  chart.Plot.Axes.SetLimitsX(now.LocalDateTime.AddMinutes(-15).ToOADate(),now.LocalDateTime.ToOADate());
  double peakPower = 0;
  foreach (double value in power) if (double.IsFinite(value)) peakPower = Math.Max(peakPower,value);
  chart.Plot.Axes.SetLimitsY(0,Math.Max(400,Math.Ceiling(peakPower * 1.1 / 100) * 100));
  chart.Plot.Axes.SetLimitsY(0,Math.Max(120,(double)vm.Reference+10),chart.Plot.Axes.Right);
 }
 private void OnUpdateSpeedChanged() { if (speedChanged.CurrentCount == 0) speedChanged.Release(); }
 private async Task SampleLoop()
 {
  using var systemService=new SystemTelemetryService();
  using IGpuTelemetryService service = new LibreHardwareMonitorTelemetryService();
  try
  {
   while (!cancellation.IsCancellationRequested)
   {
    if (vm.UpdateIntervalMs == Timeout.Infinite) { await speedChanged.WaitAsync(cancellation.Token); continue; }
    var started = System.Diagnostics.Stopwatch.GetTimestamp();
    var sample = service.Read(); var system=systemService.Read();
    if (cancellation.IsCancellationRequested) break;
    // Await dispatch: at most one update queued even when the UI is busy.
    try { await Dispatcher.UIThread.InvokeAsync(() => Apply(sample,service.MemorySensorDescription,system)); }
    catch (Exception e) { Console.Error.WriteLine(e); System.Diagnostics.Debug.WriteLine(e); }
    int interval = vm.UpdateIntervalMs;
    if (interval == Timeout.Infinite) continue;
    var remaining = TimeSpan.FromMilliseconds(interval) - System.Diagnostics.Stopwatch.GetElapsedTime(started);
    if (remaining > TimeSpan.Zero) await speedChanged.WaitAsync(remaining,cancellation.Token);
   }
  }
  catch (OperationCanceledException) { }
 }
 private DateTimeOffset clearedAt = DateTimeOffset.MinValue;
 private void ClearHistory(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
 {
  clearedAt = DateTimeOffset.Now;
  history.Clear(); systemHistory.Clear();
  Array.Fill(power,double.NaN); Array.Fill(coreTemperature,double.NaN); Array.Fill(temperature,double.NaN);
  Array.Fill(cpuValues,double.NaN); Array.Fill(ramValues,double.NaN);
   Array.Fill(fanValues,double.NaN); Array.Fill(loadValues,double.NaN);
  for(int i=0;i<times.Length;i++) times[i]=clearedAt.LocalDateTime.AddSeconds((i-3600)*0.25).ToOADate();
  UpdateAxes(clearedAt);
  systemChart.Plot.Axes.SetLimitsX(times[0],times[^1]);
  systemChart.Plot.Axes.SetLimitsY(0,100);

  chart.Refresh(); systemChart.Refresh();
 }
 private void Apply(GpuSample sample,string sensor,SystemSample system)
 {
  if (closing || vm.UpdateIntervalMs == Timeout.Infinite) return;
  if (sample.Timestamp <= clearedAt) return;
  history.Add(sample); systemHistory.Add(system); vm.Apply(sample,sensor); vm.ApplySystem(system);
  if (WindowState == WindowState.Minimized) return;
  var offset=times.Length-history.Count;
  for (int i=0;i<offset;i++) { times[i]=history[0].Timestamp.LocalDateTime.AddSeconds(i-offset).ToOADate(); power[i]=coreTemperature[i]=temperature[i]=cpuValues[i]=ramValues[i]=fanValues[i]=loadValues[i]=double.NaN; }
  for (int i=0;i<history.Count;i++) { var s=history[i]; var j=offset+i; times[j]=s.Timestamp.LocalDateTime.ToOADate(); power[j]=s.PowerW??double.NaN; coreTemperature[j]=s.CoreC??double.NaN; temperature[j]=s.MemoryJunctionC??double.NaN; fanValues[j]=s.FanPercent??double.NaN; loadValues[j]=s.LoadPercent??double.NaN; var sys=systemHistory[i]; cpuValues[j]=sys.CpuPercent??double.NaN; ramValues[j]=sys.RamPercent??double.NaN;  }
  if (vm.ValidThresholds) { warning.Y=(double)vm.Warning; hot.Y=(double)vm.Hot; reference.Y=sample.MemoryThermalLimitC ?? (double)vm.Reference; }
  UpdateAxes(sample.Timestamp); chart.Refresh();
  systemChart.Plot.Axes.SetLimitsX(sample.Timestamp.LocalDateTime.AddMinutes(-15).ToOADate(),sample.Timestamp.LocalDateTime.ToOADate());
  systemChart.Plot.Axes.SetLimitsY(0,100);
  systemChart.Refresh();
 }
 private async void OnClosing(object? sender,WindowClosingEventArgs e)
 {
  if (allowClose) return;
  e.Cancel=true;
  if (closing) return;
  closing=true; cancellation.Cancel();
  try { if (sampling != null) await sampling; }
  finally { SaveSettings(); vm.UpdateSpeedChanged -= OnUpdateSpeedChanged; speedChanged.Dispose(); cancellation.Dispose(); allowClose=true; Close(); }
 }
 private void SetupSystemChart()
 {
  Array.Fill(cpuValues,double.NaN);Array.Fill(ramValues,double.NaN);Array.Fill(fanValues,double.NaN); Array.Fill(loadValues,double.NaN);
  var p=systemChart.Plot;
  var c=p.Add.Scatter(times,cpuValues);c.LegendText="CPU %";c.Color=Color.FromHex("C49BFF");c.MarkerSize=0;c.LineWidth=2;
  var r=p.Add.Scatter(times,ramValues);r.LegendText="RAM %";r.Color=Color.FromHex("6CAEFF");r.MarkerSize=0;r.LineWidth=2;
  var fan=p.Add.Scatter(times,fanValues);fan.LegendText="GPU fan %";fan.Color=Color.FromHex("FF83BE");fan.MarkerSize=0;fan.LineWidth=2;
  var load=p.Add.Scatter(times,loadValues);load.LegendText="GPU load %";load.Color=Color.FromHex("FFC66D");load.MarkerSize=0;load.LineWidth=2;
  var axis=p.Axes.DateTimeTicksBottom();((ScottPlot.TickGenerators.DateTimeAutomatic)axis.TickGenerator).LabelFormatter=dt=>dt.ToString("HH:mm");
  p.Axes.Left.Label.Text="CPU / RAM / fan / GPU load · %";p.Axes.Right.IsVisible=false;
  p.FigureBackground.Color=Color.FromHex("192332");p.DataBackground.Color=Color.FromHex("192332");p.Axes.Color(Color.FromHex("A5B4C9"));p.Grid.MajorLineColor=Color.FromHex("293548");p.ShowLegend(Alignment.UpperLeft);systemChart.UserInputProcessor.Disable();
 }
 private async void SaveDashboardPng(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
 {
  if (sender is not Button button) return;
  button.IsEnabled=false;
  try
  {
   var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
   {
    Title="Save dashboard as PNG", SuggestedFileName=$"GpuMonitor-{DateTime.Now:yyyy-MM-dd-HHmmss}.png",
    DefaultExtension="png", ShowOverwritePrompt=true,
    FileTypeChoices=[new Avalonia.Platform.Storage.FilePickerFileType("PNG image") { Patterns=["*.png"] }]
   });
   if (file == null) return;
   // Render the window content at its current display scale, without desktop occlusion.
   var content = (Control)Content!;
   var scale = RenderScaling;
   using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(
    new Avalonia.PixelSize((int)Math.Ceiling(content.Bounds.Width*scale),(int)Math.Ceiling(content.Bounds.Height*scale)),
    new Avalonia.Vector(96*scale,96*scale));
   bitmap.Render(content);
   // A small opaque passe-partout gives the dashboard breathing room in the PNG.
   const double mat = 20;
   var width = content.Bounds.Width;
   var height = content.Bounds.Height;
   using var framed = new Avalonia.Media.Imaging.RenderTargetBitmap(
    new Avalonia.PixelSize((int)Math.Ceiling((width+2*mat)*scale),(int)Math.Ceiling((height+2*mat)*scale)),
    new Avalonia.Vector(96*scale,96*scale));
   using (var drawing = framed.CreateDrawingContext())
   {
    drawing.FillRectangle(Avalonia.Media.Brush.Parse("#101722"),new Avalonia.Rect(0,0,width+2*mat,height+2*mat));
    drawing.DrawImage(bitmap,new Avalonia.Rect(0,0,width,height),new Avalonia.Rect(mat,mat,width,height));
   }
   await using var stream = await file.OpenWriteAsync();
   stream.SetLength(0);
   framed.Save(stream);
  }
  catch (Exception ex)
  {
   Console.Error.WriteLine($"PNG export failed: {ex}");
   var dialog = new Window { Title="PNG export failed", Width=460, Height=160,
    Content=new TextBlock { Text=$"Could not save the dashboard: {ex.Message}", TextWrapping=Avalonia.Media.TextWrapping.Wrap, Margin=new Avalonia.Thickness(20) } };
   await dialog.ShowDialog(this);
  }
  finally { button.IsEnabled=true; }
 }
 private void LoadSettings()
 { try { if (File.Exists(settingsPath)) { var a=JsonSerializer.Deserialize<decimal[]>(File.ReadAllText(settingsPath)); if(a is {Length:3} && a[0]>=20 && a[0]<a[1] && a[1]<a[2] && a[2]<=150) { vm.Warning=a[0];vm.Hot=a[1];vm.Reference=a[2]; } } } catch(Exception e) { System.Diagnostics.Debug.WriteLine(e.Message); } }
 private void SaveSettings()
 { try { if(vm.ValidThresholds) { Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!); File.WriteAllText(settingsPath,JsonSerializer.Serialize(new[]{vm.Warning,vm.Hot,vm.Reference})); } } catch(Exception e) { System.Diagnostics.Debug.WriteLine(e.Message); } }
}






