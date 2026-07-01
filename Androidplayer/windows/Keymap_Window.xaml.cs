using System.ComponentModel;
using System.Windows;
using Androidplayer.Src.Keymap.K_store;

namespace Androidplayer.windows;

public partial class Keymap_Window : Window
{
    private Window mainWindow;
    public Keymap_Window(Window mainWindow)
    {
        InitializeComponent();
        
        this.mainWindow = mainWindow;
            
        this.Owner = mainWindow;
        // Position the sidebar when loaded
        Loaded += Keymap_Window_Loaded;
        // Activated += Keymap_Window_OnActivated;
        
        mainWindow.LocationChanged += MainWindow_LocationChanged;
        mainWindow.SizeChanged += MainWindow_SizeChanged;
        mainWindow.StateChanged += MainWindow_StateChanged;
            
            
        mainWindow.Activated += MainWindow_Activated;
        
        k_info.Instance.PropertyChanged += K_info_PropertyChanged;
    }

    private void K_info_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // case nameof(My_Store.VideoWidth):
            
                
            case nameof(k_info.Collapsed):
    
                    
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // ShowToast("typing mode changed");
    
                    if (k_info.Instance.Collapsed)
                    {
                        // AnimateModeOverlay("keyboard");
                      this.Width = 24;
                      
                  
    
                    }
                    else
                    {
                      this.Width = 314;
                        
                        
                    }
                   
                        
                        
                });
    
    
                    
    
                break;
                
    
        }
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePosition();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        UpdatePosition();
    }

    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        // UpdatePosition();
        
        if (this.IsLoaded)
        {
            this.Topmost = true;
            this.Topmost = false; // This tricks it into coming forward
            this.UpdateLayout();

            // this.Activate();
        }
    }

    private void MainWindow_LocationChanged(object? sender, EventArgs e)
    {
        UpdatePosition();
    }

    private void Keymap_Window_Loaded(object sender, RoutedEventArgs e)
    {
        UpdatePosition();

        this.Activate();
    }
    
    
    private void UpdatePosition()
    {
       
            // Position sidebar to the right of main window
            this.Left = mainWindow.Left + 3;
            this.Top = mainWindow.Top + 30;
            
            
            // this.Height = mainWindow.Height - 30;

            var my_hight = mainWindow.Height - 32;
            if (my_hight >= 0)
            {
                this.Height = my_hight;
                
            }
            
                
            // Show the sidebar
            this.Show();
        
        
    }

    private void Keymap_Window_OnActivated(object? sender, EventArgs e)
    {
        
        if (mainWindow != null && !mainWindow.IsActive)
        {
            
            mainWindow.Activate();

            // this.Activate();
        }
        
        
    }
}









//
// using System;
// using System.ComponentModel;
// using System.Runtime.InteropServices;
// using System.Windows;
// using System.Windows.Interop;
// using Androidplayer.Src.Keymap.K_store;
//
// namespace Androidplayer.windows;
//
// public partial class Keymap_Window : Window
// {
//     private Window mainWindow;
//     private bool isClosing = false;
//
//     // Win32 API for window ownership
//     [DllImport("user32.dll")]
//     private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
//
//     [DllImport("user32.dll")]
//     private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
//
//     [DllImport("user32.dll")]
//     private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
//
//     private const int GWL_STYLE = -16;
//     private const int WS_CHILD = 0x40000000;
//     private const int WS_POPUP = unchecked((int)0x80000000);
//
//     public Keymap_Window(Window mainWindow)
//     {
//         InitializeComponent();
//         
//         this.mainWindow = mainWindow;
//         
//         // Make this window a child of the main window
//         this.Owner = mainWindow;
//         
//         // Remove window borders and make it transparent
//         this.WindowStyle = WindowStyle.None;
//         this.AllowsTransparency = true;
//         this.Background = System.Windows.Media.Brushes.Transparent;
//         
//         // Ensure it stays on top
//         this.Topmost = true;
//         
//         // Position the window when loaded
//         Loaded += Keymap_Window_Loaded;
//         
//         // Subscribe to main window events
//         mainWindow.LocationChanged += MainWindow_LocationChanged;
//         mainWindow.SizeChanged += MainWindow_SizeChanged;
//         mainWindow.StateChanged += MainWindow_StateChanged;
//         mainWindow.Closed += MainWindow_Closed;
//         mainWindow.IsVisibleChanged += MainWindow_IsVisibleChanged;
//         
//         // Subscribe to property changes
//         k_info.Instance.PropertyChanged += K_info_PropertyChanged;
//         
//         // Ensure window is shown when main window is active
//         this.IsVisibleChanged += Keymap_Window_IsVisibleChanged;
//     }
//
//     private void MainWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
//     {
//         // Sync visibility with main window
//         if (mainWindow.IsVisible)
//         {
//             this.Show();
//             UpdatePosition();
//         }
//         else
//         {
//             this.Hide();
//         }
//     }
//
//     private void Keymap_Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
//     {
//         // Keep in sync with main window
//         if (this.IsVisible && mainWindow != null && !mainWindow.IsVisible)
//         {
//             this.Hide();
//         }
//     }
//
//     private void MainWindow_Closed(object? sender, EventArgs e)
//     {
//         isClosing = true;
//         this.Close();
//     }
//
//     private void K_info_PropertyChanged(object? sender, PropertyChangedEventArgs e)
//     {
//         if (e.PropertyName == nameof(k_info.Collapsed))
//         {
//             Application.Current.Dispatcher.Invoke(() =>
//             {
//                 if (k_info.Instance.Collapsed)
//                 {
//                     this.Width = 24;
//                 }
//                 else
//                 {
//                     this.Width = 314;
//                 }
//                 
//                 // Update position after width change
//                 UpdatePosition();
//             });
//         }
//     }
//
//     private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
//     {
//         UpdatePosition();
//     }
//
//     private void MainWindow_StateChanged(object? sender, EventArgs e)
//     {
//         UpdatePosition();
//         
//         // Handle minimized state
//         if (mainWindow.WindowState == WindowState.Minimized)
//         {
//             this.WindowState = WindowState.Minimized;
//         }
//         else
//         {
//             this.WindowState = WindowState.Normal;
//             this.Show();
//         }
//     }
//
//     private void MainWindow_LocationChanged(object? sender, EventArgs e)
//     {
//         UpdatePosition();
//     }
//
//     private void Keymap_Window_Loaded(object sender, RoutedEventArgs e)
//     {
//         // Make it a child window using Win32 API
//         MakeChildWindow();
//         UpdatePosition();
//         this.Show();
//     }
//
//     private void MakeChildWindow()
//     {
//         try
//         {
//             var helper = new WindowInteropHelper(this);
//             var mainHelper = new WindowInteropHelper(mainWindow);
//             
//             // Set as child window
//             SetParent(helper.Handle, mainHelper.Handle);
//             
//             // Remove popup style to make it truly a child
//             int style = GetWindowLong(helper.Handle, GWL_STYLE);
//             style &= ~WS_POPUP;
//             style |= WS_CHILD;
//             SetWindowLong(helper.Handle, GWL_STYLE, style);
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"Failed to make child window: {ex.Message}");
//             // Fallback: just use Owner and Topmost
//             this.Owner = mainWindow;
//             this.Topmost = true;
//         }
//     }
//
//     private void UpdatePosition()
//     {
//         if (mainWindow == null || isClosing)
//             return;
//
//         try
//         {
//             Application.Current.Dispatcher.Invoke(() =>
//             {
//                 // Position the overlay relative to main window
//                 // this.Left = mainWindow.Left;
//                 // this.Top = mainWindow.Top + 30;
//                 // this.Height = mainWindow.Height - 30;
//                 //
//                 this.Left = 0;
//                 this.Top =  30;
//                 this.Height = mainWindow.Height - 30;
//                 
//                 // Ensure it stays on top
//                 this.Topmost = true;
//                 
//                 // Bring to front
//                 this.Activate();
//                 
//                 // Show if hidden
//                 if (!this.IsVisible && mainWindow.IsVisible)
//                 {
//                     this.Show();
//                 }
//             });
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"Error updating position: {ex.Message}");
//         }
//     }
//
//     // Method to ensure the overlay is always on top
//     public void BringToFront()
//     {
//         if (this.IsVisible && !isClosing)
//         {
//             this.Topmost = true;
//             this.Activate();
//             this.Topmost = true; // Re-apply to ensure it stays on top
//         }
//     }
//
//     protected override void OnDeactivated(EventArgs e)
//     {
//         base.OnDeactivated(e);
//         
//         // When deactivated, bring main window to front if this window is clicked
//         if (mainWindow != null && !mainWindow.IsActive && mainWindow.IsVisible)
//         {
//             mainWindow.Activate();
//         }
//     }
//
//     protected override void OnClosed(EventArgs e)
//     {
//         // Clean up event handlers
//         if (mainWindow != null)
//         {
//             mainWindow.LocationChanged -= MainWindow_LocationChanged;
//             mainWindow.SizeChanged -= MainWindow_SizeChanged;
//             mainWindow.StateChanged -= MainWindow_StateChanged;
//             mainWindow.Closed -= MainWindow_Closed;
//             mainWindow.IsVisibleChanged -= MainWindow_IsVisibleChanged;
//         }
//         
//         k_info.Instance.PropertyChanged -= K_info_PropertyChanged;
//         
//         base.OnClosed(e);
//     }
// }