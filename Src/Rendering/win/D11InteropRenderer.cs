//
//
// using System;
// using System.Diagnostics;
// using System.Globalization;
// using System.IO;
// using System.Linq;
// using System.Threading;
// using System.Threading.Tasks;
// using Androidplayer.Store;
// using Avalonia;
// using Avalonia.Controls;
// using Avalonia.Platform;
// using Avalonia.Rendering.Composition;
// using Avalonia.Threading;
// using Avalonia.VisualTree;
//
// using SharpDX;
// using SharpDX.Direct3D;
// using SharpDX.Direct3D11;
// using SharpDX.DXGI;
// using SharpDX.D3DCompiler;
// using SharpDX.Mathematics.Interop;
// using SharpDX.WIC;
//
// using D3DDevice   = SharpDX.Direct3D11.Device;
// using DxgiFactory = SharpDX.DXGI.Factory1;
// using Buffer      = SharpDX.Direct3D11.Buffer;
// using Resource    = SharpDX.Direct3D11.Resource;
//
//
// namespace Androidplayer.Rendering.win;
//
//
//
//
// public class D11InteropRenderer : DrawingSurfaceDemoBase
// {
//     
//     
//     
//     
//     // ------------------------------------------------------------------
//     // GPU resources
//     // ------------------------------------------------------------------
//     private D3DDevice?      _device;
//     private D3D11Swapchain? _swapchain;
//
//     private Texture2D?          _staticImageTexture;
//     private ShaderResourceView? _staticImageView;
//
//     private VertexShader? _vertexShader;
//     private PixelShader?  _pixelShader;
//     private InputLayout?  _inputLayout;
//     private Buffer?       _vertexBuffer;
//     private SamplerState? _sampler;
//
//     private PixelSize _lastSize;
//     private PixelSize _lastRenderedSize;
//
//     private int _imageWidth;
//     private int _imageHeight;
//
//     private string? _currentImagePath;
//
//     // A copy of the last successfully-blitted video frame's RGBA content,
//     // kept as a shader-resource-viewable texture. When VideoProcessorBlt
//     // fails on a given tick (this happens transiently, independent of the
//     // frame-availability fix — see BlitVideoFrame's error log), we draw
//     // this instead of falling through to a cleared black frame. Recreated
//     // whenever the swapchain size changes.
//     private Texture2D?          _lastGoodFrameTexture;
//     private ShaderResourceView? _lastGoodFrameView;
//     private PixelSize           _lastGoodFrameSize;
//
//     // ------------------------------------------------------------------
//     // Interop surface
//     // ------------------------------------------------------------------
//     private CompositionDrawingSurface? _drawSurface;
//
//     // Opts into DrawingSurfaceDemoBase's own continuous-update mechanism:
//     // UpdateFrame re-arms compositor.RequestCompositionUpdate on every
//     // call, so RenderFrame keeps firing every composition frame (real
//     // vsync-driven timing, on the UI thread) instead of only when Bounds
//     // changes. This replaces the old DispatcherTimer + InvalidateVisual()
//     // pump, which posted to the wrong visual (the control's own render
//     // pass, not the composition child visual actually hosting the GPU
//     // surface) and so never actually drove redraws on its own — only a
//     // resize (which touches Bounds) ever triggered a frame.
//     protected override bool RunContinuously => true;
//
//     // ------------------------------------------------------------------
//     // Video processor (NV12 decoder -> BGRA render target)
//     // ------------------------------------------------------------------
//     private VideoDevice1?             _videoDevice1;
//     private VideoContext1?            _videoContext1;
//     private VideoProcessor?           _videoProcessor;
//     private VideoProcessorEnumerator? _vpe;
//
//     private VideoProcessorContentDescription    _vpcd;
//     private VideoProcessorInputViewDescription  _vpivd;
//     private VideoProcessorOutputViewDescription _vpovd;
//
//     // Holds the frame currently on screen. RenderFrame now fires every
//     // composition tick (RunContinuously), which is faster than the decode
//     // thread produces frames, so this must be redrawn on every tick it's
//     // still current — not consumed/disposed after a single blit. It's
//     // replaced (old one disposed, new one takes over) only when a genuinely
//     // new frame arrives via SetSourceTexture. Both the replace and every
//     // read/use happen under _d3dLock, so a redraw can never race a dispose.
//     private Texture2D? _currentVideoFrame;
//
//     private IntPtr                    _vpovTargetIdentity = IntPtr.Zero;
//     private VideoProcessorOutputView? _vpov;
//
//     private bool _videoProcessorReady;
//
//     // How much to over-allocate the video processor's declared output
//     // bounds beyond what's currently needed, so a few pixels of resize
//     // don't force a processor rebuild every single frame.
//     private const int VideoProcessorOutputPadding = 128;
//
//     // ------------------------------------------------------------------
//     // Resize debounce
//     // ------------------------------------------------------------------
//
//     private PixelSize _currentSwapchainSize;
//
//     // ------------------------------------------------------------------
// // Latency logging (decode -> draw only)
// // ------------------------------------------------------------------
//
//
//
//     private const int StatsWindowSize = 120;
//     private readonly Stopwatch _statsPrintStopwatch = new();
//     private const long StatsPrintIntervalMs = 1000;
//     private const bool EnableLatencyLogging = false;
//     
//     
//     // ------------------------------------------------------------------
//     // Decode -> draw latency
//     //
//     // Shared high-resolution clock used by both the decode thread and the
//     // render (composition) thread so timestamps taken on either thread are
//     // directly comparable.
//     // ------------------------------------------------------------------
//     private readonly Stopwatch _globalClock = Stopwatch.StartNew();
//     
//     
//     
//     public long NowTicks => _globalClock.ElapsedTicks;
//     
//
//     // Timestamp (in _globalClock ticks) of when the frame currently held in
//     // _currentVideoFrame finished decoding. Written under _d3dLock by
//     // SetSourceTexture, read under _d3dLock by RenderFrame.
//     private long _currentVideoFrameDecodeTimestamp;
//
//     // The last frame instance we've already recorded a decode->draw sample
//     // for, so a frame that gets redrawn on many consecutive ticks (because
//     // decode is slower than the render tick rate) is only timed once, at
//     // the first tick it's actually drawn — not on every redraw.
//     private Texture2D? _lastLatencyTimedFrame;
//
//     private readonly double[] _decodeToDrawHistory = new double[StatsWindowSize];
//     private int _decodeToDrawIndex;
//     private int _decodeToDrawCount;
//
//     private double _minDecodeToDrawMs = double.MaxValue;
//     private double _maxDecodeToDrawMs = double.MinValue;
//
//     // How many consecutive render ticks the currently-displayed frame has
//     // been redrawn without a new frame arriving from decode. High values
//     // mean render is outpacing decode (expected/benign); this is separate
//     // from decode->draw latency and just helps interpret it.
//     private int _currentFrameRedrawStreak;
//
//     // ==================================================================
//     // Public API
//     // ==================================================================
//
//     public D3DDevice? my_Device => _device;
//
//     public string BackendName =>
//         _device is null
//             ? "Direct3D 11 (Avalonia interop, uninitialized)"
//             : $"Direct3D 11 ({_device.FeatureLevel}) (Avalonia interop)";
//
//     public event EventHandler? Initialized;
//     public bool IsInitialized => _device is not null && _swapchain is not null;
//
//     public static D11InteropRenderer? Instance { get; private set; }
//
//     // ---- config / test ----
//     private string fileToPlay = @"M:\movie\Kung.Fu.Panda.3.2016.720p.WEBRip.x264.AAC-ETRG.mp4";
//     private Src.FFmpeg? ffmpeg;
//     private Thread? threadPlay;
//     private volatile bool is_running = true;
//
//     // Guards every access to _device.ImmediateContext (and anything that
//     // issues D3D11 device-context calls, e.g. FFmpeg's CopySubresourceRegion
//     // during decode) so the render thread and the decode thread never touch
//     // the immediate context concurrently. ID3D11DeviceContext is NOT
//     // free-threaded; concurrent use corrupts its internal state and shows
//     // up as unpredictable AccessViolationExceptions.
//     public readonly object _d3dLock = new();
//
//     // ---------------- shims ----------------
//
//     public void Initialize(IntPtr outputHandle, int d_width = 0, int d_height = 0) { }
//
//     public void DisplayImage(string fileName)
//     {
//         Console.WriteLine("disp[lay called $$$$$$");
//         if (_device == null)
//         {
//             Console.WriteLine("[AvaloniaInteropRenderer] DisplayImage before init.");
//             return;
//         }
//
//         try
//         {
//             string imagePath = Path.IsPathRooted(fileName)
//                 ? fileName
//                 : Path.Combine(Directory.GetCurrentDirectory(), fileName);
//
//             if (!File.Exists(imagePath))
//             {
//                 Console.WriteLine($"Image file not found: {imagePath}");
//                 return;
//             }
//
//             lock (_d3dLock)
//             {
//                 Utilities.Dispose(ref _staticImageView);
//                 Utilities.Dispose(ref _staticImageTexture);
//
//                 _staticImageTexture = LoadTextureFromFile(imagePath);
//                 _currentImagePath   = imagePath;
//
//                 if (_staticImageTexture != null)
//                 {
//                     _staticImageView = new ShaderResourceView(_device, _staticImageTexture);
//                     Console.WriteLine($"Successfully loaded image: {fileName}");
//                 }
//             }
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"Error displaying image {fileName}: {ex.Message}");
//         }
//     }
//
//     public Texture2D? LoadTextureFromFile(string filePath)
//     {
//         if (_device == null) return null;
//
//         try
//         {
//             using var bitmapDecoder = new BitmapDecoder(
//                 new ImagingFactory(), filePath, DecodeOptions.CacheOnLoad);
//
//             var frame = bitmapDecoder.GetFrame(0);
//             
//             using var flipRotator = new BitmapFlipRotator(new ImagingFactory());
//             flipRotator.Initialize(frame, BitmapTransformOptions.FlipVertical);
//             
//             using var formatConverter = new FormatConverter(new ImagingFactory());
//             formatConverter.Initialize(flipRotator, SharpDX.WIC.PixelFormat.Format32bppRGBA);
//
//             // using var formatConverter = new FormatConverter(new ImagingFactory());
//             // formatConverter.Initialize(frame, SharpDX.WIC.PixelFormat.Format32bppRGBA);
//
//             var width  = formatConverter.Size.Width;
//             var height = formatConverter.Size.Height;
//
//             _imageWidth  = width;
//             _imageHeight = height;
//
//           
//             
//             Console.WriteLine($"{width} x{height}");
//
//             if (My_Store.Instance.VideoHeight == 0 || My_Store.Instance.VideoHeight == 0)
//             {
//                 My_Store.Instance.SetVideoResolution((int)width, (int)height);
//             }
//
//             if (My_Store.Instance?.DeviceHeight == 0 || My_Store.Instance?.DeviceWidth == 0 && my_info.Instance.DeveloperMode)
//             {
//                 My_Store.Instance.SetDeviceResolution((int)width, (int)height);
//             }
//
//
//             var stride     = width * 4;
//             var dataStream = new DataStream(height * stride, true, true);
//             formatConverter.CopyPixels(stride, dataStream);
//
//             var textureDesc = new Texture2DDescription
//             {
//                 Width             = width,
//                 Height            = height,
//                 ArraySize         = 1,
//                 BindFlags         = BindFlags.ShaderResource | BindFlags.RenderTarget,
//                 Usage             = ResourceUsage.Default,
//                 CpuAccessFlags    = CpuAccessFlags.None,
//                 Format            = Format.R8G8B8A8_UNorm,
//                 MipLevels         = 1,
//                 OptionFlags       = ResourceOptionFlags.None,
//                 SampleDescription = new SampleDescription(1, 0)
//             };
//
//             return new Texture2D(
//                 _device,
//                 textureDesc,
//                 new DataRectangle(dataStream.DataPointer, stride));
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"Error loading texture from file: {ex.Message}");
//             return null;
//         }
//     }
//
//     
//     private const int TopOffsetPx = 0; // set >0 if you want a reserved top strip
//     
//     private void ComputeLetterboxRect(int sourceWidth, int sourceHeight,
//         out int outX, out int outY, out int outW, out int outH)
//     {
//         int bbW = _currentSwapchainSize.Width;
//         int bbH = _currentSwapchainSize.Height;
//     
//         if (sourceWidth <= 0 || sourceHeight <= 0 || bbW <= 0 || bbH <= 0)
//         {
//             outX = outY = 0;
//             outW = bbW;
//             outH = bbH;
//             return;
//         }
//     
//         int top = Math.Min(TopOffsetPx, bbH - 1);
//         // if (top < 0) top = 0;
//     
//         int availW = bbW;
//         int availH = bbH - top;
//         if (availH < 1) availH = 1;
//     
//         float srcAspect = (float)sourceWidth / sourceHeight;
//     
//         if ((float)availW / availH > srcAspect)
//         {
//             outH = availH;
//             outW = (int)Math.Round(availH * srcAspect);
//         }
//         else
//         {
//             outW = availW;
//             outH = (int)Math.Round(availW / srcAspect);
//         }
//     
//         if (outW < 1) outW = 1;
//         if (outH < 1) outH = 1;
//     
//         outX = (bbW - outW) / 2;
//         outY = top + (availH - outH) / 2;
//     
//         if (outX < 0) outX = 0;
//         if (outY < 0) outY = 0;
//         if (outX + outW > bbW) outW = bbW - outX;
//         if (outY + outH > bbH) outH = bbH - outY;
//         if (outW < 1) outW = 1;
//         if (outH < 1) outH = 1;
//     
//     
//         // Console.WriteLine($"outY : {outY}");
//     }
//     
//     
//     
//     
//     
//     // private const int TopOffsetPx = -30;
//     //
//     // private void ComputeLetterboxRect(
//     //     int sourceWidth,
//     //     int sourceHeight,
//     //     out int outX,
//     //     out int outY,
//     //     out int outW,
//     //     out int outH)
//     // {
//     //     int bbW = _currentSwapchainSize.Width;
//     //     int bbH = _currentSwapchainSize.Height;
//     //
//     //     if (sourceWidth <= 0 || sourceHeight <= 0 || bbW <= 0 || bbH <= 0)
//     //     {
//     //         outX = outY = 0;
//     //         outW = bbW;
//     //         outH = bbH;
//     //         return;
//     //     }
//     //
//     //     // Positive  = move image down
//     //     // Negative  = move image up
//     //     int top = TopOffsetPx;
//     //
//     //     // The available area begins at 'top'.
//     //     // For negative top, the available area extends above the framebuffer.
//     //     int availTop = Math.Min(top, 0);
//     //     int availBottom = bbH;
//     //
//     //     int availH = availBottom - availTop;
//     //     if (availH < 1)
//     //         availH = 1;
//     //
//     //     int availW = bbW;
//     //
//     //     float srcAspect = (float)sourceWidth / sourceHeight;
//     //
//     //     if ((float)availW / availH > srcAspect)
//     //     {
//     //         outH = availH;
//     //         outW = (int)Math.Round(availH * srcAspect);
//     //     }
//     //     else
//     //     {
//     //         outW = availW;
//     //         outH = (int)Math.Round(availW / srcAspect);
//     //     }
//     //
//     //     if (outW < 1) outW = 1;
//     //     if (outH < 1) outH = 1;
//     //
//     //     // Center inside the available region.
//     //     outX = (bbW - outW) / 2;
//     //     outY = availTop + (availH - outH) / 2;
//     //
//     //     // Apply the requested offset.
//     //     outY += top;
//     //
//     //     // Clip the rectangle to the actual framebuffer.
//     //     if (outX < 0)
//     //         outX = 0;
//     //
//     //     if (outY < 0)
//     //     {
//     //         int clipped = -outY;
//     //         outH -= clipped;
//     //         outY = 0;
//     //     }
//     //
//     //     if (outX + outW > bbW)
//     //         outW = bbW - outX;
//     //
//     //     if (outY + outH > bbH)
//     //         outH = bbH - outY;
//     //
//     //     if (outW < 1) outW = 1;
//     //     if (outH < 1) outH = 1;
//     // }
//     
//     
//     
//     
//     public void PresentStaticImage() { }
//
//     /// <summary>
//     /// Presents a decoded frame, stamping it with the moment decode
//     /// finished (in _globalClock ticks) so RenderFrame can compute
//     /// decode-to-draw latency once it's actually blitted to screen.
//     /// </summary>
//     public void PresentFrame(Texture2D textureHW, long decodeTimestamp, int d_width = 0, int d_height = 0)
//     {
//         SetSourceTexture(textureHW, decodeTimestamp);
//         
//         
//         Avalonia.Threading.Dispatcher.UIThread.Post(() =>
//         {
//             if (this.GetVisualRoot() is Avalonia.Rendering.IRenderRoot root)
//             {
//                 // This is the snippet you quoted. It's a UI-thread-only call.
//                 root.Renderer.Paint(new Rect(root.ClientSize));
//             }
//         }, Avalonia.Threading.DispatcherPriority.Render);
//     }
//
//     /// <summary>
//     /// Back-compat overload for callers that don't have a decode timestamp
//     /// to hand — stamps "now" instead, which means decode->draw latency
//     /// measured for frames presented this way only reflects post-decode
//     /// dispatch overhead, not actual decode time.
//     /// </summary>
//     public void PresentFrame(Texture2D textureHW, int d_width = 0, int d_height = 0)
//     {
//         SetSourceTexture(textureHW, _globalClock.ElapsedTicks);
//         
//         Avalonia.Threading.Dispatcher.UIThread.Post(() =>
//         {
//             if (this.GetVisualRoot() is Avalonia.Rendering.IRenderRoot root)
//             {
//                 // This is the snippet you quoted. It's a UI-thread-only call.
//                 root.Renderer.Paint(new Rect(root.ClientSize));
//             }
//         }, Avalonia.Threading.DispatcherPriority.Render);
//     }
//
//     /// <summary>
//     /// Replaces the texture that RenderFrame blits on every tick. The
//     /// previous frame is disposed here, atomically with the swap, so
//     /// RenderFrame (which holds the same lock for its whole body) always
//     /// sees either the fully-old or fully-new frame, never a disposed one.
//     ///
//     /// OWNERSHIP CAUTION: this renderer owns and disposes every texture
//     /// passed in here (either when replaced by the next call, or on
//     /// shutdown). That is only correct if FFmpeg.GetFrame() hands back a
//     /// texture this renderer exclusively owns (e.g. a fresh staging copy
//     /// per call). If GetFrame() instead returns a view into FFmpeg's own
//     /// pooled/ring-buffered D3D11VA decode surfaces, disposing it is a bug
//     /// — it can be reused or freed out from under FFmpeg, producing
//     /// intermittent NullReferenceException / AccessViolation crashes. If
//     /// you see those, check FFmpeg.GetFrame()'s texture lifetime and, if
//     /// pooled, stop disposing here (and instead just drop the reference).
//     /// </summary>
//     public void SetSourceTexture(Texture2D? texture, long decodeTimestamp)
//     {
//         if (texture == null) return;
//
//         lock (_d3dLock)
//         {
//             Utilities.Dispose(ref _currentVideoFrame);
//
//             _currentVideoFrame               = texture;
//             _currentVideoFrameDecodeTimestamp = decodeTimestamp;
//             _imageWidth                       = texture.Description.Width;
//             _imageHeight                      = texture.Description.Height;
//         }
//     }
//
//     /// <summary>Back-compat overload; stamps "now" as the decode time.</summary>
//     public void SetSourceTexture(Texture2D? texture)
//     {
//         SetSourceTexture(texture, _globalClock.ElapsedTicks);
//     }
//
//     public void PresentFrameKeepAlive() { }
//     public void HandleResize() { }
//     public void ResizeToClient(IntPtr hwnd) { }
//
//     public void ResizeSwapChain(int width, int height)
//     {
//         if (width <= 0 || height <= 0) return;
//
//         var newSize = new PixelSize(width, height);
//         if (newSize == _currentSwapchainSize) return;
//
//         _currentSwapchainSize = newSize;
//         _lastSize             = newSize;
//       
//     }
//
//     public void RunOnContext(Action<DeviceContext> action)
//     {
//         if (_device == null) return;
//         lock (_d3dLock) action(_device.ImmediateContext);
//     }
//
//     public new void Dispose() => DisposeAll();
//
//     // ==================================================================
//     // Init
//     // ==================================================================
//
//     protected override (bool success, string info) InitializeGraphicsResources(
//         Compositor compositor,
//         CompositionDrawingSurface surface,
//         ICompositionGpuInterop interop)
//     {
//         Instance     = this;
//         _drawSurface = surface;
//
//         if (interop.SupportedImageHandleTypes.Contains(
//                 KnownPlatformGraphicsExternalImageHandleTypes
//                     .D3D11TextureGlobalSharedHandle) != true)
//         {
//             return (false,
//                 "DXGI shared handle import is not supported by the current graphics backend");
//         }
//
//         using var factory = new DxgiFactory();
//         using var adapter = factory.GetAdapter1(0);
//
//         _device = new D3DDevice(
//             adapter,
//             DeviceCreationFlags.BgraSupport,
//             new[]
//             {
//                 FeatureLevel.Level_12_1,
//                 FeatureLevel.Level_12_0,
//                 FeatureLevel.Level_11_1,
//                 FeatureLevel.Level_11_0,
//                 FeatureLevel.Level_10_0,
//                 FeatureLevel.Level_9_3,
//                 FeatureLevel.Level_9_2,
//                 FeatureLevel.Level_9_1
//             });
//
//         _swapchain = new D3D11Swapchain(_device, interop, surface);
//
//         // --- Video processor setup ---
//         try
//         {
//             _videoDevice1  = _device.QueryInterface<VideoDevice1>();
//             _videoContext1 = _device.ImmediateContext.QueryInterface<VideoContext1>();
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"[Interop] Video processor not available: {ex.Message}");
//             _videoProcessorReady = false;
//         }
//
//         CreateShaders();
//         CreateQuad();
//         CreateSampler();
//
//       
//         _currentSwapchainSize = default;
//         _lastSize             = default;
//
//       
//         _statsPrintStopwatch.Restart();
//
//         Array.Clear(_decodeToDrawHistory, 0, _decodeToDrawHistory.Length);
//         _decodeToDrawIndex      = 0;
//         _decodeToDrawCount      = 0;
//         _minDecodeToDrawMs      = double.MaxValue;
//         _maxDecodeToDrawMs      = double.MinValue;
//         _lastLatencyTimedFrame  = null;
//         _currentFrameRedrawStreak = 0;
//
//         // Continuous redraws are now driven by RunContinuously (see field
//         // declaration above) via DrawingSurfaceDemoBase's own
//         // RequestCompositionUpdate chain — no separate pump needed here.
//
//         Initialized?.Invoke(this, EventArgs.Empty);
//
//         // Static image test path. To test video, comment this out and
//         // uncomment the Play_video() call below.
//         // DisplayImage("dev_img1.jpg");
//         // Play_video();
//
//         Console.WriteLine(Environment.Is64BitProcess);
//
//         return (true,
//             $"D3D11 ({_device.FeatureLevel}) {adapter.Description1.Description} [Avalonia interop]");
//     }
//
//     protected override void FreeGraphicsResources()
//     {
//         DisposeAll();
//     }
//
//     // ==================================================================
//     // Video processor setup
//     // ==================================================================
//
//     /// <summary>
//     /// Ensures a video processor + enumerator exist that can handle both
//     /// the current input frame size AND the current output/render-target
//     /// size. The D3D11 video API requires the dest/output rects passed to
//     /// VideoProcessorBlt to fit within the OutputWidth/OutputHeight the
//     /// enumerator was created with — if the window is resized larger than
//     /// whatever bounds were declared, VideoProcessorBlt fails with
//     /// E_INVALIDARG. We pad the declared bounds so small resizes don't
//     /// force a rebuild every frame, and rebuild whenever either dimension
//     /// is actually exceeded.
//     /// </summary>
//     private bool EnsureVideoProcessorFor(int inputWidth, int inputHeight, int outputWidth, int outputHeight)
//     {
//         if (_videoDevice1 == null) return false;
//         if (inputWidth <= 0 || inputHeight <= 0) return false;
//         if (outputWidth <= 0 || outputHeight <= 0) return false;
//
//         int neededOutW = Math.Max(outputWidth, inputWidth);
//         int neededOutH = Math.Max(outputHeight, inputHeight);
//
//         // if (_videoProcessorReady &&
//         //     _vpcd.InputWidth  == inputWidth &&
//         //     _vpcd.InputHeight == inputHeight &&
//         //     neededOutW <= _vpcd.OutputWidth &&
//         //     neededOutH <= _vpcd.OutputHeight)
//         // {
//         //     return true;
//         // }
//         
//         if (_videoProcessorReady &&
//             _vpcd.InputWidth  == inputWidth &&
//             _vpcd.InputHeight == inputHeight)
//         {
//             return true;
//         }
//
//         Utilities.Dispose(ref _videoProcessor);
//         Utilities.Dispose(ref _vpe);
//         Utilities.Dispose(ref _vpov);
//         _vpovTargetIdentity = IntPtr.Zero;
//
//         // int paddedOutW = neededOutW + VideoProcessorOutputPadding;
//         // int paddedOutH = neededOutH + VideoProcessorOutputPadding;
//         
//         int paddedOutW = Math.Max(4096, neededOutW);
//         int paddedOutH = Math.Max(4096, neededOutH);
//
//         _vpcd = new VideoProcessorContentDescription
//         {
//             Usage            = VideoUsage.PlaybackNormal,
//             InputFrameFormat = VideoFrameFormat.Progressive,
//             InputFrameRate   = new Rational(1, 1),
//             OutputFrameRate  = new Rational(1, 1),
//             InputWidth       = inputWidth,
//             InputHeight      = inputHeight,
//             OutputWidth      = paddedOutW,
//             OutputHeight     = paddedOutH
//         };
//
//         try
//         {
//             _videoDevice1.CreateVideoProcessorEnumerator(ref _vpcd, out _vpe);
//             _videoDevice1.CreateVideoProcessor(_vpe, 0, out _videoProcessor);
//
//             _vpivd = new VideoProcessorInputViewDescription
//             {
//                 FourCC    = 0,
//                 Dimension = VpivDimension.Texture2D,
//                 Texture2D = new Texture2DVpiv
//                 {
//                     MipSlice   = 0,
//                     ArraySlice = 0
//                 }
//             };
//
//             _vpovd = new VideoProcessorOutputViewDescription
//             {
//                 Dimension = VpovDimension.Texture2D,
//                 Texture2D = new Texture2DVpov { MipSlice = 0 }
//             };
//
//             _videoProcessorReady = true;
//             Console.WriteLine(
//                 $"[Interop] Video processor ready for input {inputWidth}x{inputHeight}, " +
//                 $"output up to {paddedOutW}x{paddedOutH}");
//             return true;
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"[Interop] Video processor creation failed: {ex.Message}");
//             _videoProcessorReady = false;
//             return false;
//         }
//     }
//
//     // ==================================================================
//     // Render
//     // ==================================================================
//
//     protected override void RenderFrame(PixelSize pixelSize)
//     {
//         if (pixelSize == default) return;
//        
//         
//         if (pixelSize.Width <= 1 || pixelSize.Height <= 1) return;
//         if (_swapchain is null || _device is null) return;
//         
//         // Thread.Sleep(200);
//
//     
//
//       
//
//         // Apply the new size immediately — no debounce. The swapchain
//         // wrapper (D3D11Swapchain.BeginDraw) is expected to rebuild its
//         // back buffer when the requested size differs from its current
//         // one. The letterbox math below reads _currentSwapchainSize, so
//         // keeping it in lockstep with the control's size avoids the
//         // "stretched until you stop dragging" artifact.
//         _lastSize             = pixelSize;
//         _currentSwapchainSize = pixelSize;
//
//         // Everything below touches the D3D11 immediate context. Hold the
//         // same lock the decode thread uses so the two never issue calls on
//         // the context concurrently (ID3D11DeviceContext is not
//         // free-threaded — concurrent use corrupts it and previously showed
//         // up as a crash deep inside FFmpeg's CopySubresourceRegion call).
//         lock (_d3dLock)
//         {
//             using (_swapchain.BeginDraw(_currentSwapchainSize, out var renderView))
//             {
//                 var context = _device.ImmediateContext;
//
//                 context.OutputMerger.SetTargets(renderView);
//                 context.ClearRenderTargetView(renderView, new RawColor4(0f, 0f, 0f, 1f));
//
//                 // Read the current frame but do NOT null or dispose it here.
//                 // RenderFrame now runs every composition tick (RunContinuously),
//                 // which is generally faster than the decode thread produces
//                 // frames — if we consumed/disposed the frame on every read,
//                 // most ticks would find nothing to draw and flash black
//                 // (that was the flicker). The same frame is redrawn on every
//                 // tick until SetSourceTexture atomically swaps it for a new
//                 // one (see SetSourceTexture for the disposal/locking story).
//                 Texture2D? frame = _currentVideoFrame;
//                 long       frameDecodeTimestamp = _currentVideoFrameDecodeTimestamp;
//
//                 bool didBlit = false;
//
//                 if (frame is not null && frame.NativePointer != IntPtr.Zero)
//                 {
//                     Texture2DDescription frameDesc;
//                     bool frameValid = true;
//
//                     try
//                     {
//                         // If the underlying native resource is already gone
//                         // (e.g. an ownership mismatch disposed/reused it
//                         // elsewhere), this throws instead of us crashing
//                         // later with a raw NullReferenceException.
//                         frameDesc = frame.Description;
//                     }
//                     catch (Exception ex)
//                     {
//                         Console.WriteLine(
//                             $"[Interop] Dropping invalid current video frame: {ex.GetType().Name}: {ex.Message}");
//                         // Also clear it from the shared field so we don't
//                         // keep retrying a dead texture on every tick.
//                         if (ReferenceEquals(frame, _currentVideoFrame))
//                             _currentVideoFrame = null;
//                         frameValid = false;
//                         frameDesc  = default;
//                         frame      = null;
//                     }
//
//                     if (frameValid &&
//                         frame is not null &&
//                         _videoDevice1  is not null &&
//                         _videoContext1 is not null &&
//                         EnsureVideoProcessorFor(
//                             frameDesc.Width, frameDesc.Height,
//                             _currentSwapchainSize.Width, _currentSwapchainSize.Height))
//                     {
//                         Texture2D? renderTexture = null;
//                         try
//                         {
//                             renderTexture = renderView.Resource.QueryInterface<Texture2D>();
//                         }
//                         catch (Exception ex)
//                         {
//                             Console.WriteLine($"[Interop] RT query failed: {ex.Message}");
//                         }
//
//                         if (renderTexture is not null)
//                         {
//                             try
//                             {
//                                 // Use the settled swapchain size consistently
//                                 // here — it's what renderTexture and the
//                                 // enumerator bounds were actually sized to.
//                                 // Using the raw incoming pixelSize instead
//                                 // (which can be ahead of _currentSwapchainSize
//                                 // during the resize debounce window) can ask
//                                 // VideoProcessorBlt for a dest rect larger
//                                 // than the real render target, producing
//                                 // E_INVALIDARG.
//                                 didBlit = BlitVideoFrame(frame, renderTexture, _currentSwapchainSize);
//
//                                 if (didBlit)
//                                 {
//                                     // BlitVideoFrame still fails transiently
//                                     // on some ticks for reasons independent
//                                     // of frame availability (see its error
//                                     // log). Cache this successful result so
//                                     // a failed tick can redraw it instead of
//                                     // falling through to a cleared black
//                                     // frame.
//                                     CaptureLastGoodFrame(context, renderTexture, _currentSwapchainSize);
//
//                                     // Decode -> draw latency: only record the
//                                     // first tick a given frame instance is
//                                     // actually blitted. RenderFrame typically
//                                     // ticks faster than decode produces new
//                                     // frames, so most ticks just redraw the
//                                     // same frame again — those redraws are
//                                     // not additional decode->draw samples.
//                                     if (!ReferenceEquals(frame, _lastLatencyTimedFrame))
//                                     {
//                                         double latencyMs =
//                                             (_globalClock.ElapsedTicks - frameDecodeTimestamp)
//                                             * 1000.0 / Stopwatch.Frequency;
//
//                                         RecordDecodeToDrawLatency(latencyMs);
//
//                                         _lastLatencyTimedFrame   = frame;
//                                         _currentFrameRedrawStreak = 0;
//                                     }
//                                     else
//                                     {
//                                         _currentFrameRedrawStreak++;
//                                     }
//                                 }
//                             }
//                             finally
//                             {
//                                 renderTexture.Dispose();
//                             }
//                         }
//                     }
//                 }
//
//                 if (!didBlit)
//                 {
//                     if (_lastGoodFrameView is not null && _lastGoodFrameSize == _currentSwapchainSize)
//                     {
//                         // DrawFullscreenQuad(context, _lastGoodFrameView, pixelSize);
//                         DrawFullscreenQuad(context, _lastGoodFrameView, pixelSize,
//                             _lastGoodFrameTexture!.Description.Width,
//                             _lastGoodFrameTexture!.Description.Height);
//                     }
//                     else if (_staticImageView is not null)
//                     {
//                         // DrawFullscreenQuad(context, _staticImageView, pixelSize);
//                         DrawFullscreenQuad(context, _staticImageView, pixelSize,
//                             _staticImageTexture!.Description.Width,
//                             _staticImageTexture!.Description.Height);
//                     }
//                 }
//
//                 context.Flush();
//
//                 // frame is intentionally NOT disposed here. It stays alive
//                 // across ticks (redrawn each time) until SetSourceTexture
//                 // replaces and disposes it, or DisposeAll tears everything
//                 // down. See SetSourceTexture's ownership caution above.
//             }
//         }
//
//         _lastRenderedSize = pixelSize;
//
//         // _frameStopwatch.Stop();
//         // RecordFrameTime(_frameStopwatch.Elapsed.TotalMilliseconds);
//         
//         
//       
//         RecordFrameTime();
//         
//         
//         
//     }
//
//     
//     
//     
//     
//     /// <summary>
//     /// Draws the fullscreen textured quad using the given shader resource
//     /// view. Shared by the static-image path and the last-good-frame
//     /// fallback path — both just sample a texture over the same quad.
//     /// </summary>
//     private void DrawFullscreenQuad(DeviceContext context, ShaderResourceView view, PixelSize pixelSize, int srcW, int srcH)
//     {
//         
//         ComputeLetterboxRect(srcW, srcH,
//             out int outX, out int outY, out int outW, out int outH);
//         
//         // context.Rasterizer.SetViewport(0, 0, pixelSize.Width, pixelSize.Height, 0f, 1f);
//         context.Rasterizer.SetViewport(outX, outY, outW, outH, 0f, 1f);
//         // context.Rasterizer.SetViewport(
//         //     0,
//         //     30,
//         //     _currentSwapchainSize.Width,
//         //     _currentSwapchainSize.Height,
//         //     0f,
//         //     1f);
//
//         context.InputAssembler.InputLayout = _inputLayout;
//         context.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
//         context.InputAssembler.SetVertexBuffers(
//             0, new VertexBufferBinding(_vertexBuffer!, sizeof(float) * 5, 0));
//
//         context.VertexShader.Set(_vertexShader);
//         context.PixelShader.Set(_pixelShader);
//         context.PixelShader.SetShaderResource(0, view);
//         context.PixelShader.SetSampler(0, _sampler);
//
//         context.Draw(6, 0);
//     }
//
//     /// <summary>
//     /// Copies the just-blitted render target into a persistent, shader-
//     /// resource-viewable texture so it can be redrawn on a later tick if
//     /// BlitVideoFrame transiently fails. Recreates the cache texture/view
//     /// whenever the swapchain size changes.
//     /// </summary>
//     private void CaptureLastGoodFrame(DeviceContext context, Texture2D sourceRenderTexture, PixelSize size)
//     {
//         if (_device is null) return;
//
//         if (_lastGoodFrameTexture is null || _lastGoodFrameSize != size)
//         {
//             Utilities.Dispose(ref _lastGoodFrameView);
//             Utilities.Dispose(ref _lastGoodFrameTexture);
//
//             try
//             {
//                 _lastGoodFrameTexture = new Texture2D(_device, new Texture2DDescription
//                 {
//                     Width             = size.Width,
//                     Height            = size.Height,
//                     ArraySize         = 1,
//                     MipLevels         = 1,
//                     Format            = Format.R8G8B8A8_UNorm,
//                     Usage             = ResourceUsage.Default,
//                     BindFlags         = BindFlags.ShaderResource,
//                     CpuAccessFlags    = CpuAccessFlags.None,
//                     OptionFlags       = ResourceOptionFlags.None,
//                     SampleDescription = new SampleDescription(1, 0)
//                 });
//                 _lastGoodFrameView = new ShaderResourceView(_device, _lastGoodFrameTexture);
//                 _lastGoodFrameSize = size;
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"[Interop] Failed to allocate last-good-frame cache: {ex.Message}");
//                 Utilities.Dispose(ref _lastGoodFrameView);
//                 Utilities.Dispose(ref _lastGoodFrameTexture);
//                 return;
//             }
//         }
//
//         try
//         {
//             context.CopyResource(sourceRenderTexture, _lastGoodFrameTexture);
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"[Interop] Failed to capture last-good-frame: {ex.Message}");
//         }
//     }
//
//     private bool BlitVideoFrame(Texture2D inputTexture, Texture2D outputTexture, PixelSize destSize)
//     {
//         if (_videoDevice1  is null ||
//             _videoContext1 is null ||
//             _videoProcessor is null ||
//             _vpe is null)
//         {
//             return false;
//         }
//
//         VideoProcessorInputView? vpiv = null;
//         try
//         {
//             
//             ComputeLetterboxRect(inputTexture.Description.Width,
//                 inputTexture.Description.Height,
//                 out int outX, out int outY, out int outW, out int outH);
//             
//             _videoDevice1.CreateVideoProcessorInputView(
//                 inputTexture, _vpe, _vpivd, out vpiv);
//
//             IntPtr outId = outputTexture.NativePointer;
//             if (_vpov is null || _vpovTargetIdentity != outId)
//             {
//                 Utilities.Dispose(ref _vpov);
//                 _videoDevice1.CreateVideoProcessorOutputView(
//                     outputTexture, _vpe, _vpovd, out _vpov);
//                 _vpovTargetIdentity = outId;
//             }
//
//             _videoContext1.VideoProcessorSetStreamMirror(
//                 _videoProcessor, 0, true, false, true); // flip vertical
//             
//             // _videoContext1.VideoProcessorSetStreamMirror(
//             //     _videoProcessor, 0, false, false, false);
//             //
//             //
//             
//             
//             _videoContext1.VideoProcessorSetStreamSourceRect(
//                 _videoProcessor, 0, true,
//                 new RawRectangle(0, 0,
//                     inputTexture.Description.Width,
//                     inputTexture.Description.Height));
//             
//           
//             
//             
//             
//
//             // _videoContext1.VideoProcessorSetStreamDestRect(
//             //     _videoProcessor, 0, true,
//             //     new RawRectangle(0, 0, destSize.Width, destSize.Height));
//             
//             _videoContext1.VideoProcessorSetStreamDestRect(
//                 _videoProcessor, 0, true,
//                 new RawRectangle(outX, outY, outX + outW, outY + outH));
//
//             _videoContext1.VideoProcessorSetOutputTargetRect(
//                 _videoProcessor, true,
//                 new RawRectangle(0, 0, destSize.Width, destSize.Height));
//
//             var stream = new VideoProcessorStream
//             {
//                 PInputSurface = vpiv,
//                 Enable        = new RawBool(true)
//             };
//             var streams = new[] { stream };
//
//             _videoContext1.VideoProcessorBlt(
//                 _videoProcessor, _vpov!, 0, 1, streams);
//
//             return true;
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"[Interop] VideoProcessorBlt failed: {ex.Message}");
//             return false;
//         }
//         finally
//         {
//             Utilities.Dispose(ref vpiv);
//         }
//     }
//
//     // ==================================================================
//     // Video playback (leave Play_video() call commented in Init to test
//     // static image only)
//     // ==================================================================
//
//     public void Play_video()
//     {
//         try
//         {
//             ffmpeg = new Src.FFmpeg();
//
//             if (!ffmpeg.InitHWAccel(_device!))
//             {
//                 Console.WriteLine("Failed to Initialize FFmpeg's HW Acceleration");
//                 return;
//             }
//
//             if (!ffmpeg.Open(fileToPlay))
//             {
//                 Console.WriteLine("FFmpeg failed to open input");
//                 return;
//             }
//
//             threadPlay = new Thread(() =>
//             {
//                 try
//                 {
//                     var swDecode  = new Stopwatch();
//                     var swPresent = new Stopwatch();
//                     var swTotal   = new Stopwatch();
//
//                     double sumDecode  = 0;
//                     double sumPresent = 0;
//                     double sumTotal   = 0;
//                     int    samples    = 0;
//                     int    frameCount = 0;
//                     const int ReportEvery = 60;
//
//                     double minTotal = double.MaxValue;
//                     double maxTotal = 0;
//
//                     while (is_running)
//                     {
//                         swTotal.Restart();
//
//                         Texture2D? textureHW;
//                         long       decodeStamp;
//                         lock (_d3dLock)
//                         {
//                             swDecode.Restart();
//                             textureHW = ffmpeg.GetFrame();
//                             swDecode.Stop();
//
//                             
//                             Avalonia.Threading.Dispatcher.UIThread.Post(() =>
//                             {
//                                 if (this.GetVisualRoot() is Avalonia.Rendering.IRenderRoot root)
//                                 {
//                                     // This is the snippet you quoted. It's a UI-thread-only call.
//                                     root.Renderer.Paint(new Rect(root.ClientSize));
//                                 }
//                             }, Avalonia.Threading.DispatcherPriority.Render);
//                             
//                             
//                             
//                             
//                             
//                             
//                             // Stamp the moment decode finished, still inside
//                             // the lock so it's as close as possible to the
//                             // actual decode-complete instant.
//                             decodeStamp = _globalClock.ElapsedTicks;
//                         }
//
//                         if (textureHW == null)
//                         {
//                             Thread.Sleep(1);
//                             continue;
//                         }
//
//                         swPresent.Restart();
//                         PresentFrame(textureHW, decodeStamp);
//                         swPresent.Stop();
//
//                         swTotal.Stop();
//
//                         double decodeMs  = swDecode.Elapsed.TotalMilliseconds;
//                         double presentMs = swPresent.Elapsed.TotalMilliseconds;
//                         double totalMs   = swTotal.Elapsed.TotalMilliseconds;
//
//                         sumDecode  += decodeMs;
//                         sumPresent += presentMs;
//                         sumTotal   += totalMs;
//                         samples++;
//
//                         if (totalMs < minTotal) minTotal = totalMs;
//                         if (totalMs > maxTotal) maxTotal = totalMs;
//
//                         if (++frameCount >= ReportEvery)
//                         {
//                             frameCount = 0;
//
//                            
//
//                             sumDecode  = 0;
//                             sumPresent = 0;
//                             sumTotal   = 0;
//                             samples    = 0;
//                             minTotal   = double.MaxValue;
//                             maxTotal   = 0;
//                         }
//
//                         double remaining = 16.67 - swTotal.Elapsed.TotalMilliseconds;
//                         // double remaining = 8 - swTotal.Elapsed.TotalMilliseconds;
//                         if (remaining > 1)
//                             Thread.Sleep((int)remaining);
//                     }
//                 }
//                 catch (Exception ex)
//                 {
//                     Console.WriteLine($"Thread error: {ex.Message}");
//                     Console.WriteLine($"Stack trace: {ex.StackTrace}");
//                 }
//             });
//
//             threadPlay.SetApartmentState(ApartmentState.STA);
//             threadPlay.Start();
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"Initialization error: {ex.Message}");
//             Console.WriteLine($"Stack trace: {ex.StackTrace}");
//         }
//     }
//
//     // ==================================================================
//     // Shaders, quad, sampler
//     // ==================================================================
//
//     private void CreateShaders()
//     {
//         const string vertexShaderCode = @"
// struct VSInput
// {
//     float3 Position : POSITION;
//     float2 TexCoord : TEXCOORD0;
// };
//
// struct VSOutput
// {
//     float4 Position : SV_POSITION;
//     float2 TexCoord : TEXCOORD0;
// };
//
// VSOutput main(VSInput input)
// {
//     VSOutput output;
//     output.Position = float4(input.Position, 1.0);
//     output.TexCoord = input.TexCoord;
//     return output;
// }";
//
//         const string pixelShaderCode = @"
// Texture2D Image : register(t0);
// SamplerState Sampler : register(s0);
//
// struct PSInput
// {
//     float4 Position : SV_POSITION;
//     float2 TexCoord : TEXCOORD0;
// };
//
// float4 main(PSInput input) : SV_TARGET
// {
//     return Image.Sample(Sampler, input.TexCoord);
// }";
//
//         using var vsByteCode = ShaderBytecode.Compile(vertexShaderCode, "main", "vs_5_0");
//         using var psByteCode = ShaderBytecode.Compile(pixelShaderCode,   "main", "ps_5_0");
//
//         _vertexShader = new VertexShader(_device!, vsByteCode);
//         _pixelShader  = new PixelShader(_device!, psByteCode);
//
//         _inputLayout = new InputLayout(
//             _device!,
//             ShaderSignature.GetInputSignature(vsByteCode),
//             new[]
//             {
//                 new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
//                 new InputElement("TEXCOORD", 0, Format.R32G32_Float,   12, 0)
//             });
//     }
//
//     // private void CreateQuad()
//     // {
//     //     var vertices = new[]
//     //     {
//     //         -1.0f,  1.0f, 0.0f,   0.0f, 1.0f,
//     //          1.0f,  1.0f, 0.0f,   1.0f, 1.0f,
//     //          1.0f, -1.0f, 0.0f,   1.0f, 0.0f,
//     //
//     //         -1.0f,  1.0f, 0.0f,   0.0f, 1.0f,
//     //          1.0f, -1.0f, 0.0f,   1.0f, 0.0f,
//     //         -1.0f, -1.0f, 0.0f,   0.0f, 0.0f
//     //     };
//     //
//     //     _vertexBuffer = Buffer.Create(_device!, BindFlags.VertexBuffer, vertices);
//     // }
//
//     private void CreateQuad()
//     {
//         var vertices = new[]
//         {
//             -1.0f,  1.0f, 0.0f,   0.0f, 0.0f,
//             1.0f,  1.0f, 0.0f,   1.0f, 0.0f,
//             1.0f, -1.0f, 0.0f,   1.0f, 1.0f,
//         
//             -1.0f,  1.0f, 0.0f,   0.0f, 0.0f,
//             1.0f, -1.0f, 0.0f,   1.0f, 1.0f,
//             -1.0f, -1.0f, 0.0f,   0.0f, 1.0f
//         };
//         
//         
//         // var vertices = new[]
//         // {
//         //     -1.0f,  1.0f, 0.0f,   0.0f, 1.0f,
//         //     1.0f,  1.0f, 0.0f,   1.0f, 1.0f,
//         //     1.0f, -1.0f, 0.0f,   1.0f, 0.0f,
//         //
//         //     -1.0f,  1.0f, 0.0f,   0.0f, 1.0f,
//         //     1.0f, -1.0f, 0.0f,   1.0f, 0.0f,
//         //     -1.0f, -1.0f, 0.0f,   0.0f, 0.0f
//         // };
//
//         Utilities.Dispose(ref _vertexBuffer);
//
//         _vertexBuffer = Buffer.Create(
//             _device!,
//             BindFlags.VertexBuffer,
//             vertices);
//     }
//
//     private void CreateSampler()
//     {
//         _sampler = new SamplerState(_device!, new SamplerStateDescription
//         {
//             Filter             = Filter.MinMagMipLinear,
//             AddressU           = TextureAddressMode.Clamp,
//             AddressV           = TextureAddressMode.Clamp,
//             AddressW           = TextureAddressMode.Clamp,
//             ComparisonFunction = Comparison.Never,
//             MinimumLod         = 0,
//             MaximumLod         = float.MaxValue
//         });
//     }
//
//     // ==================================================================
//     // Latency logging
//     // ==================================================================
//
//     private void RecordFrameTime()
//     {
//         if (!EnableLatencyLogging) return;
//         if (_statsPrintStopwatch.ElapsedMilliseconds < StatsPrintIntervalMs) return;
//
//         _statsPrintStopwatch.Restart();
//
//         PrintDecodeToDrawStats();
//     }
//
//     /// <summary>
//     /// Records one decode->draw latency sample (in milliseconds). Called
//     /// once per frame instance, the first tick it's actually blitted to
//     /// the render target — see the call site in RenderFrame.
//     /// </summary>
//     private void RecordDecodeToDrawLatency(double latencyMs)
//     {
//         if (latencyMs < 0) latencyMs = 0; // guard against clock/ordering edge cases
//
//         _decodeToDrawHistory[_decodeToDrawIndex] = latencyMs;
//         _decodeToDrawIndex = (_decodeToDrawIndex + 1) % StatsWindowSize;
//
//         if (_decodeToDrawCount < StatsWindowSize)
//             _decodeToDrawCount++;
//
//         if (latencyMs < _minDecodeToDrawMs) _minDecodeToDrawMs = latencyMs;
//         if (latencyMs > _maxDecodeToDrawMs) _maxDecodeToDrawMs = latencyMs;
//     }
//
//     /// <summary>
//     /// Prints avg/min/max/stddev of the decode->draw latency window. Safe
//     /// to call even if no samples have been recorded yet.
//     /// </summary>
//     private void PrintDecodeToDrawStats()
//     {
//         if (!EnableLatencyLogging) return;
//         if (_decodeToDrawCount == 0) return;
//
//         double sum = 0;
//         for (int i = 0; i < _decodeToDrawCount; i++)
//             sum += _decodeToDrawHistory[i];
//
//         double avg = sum / _decodeToDrawCount;
//
//         double varianceSum = 0;
//         for (int i = 0; i < _decodeToDrawCount; i++)
//         {
//             double d = _decodeToDrawHistory[i] - avg;
//             varianceSum += d * d;
//         }
//         double stdDev = Math.Sqrt(varianceSum / _decodeToDrawCount);
//
//         var msg = string.Format(
//             CultureInfo.InvariantCulture,
//             "[DecodeToDrawLatency][Interop] avg={0:F3}ms min={1:F3}ms max={2:F3}ms stddev={3:F3}ms " +
//             "window={4} redrawStreak={5}",
//             avg, _minDecodeToDrawMs, _maxDecodeToDrawMs, stdDev, _decodeToDrawCount, _currentFrameRedrawStreak);
//
//         Debug.WriteLine(msg);
//         Console.WriteLine(msg);
//
//         _minDecodeToDrawMs = double.MaxValue;
//         _maxDecodeToDrawMs = double.MinValue;
//     }
//
//     // ==================================================================
//     // Cleanup
//     // ==================================================================
//     //
//     // public void DisposeAll()
//     // {
//     //     _swapchainNeedsResize = false;
//     //     _resizeStopwatch.Reset();
//     //     _frameStopwatch.Reset();
//     //
//     //     is_running = false;
//     //     try { threadPlay?.Join(500); } catch { }
//     //
//     //     if (_swapchain is not null)
//     //     {
//     //         try { _swapchain.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
//     //         catch (Exception ex)
//     //         {
//     //             Console.WriteLine($"[AvaloniaInteropRenderer] swapchain dispose: {ex.Message}");
//     //         }
//     //         _swapchain = null;
//     //     }
//     //
//     //     lock (_d3dLock)
//     //     {
//     //         Utilities.Dispose(ref _currentVideoFrame);
//     //         Utilities.Dispose(ref _vpov);
//     //         Utilities.Dispose(ref _videoProcessor);
//     //         Utilities.Dispose(ref _vpe);
//     //         Utilities.Dispose(ref _videoContext1);
//     //         Utilities.Dispose(ref _videoDevice1);
//     //
//     //         Utilities.Dispose(ref _sampler);
//     //         Utilities.Dispose(ref _vertexBuffer);
//     //         Utilities.Dispose(ref _inputLayout);
//     //         Utilities.Dispose(ref _vertexShader);
//     //         Utilities.Dispose(ref _pixelShader);
//     //         Utilities.Dispose(ref _staticImageView);
//     //         Utilities.Dispose(ref _staticImageTexture);
//     //         Utilities.Dispose(ref _lastGoodFrameView);
//     //         Utilities.Dispose(ref _lastGoodFrameTexture);
//     //         Utilities.Dispose(ref _device);
//     //     }
//     //
//     //     _drawSurface = null;
//     // }
//     
//     
//     
//     
//     private bool _disposed;
//
//     public void DisposeAll()
//     {
//         if (_disposed) return;
//         _disposed = true;
//
//         is_running = false;                 // stop the decode thread
//         _drawSurface = null;
//
//         var swapchain = _swapchain;
//         _swapchain = null;
//
//         if (swapchain is null)
//             ReleaseD3DResources();
//         else
//             _ = swapchain.DisposeAsync().AsTask()
//                 .ContinueWith(_ => ReleaseD3DResources(), TaskScheduler.Default);
//     }
//
//     private void ReleaseD3DResources()
//     {
//         lock (_d3dLock)
//         {
//             Utilities.Dispose(ref _currentVideoFrame);
//             Utilities.Dispose(ref _vpov);
//             Utilities.Dispose(ref _videoProcessor);
//             Utilities.Dispose(ref _vpe);
//             Utilities.Dispose(ref _videoContext1);
//             Utilities.Dispose(ref _videoDevice1);
//
//             Utilities.Dispose(ref _sampler);
//             Utilities.Dispose(ref _vertexBuffer);
//             Utilities.Dispose(ref _inputLayout);
//             Utilities.Dispose(ref _vertexShader);
//             Utilities.Dispose(ref _pixelShader);
//             Utilities.Dispose(ref _staticImageView);
//             Utilities.Dispose(ref _staticImageTexture);
//             Utilities.Dispose(ref _lastGoodFrameView);
//             Utilities.Dispose(ref _lastGoodFrameTexture);
//             Utilities.Dispose(ref _device);
//         }
//     }
//     
//     
// }

















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
    private const bool EnableLatencyLogging = true;

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
                    if (EnableLastGoodFrameFallback &&
                        _lastGoodFrameView is not null &&
                        _lastGoodFrameTexture is not null &&
                        _lastGoodFrameSize == _currentSwapchainSize)
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

    private void PrintDecodeToDrawStats()
    {
        if (!EnableLatencyLogging) return;
        if (_decodeToDrawCount == 0) return;

        double sum = 0;
        for (int i = 0; i < _decodeToDrawCount; i++)
            sum += _decodeToDrawHistory[i];

        double avg = sum / _decodeToDrawCount;

        double varianceSum = 0;
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
            avg, _minDecodeToDrawMs, _maxDecodeToDrawMs, stdDev, _decodeToDrawCount, _currentFrameRedrawStreak);

        Debug.WriteLine(msg);
        Console.WriteLine(msg);

        _minDecodeToDrawMs = double.MaxValue;
        _maxDecodeToDrawMs = double.MinValue;
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