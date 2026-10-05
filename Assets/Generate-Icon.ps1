Add-Type -AssemblyName System.Drawing
$frames = @()
foreach ($size in @(16,24,32,48,64,128,256)) {
 $bmp = [System.Drawing.Bitmap]::new($size,$size)
 $g = [System.Drawing.Graphics]::FromImage($bmp)
 $g.SmoothingMode = 'AntiAlias'
 $g.ScaleTransform($size/256.0,$size/256.0)
 $bg=[System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#101722'))
 $mint=[System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#58D6B0'),12)
 $blue=[System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#6CAEFF'),13)
 $blue.StartCap='Round'; $blue.EndCap='Round'; $blue.LineJoin='Round'
 $path=[System.Drawing.Drawing2D.GraphicsPath]::new()
 $path.AddArc(4,4,64,64,180,90); $path.AddArc(188,4,64,64,270,90); $path.AddArc(188,188,64,64,0,90); $path.AddArc(4,188,64,64,90,90); $path.CloseFigure()
 $g.FillPath($bg,$path)
 foreach ($pin in @(96,128,160)) { $g.DrawLine($mint,$pin,43,$pin,65); $g.DrawLine($mint,$pin,191,$pin,213); $g.DrawLine($mint,43,$pin,65,$pin); $g.DrawLine($mint,191,$pin,213,$pin) }
 $g.DrawRectangle($mint,65,65,126,126)
 $points=[System.Drawing.PointF[]]@([System.Drawing.PointF]::new(86,145),[System.Drawing.PointF]::new(110,145),[System.Drawing.PointF]::new(128,104),[System.Drawing.PointF]::new(147,157),[System.Drawing.PointF]::new(170,119))
 $g.DrawLines($blue,$points)
 $stream=[System.IO.MemoryStream]::new(); $bmp.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
 $frames += ,@{Size=$size;Bytes=$stream.ToArray()}
 if ($size -eq 256) { $bmp.Save((Join-Path $PWD 'Assets\gpu-monitor.png'),[System.Drawing.Imaging.ImageFormat]::Png) }
 $stream.Dispose();$g.Dispose();$bmp.Dispose();$bg.Dispose();$mint.Dispose();$blue.Dispose();$path.Dispose()
}
$out=[System.IO.File]::Create((Join-Path $PWD 'Assets\gpu-monitor.ico'));$writer=[System.IO.BinaryWriter]::new($out)
$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
$offset=6+16*$frames.Count
foreach($frame in $frames) { $dimension=if($frame.Size -eq 256){0}else{$frame.Size};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset);$offset+=$frame.Bytes.Length }
foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
$writer.Dispose()
