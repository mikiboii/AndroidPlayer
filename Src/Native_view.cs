//
//
//
// using System;
// using System.Reflection;
// using System.Runtime.InteropServices;
// using Avalonia;
// using Avalonia.Controls;
// using Avalonia.Data;
// using Avalonia.Input;
// using Avalonia.Layout;
// using Avalonia.Media;
// using Avalonia.Metadata;
// using Avalonia.Platform;
// using Avalonia.VisualTree;
// using LibVLCSharp.Shared;
//
//
// namespace Androidplayer;
//
//     /// <summary>
//     /// Avalonia Native_view for Windows, Linux and Mac.
//     /// </summary>
//     public class Native_view : NativeControlHost
//     {
//         private IPlatformHandle? _platformHandle = null;
//         private MediaPlayer? _mediaPlayer = null;
//         private Window? _floatingContent = null;
//         IDisposable? contentChangedHandler = null;
//         IDisposable? isVisibleChangedHandler = null;
//         IDisposable? floatingContentChangedHandler = null;
//
//         private static readonly Version AvaloniaVersion =
//             typeof(Application).Assembly.GetName().Version!;
//         private static readonly bool IsAvalonia12OrAbove = AvaloniaVersion.Major >= 12;
//
//         /// <summary>
//         /// MediaPlayer Data Bound property
//         /// </summary>
//         /// <summary>
//         /// Defines the <see cref="MediaPlayer"/> property.
//         /// </summary>
//         public static readonly DirectProperty<Native_view, MediaPlayer?> MediaPlayerProperty =
//             AvaloniaProperty.RegisterDirect<Native_view, MediaPlayer?>(
//                 nameof(MediaPlayer),
//                 o => o.MediaPlayer,
//                 (o, v) => o.MediaPlayer = v,
//                 defaultBindingMode: BindingMode.TwoWay);
//
//         /// <summary>
//         /// Defines the <see cref="Content"/> property.
//         /// </summary>
//         public static readonly StyledProperty<object?> ContentProperty =
//             AvaloniaProperty.Register<Native_view, object?>(nameof(Content));
//
//         /// <summary>
//         /// Gets or sets the MediaPlayer that will be displayed.
//         /// </summary>
//         public MediaPlayer? MediaPlayer
//         {
//             get { return _mediaPlayer; }
//             set
//             {
//                 if (ReferenceEquals(_mediaPlayer, value))
//                 {
//                     return;
//                 }
//
//                 Detach();
//                 _mediaPlayer = value;
//                 Attach();
//             }
//         }
//
//         /// <summary>
//         /// Gets or sets the content to display.
//         /// </summary>
//         [Content]
//         public object? Content
//         {
//             get => GetValue(ContentProperty);
//             set => SetValue(ContentProperty, value);
//         }
//
//         /// <inheritdoc />
//         public Native_view()
//         {
//             Initialized += (_, _) => { Attach(); };
//
//             contentChangedHandler = ContentProperty.Changed.AddClassHandler<Native_view>((s, _) => s.UpdateOverlayPosition());
//             isVisibleChangedHandler = IsVisibleProperty.Changed.AddClassHandler<Native_view>((s, _) => s.ShowNativeOverlay(s.IsVisible));
//         }
//
//         private void UpdateOverlayPosition()
//         {
//             if (_floatingContent == null || !IsVisible)
//             {
//                 return;
//             }
//             bool forceSetWidth = false, forceSetHeight = false;
//             var topLeft = new Point();
//             var child = _floatingContent.Presenter?.Child;
//
//             if (child?.IsArrangeValid == true)
//             {
//                 switch (child.HorizontalAlignment)
//                 {
//                     case HorizontalAlignment.Right:
//                         topLeft = topLeft.WithX(Bounds.Width - _floatingContent.Bounds.Width);
//                         break;
//                     case HorizontalAlignment.Center:
//                         topLeft = topLeft.WithX((Bounds.Width - _floatingContent.Bounds.Width) / 2);
//                         break;
//                     case HorizontalAlignment.Stretch:
//                         forceSetWidth = true;
//                         break;
//                     case HorizontalAlignment.Left:
//                         break;
//                     default:
//                         throw new ArgumentOutOfRangeException();
//                 }
//
//                 switch (child.VerticalAlignment)
//                 {
//                     case VerticalAlignment.Bottom:
//                         topLeft = topLeft.WithY(Bounds.Height - _floatingContent.Bounds.Height);
//                         break;
//                     case VerticalAlignment.Center:
//                         topLeft = topLeft.WithY((Bounds.Height - _floatingContent.Bounds.Height) / 2);
//                         break;
//                     case VerticalAlignment.Stretch:
//                         forceSetHeight = true;
//                         break;
//                     case VerticalAlignment.Top:
//                         break;
//                     default:
//                         throw new ArgumentOutOfRangeException();
//                 }
//             }
//
//             if (forceSetWidth && forceSetHeight)
//                 _floatingContent.SizeToContent = SizeToContent.Manual;
//             else if (forceSetHeight)
//                 _floatingContent.SizeToContent = SizeToContent.Width;
//             else if (forceSetWidth)
//                 _floatingContent.SizeToContent = SizeToContent.Height;
//             else
//                 _floatingContent.SizeToContent = SizeToContent.Manual;
//
//             _floatingContent.Width = forceSetWidth ? Bounds.Width : double.NaN;
//             _floatingContent.Height = forceSetHeight ? Bounds.Height : double.NaN;
//
//             _floatingContent.MaxWidth = Bounds.Width;
//             _floatingContent.MaxHeight = Bounds.Height;
//
//             if (!this.IsAttachedToVisualTree())
//             {
//                 return;
//             }
//
//             var newPosition = this.PointToScreen(topLeft);
//
//             if (newPosition != _floatingContent.Position)
//             {
//                 _floatingContent.Position = newPosition;
//             }
//             
//             var visualRoot = TopLevel.GetTopLevel(this);
//             if (_floatingContent.Content is Visual content && visualRoot != null && child != null)
//             {
//                 content.Clip = GetVisibleRegionAsGeometry(visualRoot, this, child.Margin);
//             }
//         }
//         
//         private static RectangleGeometry? GetVisibleRegionAsGeometry(Visual parent, Visual child, Thickness childMargin)
//         {
//             var childPosition = child.TranslatePoint(new Point(0, 0), parent);
//
//             if (!childPosition.HasValue) return null;
//         
//             var topDistance = childPosition.Value.Y + childMargin.Top;
//             var leftDistance = childPosition.Value.X + childMargin.Left;
//             var bottomDistance = parent.Bounds.Height - (childPosition.Value.Y + child.Bounds.Height + childMargin.Bottom);
//             var rightDistance = parent.Bounds.Width - (childPosition.Value.X + child.Bounds.Width + childMargin.Right);
//         
//             var region = new Rect(0, 0, child.Bounds.Width, child.Bounds.Height);
//         
//             if (topDistance < 0)
//             {
//                 region = new Rect(region.X, region.Y - topDistance, region.Width, region.Height + topDistance);
//             }
//         
//             if (leftDistance < 0)
//             {
//                 region = new Rect(region.X - leftDistance, region.Y, region.Width + leftDistance, region.Height);
//             }
//         
//             if (rightDistance < 0)
//             {
//                 region = region.WithWidth(region.Width + rightDistance);
//             }
//         
//             if (bottomDistance < 0)
//             {
//                 region = region.WithHeight(region.Height + bottomDistance);
//             }
//         
//             return new RectangleGeometry(region);
//         }
//
//         private void Attach()
//         {
//             if (_mediaPlayer == null || _platformHandle == null || !IsInitialized)
//                 return;
//
//             if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
//             {
//                 _mediaPlayer.Hwnd = _platformHandle.Handle;
//             }
//             else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
//             {
//                 _mediaPlayer.XWindow = (uint)_platformHandle.Handle;
//             }
//             else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
//             {
//                 _mediaPlayer.NsObject = _platformHandle.Handle;
//             }
//         }
//
//         private void Detach()
//         {
//             if (_mediaPlayer == null)
//                 return;
//
//             if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
//             {
//                 _mediaPlayer.Hwnd = IntPtr.Zero;
//             }
//             else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
//             {
//                 _mediaPlayer.XWindow = 0;
//             }
//             else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
//             {
//                 _mediaPlayer.NsObject = IntPtr.Zero;
//             }
//         }
//
//         private void InitializeNativeOverlay()
//         {
//             if (!this.IsAttachedToVisualTree())
//                 return;
//
//             if (TopLevel.GetTopLevel(this) is not Window visualRoot)
//             {
//                 return;
//             }
//
//             if (_floatingContent == null && Content != null)
//             {
//                 _floatingContent = new Window()
//                 {
//                     TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
//                     Background = Brushes.Transparent,
//                     SizeToContent = SizeToContent.WidthAndHeight,
//                     CanResize = false,
//                     ShowInTaskbar = false,
//                     ZIndex = int.MaxValue,
//                     Opacity = 1.0,
//                     DataContext = DataContext
//                 };
//                 SetWindowDecorationsNone(_floatingContent);
//                 floatingContentChangedHandler = _floatingContent.Bind(ContentControl.ContentProperty, this.GetObservable(ContentProperty));
//                 _floatingContent.PointerEntered += FloatingContentOnPointerEvent;
//                 _floatingContent.PointerExited += FloatingContentOnPointerEvent;
//                 _floatingContent.PointerPressed += FloatingContentOnPointerEvent;
//                 _floatingContent.PointerReleased += FloatingContentOnPointerEvent;
//
//                 visualRoot.LayoutUpdated += VisualRoot_UpdateOverlayPosition;
//                 visualRoot.PositionChanged += VisualRoot_UpdateOverlayPosition;
//             }
//
//             ShowNativeOverlay(IsEffectivelyVisible);
//         }
//
//         private static void SetWindowDecorationsNone(Window window)
//         {
//             var prop = IsAvalonia12OrAbove
//                 ? typeof(Window).GetProperty("WindowDecorations")
//                 : typeof(Window).GetProperty("SystemDecorations");
//             prop!.SetValue(window, Enum.Parse(prop.PropertyType, "None"));
//         }
//
//         private void VisualRoot_UpdateOverlayPosition(object? sender, EventArgs e) => UpdateOverlayPosition();
//
//         private void FloatingContentOnPointerEvent(object? sender, PointerEventArgs e)
//         {
//             RaiseEvent(e);
//         }
//
//         private void ShowNativeOverlay(bool show)
//         {
//             if (_floatingContent == null || _floatingContent.IsVisible == show || TopLevel.GetTopLevel(this) is not Window visualRoot)
//                 return;
//
//             if (show && this.IsAttachedToVisualTree())
//                 _floatingContent.Show(visualRoot);
//             else
//                 _floatingContent.Hide();
//         }
//
//         /// <inheritdoc />
//         protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
//         {
//             var parent = this.GetVisualParent();
//             if(parent != null)
//                 parent.DetachedFromVisualTree += Parent_DetachedFromVisualTree;
//             base.OnAttachedToVisualTree(e);
//             InitializeNativeOverlay();
//         }
//
//         private void Parent_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
//         {
//             if (TopLevel.GetTopLevel(this) is not Window visualRoot)
//             {
//                 return;
//             }
//
//             visualRoot.LayoutUpdated -= VisualRoot_UpdateOverlayPosition;
//             visualRoot.PositionChanged -= VisualRoot_UpdateOverlayPosition;
//         }
//
//         /// <inheritdoc />
//         protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
//         {
//             var parent = this.GetVisualParent();
//             if (parent != null)
//                 parent.DetachedFromVisualTree -= Parent_DetachedFromVisualTree;
//             base.OnDetachedFromVisualTree(e);
//             ShowNativeOverlay(false);
//         }
//
//         /// <inheritdoc />
//         protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
//         {
//             _platformHandle = base.CreateNativeControlCore(parent);
//             return _platformHandle;
//         }
//
//         /// <inheritdoc />
//         protected override void DestroyNativeControlCore(IPlatformHandle control)
//         {
//             contentChangedHandler?.Dispose();
//             isVisibleChangedHandler?.Dispose();
//             floatingContentChangedHandler?.Dispose();
//
//             Detach();
//             if (_floatingContent != null)
//             {
//                 _floatingContent.PointerEntered -= FloatingContentOnPointerEvent;
//                 _floatingContent.PointerExited -= FloatingContentOnPointerEvent;
//                 _floatingContent.PointerPressed -= FloatingContentOnPointerEvent;
//                 _floatingContent.PointerReleased -= FloatingContentOnPointerEvent;
//                 _floatingContent.Close();
//                 _floatingContent = null;
//             }
//
//             base.DestroyNativeControlCore(control);
//
//             if (_platformHandle != null)
//             {
//                 _platformHandle = null;
//             }
//         }
//     }






//
// using System;
// using System.Runtime.InteropServices;
// using Avalonia;
// using Avalonia.Controls;
// using Avalonia.Controls.Presenters;
// using Avalonia.Controls.Primitives;
// using Avalonia.Data;
// using Avalonia.Input;
// using Avalonia.Layout;
// using Avalonia.Media;
// using Avalonia.Metadata;
// using Avalonia.Platform;
// using Avalonia.Reactive;
// using Avalonia.VisualTree;
// using LibVLCSharp.Shared;
//
// namespace Androidplayer;
//
// /// <summary>
// /// Avalonia NativeView for Windows, Linux and Mac that hosts a LibVLC MediaPlayer
// /// and renders Avalonia content on top of it using a transparent floating window.
// /// </summary>
// public class Native_view : NativeControlHost
// {
//     private IPlatformHandle? _platformHandle;
//     private MediaPlayer? _mediaPlayer;
//     private Window? _floatingContent;
//
//     private IDisposable? _contentChangedHandler;
//     private IDisposable? _isVisibleChangedHandler;
//     private IDisposable? _floatingContentChangedHandler;
//     private IDisposable? _floatingContentDataContextHandler;
//
//     private static readonly Version AvaloniaVersion =
//         typeof(Application).Assembly.GetName().Version!;
//     private static readonly bool IsAvalonia12OrAbove = AvaloniaVersion.Major >= 12;
//
//     public static readonly DirectProperty<Native_view, MediaPlayer?> MediaPlayerProperty =
//         AvaloniaProperty.RegisterDirect<Native_view, MediaPlayer?>(
//             nameof(MediaPlayer),
//             o => o.MediaPlayer,
//             (o, v) => o.MediaPlayer = v,
//             defaultBindingMode: BindingMode.TwoWay);
//
//     public static readonly StyledProperty<object?> ContentProperty =
//         AvaloniaProperty.Register<Native_view, object?>(nameof(Content));
//
//     public MediaPlayer? MediaPlayer
//     {
//         get => _mediaPlayer;
//         set
//         {
//             if (ReferenceEquals(_mediaPlayer, value))
//                 return;
//
//             Detach();
//             _mediaPlayer = value;
//             Attach();
//         }
//     }
//
//     [Content]
//     public object? Content
//     {
//         get => GetValue(ContentProperty);
//         set => SetValue(ContentProperty, value);
//     }
//
//     public Native_view()
//     {
//         Initialized += (_, _) => Attach();
//
//         _contentChangedHandler = ContentProperty.Changed.AddClassHandler<Native_view>(
//             (s, _) => s.OnContentChanged());
//
//         _isVisibleChangedHandler = IsVisibleProperty.Changed.AddClassHandler<Native_view>(
//             (s, _) => s.ShowNativeOverlay(s.IsEffectivelyVisible));
//         
//         this.GetObservable(BoundsProperty).Subscribe(new AnonymousObserver<Rect>(_ =>
//         {
//             UpdateOverlayPosition();
//         }));
//     }
//
//     // ---------------------------------------------------------------------
//     //  Attach / Detach LibVLC to the native handle
//     // ---------------------------------------------------------------------
//     private void Attach()
//     {
//         if (_mediaPlayer == null || _platformHandle == null || !IsInitialized)
//             return;
//
//         if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
//         {
//             _mediaPlayer.Hwnd = _platformHandle.Handle;
//         }
//         else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
//         {
//             _mediaPlayer.XWindow = (uint)_platformHandle.Handle;
//         }
//         else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
//         {
//             _mediaPlayer.NsObject = _platformHandle.Handle;
//         }
//     }
//
//     private void Detach()
//     {
//         if (_mediaPlayer == null)
//             return;
//
//         if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
//             _mediaPlayer.Hwnd = IntPtr.Zero;
//         else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
//             _mediaPlayer.XWindow = 0;
//         else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
//             _mediaPlayer.NsObject = IntPtr.Zero;
//     }
//
//     // ---------------------------------------------------------------------
//     //  Overlay window lifecycle
//     // ---------------------------------------------------------------------
//     private void OnContentChanged()
//     {
//         if (_floatingContent == null)
//         {
//             InitializeNativeOverlay();
//         }
//         else
//         {
//             // Rebind the Content in case the floating window already existed
//             _floatingContentChangedHandler?.Dispose();
//             _floatingContentChangedHandler = _floatingContent.Bind(
//                 ContentControl.ContentProperty,
//                 this.GetObservable(ContentProperty));
//         }
//
//         UpdateOverlayPosition();
//     }
//
//     private void InitializeNativeOverlay()
//     {
//         if (!this.IsAttachedToVisualTree())
//             return;
//
//         if (TopLevel.GetTopLevel(this) is not Window visualRoot)
//             return;
//
//         if (_floatingContent == null)
//         {
//             _floatingContent = new Window
//             {
//                 TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
//                 Background = Brushes.Transparent,
//                 SizeToContent = SizeToContent.Manual,
//                 CanResize = false,
//                 ShowInTaskbar = false,
//                 ZIndex = int.MaxValue,
//                 Opacity = 1.0,
//                 DataContext = DataContext,
//                 WindowStartupLocation = WindowStartupLocation.Manual,
//                 SystemDecorations = SystemDecorations.None
//             };
//
//             SetWindowDecorationsNone(_floatingContent);
//
//             _floatingContentChangedHandler = _floatingContent.Bind(
//                 ContentControl.ContentProperty,
//                 this.GetObservable(ContentProperty));
//
//             // Keep DataContext in sync with the host control.
//             // _floatingContentDataContextHandler = this.GetObservable(DataContextProperty)
//             //     .Subscribe(dc => { if (_floatingContent != null) _floatingContent.DataContext = dc; });
//
//             // Keep DataContext in sync with the host control.
//             _floatingContentDataContextHandler = this
//                 .GetObservable(DataContextProperty)
//                 .Subscribe(
//                     new AnonymousObserver<object?>(dc =>
//                     {
//                         if (_floatingContent != null)
//                             _floatingContent.DataContext = dc;
//                     }));
//             
//             _floatingContent.PointerEntered += FloatingContentOnPointerEvent;
//             _floatingContent.PointerExited += FloatingContentOnPointerEvent;
//             _floatingContent.PointerPressed += FloatingContentOnPointerEvent;
//             _floatingContent.PointerReleased += FloatingContentOnPointerEvent;
//
//             visualRoot.LayoutUpdated += VisualRoot_UpdateOverlayPosition;
//             visualRoot.PositionChanged += VisualRoot_UpdateOverlayPosition;
//             visualRoot.PropertyChanged += VisualRoot_PropertyChanged;
//         }
//
//         ShowNativeOverlay(IsEffectivelyVisible);
//     }
//
//     private static void SetWindowDecorationsNone(Window window)
//     {
//         var prop = IsAvalonia12OrAbove
//             ? typeof(Window).GetProperty("WindowDecorations")
//             : typeof(Window).GetProperty("SystemDecorations");
//         prop?.SetValue(window, Enum.Parse(prop.PropertyType, "None"));
//     }
//
//     private void VisualRoot_UpdateOverlayPosition(object? sender, EventArgs e)
//         => UpdateOverlayPosition();
//
//     private void VisualRoot_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
//     {
//         // Re-clip when the parent resizes even if LayoutUpdated doesn't fire
//         // (e.g. some compositor backends).
//         if (e.Property == Visual.BoundsProperty)
//             UpdateOverlayPosition();
//     }
//
//     private void FloatingContentOnPointerEvent(object? sender, PointerEventArgs e)
//         => RaiseEvent(e);
//
//     private void ShowNativeOverlay(bool show)
//     {
//         if (_floatingContent == null)
//             return;
//
//         if (TopLevel.GetTopLevel(this) is not Window visualRoot)
//             return;
//
//         if (show && this.IsAttachedToVisualTree())
//         {
//             if (!_floatingContent.IsVisible)
//             {
//                 _floatingContent.Show(visualRoot);
//                 // Position AFTER Show() — on X11/Windows the first Position set
//                 // before Show is often ignored.
//                 UpdateOverlayPosition();
//             }
//             else
//             {
//                 UpdateOverlayPosition();
//             }
//         }
//         else
//         {
//             if (_floatingContent.IsVisible)
//                 _floatingContent.Hide();
//         }
//     }
//
//     // ---------------------------------------------------------------------
//     //  Positioning + clipping (this is what prevents the overlay from
//     //  "overlapping its parent" outside the control bounds)
//     // ---------------------------------------------------------------------
//     
//     
//     
//     private void UpdateOverlayPosition()
// {
//     if (_floatingContent == null || !_floatingContent.IsVisible || !IsEffectivelyVisible)
//         return;
//
//     if (!this.IsAttachedToVisualTree())
//         return;
//
//     if (TopLevel.GetTopLevel(this) is not Window visualRoot)
//         return;
//
//     var child = (_floatingContent.Presenter as ContentPresenter)?.Child;
//     if (child == null)
//         return;
//
//     // ---- 1. Size / alignment of the floating window inside the control ----
//     bool forceSetWidth = false, forceSetHeight = false;
//     var topLeft = new Point();
//
//     if (child.IsArrangeValid)
//     {
//         switch (child.HorizontalAlignment)
//         {
//             case HorizontalAlignment.Right:
//                 topLeft = topLeft.WithX(Bounds.Width - _floatingContent.Bounds.Width);
//                 break;
//             case HorizontalAlignment.Center:
//                 topLeft = topLeft.WithX((Bounds.Width - _floatingContent.Bounds.Width) / 2);
//                 break;
//             case HorizontalAlignment.Stretch:
//                 forceSetWidth = true;
//                 break;
//             case HorizontalAlignment.Left:
//                 break;
//         }
//
//         switch (child.VerticalAlignment)
//         {
//             case VerticalAlignment.Bottom:
//                 topLeft = topLeft.WithY(Bounds.Height - _floatingContent.Bounds.Height);
//                 break;
//             case VerticalAlignment.Center:
//                 topLeft = topLeft.WithY((Bounds.Height - _floatingContent.Bounds.Height) / 2);
//                 break;
//             case VerticalAlignment.Stretch:
//                 forceSetHeight = true;
//                 break;
//             case VerticalAlignment.Top:
//                 break;
//         }
//     }
//
//     if (forceSetWidth && forceSetHeight)
//         _floatingContent.SizeToContent = SizeToContent.Manual;
//     else if (forceSetHeight)
//         _floatingContent.SizeToContent = SizeToContent.Width;
//     else if (forceSetWidth)
//         _floatingContent.SizeToContent = SizeToContent.Height;
//     else
//         _floatingContent.SizeToContent = SizeToContent.Manual;
//
//     _floatingContent.Width  = forceSetWidth  ? Bounds.Width  : double.NaN;
//     _floatingContent.Height = forceSetHeight ? Bounds.Height : double.NaN;
//     _floatingContent.MaxWidth  = Bounds.Width;
//     _floatingContent.MaxHeight = Bounds.Height;
//
//     // ---- 2. Screen-space rects ----
//     // Our control's top-left corner on screen.
//     var controlTopLeftPixel = this.PointToScreen(new Point(0, 0));
//     var controlTopLeft = new Point(controlTopLeftPixel.X, controlTopLeftPixel.Y);
//
//     // The floating window's top-left corner should be at:
//     //   controlTopLeft + topLeft (topLeft is in control-local coords)
//     var desiredWindowTopLeft = new Point(
//         controlTopLeft.X + topLeft.X,
//         controlTopLeft.Y + topLeft.Y);
//
//     var desiredWindowTopLeftPixel = new PixelPoint(
//         (int)Math.Round(desiredWindowTopLeft.X),
//         (int)Math.Round(desiredWindowTopLeft.Y));
//
//     if (desiredWindowTopLeftPixel != _floatingContent.Position)
//         _floatingContent.Position = desiredWindowTopLeftPixel;
//
//     // ---- 3. Clip so the overlay never leaves *this control's* bounds ----
//     //      (and never leaves the main window's client area either).
//     //
//     //      controlScreenRect  = where the Native_view is on screen
//     //      windowScreenRect   = where the floating window is on screen
//     //      clipScreenRect     = intersection of those two
//     //      localClip          = clipScreenRect expressed in the floating
//     //                           window's local coordinate space
//     var controlScreenRect = new Rect(controlTopLeft, Bounds.Size);
//     var windowScreenRect  = new Rect(desiredWindowTopLeft,
//                                      _floatingContent.Bounds.Size);
//
//     var visible = controlScreenRect.Intersect(windowScreenRect);
//
//     // Also clamp to the main window's client area (excludes titlebar).
//     var clientRect = GetWindowClientRectOnScreen(visualRoot);
//     if (clientRect.HasValue)
//         visible = visible.Intersect(clientRect.Value);
//
//     if (visible.Width <= 0 || visible.Height <= 0)
//     {
//         // Fully outside → hide so it can't paint over anything else.
//         if (_floatingContent.Content is Visual v0)
//             v0.Clip = null;
//         _floatingContent.Opacity = 0;
//         return;
//     }
//
//     _floatingContent.Opacity = 1;
//
//     var localClip = new Rect(
//         visible.X - desiredWindowTopLeft.X,
//         visible.Y - desiredWindowTopLeft.Y,
//         visible.Width,
//         visible.Height);
//
//     if (_floatingContent.Content is Visual contentVisual)
//     {
//         contentVisual.Clip = new RectangleGeometry(localClip);
//     }
// }
//
// /// <summary>
// /// Returns the main window's client area in screen coordinates.
// /// Works on Windows, Linux (X11/Wayland) and macOS because it uses
// /// Avalonia's own coordinate conversion, not native APIs.
// /// </summary>
// private static Rect? GetWindowClientRectOnScreen(Window window)
// {
//     var topLeftPixel = window.PointToScreen(new Point(0, 0));
//     var topLeft = new Point(topLeftPixel.X, topLeftPixel.Y);
//
//     // ClientSize is the drawable area (excludes OS titlebar/borders).
//     // For a decorations-less window this equals Bounds.Size.
//     var size = window.ClientSize;
//     if (size.Width <= 0 || size.Height <= 0)
//         size = window.Bounds.Size;
//
//     return new Rect(topLeft, size);
// }
//
//     /// <summary>
//     /// Returns a geometry that clips <paramref name="child"/> so it never draws
//     /// outside <paramref name="parent"/>. Works on all platforms because it only
//     /// uses Avalonia's coordinate system (not native APIs).
//     /// </summary>
//     private static RectangleGeometry? GetVisibleRegionAsGeometry(
//         Visual parent, Visual child, Thickness childMargin)
//     {
//         var childPosition = child.TranslatePoint(new Point(0, 0), parent);
//         if (!childPosition.HasValue)
//             return null;
//
//         var topDistance    = childPosition.Value.Y + childMargin.Top;
//         var leftDistance   = childPosition.Value.X + childMargin.Left;
//         var bottomDistance = parent.Bounds.Height -
//                              (childPosition.Value.Y + child.Bounds.Height + childMargin.Bottom);
//         var rightDistance  = parent.Bounds.Width -
//                              (childPosition.Value.X + child.Bounds.Width + childMargin.Right);
//
//         var region = new Rect(0, 0, child.Bounds.Width, child.Bounds.Height);
//
//         if (topDistance < 0)
//             region = new Rect(region.X, region.Y - topDistance, region.Width, region.Height + topDistance);
//
//         if (leftDistance < 0)
//             region = new Rect(region.X - leftDistance, region.Y, region.Width + leftDistance, region.Height);
//
//         if (rightDistance < 0)
//             region = region.WithWidth(region.Width + rightDistance);
//
//         if (bottomDistance < 0)
//             region = region.WithHeight(region.Height + bottomDistance);
//
//         // Guard against degenerate rects (negative size)
//         if (region.Width <= 0 || region.Height <= 0)
//             region = new Rect(0, 0, 0, 0);
//
//         return new RectangleGeometry(region);
//     }
//
//     // ---------------------------------------------------------------------
//     //  Visual tree hooks
//     // ---------------------------------------------------------------------
//     protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
//     {
//         var parent = this.GetVisualParent();
//         if (parent != null)
//             parent.DetachedFromVisualTree += Parent_DetachedFromVisualTree;
//
//         base.OnAttachedToVisualTree(e);
//         InitializeNativeOverlay();
//     }
//
//     private void Parent_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
//     {
//         if (TopLevel.GetTopLevel(this) is not Window visualRoot)
//             return;
//
//         visualRoot.LayoutUpdated -= VisualRoot_UpdateOverlayPosition;
//         visualRoot.PositionChanged -= VisualRoot_UpdateOverlayPosition;
//         visualRoot.PropertyChanged -= VisualRoot_PropertyChanged;
//     }
//
//     protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
//     {
//         var parent = this.GetVisualParent();
//         if (parent != null)
//             parent.DetachedFromVisualTree -= Parent_DetachedFromVisualTree;
//
//         base.OnDetachedFromVisualTree(e);
//         ShowNativeOverlay(false);
//     }
//
//     // ---------------------------------------------------------------------
//     //  Native control creation / destruction
//     // ---------------------------------------------------------------------
//     protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
//     {
//         _platformHandle = base.CreateNativeControlCore(parent);
//         Attach();
//         return _platformHandle;
//     }
//
//     protected override void DestroyNativeControlCore(IPlatformHandle control)
//     {
//         _contentChangedHandler?.Dispose();
//         _isVisibleChangedHandler?.Dispose();
//         _floatingContentChangedHandler?.Dispose();
//         _floatingContentDataContextHandler?.Dispose();
//
//         Detach();
//
//         if (_floatingContent != null)
//         {
//             if (TopLevel.GetTopLevel(this) is Window visualRoot)
//             {
//                 visualRoot.LayoutUpdated -= VisualRoot_UpdateOverlayPosition;
//                 visualRoot.PositionChanged -= VisualRoot_UpdateOverlayPosition;
//                 visualRoot.PropertyChanged -= VisualRoot_PropertyChanged;
//             }
//
//             _floatingContent.PointerEntered -= FloatingContentOnPointerEvent;
//             _floatingContent.PointerExited -= FloatingContentOnPointerEvent;
//             _floatingContent.PointerPressed -= FloatingContentOnPointerEvent;
//             _floatingContent.PointerReleased -= FloatingContentOnPointerEvent;
//
//             _floatingContent.Close();
//             _floatingContent = null;
//         }
//
//         base.DestroyNativeControlCore(control);
//         _platformHandle = null;
//     }
// }
























using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.Reactive;
using Avalonia.VisualTree;
using LibVLCSharp.Shared;

namespace Androidplayer;

public class Native_view : NativeControlHost
{
    private IPlatformHandle? _platformHandle;
    private MediaPlayer? _mediaPlayer;
    private Window? _floatingContent;

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
    
    
    

    public IPlatformHandle? PlatformHandle => _platformHandle;
    public IntPtr NativeHandle => _platformHandle?.Handle ?? IntPtr.Zero;
    public string HandleDescriptor => _platformHandle?.HandleDescriptor ?? string.Empty;

    public event EventHandler<IPlatformHandle>? HandleCreated;

    public static readonly DirectProperty<Native_view, MediaPlayer?> MediaPlayerProperty =
        AvaloniaProperty.RegisterDirect<Native_view, MediaPlayer?>(
            nameof(MediaPlayer),
            o => o.MediaPlayer,
            (o, v) => o.MediaPlayer = v,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<Native_view, object?>(nameof(Content));

    public MediaPlayer? MediaPlayer
    {
        get => _mediaPlayer;
        set
        {
            if (ReferenceEquals(_mediaPlayer, value)) return;
            Detach();
            _mediaPlayer = value;
            Attach();
        }
    }

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
        if (_mediaPlayer == null || _platformHandle == null || !IsInitialized)
            return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            _mediaPlayer.Hwnd = _platformHandle.Handle;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            _mediaPlayer.XWindow = (uint)_platformHandle.Handle;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            _mediaPlayer.NsObject = _platformHandle.Handle;
    }

    private void Detach()
    {
        if (_mediaPlayer == null) return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            _mediaPlayer.Hwnd = IntPtr.Zero;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            _mediaPlayer.XWindow = 0;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            _mediaPlayer.NsObject = IntPtr.Zero;
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

            visualRoot.LayoutUpdated += VisualRoot_UpdateOverlayPosition;
            visualRoot.PositionChanged += VisualRoot_UpdateOverlayPosition;
            visualRoot.PropertyChanged += VisualRoot_PropertyChanged;

            _boundsHandler = this.GetObservable(BoundsProperty)
                .Subscribe(new AnonymousObserver<Rect>(_ => SyncWindows()));
        }

        ShowNativeOverlay(IsEffectivelyVisible);
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

    private void VisualRoot_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
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