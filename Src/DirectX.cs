using System;
using System.IO;
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
        public DirectX(IntPtr outputHandle) { Initialize(outputHandle); }
        #endregion

        private void Initialize(IntPtr outputHandle)
        {
            try
            {
                // Get the window size first
                int windowWidth = 640;
                int windowHeight = 480;
                
                try
                {
                    var window = System.Windows.Interop.HwndSource.FromHwnd(outputHandle);
                    if (window != null && window.RootVisual != null)
                    {
                        // Cast to FrameworkElement to get ActualWidth/ActualHeight
                        if (window.RootVisual is System.Windows.FrameworkElement fe)
                        {
                            windowWidth = Math.Max((int)fe.ActualWidth, 1);
                            windowHeight = Math.Max((int)fe.ActualHeight, 1);
                        }
                        else
                        {
                            // Fallback: try to get the window size from the parent
                            var parent = System.Windows.Media.VisualTreeHelper.GetParent(window.RootVisual);
                            if (parent is System.Windows.FrameworkElement parentFe)
                            {
                                windowWidth = Math.Max((int)parentFe.ActualWidth, 1);
                                windowHeight = Math.Max((int)parentFe.ActualHeight, 1);
                            }
                        }
                    }
                }
                catch { }

                Console.WriteLine($"Creating swap chain with size: {windowWidth}x{windowHeight}");

                // SwapChain Description
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

                // Try Hardware first, fallback to WARP software renderer
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

                // Creates Association between outputHandle and BackBuffer
                var factory = _swapChain.GetParent<Factory>();
                factory.MakeWindowAssociation(outputHandle, WindowAssociationFlags.IgnoreAll);

                _imagingFactory = new ImagingFactory();
                
                // Try to initialize video processing (may fail on WARP)
                if (!useWarp)
                {
                    try
                    {
                        // Video Device | Video Context
                        videoDevice1 = _device.QueryInterface<VideoDevice1>();
                        videoContext1 = _device.ImmediateContext.QueryInterface<VideoContext1>();

                        // Create Video Processor Enumerator
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
                        
                        // Video Processor Input View Description
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

                        // Video Processor Output View Description
                        vpovd = new VideoProcessorOutputViewDescription() 
                        { 
                            Dimension = VpovDimension.Texture2D,
                            Texture2D = new Texture2DVpov()
                            {
                                MipSlice = 0
                            }
                        };

                        // Create output view
                        videoDevice1.CreateVideoProcessorOutputView(
                            (Resource)_backBuffer, 
                            vpe, 
                            vpovd, 
                            out vpov);

                        // Prepares Streams Array
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

                // Always initialize shader resources for fallback
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
                // Simple vertex shader for fullscreen quad
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

                // Simple pixel shader with texture sampling
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

                // Compile shaders
                using (var vertexShaderByteCode = ShaderBytecode.Compile(vertexShaderCode, "main", "vs_4_0"))
                using (var pixelShaderByteCode = ShaderBytecode.Compile(pixelShaderCode, "main", "ps_4_0"))
                {
                    _vertexShader = new VertexShader(_device, vertexShaderByteCode);
                    _pixelShader = new PixelShader(_device, pixelShaderByteCode);

                    // Input layout
                    _inputLayout = new InputLayout(_device, vertexShaderByteCode, new[]
                    {
                        new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                        new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0)
                    });
                }

                // Create vertex buffer for fullscreen quad
                var vertices = new[]
                {
                    // Position (x,y,z), TexCoord (u,v)
                    -1.0f,  1.0f, 0.0f, 0.0f, 0.0f,  // Top-left
                     1.0f,  1.0f, 0.0f, 1.0f, 0.0f,  // Top-right
                    -1.0f, -1.0f, 0.0f, 0.0f, 1.0f,  // Bottom-left
                     1.0f, -1.0f, 0.0f, 1.0f, 1.0f   // Bottom-right
                };

                _vertexBuffer = SharpDX.Direct3D11.Buffer.Create(_device, BindFlags.VertexBuffer, vertices);
                
                // Create index buffer
                var indices = new[] { 0, 1, 2, 1, 3, 2 };
                _indexBuffer = SharpDX.Direct3D11.Buffer.Create(_device, BindFlags.IndexBuffer, indices);

                // Create sampler state
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

            // Clear back buffer to black
            using (var rtv = new RenderTargetView(_device, _backBuffer))
            {
                context.ClearRenderTargetView(rtv, new RawColor4(0, 0, 0, 1));
            }

            // Set up rendering pipeline
            using (var srv = new ShaderResourceView(_device, sourceTexture))
            using (var rtv = new RenderTargetView(_device, _backBuffer))
            {
                context.OutputMerger.SetRenderTargets(rtv);
                context.Rasterizer.SetViewport(new Viewport(0, 0, _backBuffer.Description.Width, _backBuffer.Description.Height));
                
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

        private Texture2D _staticImageTexture;
        private string _currentImagePath;

        public void DisplayImage(string fileName)
        {
            try
            {
                string imagePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
                Console.WriteLine($"Loading image from: {imagePath}");

                if (!File.Exists(imagePath))
                {
                    Console.WriteLine($"Image file not found: {imagePath}");
                    return;
                }

                // Dispose previous static image
                Utilities.Dispose(ref _staticImageTexture);

                // Load new static image
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
            if (_staticImageTexture == null) return;
    
            try
            {
                if (_useHardwareVideoProcessor && videoDevice1 != null && videoProcessor != null && vpov != null)
                {
                    // Use video processor (hardware path)
                    videoDevice1.CreateVideoProcessorInputView(_staticImageTexture, vpe, vpivd, out vpiv);
                    VideoProcessorStream vps = new VideoProcessorStream()
                    {
                        PInputSurface = vpiv,
                        Enable = new SharpDX.Mathematics.Interop.RawBool(true)
                    };
                    vpsa[0] = vps;

                    videoContext1.VideoProcessorBlt(videoProcessor, vpov, 0, 1, vpsa);
                }
                else
                {
                    // Use shader-based rendering (software fallback)
                    RenderWithShader(_staticImageTexture);
                }
                
                _swapChain.Present(0, PresentFlags.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PresentStaticImage error: {ex.Message}");
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
                // Re-present the image after resize
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
                    
                    // Convert to RGBA format
                    using (var formatConverter = new FormatConverter(_imagingFactory))
                    {
                        formatConverter.Initialize(frame, PixelFormat.Format32bppRGBA);
                        
                        var width = formatConverter.Size.Width;
                        var height = formatConverter.Size.Height;

                        Console.WriteLine($"{width} x{height}");
                        
                        if (My_Store.Instance.VideoHeight == 0 || My_Store.Instance.VideoHeight == 0)
                        {
                            My_Store.Instance.SetVideoResolution((int)width,(int)height);
                        }
                        
                        if (My_Store.Instance?.DeviceHeight == 0 || My_Store.Instance?.DeviceWidth == 0 && my_info.Instance.DeveloperMode)
                        {
                            My_Store.Instance.SetDeviceResolution((int)width,(int)height);
                        }

                        // Copy image data
                        var stride = width * 4;
                        var dataStream = new DataStream(height * stride, true, true);
                        formatConverter.CopyPixels(stride, dataStream);
                        
                        // Create texture description
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
                        
                        // Create texture
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
                if (_isresizing) return;
        
                try
                {
                    if (_useHardwareVideoProcessor && videoDevice1 != null && videoProcessor != null && vpov != null)
                    {
                        // Use video processor (hardware path)
                        videoDevice1.CreateVideoProcessorInputView(textureHW, vpe, vpivd, out vpiv);
                        
                        vpsa[0] = new VideoProcessorStream
                        {
                            PInputSurface = vpiv,
                            Enable = new RawBool(true)
                        };
        
                        videoContext1.VideoProcessorBlt(videoProcessor, vpov, 0, 1, vpsa);
                    }
                    else if (_shaderResourcesInitialized)
                    {
                        // Use shader-based rendering (software fallback)
                        RenderWithShader(textureHW);
                    }
                    else
                    {
                        Console.WriteLine("No rendering method available");
                        return;
                    }
        
                    _swapChain.Present(0, PresentFlags.None);
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

        private void UpdateVideoProcessor(int width, int height)
        {
            try
            {
                if (!_useHardwareVideoProcessor || videoDevice1 == null) return;

                // Dispose old resources
                Utilities.Dispose(ref vpov);
                Utilities.Dispose(ref videoProcessor);
                Utilities.Dispose(ref vpe);

                // Update dimensions
                vpcd.InputWidth = width;
                vpcd.OutputWidth = width;
                vpcd.InputHeight = height;
                vpcd.OutputHeight = height;

                // Recreate video processor
                videoDevice1.CreateVideoProcessorEnumerator(ref vpcd, out vpe);
                videoDevice1.CreateVideoProcessor(vpe, 0, out videoProcessor);
                
                // Recreate output view
                videoDevice1.CreateVideoProcessorOutputView(_backBuffer, vpe, vpovd, out vpov);

                Console.WriteLine($"Video processor updated to: {width}x{height}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateVideoProcessor failed: {ex.Message}");
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
                _swapChain.Present(0, PresentFlags.None);
            }
            catch { }
        }

        public void ResizeSwapChain(int width, int height)
        {
            lock (_renderLock)
            {
                _isresizing = true;

                try
                {
                    width = Math.Max(width, 1);
                    height = Math.Max(height, 1);

                    // Check if we need to resize
                    if (_backBuffer != null && 
                        _backBuffer.Description.Width == width && 
                        _backBuffer.Description.Height == height)
                    {
                        // Console.WriteLine($"Already at {width}x{height}, skipping resize");
                        return;
                    }

                    // Unbind all resources before resize
                    var context = _device.ImmediateContext;
                    context.ClearState();
                    context.OutputMerger.SetRenderTargets((RenderTargetView)null);
                    context.Flush();

                    // Dispose resources
                    Utilities.Dispose(ref vpov);
                    Utilities.Dispose(ref _backBuffer);

                    _swapChain.ResizeBuffers(
                        2,
                        width,
                        height,
                        Format.B8G8R8A8_UNorm,
                        SwapChainFlags.None);

                    _backBuffer = Texture2D.FromSwapChain<Texture2D>(_swapChain, 0);

                    // Recreate video processor if available
                    if (_useHardwareVideoProcessor && videoDevice1 != null)
                    {
                        Utilities.Dispose(ref vpe);
                        Utilities.Dispose(ref videoProcessor);

                        vpcd.InputWidth = width;
                        vpcd.OutputWidth = width;
                        vpcd.InputHeight = height;
                        vpcd.OutputHeight = height;

                        videoDevice1.CreateVideoProcessorEnumerator(ref vpcd, out vpe);
                        videoDevice1.CreateVideoProcessor(vpe, 0, out videoProcessor);

                        videoDevice1.CreateVideoProcessorOutputView(_backBuffer, vpe, vpovd, out vpov);
                    }
                }
                finally
                {
                    _isresizing = false;
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