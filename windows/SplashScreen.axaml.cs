
using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Androidplayer.windows
{
    public partial class SplashScreen : Window
    {
        
        
#if WINDOWS
        
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
        
        
        
        
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW  = 0x00040000;

       

            
            
            
            
            
#endif

        
        
        
        public double StartScale { get; set; } = 0.5;
        public double EndScale { get; set; } = 1.0;
        public double AnimationDurationSeconds { get; set; } = 3.0;

        private readonly ScaleTransform _imageScale = new();

        public SplashScreen()
        {
            InitializeComponent();

            _imageScale.ScaleX = StartScale;
            _imageScale.ScaleY = StartScale;
            SplashImage.RenderTransform = _imageScale;

            Opened += OnOpened;
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            
            
            
            if (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(6, 2))
            {

                // Background = "#FF00FF";
                
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0xFF, 0x00, 0xFF));
                
                
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

            
            
            Dispatcher.UIThread.Post(AnimateImageZoom, DispatcherPriority.Loaded);
        }

        private void AnimateImageZoom()
        {
            SplashImage.IsVisible = true;

            var duration = TimeSpan.FromSeconds(AnimationDurationSeconds);
            var easing = new CubicEaseOut();

            var scaleXAnimation = new Animation
            {
                Duration = duration,
                Easing = easing,
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0d),
                        Setters = { new Setter(ScaleTransform.ScaleXProperty, StartScale) } },
                    new KeyFrame { Cue = new Cue(1d),
                        Setters = { new Setter(ScaleTransform.ScaleXProperty, EndScale) } }
                }
            };

            var scaleYAnimation = new Animation
            {
                Duration = duration,
                Easing = easing,
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0d),
                        Setters = { new Setter(ScaleTransform.ScaleYProperty, StartScale) } },
                    new KeyFrame { Cue = new Cue(1d),
                        Setters = { new Setter(ScaleTransform.ScaleYProperty, EndScale) } }
                }
            };

            // ✅ Run against the Image (a Visual), NOT against _imageScale
            _ = scaleXAnimation.RunAsync(SplashImage);
            _ = scaleYAnimation.RunAsync(SplashImage);
        }
    }
}