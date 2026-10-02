


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Androidplayer.Store;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;

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
    // Core GPU objects
    // ------------------------------------------------------------------
    private D3DDevice?      _device;
    private D3D11Swapchain? _swapchain;

    // Guards every access to _device.ImmediateContext and the frame fields.
    public readonly object _d3dLock = new();

    // RenderFrame fires every composition tick; it early-outs when nothing changed.
    protected override bool RunContinuously => true;

    // ------------------------------------------------------------------
    // Static image (shown when there is no video frame)
    // ------------------------------------------------------------------
    private Texture2D?          _staticImageTexture;
    private ShaderResourceView? _staticImageView;

    private VertexShader? _vertexShader;
    private PixelShader?  _pixelShader;
    private InputLayout?  _inputLayout;
    private Buffer?       _vertexBuffer;
    private SamplerState? _sampler;

    // ------------------------------------------------------------------
    // Resize debounce
    // A swapchain rebuild on every tick of a drag-resize races the
    // compositor, so a new size must be stable for ResizeStableTicks ticks.
    // ------------------------------------------------------------------
    private PixelSize _currentSwapchainSize;
    private PixelSize _pendingSwapchainSize;
    private int       _pendingSwapchainTicks;
    private const int ResizeStableTicks = 2;

    // ------------------------------------------------------------------
    // Video processor (decoder texture -> back buffer, scale + colour convert)
    // ------------------------------------------------------------------
    private VideoDevice1?             _videoDevice1;
    private VideoContext1?            _videoContext1;
    private VideoProcessor?           _videoProcessor;
    private VideoProcessorEnumerator? _vpe;

    private VideoProcessorInputViewDescription  _vpivd;
    private VideoProcessorOutputViewDescription _vpovd;
    private VideoProcessorContentDescription    _vpcd;
    private bool _videoProcessorReady;

    // Output views cached per back-buffer texture pointer.
    private readonly Dictionary<IntPtr, VideoProcessorOutputView> _vpovCache = new();
    private const int MaxOutputViewCacheSize = 8;

    // Declared output size is rounded up to this, so small resizes don't
    // force a rebuild. It also shrinks when it is more than 2x too big.
    private const int VideoProcessorOutputAlignment = 256;
    private const int VideoProcessorShrinkThreshold = VideoProcessorOutputAlignment * 4;

    // ------------------------------------------------------------------
    // Current frame (all access under _d3dLock)
    // ------------------------------------------------------------------
    private Texture2D? _currentVideoFrame;
    private bool       _currentVideoFrameOwned = true; // false => never Dispose, FFmpeg owns it
    private int        _currentVideoFrameSlice;        // array slice inside the texture
    private long       _currentFrameSerial;            // bumped on every new frame
    private long       _lastDrawnSerial = -1;
    private bool       _forceRedraw = true;

    // ------------------------------------------------------------------
    // Last-good-frame copy of the back buffer. If a blit fails, this is
    // copied back instead of presenting a black frame.
    // ------------------------------------------------------------------
    private Texture2D? _lastGoodFrameTexture;
    private PixelSize  _lastGoodFrameSize;
    private Format     _lastGoodFrameFormat;

    // ------------------------------------------------------------------
    // Misc
    // ------------------------------------------------------------------
    private readonly Stopwatch _globalClock = Stopwatch.StartNew();
    public long NowTicks => _globalClock.ElapsedTicks;

    private string fileToPlay = @"M:\movie\Kung.Fu.Panda.3.2016.720p.WEBRip.x264.AAC-ETRG.mp4";
    private Src.FFmpeg? ffmpeg;
    private Thread? threadPlay;
    private volatile bool is_running = true;
    private bool _disposed;

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

    // ---- shims (kept so existing callers still compile) ----
    public void Initialize(IntPtr outputHandle, int d_width = 0, int d_height = 0) { }
    public void PresentStaticImage() { }
    public void PresentFrameKeepAlive() { }
    public void HandleResize() { }
    public void ResizeToClient(IntPtr hwnd) { }

    public void PresentFrame(Texture2D textureHW, long decodeTimestamp, int d_width = 0, int d_height = 0)
        => SetSourceTexture(textureHW, decodeTimestamp);

    public void PresentFrame(Texture2D textureHW, int d_width = 0, int d_height = 0)
        => SetSourceTexture(textureHW, _globalClock.ElapsedTicks);

    /// <summary>
    /// Replaces the frame RenderFrame draws.
    ///
    /// ownsTexture = true : this renderer disposes the texture when it is replaced.
    /// ownsTexture = false: the texture belongs to FFmpeg (pooled D3D11VA surface);
    ///                      pass the frame's array slice and never dispose it here.
    /// </summary>
    public void SetSourceTexture(Texture2D? texture, long decodeTimestamp = 0,
                                 int arraySlice = 0, bool ownsTexture = true)
    {
        if (texture == null) return;

        lock (_d3dLock)
        {
            var old = _currentVideoFrame;
            if (old != null && _currentVideoFrameOwned && !ReferenceEquals(old, texture))
            {
                try { old.Dispose(); } catch { }
            }

            _currentVideoFrame      = texture;
            _currentVideoFrameOwned = ownsTexture;
            _currentVideoFrameSlice = arraySlice;
            _currentFrameSerial++;
        }
    }

    public void ResizeSwapChain(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        _currentSwapchainSize = new PixelSize(width, height);
    }

    public void RunOnContext(Action<DeviceContext> action)
    {
        if (_device == null) return;
        lock (_d3dLock) action(_device.ImmediateContext);
    }

    public new void Dispose() => DisposeAll();

    // ==================================================================
    // Static image
    // ==================================================================

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

                if (_staticImageTexture != null)
                {
                    _staticImageView = new ShaderResourceView(_device, _staticImageTexture);
                    Console.WriteLine($"Successfully loaded image: {fileName}");
                }

                _forceRedraw = true;
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
            using var frame = bitmapDecoder.GetFrame(0);

            using var flipRotator = new BitmapFlipRotator(factory);
            flipRotator.Initialize(frame, BitmapTransformOptions.FlipVertical);

            using var formatConverter = new FormatConverter(factory);
            formatConverter.Initialize(flipRotator, SharpDX.WIC.PixelFormat.Format32bppRGBA);

            var width  = formatConverter.Size.Width;
            var height = formatConverter.Size.Height;

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

    // ==================================================================
    // Init
    // ==================================================================

    protected override (bool success, string info) InitializeGraphicsResources(
        Compositor compositor,
        CompositionDrawingSurface surface,
        ICompositionGpuInterop interop)
    {
        Instance = this;

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
        _lastDrawnSerial       = -1;
        _forceRedraw           = true;

        Initialized?.Invoke(this, EventArgs.Empty);

        // Static image test path:
        // DisplayImage("dev_img1.jpg");
        // Video test path:
        // Play_video();

        return (true,
            $"D3D11 ({_device.FeatureLevel}) {adapter.Description1.Description} [Avalonia interop]");
    }

    protected override void FreeGraphicsResources() => DisposeAll();

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

            // Scale + colour convert only; no driver enhancement.
            try { _videoContext1.VideoProcessorSetStreamAutoProcessingMode(_videoProcessor, 0, false); }
            catch { /* not implemented on some drivers; not fatal */ }

            _vpivd = new VideoProcessorInputViewDescription
            {
                FourCC    = 0,
                Dimension = VpivDimension.Texture2D,
                Texture2D = new Texture2DVpiv { MipSlice = 0, ArraySlice = 0 }
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
    /// Commits pixelSize once it has been stable for ResizeStableTicks ticks.
    /// The very first size commits immediately. Returns true when the
    /// committed size just changed.
    /// </summary>
    private bool TryCommitSwapchainSize(PixelSize pixelSize)
    {
        if (_currentSwapchainSize == default)
        {
            _currentSwapchainSize  = pixelSize;
            _pendingSwapchainSize  = default;
            _pendingSwapchainTicks = 0;
            return true;
        }

        if (pixelSize == _currentSwapchainSize)
        {
            _pendingSwapchainSize  = default;
            _pendingSwapchainTicks = 0;
            return false;
        }

        if (pixelSize != _pendingSwapchainSize)
        {
            _pendingSwapchainSize  = pixelSize;
            _pendingSwapchainTicks = 1;
            return false;
        }

        if (++_pendingSwapchainTicks < ResizeStableTicks)
            return false;

        _currentSwapchainSize  = pixelSize;
        _pendingSwapchainSize  = default;
        _pendingSwapchainTicks = 0;
        return true;
    }

    protected override void RenderFrame(PixelSize pixelSize)
    {
        if (pixelSize == default) return;
        if (pixelSize.Width <= 1 || pixelSize.Height <= 1) return;
        if (_swapchain is null || _device is null) return;

        bool sizeChanged = TryCommitSwapchainSize(pixelSize);

        // Size still settling: keep showing the last presented image.
        if (pixelSize != _currentSwapchainSize) return;

        lock (_d3dLock)
        {
            // Cached output views reference the OLD back buffers.
            if (sizeChanged)
                ClearOutputViewCache();

            bool newFrame = _currentFrameSerial != _lastDrawnSerial;
            if (!newFrame && !sizeChanged && !_forceRedraw)
                return;

            var context = _device.ImmediateContext;

            using (_swapchain.BeginDraw(_currentSwapchainSize, out var renderView))
            {
                // renderView.Resource returns a NEW COM reference each call,
                // so resolve once and dispose the wrapper.
                Texture2D? renderTexture = null;
                Resource?  rtResource    = null;
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

                try
                {
                    context.OutputMerger.SetTargets(renderView);
                    context.ClearRenderTargetView(renderView, new RawColor4(0f, 0f, 0f, 1f));

                    bool didBlit = false;
                    Texture2D? frame = _currentVideoFrame;

                    if (renderTexture is not null && frame is not null && frame.NativePointer != IntPtr.Zero)
                    {
                        Texture2DDescription frameDesc = default;
                        bool frameValid = true;

                        try
                        {
                            frameDesc = frame.Description;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Interop] Dropping invalid video frame: {ex.Message}");
                            _currentVideoFrame = null;
                            frameValid = false;
                        }

                        if (frameValid &&
                            EnsureVideoProcessorFor(
                                frameDesc.Width, frameDesc.Height,
                                _currentSwapchainSize.Width, _currentSwapchainSize.Height))
                        {
                            didBlit = BlitVideoFrame(frame, _currentVideoFrameSlice, renderTexture, _currentSwapchainSize);

                            // One retry with fresh output views: a stale view
                            // keyed by a recycled back-buffer pointer is a
                            // classic cause of a single black frame.
                            if (!didBlit)
                            {
                                ClearOutputViewCache();
                                didBlit = BlitVideoFrame(frame, _currentVideoFrameSlice, renderTexture, _currentSwapchainSize);
                            }

                            if (didBlit)
                                CaptureLastGoodFrame(context, renderTexture);
                        }
                    }

                    if (!didBlit)
                    {
                        bool drawn = false;

                        // A video frame exists but the blit failed:
                        // show the previous picture instead of black.
                        if (_currentVideoFrame is not null && renderTexture is not null)
                            drawn = TryRestoreLastGoodFrame(context, renderTexture, renderView);

                        // No video (or nothing to restore): show the static image.
                        if (!drawn && _staticImageView is not null && _staticImageTexture is not null)
                        {
                            DrawFullscreenQuad(context, _staticImageView,
                                _staticImageTexture.Description.Width,
                                _staticImageTexture.Description.Height);
                        }
                    }

                    // Unbind so D3D11 can free old back buffers.
                    context.PixelShader.SetShaderResource(0, null);
                    context.OutputMerger.ResetTargets();

                    _lastDrawnSerial = _currentFrameSerial;
                    _forceRedraw     = false;
                }
                finally
                {
                    renderTexture?.Dispose();
                }
            }

            // D3D11 frees resources lazily; flush only on size change.
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
    }

    // ------------------------------------------------------------------
    // Letterbox
    // ------------------------------------------------------------------
    private void ComputeLetterboxRect(int srcW, int srcH,
        out int outX, out int outY, out int outW, out int outH)
    {
        int bbW = _currentSwapchainSize.Width;
        int bbH = _currentSwapchainSize.Height;

        if (srcW <= 0 || srcH <= 0 || bbW <= 0 || bbH <= 0)
        {
            outX = outY = 0;
            outW = bbW;
            outH = bbH;
            return;
        }

        float srcAspect = (float)srcW / srcH;

        if ((float)bbW / bbH > srcAspect)
        {
            outH = bbH;
            outW = (int)Math.Round(bbH * srcAspect);
        }
        else
        {
            outW = bbW;
            outH = (int)Math.Round(bbW / srcAspect);
        }

        outW = Math.Clamp(outW, 1, bbW);
        outH = Math.Clamp(outH, 1, bbH);
        outX = (bbW - outW) / 2;
        outY = (bbH - outH) / 2;
    }

    // ------------------------------------------------------------------
    // Static image quad
    // ------------------------------------------------------------------
    private void DrawFullscreenQuad(DeviceContext context, ShaderResourceView view, int srcW, int srcH)
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

    // ------------------------------------------------------------------
    // Video blit
    // ------------------------------------------------------------------
    private bool BlitVideoFrame(Texture2D inputTexture, int arraySlice,
                                Texture2D outputTexture, PixelSize destSize)
    {
        if (_videoDevice1 is null || _videoContext1 is null ||
            _videoProcessor is null || _vpe is null)
            return false;

        VideoProcessorInputView? vpiv = null;
        try
        {
            var inDesc = inputTexture.Description;

            ComputeLetterboxRect(inDesc.Width, inDesc.Height,
                out int outX, out int outY, out int outW, out int outH);

            // D3D11VA pool textures are texture arrays: use the frame's slice.
            var vpivd = _vpivd;
            vpivd.Texture2D = new Texture2DVpiv
            {
                MipSlice   = 0,
                ArraySlice = inDesc.ArraySize > 1 ? arraySlice : 0
            };

            _videoDevice1.CreateVideoProcessorInputView(inputTexture, _vpe, vpivd, out vpiv);

            IntPtr outId = outputTexture.NativePointer;
            if (!_vpovCache.TryGetValue(outId, out var vpov))
            {
                if (_vpovCache.Count >= MaxOutputViewCacheSize)
                    ClearOutputViewCache();

                _videoDevice1.CreateVideoProcessorOutputView(outputTexture, _vpe, _vpovd, out vpov);
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

            var streams = new[]
            {
                new VideoProcessorStream { PInputSurface = vpiv, Enable = new RawBool(true) }
            };

            _videoContext1.VideoProcessorBlt(_videoProcessor, vpov, 0, 1, streams);
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

    // ------------------------------------------------------------------
    // Last good frame (same size + format as the back buffer => CopyResource)
    // ------------------------------------------------------------------
    private void CaptureLastGoodFrame(DeviceContext context, Texture2D backBuffer)
    {
        if (_device is null) return;

        Texture2DDescription src;
        try { src = backBuffer.Description; }
        catch { return; }

        var size = new PixelSize(src.Width, src.Height);

        if (_lastGoodFrameTexture is null ||
            _lastGoodFrameSize   != size ||
            _lastGoodFrameFormat != src.Format)
        {
            Utilities.Dispose(ref _lastGoodFrameTexture);

            try
            {
                _lastGoodFrameTexture = new Texture2D(_device, new Texture2DDescription
                {
                    Width             = src.Width,
                    Height            = src.Height,
                    ArraySize         = 1,
                    MipLevels         = 1,
                    Format            = src.Format, // must match the back buffer
                    Usage             = ResourceUsage.Default,
                    BindFlags         = BindFlags.None,
                    CpuAccessFlags    = CpuAccessFlags.None,
                    OptionFlags       = ResourceOptionFlags.None,
                    SampleDescription = new SampleDescription(1, 0)
                });
                _lastGoodFrameSize   = size;
                _lastGoodFrameFormat = src.Format;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Interop] Failed to allocate last-good-frame: {ex.Message}");
                Utilities.Dispose(ref _lastGoodFrameTexture);
                return;
            }
        }

        try { context.CopyResource(backBuffer, _lastGoodFrameTexture); }
        catch (Exception ex) { Console.WriteLine($"[Interop] Capture last-good-frame failed: {ex.Message}"); }
    }

    private bool TryRestoreLastGoodFrame(DeviceContext context, Texture2D backBuffer, RenderTargetView renderView)
    {
        if (_lastGoodFrameTexture is null) return false;

        try
        {
            var dst = backBuffer.Description;
            if (dst.Width  != _lastGoodFrameSize.Width  ||
                dst.Height != _lastGoodFrameSize.Height ||
                dst.Format != _lastGoodFrameFormat)
                return false;

            // Don't copy into a texture that is bound as the current target.
            context.OutputMerger.ResetTargets();
            context.CopyResource(_lastGoodFrameTexture, backBuffer);
            context.OutputMerger.SetTargets(renderView);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Interop] Restore last-good-frame failed: {ex.Message}");
            try { context.OutputMerger.SetTargets(renderView); } catch { }
            return false;
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
                    var sw = new Stopwatch();

                    while (is_running)
                    {
                        sw.Restart();

                        // Decode with NO lock held, so a slow decode never
                        // blocks the render tick.
                        Texture2D? texture = ffmpeg.GetFrame();

                        if (texture == null)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        // Brief lock inside: swap + dispose the old frame.
                        // If GetFrame() returns FFmpeg's pooled surface use:
                        //   SetSourceTexture(texture, stamp, sliceIndex, ownsTexture: false)
                        SetSourceTexture(texture, _globalClock.ElapsedTicks);

                        double remaining = 16.67 - sw.Elapsed.TotalMilliseconds;
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
    // Cleanup
    // ==================================================================

    public void DisposeAll()
    {
        if (_disposed) return;
        _disposed = true;

        is_running = false;

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
            if (_currentVideoFrameOwned)
                Utilities.Dispose(ref _currentVideoFrame);
            else
                _currentVideoFrame = null;

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
            Utilities.Dispose(ref _lastGoodFrameTexture);
            Utilities.Dispose(ref _device);
        }
    }
}