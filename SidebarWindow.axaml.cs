using System;
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
        private Window mainWindow;
        public event Action<string>? SidebarButtonClicked;

        public SidebarWindow()
        {
            InitializeComponent();
        }
        
        
        
        public SidebarWindow(Window mainWindow)
        {
            InitializeComponent();

            this.mainWindow = mainWindow;

            this.Owner = this.mainWindow;

            Loaded += SidebarWindow_Loaded;

            mainWindow.PositionChanged += MainWindow_PositionChanged;
            mainWindow.PropertyChanged += MainWindow_PropertyChanged;
            mainWindow.Activated += MainWindow_Activated;
        }

        private void OnActivated(object? sender, EventArgs e)
        {
            if (mainWindow != null && !mainWindow.IsActive)
            {
                mainWindow.Activate();
            }
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