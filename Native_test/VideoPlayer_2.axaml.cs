// using System;
// using System.Diagnostics;
// using System.Threading;
// using Androidplayer.Src;
// using Avalonia;
// using Avalonia.Controls;
// using Avalonia.Interactivity;
// using Avalonia.Markup.Xaml;
// using Avalonia.Platform;
// using SharpDX.Direct3D11;
//
// namespace Androidplayer.Native_test;
//
// public partial class VideoPlayer_2 : UserControl
// {
//     
//     
//     // string fileToPlay = @"I:\Hour Timer_1080p.mp4";
//     // string fileToPlay = @"C:\Users\miki\Downloads\Compressed\scrcpy-win64-v1.20\demo.mp4";
//     
//     string fileToPlay = @"M:\movie\Kung.Fu.Panda.3.2016.720p.WEBRip.x264.AAC-ETRG.mp4";
//     
//     // string fileToPlay = @"M:\movie\demo_h264_opus.mp4";
//
//     Src.FFmpeg ffmpeg; // FFmpeg Video Demuxing & HW Decoding
//     public DirectX_2 directX; // DirectX Video Processing & Rendering
//     // public static DirectX directX { get; set; }
//     Thread threadPlay; // Simulates FPS  
//
//
//     private readonly object _d3dLock = new object();
//     
//     private bool is_running = true;
//     
//     public VideoPlayer_2()
//     {
//         InitializeComponent();
//         
//         
//         Loaded += OnLoaded;
//         Unloaded += OnUnloaded;
//     
//         NativeView.HandleCreated += NativeViewOnHandleCreated;
//     }
//     
//     
//      private void OnUnloaded(object? sender, RoutedEventArgs e)
//     {
//         is_running = false;
//     }
//
//     private void NativeViewOnHandleCreated(object? sender, IPlatformHandle e)
//     {
//         var handle = NativeView.PlatformHandle;
//         
//         Console.WriteLine(handle?.HandleDescriptor);
//         
//         
//         
//         
//         
//        
//             
//             
//             
//             try
// {
//     ffmpeg = new Src.FFmpeg();
//     directX = new DirectX_2(handle.Handle);
//
//     if (!ffmpeg.InitHWAccel(directX._device))
//     {
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
//     NativeView.SizeChanged += (_, e) =>
//     {
//         // if (directX != null && e.NewSize.Width > 0 && e.NewSize.Height > 0)
//         // {
//         //     directX.ResizeSwapChain((int)e.NewSize.Width, (int)e.NewSize.Height);
//         // }
//         
//         if (directX != null &&
//             e.NewSize.Width > 0 &&
//             e.NewSize.Height > 0)
//         {
//             lock (_d3dLock)
//             {
//                 directX.ResizeSwapChain(
//                     (int)e.NewSize.Width,
//                     (int)e.NewSize.Height);
//             }
//         }
//     };
//     
//
//     threadPlay = new Thread(() =>
//     {
//         try
//         {
//             // ---- Measurement state ----
//             var swDecode  = new Stopwatch();
//             var swPresent = new Stopwatch();
//             var swTotal   = new Stopwatch();
//
//             double sumDecode  = 0;
//             double sumPresent = 0;
//             double sumTotal   = 0;
//             int    samples    = 0;
//             int    frameCount = 0;
//             const int ReportEvery = 60; // ~1 second at 60 fps
//
//             double minTotal = double.MaxValue;
//             double maxTotal = 0;
//
//       
//             
//             while (is_running)
//             {
//                 swTotal.Restart();
//
//                 Texture2D? textureHW = null;
//
//                 lock (_d3dLock)
//                 {
//                     // Decode while resize is blocked.
//                     swDecode.Restart();
//
//                     textureHW = ffmpeg.GetFrame();
//
//                     swDecode.Stop();
//
//                     if (textureHW != null)
//                     {
//                         swPresent.Restart();
//
//                         try
//                         {
//                             directX.PresentFrame(textureHW);
//                         }
//                         finally
//                         {
//                             textureHW.Dispose();
//                         }
//
//                         swPresent.Stop();
//                     }
//                 }
//
//                 swTotal.Stop();
//
//                 if (textureHW == null)
//                 {
//                     Thread.Sleep(1);
//                     continue;
//                 }
//
//                 double decodeMs = swDecode.Elapsed.TotalMilliseconds;
//                 double presentMs = swPresent.Elapsed.TotalMilliseconds;
//                 double totalMs = swTotal.Elapsed.TotalMilliseconds;
//
//                 sumDecode += decodeMs;
//                 sumPresent += presentMs;
//                 sumTotal += totalMs;
//
//                 samples++;
//
//                 if (totalMs < minTotal)
//                     minTotal = totalMs;
//
//                 if (totalMs > maxTotal)
//                     maxTotal = totalMs;
//
//                 if (++frameCount >= ReportEvery)
//                 {
//                     frameCount = 0;
//
//                     double avgDecode = sumDecode / samples;
//                     double avgPresent = sumPresent / samples;
//                     double avgTotal = sumTotal / samples;
//
//                     double fps = avgTotal > 0
//                         ? 1000.0 / avgTotal
//                         : 0;
//
//                     Console.WriteLine(
//                         $"[latency] decode={avgDecode:F2}ms  " +
//                         $"present={avgPresent:F2}ms  " +
//                         $"total={avgTotal:F2}ms  " +
//                         $"min={minTotal:F2}ms  " +
//                         $"max={maxTotal:F2}ms  " +
//                         $"fps={fps:F1}");
//
//                     sumDecode = 0;
//                     sumPresent = 0;
//                     sumTotal = 0;
//
//                     samples = 0;
//                     minTotal = double.MaxValue;
//                     maxTotal = 0;
//                 }
//
//                 double remaining =
//                     16.67 - swTotal.Elapsed.TotalMilliseconds;
//
//                 if (remaining > 1)
//                     Thread.Sleep((int)remaining);
//             }
//             
//             
//             
//             
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
// }
//         
//         
//         
//         
//     }
//
//     private void OnLoaded(object? sender, RoutedEventArgs e)
//     {
//         
//     }
//
//     
// }



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

public partial class VideoPlayer_2 : UserControl
{
    string fileToPlay = @"M:\movie\Kung.Fu.Panda.3.2016.720p.WEBRip.x264.AAC-ETRG.mp4";

    Src.FFmpeg ffmpeg;          // FFmpeg Video Demuxing & HW Decoding
    public DirectX directX;   // DirectX Video Processing & Rendering
    Thread threadPlay;          // Simulates FPS

    private volatile bool is_running = true;

    
    private readonly object _d3dLock = new object();
    public VideoPlayer_2()
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
        
        
        // directX = new DirectX_2(handle.Handle);
        // directX.DisplayImage("dev_img1.jpg");

        
        // NativeView.SizeChanged += (_, e) =>
        // {
        //     // if (directX != null && e.NewSize.Width > 0 && e.NewSize.Height > 0)
        //     // {
        //     //     directX.ResizeSwapChain((int)e.NewSize.Width, (int)e.NewSize.Height);
        //     // }
        //     
        //     if (directX != null &&
        //         e.NewSize.Width > 0 &&
        //         e.NewSize.Height > 0)
        //     {
        //         lock (_d3dLock)
        //         {
        //             
        //             // 1600x720
        //             
        //             
        //             directX.ResizeSwapChain(
        //                 (int)e.NewSize.Width,
        //                 (int)e.NewSize.Height);
        //         }
        //     }
        // };
        
        
        
        
        
        NativeView.SizeChanged += (_, e) =>
        {
            if (directX != null &&
                e.NewSize.Width > 0 &&
                e.NewSize.Height > 0)
            {
                // const double sourceWidth = 1600;
                // const double sourceHeight = 720;
                
                
                const double sourceWidth = 1280;
                const double sourceHeight = 720;

                double windowWidth = e.NewSize.Width;
                double windowHeight = e.NewSize.Height;

                // Calculate the largest 1600:720 rectangle
                // that fits inside the native window.
                double scale = Math.Min(
                    windowWidth / sourceWidth,
                    windowHeight / sourceHeight
                );

                double displayWidth = sourceWidth * scale;
                double displayHeight = sourceHeight * scale;

                // Center it inside the native window.
                double x = (windowWidth - displayWidth) / 2.0;
                double y = (windowHeight - displayHeight) / 2.0;

                lock (_d3dLock)
                {
                    // directX.ResizeSwapChain(
                    // (int)Math.Round(displayWidth),
                    // (int)Math.Round(displayHeight)
                    // );
                    my_overlay.Width = displayWidth;
                    my_overlay.Height = displayHeight;  
                    
                    // directX.SetTargetRegionFraction(0.0, 0.0, 
                    //     (int)Math.Round(displayWidth),
                    //     (int)Math.Round(displayHeight)
                    //     
                    //     );
                    
                    
                    
                    directX.ResizeSwapChain((int)Math.Round(windowWidth), (int)Math.Round(windowHeight));
                    // directX.SetTargetRegion(
                    //     (int)Math.Round(x),
                    //     (int)Math.Round(y),
                    //     (int)Math.Round(displayWidth),
                    //     (int)Math.Round(displayWidth));
                    
                    
                    
                }
            }
        };
        
        
        
        
        
        
        
        
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
        
            // NOTE: No SizeChanged handler here on purpose.
            // Avalonia reports DIPs (not physical pixels) and may fire before the HWND
            // is actually resized. DirectX_2.PresentFrame() now syncs the swap chain
            // with the real client size of the HWND every frame.
        
            threadPlay = new Thread(() =>
            {
                try
                {
                    var swDecode  = new Stopwatch();
                    var swPresent = new Stopwatch();
                    var swTotal   = new Stopwatch();
        
                    double sumDecode  = 0;
                    double sumPresent = 0;
                    double sumTotal   = 0;
                    int    samples    = 0;
                    int    frameCount = 0;
                    const int ReportEvery = 60;
        
                    double minTotal = double.MaxValue;
                    double maxTotal = 0;
        
                    while (is_running)
                    {
                        swTotal.Restart();
                        
                        
                        
                        ////////////////////////////////////
                        
                        
                        ////////////////////////////////////

                        lock (_d3dLock)
                        {

                        // ---- Decode ----
                        swDecode.Restart();
                        Texture2D? textureHW = ffmpeg.GetFrame();
                        swDecode.Stop();
        
                        if (textureHW == null)
                        {
                            Thread.Sleep(1);
                            continue;
                        }
        
                        // ---- Present (resize sync is handled inside DirectX_2) ----
                        // If your decoder texture is padded (e.g. 1088 rows for 1080 video),
                        // pass the real frame size: directX.PresentFrame(textureHW, frameW, frameH);
                        swPresent.Restart();
                        try
                        {
                            directX.PresentFrame(textureHW);
        
                            // Console.WriteLine(textureHW.Description.Width);
                        }
                        finally
                        {
                            textureHW.Dispose();
                        }
                        swPresent.Stop();
        
                        }
                        swTotal.Stop();
        
                        double decodeMs  = swDecode.Elapsed.TotalMilliseconds;
                        double presentMs = swPresent.Elapsed.TotalMilliseconds;
                        double totalMs   = swTotal.Elapsed.TotalMilliseconds;
        
                        sumDecode  += decodeMs;
                        sumPresent += presentMs;
                        sumTotal   += totalMs;
                        samples++;
        
                        if (totalMs < minTotal) minTotal = totalMs;
                        if (totalMs > maxTotal) maxTotal = totalMs;
        
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
                                $"min={minTotal:F2}ms  " +
                                $"max={maxTotal:F2}ms  " +
                                $"fps={fps:F1}");
        
                            sumDecode = 0;
                            sumPresent = 0;
                            sumTotal = 0;
                            samples = 0;
                            minTotal = double.MaxValue;
                            maxTotal = 0;
                        }
        
                        // Pace to ~60 fps
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

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        // directX.ClearTargetRegion();
        //
        // // Center 80% of the window:
        // // directX.SetTargetRegionFraction(0.1, 0.1, 0.8, 0.8);
        // directX.SetTargetRegionFraction(0.0, 0.0, 0.5, 0.5);
        
        // directX.DisplayImage("dev_img1.jpg");
    }

    private void My_overlay_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var p = e.GetPosition((Visual)sender);   // Avalonia
        // var p = e.GetCurrentPoint((Visual)sender).Position; // alternative

        Console.WriteLine($"x={p.X}, y={p.Y}");
    }
}