using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using Avalonia.Threading;
// using Androidplayer.Src.Controls;
using Androidplayer.Src.Keymap;
using Androidplayer.Src.Keymap.K_store;
using Androidplayer.Src.Rendering;
using Androidplayer.Store;
using Avalonia.Media;


#if WINDOWS
using SharpDX.Direct3D11;
using SharpDX.DXGI;
#endif

namespace Androidplayer.Src;

public class App_manager : IDisposable
{
    private Adb_worker my_adb_worker;
    private app_worker my_app_worker;
    private Scrcpy_worker scrcpy_worker;

    private Native_view my_image;

    private bool first_frame_displayed = false;

    // private DirectX directX;
    
    // After
    private IVideoRenderer? my_renderer;

    string fileToPlay = @"I:\movie\Kung.Fu.Panda.3.2016.720p.WEBRip.x264.AAC-ETRG.mp4";
    Src.FFmpeg ffmpeg; // FFmpeg Video Demuxing & HW Decoding
    Thread threadPlay; // Simulates FPS

    private bool is_running = true;

    public App_manager(Native_view image)
    {
        my_image = image;

        my_info.Instance.PropertyChanged += my_info_propertychanged;

        if (!my_info.Instance.DeveloperMode)
        {
            // k_info.Instance.directx = new DirectX(my_image.PlatformHandle.Handle);
            
            k_info.Instance.my_renderer.Initialize(my_image.PlatformHandle.Handle);

            my_adb_worker = new Adb_worker();
            my_adb_worker.ProgressChanged += my_app_worker_ProgressChanged;
            my_adb_worker.CountingCompleted += My_adb_workerOnCountingCompleted;
            my_adb_worker.devicedisconnected += My_adb_workerOndevicedisconnected;

            my_adb_worker.StartCounting();
        }
        else
        {
            // k_info.Instance.directx = new DirectX(my_image.PlatformHandle.Handle);
            k_info.Instance.my_renderer.Initialize(my_image.PlatformHandle.Handle);

            var w = my_image.Bounds.Width;
            var h = my_image.Bounds.Height;

            Console.WriteLine($"Surface size FIXED after init: {w} x {h}");
            Console.WriteLine(my_image.PlatformHandle.Handle);

            // my_app_worker = new app_worker();
            //
            // my_app_worker.ProgressChanged += my_app_worker_ProgressChanged;
            // my_app_worker.CountingCompleted += my_app_worker_Completed;
            //
            // my_app_worker.StartCounting();
            
            
            my_app_worker_Completed();
            
            
            
            
            
        }
    }

    
    #if WINDOWS
    
    
    private void play_video()
    {
        try
        {
            ffmpeg = new Src.FFmpeg();
            

            // if (!ffmpeg.InitHWAccel(k_info.Instance.my_renderer._device))
            // {
            //     Console.WriteLine("Failed to Initialize FFmpeg's HW Acceleration");
            //     return;
            // }
            
#if WINDOWS
            if (my_renderer is DirectX dx && !ffmpeg.InitHWAccel(dx._device))
            {
                Console.WriteLine("Failed to Initialize FFmpeg's HW Acceleration");
                return;
            }
#endif

            if (!ffmpeg.Open(fileToPlay))
            {
                Console.WriteLine("FFmpeg failed to open input");
                return;
            }

            threadPlay = new Thread(() =>
            {
                try
                {
                    Stopwatch sw = new Stopwatch();
                    while (is_running)
                    {
                        sw.Restart();
                        Texture2D textureHW = ffmpeg.GetFrame();
                        if (textureHW == null)
                        {
                            Console.WriteLine("Empty Texture!");
                            continue;
                        }

                        if (My_Store.Instance.VideoWidth == 0 || My_Store.Instance.VideoHeight == 0)
                        {
                            if (My_Store.Instance.VideoWidth != textureHW.Description.Width &&
                                My_Store.Instance.VideoHeight != textureHW.Description.Height)
                            {
                                My_Store.Instance.SetVideoResolution(
                                    textureHW.Description.Width,
                                    textureHW.Description.Height);
                            }
                        }

                        // directX.PresentFrame(textureHW);
                        
#if WINDOWS
                        if (k_info.Instance.my_renderer is DirectX dx)
                            dx.PresentFrame(textureHW);
#endif
                        sw.Stop();

                        Thread.Sleep(16);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Thread error: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            });

            // Removed: threadPlay.SetApartmentState(ApartmentState.STA) — unsupported on .NET Core
            threadPlay.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Initialization error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }


    
    #endif
    
    
    private void my_info_propertychanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(my_info.TakeScreenshot):

                Dispatcher.UIThread.Post(() =>
                {
                    if (my_info.Instance.TakeScreenshot)
                    {
                        if (my_image != null)
                        {
                            var w = my_image.Bounds.Width;
                            var h = my_image.Bounds.Height;
                            // Hook here if you need to react to screenshot state
                        }

                        Console.WriteLine("reseting takescreenshot");
                        my_info.Instance.TakeScreenshot = false;
                    }
                });

                break;

            case nameof(my_info.Window_resizing):

                if (!my_info.Instance.Window_resizing)
                {
                    Console.WriteLine("finished resizing #####");

                    Dispatcher.UIThread.Post(() =>
                    {
                        Console.WriteLine($" Display view {my_image.Bounds.Width}, {my_image.Bounds.Height}");

                        if (my_image != null)
                        {
                            // k_info.Instance.directx?.ResizeSwapChain(
                            //     (int)my_image.Bounds.Width,
                            //     (int)my_image.Bounds.Height);
                        
                            
                            // k_info.Instance.my_renderer?.ResizeSwapChain(
                            //     (int)my_image.Bounds.Width,
                            //     (int)my_image.Bounds.Height);
                            
#if WINDOWS

                            
                            if (k_info.Instance.my_renderer is DirectX dx)
                                dx.ResizeSwapChain(
                                    (int)my_image.Bounds.Width,
                                    (int)my_image.Bounds.Height);
#endif

                            if (my_info.Instance.DeveloperMode)
                            {
                                // k_info.Instance.directx?.HandleResize();
                                // k_info.Instance.my_renderer?.HandleResize();
                                
                                #if WINDOWS
                                
                                
                                if (k_info.Instance.my_renderer is DirectX dx1)
                                    dx1.HandleResize();
                                #endif
                            }
                        }
                    }, DispatcherPriority.Render);
                }
                break;
        }
    }

    private void del_mm()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (my_image != null)
            {
                var w = my_image.Bounds.Width;
                var h = my_image.Bounds.Height;

                Console.WriteLine($"Surface size FIXED: {w} x {h}");
                Console.WriteLine($"INIT DX SIZE: {w} x {h}");
                // k_info.Instance.directx?.DisplayImage("dev_img1.jpg");
                // k_info.Instance.directx?.DisplayImage("dev_img2.jpg");
                
                k_info.Instance.my_renderer?.DisplayImage("dev_img2.jpg");
                
                
                // MainImage._floatingContent.Background = Brushes.Transparent;
                Home.Instance.displayView.MainImage._floatingContent.Background = Brushes.Transparent;

                // Home.Instance.displayView.MainImage.IsVisible = true;
                
                // Home.Instance.Show();
                // Home.Instance.IsVisible = true;
            }
        }, DispatcherPriority.Loaded);
    }

    private void My_adb_workerOndevicedisconnected()
    {
        if (scrcpy_worker != null)
        {
            scrcpy_worker.Dispose();
            scrcpy_worker = null;
        }
        my_app_worker_ProgressChanged(0, "No device found. please reconnect your device");

        Dispatcher.UIThread.Post(() =>
        {
            if (Home.Instance != null)
            {
                Home.Instance.loadingpage.IsVisible = true;
                Home.Instance.displayView.IsVisible = false;
            }
        });

        my_info.Instance.Auto_resizing = true;
    }

    private void my_app_worker_Completed()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Home.Instance != null)
            {
                Home.Instance.loadingpage.IsVisible = false;
                Home.Instance.displayView.IsVisible = true;
            }

            del_mm();

            Console.WriteLine("displayview ready ##################");

            OverlayManager.Instance?.rerender_overlay();
        });
    }

    private void my_app_worker_ProgressChanged(int num, string status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Home.Instance?.loadingpage.UpdateProgress((double)num, status);
        });
    }

    private void My_adb_workerOnCountingCompleted()
    {
        Console.WriteLine("adb finished from app manager");

        if (my_adb_worker?.is_deviceconnected == false)
        {
            return;
        }

        if (scrcpy_worker != null)
        {
            scrcpy_worker.Dispose();
            scrcpy_worker = null;
        }

        scrcpy_worker = new Scrcpy_worker();

        // scrcpy_worker.dx_Device = k_info.Instance.directx?.my_Device;
        
        #if WINDOWS
        scrcpy_worker.dx_Device = (k_info.Instance.my_renderer as DirectX)?.my_Device;
        #endif

        scrcpy_worker.Frame_almostready += Scrcpy_workerOnFrame_almostready;
        scrcpy_worker.videosizeReady += on_videosizeready;
        scrcpy_worker.ControlSocketReady += on_ControlSocketReady;
        scrcpy_worker.DeviceResolutionReady += on_DeviceResolutionReady;

        // scrcpy_worker.FrameReady += del_display_frame;
        scrcpy_worker.scrcpy_desposed += My_adb_workerOnCountingCompleted;

        scrcpy_worker.Start();
    }

    private void Scrcpy_workerOnFrame_almostready()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Home.Instance != null)
            {
                Home.Instance.loadingpage.IsVisible = false;
                Home.Instance.displayView.IsVisible = true;
            }

            OverlayManager.Instance?.rerender_overlay();
        });
    }

    private void on_DeviceResolutionReady((int Width, int Height) div)
    {
        My_Store.Instance.SetDeviceResolution(div.Width, div.Height);
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
        
            Home.Instance.displayView.MainImage._floatingContent.Background = Brushes.Transparent;
            
        });
        
    }

    private void on_ControlSocketReady(TcpClient control_socket)
    {
        My_Store.Instance.SetControlSocket(control_socket);
    }

    private void on_videosizeready((int Width, int Height) vid)
    {
        My_Store.Instance.SetVideoResolution(vid.Width, vid.Height);

        if (vid.Width > vid.Height)
        {
            my_info.Instance.IsLandscapemode = true;
        }
        else
        {
            my_info.Instance.IsLandscapemode = false;
        }

        Console.WriteLine("video size ready #######");
    }

    
    public void Dispose()
    {
        is_running = false;

        threadPlay?.Join();
    }
}