




using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Androidplayer.Store;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;

using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.D3DCompiler;
using SharpDX.Mathematics.Interop;
using SharpDX.WIC;

using D3DDevice   = SharpDX.Direct3D11.Device;
using DxgiFactory = SharpDX.DXGI.Factory1;
using Buffer      = SharpDX.Direct3D11.Buffer;
using Resource    = SharpDX.Direct3D11.Resource;

namespace Androidplayer.Rendering.win;

public class D11InteropRenderer : DrawingSurfaceDemoBase
{
    // ------------------------------------------------------------------
    // GPU resources
    // ------------------------------------------------------------------
    private D3DDevice?      _device;
    private D3D11Swapchain? _swapchain;

    private Texture2D?          _staticImageTexture;
    private ShaderResourceView? _staticImageView;

    private VertexShader? _vertexShader;
    private PixelShader?  _pixelShader;
    private InputLayout?  _inputLayout;
    private Buffer?       _vertexBuffer;
    private SamplerState? _sampler;

    private PixelSize _lastSize;
    private PixelSize _lastRenderedSize;

    private int _imageWidth;
    private int _imageHeight;

    private string? _currentImagePath;

    // ------------------------------------------------------------------
    // Last-good-frame fallback (OFF by default; full-RT copy per blit)
    // ------------------------------------------------------------------
    private const bool EnableLastGoodFrameFallback = false;

    private Texture2D?          _lastGoodFrameTexture;
    private ShaderResourceView? _lastGoodFrameView;
    private PixelSize           _lastGoodFrameSize;

    // ------------------------------------------------------------------
    // Redraw-on-change tracking
    // ------------------------------------------------------------------
    private Texture2D? _lastDrawnFrame;
    private PixelSize  _lastDrawnSize;
    private bool       _staticDirty = true;
    private int        _blitFailStreak;
    private const int  MaxBlitRetries = 3;

    // ------------------------------------------------------------------
    // Resize debounce
    //
    // pixelSize can change on every composition tick during a drag-resize.
    // Applying it immediately causes the swapchain to be rebuilt every
    // tick, which races the compositor's own rebuild and produces the
    // "Could not allocate vertices/indices" spam. We require the same
    // size for ResizeStableTicks consecutive ticks before committing.
    // ------------------------------------------------------------------
    private PixelSize _currentSwapchainSize;
    private PixelSize _pendingSwapchainSize;
    private int       _pendingSwapchainTicks;
    private const int ResizeStableTicks = 2;

    // ------------------------------------------------------------------
    // Interop surface
    // ------------------------------------------------------------------
    private CompositionDrawingSurface? _drawSurface;

    // Keep RenderFrame firing every composition tick; RenderFrame itself
    // early-outs when nothing changed, so the tick is cheap.
    protected override bool RunContinuously => true;

    // ------------------------------------------------------------------
    // Video processor (NV12 decoder -> BGRA render target)
    // ------------------------------------------------------------------
    private VideoDevice1?             _videoDevice1;
    private VideoContext1?            _videoContext1;
    private VideoProcessor?           _videoProcessor;
    private VideoProcessorEnumerator? _vpe;

    private VideoProcessorInputViewDescription  _vpivd;
    private VideoProcessorOutputViewDescription _vpovd;
    private VideoProcessorContentDescription    _vpcd;

    // Frame currently on screen. Replaced (old one disposed) only when a
    // new frame arrives. All access under _d3dLock.
    private Texture2D? _currentVideoFrame;

    // Output views cached per back-buffer texture pointer.
    private readonly Dictionary<IntPtr, VideoProcessorOutputView> _vpovCache = new();
    private const int MaxOutputViewCacheSize = 8;

    private bool _videoProcessorReady;

    // Declared processor output bounds are rounded up to this multiple,
    // so small resizes don't force a processor rebuild.
    private const int VideoProcessorOutputAlignment = 256;

    // FIX: the processor is also rebuilt SMALLER when its declared output
    // is more than 2x what is needed AND larger than this. Without this the
    // processor only ever grows, and its driver memory is never returned.
    private const int VideoProcessorShrinkThreshold = VideoProcessorOutputAlignment * 4;

    // ------------------------------------------------------------------
    // Latency logging
    // ------------------------------------------------------------------
    private const int StatsWindowSize = 120;
    private readonly Stopwatch _statsPrintStopwatch = new();
    private const long StatsPrintIntervalMs = 1000;
    public const bool EnableLatencyLogging = false;

    private readonly Stopwatch _globalClock = Stopwatch.StartNew();

    public long NowTicks => _globalClock.ElapsedTicks;

    private long _currentVideoFrameDecodeTimestamp;
    private Texture2D? _lastLatencyTimedFrame;

    private readonly double[] _decodeToDrawHistory = new double[StatsWindowSize];
    private int _decodeToDrawIndex;
    private int _decodeToDrawCount;

    private double _minDecodeToDrawMs = double.MaxValue;
    private double _maxDecodeToDrawMs = double.MinValue;

    private int _currentFrameRedrawStreak;

    // ==================================================================
    // Public API
    // ==================================================================

    public D3DDevice? my_Device => _device;

    public string BackendName =>
        _device is null
            ? "Direct3D 11 (Avalonia interop, uninitialized)"
            : $"Direct3D 11 ({_device.FeatureLevel}) (Avalonia interop)";

    public event EventHandler? Initialized;
    public bool IsInitialized => _device is not null && _swapchain is not null;

    public static D11InteropRenderer? Instance { get; private set; }

    // ---- config / test ----
    private string fileToPlay = @"M:\movie\Kung.Fu.Panda.3.2016.720p.WEBRip.x264.AAC-ETRG.mp4";
    private Src.FFmpeg? ffmpeg;
    private Thread? threadPlay;
    private volatile bool is_running = true;

    // Guards every access to _device.ImmediateContext.
    public readonly object _d3dLock = new();

    // ---------------- shims ----------------

    public void Initialize(IntPtr outputHandle, int d_width = 0, int d_height = 0) { }

    public void DisplayImage(string fileName)
    {
        if (_device == null)
        {
            Console.WriteLine("[AvaloniaInteropRenderer] DisplayImage before init.");
            return;
        }

        try
        {
            string imagePath = Path.IsPathRooted(fileName)
                ? fileName
                : Path.Combine(Directory.GetCurrentDirectory(), fileName);

            if (!File.Exists(imagePath))
            {
                Console.WriteLine($"Image file not found: {imagePath}");
                return;
            }

            lock (_d3dLock)
            {
                Utilities.Dispose(ref _staticImageView);
                Utilities.Dispose(ref _staticImageTexture);

                _staticImageTexture = LoadTextureFromFile(imagePath);
                _currentImagePath   = imagePath;

                if (_staticImageTexture != null)
                {
                    _staticImageView = new ShaderResourceView(_device, _staticImageTexture);
                    Console.WriteLine($"Successfully loaded image: {fileName}");
                }

                _staticDirty = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error displaying image {fileName}: {ex.Message}");
        }
    }

    public Texture2D? LoadTextureFromFile(string filePath)
    {
        if (_device == null) return null;

        try
        {
            using var factory = new ImagingFactory();
            using var bitmapDecoder = new BitmapDecoder(factory, filePath, DecodeOptions.CacheOnLoad);

            // FIX: GetFrame returns a COM object that must be disposed.
            using var frame = bitmapDecoder.GetFrame(0);

            using var flipRotator = new BitmapFlipRotator(factory);
            flipRotator.Initialize(frame, BitmapTransformOptions.FlipVertical);

            using var formatConverter = new FormatConverter(factory);
            formatConverter.Initialize(flipRotator, SharpDX.WIC.PixelFormat.Format32bppRGBA);

            var width  = formatConverter.Size.Width;
            var height = formatConverter.Size.Height;

            _imageWidth  = width;
            _imageHeight = height;

            
                         if (My_Store.Instance.VideoHeight == 0 || My_Store.Instance.VideoHeight == 0)
             {
                 My_Store.Instance.SetVideoResolution((int)width, (int)height);
             }

             if (My_Store.Instance?.DeviceHeight == 0 || My_Store.Instance?.DeviceWidth == 0 && my_info.Instance.DeveloperMode)
             {
                 My_Store.Instance.SetDeviceResolution((int)width, (int)height);
             }
            
            
            var stride = width * 4;
            using var dataStream = new DataStream(height * stride, true, true);
            formatConverter.CopyPixels(stride, dataStream);

            var textureDesc = new Texture2DDescription
            {
                Width             = width,
                Height            = height,
                ArraySize         = 1,
                BindFlags         = BindFlags.ShaderResource | BindFlags.RenderTarget,
                Usage             = ResourceUsage.Default,
                CpuAccessFlags    = CpuAccessFlags.None,
                Format            = Format.R8G8B8A8_UNorm,
                MipLevels         = 1,
                OptionFlags       = ResourceOptionFlags.None,
                SampleDescription = new SampleDescription(1, 0)
            };

            return new Texture2D(
                _device,
                textureDesc,
                new DataRectangle(dataStream.DataPointer, stride));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading texture from file: {ex.Message}");
            return null;
        }
    }

    private const int TopOffsetPx = 0;

    private void ComputeLetterboxRect(int sourceWidth, int sourceHeight,
        out int outX, out int outY, out int outW, out int outH)
    {
        int bbW = _currentSwapchainSize.Width;
        int bbH = _currentSwapchainSize.Height;

        if (sourceWidth <= 0 || sourceHeight <= 0 || bbW <= 0 || bbH <= 0)
        {
            outX = outY = 0;
            outW = bbW;
            outH = bbH;
            return;
        }

        int top = Math.Min(TopOffsetPx, bbH - 1);

        int availW = bbW;
        int availH = bbH - top;
        if (availH < 1) availH = 1;

        float srcAspect = (float)sourceWidth / sourceHeight;

        if ((float)availW / availH > srcAspect)
        {
            outH = availH;
            outW = (int)Math.Round(availH * srcAspect);
        }
        else
        {
            outW = availW;
            outH = (int)Math.Round(availW / srcAspect);
        }

        if (outW < 1) outW = 1;
        if (outH < 1) outH = 1;

        outX = (bbW - outW) / 2;
        outY = top + (availH - outH) / 2;

        if (outX < 0) outX = 0;
        if (outY < 0) outY = 0;
        if (outX + outW > bbW) outW = bbW - outX;
        if (outY + outH > bbH) outH = bbH - outY;
        if (outW < 1) outW = 1;
        if (outH < 1) outH = 1;
    }

    public void PresentStaticImage() { }

    public void PresentFrame(Texture2D textureHW, long decodeTimestamp, int d_width = 0, int d_height = 0)
    {
        SetSourceTexture(textureHW, decodeTimestamp);
        
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (this.GetVisualRoot() is Avalonia.Rendering.IRenderRoot root)
            {
                // This is the snippet you quoted. It's a UI-thread-only call.
                root.Renderer.Paint(new Rect(root.ClientSize));
            }
        }, Avalonia.Threading.DispatcherPriority.Render);
    }

    public void PresentFrame(Texture2D textureHW, int d_width = 0, int d_height = 0)
    {
        SetSourceTexture(textureHW, _globalClock.ElapsedTicks);
        
        
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (this.GetVisualRoot() is Avalonia.Rendering.IRenderRoot root)
            {
                // This is the snippet you quoted. It's a UI-thread-only call.
                root.Renderer.Paint(new Rect(root.ClientSize));
            }
        }, Avalonia.Threading.DispatcherPriority.Render);
        
        
    }

    /// <summary>
    /// Replaces the texture RenderFrame draws. The previous frame is
    /// disposed here, atomically with the swap.
    ///
    /// OWNERSHIP CAUTION: this renderer owns and disposes every texture
    /// passed in. That is only correct if FFmpeg.GetFrame() hands back a
    /// texture this renderer exclusively owns. If GetFrame() returns a view
    /// into FFmpeg's own pooled D3D11VA surfaces, disposing it is a bug.
    /// </summary>
    public void SetSourceTexture(Texture2D? texture, long decodeTimestamp)
    {
        if (texture == null) return;

        lock (_d3dLock)
        {
            Utilities.Dispose(ref _currentVideoFrame);

            _currentVideoFrame                = texture;
            _currentVideoFrameDecodeTimestamp = decodeTimestamp;
            _imageWidth                       = texture.Description.Width;
            _imageHeight                      = texture.Description.Height;
        }
    }

    public void SetSourceTexture(Texture2D? texture)
    {
        SetSourceTexture(texture, _globalClock.ElapsedTicks);
    }

    public void PresentFrameKeepAlive() { }
    public void HandleResize() { }
    public void ResizeToClient(IntPtr hwnd) { }

    public void ResizeSwapChain(int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        var newSize = new PixelSize(width, height);
        if (newSize == _currentSwapchainSize) return;

        _currentSwapchainSize = newSize;
        _lastSize             = newSize;
    }

    public void RunOnContext(Action<DeviceContext> action)
    {
        if (_device == null) return;
        lock (_d3dLock) action(_device.ImmediateContext);
    }

    public new void Dispose() => DisposeAll();

    // ==================================================================
    // Init
    // ==================================================================

    protected override (bool success, string info) InitializeGraphicsResources(
        Compositor compositor,
        CompositionDrawingSurface surface,
        ICompositionGpuInterop interop)
    {
        Instance     = this;
        _drawSurface = surface;

        if (interop.SupportedImageHandleTypes.Contains(
                KnownPlatformGraphicsExternalImageHandleTypes
                    .D3D11TextureGlobalSharedHandle) != true)
        {
            return (false,
                "DXGI shared handle import is not supported by the current graphics backend");
        }

        using var factory = new DxgiFactory();
        using var adapter = factory.GetAdapter1(0);

        _device = new D3DDevice(
            adapter,
            DeviceCreationFlags.BgraSupport,
            new[]
            {
                FeatureLevel.Level_12_1,
                FeatureLevel.Level_12_0,
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_0,
                FeatureLevel.Level_9_3,
                FeatureLevel.Level_9_2,
                FeatureLevel.Level_9_1
            });

        _swapchain = new D3D11Swapchain(_device, interop, surface);

        try
        {
            _videoDevice1  = _device.QueryInterface<VideoDevice1>();
            _videoContext1 = _device.ImmediateContext.QueryInterface<VideoContext1>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Interop] Video processor not available: {ex.Message}");
            _videoProcessorReady = false;
        }

        CreateShaders();
        CreateQuad();
        CreateSampler();

        _currentSwapchainSize  = default;
        _pendingSwapchainSize  = default;
        _pendingSwapchainTicks = 0;
        _lastSize              = default;

        _lastDrawnFrame = null;
        _lastDrawnSize  = default;
        _staticDirty    = true;
        _blitFailStreak = 0;

        _statsPrintStopwatch.Restart();

        Array.Clear(_decodeToDrawHistory, 0, _decodeToDrawHistory.Length);
        _decodeToDrawIndex        = 0;
        _decodeToDrawCount        = 0;
        _minDecodeToDrawMs        = double.MaxValue;
        _maxDecodeToDrawMs        = double.MinValue;
        _lastLatencyTimedFrame    = null;
        _currentFrameRedrawStreak = 0;

        Initialized?.Invoke(this, EventArgs.Empty);

        // Static image test path:
        // DisplayImage("dev_img1.jpg");
        // Video test path:
        // Play_video();

        return (true,
            $"D3D11 ({_device.FeatureLevel}) {adapter.Description1.Description} [Avalonia interop]");
    }

    protected override void FreeGraphicsResources()
    {
        DisposeAll();
    }

    // ==================================================================
    // Video processor setup
    // ==================================================================

    private void ClearOutputViewCache()
    {
        foreach (var v in _vpovCache.Values)
        {
            try { v.Dispose(); } catch { }
        }
        _vpovCache.Clear();
    }

    private static int AlignUp(int value, int alignment)
        => ((value + alignment - 1) / alignment) * alignment;

    private bool EnsureVideoProcessorFor(int inputWidth, int inputHeight, int outputWidth, int outputHeight)
    {
        if (_videoDevice1 == null || _videoContext1 == null) return false;
        if (inputWidth <= 0 || inputHeight <= 0) return false;
        if (outputWidth <= 0 || outputHeight <= 0) return false;

        int neededOutW = Math.Max(outputWidth, inputWidth);
        int neededOutH = Math.Max(outputHeight, inputHeight);

        // FIX: allow the processor to shrink as well as grow.
        bool tooBig = _videoProcessorReady &&
            ((_vpcd.OutputWidth  > neededOutW * 2 && _vpcd.OutputWidth  > VideoProcessorShrinkThreshold) ||
             (_vpcd.OutputHeight > neededOutH * 2 && _vpcd.OutputHeight > VideoProcessorShrinkThreshold));

        if (_videoProcessorReady &&
            !tooBig &&
            _vpcd.InputWidth  == inputWidth &&
            _vpcd.InputHeight == inputHeight &&
            neededOutW <= _vpcd.OutputWidth &&
            neededOutH <= _vpcd.OutputHeight)
        {
            return true;
        }

        // Output views are tied to the enumerator, so they must go too.
        ClearOutputViewCache();
        Utilities.Dispose(ref _videoProcessor);
        Utilities.Dispose(ref _vpe);

        int paddedOutW = AlignUp(neededOutW, VideoProcessorOutputAlignment);
        int paddedOutH = AlignUp(neededOutH, VideoProcessorOutputAlignment);

        _vpcd = new VideoProcessorContentDescription
        {
            Usage            = VideoUsage.PlaybackNormal,
            InputFrameFormat = VideoFrameFormat.Progressive,
            InputFrameRate   = new Rational(1, 1),
            OutputFrameRate  = new Rational(1, 1),
            InputWidth       = inputWidth,
            InputHeight      = inputHeight,
            OutputWidth      = paddedOutW,
            OutputHeight     = paddedOutH
        };

        try
        {
            _videoDevice1.CreateVideoProcessorEnumerator(ref _vpcd, out _vpe);
            _videoDevice1.CreateVideoProcessor(_vpe, 0, out _videoProcessor);

            // Disable the driver's automatic enhancement (denoise, edge
            // enhancement, etc.). We only want scale + colour convert.
            try
            {
                _videoContext1.VideoProcessorSetStreamAutoProcessingMode(
                    _videoProcessor, 0, false);
            }
            catch
            {
                // Some drivers don't implement this; not fatal.
            }

            _vpivd = new VideoProcessorInputViewDescription
            {
                FourCC    = 0,
                Dimension = VpivDimension.Texture2D,
                Texture2D = new Texture2DVpiv
                {
                    MipSlice   = 0,
                    ArraySlice = 0
                }
            };

            _vpovd = new VideoProcessorOutputViewDescription
            {
                Dimension = VpovDimension.Texture2D,
                Texture2D = new Texture2DVpov { MipSlice = 0 }
            };

            _videoProcessorReady = true;
            Console.WriteLine(
                $"[Interop] Video processor ready for input {inputWidth}x{inputHeight}, " +
                $"output up to {paddedOutW}x{paddedOutH}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Interop] Video processor creation failed: {ex.Message}");
            _videoProcessorReady = false;
            return false;
        }
    }

    // ==================================================================
    // Render
    // ==================================================================

    /// <summary>
    /// Commits the incoming pixelSize to _currentSwapchainSize only when
    /// it has been stable for ResizeStableTicks consecutive ticks. This
    /// prevents a swapchain rebuild on every composition frame during a
    /// drag-resize, which was racing the compositor's own rebuild.
    /// </summary>
    private bool TryCommitSwapchainSize(PixelSize pixelSize)
    {
        if (pixelSize == _currentSwapchainSize)
        {
            _pendingSwapchainTicks = 0;
            return false;
        }

        if (pixelSize != _pendingSwapchainSize)
        {
            _pendingSwapchainSize  = pixelSize;
            _pendingSwapchainTicks = 1;
            return false;
        }

        _pendingSwapchainTicks++;
        if (_pendingSwapchainTicks < ResizeStableTicks)
            return false;

        _currentSwapchainSize  = pixelSize;
        _lastSize              = pixelSize;
        _pendingSwapchainTicks = 0;
        return true; // size just changed
    }

    protected override void RenderFrame(PixelSize pixelSize)
    {
        if (pixelSize == default) return;
        if (pixelSize.Width <= 1 || pixelSize.Height <= 1) return;
        if (_swapchain is null || _device is null) return;

        // Debounced commit. If the size hasn't settled, skip this tick.
        bool sizeChanged = TryCommitSwapchainSize(pixelSize);

        lock (_d3dLock)
        {
            // FIX #1: cached output views hold references to the OLD
            // back-buffer textures. Drop them the moment the size changes
            // so the old textures can actually be released.
            if (sizeChanged)
                ClearOutputViewCache();

            // ---- Skip the tick entirely if nothing changed ----------------
            bool newFrame = !ReferenceEquals(_currentVideoFrame, _lastDrawnFrame);

            if (!newFrame && !sizeChanged && !_staticDirty)
            {
                RecordFrameTime();
                return;
            }

            var context = _device.ImmediateContext;

            using (_swapchain.BeginDraw(_currentSwapchainSize, out var renderView))
            {
                context.OutputMerger.SetTargets(renderView);
                context.ClearRenderTargetView(renderView, new RawColor4(0f, 0f, 0f, 1f));

                Texture2D? frame = _currentVideoFrame;
                long       frameDecodeTimestamp = _currentVideoFrameDecodeTimestamp;

                bool didBlit = false;

                if (frame is not null && frame.NativePointer != IntPtr.Zero)
                {
                    Texture2DDescription frameDesc;
                    bool frameValid = true;

                    try
                    {
                        frameDesc = frame.Description;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[Interop] Dropping invalid current video frame: {ex.GetType().Name}: {ex.Message}");
                        if (ReferenceEquals(frame, _currentVideoFrame))
                            _currentVideoFrame = null;
                        frameValid = false;
                        frameDesc  = default;
                        frame      = null;
                    }

                    if (frameValid &&
                        frame is not null &&
                        _videoDevice1  is not null &&
                        _videoContext1 is not null &&
                        EnsureVideoProcessorFor(
                            frameDesc.Width, frameDesc.Height,
                            _currentSwapchainSize.Width, _currentSwapchainSize.Height))
                    {
                        Texture2D? renderTexture = null;

                        // FIX #2: renderView.Resource returns a NEW COM
                        // reference on every call. It must be disposed too,
                        // otherwise one wrapper leaks per frame and keeps
                        // old back buffers alive until the GC finalizes it.
                        Resource? rtResource = null;
                        try
                        {
                            rtResource    = renderView.Resource;
                            renderTexture = rtResource.QueryInterface<Texture2D>();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Interop] RT query failed: {ex.Message}");
                        }
                        finally
                        {
                            rtResource?.Dispose();
                        }

                        if (renderTexture is not null)
                        {
                            try
                            {
                                didBlit = BlitVideoFrame(frame, renderTexture, _currentSwapchainSize);

                                if (didBlit)
                                {
                                    if (EnableLastGoodFrameFallback)
                                        CaptureLastGoodFrame(context, renderTexture, _currentSwapchainSize);

                                    if (!ReferenceEquals(frame, _lastLatencyTimedFrame))
                                    {
                                        double latencyMs =
                                            (_globalClock.ElapsedTicks - frameDecodeTimestamp)
                                            * 1000.0 / Stopwatch.Frequency;

                                        RecordDecodeToDrawLatency(latencyMs);

                                        _lastLatencyTimedFrame    = frame;
                                        _currentFrameRedrawStreak = 0;
                                    }
                                    else
                                    {
                                        _currentFrameRedrawStreak++;
                                    }
                                }
                            }
                            finally
                            {
                                renderTexture.Dispose();
                            }
                        }
                    }
                }

                if (!didBlit)
                {
                    // if (EnableLastGoodFrameFallback &&
                    //     _lastGoodFrameView is not null &&
                    //     _lastGoodFrameTexture is not null &&
                    //     _lastGoodFrameSize == _currentSwapchainSize)
                    if (_lastGoodFrameView is not null && _lastGoodFrameSize == _currentSwapchainSize)
                    {
                        DrawFullscreenQuad(context, _lastGoodFrameView, pixelSize,
                            _lastGoodFrameTexture.Description.Width,
                            _lastGoodFrameTexture.Description.Height);
                    }
                    else if (_staticImageView is not null && _staticImageTexture is not null)
                    {
                        DrawFullscreenQuad(context, _staticImageView, pixelSize,
                            _staticImageTexture.Description.Width,
                            _staticImageTexture.Description.Height);
                    }
                }

                // FIX #3a: unbind everything that references the back buffer
                // or other resources. D3D11 will not free a resource that is
                // still bound to the pipeline, so stale bindings keep old
                // back buffers alive.
                context.PixelShader.SetShaderResource(0, null);
                context.OutputMerger.ResetTargets();

                // ---- Remember what we drew ----------------------------------
                bool hadFrame = _currentVideoFrame is not null;
                if (didBlit || !hadFrame)
                {
                    _blitFailStreak = 0;
                    _lastDrawnFrame = _currentVideoFrame;
                }
                else
                {
                    _blitFailStreak++;
                    if (_blitFailStreak >= MaxBlitRetries)
                    {
                        _blitFailStreak = 0;
                        _lastDrawnFrame = _currentVideoFrame;
                    }
                }

                _lastDrawnSize = _currentSwapchainSize;
                _staticDirty   = false;
            }

            // FIX #3b: D3D11 destroys resources lazily. Flush ONLY when the
            // size changed (not every tick, which stalled the GPU during
            // compositor rebuilds) so the driver actually frees the old
            // back buffers and processor memory. ClearState also drops any
            // remaining bindings; everything is re-set on every draw.
            if (sizeChanged)
            {
                try
                {
                    context.ClearState();
                    context.Flush();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Interop] Post-resize flush failed: {ex.Message}");
                }
            }
        }

        _lastRenderedSize = pixelSize;

        RecordFrameTime();
    }

    private void DrawFullscreenQuad(DeviceContext context, ShaderResourceView view, PixelSize pixelSize, int srcW, int srcH)
    {
        ComputeLetterboxRect(srcW, srcH,
            out int outX, out int outY, out int outW, out int outH);

        context.Rasterizer.SetViewport(outX, outY, outW, outH, 0f, 1f);

        context.InputAssembler.InputLayout = _inputLayout;
        context.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
        context.InputAssembler.SetVertexBuffers(
            0, new VertexBufferBinding(_vertexBuffer!, sizeof(float) * 5, 0));

        context.VertexShader.Set(_vertexShader);
        context.PixelShader.Set(_pixelShader);
        context.PixelShader.SetShaderResource(0, view);
        context.PixelShader.SetSampler(0, _sampler);

        context.Draw(6, 0);
    }

    private void CaptureLastGoodFrame(DeviceContext context, Texture2D sourceRenderTexture, PixelSize size)
    {
        if (_device is null) return;

        if (_lastGoodFrameTexture is null || _lastGoodFrameSize != size)
        {
            Utilities.Dispose(ref _lastGoodFrameView);
            Utilities.Dispose(ref _lastGoodFrameTexture);

            try
            {
                _lastGoodFrameTexture = new Texture2D(_device, new Texture2DDescription
                {
                    Width             = size.Width,
                    Height            = size.Height,
                    ArraySize         = 1,
                    MipLevels         = 1,
                    Format            = Format.R8G8B8A8_UNorm,
                    Usage             = ResourceUsage.Default,
                    BindFlags         = BindFlags.ShaderResource,
                    CpuAccessFlags    = CpuAccessFlags.None,
                    OptionFlags       = ResourceOptionFlags.None,
                    SampleDescription = new SampleDescription(1, 0)
                });
                _lastGoodFrameView = new ShaderResourceView(_device, _lastGoodFrameTexture);
                _lastGoodFrameSize = size;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Interop] Failed to allocate last-good-frame cache: {ex.Message}");
                Utilities.Dispose(ref _lastGoodFrameView);
                Utilities.Dispose(ref _lastGoodFrameTexture);
                return;
            }
        }

        try
        {
            context.CopyResource(sourceRenderTexture, _lastGoodFrameTexture);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Interop] Failed to capture last-good-frame: {ex.Message}");
        }
    }

    private bool BlitVideoFrame(Texture2D inputTexture, Texture2D outputTexture, PixelSize destSize)
    {
        if (_videoDevice1  is null ||
            _videoContext1 is null ||
            _videoProcessor is null ||
            _vpe is null)
        {
            return false;
        }

        VideoProcessorInputView? vpiv = null;
        try
        {
            var inDesc = inputTexture.Description;

            ComputeLetterboxRect(inDesc.Width, inDesc.Height,
                out int outX, out int outY, out int outW, out int outH);

            _videoDevice1.CreateVideoProcessorInputView(
                inputTexture, _vpe, _vpivd, out vpiv);

            // Output view: cached per back-buffer texture pointer.
            IntPtr outId = outputTexture.NativePointer;
            if (!_vpovCache.TryGetValue(outId, out var vpov))
            {
                if (_vpovCache.Count >= MaxOutputViewCacheSize)
                    ClearOutputViewCache();

                _videoDevice1.CreateVideoProcessorOutputView(
                    outputTexture, _vpe, _vpovd, out vpov);
                _vpovCache[outId] = vpov;
            }

            _videoContext1.VideoProcessorSetStreamMirror(
                _videoProcessor, 0, true, false, true); // flip vertical

            _videoContext1.VideoProcessorSetStreamSourceRect(
                _videoProcessor, 0, true,
                new RawRectangle(0, 0, inDesc.Width, inDesc.Height));

            _videoContext1.VideoProcessorSetStreamDestRect(
                _videoProcessor, 0, true,
                new RawRectangle(outX, outY, outX + outW, outY + outH));

            _videoContext1.VideoProcessorSetOutputTargetRect(
                _videoProcessor, true,
                new RawRectangle(0, 0, destSize.Width, destSize.Height));

            var stream = new VideoProcessorStream
            {
                PInputSurface = vpiv,
                Enable        = new RawBool(true)
            };
            var streams = new[] { stream };

            _videoContext1.VideoProcessorBlt(
                _videoProcessor, vpov, 0, 1, streams);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Interop] VideoProcessorBlt failed: {ex.Message}");
            return false;
        }
        finally
        {
            Utilities.Dispose(ref vpiv);
        }
    }

    // ==================================================================
    // Video playback
    // ==================================================================

    public void Play_video()
    {
        try
        {
            ffmpeg = new Src.FFmpeg();

            if (!ffmpeg.InitHWAccel(_device!))
            {
                Console.WriteLine("Failed to Initialize FFmpeg's HW Acceleration");
                return;
            }

            if (!ffmpeg.Open(fileToPlay))
            {
                Console.WriteLine("FFmpeg failed to open input");
                return;
            }

            threadPlay = new Thread(() =>
            {
                try
                {
                    var swTotal = new Stopwatch();

                    while (is_running)
                    {
                        swTotal.Restart();

                        Texture2D? textureHW = null;
                        long       decodeStamp = 0;

                        // One lock acquisition per frame: decode + swap
                        // the current frame atomically.
                        lock (_d3dLock)
                        {
                            textureHW = ffmpeg.GetFrame();
                            decodeStamp = _globalClock.ElapsedTicks;

                            if (textureHW is not null)
                            {
                                Utilities.Dispose(ref _currentVideoFrame);

                                _currentVideoFrame                = textureHW;
                                _currentVideoFrameDecodeTimestamp = decodeStamp;
                                _imageWidth                       = textureHW.Description.Width;
                                _imageHeight                      = textureHW.Description.Height;
                            }
                        }

                        if (textureHW == null)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        double remaining = 16.67 - swTotal.Elapsed.TotalMilliseconds;
                        if (remaining > 1)
                            Thread.Sleep((int)remaining);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Thread error: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            });

            threadPlay.SetApartmentState(ApartmentState.STA);
            threadPlay.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Initialization error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    // ==================================================================
    // Shaders, quad, sampler
    // ==================================================================

    private void CreateShaders()
    {
        const string vertexShaderCode = @"
struct VSInput
{
    float3 Position : POSITION;
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

VSOutput main(VSInput input)
{
    VSOutput output;
    output.Position = float4(input.Position, 1.0);
    output.TexCoord = input.TexCoord;
    return output;
}";

        const string pixelShaderCode = @"
Texture2D Image : register(t0);
SamplerState Sampler : register(s0);

struct PSInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

float4 main(PSInput input) : SV_TARGET
{
    return Image.Sample(Sampler, input.TexCoord);
}";

        using var vsByteCode = ShaderBytecode.Compile(vertexShaderCode, "main", "vs_5_0");
        using var psByteCode = ShaderBytecode.Compile(pixelShaderCode,   "main", "ps_5_0");

        _vertexShader = new VertexShader(_device!, vsByteCode);
        _pixelShader  = new PixelShader(_device!, psByteCode);

        _inputLayout = new InputLayout(
            _device!,
            ShaderSignature.GetInputSignature(vsByteCode),
            new[]
            {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float,   12, 0)
            });
    }

    private void CreateQuad()
    {
        var vertices = new[]
        {
            -1.0f,  1.0f, 0.0f,   0.0f, 0.0f,
             1.0f,  1.0f, 0.0f,   1.0f, 0.0f,
             1.0f, -1.0f, 0.0f,   1.0f, 1.0f,

            -1.0f,  1.0f, 0.0f,   0.0f, 0.0f,
             1.0f, -1.0f, 0.0f,   1.0f, 1.0f,
            -1.0f, -1.0f, 0.0f,   0.0f, 1.0f
        };

        Utilities.Dispose(ref _vertexBuffer);

        _vertexBuffer = Buffer.Create(
            _device!,
            BindFlags.VertexBuffer,
            vertices);
    }

    private void CreateSampler()
    {
        _sampler = new SamplerState(_device!, new SamplerStateDescription
        {
            Filter             = Filter.MinMagMipLinear,
            AddressU           = TextureAddressMode.Clamp,
            AddressV           = TextureAddressMode.Clamp,
            AddressW           = TextureAddressMode.Clamp,
            ComparisonFunction = Comparison.Never,
            MinimumLod         = 0,
            MaximumLod         = float.MaxValue
        });
    }

    // ==================================================================
    // Latency logging
    // ==================================================================

    private void RecordFrameTime()
    {
        if (!EnableLatencyLogging) return;
        if (_statsPrintStopwatch.ElapsedMilliseconds < StatsPrintIntervalMs) return;

        _statsPrintStopwatch.Restart();

        PrintDecodeToDrawStats();
    }

    private void RecordDecodeToDrawLatency(double latencyMs)
    {
        if (!EnableLatencyLogging) return;
        if (latencyMs < 0) latencyMs = 0;

        _decodeToDrawHistory[_decodeToDrawIndex] = latencyMs;
        _decodeToDrawIndex = (_decodeToDrawIndex + 1) % StatsWindowSize;

        if (_decodeToDrawCount < StatsWindowSize)
            _decodeToDrawCount++;

        if (latencyMs < _minDecodeToDrawMs) _minDecodeToDrawMs = latencyMs;
        if (latencyMs > _maxDecodeToDrawMs) _maxDecodeToDrawMs = latencyMs;
    }

    // private void PrintDecodeToDrawStats()
    // {
    //     if (!EnableLatencyLogging) return;
    //     if (_decodeToDrawCount == 0) return;
    //
    //     double sum = 0;
    //     for (int i = 0; i < _decodeToDrawCount; i++)
    //         sum += _decodeToDrawHistory[i];
    //
    //     double avg = sum / _decodeToDrawCount;
    //
    //     double varianceSum = 0;
    //     for (int i = 0; i < _decodeToDrawCount; i++)
    //     {
    //         double d = _decodeToDrawHistory[i] - avg;
    //         varianceSum += d * d;
    //     }
    //     double stdDev = Math.Sqrt(varianceSum / _decodeToDrawCount);
    //     
    //    
    //     // var msg = string.Format(
    //     //     CultureInfo.InvariantCulture,
    //     //     "[DecodeToDrawLatency][Interop] avg={0:F3}ms min={1:F3}ms max={2:F3}ms stddev={3:F3}ms " +
    //     //     "window={4} redrawStreak={5}",
    //     //     avg, _minDecodeToDrawMs, _maxDecodeToDrawMs, stdDev, _decodeToDrawCount, _currentFrameRedrawStreak);
    //
    //     var min = _decodeToDrawCount > 0 ? _minDecodeToDrawMs : 0.0;
    //     var max = _decodeToDrawCount > 0 ? _maxDecodeToDrawMs : 0.0;
    //     
    //     var msg = string.Format(
    //         CultureInfo.InvariantCulture,
    //         "[DecodeToDrawLatency][Interop] avg={0:F3}ms min={1:F3}ms max={2:F3}ms stddev={3:F3}ms " +
    //         "window={4} redrawStreak={5}",
    //         avg, min, max, stdDev, _decodeToDrawCount, _currentFrameRedrawStreak);
    //     
    //     Debug.WriteLine(msg);
    //     Console.WriteLine(msg);
    //
    //     _minDecodeToDrawMs = double.MaxValue;
    //     _maxDecodeToDrawMs = double.MinValue;
    // }

    
    
    
    private void PrintDecodeToDrawStats()
    {
        if (!EnableLatencyLogging) return;
        if (_decodeToDrawCount == 0) return;

        double sum = 0.0;
        double min = double.MaxValue;
        double max = double.MinValue;

        for (int i = 0; i < _decodeToDrawCount; i++)
        {
            double value = _decodeToDrawHistory[i];

            sum += value;

            if (value < min)
                min = value;

            if (value > max)
                max = value;
        }

        double avg = sum / _decodeToDrawCount;

        double varianceSum = 0.0;

        for (int i = 0; i < _decodeToDrawCount; i++)
        {
            double d = _decodeToDrawHistory[i] - avg;
            varianceSum += d * d;
        }

        double stdDev = Math.Sqrt(varianceSum / _decodeToDrawCount);

        var msg = string.Format(
            CultureInfo.InvariantCulture,
            "[DecodeToDrawLatency][Interop] avg={0:F3}ms min={1:F3}ms max={2:F3}ms stddev={3:F3}ms " +
            "window={4} redrawStreak={5}",
            avg,
            min,
            max,
            stdDev,
            _decodeToDrawCount,
            _currentFrameRedrawStreak);

        Debug.WriteLine(msg);
        Console.WriteLine(msg);
    }
    
    
    
    // ==================================================================
    // Cleanup
    // ==================================================================

    private bool _disposed;

    public void DisposeAll()
    {
        if (_disposed) return;
        _disposed = true;

        is_running   = false;
        _drawSurface = null;

        var swapchain = _swapchain;
        _swapchain = null;

        if (swapchain is null)
            ReleaseD3DResources();
        else
            _ = swapchain.DisposeAsync().AsTask()
                .ContinueWith(_ => ReleaseD3DResources(), TaskScheduler.Default);
    }

    private void ReleaseD3DResources()
    {
        lock (_d3dLock)
        {
            _lastDrawnFrame = null;

            Utilities.Dispose(ref _currentVideoFrame);
            ClearOutputViewCache();
            Utilities.Dispose(ref _videoProcessor);
            Utilities.Dispose(ref _vpe);
            Utilities.Dispose(ref _videoContext1);
            Utilities.Dispose(ref _videoDevice1);

            Utilities.Dispose(ref _sampler);
            Utilities.Dispose(ref _vertexBuffer);
            Utilities.Dispose(ref _inputLayout);
            Utilities.Dispose(ref _vertexShader);
            Utilities.Dispose(ref _pixelShader);
            Utilities.Dispose(ref _staticImageView);
            Utilities.Dispose(ref _staticImageTexture);
            Utilities.Dispose(ref _lastGoodFrameView);
            Utilities.Dispose(ref _lastGoodFrameTexture);
            Utilities.Dispose(ref _device);
        }
    }
}