using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Androidplayer.Rendering.win;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Androidplayer.Src.Keyboard;
using Androidplayer.Src.Keymap;
using Androidplayer.Src.Keymap.K_store;
using Androidplayer.Src.Rawinput;
using Androidplayer.Store;
using Androidplayer.windows;
using Androidplayer.Src;
using Androidplayer.Src.Android;
using Avalonia.Media;
using Avalonia.Platform;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;

namespace Androidplayer;

public partial class Home : Window
{
    
    
    // windows 7 code 
    
    
    
    // windows 7 code 
    
    
    
    private Rect _restoreBounds; // store original window size/position
    
    
    
    private bool _isFullscreen = false;
    
   

    private const byte TYPE_INJECT_KEYCODE = 0x00;
    private const byte TYPE_INJECT_TEXT = 0x01;

    public const int KEYCODE_HOME = 3;
    public const int KEYCODE_BACK = 4;
    public const int KEYCODE_APP_SWITCH = 187;

    // Key action constants
    private const byte ACTION_DOWN = 0x00;
    private const byte ACTION_UP = 0x01;

    // MetaState constants (shift, ctrl, etc.)
    private const int META_NONE = 0x0;

    private handle_rawinput rawInputHandler;

    private Gaming_Keyboard gaming_keyboard;
    private Normal_Keyboard normal_keyboard;

    private SidebarWindow sidebarWindow;

    private Keymap_Window keymapWindow;

    private settings _settingsWindow;
    
    public SplashScreen splashScreen;

    private keymap_worker my_keymap_worker;

    private bool sidebarVisible = false;

    public static Home? Instance { get; private set; }

    private bool already_activated = false;
    
    private DispatcherTimer _resizeTimer;
    
    public DispatcherTimer _NativeTimer;

    
    private  String currently_active = null;
    
    public Control? displayView; 
    
    
//
//     public Home()
//     {
//         StartupTimer.Mark("Home ctor start");
// // ... each significant step ...
//         
//         string _filePath = Path.Combine(Environment.CurrentDirectory, "user", "data.db");
//
//         my_info.Instance.Dataeditor = new LiteDbEditor(_filePath, new LiteDbEditorOptions { Autosave = true });
//
//         InitializeComponent();
//
//         Instance = this;
//
//         rawInputHandler = new handle_rawinput(this);
//
//         if (_settingsWindow == null)
//             _settingsWindow = new settings();
//
//         keymap_worker.GetInstance().StartWorker();
//
//         gaming_keyboard = new Gaming_Keyboard();
//         normal_keyboard = new Normal_Keyboard();
//
//         this.Closed += Home_OnClosed;
//         this.Activated += home_activated;
//
//         // displayView.MainImage.Loaded += MainImageOnLoaded;
//         // displayView.Loaded += MainImageOnLoaded;
//
//         
//         Loaded += MainImageOnLoaded;
//         k_info.Instance.PropertyChanged += K_info_changed;
//         
//         StartupTimer.Mark("Home ctor end");
//     }



    public Home()
    {
        StartupTimer.Mark("Home ctor start");

        // string _filePath = Path.Combine(Environment.CurrentDirectory, "user", "data.db");
        
        string _filePath = Path.Combine(AppContext.BaseDirectory, "user", "data.db");

        my_info.Instance.Dataeditor = new LiteDbEditor(_filePath, new LiteDbEditorOptions { Autosave = true });
        StartupTimer.Mark("  LiteDbEditor done");

        InitializeComponent();
        StartupTimer.Mark("  InitializeComponent done");
        
       // show_splashscreen();

        // Background = "#FF00FF";

        // Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0xFF));
        Instance = this;

        #if WINDOWS
        
        
        rawInputHandler = new handle_rawinput(this);
        StartupTimer.Mark("  rawInputHandler done");
        #endif

        if (_settingsWindow == null)
            _settingsWindow = new settings();
        StartupTimer.Mark("  settings done");

        keymap_worker.GetInstance().StartWorker();
        StartupTimer.Mark("  keymap_worker done");

        gaming_keyboard = new Gaming_Keyboard();
        normal_keyboard = new Normal_Keyboard();
        StartupTimer.Mark("  keyboards done");

        this.Closed += Home_OnClosed;
        this.Activated += home_activated;
        this.Deactivated += OnDeactivated;
        
        
        this.PropertyChanged += Home_PropertyChanged;


        my_info.Instance.Nativeview_mode_local = UISettings.Instance.Nativeview_mode;
        
        
        
        if (UISettings.Instance.Nativeview_mode && OperatingSystem.IsWindows())
        {
            // Native overlay path
            displayView = new Display_view
            {
                
            };
        }
        else
        {
            
            displayView = new Gpuintrop_view
            {
                
            };
            
        }
        
        Grid.SetRow(displayView, 1);
        int insertAt = RootGrid.Children.IndexOf(loadingpage);
        if (insertAt < 0) insertAt = RootGrid.Children.Count;
        RootGrid.Children.Insert(insertAt, displayView);

        Loaded += MainImageOnLoaded;
        k_info.Instance.PropertyChanged += K_info_changed;

        
        _resizeTimer = new DispatcherTimer();
        _resizeTimer.Interval = TimeSpan.FromMilliseconds(200);
        _resizeTimer.Tick += ResizeTimer_Tick;
        
        
        _NativeTimer = new DispatcherTimer();
        _NativeTimer.Interval = TimeSpan.FromMilliseconds(200);
        _NativeTimer.Tick += NativeTimer_Tick;
        StartupTimer.Mark("Home ctor end");
    }

    private void NativeTimer_Tick(object? sender, EventArgs e)
    {
        _NativeTimer.Stop();
    
           
    
        var resetTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        resetTimer.Tick += (s, args) =>
        {
            resetTimer.Stop();


            
            // Home.Instance.displayView.MainImage.IsVisible = true;
            var mainImage = displayView?.FindControl<Native_view>("MainImage");
            mainImage._floatingContent.Background =  Brushes.Transparent;
            // Home.Instance.displayView.MainImage._floatingContent.Background = Brushes.Transparent;
            // k_info.Instance.directx.HandleResize();

            k_info.Instance.my_renderer?.HandleResize();

            Console.WriteLine("native window finished");

            
        };
        resetTimer.Start();
    }


    private void Home_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            var oldState = (WindowState)e.OldValue!;
            var newState = (WindowState)e.NewValue!;

            Console.WriteLine($"WindowState changed: {oldState} -> {newState}");

            switch (newState)
            {
                case WindowState.Minimized:
                    Console.WriteLine("got minimized");
                    
                    // Home.Instance.displayView.MainImage.IsVisible = false;
                    var mainImage = displayView?.FindControl<Native_view>("MainImage");
                    if (mainImage != null)
                    {
                        if (mainImage._floatingContent != null)
                        {
                                
                            mainImage._floatingContent.Background = Brushes.Black;
                        }
                    }
                    // Home.Instance.displayView.MainImage._floatingContent.Background = Brushes.Black;
                    break;

                case WindowState.Maximized:
                    
                    break;

                case WindowState.Normal:
                    Console.WriteLine("got Normal");
                    
                    var mainImage2 = displayView?.FindControl<Native_view>("MainImage");
                    if (mainImage2 != null)
                    {
                        if (mainImage2._floatingContent != null)
                        {
                                
                            mainImage2._floatingContent.Background = Brushes.Black;
                        }
                    }
                    // Home.Instance.displayView.MainImage._floatingContent.Background = Brushes.Transparent;
                    
                    _NativeTimer.Stop();
                    _NativeTimer.Start();
                    
                    
                    break;
            }
        }
    }
    
    
    
    private void OnDeactivated(object? sender, EventArgs e)
    {
        // if (keymapWindow?.IsVisible == true)
        // {
        //     
        //     
        //     
        //     // keymapWindow.Topmost = true;
        //     keymapWindow.Topmost = false;
        //         
        // }
    }


    private void K_info_changed(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(k_info.KeymapMode):
                Dispatcher.UIThread.Post(() =>
                {
                    if (k_info.Instance.KeymapMode)
                    {
                        // ShowKeymap_window();
                    }
                    else
                    {
                        // keymapWindow?.Hide();
                    }
                });
                break;
        }
    }

    private void MainImageOnLoaded(object? sender, RoutedEventArgs e)
    {
        
        // displayView.MainImage.IsVisible = false;
        // this.Hide();
        
        StartupTimer.Mark("Home Loaded");
        ShowSidebar();

        sidebarWindow?.Activate();
        this.Activate();

        // keymapWindow = new Keymap_Window(this);
        
        // Home.Instance.displayView.MainImage._floatingContent.Background = Brushes.Black;
        //
        // _NativeTimer.Stop();
        // _NativeTimer.Start();
        UISettings.Instance.PropertyChanged += UISettingsOnPropertyChanged;
        
        UISettings.Instance.RefreshCurrentCursor();
        StartupTimer.Mark("Sidebar shown");
    }

    private void UISettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UISettings.CurrentCursor))
        {
            // WPF: Application.Current.Dispatcher.Invoke(...)
            // Avalonia: Dispatcher.UIThread.Post(...) — fire-and-forget on UI thread
            Dispatcher.UIThread.Post(() =>
            {


                if (UISettings.Instance.CurrentCursor == "Default")
                {
                    this.Cursor = new Cursor(StandardCursorType.Arrow);
                }
                else
                {
                 
                    var cursorUri = new Uri(UISettings.Instance.CurrentCursor);

                    using var cursorStream = AssetLoader.Open(cursorUri);

                    var cursorBitmap = new Avalonia.Media.Imaging.Bitmap(cursorStream);

                    var hotSpot = new PixelPoint(0, 0);

                    Cursor customCursor = new Cursor(cursorBitmap, hotSpot);

                    this.Cursor = customCursor;
                    
                }
                
               
            });
        }
    }


    
    
    
    
    private void my_store_propertychanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(My_Store.VideoResolution):

                var screen = Screens.Primary;
                if (screen == null) break;

                double screenWidth = screen.Bounds.Width;
                double screenHeight = screen.Bounds.Height;

                double videoWidth = My_Store.Instance.VideoWidth;
                double videoHeight = My_Store.Instance.VideoHeight;

                const double scaleFactor = 0.7;

                double aspect = videoWidth / videoHeight;

                double targetWidth;
                double targetHeight;

                if (!my_info.Instance.Auto_resizing)
                {
                    break;
                }

                if (My_Store.Instance.VideoWidth > My_Store.Instance.VideoHeight)
                {
                    targetWidth = screenWidth * scaleFactor;
                    targetHeight = targetWidth / aspect;

                    if (targetHeight > screenHeight * scaleFactor)
                    {
                        targetHeight = screenHeight * scaleFactor;
                        targetWidth = targetHeight * aspect;
                    }
                }
                else
                {
                    targetHeight = screenHeight * scaleFactor;
                    targetWidth = targetHeight * aspect;

                    if (targetWidth > screenWidth * scaleFactor)
                    {
                        targetWidth = screenWidth * scaleFactor;
                        targetHeight = targetWidth / aspect;
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    this.Width = targetWidth;
                    this.Height = targetHeight;

                    this.Position = new PixelPoint(
                        (int)((screenWidth - targetWidth) / 2),
                        (int)((screenHeight - targetHeight) / 2));

                    my_info.Instance.Auto_resizing = false;

                    // this.displayView.ScaleFormToFit((int)videoWidth, (int)videoHeight);
                });

                break;
        }
    }

    private void Home_OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Custom cursor setup (uncomment if you want it)
        // var cursorUri = new Uri("avares://Androidplayer/Icons/cursor/cursor_new.cur");
        // using var stream = AssetLoader.Open(cursorUri);
        // this.Cursor = new Cursor(stream);
    }

    
     private void ResizeTimer_Tick(object? sender, EventArgs e)
        {
            _resizeTimer.Stop();
    
           
    
            var resetTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            resetTimer.Tick += (s, args) =>
            {
                resetTimer.Stop();


                Console.WriteLine("resize finished");
                
                if (k_info.Instance.my_renderer is DirectX dx && my_info.Instance.Nativeview_mode_local)
                {
            
                    if (dx != null)
                    {
                
                        dx.RunOnContext(_ =>
                        {
                            // dx.ResizeToClient(MainImage.NativeHandle);
                            // dx.ResizeSwapChain((int)_restoreBounds.Width , (int)_restoreBounds.Height);

                            dx.HandleResize();

                        });
                    }
                }

                // Console.WriteLine($"window activation finished {currently_active}" );
                currently_active = null;
            };
            resetTimer.Start();
        }
    private void home_activated(object? sender, EventArgs e)
    {
        // if (!already_activated)
        // {
        //
        //     Console.WriteLine("Home activated called");
        //     sidebarWindow?.Activate();
        //     this.Activate();
        //
        //     keymapWindow?.Activate();
        //
        //     already_activated = true;
        //
        //     // var cursorUri = new Uri("avares://Androidplayer/Icons/cursor/black_sword.cur");
        //     // using var stream = AssetLoader.Open(cursorUri);
        //     // Cursor customCursor = new Cursor(stream);
        //     //
        //     // this.Cursor = customCursor;
        //     //
        //     // if (sidebarWindow != null)
        //     // {
        //     //     sidebarWindow.Cursor = customCursor;
        //     // }
        //     
        //     
        //     
        //     
        // }
        
        _resizeTimer.Stop();
        _resizeTimer.Start();

        if (currently_active == null)
        {
            
            currently_active = "Home";
            Console.WriteLine("activating in home");
            // sidebarWindow?.Activate();
            // keymapWindow?.Activate();

            if (sidebarWindow?.IsVisible == true)
            {
                sidebarWindow.Topmost = true;
                sidebarWindow.Topmost = false;
                
            }
            
            // if (keymapWindow?.IsVisible == true)
            // {
            //     keymapWindow.Topmost = true;
            //     // keymapWindow.Topmost = false;
            //     
            // }
            // keymapWindow?.Activate();
        }
        else
        {
            
            
            
        }
        
        
        
        
        
    }

    
    
    #region Sidebar_window

    // private void KeymapWindowOnActivated(object? sender, EventArgs e)
    // {
    //     _resizeTimer.Stop();
    //     _resizeTimer.Start();
    //
    //     if (currently_active == null)
    //     {
    //         
    //         currently_active = "keymap";
    //         
    //         if (sidebarWindow?.IsVisible == true)
    //         {
    //             sidebarWindow.Topmost = true;
    //             sidebarWindow.Topmost = false;
    //             
    //         }
    //         
    //         if (keymapWindow?.IsVisible == true)
    //         {
    //             keymapWindow.Topmost = true;
    //             keymapWindow.Topmost = false;
    //             
    //         }
    //     }
    // }
    //
    //
    private void SidebarWindowOnActivated(object? sender, EventArgs e)
    {
        _resizeTimer.Stop();
        _resizeTimer.Start();

        if (currently_active == null)
        {
            
            currently_active = "sidebar";
            
            if (sidebarWindow?.IsVisible == true)
            {
                sidebarWindow.Topmost = true;
                sidebarWindow.Topmost = false;
                
            }
            
            // if (keymapWindow?.IsVisible == true)
            // {
            //     keymapWindow.Topmost = true;
            //     keymapWindow.Topmost = false;
            //     
            // }
        }
    }
    
    public void SendAndroidKeycode(int keycode)
    {
        SendKey(keycode, ACTION_DOWN);
        SendKey(keycode, ACTION_UP);
    }

    public void SendRotateDevice()
    {
        byte[] msg = new byte[1];
        msg[0] = 0x0B;

        Console.WriteLine("rotate device clicked");

        SendData(msg);
    }

    public void SendKey(int keycode, byte action, int metastate = META_NONE)
    {
        byte[] down = new byte[14];
        down[0] = TYPE_INJECT_KEYCODE;
        down[1] = action;
        BinaryPrimitives.WriteInt32BigEndian(down.AsSpan(2, 4), keycode);
        BinaryPrimitives.WriteInt32BigEndian(down.AsSpan(6, 4), 0); // repeat
        BinaryPrimitives.WriteInt32BigEndian(down.AsSpan(10, 4), metastate);
        SendData(down);
    }

    private void SendData(byte[] data)
    {
        TcpClient controlSocket = My_Store.Instance.ControlSocket;

        if (controlSocket != null && controlSocket.Connected)
        {
            try
            {
                Socket socket = controlSocket.Client;

                socket.NoDelay = true;
                socket.Blocking = false;
                socket.Send(data, 0, data.Length, SocketFlags.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending direction data: {ex.Message}");
            }
        }
    }

    private void ShowKeymap_window()
    {
        // if (keymapWindow == null || !keymapWindow.IsVisible)
        // {
        //     keymapWindow = new Keymap_Window(this);
        //     keymapWindow.Activated += KeymapWindowOnActivated;
        // }
        //
        // keymapWindow.Show();
    }

  
    private void ShowSidebar()
    {
        if (sidebarWindow == null || !sidebarWindow.IsVisible)
        {
            sidebarWindow = new SidebarWindow(this);
            sidebarWindow.Closed += (s, e) => { sidebarVisible = false; };

            sidebarWindow.SidebarButtonClicked += SidebarWindowOnSidebarButtonClicked;
            
            sidebarWindow.Activated += SidebarWindowOnActivated;
        }

        sidebarWindow.Show();
        sidebarVisible = true;
    }

    
   

    private void SidebarWindowOnSidebarButtonClicked(string tag)
    {
        switch (tag)
        {
            case "Screenshot":
                my_info.Instance.TakeScreenshot = true;
                break;
            case "Record":
                
                my_info.Instance.Toggle_Recording_mode();
                
                break;
            case "Keyboard":
                break;
            case "Keymap":
                k_info.Instance.Toggle_Keymap_mode();
                break;

            case "Settings":
                if (_settingsWindow == null)
                    _settingsWindow = new settings();
              

                if (_settingsWindow.IsVisible)
                {
                    _settingsWindow.Activate();
                }
                else
                {
                    _settingsWindow.Show();
                    _settingsWindow.Activate();
                }
                return;

            case "Rotate":
                SendRotateDevice();
                break;
            case "Recent_apps":
                SendAndroidKeycode(KEYCODE_APP_SWITCH);
                break;
            case "Home":
                SendAndroidKeycode(KEYCODE_HOME);
                break;
            case "Back":
                SendAndroidKeycode(KEYCODE_BACK);
                break;
        }

        if (!this.IsActive)
        {
            // this.Activate();
        }

        // Console.WriteLine("sidebar buttons");
    }

    private void CloseSidebar()
    {
        sidebarWindow?.Close();
        // keymapWindow?.Close();
        sidebarVisible = false;
    }

    #endregion

    private void Home_OnClosed(object? sender, EventArgs e)
    {
        rawInputHandler?.Dispose();
        

        // keymapWindow?.Close(); 
        sidebarWindow?.Close();
        _settingsWindow?.Close();
        sidebarVisible = false;
    }

    private void ToggleFullscreen()
    {
        if (!my_info.Instance.IsFullscreen)
        {
            // Save current size and position
            _restoreBounds = new Rect(this.Position.X, this.Position.Y, this.Width, this.Height);

            // In Avalonia: hide decorations, disable resize, cover screen
            SystemDecorations = SystemDecorations.None;
            CanResize = false;

            // Position = new PixelPoint(0, 0);

            var screen = Screens.Primary;
            if (screen != null)
            {
                // D11InteropRenderer.Instance?.ResizeSwapChain(screen.Bounds.Width, screen.Bounds.Height);
                
                // Width = screen.Bounds.Width;
                // Height = screen.Bounds.Height;
                
                this.WindowState = WindowState.FullScreen;
                
                               
#if WINDOWS
                if (k_info.Instance.my_renderer is DirectX dx && my_info.Instance.Nativeview_mode_local)
                {
            
                    if (dx != null)
                    {
                
                        dx.RunOnContext(_ =>
                        {
                            // dx.ResizeToClient(MainImage.NativeHandle);
                            dx.ResizeSwapChain((int)screen.Bounds.Width , (int)screen.Bounds.Height);

                            dx.HandleResize();

                        });
                    }
                }
            
#endif


            }

            my_info.Instance.IsFullscreen = true;
            
            

            // CustomTitleBar.IsVisible = false;
        }
        else
        {
            // Restore
            SystemDecorations = SystemDecorations.Full;
            CanResize = true;

            // Position = new PixelPoint((int)_restoreBounds.X, (int)_restoreBounds.Y);
            // // D11InteropRenderer.Instance?.ResizeSwapChain((int)_restoreBounds.Width, (int)_restoreBounds.Height);
            //
            // Width = _restoreBounds.Width;
            // Height = _restoreBounds.Height;
            
            this.WindowState = WindowState.Normal;
                
#if WINDOWS
            if (k_info.Instance.my_renderer is DirectX dx && my_info.Instance.Nativeview_mode_local)
            {
            
                if (dx != null)
                {
                
                    dx.RunOnContext(_ =>
                    {
                        // dx.ResizeToClient(MainImage.NativeHandle);
                        dx.ResizeSwapChain((int)_restoreBounds.Width , (int)_restoreBounds.Height);

                        dx.HandleResize();

                    });
                }
            }
            
#endif

            
            
            

            my_info.Instance.IsFullscreen = false;

            // CustomTitleBar.IsVisible = true;
            
        }
    }


    protected override void OnResized(WindowResizedEventArgs e)
    {
        base.OnResized(e);
        
        if (k_info.Instance.my_renderer is DirectX dx && my_info.Instance.Nativeview_mode_local)
        {
            
            if (dx != null)
            {
                
                dx.RunOnContext(_ =>
                {
                    // dx.ResizeToClient(MainImage.NativeHandle);
                    // dx.ResizeSwapChain((int)_restoreBounds.Width , (int)_restoreBounds.Height);

                    dx.HandleResize();

                });
            }
        }
    }

    private async void show_splashscreen()
    {
        splashScreen = new SplashScreen();
        splashScreen.Show();
        
        
        
    }
    

    public void restore_Home()
    {
        
        // splashScreen?.Close();

        Console.WriteLine("restore home called");
        
        this.Opacity = 1;
        this.IsVisible = true;
        this.ShowInTaskbar = true;
        this.WindowState = WindowState.Normal;
        
        scale_mainwindow();

        my_info.Instance.Restored_window = true;

    }
    
    public void scale_mainwindow()
    {
        var screen = TopLevel.GetTopLevel(this)?.Screens.Primary;
        if (screen == null) return;

        // Screen size in DIPs
        double screenWidth  = screen.Bounds.Width  / screen.Scaling;
        double screenHeight = screen.Bounds.Height / screen.Scaling;

        double videoWidth  = My_Store.Instance.VideoWidth;
        double videoHeight = My_Store.Instance.VideoHeight;
        if (videoWidth <= 0 || videoHeight <= 0) return;

        const double scaleFactor = 0.8;
        const double titleBarHeight = 30;

        double aspect = videoWidth / videoHeight;

        double targetWidth, targetHeight;

        if (videoWidth > videoHeight)
        {
            targetWidth  = screenWidth * scaleFactor;
            targetHeight = targetWidth / aspect;

            if (targetHeight > screenHeight * scaleFactor)
            {
                targetHeight = screenHeight * scaleFactor;
                targetWidth  = targetHeight * aspect;
            }
        }
        else
        {
            targetHeight = screenHeight * scaleFactor;
            targetWidth  = targetHeight * aspect;

            if (targetWidth > screenWidth * scaleFactor)
            {
                targetWidth  = screenWidth * scaleFactor;
                targetHeight = targetWidth / aspect;
            }
        }

        if (Home.Instance != null)
        {
            double windowWidth  = targetWidth;
            double windowHeight = targetHeight + titleBarHeight;

            Home.Instance.Width  = windowWidth;
            Home.Instance.Height = windowHeight;

            // Center using the ACTUAL window dimensions, in DIPs
            double dipX = (screenWidth  - windowWidth)  / 2.0;
            double dipY = (screenHeight - windowHeight) / 2.0;

            // Convert to physical pixels for PixelPoint
            int pxX = (int)Math.Round(dipX * screen.Scaling);
            int pxY = (int)Math.Round(dipY * screen.Scaling);

            Home.Instance.Position = new PixelPoint(pxX, pxY);
        }

        my_info.Instance.Auto_resizing = false;
    }
    private void Home_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (k_info.Instance.KeymapMode)
        {
            return;
        }

        if (e.Key == Key.F12)
        {
            my_info.Instance.Toggle_typing_mode();
        }
        if (e.Key == Key.F9)
        {
            Console.WriteLine($"{WindowState} : {SystemDecorations}");

            ToggleFullscreen();
        }

        if (my_info.Instance.Typing_mode)
        {
            normal_keyboard.Key_pressed(this, e);
        }
        else
        {
            gaming_keyboard.Key_pressed(this, e);
        }
    }

    private void Home_OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (k_info.Instance.KeymapMode)
        {
            return;
        }

        if (my_info.Instance.Typing_mode)
        {
            normal_keyboard.Key_Released(this, e);
        }
        else
        {
            gaming_keyboard.Key_Released(this, e);
        }
    }

    private void Home_OnPreviewMouseDown(object? sender, PointerPressedEventArgs e)
    {
        var clickedElement = e.Source as Visual;

        if (clickedElement is TextBox)
            return;

        if (clickedElement?.GetType().Name == "TextBoxView")
        {
            return;
        }

        // Clear focus only if not on a TextBox
        if (sender is Window window)
        {
            window.Focus();
        }
        else if (sender is Control control)
        {
            var wnd = TopLevel.GetTopLevel(control) as Window;
            if (wnd != null)
            {
                wnd.Focus();
            }
        }
    }

    private void CustomTitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Console.WriteLine("pressed title bar");
        
        
        // if (keymapWindow?.IsVisible == true)
        // {
        //     keymapWindow.Topmost = true;
        //     // keymapWindow.Topmost = false;
        //         
        // }
    }

    private void Control_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        
    }

    // private void WindowBase_OnResized(object? sender, WindowResizedEventArgs e)
    // {
    //     Console.WriteLine("resized");
    // }
}