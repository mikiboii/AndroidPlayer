using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;

namespace Androidplayer.Rendering;


public abstract class DrawingSurfaceDemoBase : Control
{
    private CompositionSurfaceVisual? _visual;
    private Compositor? _compositor;
    private readonly Action _update;
    private bool _updateQueued;
    private bool _initialized;

    protected CompositionDrawingSurface? Surface { get; private set; }

    protected DrawingSurfaceDemoBase()
    {
        _update = UpdateFrame;
    }

    // ------------------------------------------------------------------
    // Host-facing state
    // ------------------------------------------------------------------

    private string _info = string.Empty;

   
    public string Info
    {
        get => _info;
        set
        {
            if (_info == value) return;
            _info = value;
            InfoChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Raised whenever <see cref="Info"/> changes. Fires on whichever
    /// thread set the property (usually the UI thread).
    /// </summary>
    public event EventHandler? InfoChanged;

    /// <summary>
    /// Animatable rotation/disco values. The base class neither reads
    /// nor interprets these — RenderFrame implementations do. Setters
    /// are plain; callers who mutate them after init should follow up
    /// with <see cref="RequestUpdate"/> if they want an immediate redraw.
    /// </summary>
    public float Yaw   { get; set; }
    public float Pitch { get; set; }
    public float Roll  { get; set; }
    public float Disco { get; set; }

    /// <summary>
    /// Advisory flag telling the host whether it should show its "disco"
    /// control. The base class does not enforce anything based on this.
    /// </summary>
    public bool DiscoVisible { get; set; }

    /// <summary>
    /// Queues another composition frame. Useful after mutating Yaw/Pitch/
    /// Roll/Disco from outside, or after the host changes something that
    /// should be reflected on the next draw.
    /// </summary>
    public void RequestUpdate() => QueueNextFrame();

    // ------------------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Initialize();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        if (_initialized)
            FreeGraphicsResources();
        _initialized = false;
        base.OnDetachedFromLogicalTree(e);
    }

    async void Initialize()
    {
        try
        {
            var selfVisual = ElementComposition.GetElementVisual(this)!;
            _compositor = selfVisual.Compositor;

            Surface = _compositor.CreateDrawingSurface();
            _visual = _compositor.CreateSurfaceVisual();
            _visual.Size = new(Bounds.Width, Bounds.Height);
            _visual.Surface = Surface;
            ElementComposition.SetElementChildVisual(this, _visual);
            var (res, info) = await DoInitialize(_compositor, Surface);
            Info = info;
            _initialized = res;
            QueueNextFrame();
        }
        catch (Exception e)
        {
            Info = e.ToString();
        }
    }

    void UpdateFrame()
    {
        _updateQueued = false;
        var root = this.GetVisualRoot();
        if (root == null)
            return;

        _visual!.Size = new(Bounds.Width, Bounds.Height);
        var size = PixelSize.FromSize(Bounds.Size, root.RenderScaling);
        RenderFrame(size);
        if ((SupportsDisco && Disco > 0) || RunContinuously)
            QueueNextFrame();
    }

    void QueueNextFrame()
    {
        if (_initialized && !_updateQueued && _compositor != null)
        {
            _updateQueued = true;
            _compositor.RequestCompositionUpdate(_update);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == BoundsProperty)
            QueueNextFrame();
        base.OnPropertyChanged(change);
    }

    async Task<(bool success, string info)> DoInitialize(Compositor compositor,
        CompositionDrawingSurface compositionDrawingSurface)
    {
        var interop = await compositor.TryGetCompositionGpuInterop();
        if (interop == null)
            return (false, "Compositor doesn't support interop for the current backend");
        return InitializeGraphicsResources(compositor, compositionDrawingSurface, interop);
    }

    // ------------------------------------------------------------------
    // Subclass contract
    // ------------------------------------------------------------------

    protected abstract (bool success, string info) InitializeGraphicsResources(
        Compositor compositor,
        CompositionDrawingSurface compositionDrawingSurface,
        ICompositionGpuInterop gpuInterop);

    protected abstract void FreeGraphicsResources();

    protected abstract void RenderFrame(PixelSize pixelSize);

    protected virtual bool SupportsDisco => false;

    /// <summary>
    /// When true, UpdateFrame re-arms RequestCompositionUpdate on every
    /// call, so RenderFrame keeps firing continuously (tied to the
    /// compositor's own frame callback / vsync) instead of only when
    /// Bounds changes. Use this for content that animates or updates on
    /// its own (e.g. video playback), where nothing external is driving
    /// property changes to trigger redraws.
    /// </summary>
    protected virtual bool RunContinuously => false;
}