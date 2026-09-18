using System;
using System.Diagnostics;
using System.Threading;
using Androidplayer.Src;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using SharpDX.Direct3D11;

namespace Androidplayer.Native_test;

public partial class VideoPlayer : UserControl
{
    
    // string fileToPlay = @"I:\Hour Timer_1080p.mp4";
    // string fileToPlay = @"C:\Users\miki\Downloads\Compressed\scrcpy-win64-v1.20\demo.mp4";
    string fileToPlay = @"M:\movie\Kung.Fu.Panda.3.2016.720p.WEBRip.x264.AAC-ETRG.mp4";

    Src.FFmpeg ffmpeg; // FFmpeg Video Demuxing & HW Decoding
    public DirectX directX; // DirectX Video Processing & Rendering
    // public static DirectX directX { get; set; }
    Thread threadPlay; // Simulates FPS  


    private bool is_running = true;
    public VideoPlayer()
    {
        InitializeComponent();
        
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        
        NativeView.HandleCreated += NativeViewOnHandleCreated;
        
      
    }

  
    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        is_running = false;
    }

    private void NativeViewOnHandleCreated(object? sender, IPlatformHandle e)
    {
        var handle = NativeView.PlatformHandle;
        
        Console.WriteLine(handle?.HandleDescriptor);
        
        
        
            //   try
            // {
            //     ffmpeg = new Src.FFmpeg();
            //     directX = new DirectX(handle.Handle);
            //
            //     if (!ffmpeg.InitHWAccel(directX._device)) 
            //     { 
            //        
            //         Console.WriteLine("Failed to Initialize FFmpeg's HW Acceleration");
            //         return; 
            //     }
            //
            //     if (!ffmpeg.Open(fileToPlay)) 
            //     { 
            //         Console.WriteLine("FFmpeg failed to open input"); 
            //         return; 
            //     }
            //
            //     threadPlay = new Thread(() =>
            //     {
            //         try
            //         {
            //             Stopwatch sw = new Stopwatch();
            //             while (is_running)
            //             {
            //                 
            //                 sw.Restart();
            //                 // FFmpeg HW Decode Frame
            //                 Texture2D textureHW = ffmpeg.GetFrame();
            //                 if (textureHW == null) 
            //                 { 
            //                     Console.WriteLine("Empty Texture!"); 
            //                     continue; 
            //                 }
            //
            //                 // DirectX HW Process & Present Frame
            //                 // directX.PresentFrame(textureHW);
            //                 
            //                 try
            //                 {
            //                     directX.PresentFrame(textureHW);
            //                 }
            //                 finally
            //                 {
            //                     textureHW.Dispose();
            //                 }
            //                 
            //                 
            //                 sw.Stop(); // Stop measuring after presenting
            //                 
            //                 // 3️⃣ Print time in milliseconds
            //                 // Console.WriteLine($"Frame time (decode + render): {sw.Elapsed.TotalMilliseconds:F2} ms");
            //                 
            //
            //                 Thread.Sleep(16); // Simulates FPS
            //             }
            //         }
            //         catch (Exception ex)
            //         {
            //             Console.WriteLine($"Thread error: {ex.Message}");
            //             Console.WriteLine($"Stack trace: {ex.StackTrace}");
            //         }
            //     });
            //
            //     threadPlay.SetApartmentState(ApartmentState.STA);
            //     threadPlay.Start();
            // }
            // catch (Exception ex)
            // {
            //     Console.WriteLine($"Initialization error: {ex.Message}");
            //     Console.WriteLine($"Stack trace: {ex.StackTrace}");
            //     // MessageBox.Show($"Failed to initialize: {ex.Message}");
            // }
            //
                 
        
            
            
            
            
            
            
            
            
            
            try
{
    ffmpeg = new Src.FFmpeg();
    directX = new DirectX(handle.Handle);

    if (!ffmpeg.InitHWAccel(directX._device))
    {
        Console.WriteLine("Failed to Initialize FFmpeg's HW Acceleration");
        return;
    }

    if (!ffmpeg.Open(fileToPlay))
    {
        Console.WriteLine("FFmpeg failed to open input");
        return;
    }

    threadPlay = new Thread(() =>
    {
        try
        {
            // ---- Measurement state ----
            var swDecode  = new Stopwatch();
            var swPresent = new Stopwatch();
            var swTotal   = new Stopwatch();

            double sumDecode  = 0;
            double sumPresent = 0;
            double sumTotal   = 0;
            int    samples    = 0;
            int    frameCount = 0;
            const int ReportEvery = 60; // ~1 second at 60 fps

            double minTotal = double.MaxValue;
            double maxTotal = 0;

            while (is_running)
            {
                swTotal.Restart();

                // ---- Decode (FFmpeg av_read_frame + send/receive) ----
                swDecode.Restart();
                Texture2D textureHW = ffmpeg.GetFrame();
                swDecode.Stop();

                if (textureHW == null)
                {
                    // Don't count empty frames; short yield so we don't spin.
                    Thread.Sleep(1);
                    continue;
                }

                // ---- Present (GPU copy + VideoProcessorBlt + swapchain) ----
                swPresent.Restart();
                try
                {
                    directX.PresentFrame(textureHW);
                }
                finally
                {
                    textureHW.Dispose();
                }
                swPresent.Stop();

                swTotal.Stop();

                // ---- Accumulate ----
                double decodeMs  = swDecode.Elapsed.TotalMilliseconds;
                double presentMs = swPresent.Elapsed.TotalMilliseconds;
                double totalMs   = swTotal.Elapsed.TotalMilliseconds;

                sumDecode  += decodeMs;
                sumPresent += presentMs;
                sumTotal   += totalMs;
                samples++;

                if (totalMs < minTotal) minTotal = totalMs;
                if (totalMs > maxTotal) maxTotal = totalMs;

                // ---- Report once per second ----
                if (++frameCount >= ReportEvery)
                {
                    frameCount = 0;

                    double avgDecode  = sumDecode  / samples;
                    double avgPresent = sumPresent / samples;
                    double avgTotal   = sumTotal   / samples;
                    double fps        = avgTotal > 0 ? 1000.0 / avgTotal : 0;

                    Console.WriteLine(
                        $"[latency] decode={avgDecode:F2}ms  " +
                        $"present={avgPresent:F2}ms  " +
                        $"total={avgTotal:F2}ms  " +
                        $"min={minTotal:F2}ms  max={maxTotal:F2}ms  " +
                        $"fps={fps:F1}");

                    sumDecode = sumPresent = sumTotal = 0;
                    samples = 0;
                    minTotal = double.MaxValue;
                    maxTotal = 0;
                }

                // ---- Pace to ~60 fps, sleeping only the remainder ----
                double remaining = 16.67 - swTotal.Elapsed.TotalMilliseconds;
                if (remaining > 1)
                    Thread.Sleep((int)remaining);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Thread error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    });

    threadPlay.SetApartmentState(ApartmentState.STA);
    threadPlay.Start();
}
catch (Exception ex)
{
    Console.WriteLine($"Initialization error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}
        
        
        
        
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        
    }


    // private void VideoViewOnPointerEntered(object sender, PointerEventArgs e)
    // {
    //     ControlsPanel.IsVisible = true;
    // }
    //
    // private void VideoViewOnPointerExited(object sender, PointerEventArgs e)
    // {
    //     ControlsPanel.IsVisible = false;
    // }
}