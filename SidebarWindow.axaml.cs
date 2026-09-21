using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Androidplayer
{
    public partial class SidebarWindow : Window
    {
        
        // 1. P/Invoke declarations
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x00080000;
        private const uint LWA_COLORKEY = 0x00000001;
        
        
        private Window mainWindow;
        public event Action<string>? SidebarButtonClicked;

        public SidebarWindow()
        {
            InitializeComponent();
        }
        
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            // 2. Only apply this workaround on Windows 7 (or when DWM is not available)
            // You can check the OS version or simply try to apply it and see if it works.
            // For simplicity, we'll check if it's not Windows 8 or newer.
            if (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(6, 2))
            {
                var handle = this.TryGetPlatformHandle()?.Handle;
                if (handle != null && handle != IntPtr.Zero)
                {
                    // Get current extended style and add WS_EX_LAYERED
                    int exStyle = GetWindowLong(handle.Value, GWL_EXSTYLE);
                    SetWindowLong(handle.Value, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);

                    // 3. Choose a color to be the "transparent key"
                    // This color should NOT be used anywhere else in your visible UI.
                    // A color like Magenta (255, 0, 255) is a good choice.
                    // The crKey parameter expects a COLORREF (0x00BBGGRR).
                    uint colorKey = 0x00FF00FF; // Magenta in BGR

                    // Apply the transparency key
                    SetLayeredWindowAttributes(handle.Value, colorKey, 0, LWA_COLORKEY);

                    // 4. Update your XAML to use this color as the window background
                    // In your SidebarWindow.axaml:
                    // <Window ... Background="#FF00FF">  <-- This will become transparent
                    //     <Border Background="#f0f0f0" ...>
                }
            }
        }
        
        public SidebarWindow(Window mainWindow)
        {
            InitializeComponent();

            this.mainWindow = mainWindow;

            this.Owner = this.mainWindow;

            Loaded += SidebarWindow_Loaded;

            // this.Activated += OnActivated;

            mainWindow.PositionChanged += MainWindow_PositionChanged;
            mainWindow.PropertyChanged += MainWindow_PropertyChanged;
            // mainWindow.Activated += MainWindow_Activated;
        }

        private void OnActivated(object? sender, EventArgs e)
        {


            
            Console.WriteLine("sidebar activated ");
            Console.WriteLine(this.Topmost);
            // this.mainWindow.Activate();
            this.Topmost = true;
            this.Topmost = false;
            
            
            
            // if (this.IsVisible)
            // {
            // this.Topmost = true;
            // this.Topmost = false;
            //     // Avalonia has no UpdateLayout() — layout is done automatically.
            // }
            //
            
            // if (mainWindow != null && !mainWindow.IsActive)
            // {
            //     // mainWindow.Activate();
            //     this.mainWindow.Activate();
            //     if (this.IsVisible)
            //     {
            //         this.Topmost = true;
            //         this.Topmost = false;
            //         // Avalonia has no UpdateLayout() — layout is done automatically.
            //     }
            // }
        }

        private void OnSidebarButtonClick(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Image img)
            {
                string tag = img.Tag?.ToString() ?? "unknown";
                SidebarButtonClicked?.Invoke(tag);

                if (tag == "Settings")
                {
                    return;
                }
            }
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            if (this.IsVisible)
            {
                this.Topmost = true;
                this.Topmost = false;
                // Avalonia has no UpdateLayout() — layout is automatic
            }
        }

        private void MainWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            // Watch for size / state changes on the main window
            if (e.Property == Window.BoundsProperty ||
                e.Property == Window.WindowStateProperty ||
                e.Property == Window.ClientSizeProperty)
            {
                UpdatePosition();
            }
        }

        private void SidebarWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            UpdatePosition();
            DebugImages();
        }

        private void DebugImages()
        {
            var images = new[]
            {
                "Icons/sidebar1/screenshot.png",
                "Icons/sidebar1/record.png",
                "Icons/sidebar1/keyboard.png",
            };

            foreach (var imagePath in images)
            {
                try
                {
                    // Avalonia: AssetLoader.Open + Bitmap instead of BitmapImage
                    var uri = new Uri($"avares://Androidplayer/{imagePath}");
                    using var stream = AssetLoader.Open(uri);
                    var bitmap = new Bitmap(stream);
                    // Console.WriteLine($"Loaded: {imagePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load: {imagePath} - {ex.Message}");
                }
            }
        }

        private void MainWindow_PositionChanged(object? sender, PixelPointEventArgs e)
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (mainWindow.WindowState == WindowState.Normal)
            {
                // Avalonia: use Position (PixelPoint) for top-left,
                // and Bounds.Width for the main window's pixel width.
                var mainPos = mainWindow.Position;
                double mainWidth = mainWindow.Bounds.Width;

                this.Position = new PixelPoint(
                    mainPos.X + (int)mainWidth + 1,
                    mainPos.Y + 30);

                // this.Height = mainWindow.Bounds.Height;

                if (!this.IsVisible)
                    this.Show();
            }
            else
            {
                this.Hide();
            }
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Clean up event handlers
            mainWindow.PositionChanged -= MainWindow_PositionChanged;
            mainWindow.PropertyChanged -= MainWindow_PropertyChanged;
            mainWindow.Activated -= MainWindow_Activated;

            base.OnClosed(e);
        }
    }
}