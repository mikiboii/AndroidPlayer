//
// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.IO;
// using System.Linq;
// using System.Runtime.InteropServices;
// using System.Threading;
// using System.Threading.Tasks;
//
// using Avalonia;
// using Avalonia.Media;
// using Avalonia.Platform;
// using Avalonia.Rendering.Composition;
// using Avalonia.VisualTree;
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
// namespace Androidplayer.Rendering.win;
//
// // NOTE: requires <AllowUnsafeBlocks>true</AllowUnsafeBlocks> in the .csproj
// public class D11InteropRenderer : DrawingSurfaceDemoBase
// {
//     // ==================================================================
//     // Modes
//     //
//     //   Swapchain  : full Avalonia GPU interop, VideoProcessor blits into
//     //                the composition surface's swapchain image.
//     //   GpuShader  : D3D11 device works, but Avalonia interop doesn't.
//     //                The frame is drawn with a pixel shader into an
//     //                offscreen texture ON THE CALLING (DECODER) THREAD,
//     //                read back, and copied once into a WriteableBitmap.
//     //                The UI thread only ever draws that bitmap.
//     //                (Same idea as the HWND DirectX class: all D3D work
//     //                happens on the thread that presents the frame.)
//     //   Software   : no D3D11 device at all. Pure CPU path via
//     //                SetSoftwareFrame.
//     // ==================================================================
//
//     private volatile bool _gpuShaderMode;
//     private volatile bool _softwareMode;
//     public  bool IsSoftwareMode  => _softwareMode;
//     public  bool IsGpuShaderMode => _gpuShaderMode;
//
//     // Only swapchain mode needs RenderFrame to fire every composition tick.
//     // Shader mode renders from PresentFrame (decoder thread); software mode
//     // draws in Render(). Neither should spin the UI thread.
//     protected override bool RunContinuously => !_softwareMode && !_gpuShaderMode;
//
//     // ------------------------------------------------------------------
//     // Core GPU objects
//     // ------------------------------------------------------------------
//     private D3DDevice?      _device;
//     private D3D11Swapchain? _swapchain;
//
//     public readonly object _d3dLock = new();
//
//     // ------------------------------------------------------------------
//     // Software (CPU) rendering path
//     //
//     // Frames are copied ONCE, directly into a WriteableBitmap on the
//     // producer thread. Render() only draws; it never copies pixels.
//     // ------------------------------------------------------------------
//     private readonly object _swLock = new();
//     private int _swW, _swH;
//     private long _swSerial;
//     private Avalonia.Media.Imaging.WriteableBitmap? _swBitmap;
//     private Avalonia.Media.Imaging.WriteableBitmap? _swRetired;   // old bitmap, disposed on UI thread
//     private Avalonia.Media.Imaging.Bitmap? _swStaticImage;
//     private int _swInvalidatePending;
//
//     // ------------------------------------------------------------------
//     // GPU-shader-mode offscreen target (sized to the SOURCE frame, so the
//     // GPU/WARP does no scaling; Avalonia scales the bitmap on draw)
//     // ------------------------------------------------------------------
//     private Texture2D?          _shaderTarget;
//     private ShaderResourceView? _shaderTargetSrv;
//     private RenderTargetView?   _shaderTargetRtv;
//     private PixelSize           _shaderTargetSize;
//
//     private Texture2D? _readbackStaging;
//     private PixelSize  _readbackSize;
//
//     // ------------------------------------------------------------------
//     // Static image (GPU modes)
//     // ------------------------------------------------------------------
//     private Texture2D?          _staticImageTexture;
//     private ShaderResourceView? _staticImageView;
//
//     private VertexShader? _vertexShader;
//     private PixelShader?  _pixelShader;
//     private InputLayout?  _inputLayout;
//     private Buffer?       _vertexBuffer;
//     private SamplerState? _sampler;
//
//     // ------------------------------------------------------------------
//     // Resize debounce (swapchain mode)
//     // ------------------------------------------------------------------
//     private PixelSize _currentSwapchainSize;
//     private PixelSize _pendingSwapchainSize;
//     private int       _pendingSwapchainTicks;
//     private const int ResizeStableTicks = 2;
//
//     // ------------------------------------------------------------------
//     // Video processor
//     // ------------------------------------------------------------------
//     private VideoDevice1?             _videoDevice1;
//     private VideoContext1?            _videoContext1;
//     private VideoProcessor?           _videoProcessor;
//     private VideoProcessorEnumerator? _vpe;
//
//     private VideoProcessorInputViewDescription  _vpivd;
//     private VideoProcessorOutputViewDescription _vpovd;
//     private VideoProcessorContentDescription    _vpcd;
//     private bool _videoProcessorReady;
//
//     private readonly Dictionary<IntPtr, VideoProcessorOutputView> _vpovCache = new();
//     private const int MaxOutputViewCacheSize = 8;
//
//     private const int VideoProcessorOutputAlignment = 256;
//     private const int VideoProcessorShrinkThreshold = VideoProcessorOutputAlignment * 4;
//
//     // ------------------------------------------------------------------
//     // Current frame (all access under _d3dLock)
//     // ------------------------------------------------------------------
//     private Texture2D? _currentVideoFrame;
//     private bool       _currentVideoFrameOwned = true;
//     private int        _currentVideoFrameSlice;
//     private long       _currentFrameSerial;
//     private long       _lastDrawnSerial = -1;
//     private bool       _forceRedraw = true;
//
//     // ------------------------------------------------------------------
//     // Last-good-frame copy of the back buffer (swapchain mode only)
//     // ------------------------------------------------------------------
//     private Texture2D? _lastGoodFrameTexture;
//     private PixelSize  _lastGoodFrameSize;
//     private Format     _lastGoodFrameFormat;
//
//     // ------------------------------------------------------------------
//     // Misc
//     // ------------------------------------------------------------------
//     private readonly Stopwatch _globalClock = Stopwatch.StartNew();
//     public long NowTicks => _globalClock.ElapsedTicks;
//
//     // ------------------------------------------------------------------
//     // Decode -> draw latency measurement (unchanged)
//     // ------------------------------------------------------------------
//     private const int StatsWindowSize = 120; 
//     private const bool EnableLatencyLogging = false;
//     
//     
//     private readonly object _latencyLock = new();
//     private readonly Queue<double> _decodeToDrawHistory = new();
//     private Texture2D? _lastLatencyTimedFrame;
//     private long _currentFrameDecodeTimestamp;
//     private long _swFrameDecodeTimestamp;
//     private long _swLatencyTimedSerial = -1;
//     private int _redrawStreak;
//
//     /// <summary>Path of the video to play. Set this before calling Play_video().</summary>
//     public string FileToPlay { get; set; } =
//         @"M:\movie\Kung.Fu.Panda.3.2016.720p.WEBRip.x264.AAC-ETRG.mp4";
//
//     private Src.FFmpeg? ffmpeg;
//     private Thread? threadPlay;
//     private volatile bool is_running = true;
//     private bool _disposed;
//
//     // ==================================================================
//     // Public API
//     // ==================================================================
//
//     public D3DDevice? my_Device => _device;
//
//     public string BackendName =>
//         _softwareMode
//             ? "Software renderer (no GPU)"
//             : _device is null
//                 ? "Direct3D 11 (Avalonia interop, uninitialized)"
//                 : _gpuShaderMode
//                     ? $"Direct3D 11 ({_device.FeatureLevel}) [Avalonia interop: shader mode]"
//                     : $"Direct3D 11 ({_device.FeatureLevel}) [Avalonia interop]";
//
//     public event EventHandler? Initialized;
//     public bool IsInitialized =>
//         _softwareMode || (_device is not null && (_swapchain is not null || _gpuShaderMode));
//
//     public static D11InteropRenderer? Instance { get; private set; }
//
//     // ---- shims (kept so existing callers still compile) ----
//     public void Initialize(IntPtr outputHandle, int d_width = 0, int d_height = 0) { }
//     public void PresentStaticImage() { }
//     public void PresentFrameKeepAlive() { }
//     public void HandleResize() { }
//     public void ResizeToClient(IntPtr hwnd) { }
//
//     // ------------------------------------------------------------------
//     // PresentFrame overloads
//     //
//     //   Swapchain mode : stores the texture; RenderFrame blits it.
//     //   GPU-shader mode: stores the texture and renders it RIGHT HERE on
//     //                    the caller's thread (decoder), then hands the
//     //                    pixels to Avalonia through SetSoftwareFrame.
//     //   Software mode  : no D3D device, so a Texture2D can't be drawn.
//     //                    Call SetSoftwareFrame directly instead.
//     // ------------------------------------------------------------------
//     public void PresentFrame(Texture2D textureHW, long decodeTimestamp,
//                              int d_width = 0, int d_height = 0)
//         => PresentFrameInternal(textureHW, decodeTimestamp, arraySlice: 0, ownsTexture: true);
//
//     public void PresentFrame(Texture2D textureHW, int d_width = 0, int d_height = 0)
//         => PresentFrameInternal(textureHW, _globalClock.ElapsedTicks, arraySlice: 0, ownsTexture: true);
//
//     private void PresentFrameInternal(Texture2D? texture, long decodeTimestamp,
//                                       int arraySlice, bool ownsTexture)
//     {
//         if (texture == null) return;
//
//         if (_softwareMode)
//         {
//             // No device — nothing we can do with a texture here.
//             if (ownsTexture) { try { texture.Dispose(); } catch { } }
//             return;
//         }
//
//         SetSourceTexture(texture, decodeTimestamp, arraySlice, ownsTexture);
//
//         if (_gpuShaderMode)
//         {
//             // All D3D work happens on THIS (non-UI) thread.
//             // SetSoftwareFrame (called from the readback) posts InvalidateVisual
//             // + the immediate Paint to the UI thread.
//             RenderShaderNow();
//             return;
//         }
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
//     /// GPU modes only. Replaces the frame RenderFrame draws.
//     /// ownsTexture = true : this renderer disposes the texture when replaced.
//     /// ownsTexture = false: the texture belongs to FFmpeg; never dispose it here.
//     /// </summary>
//     public void SetSourceTexture(Texture2D? texture, long decodeTimestamp = 0,
//                                  int arraySlice = 0, bool ownsTexture = true)
//     {
//         if (texture == null) return;
//         if (_softwareMode)
//         {
//             if (ownsTexture) { try { texture.Dispose(); } catch { } }
//             return;
//         }
//
//         lock (_d3dLock)
//         {
//             var old = _currentVideoFrame;
//             if (old != null && _currentVideoFrameOwned && !ReferenceEquals(old, texture))
//             {
//                 try { old.Dispose(); } catch { }
//             }
//
//             _currentVideoFrame      = texture;
//             _currentVideoFrameOwned = ownsTexture;
//             _currentVideoFrameSlice = arraySlice;
//             _currentFrameDecodeTimestamp = decodeTimestamp != 0
//                 ? decodeTimestamp
//                 : _globalClock.ElapsedTicks;
//             _currentFrameSerial++;
//             _forceRedraw = true;
//         }
//     }
//
//     public void ResizeSwapChain(int width, int height)
//     {
//         if (width <= 0 || height <= 0) return;
//         if (_softwareMode) return;
//
//         lock (_d3dLock)
//         {
//             if (_gpuShaderMode)
//             {
//                 // Shader mode renders at the source frame size; Avalonia scales
//                 // the bitmap on draw, so a window resize needs no D3D work.
//                 return;
//             }
//
//             _currentSwapchainSize  = new PixelSize(width, height);
//             _pendingSwapchainSize  = default;
//             _pendingSwapchainTicks = 0;
//             ClearOutputViewCache();
//             _forceRedraw = true;
//         }
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
//     // Software (CPU) rendering path
//     // ==================================================================
//
//     protected override (bool success, string info) InitializeSoftwareFallback(string reason)
//     {
//         Instance = this;
//
//         // Keep / create a D3D11 device so my_Device is never null.
//         // Try WARP — that's the same fallback DirectX uses.
//         if (_device is null)
//         {
//             try
//             {
//                 _device = new D3DDevice(
//                     SharpDX.Direct3D.DriverType.Warp,
//                     DeviceCreationFlags.BgraSupport,
//                     new[]
//                     {
//                         FeatureLevel.Level_11_1,
//                         FeatureLevel.Level_11_0,
//                         FeatureLevel.Level_10_0,
//                         FeatureLevel.Level_9_3,
//                         FeatureLevel.Level_9_2,
//                         FeatureLevel.Level_9_1
//                     });
//                 Console.WriteLine("[Interop] Created WARP device for software fallback");
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"[Interop] WARP device creation failed: {ex.Message}");
//             }
//         }
//
//         // Make sure the shared rendering resources exist so the shader path
//         // and my_AV_win both have what they need.
//         if (_device is not null)
//         {
//             try
//             {
//                 if (_vertexShader is null)   CreateShaders();
//                 if (_vertexBuffer is null)   CreateQuad();
//                 if (_sampler is null)        CreateSampler();
//
//                 _gpuShaderMode = true;   // we have a device, draw with the pixel shader
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"[Interop] Shader setup failed in fallback: {ex.Message}");
//                 _gpuShaderMode = false;
//             }
//         }
//
//         _softwareMode = _device is null;   // only true if even WARP failed
//
//         Console.WriteLine(
//             $"[Interop] Fallback mode: " +
//             $"{(_softwareMode ? "CPU only" : "D3D11 shader (WARP/hardware)")} ({reason})");
//
//         Initialized?.Invoke(this, EventArgs.Empty);
//
//         return (true,
//             _softwareMode
//                 ? $"Software renderer ({reason})"
//                 : $"D3D11 shader renderer ({reason})");
//     }
//
//     /// <summary>
//     /// Feed a BGRA frame (stride in bytes). Pixels are copied once, straight
//     /// into the WriteableBitmap, on the calling thread.
//     /// </summary>
//     public unsafe void SetSoftwareFrame(byte[] bgra, int width, int height, int stride, long decodeTimestamp = 0)
//     {
//         if (bgra == null || width <= 0 || height <= 0 || stride < width * 4) return;
//         if (bgra.Length < stride * height) return;
//
//         fixed (byte* p = bgra)
//             SetSoftwareFrame((IntPtr)p, width, height, stride, decodeTimestamp);
//     }
//
//     /// <summary>
//     /// Zero-extra-copy path (mapped texture, AVFrame data, ...).
//     /// The memory only needs to stay valid for the duration of the call.
//     /// </summary>
//     public unsafe void SetSoftwareFrame(IntPtr src, int width, int height, int stride, long decodeTimestamp = 0)
//     {
//         if (src == IntPtr.Zero || width <= 0 || height <= 0 || stride < width * 4) return;
//
//         lock (_swLock)
//         {
//             if (_swBitmap == null || _swW != width || _swH != height)
//             {
//                 // The renderer may still reference the old bitmap; dispose it later in Render().
//                 _swRetired?.Dispose();
//                 _swRetired = _swBitmap;
//
//                 _swBitmap = new Avalonia.Media.Imaging.WriteableBitmap(
//                     new PixelSize(width, height),
//                     new Vector(96, 96),
//                     Avalonia.Platform.PixelFormat.Bgra8888,
//                     Avalonia.Platform.AlphaFormat.Opaque);
//                 _swW = width;
//                 _swH = height;
//             }
//
//             using (var fb = _swBitmap.Lock())
//             {
//                 byte* s = (byte*)src;
//                 byte* d = (byte*)fb.Address;
//
//                 if (fb.RowBytes == stride)
//                 {
//                     long total = (long)stride * height;
//                     System.Buffer.MemoryCopy(s, d, total, total);
//                 }
//                 else
//                 {
//                     int copyBytes = Math.Min(width * 4, fb.RowBytes);
//                     for (int y = 0; y < height; y++)
//                     {
//                         System.Buffer.MemoryCopy(
//                             s + (long)y * stride,
//                             d + (long)y * fb.RowBytes,
//                             fb.RowBytes, copyBytes);
//                     }
//                 }
//             }
//
//             _swFrameDecodeTimestamp = decodeTimestamp != 0
//                 ? decodeTimestamp
//                 : _globalClock.ElapsedTicks;
//             _swSerial++;
//         }
//
//         // Coalesce: at most one pending UI post. It invalidates and tells the
//         // compositor to render now instead of waiting for its next tick.
//         if (Interlocked.Exchange(ref _swInvalidatePending, 1) == 0)
//         {
//             Avalonia.Threading.Dispatcher.UIThread.Post(() =>
//             {
//                 Interlocked.Exchange(ref _swInvalidatePending, 0);
//                 InvalidateVisual();
//
//                 if (this.GetVisualRoot() is Avalonia.Rendering.IRenderRoot root)
//                 {
//                     // This is the snippet you quoted. It's a UI-thread-only call.
//                     root.Renderer.Paint(new Rect(root.ClientSize));
//                 }
//             }, Avalonia.Threading.DispatcherPriority.Render);
//         }
//     }
//
//     public override void Render(DrawingContext ctx)
//     {
//         // Only the CPU-bitmap paths paint here.
//         if (!_softwareMode && !_gpuShaderMode)
//         {
//             base.Render(ctx);
//             return;
//         }
//
//         var bounds = new Rect(Bounds.Size);
//         ctx.FillRectangle(Brushes.Black, bounds);
//
//         Avalonia.Media.Imaging.WriteableBitmap? bmp, retired;
//         int w, h;
//         long serial, ts;
//
//         // Grab references only; no pixel copy happens on the UI thread anymore.
//         lock (_swLock)
//         {
//             bmp = _swBitmap; w = _swW; h = _swH;
//             serial = _swSerial; ts = _swFrameDecodeTimestamp;
//             retired = _swRetired; _swRetired = null;
//         }
//
//         retired?.Dispose();
//
//         if (bmp != null && w > 0 && h > 0)
//         {
//             DrawLetterboxed(ctx, bmp, w, h, bounds);
//
//             if (serial != _swLatencyTimedSerial && ts != 0)
//             {
//                 double latencyMs = (_globalClock.ElapsedTicks - ts)
//                     * 1000.0 / Stopwatch.Frequency;
//                 RecordDecodeToDrawLatency(latencyMs);
//                 _swLatencyTimedSerial = serial;
//             }
//
//             return;
//         }
//
//         if (_swStaticImage != null)
//         {
//             DrawLetterboxed(ctx, _swStaticImage,
//                 (int)_swStaticImage.Size.Width, (int)_swStaticImage.Size.Height, bounds);
//         }
//     }
//
//     private void RecordDecodeToDrawLatency(double latencyMs)
//     {
//         if (latencyMs < 0 || double.IsNaN(latencyMs) || double.IsInfinity(latencyMs))
//             return;
//
//         lock (_latencyLock)
//         {
//             _decodeToDrawHistory.Enqueue(latencyMs);
//             while (_decodeToDrawHistory.Count > StatsWindowSize)
//                 _decodeToDrawHistory.Dequeue();
//         }
//
//         if (EnableLatencyLogging)
//             PrintDecodeToDrawStats();
//     }
//
//     private void PrintDecodeToDrawStats()
//     {
//         double[] samples;
//         int redrawStreak;
//
//         lock (_latencyLock)
//         {
//             samples = _decodeToDrawHistory.ToArray();
//             redrawStreak = _redrawStreak;
//         }
//
//         if (samples.Length == 0) return;
//
//         double sum = 0;
//         double min = double.MaxValue;
//         double max = double.MinValue;
//
//         foreach (double value in samples)
//         {
//             sum += value;
//             if (value < min) min = value;
//             if (value > max) max = value;
//         }
//
//         double avg = sum / samples.Length;
//         double variance = 0;
//         foreach (double value in samples)
//         {
//             double delta = value - avg;
//             variance += delta * delta;
//         }
//
//         double stddev = Math.Sqrt(variance / samples.Length);
//
//         string message =
//             $"[DecodeToDrawLatency][Interop] avg={avg:F3}ms min={min:F3}ms " +
//             $"max={max:F3}ms stddev={stddev:F3}ms window={samples.Length} " +
//             $"redrawStreak={redrawStreak}";
//
//         Debug.WriteLine(message);
//         Console.WriteLine(message);
//     }
//
//     private static void DrawLetterboxed(DrawingContext ctx, Avalonia.Media.IImage image,
//                                         int srcW, int srcH, Rect bounds)
//     {
//         if (srcW <= 0 || srcH <= 0 || bounds.Width <= 0 || bounds.Height <= 0) return;
//
//         double scale = Math.Min(bounds.Width / srcW, bounds.Height / srcH);
//         double w = srcW * scale, h = srcH * scale;
//         var dest = new Rect((bounds.Width - w) / 2, (bounds.Height - h) / 2, w, h);
//         ctx.DrawImage(image, new Rect(0, 0, srcW, srcH), dest);
//     }
//
//     // ==================================================================
//     // Static image
//     // ==================================================================
//
//     public void DisplayImage(string fileName)
//     {
//         string imagePath = Path.IsPathRooted(fileName)
//             ? fileName
//             : Path.Combine(Directory.GetCurrentDirectory(), fileName);
//
//         if (_softwareMode)
//         {
//             try
//             {
//                 if (!File.Exists(imagePath))
//                 {
//                     Console.WriteLine($"Image file not found: {imagePath}");
//                     return;
//                 }
//
//                 var bmp = new Avalonia.Media.Imaging.Bitmap(imagePath);
//                 lock (_swLock)
//                 {
//                     _swStaticImage?.Dispose();
//                     _swStaticImage = bmp;
//                 }
//                 Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual);
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"Error displaying image {fileName}: {ex.Message}");
//             }
//             return;
//         }
//
//         if (_device == null)
//         {
//             Console.WriteLine("[Interop] DisplayImage before init.");
//             return;
//         }
//
//         try
//         {
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
//
//                 if (_staticImageTexture != null)
//                 {
//                     _staticImageView = new ShaderResourceView(_device, _staticImageTexture);
//                     Console.WriteLine($"Successfully loaded image: {fileName}");
//                 }
//
//                 _forceRedraw = true;
//             }
//
//             // Shader mode has no per-tick RenderFrame anymore: draw it now.
//             if (_gpuShaderMode)
//                 RenderShaderNow();
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
//             using var factory = new ImagingFactory();
//             using var bitmapDecoder = new BitmapDecoder(factory, filePath, DecodeOptions.CacheOnLoad);
//             using var frame = bitmapDecoder.GetFrame(0);
//
//             using var flipRotator = new BitmapFlipRotator(factory);
//             flipRotator.Initialize(frame, BitmapTransformOptions.FlipVertical);
//
//             using var formatConverter = new FormatConverter(factory);
//             formatConverter.Initialize(flipRotator, SharpDX.WIC.PixelFormat.Format32bppRGBA);
//
//             var width  = formatConverter.Size.Width;
//             var height = formatConverter.Size.Height;
//
//             var stride = width * 4;
//             using var dataStream = new DataStream(height * stride, true, true);
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
//     // ==================================================================
//     // Init
//     // ==================================================================
//
//     protected override (bool success, string info) InitializeGraphicsResources(
//         Compositor compositor,
//         CompositionDrawingSurface surface,
//         ICompositionGpuInterop interop)
//     {
//         try
//         {
//             Instance = this;
//
//             bool interopOk =
//                 interop.SupportedImageHandleTypes.Contains(
//                     KnownPlatformGraphicsExternalImageHandleTypes
//                         .D3D11TextureGlobalSharedHandle) == true;
//
//             using var factory = new DxgiFactory();
//
//             // Pick the first REAL hardware adapter. Skip WARP / Basic Render Driver.
//             Adapter1? adapter = null;
//             int count = factory.GetAdapterCount1();
//             for (int i = 0; i < count; i++)
//             {
//                 var candidate = factory.GetAdapter1(i);
//                 var d = candidate.Description1;
//                 bool isSoftware = (d.Flags & AdapterFlags.Software) != 0 || d.VendorId == 0x1414;
//                 if (!isSoftware)
//                 {
//                     adapter = candidate;
//                     break;
//                 }
//                 candidate.Dispose();
//             }
//
//             if (adapter == null)
//                 return (false, "No hardware GPU found");
//
//             using (adapter)
//             {
//                 _device = new D3DDevice(
//                     adapter,
//                     DeviceCreationFlags.BgraSupport,
//                     new[]
//                     {
//                         FeatureLevel.Level_12_1,
//                         FeatureLevel.Level_12_0,
//                         FeatureLevel.Level_11_1,
//                         FeatureLevel.Level_11_0,
//                         FeatureLevel.Level_10_0,
//                         FeatureLevel.Level_9_3,
//                         FeatureLevel.Level_9_2,
//                         FeatureLevel.Level_9_1
//                     });
//
//                 // ---- Swapchain only if interop is usable. ----
//                 if (interopOk)
//                 {
//                     try
//                     {
//                         _swapchain = new D3D11Swapchain(_device, interop, surface);
//                     }
//                     catch (Exception ex)
//                     {
//                         Console.WriteLine(
//                             $"[Interop] Swapchain creation failed: {ex.Message} — falling back to shader mode");
//                         _swapchain = null;
//                         interopOk = false;
//                     }
//                 }
//
//                 if (!interopOk)
//                 {
//                     _gpuShaderMode = true;
//                     Console.WriteLine(
//                         "[Interop] Avalonia GPU interop unavailable — using D3D11 shader mode");
//                 }
//
//                 // Video processor (only useful in swapchain mode).
//                 if (interopOk)
//                 {
//                     try
//                     {
//                         _videoDevice1  = _device.QueryInterface<VideoDevice1>();
//                         _videoContext1 = _device.ImmediateContext.QueryInterface<VideoContext1>();
//                     }
//                     catch (Exception ex)
//                     {
//                         Console.WriteLine($"[Interop] Video processor not available: {ex.Message}");
//                         _videoProcessorReady = false;
//                     }
//                 }
//
//                 CreateShaders();
//                 CreateQuad();
//                 CreateSampler();
//
//                 _currentSwapchainSize  = default;
//                 _pendingSwapchainSize  = default;
//                 _pendingSwapchainTicks = 0;
//                 _lastDrawnSerial       = -1;
//                 _forceRedraw           = true;
//
//                 Initialized?.Invoke(this, EventArgs.Empty);
//
//                 Console.WriteLine("initialized gpuinterop class");
//
//                 string adapterName = adapter.Description1.Description;
//                 return (true,
//                     _gpuShaderMode
//                         ? $"D3D11 ({_device.FeatureLevel}) {adapterName} [Avalonia interop: shader mode]"
//                         : $"D3D11 ({_device.FeatureLevel}) {adapterName} [Avalonia interop]");
//             }
//         }
//         catch (Exception ex)
//         {
//             // Base class will clean up and switch to software mode.
//             return (false, $"GPU init failed: {ex.Message}");
//         }
//     }
//
//     protected override void FreeGraphicsResources() => DisposeAll();
//
//     // ==================================================================
//     // Video processor setup
//     // ==================================================================
//
//     private void ClearOutputViewCache()
//     {
//         foreach (var v in _vpovCache.Values)
//         {
//             try { v.Dispose(); } catch { }
//         }
//         _vpovCache.Clear();
//     }
//
//     private static int AlignUp(int value, int alignment)
//         => ((value + alignment - 1) / alignment) * alignment;
//
//     private bool EnsureVideoProcessorFor(int inputWidth, int inputHeight, int outputWidth, int outputHeight)
//     {
//         if (_videoDevice1 == null || _videoContext1 == null) return false;
//         if (inputWidth <= 0 || inputHeight <= 0) return false;
//         if (outputWidth <= 0 || outputHeight <= 0) return false;
//
//         int neededOutW = Math.Max(outputWidth, inputWidth);
//         int neededOutH = Math.Max(outputHeight, inputHeight);
//
//         bool tooBig = _videoProcessorReady &&
//             ((_vpcd.OutputWidth  > neededOutW * 2 && _vpcd.OutputWidth  > VideoProcessorShrinkThreshold) ||
//              (_vpcd.OutputHeight > neededOutH * 2 && _vpcd.OutputHeight > VideoProcessorShrinkThreshold));
//
//         if (_videoProcessorReady &&
//             !tooBig &&
//             _vpcd.InputWidth  == inputWidth &&
//             _vpcd.InputHeight == inputHeight &&
//             neededOutW <= _vpcd.OutputWidth &&
//             neededOutH <= _vpcd.OutputHeight)
//         {
//             return true;
//         }
//
//         ClearOutputViewCache();
//         Utilities.Dispose(ref _videoProcessor);
//         Utilities.Dispose(ref _vpe);
//
//         int paddedOutW = AlignUp(neededOutW, VideoProcessorOutputAlignment);
//         int paddedOutH = AlignUp(neededOutH, VideoProcessorOutputAlignment);
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
//             try { _videoContext1.VideoProcessorSetStreamAutoProcessingMode(_videoProcessor, 0, false); }
//             catch { /* not implemented on some drivers; not fatal */ }
//
//             _vpivd = new VideoProcessorInputViewDescription
//             {
//                 FourCC    = 0,
//                 Dimension = VpivDimension.Texture2D,
//                 Texture2D = new Texture2DVpiv { MipSlice = 0, ArraySlice = 0 }
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
//     // Render dispatch
//     // ==================================================================
//
//     private bool TryCommitSwapchainSize(PixelSize pixelSize)
//     {
//         if (_currentSwapchainSize == default)
//         {
//             _currentSwapchainSize  = pixelSize;
//             _pendingSwapchainSize  = default;
//             _pendingSwapchainTicks = 0;
//             return true;
//         }
//
//         if (pixelSize == _currentSwapchainSize)
//         {
//             _pendingSwapchainSize  = default;
//             _pendingSwapchainTicks = 0;
//             return false;
//         }
//
//         if (pixelSize != _pendingSwapchainSize)
//         {
//             _pendingSwapchainSize  = pixelSize;
//             _pendingSwapchainTicks = 1;
//             return false;
//         }
//
//         if (++_pendingSwapchainTicks < ResizeStableTicks)
//             return false;
//
//         _currentSwapchainSize  = pixelSize;
//         _pendingSwapchainSize  = default;
//         _pendingSwapchainTicks = 0;
//         return true;
//     }
//
//     protected override void RenderFrame(PixelSize pixelSize)
//     {
//         if (_softwareMode) return;
//
//         // Shader mode is driven by PresentFrame on the decoder thread.
//         // Nothing to do on the UI/composition tick.
//         if (_gpuShaderMode) return;
//
//         if (pixelSize == default) return;
//         if (pixelSize.Width <= 1 || pixelSize.Height <= 1) return;
//         if (_device is null) return;
//
//         RenderSwapchainMode(pixelSize);
//     }
//
//     // ==================================================================
//     // GPU shader mode (runs on the caller's thread, NOT the UI thread)
//     //
//     //   - target texture = source frame size (no GPU scaling)
//     //   - draw the frame with the pixel shader
//     //   - copy to a reused staging texture, map it
//     //   - copy once into the WriteableBitmap via SetSoftwareFrame
//     //
//     // The UI thread then only draws the bitmap.
//     // ==================================================================
//
//     private void RenderShaderNow()
//     {
//         lock (_d3dLock)
//         {
//             if (_device is null || !_gpuShaderMode) return;
//
//             // Pick the source: live frame first, then the static image.
//             ShaderResourceView? srv = null;
//             bool ownsSrv = false;
//             int srcW = 0, srcH = 0;
//
//             try
//             {
//                 var frame = _currentVideoFrame;
//                 if (frame is not null && frame.NativePointer != IntPtr.Zero)
//                 {
//                     try
//                     {
//                         var fd = frame.Description;
//                         srv  = new ShaderResourceView(_device, frame);
//                         ownsSrv = true;
//                         srcW = fd.Width;
//                         srcH = fd.Height;
//                     }
//                     catch (Exception ex)
//                     {
//                         Console.WriteLine($"[Interop/shader] Live frame SRV failed: {ex.Message}");
//                         srv = null;
//                     }
//                 }
//
//                 if (srv is null && _staticImageView is not null && _staticImageTexture is not null)
//                 {
//                     var sd = _staticImageTexture.Description;
//                     srv  = _staticImageView;
//                     srcW = sd.Width;
//                     srcH = sd.Height;
//                 }
//
//                 if (srv is null || srcW <= 0 || srcH <= 0) return;
//
//                 var targetSize = new PixelSize(srcW, srcH);
//
//                 // (Re)create the offscreen target only when the source size changes.
//                 if (_shaderTarget is null || _shaderTargetRtv is null || _shaderTargetSize != targetSize)
//                 {
//                     Utilities.Dispose(ref _shaderTargetRtv);
//                     Utilities.Dispose(ref _shaderTargetSrv);
//                     Utilities.Dispose(ref _shaderTarget);
//
//                     try
//                     {
//                         _shaderTarget = new Texture2D(_device, new Texture2DDescription
//                         {
//                             Width             = srcW,
//                             Height            = srcH,
//                             ArraySize         = 1,
//                             MipLevels         = 1,
//                             Format            = Format.B8G8R8A8_UNorm,
//                             SampleDescription = new SampleDescription(1, 0),
//                             Usage             = ResourceUsage.Default,
//                             BindFlags         = BindFlags.RenderTarget | BindFlags.ShaderResource,
//                             CpuAccessFlags    = CpuAccessFlags.None,
//                             OptionFlags       = ResourceOptionFlags.None
//                         });
//                         _shaderTargetSrv  = new ShaderResourceView(_device, _shaderTarget);
//                         _shaderTargetRtv  = new RenderTargetView(_device, _shaderTarget);
//                         _shaderTargetSize = targetSize;
//                     }
//                     catch (Exception ex)
//                     {
//                         Console.WriteLine($"[Interop/shader] Failed to create target: {ex.Message}");
//                         Utilities.Dispose(ref _shaderTargetRtv);
//                         Utilities.Dispose(ref _shaderTargetSrv);
//                         Utilities.Dispose(ref _shaderTarget);
//                         return;
//                     }
//                 }
//
//                 var context = _device.ImmediateContext;
//
//                 try
//                 {
//                     context.OutputMerger.SetTargets(_shaderTargetRtv);
//                     context.ClearRenderTargetView(_shaderTargetRtv, new RawColor4(0, 0, 0, 1));
//
//                     // Target == source size, so the letterbox rect is the full target.
//                     DrawFullscreenQuad(context, srv, srcW, srcH, targetSize);
//
//                     context.PixelShader.SetShaderResource(0, null);
//                     context.OutputMerger.ResetTargets();
//
//                     _lastDrawnSerial = _currentFrameSerial;
//                     _forceRedraw     = false;
//
//                     // CopyResource + Map synchronise with the GPU; no extra Flush needed.
//                     ReadbackShaderTargetToSoftware(_currentFrameDecodeTimestamp);
//                 }
//                 catch (Exception ex)
//                 {
//                     Console.WriteLine($"[Interop/shader] Render failed: {ex.Message}");
//                 }
//             }
//             finally
//             {
//                 if (ownsSrv) srv?.Dispose();
//             }
//         }
//     }
//
//     /// <summary>
//     /// Copies _shaderTarget to a reused staging texture, maps it, and feeds
//     /// the mapped memory straight to SetSoftwareFrame (no managed array,
//     /// correct RowPitch).
//     /// </summary>
//     private void ReadbackShaderTargetToSoftware(long decodeTimestamp)
//     {
//         if (_device is null || _shaderTarget is null) return;
//
//         var desc = _shaderTarget.Description;
//         if (desc.Width <= 0 || desc.Height <= 0) return;
//
//         try
//         {
//             var size = new PixelSize(desc.Width, desc.Height);
//             if (_readbackStaging is null || _readbackSize != size)
//             {
//                 Utilities.Dispose(ref _readbackStaging);
//                 _readbackStaging = new Texture2D(_device, new Texture2DDescription
//                 {
//                     Width             = desc.Width,
//                     Height            = desc.Height,
//                     ArraySize         = 1,
//                     MipLevels         = 1,
//                     Format            = Format.B8G8R8A8_UNorm,
//                     SampleDescription = new SampleDescription(1, 0),
//                     Usage             = ResourceUsage.Staging,
//                     BindFlags         = BindFlags.None,
//                     CpuAccessFlags    = CpuAccessFlags.Read,
//                     OptionFlags       = ResourceOptionFlags.None
//                 });
//                 _readbackSize = size;
//             }
//
//             var ctx = _device.ImmediateContext;
//             ctx.CopyResource(_shaderTarget, _readbackStaging);
//
//             var box = ctx.MapSubresource(_readbackStaging, 0, MapMode.Read,
//                                          SharpDX.Direct3D11.MapFlags.None);
//             try
//             {
//                 SetSoftwareFrame(box.DataPointer, desc.Width, desc.Height, box.RowPitch, decodeTimestamp);
//             }
//             finally
//             {
//                 ctx.UnmapSubresource(_readbackStaging, 0);
//             }
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"[Interop/shader] Readback failed: {ex.Message}");
//         }
//     }
//
//     // ==================================================================
//     // Swapchain mode (unchanged behavior)
//     // ==================================================================
//
//     private void RenderSwapchainMode(PixelSize pixelSize)
//     {
//         if (_swapchain is null || _device is null) return;
//
//         bool sizeChanged = TryCommitSwapchainSize(pixelSize);
//
//         // Size still settling: keep showing the last presented image.
//         if (pixelSize != _currentSwapchainSize) return;
//
//         lock (_d3dLock)
//         {
//             if (sizeChanged)
//                 ClearOutputViewCache();
//
//             bool newFrame = _currentFrameSerial != _lastDrawnSerial;
//             if (!newFrame && !sizeChanged && !_forceRedraw)
//             {
//                 _redrawStreak++;
//                 return;
//             }
//
//             var context = _device.ImmediateContext;
//
//             using (_swapchain.BeginDraw(_currentSwapchainSize, out var renderView))
//             {
//                 Texture2D? renderTexture = null;
//                 Resource?  rtResource    = null;
//                 try
//                 {
//                     rtResource    = renderView.Resource;
//                     renderTexture = rtResource.QueryInterface<Texture2D>();
//                 }
//                 catch (Exception ex)
//                 {
//                     Console.WriteLine($"[Interop] RT query failed: {ex.Message}");
//                 }
//                 finally
//                 {
//                     rtResource?.Dispose();
//                 }
//
//                 try
//                 {
//                     context.OutputMerger.SetTargets(renderView);
//                     context.ClearRenderTargetView(renderView, new RawColor4(0f, 0f, 0f, 1f));
//
//                     bool didBlit = false;
//                     Texture2D? frame = _currentVideoFrame;
//
//                     if (renderTexture is not null && frame is not null && frame.NativePointer != IntPtr.Zero)
//                     {
//                         Texture2DDescription frameDesc = default;
//                         bool frameValid = true;
//
//                         try
//                         {
//                             frameDesc = frame.Description;
//                         }
//                         catch (Exception ex)
//                         {
//                             Console.WriteLine($"[Interop] Dropping invalid video frame: {ex.Message}");
//                             _currentVideoFrame = null;
//                             frameValid = false;
//                         }
//
//                         if (frameValid &&
//                             EnsureVideoProcessorFor(
//                                 frameDesc.Width, frameDesc.Height,
//                                 _currentSwapchainSize.Width, _currentSwapchainSize.Height))
//                         {
//                             didBlit = BlitVideoFrame(frame, _currentVideoFrameSlice,
//                                 renderTexture, _currentSwapchainSize);
//
//                             if (!didBlit)
//                             {
//                                 ClearOutputViewCache();
//                                 didBlit = BlitVideoFrame(frame, _currentVideoFrameSlice,
//                                     renderTexture, _currentSwapchainSize);
//                             }
//
//                             if (didBlit)
//                             {
//                                 CaptureLastGoodFrame(context, renderTexture);
//
//                                 if (frame != _lastLatencyTimedFrame && _currentFrameDecodeTimestamp != 0)
//                                 {
//                                     double latencyMs = (_globalClock.ElapsedTicks - _currentFrameDecodeTimestamp)
//                                         * 1000.0 / Stopwatch.Frequency;
//                                     RecordDecodeToDrawLatency(latencyMs);
//                                     _lastLatencyTimedFrame = frame;
//                                     _redrawStreak = 0;
//                                 }
//                             }
//                         }
//                     }
//
//                     if (!didBlit)
//                     {
//                         bool drawn = false;
//
//                         if (_currentVideoFrame is not null && renderTexture is not null)
//                             drawn = TryRestoreLastGoodFrame(context, renderTexture, renderView);
//
//                         if (!drawn && _staticImageView is not null && _staticImageTexture is not null)
//                         {
//                             DrawFullscreenQuad(context, _staticImageView,
//                                 _staticImageTexture.Description.Width,
//                                 _staticImageTexture.Description.Height,
//                                 _currentSwapchainSize);
//                         }
//                     }
//
//                     context.PixelShader.SetShaderResource(0, null);
//                     context.OutputMerger.ResetTargets();
//
//                     _lastDrawnSerial = _currentFrameSerial;
//                     _forceRedraw     = false;
//                 }
//                 finally
//                 {
//                     renderTexture?.Dispose();
//                 }
//             }
//
//             if (sizeChanged)
//             {
//                 try
//                 {
//                     context.ClearState();
//                     context.Flush();
//                 }
//                 catch (Exception ex)
//                 {
//                     Console.WriteLine($"[Interop] Post-resize flush failed: {ex.Message}");
//                 }
//             }
//         }
//     }
//
//     // ------------------------------------------------------------------
//     // Letterbox
//     // ------------------------------------------------------------------
//     private static void ComputeLetterboxRect(int srcW, int srcH, PixelSize bb,
//         out int outX, out int outY, out int outW, out int outH)
//     {
//         int bbW = bb.Width;
//         int bbH = bb.Height;
//
//         if (srcW <= 0 || srcH <= 0 || bbW <= 0 || bbH <= 0)
//         {
//             outX = outY = 0;
//             outW = bbW;
//             outH = bbH;
//             return;
//         }
//
//         float srcAspect = (float)srcW / srcH;
//
//         if ((float)bbW / bbH > srcAspect)
//         {
//             outH = bbH;
//             outW = (int)Math.Round(bbH * srcAspect);
//         }
//         else
//         {
//             outW = bbW;
//             outH = (int)Math.Round(bbW / srcAspect);
//         }
//
//         outW = Math.Clamp(outW, 1, bbW);
//         outH = Math.Clamp(outH, 1, bbH);
//         outX = (bbW - outW) / 2;
//         outY = (bbH - outH) / 2;
//     }
//
//     // ------------------------------------------------------------------
//     // Fullscreen quad
//     // ------------------------------------------------------------------
//     private void DrawFullscreenQuad(DeviceContext context, ShaderResourceView view,
//                                     int srcW, int srcH, PixelSize target)
//     {
//         ComputeLetterboxRect(srcW, srcH, target,
//             out int outX, out int outY, out int outW, out int outH);
//
//         context.Rasterizer.SetViewport(outX, outY, outW, outH, 0f, 1f);
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
//     // ------------------------------------------------------------------
//     // Video blit (swapchain mode)
//     // ------------------------------------------------------------------
//     private bool BlitVideoFrame(Texture2D inputTexture, int arraySlice,
//                                 Texture2D outputTexture, PixelSize destSize)
//     {
//         if (_videoDevice1 is null || _videoContext1 is null ||
//             _videoProcessor is null || _vpe is null)
//             return false;
//
//         VideoProcessorInputView? vpiv = null;
//         try
//         {
//             var inDesc = inputTexture.Description;
//
//             ComputeLetterboxRect(inDesc.Width, inDesc.Height, destSize,
//                 out int outX, out int outY, out int outW, out int outH);
//
//             var vpivd = _vpivd;
//             vpivd.Texture2D = new Texture2DVpiv
//             {
//                 MipSlice   = 0,
//                 ArraySlice = inDesc.ArraySize > 1 ? arraySlice : 0
//             };
//
//             _videoDevice1.CreateVideoProcessorInputView(inputTexture, _vpe, vpivd, out vpiv);
//
//             IntPtr outId = outputTexture.NativePointer;
//             if (!_vpovCache.TryGetValue(outId, out var vpov))
//             {
//                 if (_vpovCache.Count >= MaxOutputViewCacheSize)
//                     ClearOutputViewCache();
//
//                 _videoDevice1.CreateVideoProcessorOutputView(outputTexture, _vpe, _vpovd, out vpov);
//                 _vpovCache[outId] = vpov;
//             }
//
//             _videoContext1.VideoProcessorSetStreamMirror(
//                 _videoProcessor, 0, true, false, true); // flip vertical
//
//             _videoContext1.VideoProcessorSetStreamSourceRect(
//                 _videoProcessor, 0, true,
//                 new RawRectangle(0, 0, inDesc.Width, inDesc.Height));
//
//             _videoContext1.VideoProcessorSetStreamDestRect(
//                 _videoProcessor, 0, true,
//                 new RawRectangle(outX, outY, outX + outW, outY + outH));
//
//             _videoContext1.VideoProcessorSetOutputTargetRect(
//                 _videoProcessor, true,
//                 new RawRectangle(0, 0, destSize.Width, destSize.Height));
//
//             var streams = new[]
//             {
//                 new VideoProcessorStream { PInputSurface = vpiv, Enable = new RawBool(true) }
//             };
//
//             _videoContext1.VideoProcessorBlt(_videoProcessor, vpov, 0, 1, streams);
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
//     // ------------------------------------------------------------------
//     // Last good frame (swapchain mode)
//     // ------------------------------------------------------------------
//     private void CaptureLastGoodFrame(DeviceContext context, Texture2D backBuffer)
//     {
//         if (_device is null) return;
//
//         Texture2DDescription src;
//         try { src = backBuffer.Description; }
//         catch { return; }
//
//         var size = new PixelSize(src.Width, src.Height);
//
//         if (_lastGoodFrameTexture is null ||
//             _lastGoodFrameSize   != size ||
//             _lastGoodFrameFormat != src.Format)
//         {
//             Utilities.Dispose(ref _lastGoodFrameTexture);
//
//             try
//             {
//                 _lastGoodFrameTexture = new Texture2D(_device, new Texture2DDescription
//                 {
//                     Width             = src.Width,
//                     Height            = src.Height,
//                     ArraySize         = 1,
//                     MipLevels         = 1,
//                     Format            = src.Format,
//                     Usage             = ResourceUsage.Default,
//                     BindFlags         = BindFlags.None,
//                     CpuAccessFlags    = CpuAccessFlags.None,
//                     OptionFlags       = ResourceOptionFlags.None,
//                     SampleDescription = new SampleDescription(1, 0)
//                 });
//                 _lastGoodFrameSize   = size;
//                 _lastGoodFrameFormat = src.Format;
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"[Interop] Failed to allocate last-good-frame: {ex.Message}");
//                 Utilities.Dispose(ref _lastGoodFrameTexture);
//                 return;
//             }
//         }
//
//         try { context.CopyResource(backBuffer, _lastGoodFrameTexture); }
//         catch (Exception ex) { Console.WriteLine($"[Interop] Capture last-good-frame failed: {ex.Message}"); }
//     }
//
//     private bool TryRestoreLastGoodFrame(DeviceContext context, Texture2D backBuffer, RenderTargetView renderView)
//     {
//         if (_lastGoodFrameTexture is null) return false;
//
//         try
//         {
//             var dst = backBuffer.Description;
//             if (dst.Width  != _lastGoodFrameSize.Width  ||
//                 dst.Height != _lastGoodFrameSize.Height ||
//                 dst.Format != _lastGoodFrameFormat)
//                 return false;
//
//             context.OutputMerger.ResetTargets();
//             context.CopyResource(_lastGoodFrameTexture, backBuffer);
//             context.OutputMerger.SetTargets(renderView);
//             return true;
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"[Interop] Restore last-good-frame failed: {ex.Message}");
//             try { context.OutputMerger.SetTargets(renderView); } catch { }
//             return false;
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
//     private void CreateQuad()
//     {
//         var vertices = new[]
//         {
//             -1.0f,  1.0f, 0.0f,   0.0f, 0.0f,
//              1.0f,  1.0f, 0.0f,   1.0f, 0.0f,
//              1.0f, -1.0f, 0.0f,   1.0f, 1.0f,
//
//             -1.0f,  1.0f, 0.0f,   0.0f, 0.0f,
//              1.0f, -1.0f, 0.0f,   1.0f, 1.0f,
//             -1.0f, -1.0f, 0.0f,   0.0f, 1.0f
//         };
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
//     // Cleanup
//     // ==================================================================
//
//     public void DisposeAll()
//     {
//         if (_disposed) return;
//         _disposed = true;
//
//         is_running = false;
//
//         ReleaseGpuAsync();
//         ReleaseSoftwareResources();
//     }
//
//     /// <summary>
//     /// Disposes the swapchain first (async), then every other D3D object.
//     /// Safe to call when nothing was created.
//     /// </summary>
//     private void ReleaseGpuAsync()
//     {
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
//     private void ReleaseSoftwareResources()
//     {
//         lock (_swLock)
//         {
//             _swBitmap?.Dispose();      _swBitmap = null;
//             _swRetired?.Dispose();     _swRetired = null;
//             _swStaticImage?.Dispose(); _swStaticImage = null;
//         }
//     }
//
//     private void ReleaseD3DResources()
//     {
//         lock (_d3dLock)
//         {
//             if (_currentVideoFrameOwned)
//                 Utilities.Dispose(ref _currentVideoFrame);
//             else
//                 _currentVideoFrame = null;
//
//             ClearOutputViewCache();
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
//
//             Utilities.Dispose(ref _shaderTargetRtv);
//             Utilities.Dispose(ref _shaderTargetSrv);
//             Utilities.Dispose(ref _shaderTarget);
//             Utilities.Dispose(ref _readbackStaging);
//
//             Utilities.Dispose(ref _lastGoodFrameTexture);
//             Utilities.Dispose(ref _device);
//         }
//     }
// }















using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Androidplayer.Store;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
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

// NOTE: requires <AllowUnsafeBlocks>true</AllowUnsafeBlocks> in the .csproj
public class D11InteropRenderer : DrawingSurfaceDemoBase
{
    // ==================================================================
    // Modes
    //
    //   Swapchain  : full Avalonia GPU interop, VideoProcessor blits into
    //                the composition surface's swapchain image.
    //   GpuShader  : D3D11 device works, but Avalonia interop doesn't.
    //                The frame is brought to the CPU ON THE CALLING
    //                (DECODER) THREAD and copied once into a WriteableBitmap.
    //                The UI thread only ever draws that bitmap.
    //                  - BGRA frames  : direct CopySubresourceRegion -> staging
    //                                   (no shader pass, bit-identical)
    //                  - other formats: pixel shader into an offscreen target,
    //                                   then readback
    //   Software   : no physical GPU. Uses WARP (CPU D3D11) when available;
    //                if WARP cannot be created, falls back to pure Avalonia
    //                CPU rendering via SetSoftwareFrame.
    // ==================================================================

    private volatile bool _gpuShaderMode;
    private volatile bool _softwareMode;
    public  bool IsSoftwareMode  => _softwareMode;
    public  bool IsGpuShaderMode => _gpuShaderMode;

    // Only swapchain mode needs RenderFrame to fire every composition tick.
    protected override bool RunContinuously => !_softwareMode && !_gpuShaderMode;

    // ------------------------------------------------------------------
    // Core GPU objects
    // ------------------------------------------------------------------
    private D3DDevice?      _device;
    private D3D11Swapchain? _swapchain;

    public readonly object _d3dLock = new();

    // ------------------------------------------------------------------
    // Software (CPU) rendering path
    //
    // A ring of 3 WriteableBitmaps. The producer thread copies pixels into
    // a slot NOBODY is displaying, outside the lock, then takes the lock
    // only to publish the slot index. Render() only draws.
    // ------------------------------------------------------------------
    private readonly object _swLock = new();
    private int _swW, _swH;
    private long _swSerial;

    private readonly Avalonia.Media.Imaging.WriteableBitmap?[] _swRing = new Avalonia.Media.Imaging.WriteableBitmap?[3];
    private int _swFront = -1;
    private readonly List<Avalonia.Media.Imaging.WriteableBitmap> _swRetiredList = new();

    private Avalonia.Media.Imaging.Bitmap? _swStaticImage;
    private int _swInvalidatePending;

    // CPU/software render cache.
    private long _swRenderedSerial = -1;
    private Rect _swCachedBounds;
    private int _swCachedW;
    private int _swCachedH;
    private Rect _swCachedDest;
    private bool _swCachedDestValid;

    // ------------------------------------------------------------------
    // GPU-shader-mode offscreen target (sized to the SOURCE frame)
    // ------------------------------------------------------------------
    private Texture2D?          _shaderTarget;
    private ShaderResourceView? _shaderTargetSrv;
    private RenderTargetView?   _shaderTargetRtv;
    private PixelSize           _shaderTargetSize;

    private Texture2D? _readbackStaging;
    private PixelSize  _readbackSize;

    // ------------------------------------------------------------------
    // Static image (GPU modes)
    // ------------------------------------------------------------------
    private Texture2D?          _staticImageTexture;
    private ShaderResourceView? _staticImageView;

    private VertexShader? _vertexShader;
    private PixelShader?  _pixelShader;
    private InputLayout?  _inputLayout;
    private Buffer?       _vertexBuffer;
    private SamplerState? _sampler;

    // ------------------------------------------------------------------
    // Resize debounce (swapchain mode)
    // ------------------------------------------------------------------
    private PixelSize _currentSwapchainSize;
    private PixelSize _pendingSwapchainSize;
    private int       _pendingSwapchainTicks;
    private const int ResizeStableTicks = 2;

    // ------------------------------------------------------------------
    // Video processor
    // ------------------------------------------------------------------
    private VideoDevice1?             _videoDevice1;
    private VideoContext1?            _videoContext1;
    private VideoProcessor?           _videoProcessor;
    private VideoProcessorEnumerator? _vpe;

    private VideoProcessorInputViewDescription  _vpivd;
    private VideoProcessorOutputViewDescription _vpovd;
    private VideoProcessorContentDescription    _vpcd;
    private bool _videoProcessorReady;

    private readonly Dictionary<IntPtr, VideoProcessorOutputView> _vpovCache = new();
    private const int MaxOutputViewCacheSize = 8;

    private const int VideoProcessorOutputAlignment = 256;
    private const int VideoProcessorShrinkThreshold = VideoProcessorOutputAlignment * 4;

    // ------------------------------------------------------------------
    // Current frame (all access under _d3dLock)
    // ------------------------------------------------------------------
    private Texture2D? _currentVideoFrame;
    private bool       _currentVideoFrameOwned = true;
    private int        _currentVideoFrameSlice;
    private long       _currentFrameSerial;
    private long       _lastDrawnSerial = -1;
    private bool       _forceRedraw = true;

    // ------------------------------------------------------------------
    // Last-good-frame copy of the back buffer (swapchain mode only)
    // ------------------------------------------------------------------
    private Texture2D? _lastGoodFrameTexture;
    private PixelSize  _lastGoodFrameSize;
    private Format     _lastGoodFrameFormat;

    // ------------------------------------------------------------------
    // Misc
    // ------------------------------------------------------------------
    private readonly Stopwatch _globalClock = Stopwatch.StartNew();
    public long NowTicks => _globalClock.ElapsedTicks;

    // ------------------------------------------------------------------
    // Decode -> draw latency measurement
    // ------------------------------------------------------------------
    private const int StatsWindowSize = 120;
    private const bool EnableLatencyLogging = false;

    private readonly object _latencyLock = new();
    private readonly Queue<double> _decodeToDrawHistory = new();
    private Texture2D? _lastLatencyTimedFrame;
    private long _currentFrameDecodeTimestamp;
    private long _swFrameDecodeTimestamp;
    private long _swLatencyTimedSerial = -1;
    private int _redrawStreak;
    private int _statsPrintCounter;

    /// <summary>Path of the video to play. Set this before calling Play_video().</summary>
    public string FileToPlay { get; set; } =
        @"M:\movie\Kung.Fu.Panda.3.2016.720p.WEBRip.x264.AAC-ETRG.mp4";

    private Src.FFmpeg? ffmpeg;
    private Thread? threadPlay;
    private volatile bool is_running = true;
    private bool _disposed;
    private bool _warpMode;

    // ==================================================================
    // Public API
    // ==================================================================

    public D3DDevice? my_Device => _device;

    public string BackendName =>
        _warpMode
            ? (_device is null
                ? "Software renderer (WARP, no physical GPU)"
                : $"Software renderer (WARP, CPU D3D11 {_device.FeatureLevel})")
            : _softwareMode
                ? "Software renderer (CPU)"
                : _device is null
                    ? "Direct3D 11 (Avalonia interop, uninitialized)"
                    : _gpuShaderMode
                        ? $"Direct3D 11 ({_device.FeatureLevel}) [Avalonia interop: shader mode]"
                        : $"Direct3D 11 ({_device.FeatureLevel}) [Avalonia interop]";

    public event EventHandler? Initialized;
    public bool IsInitialized =>
        _softwareMode || (_device is not null && (_swapchain is not null || _gpuShaderMode));

    public static D11InteropRenderer? Instance { get; private set; }

    // ---- shims (kept so existing callers still compile) ----
    public void Initialize(IntPtr outputHandle, int d_width = 0, int d_height = 0) { }
    public void PresentStaticImage() { }
    public void PresentFrameKeepAlive() { }
    public void HandleResize() { }
    public void ResizeToClient(IntPtr hwnd) { }

    // ------------------------------------------------------------------
    // PresentFrame overloads
    //
    //   Swapchain mode : stores the texture; RenderFrame blits it.
    //   GPU-shader mode: stores the texture and brings it to the CPU RIGHT
    //                    HERE on the caller's thread (decoder), then hands
    //                    the pixels to Avalonia through SetSoftwareFrame.
    //   Software mode  : no D3D device, so a Texture2D can't be drawn.
    //                    Call SetSoftwareFrame directly instead.
    // ------------------------------------------------------------------
    public void PresentFrame(Texture2D textureHW, long decodeTimestamp,
                             int d_width = 0, int d_height = 0)
        => PresentFrameInternal(textureHW, decodeTimestamp, arraySlice: 0, ownsTexture: true);

    public void PresentFrame(Texture2D textureHW, int d_width = 0, int d_height = 0)
        => PresentFrameInternal(textureHW, _globalClock.ElapsedTicks, arraySlice: 0, ownsTexture: true);

    private void PresentFrameInternal(Texture2D? texture, long decodeTimestamp,
                                      int arraySlice, bool ownsTexture)
    {
        if (texture == null) return;

        if (_softwareMode && !_warpMode)
        {
            // True CPU-only mode has no D3D device.
            if (ownsTexture) { try { texture.Dispose(); } catch { } }
            return;
        }

        SetSourceTexture(texture, decodeTimestamp, arraySlice, ownsTexture);

        if (_gpuShaderMode)
        {
            // All D3D work happens on THIS (non-UI) thread.
            RenderShaderNow();
            return;
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (this.GetVisualRoot() is Avalonia.Rendering.IRenderRoot root)
            {
                root.Renderer.Paint(new Rect(root.ClientSize));
            }
        }, Avalonia.Threading.DispatcherPriority.Render);
    }

    /// <summary>
    /// GPU modes only. Replaces the frame RenderFrame draws.
    /// ownsTexture = true : this renderer disposes the texture when replaced.
    /// ownsTexture = false: the texture belongs to FFmpeg; never dispose it here.
    /// </summary>
    public void SetSourceTexture(Texture2D? texture, long decodeTimestamp = 0,
                                 int arraySlice = 0, bool ownsTexture = true)
    {
        if (texture == null) return;
        if (_softwareMode && !_warpMode)
        {
            if (ownsTexture) { try { texture.Dispose(); } catch { } }
            return;
        }

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
            _currentFrameDecodeTimestamp = decodeTimestamp != 0
                ? decodeTimestamp
                : _globalClock.ElapsedTicks;
            _currentFrameSerial++;
            _forceRedraw = true;
        }
    }

    public void ResizeSwapChain(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        if (_softwareMode) return;

        lock (_d3dLock)
        {
            if (_gpuShaderMode)
            {
                // Shader mode renders at the source frame size; Avalonia scales
                // the bitmap on draw, so a window resize needs no D3D work.
                return;
            }

            _currentSwapchainSize  = new PixelSize(width, height);
            _pendingSwapchainSize  = default;
            _pendingSwapchainTicks = 0;
            ClearOutputViewCache();
            _forceRedraw = true;
        }
    }

    public void RunOnContext(Action<DeviceContext> action)
    {
        if (_device == null) return;
        lock (_d3dLock) action(_device.ImmediateContext);
    }

    public new void Dispose() => DisposeAll();

    // ==================================================================
    // Software (CPU) rendering path
    // ==================================================================

    protected override (bool success, string info) InitializeSoftwareFallback(string reason)
    {
        Instance = this;

        // Software mode means there is NO physical GPU. WARP is Microsoft's
        // CPU implementation of D3D11. Never reuse a device that
        // InitializeGraphicsResources() may have partially created.
        lock (_d3dLock)
        {
            _gpuShaderMode = false;
            _warpMode = false;

            if (_device is not null)
            {
                try { _device.ImmediateContext.ClearState(); } catch { }
                try { _device.ImmediateContext.Flush(); } catch { }
            }

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

            Utilities.Dispose(ref _shaderTargetRtv);
            Utilities.Dispose(ref _shaderTargetSrv);
            Utilities.Dispose(ref _shaderTarget);
            Utilities.Dispose(ref _readbackStaging);

            Utilities.Dispose(ref _lastGoodFrameTexture);

            if (_currentVideoFrameOwned)
                Utilities.Dispose(ref _currentVideoFrame);
            else
                _currentVideoFrame = null;

            Utilities.Dispose(ref _device);

            _swapchain = null;
        }

        // Create ONLY WARP. Do not enumerate adapters and do not request
        // DriverType.Hardware here.
        try
        {
            lock (_d3dLock)
            {
                _device = new D3DDevice(
                    SharpDX.Direct3D.DriverType.Warp,
                    DeviceCreationFlags.BgraSupport,
                    new[]
                    {
                        FeatureLevel.Level_11_1,
                        FeatureLevel.Level_11_0,
                        FeatureLevel.Level_10_0,
                        FeatureLevel.Level_9_3,
                        FeatureLevel.Level_9_2,
                        FeatureLevel.Level_9_1
                    });

                _warpMode = true;

                CreateShaders();
                CreateQuad();
                CreateSampler();

                // WARP is CPU D3D11, so use the same shader/readback path.
                _gpuShaderMode = true;
                _softwareMode = true;
            }

            Console.WriteLine(
                "[Interop] Software mode: created WARP D3D11 device " +
                "(CPU only, no physical GPU)");

            Initialized?.Invoke(this, EventArgs.Empty);

            return (
                true,
                $"Software renderer (WARP/CPU, no physical GPU) ({reason})");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[Interop] WARP software fallback failed: {ex.Message}");

            // If WARP itself is unavailable, fall all the way back to the
            // pure Avalonia CPU bitmap path.
            lock (_d3dLock)
            {
                Utilities.Dispose(ref _sampler);
                Utilities.Dispose(ref _vertexBuffer);
                Utilities.Dispose(ref _inputLayout);
                Utilities.Dispose(ref _vertexShader);
                Utilities.Dispose(ref _pixelShader);
                Utilities.Dispose(ref _device);

                _gpuShaderMode = false;
                _warpMode = false;
                _softwareMode = true;
            }

            Console.WriteLine(
                $"[Interop] Software mode: pure Avalonia CPU renderer ({reason})");

            Initialized?.Invoke(this, EventArgs.Empty);

            return (
                true,
                $"Software renderer (CPU only) ({reason})");
        }
    }

    /// <summary>
    /// Feed a BGRA frame (stride in bytes). Pixels are copied once, straight
    /// into the WriteableBitmap, on the calling thread.
    /// </summary>
    public unsafe void SetSoftwareFrame(byte[] bgra, int width, int height, int stride, long decodeTimestamp = 0)
    {
        if (bgra == null || width <= 0 || height <= 0 || stride < width * 4) return;
        if (bgra.Length < stride * height) return;

        fixed (byte* p = bgra)
            SetSoftwareFrame((IntPtr)p, width, height, stride, decodeTimestamp);
    }

    /// <summary>
    /// Zero-extra-copy path (mapped texture, AVFrame data, ...).
    /// The memory only needs to stay valid for the duration of the call.
    /// </summary>
    public unsafe void SetSoftwareFrame(IntPtr src, int width, int height, int stride, long decodeTimestamp = 0)
    {
        if (src == IntPtr.Zero ||
            width <= 0 ||
            height <= 0 ||
            stride < width * 4)
            return;

        Avalonia.Media.Imaging.WriteableBitmap target;
        int slot;

        // 1) Pick a slot that is NOT the one being displayed. Lock is brief.
        lock (_swLock)
        {
            // Ring is rebuilt only when the VIDEO SOURCE size changes.
            if (_swW != width || _swH != height)
            {
                for (int i = 0; i < _swRing.Length; i++)
                {
                    if (_swRing[i] != null) _swRetiredList.Add(_swRing[i]!);
                    _swRing[i] = null;
                }

                _swFront = -1;
                _swW = width;
                _swH = height;
                _swCachedDestValid = false;
            }

            slot = (_swFront + 1) % _swRing.Length;

            _swRing[slot] ??= new Avalonia.Media.Imaging.WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                Avalonia.Platform.AlphaFormat.Opaque);

            target = _swRing[slot]!;
        }

        // 2) Copy OUTSIDE the lock, into a buffer nobody is displaying.
        using (var fb = target.Lock())
        {
            byte* s = (byte*)src;
            byte* d = (byte*)fb.Address;

            if (fb.RowBytes == stride)
            {
                long totalBytes = (long)stride * height;
                System.Buffer.MemoryCopy(s, d, totalBytes, totalBytes);
            }
            else
            {
                int rowBytes = Math.Min(width * 4, fb.RowBytes);

                for (int y = 0; y < height; y++)
                {
                    System.Buffer.MemoryCopy(
                        s + (long)y * stride,
                        d + (long)y * fb.RowBytes,
                        rowBytes,
                        rowBytes);
                }
            }
        }

        // 3) Publish the slot.
        lock (_swLock)
        {
            if (_swW == width && _swH == height)
            {
                _swFront = slot;
                _swFrameDecodeTimestamp = decodeTimestamp != 0
                    ? decodeTimestamp
                    : _globalClock.ElapsedTicks;
                _swSerial++;
            }
        }

        // Coalesce frame notifications. Do NOT force root.Renderer.Paint()
        // here: forcing a Paint in the middle of resize processing causes a
        // one-frame glitch. Send priority so we are not queued behind other
        // // render-priority work.
        if (Interlocked.Exchange(ref _swInvalidatePending, 1) == 0)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () =>
                {
                    Interlocked.Exchange(ref _swInvalidatePending, 0);
        
                    if (_disposed)
                        return;
        
                    InvalidateVisual();
                },
                Avalonia.Threading.DispatcherPriority.Send);
        }
        
        
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (this.GetVisualRoot() is Avalonia.Rendering.IRenderRoot root)
            {
                root.Renderer.Paint(new Rect(root.ClientSize));
            }
        }, Avalonia.Threading.DispatcherPriority.Render);
        
    }

    public override void Render(DrawingContext ctx)
    {
        // Only the CPU-bitmap paths paint here.
        if (!_softwareMode && !_gpuShaderMode)
        {
            base.Render(ctx);
            return;
        }

        var bounds = new Rect(Bounds.Size);

        ctx.FillRectangle(Brushes.Black, bounds);

        Avalonia.Media.Imaging.WriteableBitmap? bmp;

        int w;
        int h;
        long serial;
        long timestamp;

        lock (_swLock)
        {
            bmp = _swFront >= 0 ? _swRing[_swFront] : null;

            w = _swW;
            h = _swH;

            serial = _swSerial;
            timestamp = _swFrameDecodeTimestamp;

            // Dispose bitmaps from a previous source size on the UI side,
            // never from the decoder thread.
            if (_swRetiredList.Count > 0)
            {
                foreach (var r in _swRetiredList)
                    r.Dispose();
                _swRetiredList.Clear();
            }
        }

        if (bmp != null &&
            w > 0 &&
            h > 0)
        {
            Rect destination;

            // The destination rectangle changes only when the control is
            // resized. Normal video frames reuse the cached rectangle.
            if (!_swCachedDestValid ||
                _swCachedBounds != bounds ||
                _swCachedW != w ||
                _swCachedH != h)
            {
                destination = CalculateSoftwareDestination(w, h, bounds);

                _swCachedBounds = bounds;
                _swCachedW = w;
                _swCachedH = h;
                _swCachedDest = destination;
                _swCachedDestValid = true;
            }
            else
            {
                destination = _swCachedDest;
            }

            if (destination != new Rect())
            {
                ctx.DrawImage(
                    bmp,
                    new Rect(0, 0, w, h),
                    destination);
            }

            // Decode -> draw measurement, recorded once per new frame.
            if (serial != _swLatencyTimedSerial &&
                timestamp != 0)
            {
                double latencyMs =
                    (_globalClock.ElapsedTicks - timestamp)
                    * 1000.0
                    / Stopwatch.Frequency;

                RecordDecodeToDrawLatency(latencyMs);

                _swLatencyTimedSerial = serial;
                _swRenderedSerial = serial;
            }

            return;
        }

        if (_swStaticImage != null)
        {
            int staticWidth  = (int)_swStaticImage.Size.Width;
            int staticHeight = (int)_swStaticImage.Size.Height;

            DrawLetterboxed(ctx, _swStaticImage, staticWidth, staticHeight, bounds);
        }
    }

    private static Rect CalculateSoftwareDestination(
        int srcW,
        int srcH,
        Rect bounds)
    {
        if (srcW <= 0 ||
            srcH <= 0 ||
            bounds.Width <= 0 ||
            bounds.Height <= 0)
        {
            return new Rect();
        }

        double scale = Math.Min(
            bounds.Width / srcW,
            bounds.Height / srcH);

        double width = srcW * scale;
        double height = srcH * scale;

        return new Rect(
            (bounds.Width - width) * 0.5,
            (bounds.Height - height) * 0.5,
            width,
            height);
    }

    private void RecordDecodeToDrawLatency(double latencyMs)
    {
        if (latencyMs < 0 || double.IsNaN(latencyMs) || double.IsInfinity(latencyMs))
            return;

        lock (_latencyLock)
        {
            _decodeToDrawHistory.Enqueue(latencyMs);
            while (_decodeToDrawHistory.Count > StatsWindowSize)
                _decodeToDrawHistory.Dequeue();
        }

        // Console output can take milliseconds; print once per window,
        // not on every frame.
        if (EnableLatencyLogging && ++_statsPrintCounter >= 30)
        // if (EnableLatencyLogging)
        {
            _statsPrintCounter = 0;
            PrintDecodeToDrawStats();
        }
    }

    private void PrintDecodeToDrawStats()
    {
        double[] samples;
        int redrawStreak;

        lock (_latencyLock)
        {
            samples = _decodeToDrawHistory.ToArray();
            redrawStreak = _redrawStreak;
        }

        if (samples.Length == 0) return;

        double sum = 0;
        double min = double.MaxValue;
        double max = double.MinValue;

        foreach (double value in samples)
        {
            sum += value;
            if (value < min) min = value;
            if (value > max) max = value;
        }

        double avg = sum / samples.Length;
        double variance = 0;
        foreach (double value in samples)
        {
            double delta = value - avg;
            variance += delta * delta;
        }

        double stddev = Math.Sqrt(variance / samples.Length);

        string message =
            $"[DecodeToDrawLatency][Interop] avg={avg:F3}ms min={min:F3}ms " +
            $"max={max:F3}ms stddev={stddev:F3}ms window={samples.Length} " +
            $"redrawStreak={redrawStreak}";

        Debug.WriteLine(message);
        Console.WriteLine(message);
    }

    private static void DrawLetterboxed(
        DrawingContext ctx,
        Avalonia.Media.IImage image,
        int srcW,
        int srcH,
        Rect bounds)
    {
        if (srcW <= 0 ||
            srcH <= 0 ||
            bounds.Width <= 0 ||
            bounds.Height <= 0)
            return;

        Rect destination = CalculateSoftwareDestination(srcW, srcH, bounds);

        if (destination == new Rect())
            return;

        ctx.DrawImage(
            image,
            new Rect(0, 0, srcW, srcH),
            destination);
    }

    // ==================================================================
    // Static image
    // ==================================================================

    public void DisplayImage(string fileName)
    {
        string imagePath = Path.IsPathRooted(fileName)
            ? fileName
            : Path.Combine(Directory.GetCurrentDirectory(), fileName);

        if (_softwareMode)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    Console.WriteLine($"Image file not found: {imagePath}");
                    return;
                }

                var bmp = new Avalonia.Media.Imaging.Bitmap(imagePath);
                
                int width  = bmp.PixelSize.Width;
                int height = bmp.PixelSize.Height;
                
                // Console.WriteLine($"gpu introp dx11 {width} x{height}");
                if (My_Store.Instance.VideoHeight == 0 || My_Store.Instance.VideoHeight == 0)
                {
                    My_Store.Instance.SetVideoResolution((int)width, (int)height);
                }

                if (My_Store.Instance?.DeviceHeight == 0 || My_Store.Instance?.DeviceWidth == 0 && my_info.Instance.DeveloperMode)
                {
                    My_Store.Instance.SetDeviceResolution((int)width, (int)height);
                }
                

                lock (_swLock)
                {
                    _swStaticImage?.Dispose();
                    _swStaticImage = bmp;
                    _swCachedDestValid = false;
                }

                if (Interlocked.Exchange(ref _swInvalidatePending, 1) == 0)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(
                        () =>
                        {
                            Interlocked.Exchange(ref _swInvalidatePending, 0);

                            if (_disposed)
                                return;

                            InvalidateVisual();
                        },
                        Avalonia.Threading.DispatcherPriority.Render);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error displaying image {fileName}: {ex.Message}");
            }

            return;
        }

        if (_device == null)
        {
            Console.WriteLine("[Interop] DisplayImage before init.");
            return;
        }

        try
        {
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

            // Shader mode has no per-tick RenderFrame: draw it now.
            if (_gpuShaderMode)
                RenderShaderNow();
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

             Console.WriteLine($"gpu introp dx11 {width} x{height}");
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
        try
        {
            Instance = this;

            bool interopOk =
                interop.SupportedImageHandleTypes.Contains(
                    KnownPlatformGraphicsExternalImageHandleTypes
                        .D3D11TextureGlobalSharedHandle) == true;

            using var factory = new DxgiFactory();

            // Pick the first REAL hardware adapter. Skip WARP / Basic Render Driver.
            Adapter1? adapter = null;
            int count = factory.GetAdapterCount1();
            for (int i = 0; i < count; i++)
            {
                var candidate = factory.GetAdapter1(i);
                var d = candidate.Description1;
                bool isSoftware = (d.Flags & AdapterFlags.Software) != 0 || d.VendorId == 0x1414;
                if (!isSoftware)
                {
                    adapter = candidate;
                    break;
                }
                candidate.Dispose();
            }

            if (adapter == null)
                return (false, "No hardware GPU found");

            using (adapter)
            {
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

                // ---- Swapchain only if interop is usable. ----
                if (interopOk)
                {
                    try
                    {
                        _swapchain = new D3D11Swapchain(_device, interop, surface);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[Interop] Swapchain creation failed: {ex.Message} — falling back to shader mode");
                        _swapchain = null;
                        interopOk = false;
                    }
                }

                if (!interopOk)
                {
                    _gpuShaderMode = true;
                    Console.WriteLine(
                        "[Interop] Avalonia GPU interop unavailable — using D3D11 shader mode");
                }

                // Video processor (only useful in swapchain mode).
                if (interopOk)
                {
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

                Console.WriteLine("initialized gpuinterop class");

                string adapterName = adapter.Description1.Description;
                return (true,
                    _gpuShaderMode
                        ? $"D3D11 ({_device.FeatureLevel}) {adapterName} [Avalonia interop: shader mode]"
                        : $"D3D11 ({_device.FeatureLevel}) {adapterName} [Avalonia interop]");
            }
        }
        catch (Exception ex)
        {
            // Base class will clean up and switch to software mode.
            return (false, $"GPU init failed: {ex.Message}");
        }
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
    // Render dispatch
    // ==================================================================

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
        if (_softwareMode) return;

        // Shader mode is driven by PresentFrame on the decoder thread.
        if (_gpuShaderMode) return;

        if (pixelSize == default) return;
        if (pixelSize.Width <= 1 || pixelSize.Height <= 1) return;
        if (_device is null) return;

        RenderSwapchainMode(pixelSize);
    }

    // ==================================================================
    // GPU shader mode (runs on the caller's thread, NOT the UI thread)
    //
    //   Fast path  (BGRA frame): CopySubresourceRegion -> staging -> Map
    //                            -> SetSoftwareFrame. No SRV, no draw.
    //   Shader path (anything else, or static image): draw into an
    //                            offscreen target at source size, then
    //                            copy -> staging -> Map -> SetSoftwareFrame.
    //
    // The UI thread then only draws the bitmap.
    // ==================================================================

    private void RenderShaderNow()
    {
        lock (_d3dLock)
        {
            if (_device is null || !_gpuShaderMode) return;

            // Fast path: plain BGRA live frame needs no shader pass at all.
            var liveFrame = _currentVideoFrame;
            if (liveFrame is not null &&
                liveFrame.NativePointer != IntPtr.Zero &&
                TryDirectCopyReadback(liveFrame, _currentFrameDecodeTimestamp, _currentVideoFrameSlice))
            {
                _lastDrawnSerial = _currentFrameSerial;
                _forceRedraw     = false;
                return;
            }

            // Shader path: live frame first, then the static image.
            ShaderResourceView? srv = null;
            bool ownsSrv = false;
            int srcW = 0, srcH = 0;

            try
            {
                var frame = _currentVideoFrame;
                if (frame is not null && frame.NativePointer != IntPtr.Zero)
                {
                    try
                    {
                        var fd = frame.Description;
                        srv  = new ShaderResourceView(_device, frame);
                        ownsSrv = true;
                        srcW = fd.Width;
                        srcH = fd.Height;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Interop/shader] Live frame SRV failed: {ex.Message}");
                        srv = null;
                    }
                }

                if (srv is null && _staticImageView is not null && _staticImageTexture is not null)
                {
                    var sd = _staticImageTexture.Description;
                    srv  = _staticImageView;
                    srcW = sd.Width;
                    srcH = sd.Height;
                }

                if (srv is null || srcW <= 0 || srcH <= 0) return;

                var targetSize = new PixelSize(srcW, srcH);

                // (Re)create the offscreen target only when the source size changes.
                if (_shaderTarget is null || _shaderTargetRtv is null || _shaderTargetSize != targetSize)
                {
                    Utilities.Dispose(ref _shaderTargetRtv);
                    Utilities.Dispose(ref _shaderTargetSrv);
                    Utilities.Dispose(ref _shaderTarget);

                    try
                    {
                        _shaderTarget = new Texture2D(_device, new Texture2DDescription
                        {
                            Width             = srcW,
                            Height            = srcH,
                            ArraySize         = 1,
                            MipLevels         = 1,
                            Format            = Format.B8G8R8A8_UNorm,
                            SampleDescription = new SampleDescription(1, 0),
                            Usage             = ResourceUsage.Default,
                            BindFlags         = BindFlags.RenderTarget | BindFlags.ShaderResource,
                            CpuAccessFlags    = CpuAccessFlags.None,
                            OptionFlags       = ResourceOptionFlags.None
                        });
                        _shaderTargetSrv  = new ShaderResourceView(_device, _shaderTarget);
                        _shaderTargetRtv  = new RenderTargetView(_device, _shaderTarget);
                        _shaderTargetSize = targetSize;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Interop/shader] Failed to create target: {ex.Message}");
                        Utilities.Dispose(ref _shaderTargetRtv);
                        Utilities.Dispose(ref _shaderTargetSrv);
                        Utilities.Dispose(ref _shaderTarget);
                        return;
                    }
                }

                var context = _device.ImmediateContext;

                try
                {
                    context.OutputMerger.SetTargets(_shaderTargetRtv);
                    context.ClearRenderTargetView(_shaderTargetRtv, new RawColor4(0, 0, 0, 1));

                    // Target == source size, so the letterbox rect is the full target.
                    DrawFullscreenQuad(context, srv, srcW, srcH, targetSize);

                    context.PixelShader.SetShaderResource(0, null);
                    context.OutputMerger.ResetTargets();

                    _lastDrawnSerial = _currentFrameSerial;
                    _forceRedraw     = false;

                    // CopyResource + Map synchronise with the GPU; no extra Flush needed.
                    ReadbackShaderTargetToSoftware(_currentFrameDecodeTimestamp);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Interop/shader] Render failed: {ex.Message}");
                }
            }
            finally
            {
                if (ownsSrv) srv?.Dispose();
            }
        }
    }

    /// <summary>
    /// Bit-exact shortcut: for a plain B8G8R8A8_UNorm frame the shader pass
    /// is an identity copy (target == source size, UV 0..1), so copy the
    /// frame straight to the staging texture. Returns false for any other
    /// format so the caller uses the shader path.
    /// Must be called under _d3dLock.
    /// </summary>
    private bool TryDirectCopyReadback(Texture2D frame, long decodeTimestamp, int slice)
    {
        if (_device is null) return false;

        Texture2DDescription fd;
        try { fd = frame.Description; }
        catch { return false; }

        if (fd.Format != Format.B8G8R8A8_UNorm ||
            fd.SampleDescription.Count != 1 ||
            fd.Width <= 0 || fd.Height <= 0)
            return false;

        try
        {
            EnsureReadbackStaging(fd.Width, fd.Height);

            var ctx = _device.ImmediateContext;
            int sub = Resource.CalculateSubResourceIndex(
                0, fd.ArraySize > 1 ? slice : 0, fd.MipLevels);

            ctx.CopySubresourceRegion(frame, sub, null, _readbackStaging, 0);
            MapAndPublish(ctx, fd.Width, fd.Height, decodeTimestamp);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Interop/shader] Direct copy failed, using shader: {ex.Message}");
            return false;
        }
    }

    private void EnsureReadbackStaging(int w, int h)
    {
        var size = new PixelSize(w, h);
        if (_readbackStaging is not null && _readbackSize == size) return;

        Utilities.Dispose(ref _readbackStaging);
        _readbackStaging = new Texture2D(_device!, new Texture2DDescription
        {
            Width             = w,
            Height            = h,
            ArraySize         = 1,
            MipLevels         = 1,
            Format            = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage             = ResourceUsage.Staging,
            BindFlags         = BindFlags.None,
            CpuAccessFlags    = CpuAccessFlags.Read,
            OptionFlags       = ResourceOptionFlags.None
        });
        _readbackSize = size;
    }

    /// <summary>
    /// Maps the staging texture and feeds the mapped memory straight to
    /// SetSoftwareFrame (no managed array, correct RowPitch).
    /// </summary>
    private void MapAndPublish(DeviceContext ctx, int w, int h, long decodeTimestamp)
    {
        var box = ctx.MapSubresource(_readbackStaging!, 0, MapMode.Read,
                                     SharpDX.Direct3D11.MapFlags.None);
        try
        {
            SetSoftwareFrame(box.DataPointer, w, h, box.RowPitch, decodeTimestamp);
        }
        finally
        {
            ctx.UnmapSubresource(_readbackStaging!, 0);
        }
    }

    /// <summary>Shader path readback: _shaderTarget -> staging -> bitmap.</summary>
    private void ReadbackShaderTargetToSoftware(long decodeTimestamp)
    {
        if (_device is null || _shaderTarget is null) return;

        var desc = _shaderTarget.Description;
        if (desc.Width <= 0 || desc.Height <= 0) return;

        try
        {
            EnsureReadbackStaging(desc.Width, desc.Height);

            var ctx = _device.ImmediateContext;
            ctx.CopyResource(_shaderTarget, _readbackStaging);
            MapAndPublish(ctx, desc.Width, desc.Height, decodeTimestamp);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Interop/shader] Readback failed: {ex.Message}");
        }
    }

    // ==================================================================
    // Swapchain mode (unchanged behavior)
    // ==================================================================

    private void RenderSwapchainMode(PixelSize pixelSize)
    {
        if (_swapchain is null || _device is null) return;

        bool sizeChanged = TryCommitSwapchainSize(pixelSize);

        // Size still settling: keep showing the last presented image.
        if (pixelSize != _currentSwapchainSize) return;

        lock (_d3dLock)
        {
            if (sizeChanged)
                ClearOutputViewCache();

            bool newFrame = _currentFrameSerial != _lastDrawnSerial;
            if (!newFrame && !sizeChanged && !_forceRedraw)
            {
                _redrawStreak++;
                return;
            }

            var context = _device.ImmediateContext;

            using (_swapchain.BeginDraw(_currentSwapchainSize, out var renderView))
            {
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
                            didBlit = BlitVideoFrame(frame, _currentVideoFrameSlice,
                                renderTexture, _currentSwapchainSize);

                            if (!didBlit)
                            {
                                ClearOutputViewCache();
                                didBlit = BlitVideoFrame(frame, _currentVideoFrameSlice,
                                    renderTexture, _currentSwapchainSize);
                            }

                            if (didBlit)
                            {
                                CaptureLastGoodFrame(context, renderTexture);

                                if (frame != _lastLatencyTimedFrame && _currentFrameDecodeTimestamp != 0)
                                {
                                    double latencyMs = (_globalClock.ElapsedTicks - _currentFrameDecodeTimestamp)
                                        * 1000.0 / Stopwatch.Frequency;
                                    RecordDecodeToDrawLatency(latencyMs);
                                    _lastLatencyTimedFrame = frame;
                                    _redrawStreak = 0;
                                }
                            }
                        }
                    }

                    if (!didBlit)
                    {
                        bool drawn = false;

                        if (_currentVideoFrame is not null && renderTexture is not null)
                            drawn = TryRestoreLastGoodFrame(context, renderTexture, renderView);

                        if (!drawn && _staticImageView is not null && _staticImageTexture is not null)
                        {
                            DrawFullscreenQuad(context, _staticImageView,
                                _staticImageTexture.Description.Width,
                                _staticImageTexture.Description.Height,
                                _currentSwapchainSize);
                        }
                    }

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
    private static void ComputeLetterboxRect(int srcW, int srcH, PixelSize bb,
        out int outX, out int outY, out int outW, out int outH)
    {
        int bbW = bb.Width;
        int bbH = bb.Height;

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
    // Fullscreen quad
    // ------------------------------------------------------------------
    private void DrawFullscreenQuad(DeviceContext context, ShaderResourceView view,
                                    int srcW, int srcH, PixelSize target)
    {
        ComputeLetterboxRect(srcW, srcH, target,
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
    // Video blit (swapchain mode)
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

            ComputeLetterboxRect(inDesc.Width, inDesc.Height, destSize,
                out int outX, out int outY, out int outW, out int outH);

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
    // Last good frame (swapchain mode)
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
                    Format            = src.Format,
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

        ReleaseGpuAsync();
        ReleaseSoftwareResources();
    }

    /// <summary>
    /// Disposes the swapchain first (async), then every other D3D object.
    /// Safe to call when nothing was created.
    /// </summary>
    private void ReleaseGpuAsync()
    {
        var swapchain = _swapchain;
        _swapchain = null;

        if (swapchain is null)
            ReleaseD3DResources();
        else
            _ = swapchain.DisposeAsync().AsTask()
                .ContinueWith(_ => ReleaseD3DResources(), TaskScheduler.Default);
    }

    private void ReleaseSoftwareResources()
    {
        lock (_swLock)
        {
            for (int i = 0; i < _swRing.Length; i++)
            {
                _swRing[i]?.Dispose();
                _swRing[i] = null;
            }
            _swFront = -1;

            foreach (var r in _swRetiredList)
                r.Dispose();
            _swRetiredList.Clear();

            _swStaticImage?.Dispose(); _swStaticImage = null;

            _swCachedDestValid = false;
            _swRenderedSerial = -1;
        }
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

            Utilities.Dispose(ref _shaderTargetRtv);
            Utilities.Dispose(ref _shaderTargetSrv);
            Utilities.Dispose(ref _shaderTarget);
            Utilities.Dispose(ref _readbackStaging);

            Utilities.Dispose(ref _lastGoodFrameTexture);
            Utilities.Dispose(ref _device);

            _gpuShaderMode = false;
            _warpMode = false;
            _softwareMode = false;
        }
    }
}

