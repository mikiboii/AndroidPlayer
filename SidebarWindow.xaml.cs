using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Androidplayer
{
    public partial class SidebarWindow : Window
    {
        private Window mainWindow;
        public event Action<string>? SidebarButtonClicked;
        public SidebarWindow(Window mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;

            this.Owner = this.mainWindow;
            
            // Position the sidebar when loaded
            Loaded += SidebarWindow_Loaded;
            mainWindow.LocationChanged += MainWindow_LocationChanged;
            mainWindow.SizeChanged += MainWindow_SizeChanged;
            mainWindow.StateChanged += MainWindow_StateChanged;
            
            
            mainWindow.Activated += MainWindow_Activated;
            
            // this.Activated += OnActivated;
           
        }

        private void OnActivated(object? sender, EventArgs e)
        {
            if (mainWindow != null && !mainWindow.IsActive)
            {
               
                
                mainWindow.Activate();
            }
        }

        private void OnSidebarButtonClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image img)
            {
                // You can identify which icon was clicked via Tag
                string tag = img.Tag?.ToString() ?? "unknown";
                SidebarButtonClicked?.Invoke(tag);

                if (tag == "Settings")
                {
                    return;
                }

                // Console.WriteLine(tag);
            }
            
            // if (mainWindow != null && !mainWindow.IsActive)
            // {
            //     mainWindow.Activate();
            // }
        }

        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            // Console.WriteLine("main is active : sidebarWindow");
            if (this.IsLoaded)
            {
                this.Topmost = true;
                this.Topmost = false; // This tricks it into coming forward
                this.UpdateLayout();
            }
        }

        private void SidebarWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePosition();
            
            DebugImages();
        }
        
        
        private void DebugImages()
        {
            // Test if images can be found
            var images = new[] 
            {
                "Icons/sidebar1/screenshot.png",
                "Icons/sidebar1/record.png",
                "Icons/sidebar1/keyboard.png",
                // Add all your image paths
            };

            foreach (var imagePath in images)
            {
                try
                {
                    var bitmap = new BitmapImage(new Uri(imagePath, UriKind.RelativeOrAbsolute));
                    // Console.WriteLine($"Loaded: {imagePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load: {imagePath} - {ex.Message}");
                }
            }
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            UpdatePosition();
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePosition();
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (mainWindow.WindowState == WindowState.Normal)
            {
                // Position sidebar to the right of main window
                this.Left = mainWindow.Left + mainWindow.Width + 1;
                this.Top = mainWindow.Top + 30;
                // this.Height = mainWindow.Height;
                
                // Show the sidebar
                this.Show();
            }
            else
            {
                // Hide sidebar when main window is minimized
                this.Hide();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Clean up event handlers
            mainWindow.LocationChanged -= MainWindow_LocationChanged;
            mainWindow.SizeChanged -= MainWindow_SizeChanged;
            mainWindow.StateChanged -= MainWindow_StateChanged;
            
            base.OnClosed(e);
        }
    }
}