using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using UserControl = System.Windows.Controls.UserControl;

namespace Androidplayer.Src.Pages;

public partial class TitleBar : UserControl
{
    public TitleBar()
    {
        InitializeComponent();
        this.DataContext = this;
    }
    
    // Dependency Property for Title
        public static readonly DependencyProperty TitleProperty = 
            DependencyProperty.Register("Title", typeof(string), typeof(TitleBar), 
                new PropertyMetadata("Android Player"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        // Dependency Property for Parent Window
        public static readonly DependencyProperty ParentWindowProperty =
            DependencyProperty.Register("ParentWindow", typeof(Window), typeof(TitleBar), 
                new PropertyMetadata(null, OnParentWindowChanged));

        public Window ParentWindow
        {
            get => (Window)GetValue(ParentWindowProperty);
            set => SetValue(ParentWindowProperty, value);
        }

        private static void OnParentWindowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var titleBar = (TitleBar)d;
            titleBar.UpdateWindowControls();
        }

        private void UpdateWindowControls()
        {
            if (ParentWindow != null)
            {
                UpdateMaximizeButtonContent();
            }
        }

        // Event Handlers
        private void DragRegion_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && ParentWindow != null)
            {
                ParentWindow.DragMove();
            }
        }

        private void MinimizeBtn_OnClick(object sender, RoutedEventArgs e)
        {
            // ParentWindow?.WindowState = WindowState.Minimized;
            if (ParentWindow != null)
                ParentWindow.WindowState = WindowState.Minimized;
        }
        

        private void MaximizeBtn_OnClick(object sender, RoutedEventArgs e)
        {
            if (ParentWindow != null)
            {
                if (ParentWindow.WindowState == WindowState.Maximized)
                {
                    ParentWindow.WindowState = WindowState.Normal;
                    MaximizeBtn.Content = "□";
                }
                else
                {
                    ParentWindow.WindowState = WindowState.Maximized;
                    MaximizeBtn.Content = "❐";
                }
            }
        }

        private void CloseBtn_OnClick(object sender, RoutedEventArgs e)
        {
            ParentWindow?.Close();
        }

        private void UpdateMaximizeButtonContent()
        {
            if (ParentWindow != null)
            {
                MaximizeBtn.Content = ParentWindow.WindowState == WindowState.Maximized ? "❐" : "□";
            }
        }
    
    
}