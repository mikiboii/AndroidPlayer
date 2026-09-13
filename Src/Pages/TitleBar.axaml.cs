// using Avalonia;
// using Avalonia.Controls;
// using Avalonia.Markup.Xaml;
//
// namespace Androidplayer.Pages;
//
// public partial class TitleBar : UserControl
// {
//     public TitleBar()
//     {
//         InitializeComponent();
//     }
// }
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Androidplayer.Pages;

public partial class TitleBar : UserControl
{

    public string mikiboii = "yoyo";
    
    
    public TitleBar()
    {
        InitializeComponent();
        DataContext = this; // This sets the DataContext to itself
        
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Title = "new mm";
    }

    // Avalonia Property for Title (supports binding)
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<TitleBar, string>(
            nameof(Title),
            defaultValue: "Android Player");

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    // Avalonia Property for Parent Window (supports binding)
    public static readonly StyledProperty<Window> ParentWindowProperty =
        AvaloniaProperty.Register<TitleBar, Window>(
            nameof(ParentWindow),
            defaultValue: null);

    public Window? ParentWindow
    {
        get => GetValue(ParentWindowProperty);
        set => SetValue(ParentWindowProperty, value);
    }

    // Event Handlers
    private void DragRegion_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && ParentWindow != null)
        {
            ParentWindow.BeginMoveDrag(e);
        }
    }

    private void MinimizeBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ParentWindow != null)
        {
            // k_info.Instance.KeymapMode = false;
            ParentWindow.WindowState = WindowState.Minimized;
        }
    }

    private void MaximizeBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ParentWindow != null)
        {
            // k_info.Instance.KeymapMode = false;
            
            if (ParentWindow.WindowState == WindowState.Maximized)
            {
                ParentWindow.WindowState = WindowState.Normal;
                MaximizeBtn.Content = "☐";
            }
            else
            {
                ParentWindow.WindowState = WindowState.Maximized;
                MaximizeBtn.Content = "❐";
            }
        }
    }

    private void CloseBtn_OnClick(object? sender, RoutedEventArgs e)
    {
        ParentWindow?.Close();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        // Find parent window if not set via binding
        if (ParentWindow == null)
        {
            ParentWindow = this.FindAncestorOfType<Window>();
        }
        
        // Update maximize button when window state changes
        if (ParentWindow != null)
        {
            ParentWindow.PropertyChanged += (s, args) =>
            {
                if (args.Property == Window.WindowStateProperty)
                {
                    UpdateMaximizeButtonContent();
                }
            };
            UpdateMaximizeButtonContent();
        }
    }

    private void UpdateMaximizeButtonContent()
    {
        if (ParentWindow != null)
        {
            MaximizeBtn.Content = ParentWindow.WindowState == WindowState.Maximized ? "❐" : "☐";
        }
    }
}