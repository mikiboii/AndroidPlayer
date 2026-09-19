using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
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
using Avalonia.Platform;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;

namespace Androidplayer;

public partial class Home : Window
{
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

    private keymap_worker my_keymap_worker;

    private bool sidebarVisible = false;

    public static Home? Instance { get; private set; }

    private bool already_activated = false;
    
    private DispatcherTimer _resizeTimer;
    
    private  String currently_active = null;
    
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

        string _filePath = Path.Combine(Environment.CurrentDirectory, "user", "data.db");

        my_info.Instance.Dataeditor = new LiteDbEditor(_filePath, new LiteDbEditorOptions { Autosave = true });
        StartupTimer.Mark("  LiteDbEditor done");

        InitializeComponent();
        StartupTimer.Mark("  InitializeComponent done");

        Instance = this;

        rawInputHandler = new handle_rawinput(this);
        StartupTimer.Mark("  rawInputHandler done");

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
        

        Loaded += MainImageOnLoaded;
        k_info.Instance.PropertyChanged += K_info_changed;

        
        _resizeTimer = new DispatcherTimer();
        _resizeTimer.Interval = TimeSpan.FromMilliseconds(200);
        _resizeTimer.Tick += ResizeTimer_Tick;
        StartupTimer.Mark("Home ctor end");
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
        StartupTimer.Mark("Home Loaded");
        ShowSidebar();

        sidebarWindow?.Activate();
        this.Activate();

        // keymapWindow = new Keymap_Window(this);
        
        
        StartupTimer.Mark("Sidebar shown");
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
        if (!_isFullscreen)
        {
            // Save current size and position
            _restoreBounds = new Rect(this.Position.X, this.Position.Y, this.Width, this.Height);

            // In Avalonia: hide decorations, disable resize, cover screen
            SystemDecorations = SystemDecorations.None;
            CanResize = false;

            Position = new PixelPoint(0, 0);

            var screen = Screens.Primary;
            if (screen != null)
            {
                Width = screen.Bounds.Width;
                Height = screen.Bounds.Height;
            }

            _isFullscreen = true;

            CustomTitleBar.IsVisible = false;
        }
        else
        {
            // Restore
            SystemDecorations = SystemDecorations.Full;
            CanResize = true;

            Position = new PixelPoint((int)_restoreBounds.X, (int)_restoreBounds.Y);
            Width = _restoreBounds.Width;
            Height = _restoreBounds.Height;

            _isFullscreen = false;

            CustomTitleBar.IsVisible = true;
        }
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
}