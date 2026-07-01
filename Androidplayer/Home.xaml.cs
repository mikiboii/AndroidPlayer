using System.Windows;
using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Net.Sockets;

using Androidplayer.Src;
using Androidplayer.Src.Android;
using Androidplayer.Src.Keyboard;
using Androidplayer.Src.Keymap;
using Androidplayer.Src.Keymap.K_store;
using Androidplayer.Src.Rawinput;
using Androidplayer.Store;
using Androidplayer.windows;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

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
    
    public Home()
    {
        
        
        string _filePath = Path.Combine(Environment.CurrentDirectory, "user", "data.db");
        
        
        my_info.Instance.Dataeditor =  new LiteDbEditor(_filePath, new LiteDbEditorOptions { Autosave = true });
        
        InitializeComponent();
        
        Instance = this;

        // My_Store.Instance.PropertyChanged += my_store_propertychanged;
        
        
        
        
        
        rawInputHandler = new handle_rawinput(this);
        
        
        if (_settingsWindow == null)
            _settingsWindow = new settings();
        
        // my_keymap_worker = new keymap_worker();
        // Example: after updating default keymap in LiteDB
        keymap_worker.GetInstance().StartWorker();

        
        
        
        // this.SourceInitialized += MyWindow_SourceInitialized;
        gaming_keyboard = new Gaming_Keyboard();

        normal_keyboard = new Normal_Keyboard();
        
        
        
        this.Closed += Home_OnClosed;

        this.Activated += home_activated;
        
        displayView.MainImage.Loaded += MainImageOnLoaded;
        
        
        k_info.Instance.PropertyChanged += K_info_changed;
        


    }

    private void K_info_changed(object? sender, PropertyChangedEventArgs e)
    {
        
        switch (e.PropertyName)
        {
            // case nameof(My_Store.VideoWidth):
            
                
            case nameof(k_info.KeymapMode):
    
                    
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // ShowToast("typing mode changed");
    
                    if (k_info.Instance.KeymapMode)
                    {
                        // AnimateModeOverlay("keyboard");
                        
                     ShowKeymap_window();
                  
    
                    }
                    else
                    {
                        // AnimateModeOverlay("gaming");
                        keymapWindow.Hide();
                    }
                        
                        
                });
    
    
                    
    
                break;
                
    
        }
        
        
        
        
    }

    private void MainImageOnLoaded(object sender, RoutedEventArgs e)
    {
        ShowSidebar();
       
        sidebarWindow.Activate();
        this.Activate();
        
        keymapWindow = new Keymap_Window(this);
    }

    private void my_store_propertychanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // case nameof(My_Store.VideoWidth):
            case nameof(My_Store.VideoResolution):
                
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;

                // Console.WriteLine($"Screen resolution: {screenWidth} x {screenHeight}");

                double videoWidth = My_Store.Instance.VideoWidth;
                double videoHeight = My_Store.Instance.VideoHeight;

                // Keep a safety scale factor (e.g., window occupies ~70% of screen)
                const double scaleFactor = 0.7;

                // Calculate aspect ratio
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

                    // Prevent it from being taller than screen
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

                    // Prevent it from being wider than screen
                    if (targetWidth > screenWidth * scaleFactor)
                    {
                        targetWidth = screenWidth * scaleFactor;
                        targetHeight = targetWidth / aspect;
                    }
                }
                
                
                
                Dispatcher.Invoke(() =>
                {
                    this.Width = targetWidth;
                    this.Height = targetHeight;

                    // Optionally center on screen
                    this.Left = (screenWidth - targetWidth) / 2;
                    this.Top = (screenHeight - targetHeight) / 2;

                    my_info.Instance.Auto_resizing = false;
                    
                    this.displayView.ScaleFormToFit((int)videoWidth, (int)videoHeight);
                   
                });
                
                break;

        }
    }

    private void Home_OnLoaded(object sender, RoutedEventArgs e)
    {
        // chrome.CaptionHeight = 0.1;
        
        // ShowSidebar();
        //
        // sidebarWindow.Activate();
        // this.Activate();
        
        // "pack://application:,,,/Icons/checkmark.png"
        
        
        
        
        // Uri cursorUri = new Uri("pack://application:,,,/Icons/cursor/sword_4.cur");
        // Uri cursorUri = new Uri("pack://application:,,,/Icons/cursor/cursor_new.cur");
        // Cursor customCursor = new Cursor(Application.GetResourceStream(cursorUri).Stream);
        //
        //
        //
        //
        // this.Cursor = customCursor;
        //
        // if (sidebarWindow != null)
        // {
        //     
        //     sidebarWindow.Cursor = customCursor;
        // }


    }
    
    private void home_activated(object? sender, EventArgs e)
    {
        // Console.WriteLine("### home activated");


        // sidebarWindow.Activate();
        // this.Activate();

        if (!already_activated)
        {
            // ShowSidebar();
            sidebarWindow?.Activate();
            this.Activate();

            keymapWindow?.Activate();

            already_activated = true;
            
            
            
            Uri cursorUri = new Uri("pack://application:,,,/Icons/cursor/black_sword.cur");
            Cursor customCursor = new Cursor(Application.GetResourceStream(cursorUri).Stream);


        

            this.Cursor = customCursor;

            if (sidebarWindow != null)
            {
            
                sidebarWindow.Cursor = customCursor;
            }
        }
        
        
        
        
        //
        // if (sidebarWindow != null && sidebarVisible)
        // {
        //     sidebarWindow.Topmost = false;
        //     sidebarWindow.Topmost = true; // Reset topmost to refresh z-order
        // }
    }


    #region Sidebar_window

    
    public void SendAndroidKeycode(int keycode)
    {
        
        // DOWN
        SendKey(keycode, ACTION_DOWN);

        // UP
        SendKey(keycode, ACTION_UP);
    }

    
    
    
    public void SendRotateDevice()
    {
        byte[] msg = new byte[1];
        msg[0] = 0x0B;


        Console.WriteLine("rotate device clicked");

        SendData(msg);
    }
    
    
    
    public  void SendKey(int keycode,byte action , int metastate = META_NONE)
    {
        // DOWN
        byte[] down = new byte[14];
        down[0] = TYPE_INJECT_KEYCODE;
        down[1] = action;
        BinaryPrimitives.WriteInt32BigEndian(down.AsSpan(2, 4), keycode);
        BinaryPrimitives.WriteInt32BigEndian(down.AsSpan(6, 4), 0); // repeat
        BinaryPrimitives.WriteInt32BigEndian(down.AsSpan(10, 4), metastate);
        SendData(down);

        // UP
        // byte[] up = new byte[14];
        // up[0] = TYPE_INJECT_KEYCODE;
        // up[1] = ACTION_UP;
        // BinaryPrimitives.WriteInt32BigEndian(up.AsSpan(2, 4), keycode);
        // BinaryPrimitives.WriteInt32BigEndian(up.AsSpan(6, 4), 0); // repeat
        // BinaryPrimitives.WriteInt32BigEndian(up.AsSpan(10, 4), metastate);
        // SendData(up);
    }

    
    private void SendData(byte[] data)
    {
        TcpClient controlSocket = My_Store.Instance.ControlSocket;

        // Console.WriteLine("from gaming keyboard");
        
        if (controlSocket != null && controlSocket.Connected)
        {
            try
            {
                Socket socket = controlSocket.Client;
                
                // OPTIMIZATION: Direct socket send with no delay
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
        
        
            
        if (keymapWindow == null || !keymapWindow.IsLoaded)
        {
            keymapWindow = new Keymap_Window(this);
            // keymapWindow.Closed += (s, e) => { sidebarVisible = false; };

            
            
           
        }
            
        
        keymapWindow.Show();
    }

    private void ShowSidebar()
    {
        if (sidebarWindow == null || !sidebarWindow.IsLoaded)
        {
            sidebarWindow = new SidebarWindow(this);
            sidebarWindow.Closed += (s, e) => { sidebarVisible = false; };
            
            sidebarWindow.SidebarButtonClicked += SidebarWindowOnSidebarButtonClicked;
        }
            
        sidebarWindow.Show();
        sidebarVisible = true;
    }

    private void SidebarWindowOnSidebarButtonClicked(string tag)
    {

        // Console.WriteLine(tag);
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
                    
                    _settingsWindow.Activate(); // bring to front
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
                
               
                break;
        }
        
        if (!this.IsActive)
        {
            this.Activate();
        }
    }

    private void CloseSidebar()
    {
        sidebarWindow?.Close();
        keymapWindow?.Close();
        sidebarVisible = false;
    }

    
    #endregion
    

    
    
    private void Home_OnClosed(object? sender, EventArgs e)
    {
        rawInputHandler?.Dispose();
        
        sidebarWindow?.Close();
        _settingsWindow?.Close();
        sidebarVisible = false;
    }
    
    
    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            // Save current size and position
            _restoreBounds = new Rect(this.Left, this.Top, this.Width, this.Height);

            // Remove borders and make topmost
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            // Topmost = true;

            // Cover the entire screen (including taskbar)
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;

            _isFullscreen = true;

            CustomTitleBar.Visibility = Visibility.Collapsed;
        }
        else
        {
            // Restore
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            // Topmost = false;

            Left = _restoreBounds.Left;
            Top = _restoreBounds.Top;
            Width = _restoreBounds.Width;
            Height = _restoreBounds.Height;

            _isFullscreen = false;
            
            CustomTitleBar.Visibility = Visibility.Visible;
            
        }
    }
    

    private void Home_OnKeyDown(object sender, KeyEventArgs e)
    {

        if (k_info.Instance.KeymapMode)
        {
            return;
        }
        
        
        if (e.Key == Key.F12)
        {

            // Console.WriteLine("changing...keyboared mode");
            
            
            my_info.Instance.Toggle_typing_mode();
        }
        if (e.Key == Key.F9)
        {

            // Console.WriteLine("changing...keyboared mode");

            Console.WriteLine($"{WindowState} : {WindowStyle}");
            //
            // if (WindowState == WindowState.Normal)
            // {
            //     WindowState = WindowState.Maximized;
            //     WindowStyle = WindowStyle.None;
            //     ResizeMode = ResizeMode.NoResize;
            //     // this.Topmost = true;
            // }
            // else
            // {
            //     WindowState = WindowState.Normal;
            //     WindowStyle = WindowStyle.SingleBorderWindow;
            //     ResizeMode = ResizeMode.CanResize;
            // }
            //
            
            ToggleFullscreen();
            
        }
        
        
        
        // if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        // {
        //     
        //    
        //         my_info.Instance.Toggle_mouseLock();
        //    
        //     
        // }



        if (my_info.Instance.Typing_mode)
        {
            normal_keyboard.Key_pressed(this , e);
        }
        else
        {
            gaming_keyboard.Key_pressed(this , e);
        }
                
          
                
        
            
       
        
        
        
        
        
        // e.Handled = true;
    }

    private void Home_OnKeyUp(object sender, KeyEventArgs e)
    {
        
        if (k_info.Instance.KeymapMode)
        {
            return;
        }
        
        
        if (my_info.Instance.Typing_mode)
        {
            normal_keyboard.Key_Released(this , e);
        }
        else
        {
            gaming_keyboard.Key_Released(this , e);
        }
        
        
      
         
                
        
        
    }

    

    private void Home_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var clickedElement = e.OriginalSource as DependencyObject;

        // Console.WriteLine(clickedElement.GetType().Name);
        
        
        // Fast visual walk to see if we clicked a TextBox or inside one
        if (clickedElement is TextBox )
            return;

        if (clickedElement.GetType().Name == "TextBoxView")
        {
            return;
        }
        // while (clickedElement != null)
        // {
        //     // Console.WriteLine("running....");
        //     if (clickedElement is TextBox)
        //         return; // Don't clear focus if it's inside a TextBox
        //     clickedElement = VisualTreeHelper.GetParent(clickedElement);
        // }

        // Console.WriteLine("stole focus");
        
        // Clear focus only if not on a TextBox
        if (sender is Window window)
        {
            window.Focus(); // Sets logical focus
            Keyboard.Focus(window); // Sets keyboard focus
        }
        else if (sender is FrameworkElement fe)
        {
            var wnd = Window.GetWindow(fe);
            if (wnd != null)
            {
                wnd.Focus();
                Keyboard.Focus(wnd);
            }
        }
       
        // Keyboard.ClearFocus();
    }

   
}

