
using System;
using System.IO;
using System.Runtime.InteropServices;
using Androidplayer.Store;
using SharpDX.DXGI;
using SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;

using Device    = SharpDX.Direct3D11.Device;
using Resource  = SharpDX.Direct3D11.Resource;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.WIC;

namespace Androidplayer.Src
{
    public class DirectX
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        #region Declaration
        internal Device                     _device;
        SwapChain                           _swapChain;

        Texture2D                           _backBuffer;

        VideoDevice1                        videoDevice1;
        VideoProcessor                      videoProcessor;
        VideoContext1                       videoContext1;
        VideoProcessorEnumerator            vpe;
        VideoProcessorContentDescription    vpcd;
        VideoProcessorOutputViewDescription vpovd;
        VideoProcessorInputViewDescription  vpivd;
        VideoProcessorInputView             vpiv;
        VideoProcessorOutputView            vpov;
        VideoProcessorStream[]              vpsa;

        private ImagingFactory              _imagingFactory;

        // Shader resources for software fallback
        private VertexShader                _vertexShader;
        private PixelShader                 _pixelShader;
        private InputLayout                 _inputLayout;
        private SharpDX.Direct3D11.Buffer   _vertexBuffer;
        private SharpDX.Direct3D11.Buffer   _indexBuffer;
        private SamplerState                _samplerState;
        private bool                        _shaderResourcesInitialized = false;
        private bool                        _useHardwareVideoProcessor = false;

        public Device my_Device
        {
            get { return _device; }
        }

        private bool _isresizing = false;
        private readonly object _renderLock = new object();

        // Tracks last output rect so we only log when it changes
        private RawRectangle _lastLogDest;
        private bool _lastLogValid = false;

        public DirectX(IntPtr outputHandle) { Initialize(outputHandle); }
        #endregion

        private void Initialize(IntPtr outputHandle)
        {
            if (outputHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "DirectX output HWND is null/zero.");
            }
            try
            {
                int windowWidth = 640;
                int windowHeight = 480;

                if (GetClientRect(outputHandle, out RECT rect))
                {
                    windowWidth = Math.Max(rect.Right - rect.Left, 1);
                    windowHeight = Math.Max(rect.Bottom - rect.Top, 1);
                }

                Console.WriteLine($"Creating swap chain with size: {windowWidth}x{windowHeight}");

                var desc = new SwapChainDescription()
                {
                    BufferCount = 2,
                    ModeDescription = new ModeDescription(windowWidth, windowHeight, new Rational(60, 1), Format.B8G8R8A8_UNorm),
                    IsWindowed = true,
                    OutputHandle = outputHandle,
                    SampleDescription = new SampleDescription(1, 0),
                    SwapEffect = SwapEffect.Discard,
                    Usage = Usage.RenderTargetOutput
                };

                DriverType driverType = SharpDX.Direct3D.DriverType.Hardware;
                bool useWarp = false;

                try
                {
                    Device.CreateWithSwapChain(driverType,
                        DeviceCreationFlags.BgraSupport, desc, out _device, out _swapChain);
                    Console.WriteLine("DirectX initialized with Hardware driver");
                }
                catch (SharpDXException ex)
                {
                    Console.WriteLine($"Hardware device creation failed: {ex.Message}");
                    Console.WriteLine("Falling back to WARP software renderer...");

                    try
                    {
                        driverType = SharpDX.Direct3D.DriverType.Warp;
                        useWarp = true;
                        Device.CreateWithSwapChain(driverType,
                            DeviceCreationFlags.BgraSupport, desc, out _device, out _swapChain);
                        Console.WriteLine("DirectX initialized with WARP software renderer");
                    }
                    catch (SharpDXException ex2)
                    {
                        Console.WriteLine($"WARP device creation also failed: {ex2.Message}");
                        throw;
                    }
                }

                _backBuffer = Texture2D.FromSwapChain<Texture2D>(_swapChain, 0);
                var backBufferDesc = _backBuffer.Description;

                var factory = _swapChain.GetParent<Factory>();
                factory.MakeWindowAssociation(outputHandle, WindowAssociationFlags.IgnoreAll);

                _imagingFactory = new ImagingFactory();

                if (!useWarp)
                {
                    try
                    {
                        videoDevice1 = _device.QueryInterface<VideoDevice1>();
                        videoContext1 = _device.ImmediateContext.QueryInterface<VideoContext1>();

                        vpcd = new VideoProcessorContentDescription()
                        {
                            Usage = VideoUsage.PlaybackNormal,
                            InputFrameFormat = VideoFrameFormat.Progressive,
                            InputFrameRate = new Rational(1, 1),
                            OutputFrameRate = new Rational(1, 1),
                            InputWidth = backBufferDesc.Width,
                            OutputWidth = backBufferDesc.Width,
                            InputHeight = backBufferDesc.Height,
                            OutputHeight = backBufferDesc.Height
                        };

                        videoDevice1.CreateVideoProcessorEnumerator(ref vpcd, out vpe);
                        videoDevice1.CreateVideoProcessor(vpe, 0, out videoProcessor);

                        vpivd = new VideoProcessorInputViewDescription()
                        {
                            FourCC = 0,
                            Dimension = VpivDimension.Texture2D,
                            Texture2D = new Texture2DVpiv()
                            {
                                MipSlice = 0,
                                ArraySlice = 0
                            }
                        };

                        vpovd = new VideoProcessorOutputViewDescription()
                        {
                            Dimension = VpovDimension.Texture2D,
                            Texture2D = new Texture2DVpov()
                            {
                                MipSlice = 0
                            }
                        };

                        videoDevice1.CreateVideoProcessorOutputView(
                            (Resource)_backBuffer,
                            vpe,
                            vpovd,
                            out vpov);

                        vpsa = new VideoProcessorStream[1];

                        _useHardwareVideoProcessor = true;
                        Console.WriteLine("Video processor initialized successfully");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Video processor not available: {ex.Message}");
                        Console.WriteLine("Will use shader-based rendering instead");
                        CleanupVideoProcessor();
                        useWarp = true;
                    }
                }
                else
                {
                    Console.WriteLine("WARP mode - using shader-based rendering");
                }

                InitializeShaderResources();

                Console.WriteLine($"DirectX initialized successfully. Back buffer: {backBufferDesc.Width}x{backBufferDesc.Height}, VideoProcessor: {_useHardwareVideoProcessor}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DirectX initialization failed: {ex.Message}");
                throw;
            }
        }

        private void CleanupVideoProcessor()
        {
            Utilities.Dispose(ref vpov);
            Utilities.Dispose(ref vpe);
            Utilities.Dispose(ref videoProcessor);
            Utilities.Dispose(ref videoContext1);
            Utilities.Dispose(ref videoDevice1);
            videoDevice1 = null;
            videoContext1 = null;
            videoProcessor = null;
            vpe = null;
            vpov = null;
            vpsa = null;
            _useHardwareVideoProcessor = false;
        }

        private void InitializeShaderResources()
        {
            try
            {
                string vertexShaderCode = @"
                    struct VS_INPUT
                    {
                        float4 pos : POSITION;
                        float2 tex : TEXCOORD0;
                    };

                    struct VS_OUTPUT
                    {
                        float4 pos : SV_POSITION;
                        float2 tex : TEXCOORD0;
                    };

                    VS_OUTPUT main(VS_INPUT input)
                    {
                        VS_OUTPUT output;
                        output.pos = input.pos;
                        output.tex = input.tex;
                        return output;
                    }";

                string pixelShaderCode = @"
                    Texture2D tex : register(t0);
                    SamplerState samplerState : register(s0);

                    struct VS_OUTPUT
                    {
                        float4 pos : SV_POSITION;
                        float2 tex : TEXCOORD0;
                    };

                    float4 main(VS_OUTPUT input) : SV_TARGET
                    {
                        return tex.Sample(samplerState, input.tex);
                    }";

                using (var vertexShaderByteCode = ShaderBytecode.Compile(vertexShaderCode, "main", "vs_4_0"))
                using (var pixelShaderByteCode = ShaderBytecode.Compile(pixelShaderCode, "main", "ps_4_0"))
                {
                    _vertexShader = new VertexShader(_device, vertexShaderByteCode);
                    _pixelShader = new PixelShader(_device, pixelShaderByteCode);

                    _inputLayout = new InputLayout(_device, vertexShaderByteCode, new[]
                    {
                        new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                        new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0)
                    });
                }

                var vertices = new[]
                {
                    -1.0f,  1.0f, 0.0f, 0.0f, 0.0f,
                     1.0f,  1.0f, 0.0f, 1.0f, 0.0f,
                    -1.0f, -1.0f, 0.0f, 0.0f, 1.0f,
                     1.0f, -1.0f, 0.0f, 1.0f, 1.0f
                };

                _vertexBuffer = SharpDX.Direct3D11.Buffer.Create(_device, BindFlags.VertexBuffer, vertices);

                var indices = new[] { 0, 1, 2, 1, 3, 2 };
                _indexBuffer = SharpDX.Direct3D11.Buffer.Create(_device, BindFlags.IndexBuffer, indices);

                _samplerState = new SamplerState(_device, new SamplerStateDescription
                {
                    Filter = Filter.MinMagMipLinear,
                    AddressU = TextureAddressMode.Clamp,
                    AddressV = TextureAddressMode.Clamp,
                    AddressW = TextureAddressMode.Clamp,
                    ComparisonFunction = Comparison.Never,
                    MinimumLod = 0,
                    MaximumLod = float.MaxValue
                });

                _shaderResourcesInitialized = true;
                Console.WriteLine("Shader resources initialized for software rendering");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize shader resources: {ex.Message}");
                _shaderResourcesInitialized = false;
            }
        }

        
        
        private void RenderWithShader(Texture2D sourceTexture)
        {
            if (!_shaderResourcesInitialized || sourceTexture == null) return;

            var context = _device.ImmediateContext;
            var desc = sourceTexture.Description;

            ComputeLetterboxRect(desc.Width, desc.Height, out int outX, out int outY, out int outW, out int outH);

            using (var srv = new ShaderResourceView(_device, sourceTexture))
            using (var rtv = new RenderTargetView(_device, _backBuffer))
            {
                // Clear the WHOLE back buffer first so the letterbox bars are black
                context.OutputMerger.SetRenderTargets(rtv);
                context.ClearRenderTargetView(rtv, new RawColor4(0, 0, 0, 1));

                // Restrict drawing to just the aspect-correct centered rect
                context.Rasterizer.SetViewport(new Viewport(outX, outY, outW, outH));

                context.InputAssembler.InputLayout = _inputLayout;
                context.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
                context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(_vertexBuffer, 20, 0));
                context.InputAssembler.SetIndexBuffer(_indexBuffer, Format.R32_UInt, 0);

                context.VertexShader.Set(_vertexShader);
                context.PixelShader.Set(_pixelShader);
                context.PixelShader.SetShaderResource(0, srv);
                context.PixelShader.SetSampler(0, _samplerState);

                context.DrawIndexed(6, 0, 0);
            }
        }
        
        
        
        
        private const int TopOffsetPx = 30;

        private void ComputeLetterboxRect(int sourceWidth, int sourceHeight,
            out int outX, out int outY,
            out int outW, out int outH)
        {
            int bbW = _backBuffer.Description.Width;
            int bbH = _backBuffer.Description.Height;

            if (sourceWidth <= 0 || sourceHeight <= 0 || bbW <= 0 || bbH <= 0)
            {
                outX = outY = 0;
                outW = bbW;
                outH = bbH;
                return;
            }

            // Reserve the top strip. Clamp so we never get negative space.
            int top = Math.Min(TopOffsetPx, bbH - 1);
            if (top < 0) top = 0;

            // Available area BELOW the titlebar strip.
            int availW = bbW;
            int availH = bbH - top;
            if (availH < 1) availH = 1;

            float srcAspect = (float)sourceWidth / sourceHeight;

            // Fit the source into the available area, preserving aspect.
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

            // Center horizontally in the full width.
            outX = (bbW - outW) / 2;

            // Center vertically in the available area, then shift down by `top`.
            outY = top + (availH - outH) / 2;

            // Safety clamps.
            if (outX < 0) outX = 0;
            if (outY < 0) outY = 0;
            if (outX + outW > bbW) outW = bbW - outX;
            if (outY + outH > bbH) outH = bbH - outY;
            if (outW < 1) outW = 1;
            if (outH < 1) outH = 1;
        }
        
        
        
        
        // private void ComputeLetterboxRect(int sourceWidth, int sourceHeight, out int outX, out int outY, out int outW, out int outH)
        // {
        //     int bbW = _backBuffer.Description.Width;
        //     int bbH = _backBuffer.Description.Height;
        //
        //     float srcAspect = (float)sourceWidth / sourceHeight;
        //
        //     if ((float)bbW / bbH > srcAspect)
        //     {
        //         outH = bbH;
        //         outW = (int)Math.Round(bbH * srcAspect);
        //     }
        //     else
        //     {
        //         outW = bbW;
        //         outH = (int)Math.Round(bbW / srcAspect);
        //     }
        //
        //     if (outW < 1) outW = 1;
        //     if (outH < 1) outH = 1;
        //
        //     outX = (bbW - outW) / 2;
        //     outY = (bbH - outH) / 2;
        //
        //     if (outX < 0) outX = 0;
        //     if (outY < 0) outY = 0;
        //     if (outX + outW > bbW) outW = bbW - outX;
        //     if (outY + outH > bbH) outH = bbH - outY;
        //     if (outW < 1) outW = 1;
        //     if (outH < 1) outH = 1;
        // }
        //
        
        
        private Texture2D _staticImageTexture;
        private string _currentImagePath;

        public void DisplayImage(string fileName)
        {
            try
            {
                Console.WriteLine(Directory.GetCurrentDirectory());

                string imagePath = Path.IsPathRooted(fileName)
                    ? fileName
                    : Path.Combine(Directory.GetCurrentDirectory(), fileName);

                Console.WriteLine($"Loading image from: {imagePath}");

                if (!File.Exists(imagePath))
                {
                    Console.WriteLine($"Image file not found: {imagePath}");
                    return;
                }

                Utilities.Dispose(ref _staticImageTexture);

                _staticImageTexture = LoadTextureFromFile(imagePath);
                _currentImagePath = imagePath;

                if (_staticImageTexture != null)
                {
                    Console.WriteLine($"Successfully loaded image: {fileName}");
                    PresentStaticImage();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error displaying image {fileName}: {ex.Message}");
            }
        }
        
        
        
        public void PresentStaticImage()
{
    if (_staticImageTexture == null || _backBuffer == null)
        return;

    try
    {
        var context = _device.ImmediateContext;

        // Clear the back buffer.
        using (var rtv = new RenderTargetView(_device, _backBuffer))
        {
            context.ClearRenderTargetView(
                rtv,
                new RawColor4(0, 0, 0, 1));
        }

        if (_useHardwareVideoProcessor &&
            videoDevice1 != null &&
            videoProcessor != null &&
            vpov != null &&
            vpe != null)
        {
            // Create the input view for the current image.
            // This is the same source texture every time.
            Utilities.Dispose(ref vpiv);

            videoDevice1.CreateVideoProcessorInputView(
                _staticImageTexture,
                vpe,
                vpivd,
                out vpiv);

            VideoProcessorStream vps = new VideoProcessorStream
            {
                PInputSurface = vpiv,
                Enable = new RawBool(true)
            };

            vpsa[0] = vps;

            // IMPORTANT:
            // Only the destination rectangle changes when the
            // window is resized.
            SetVideoProcessorRects(
                _staticImageTexture.Description.Width,
                _staticImageTexture.Description.Height);

            videoContext1.VideoProcessorBlt(
                videoProcessor,
                vpov,
                0,
                1,
                vpsa);
        }
        else
        {
            RenderWithShader(_staticImageTexture);
        }

        // Keep the existing synchronization for now.
        // We are isolating the actual problem before changing
        // Present behavior.
        _swapChain.Present(1, PresentFlags.None);
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            $"PresentStaticImage error: {ex.Message}");
    }
    finally
    {
        Utilities.Dispose(ref vpiv);
    }
}
        
        
        
        public void HandleResize()
        {
            if (!string.IsNullOrEmpty(_currentImagePath) && _staticImageTexture != null)
            {
                PresentStaticImage();
            }
        }

        public Texture2D LoadTextureFromFile(string filePath)
        {
            try
            {
                using (var bitmapDecoder = new BitmapDecoder(_imagingFactory, filePath, DecodeOptions.CacheOnLoad))
                {
                    var frame = bitmapDecoder.GetFrame(0);

                    using (var formatConverter = new FormatConverter(_imagingFactory))
                    {
                        formatConverter.Initialize(frame, PixelFormat.Format32bppRGBA);

                        var width = formatConverter.Size.Width;
                        var height = formatConverter.Size.Height;

                        Console.WriteLine($"{width} x{height}");

                        if (My_Store.Instance.VideoHeight == 0 || My_Store.Instance.VideoHeight == 0)
                        {
                            My_Store.Instance.SetVideoResolution((int)width, (int)height);
                        }

                        if (My_Store.Instance?.DeviceHeight == 0 || My_Store.Instance?.DeviceWidth == 0 && my_info.Instance.DeveloperMode)
                        {
                            My_Store.Instance.SetDeviceResolution((int)width, (int)height);
                        }

                        var stride = width * 4;
                        var dataStream = new DataStream(height * stride, true, true);
                        formatConverter.CopyPixels(stride, dataStream);

                        var textureDesc = new Texture2DDescription()
                        {
                            Width = width,
                            Height = height,
                            ArraySize = 1,
                            BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                            Usage = ResourceUsage.Default,
                            CpuAccessFlags = CpuAccessFlags.None,
                            Format = Format.R8G8B8A8_UNorm,
                            MipLevels = 1,
                            OptionFlags = ResourceOptionFlags.None,
                            SampleDescription = new SampleDescription(1, 0)
                        };

                        var texture = new Texture2D(_device, textureDesc, new DataRectangle(dataStream.DataPointer, stride));

                        return texture;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading texture from file: {ex.Message}");
                return null;
            }
        }

        public void PresentFrame(Texture2D textureHW)
        {
            lock (_renderLock)
            {
                // if (_isresizing) return;
                if (_isresizing)
                {
                    PresentFrameKeepAlive();
                    return;
                }

                try
                {
                    if (_useHardwareVideoProcessor && videoDevice1 != null && videoProcessor != null && vpov != null)
                    {
                        videoDevice1.CreateVideoProcessorInputView(textureHW, vpe, vpivd, out vpiv);

                        vpsa[0] = new VideoProcessorStream
                        {
                            PInputSurface = vpiv,
                            Enable = new RawBool(true)
                        };

                        SetVideoProcessorRects(
                            textureHW.Description.Width,
                            textureHW.Description.Height);

                        videoContext1.VideoProcessorBlt(videoProcessor, vpov, 0, 1, vpsa);
                    }
                    else if (_shaderResourcesInitialized)
                    {
                        RenderWithShader(textureHW);
                    }
                    else
                    {
                        Console.WriteLine("No rendering method available");
                        return;
                    }

                    _swapChain.Present(1, PresentFlags.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PresentFrame error: {ex.Message}");
                }
                finally
                {
                    Utilities.Dispose(ref vpiv);
                }
            }
        }
        
        public void PresentFrameKeepAlive()
        {
            if (_swapChain == null) return;
            try
            {
                var context = _device.ImmediateContext;
                using (var rtv = new RenderTargetView(_device, _backBuffer))
                {
                    context.ClearRenderTargetView(rtv, new RawColor4(0, 0, 0, 1));
                }
                _swapChain.Present(1, PresentFlags.None);
            }
            catch { }
        }

     
        
        
        
        private void SetVideoProcessorRects(int sourceWidth, int sourceHeight)
        {
            if (!_useHardwareVideoProcessor || videoContext1 == null || videoProcessor == null)
                return;
            if (sourceWidth <= 0 || sourceHeight <= 0)
                return;

            int bbW = _backBuffer.Description.Width;
            int bbH = _backBuffer.Description.Height;
            if (bbW <= 0 || bbH <= 0) return;

            ComputeLetterboxRect(sourceWidth, sourceHeight, out int outX, out int outY, out int outW, out int outH);

            videoContext1.VideoProcessorSetOutputTargetRect(videoProcessor, true, new RawRectangle(0, 0, bbW, bbH));
            videoContext1.VideoProcessorSetStreamSourceRect(videoProcessor, 0, true, new RawRectangle(0, 0, sourceWidth, sourceHeight));
            videoContext1.VideoProcessorSetStreamDestRect(videoProcessor, 0, true, new RawRectangle(outX, outY, outX + outW, outY + outH));
        }
        
        
        
        
        
//         private void SetVideoProcessorRects(int sourceWidth, int sourceHeight)
// {
//     if (!_useHardwareVideoProcessor || videoContext1 == null || videoProcessor == null)
//         return;
//     if (sourceWidth <= 0 || sourceHeight <= 0)
//         return;
//
//     int bbW = _backBuffer.Description.Width;
//     int bbH = _backBuffer.Description.Height;
//     if (bbW <= 0 || bbH <= 0) return;
//
//     float srcAspect = (float)sourceWidth / sourceHeight;
//
//     int outW, outH;
//     if ((float)bbW / bbH > srcAspect)
//     {
//         outH = bbH ;
//         // outW = (int)(bbH * srcAspect);
//         outW = (int)Math.Round(bbH * srcAspect);
//     }
//     else
//     {
//         outW = bbW;
//         // outH = (int)(bbW / srcAspect);
//         outH = (int)Math.Round(bbW / srcAspect) ;
//     }
//
//     if (outW < 1) outW = 1;
//     if (outH < 1) outH = 1;
//
//     int outX = (bbW - outW) / 2;
//     int outY = (bbH - outH) / 2;
//
//     // Clamp safely.
//     if (outX < 0) outX = 0;
//     if (outY < 0) outY = 0;
//     if (outX + outW > bbW) outW = bbW - outX;
//     if (outY + outH > bbH) outH = bbH - outY;
//     if (outW < 1) outW = 1;
//     if (outH < 1) outH = 1;
//
//     var outputRect = new RawRectangle(outX, outY, outX + outW, outY + outH);
//     // var outputRect = new RawRectangle(0, 0, outW,  outH);
//
//
//     // Console.WriteLine(
//     //     $"outputRect: L={outputRect.Left} T={outputRect.Top} " +
//     //     $"R={outputRect.Right} B={outputRect.Bottom} " +
//     //     $"({outputRect.Right - outputRect.Left}x{outputRect.Bottom - outputRect.Top})");
//
//     // videoContext1.VideoProcessorSetOutputTargetRect(
//     //     videoProcessor, true,
//     //     new RawRectangle(outputRect.Left, outputRect.Top,
//     //                      outputRect.Right, outputRect.Bottom));
//     //
//     // videoContext1.VideoProcessorSetStreamSourceRect(
//     //     videoProcessor, 0, true,
//     //     new RawRectangle(0, 0, sourceWidth, sourceHeight));
//     //
//     // videoContext1.VideoProcessorSetStreamDestRect(
//     //     videoProcessor, 0, true,
//     //     new RawRectangle(0, 0, outW, outH));
//     
//     
//     
//     videoContext1.VideoProcessorSetOutputTargetRect(
//         videoProcessor, true,
//         new RawRectangle(0, 0, bbW, bbH));
//
// // Source rect = whole frame.
//     videoContext1.VideoProcessorSetStreamSourceRect(
//         videoProcessor, 0, true,
//         new RawRectangle(0, 0, sourceWidth, sourceHeight));
//
// // Dest rect = the centered, aspect-correct box, in BACK BUFFER coords.
//     videoContext1.VideoProcessorSetStreamDestRect(
//         videoProcessor, 0, true,
//         new RawRectangle(outX, outY, outX + outW, outY + outH ));
//
//     Console.WriteLine($"directx scaled frame {outW} x {outH}");
//     
//     
//
//     if (!_lastLogValid ||
//         _lastLogDest.Left   != outputRect.Left  ||
//         _lastLogDest.Top    != outputRect.Top   ||
//         _lastLogDest.Right  != outputRect.Right ||
//         _lastLogDest.Bottom != outputRect.Bottom)
//     {
//         _lastLogDest = outputRect;
//         _lastLogValid = true;
//         // Console.WriteLine(
//         //     $"[rects] video={sourceWidth}x{sourceHeight} bb={bbW}x{bbH} " +
//         //     $"output=({outputRect.Left},{outputRect.Top})-({outputRect.Right},{outputRect.Bottom}) " +
//         //     $"= {outW}x{outH}");
//     }
// }
//         
//         
        
        
        
        public void RunOnContext(Action<DeviceContext> action)
        {
            lock (_renderLock)
            {
                action(_device.ImmediateContext);
            }
        }
        
        public void ResizeToClient(IntPtr hwnd)
        {
            if (GetClientRect(hwnd, out RECT r))
            {
                
                ResizeSwapChain(r.Right - r.Left, r.Bottom - r.Top);

                // Console.WriteLine($"ResizeSwapChain done {r.Right - r.Left} x { r.Bottom - r.Top}");
            }
            
        }
        
        
        
        
        // window size 583 x 207 
        // swapchain size 583 x 207 
        // outputRect: L=107 T=0 R=475 B=207 (368x207)

        // 368 × 207
        
        public void ResizeSwapChain(int width, int height)
        {
            lock (_renderLock)
            {
                _isresizing = true;

                try
                {
                    width = Math.Max(width, 1);
                    height = Math.Max(height, 1);

                    if (_backBuffer != null &&
                        _backBuffer.Description.Width == width &&
                        _backBuffer.Description.Height == height)
                    {
                        return;
                    }

                    var context = _device.ImmediateContext;
                    context.ClearState();
                    context.OutputMerger.SetRenderTargets((RenderTargetView)null);
                    context.Flush();

                    Utilities.Dispose(ref vpov);
                    Utilities.Dispose(ref _backBuffer);

                    _swapChain.ResizeBuffers(
                        2,
                        width,
                        height,
                        Format.B8G8R8A8_UNorm,
                        SwapChainFlags.None);

                    _backBuffer = Texture2D.FromSwapChain<Texture2D>(_swapChain, 0);

                    if (_useHardwareVideoProcessor && videoDevice1 != null)
                    {
                        Utilities.Dispose(ref vpe);
                        Utilities.Dispose(ref videoProcessor);

                        // vpcd.InputWidth = width;
                        // vpcd.OutputWidth = width;
                        // vpcd.InputHeight = height;
                        // vpcd.OutputHeight = height;
                        
                        
//                         // Only the OUTPUT (back buffer) size changes on resize.
// // Input dimensions describe the source video and must stay untouched.
                         vpcd.OutputWidth = width;
                         vpcd.OutputHeight = height;

                        videoDevice1.CreateVideoProcessorEnumerator(ref vpcd, out vpe);
                        videoDevice1.CreateVideoProcessor(vpe, 0, out videoProcessor);

                        videoDevice1.CreateVideoProcessorOutputView(_backBuffer, vpe, vpovd, out vpov);
                    }

                    // Force rects to be re-applied on the next present.
                    _lastLogValid = false;
                }
                finally
                {
                    _isresizing = false;

                    // Console.WriteLine($"window size {width} x {height} ");
                    // Console.WriteLine($"swapchain size {_backBuffer.Description.Width} x {_backBuffer.Description.Height} ");
                }
            }
        }

        public void Dispose()
        {
            Utilities.Dispose(ref _samplerState);
            Utilities.Dispose(ref _indexBuffer);
            Utilities.Dispose(ref _vertexBuffer);
            Utilities.Dispose(ref _inputLayout);
            Utilities.Dispose(ref _pixelShader);
            Utilities.Dispose(ref _vertexShader);
            Utilities.Dispose(ref _staticImageTexture);
            Utilities.Dispose(ref vpov);
            Utilities.Dispose(ref videoProcessor);
            Utilities.Dispose(ref vpe);
            Utilities.Dispose(ref videoContext1);
            Utilities.Dispose(ref videoDevice1);
            Utilities.Dispose(ref _backBuffer);
            Utilities.Dispose(ref _swapChain);
            Utilities.Dispose(ref _device);
            _imagingFactory?.Dispose();
        }
    }
}