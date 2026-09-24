
using System;
using System.Runtime.InteropServices;
using Androidplayer.Src.Keymap.K_store;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.VisualTree;


namespace Androidplayer;

public class Native_view : NativeControlHost
{
    private IPlatformHandle? _platformHandle;
    
    public Window? _floatingContent;

    private IDisposable? _contentChangedHandler;
    private IDisposable? _isVisibleChangedHandler;
    private IDisposable? _floatingContentChangedHandler;
    private IDisposable? _floatingContentDataContextHandler;
    private IDisposable? _boundsHandler;

    private static readonly Version AvaloniaVersion =
        typeof(Application).Assembly.GetName().Version!;
    private static readonly bool IsAvalonia12OrAbove = AvaloniaVersion.Major >= 12;

    // Cached rects so we only touch the window when something changed.
    private Rect _rectInitLast = default;
    private Rect _rectIntersectLast = default;
    private bool _hasRectInitLast;
    private bool _hasRectIntersectLast;
    private bool _updating;
    private bool _isClippedOut;


    private bool _windowTransitioning;
    
    // windows 7 code
    // P/Invoke declarations
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

// Constants
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const uint LWA_COLORKEY = 0x00000001;

    
    
    // Keep byte order consistent: COLORREF is 0x00BBGGRR
    private const byte KeyR = 0xFF;
    private const byte KeyG = 0x01;
    private const byte KeyB = 0xFE;
    private const uint COLOR_KEY_TRANSPARENT = (KeyB << 16) | (KeyG << 8) | KeyR; // matches SetLayeredWindowAttributes

    private static readonly IBrush KeyColorBrush =
        new SolidColorBrush(Color.FromRgb(KeyR, KeyG, KeyB));
    
    
    // windows 7 code

    public IPlatformHandle? PlatformHandle => _platformHandle;
    public IntPtr NativeHandle => _platformHandle?.Handle ?? IntPtr.Zero;
    public string HandleDescriptor => _platformHandle?.HandleDescriptor ?? string.Empty;

    public event EventHandler<IPlatformHandle>? HandleCreated;



    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<Native_view, object?>(nameof(Content));

 

    [Content]
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public Native_view()
    {
        Initialized += (_, _) => Attach();

        _contentChangedHandler = ContentProperty.Changed.AddClassHandler<Native_view>(
            (s, _) => s.OnContentChanged());

        _isVisibleChangedHandler = IsVisibleProperty.Changed.AddClassHandler<Native_view>(
            (s, _) => s.ShowNativeOverlay(s.IsEffectivelyVisible));

        // Primary sync trigger — fires after every layout pass.
        LayoutUpdated += (_, _) => SyncWindows();
        
        
    }

    // ---------------------------------------------------------------------
    //  LibVLC attach / detach — cross-platform, no P/Invoke.
    //  Uses LibVLCSharp's built-in per-platform properties.
    // ---------------------------------------------------------------------
    private void Attach()
    {
     
    }

    private void Detach()
    {
        
    }
    
    
    
    private void MakeWindowTransparent(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        // Get current extended style and add WS_EX_LAYERED
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);

        // Set color key transparency (white becomes transparent)
        SetLayeredWindowAttributes(hwnd, COLOR_KEY_TRANSPARENT, 0, LWA_COLORKEY);
    }

    // ---------------------------------------------------------------------
    //  Overlay lifecycle
    // ---------------------------------------------------------------------
    private void OnContentChanged()
    {
        if (_floatingContent == null)
        {
            InitializeNativeOverlay();
        }
        else
        {
            _floatingContentChangedHandler?.Dispose();
            _floatingContentChangedHandler = _floatingContent.Bind(
                ContentControl.ContentProperty,
                this.GetObservable(ContentProperty));
        }

        SyncWindows();
    }

    private void InitializeNativeOverlay()
    {
        if (!this.IsAttachedToVisualTree()) return;
        if (TopLevel.GetTopLevel(this) is not Window visualRoot) return;

        if (_floatingContent == null)
        {
            _floatingContent = new Window
            {
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
                Background = Brushes.Transparent,
                



                // Background = Brushes.Black,
                
                
                TransparencyBackgroundFallback = KeyColorBrush,
                
                
                
                
                
                
                // Background = new SolidColorBrush(Color.FromRgb(0xFE, 0x01, 0xFF)),
                SizeToContent = SizeToContent.Manual,
                CanResize = false,
                ShowInTaskbar = false,
                ZIndex = int.MaxValue,
                Opacity = 1.0,
                DataContext = DataContext,
                WindowStartupLocation = WindowStartupLocation.Manual,
                SystemDecorations = SystemDecorations.None
            };

            SetWindowDecorationsNone(_floatingContent);

            _floatingContentChangedHandler = _floatingContent.Bind(
                ContentControl.ContentProperty,
                this.GetObservable(ContentProperty));

            _floatingContentDataContextHandler = this
                .GetObservable(DataContextProperty)
                .Subscribe(new AnonymousObserver<object?>(dc =>
                {
                    if (_floatingContent != null)
                        _floatingContent.DataContext = dc;
                }));

            _floatingContent.PointerEntered += FloatingContentOnPointerEvent;
            _floatingContent.PointerExited += FloatingContentOnPointerEvent;
            _floatingContent.PointerPressed += FloatingContentOnPointerEvent;
            _floatingContent.PointerReleased += FloatingContentOnPointerEvent;

            // _floatingContent.PropertyChanged += FloatingContentOnPropertyChanged;
            

            visualRoot.LayoutUpdated += VisualRoot_UpdateOverlayPosition;
            visualRoot.PositionChanged += VisualRoot_UpdateOverlayPosition;
            visualRoot.PropertyChanged += VisualRoot_PropertyChanged;

            // visualRoot.Resized += VisualRoot_Resized;
            
            _boundsHandler = this.GetObservable(BoundsProperty)
                .Subscribe(new AnonymousObserver<Rect>(_ => SyncWindows()));
        }

      
        
        ShowNativeOverlay(IsEffectivelyVisible);
    }

   
    
    // private void VisualRoot_Resized(object? sender, WindowResizedEventArgs e)
    // {
    //     if (sender is not Window owner) return;
    //
    //     Console.WriteLine($"Resized: {e.ClientSize}, Reason: {e.Reason}");
    //
    //     // Now the surface has its final size for this resize operation
    //     if (owner.WindowState == WindowState.Maximized)
    //     {
    //         Console.WriteLine("Surface FULLY maximized");
    //     
    //         _floatingContent.Background = Brushes.Transparent;
    //         // This is the correct moment to resize your DirectX back buffer
    //         // or to re-sync the overlay window
    //     }
    // }
    //
    //
    
    private void FloatingContentOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {

        // Console.WriteLine("floating event called");
        if (e.Property != Window.WindowStateProperty) return;
    
        if (e.NewValue is not WindowState newState) return;
        WindowState oldState = e.OldValue is WindowState o ? o : WindowState.Normal;
    
        Console.WriteLine($"WindowState floating window changed: {oldState} -> {newState}");

        // if ("Normal" == newState.ToString())
        // {
        //     _floatingContent.Background = Brushes.Transparent;
        //     Console.WriteLine("Floating Normal");
        // }
        //
        // if ("Minimized" == newState.ToString())
        // {
        //     _floatingContent.Background = Brushes.Black;
        //     Console.WriteLine("Floating minimized");
        // }
    
        switch (newState)
        {
            case WindowState.Minimized:
                Console.WriteLine("Floating minimized");
                // _floatingContent.Background = Brushes.Black;
                break;
    
            case WindowState.Maximized:
                Console.WriteLine("Floating maximized");
                break;
    
            case WindowState.Normal:
                Console.WriteLine("Floating Normal");
                // _floatingContent.Background = Brushes.Transparent;
                break;
        }
    }
    
    private static void SetWindowDecorationsNone(Window window)
    {
        // Cross-platform — no P/Invoke, uses Avalonia's own enum.
        var prop = IsAvalonia12OrAbove
            ? typeof(Window).GetProperty("WindowDecorations")
            : typeof(Window).GetProperty("SystemDecorations");
        prop?.SetValue(window, Enum.Parse(prop.PropertyType, "None"));
    }

    private void VisualRoot_UpdateOverlayPosition(object? sender, EventArgs e) => SyncWindows();

    // private void VisualRoot_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    // {
    //     
    //     // if (e.Property != Window.WindowStateProperty) return;
    //     //
    //     // if (e.NewValue is not WindowState newState) return;
    //     // WindowState oldState = e.OldValue is WindowState o ? o : WindowState.Normal;
    //     //
    //     // Console.WriteLine($"WindowState floating window changed: {oldState} -> {newState}");
    //     //
    //     // switch (newState)
    //     // {
    //     //     case WindowState.Minimized:
    //     //         Console.WriteLine("Floating minimized");
    //     //         _floatingContent.Background = Brushes.Black;
    //     //         break;
    //     //
    //     //     case WindowState.Maximized:
    //     //         Console.WriteLine("Floating maximized");
    //     //         break;
    //     //
    //     //     case WindowState.Normal:
    //     //         Console.WriteLine("Floating Normal");
    //     //         _floatingContent.Background = Brushes.Transparent;
    //     //         k_info.Instance.directx.HandleResize();
    //     //         break;
    //     // }
    //     
    //     if (e.Property == Visual.BoundsProperty)
    //         SyncWindows();
    // }

    
    private void VisualRoot_PropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
        {
            if (e.NewValue is not WindowState newState)
                return;

            switch (newState)
            {
                case WindowState.Minimized:
                    _windowTransitioning = true;

                    if (_floatingContent != null)
                    {
                        _floatingContent.Opacity = 0;
                        _floatingContent.IsHitTestVisible = false;
                        // _floatingContent.Background = Brushes.Black;
                    }

                    break;

                case WindowState.Normal:
                case WindowState.Maximized:
                    _windowTransitioning = true;

                    if (_floatingContent != null)
                    {
                        _floatingContent.Opacity = 0;
                        _floatingContent.IsHitTestVisible = false;
                        // _floatingContent.Background = Brushes.Transparent;
                    }

                    // Wait until Avalonia has completed the restore/maximize layout.
                    Dispatcher.UIThread.Post(() =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            _windowTransitioning = false;

                            SyncWindows();

                            if (_floatingContent != null &&
                                IsEffectivelyVisible)
                            {
                                _floatingContent.Opacity = 1;
                                _floatingContent.IsHitTestVisible = true;
                                
                            }
                        });
                    });

                    break;
            }
        }

        if (e.Property == Visual.BoundsProperty)
            SyncWindows();
    }
    
    
    private void FloatingContentOnPointerEvent(object? sender, PointerEventArgs e)
        => RaiseEvent(e);

    private void ShowNativeOverlay(bool show)
    {
        if (_floatingContent == null) return;
        if (TopLevel.GetTopLevel(this) is not Window visualRoot) return;

        if (show && this.IsAttachedToVisualTree())
        {
            if (!_floatingContent.IsVisible)
            {
                _floatingContent.Show(visualRoot);

                // _floatingContent.Background 
                
                // Apply native transparency after window is shown
                if (_floatingContent.TryGetPlatformHandle()?.Handle is { } nativeHandle)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        
                        var osVersion = Environment.OSVersion.Version;
                        if (osVersion.Major == 6 && osVersion.Minor == 1)
                        {
                            
                            MakeWindowTransparent(nativeHandle);

                            Console.WriteLine("making overlay  window transparent");
                            
                        }
                        
                    }
                }
                
                SyncWindows();
            }
            else
            {
                SyncWindows();
            }
        }
        else
        {
            if (_floatingContent.IsVisible)
                _floatingContent.Hide();
        }
    }

    // ---------------------------------------------------------------------
    //  Sync — cross-platform, no Win32 region tricks.
    //
    //  Strategy: instead of positioning the window at the full host rect
    //  and cropping it with an OS region (Flyleaf's Windows-only trick),
    //  we position and size the window to the *intersection* of the host
    //  with all its ancestors and with the owner window. The OS window
    //  itself therefore never extends outside the host, on any platform.
    // ---------------------------------------------------------------------
    private void SyncWindows()
    {
        
        if (_windowTransitioning)
            return;
        
        if (_floatingContent == null) return;
        if (!_floatingContent.IsVisible) return;
        if (!IsEffectivelyVisible) return;
        if (!this.IsAttachedToVisualTree()) return;
        if (_updating) return;
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        // ---- 1. Host rect relative to owner (DIPs) ----
        var hostOrigin = this.TranslatePoint(new Point(0, 0), owner);
        if (hostOrigin == null) return;

        var hostRect = new Rect(hostOrigin.Value, Bounds.Size);

        // ---- 2. Intersect with every ancestor up to the owner ----
        //        This is what makes parent margins, borders, padding,
        //        ScrollViewer clipping, etc. all respected.
        Rect intersect = hostRect;
        Visual? parent = this.GetVisualParent();
        while (parent != null && parent != owner)
        {
            var pOrigin = parent.TranslatePoint(new Point(0, 0), owner);
            if (pOrigin != null)
            {
                var pRect = new Rect(pOrigin.Value, parent.Bounds.Size);
                intersect = intersect.Intersect(pRect);
            }
            parent = parent.GetVisualParent();
        }

        // ---- 3. Intersect with the owner window's client area ----
        //        Owner bounds start at (0,0) and are already in DIPs.
        //        Using Bounds (not ClientSize) keeps this platform-neutral:
        //        Avalonia reports the drawable area here on all three OSes.
        var ownerRect = new Rect(0, 0, owner.Bounds.Width, owner.Bounds.Height);
        intersect = intersect.Intersect(ownerRect);

        // ---- 4. Fully clipped → hide the window entirely ----
        if (intersect.Width <= 0 || intersect.Height <= 0)
        {
            if (!_isClippedOut)
            {
                _isClippedOut = true;
                _hasRectIntersectLast = false;
                _rectIntersectLast = default;
                _floatingContent.Opacity = 0;
                _floatingContent.IsHitTestVisible = false;
                if (_floatingContent.Content is Visual v0)
                    v0.Clip = null;
            }
            return;
        }

        _updating = true;
        try
        {
            // ---- 5. Position & size the window AT THE INTERSECTION ----
            // This is the cross-platform replacement for SetWindowRgn.
            // The OS window itself is exactly the visible part, so it
            // can't paint over siblings or the titlebar on any platform.
            var intersectTopLeftOnScreen = owner.PointToScreen(intersect.Position);
            var pos = new PixelPoint(
                intersectTopLeftOnScreen.X,
                intersectTopLeftOnScreen.Y);

            if (!_hasRectInitLast || intersect != _rectInitLast)
            {
                _hasRectInitLast = true;
                _rectInitLast = intersect;

                if (_floatingContent.Position != pos)
                    _floatingContent.Position = pos;

                _floatingContent.Width  = intersect.Width;
                _floatingContent.Height = intersect.Height;
                _floatingContent.MaxWidth  = intersect.Width;
                _floatingContent.MaxHeight = intersect.Height;
            }
            else
            {
                // Rect unchanged, but the screen position may have moved
                // (owner moved). Update position only.
                if (_floatingContent.Position != pos)
                    _floatingContent.Position = pos;
            }

            // ---- 6. Clip the CONTENT to the intersection in local coords ----
            // The window's content coordinate space starts at the
            // intersection origin (since the window is at the
            // intersection). So the local clip is simply (0,0,size).
            // We still apply it so that anything larger than the window
            // (e.g. a big Button with shadow) is cropped.
            var localClip = new Rect(0, 0, intersect.Width, intersect.Height);

            if (_floatingContent.Content is Visual contentVisual)
                contentVisual.Clip = new RectangleGeometry(localClip);

            // ---- 7. Make it visible + hit-testable again ----
            if (_isClippedOut || _floatingContent.Opacity < 1.0)
            {
                _isClippedOut = false;
                _floatingContent.Opacity = 1.0;
                _floatingContent.IsHitTestVisible = true;
            }
        }
        finally
        {
            _updating = false;
        }
    }

    // ---------------------------------------------------------------------
    //  Visual tree hooks
    // ---------------------------------------------------------------------
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var parent = this.GetVisualParent();
        if (parent != null)
            parent.DetachedFromVisualTree += Parent_DetachedFromVisualTree;

        base.OnAttachedToVisualTree(e);
        InitializeNativeOverlay();
    }

    private void Parent_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window visualRoot) return;

        visualRoot.LayoutUpdated -= VisualRoot_UpdateOverlayPosition;
        visualRoot.PositionChanged -= VisualRoot_UpdateOverlayPosition;
        visualRoot.PropertyChanged -= VisualRoot_PropertyChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var parent = this.GetVisualParent();
        if (parent != null)
            parent.DetachedFromVisualTree -= Parent_DetachedFromVisualTree;

        base.OnDetachedFromVisualTree(e);
        ShowNativeOverlay(false);
    }

    // ---------------------------------------------------------------------
    //  Native control creation / destruction
    // ---------------------------------------------------------------------
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _platformHandle = base.CreateNativeControlCore(parent);
        Attach();
        HandleCreated?.Invoke(this, _platformHandle);

        // _floatingContent.Background = Brushes.Transparent;
        // if (_floatingContent.TryGetPlatformHandle()?.Handle is { } nativeHandle)
        // {
        //     MakeWindowTransparent(nativeHandle);
        //
        //     Console.WriteLine("making overlay  window transparent  222222");
        // }

        
        return _platformHandle;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _contentChangedHandler?.Dispose();
        _isVisibleChangedHandler?.Dispose();
        _floatingContentChangedHandler?.Dispose();
        _floatingContentDataContextHandler?.Dispose();
        _boundsHandler?.Dispose();

        Detach();

        if (_floatingContent != null)
        {
            if (TopLevel.GetTopLevel(this) is Window visualRoot)
            {
                visualRoot.LayoutUpdated -= VisualRoot_UpdateOverlayPosition;
                visualRoot.PositionChanged -= VisualRoot_UpdateOverlayPosition;
                visualRoot.PropertyChanged -= VisualRoot_PropertyChanged;
            }

            _floatingContent.PointerEntered -= FloatingContentOnPointerEvent;
            _floatingContent.PointerExited -= FloatingContentOnPointerEvent;
            _floatingContent.PointerPressed -= FloatingContentOnPointerEvent;
            _floatingContent.PointerReleased -= FloatingContentOnPointerEvent;

            _floatingContent.Close();
            _floatingContent = null;
        }

        base.DestroyNativeControlCore(control);
        _platformHandle = null;
    }



    public void Dispose()
    {
        
        
        
    }
}