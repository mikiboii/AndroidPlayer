using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Androidplayer.Src.Keymap;
using Androidplayer.Src.Keymap.K_store;
using Androidplayer.Store;
using FlyleafLib_01.Controls.WPF;
// using FlyleafLib.Controls.WPF;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace Androidplayer.Src;

public class App_manager : IDisposable
{
    private Adb_worker my_adb_worker;

    private app_worker my_app_worker;



    // private FFmpeg ffmpeg;


    // public DirectXWpf directX;

    // public DirectXWpf_2 directX;


    private D3DImage _d3dImage;

    private Scrcpy_worker scrcpy_worker;



    // private IntPtr my_image;
    
    private FlyleafHost my_image;

    private bool first_frame_displayed = false;


    // private DemoDirectX directX;
    private DirectX directX;
    // private DirectXWpf directX;

    string fileToPlay = @"I:\movie\Kung.Fu.Panda.3.2016.720p.WEBRip.x264.AAC-ETRG.mp4";
    Src.FFmpeg ffmpeg; // FFmpeg Video Demuxing & HW Decoding
    Thread threadPlay; // Simulates FPS
    
    private bool is_running = true;
    
    public App_manager(FlyleafHost  image)
    {

        my_image = image;


        
     
        // My_Store.Instance.PropertyChanged += my_store_propeertychanged;


        my_info.Instance.PropertyChanged += my_info_propertychanged;

        _d3dImage = new D3DImage();
        // my_image.Source = _d3dImage;
        
        // directX = new DirectX(my_image.SurfaceHandle);

        if (!my_info.Instance.DeveloperMode)
        {

            
            // directX = new DirectX(my_image.SurfaceHandle);

            k_info.Instance.directx = new DirectX(my_image.SurfaceHandle);
            
            my_adb_worker = new Adb_worker();
            my_adb_worker.ProgressChanged += my_app_worker_ProgressChanged;
            my_adb_worker.CountingCompleted += My_adb_workerOnCountingCompleted;
            my_adb_worker.devicedisconnected += My_adb_workerOndevicedisconnected;

            // my_adb_worker.ErrorOccurred += general_error;

            my_adb_worker.StartCounting();
            

            // directX = new DirectXWpf(new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle);

            // directX = new DirectXWpf_2(new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle);


            // directX = new DirectXWpf_2(new WindowInteropHelper(Application.Current.MainWindow).Handle, my_image);


            // directX = new DemoDirectX(my_image);

        }
        else
        {

            // directX = new DirectXWpf(new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle);

            //
            // directX = new DirectXWpf_2(new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle);
            //
            // directX.DisplayImage("dev_img1.jpg");
            //
            // // One-liner that automatically processes through your existing pipeline
            // var texture = directX.DisplayImage("dev_img1.jpg");
            // if (texture != null)
            //     del_display_frame(texture);




            // directX = new DirectXWpf_2(new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle);

            // directX = new DirectXWpf_2(new WindowInteropHelper(Application.Current.MainWindow).Handle, my_image);
            //
            //
            // // // Use the same approach as before
            // var texture = directX.DisplayImage("dev_img1.jpg");
            // if (texture != null)
            //     del_display_frame(texture); // Use your original onframe_ready, not del_display_frame
            //
            //
            //




            // directX = new DirectX(my_image.SurfaceHandle);
            
            k_info.Instance.directx = new DirectX(my_image.SurfaceHandle);
            
            var w = my_image.Surface.ActualWidth;
            var h = my_image.Surface.ActualHeight;
                                
            Console.WriteLine($"Surface size FIXED after init: {w} x {h}");
            Console.WriteLine(my_image.SurfaceHandle);
            
            
            // directX.ResizeSwapChain(100, 100);
            
            my_app_worker = new app_worker();
            
            
            
            my_app_worker.ProgressChanged += my_app_worker_ProgressChanged;
            my_app_worker.CountingCompleted += my_app_worker_Completed;
            
            my_app_worker.StartCounting();


            
            

            // my_app_worker_Completed();

            
            // my_image.OverlayCreated += (s, e) =>
            // {
            //
            //
            //     Console.WriteLine("initialized Handle ###");
            //     del_mm();
            //
            // };




            // my_image.Surface.Width = 500;
            // my_image.Surface.Height = 500;
            //
            //
            //
            // del_mm();
            // del_mm();
            
            

            

            // var demo4 = new DemoDirectX(my_image); // Fill container (may crop)
            // var demo4 = new DirectX(my_image.SurfaceHandle); // Fill container (may crop)
            // // _demoDirectX = new DemoDirectX(my_image);
            //
            // // Display an image
            // demo4.DisplayImage("dev_img1.jpg");


           

        }



    }



    private void play_video()
        {
            
              try
                        {
                            ffmpeg = new Src.FFmpeg();
                            // directX = new DirectX(my_image.SurfaceHandle);
                        
                            if (!ffmpeg.InitHWAccel(directX._device)) 
                            { 
                                MessageBox.Show("Failed to Initialize FFmpeg's HW Acceleration"); 
                                return; 
                            }
                        
                            if (!ffmpeg.Open(fileToPlay)) 
                            { 
                                MessageBox.Show("FFmpeg failed to open input"); 
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
                                        // FFmpeg HW Decode Frame
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
                                            My_Store.Instance.SetVideoResolution(textureHW.Description.Width,textureHW.Description.Height);
                                                
                                            }
                                            
                                            
                                        
                                        }
                                        
                                        // DirectX HW Process & Present Frame
                                        directX.PresentFrame(textureHW);
                                        
                                        
                                        sw.Stop(); // Stop measuring after presenting
                                        
                                        // 3️⃣ Print time in milliseconds
                                        // Console.WriteLine($"Frame time (decode + render): {sw.Elapsed.TotalMilliseconds:F2} ms");
                                        
                        
                                        Thread.Sleep(16); // Simulates FPS
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
                            // MessageBox.Show($"Failed to initialize: {ex.Message}");
                        }
                        
                             
            
            
        }
        
        
    
    
    private void my_info_propertychanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // case nameof(My_Store.VideoWidth):
            
                
            case nameof(my_info.TakeScreenshot):

                
                    
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // ShowToast("typing mode changed");
                
                    if (my_info.Instance.TakeScreenshot)
                    {
                        // AnimateModeOverlay("keyboard");
                        
                        if (my_image.Surface != null)
                        {
                            // Console.WriteLine("Surface already exists");
                                        
                            var w = my_image.Surface.ActualWidth;
                            var h = my_image.Surface.ActualHeight;
                          
                           
                            // directX?.HandleResize();
                            
                            
                            // var demo4 = new DirectX(my_image.SurfaceHandle); 
                            
                            // directX = new DirectX(my_image.SurfaceHandle);
                            
                            
                            // Fill container (may crop)
                            // var demo4 = new DirectXWpf(my_image.SurfaceHandle); // Fill container (may crop)
                            // _demoDirectX = new DemoDirectX(my_image);
                
                            // Display an image
                            // demo4.DisplayImage("dev_img1.jpg");
                
                
                            // directX = new DirectX(PlayerHost.SurfaceHandle);
                            //
                            // directX.DisplayImage("dev_img1.jpg");
                        }
                
                        Console.WriteLine("reseting takescreenshot");
                
                        my_info.Instance.TakeScreenshot = false;
                
                    }
                    else
                    {
                        // AnimateModeOverlay("gaming");
                    }
                        
                        
                });
                

                    
    
                break;
                
            case nameof(my_info.Window_resizing):

                if (!my_info.Instance.Window_resizing)
                {
                    Console.WriteLine("finished resizing #####");
                    
                    // Application.Current.Dispatcher.Invoke(() =>
                    // {
                    //     // ShowToast("typing mode changed");
                    //
                    //
                    //
                    //     directX?.HandleResize();
                    //
                    // });
                    
                    
                    Application.Current.Dispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            // directX?.ResizeSwapChain(
                            //     (int)my_image.ActualWidth,
                            //     (int)my_image.ActualHeight);

                            Console.WriteLine($" Display view {my_image.Surface.Width}, {my_image.Surface.Height}");
                            
                            // Console.WriteLine(my_image.Overlay.Width);
                            
                            
                            // my_image.Visibility = Visibility.Visible;

                            // Console.WriteLine(my_image.SurfaceHandle);

                            if (my_image != null)
                            {
                                k_info.Instance.directx?.ResizeSwapChain((int)my_image.Surface.Width, (int)my_image.Surface.Height);
                                 
                                
                                // directX?.ResizeSwapChain(496, 1088);
                                // k_info.Instance.directx?.ResizeSwapChain(496, 1088);

                                if (my_info.Instance.DeveloperMode)
                                {
                                    
                                    k_info.Instance.directx?.HandleResize();
                                }
                                
                            }
                            
                         

                            // del_mm();
                            
                        }),
                        DispatcherPriority.Render);
                }
                break;
        }
    }


    private void del_mm()
    {
        
        
        
        
        
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (my_image.Surface != null)
            {
                // Console.WriteLine("Surface already exists");
                                        
                var w = my_image.Surface.ActualWidth;
                var h = my_image.Surface.ActualHeight;
                                
                Console.WriteLine($"Surface size FIXED: {w} x {h}");
                                
                Console.WriteLine($"INIT DX SIZE: {w} x {h}");
                // var demo4 = new DirectX(my_image.SurfaceHandle); // Fill container (may crop)
                // demo4.DisplayImage("dev_img1.jpg");
                
                

                // _demoDirectX = new DemoDirectX(my_image);

                // Display an image

                
                // directX = new DirectX(my_image.SurfaceHandle); // Fill container (may crop)
                
                
                k_info.Instance.directx.DisplayImage("dev_img1.jpg");
                // k_info.Instance.directx.DisplayImage("dev_img2.jpg");
                
                
                
                
                // play_video();
                
                
                // Texture2D frame =  directX.LoadTextureFromFile("dev_img2.jpg");
                
                
                
                // directX = new DirectX(PlayerHost.SurfaceHandle);
                //
                // directX.DisplayImage("dev_img1.jpg");
            }
            // Your UI update code here
            // Example: Update UI elements
        }, System.Windows.Threading.DispatcherPriority.Loaded);

    }

    private void My_adb_workerOndevicedisconnected()
    {
        if (scrcpy_worker != null)
        {
            scrcpy_worker.Dispose();
            scrcpy_worker = null;
        }
        my_app_worker_ProgressChanged(0, "No device found. please reconnect your device");
        
        Home.Instance.Dispatcher.Invoke(() =>
        {
                    
            Home.Instance.loadingpage.Visibility = Visibility.Visible;
                                
            Home.Instance.displayView.Visibility = Visibility.Collapsed;
                    
            // Console.WriteLine($"from My_adb_workerOnCountingCompleted  displayview width : {Home.Instance.displayView.Width}");
                                
            
        });

        my_info.Instance.Auto_resizing = true;
        
        
    }

   

    private void my_app_worker_Completed()
    {
        

        // Home.Instance.displayView.Visibility = Visibility.Collapsed;

        Home.Instance.Dispatcher.Invoke(() =>
        {

            
            
            Home.Instance.loadingpage.Visibility = Visibility.Collapsed;

            Home.Instance.displayView.Visibility = Visibility.Visible;

            // my_image.Surface.Width = 1280;
            // my_image.Surface.Height = 720;
            // my_image.Overlay.Width = 1280;
            // my_image.Overlay.Height = 720;
            //
            del_mm();
            
            // play_video();

            Console.WriteLine("displayview ready ##################");

       

            OverlayManager.Instance.rerender_overlay();
        });

    }

    private void my_app_worker_ProgressChanged(int num, string status)
    {
        Home.Instance.Dispatcher.Invoke(() => { Home.Instance.loadingpage.UpdateProgress((double)num, status); });

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

        // scrcpy_worker.dx_Device = directX.Device11;
        scrcpy_worker.dx_Device = k_info.Instance.directx?.my_Device;
        
        scrcpy_worker.Frame_almostready += Scrcpy_workerOnFrame_almostready;
        scrcpy_worker.videosizeReady += on_videosizeready;
        scrcpy_worker.ControlSocketReady += on_ControlSocketReady;
        scrcpy_worker.DeviceResolutionReady += on_DeviceResolutionReady;
        // scrcpy_worker.FrameReady += onframe_ready;
        // scrcpy_worker.ErrorOccurred += general_error;

        scrcpy_worker.FrameReady += del_display_frame;

        scrcpy_worker.scrcpy_desposed += My_adb_workerOnCountingCompleted;



        scrcpy_worker.Start();



       
    }

    private void Scrcpy_workerOnFrame_almostready()
    {
        Home.Instance.Dispatcher.Invoke(() =>
        {
        
            Home.Instance.loadingpage.Visibility = Visibility.Collapsed;
                   
            Home.Instance.displayView.Visibility = Visibility.Visible;
            
            // 416, 920

      
            // My_Store.Instance.SetVideoResolution(488, 1080);
            // Console.WriteLine($"Frame displayed: {my_image.Width}x{my_image.Height}");
        
            // Console.WriteLine($"from My_adb_workerOnCountingCompleted  displayview width : {Home.Instance.displayView.Width}");
                   
            OverlayManager.Instance.rerender_overlay();
        });
    }



    private void on_DeviceResolutionReady((int Width, int Height) div)
    {
        My_Store.Instance.SetDeviceResolution(div.Width, div.Height);
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






    private void del_display_frame(Texture2D frame)
    {
        try
        {
            if (My_Store.Instance.DisplayHeight == 0 || My_Store.Instance.DisplayHeight != (int)my_image.ActualWidth)
            {
                My_Store.Instance.SetDisplayResolution((int)my_image.ActualWidth, (int)my_image.ActualHeight);
            }

            
            
            if (My_Store.Instance.VideoHeight == 0 || My_Store.Instance.VideoHeight == 0)
            {
                My_Store.Instance.SetVideoResolution(frame.Description.Width, frame.Description.Height);
            }

            if (My_Store.Instance?.DeviceHeight == 0 ||
                My_Store.Instance?.DeviceWidth == 0 && my_info.Instance.DeveloperMode)
            {
                My_Store.Instance.SetDeviceResolution(frame.Description.Width, frame.Description.Height);
            }

            // Ensure DemoDirectX resources match frame size
            // directX.EnsureResources(frame.Description.Width, frame.Description.Height);
            //
            // // Copy frame to shared texture
            // directX.Device11.ImmediateContext.CopyResource(frame, directX.SharedTexture);
            // directX.Device11.ImmediateContext.Flush();
            //
            // // Update D3DImage using DemoDirectX's method
            // directX.UpdateD3DImage();

            if (frame == null || frame.IsDisposed)
            {
                Console.WriteLine("del_display_frame: Frame is null or disposed");
                return;
            }


            

            // directX.DisplayTexture(frame);
            k_info.Instance.directx?.PresentFrame(frame);

            // Console.WriteLine($"Frame displayed: {frame.Description.Width}x{frame.Description.Height}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"del_display_frame error: {ex.Message}");
        }
    }


    private void onframe_ready(Texture2D frame)
    {


        try
        {
            if (My_Store.Instance.DisplayHeight == 0 || My_Store.Instance.DisplayHeight != (int)my_image.ActualWidth)
            {
                My_Store.Instance.SetDisplayResolution((int)my_image.ActualWidth, (int)my_image.ActualHeight);
            }

            if (My_Store.Instance.VideoHeight == 0 || My_Store.Instance.VideoHeight == 0)
            {
                My_Store.Instance.SetVideoResolution(frame.Description.Width, frame.Description.Height);
            }

            if (My_Store.Instance?.DeviceHeight == 0 ||
                My_Store.Instance?.DeviceWidth == 0 && my_info.Instance.DeveloperMode)
            {



                My_Store.Instance.SetDeviceResolution(frame.Description.Width, frame.Description.Height);

                // Console.WriteLine($"{frame.Description.Width} : {frame.Description.Height}");
                //
                // Console.WriteLine("in app manager printing... size");
            }

            // Ensure DirectX resources match frame size
            // directX.EnsureResources(frame.Description.Width, frame.Description.Height);
            //
            // directX.ProcessFrame(frame);

            
            
            
            // // Update D3DImage
            // _d3dImage.Dispatcher.Invoke(() =>
            // {
            //     _d3dImage.Lock();
            //     _d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9,
            //         directX.SharedTexture9.GetSurfaceLevel(0).NativePointer);
            //     _d3dImage.AddDirtyRect(new Int32Rect(0, 0, frame.Description.Width, frame.Description.Height));
            //     _d3dImage.Unlock();
            //
            //
            // });


            // SharpDX.Utilities.Dispose(ref frame);
        }
        catch (ThreadAbortException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"VideoLoop error: {ex.Message}");
        }




    }

    public void Dispose()
    {
        is_running = false;

        threadPlay.Join();
        
        

    }
}
    