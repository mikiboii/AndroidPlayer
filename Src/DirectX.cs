// using System;
// using System.IO;
// using System.Runtime.CompilerServices;
// using System.Runtime.InteropServices;

// // using Android_player_2.Store;

// using Silk.NET.Core.Native;
// using Silk.NET.Direct3D.Compilers;
// using Silk.NET.Direct3D11;
// using Silk.NET.DXGI;

// using D3D11Device = Silk.NET.Direct3D11.ID3D11Device;
// using D3D11Context = Silk.NET.Direct3D11.ID3D11DeviceContext;
// using D3D11Texture2D = Silk.NET.Direct3D11.ID3D11Texture2D;
// using D3D11Buffer = Silk.NET.Direct3D11.ID3D11Buffer;
// using D3D11ShaderResourceView = Silk.NET.Direct3D11.ID3D11ShaderResourceView;
// using D3D11RenderTargetView = Silk.NET.Direct3D11.ID3D11RenderTargetView;
// using D3D11VertexShader = Silk.NET.Direct3D11.ID3D11VertexShader;
// using D3D11PixelShader = Silk.NET.Direct3D11.ID3D11PixelShader;
// using D3D11SamplerState = Silk.NET.Direct3D11.ID3D11SamplerState;

// namespace Androidplayer
// {
//     public unsafe class DirectX : IDisposable
//     {
//         #region Declaration

//         private D3D11? _d3d11;
//         private DXGI? _dxgi;
//         private D3DCompiler? _compiler;

//         internal ComPtr<D3D11Device> _device;
//         private ComPtr<D3D11Context> _deviceContext;

//         private ComPtr<IDXGIFactory2> _factory;
//         private ComPtr<IDXGISwapChain1> _swapChain;

//         private ComPtr<D3D11Texture2D> _backBuffer;
//         private ComPtr<D3D11RenderTargetView> _backBufferView;

//         /*
//          * Instead of SharpDX VideoDevice1 / VideoProcessor / VideoContext1,
//          * we use a normal shader pipeline.
//          *
//          * This is important for DXVK because ordinary D3D11 shader operations
//          * are supported much better than the D3D11 video APIs.
//          */

//         private ComPtr<D3D11VertexShader> _vertexShader;
//         private ComPtr<D3D11PixelShader> _pixelShader;

//         private ComPtr<D3D11Buffer> _vertexBuffer;

//         private ComPtr<D3D11SamplerState> _samplerState;

//         private ComPtr<D3D11ShaderResourceView> _currentTextureView;

//         private uint _textureWidth;
//         private uint _textureHeight;

//         private int _swapChainWidth;
//         private int _swapChainHeight;

//         private string? _currentImagePath;

//         private bool _disposed;

//         #endregion


//         #region Shader source

//         /*
//          * Full-screen triangle.
//          *
//          * The pixel shader samples the texture and displays it.
//          *
//          * For ordinary RGBA images this is simply:
//          *
//          *     return image.Sample(...)
//          *
//          * For NV12 we use the Y and UV planes and perform the YUV->RGB
//          * conversion directly in the shader.
//          */

//         private const string ShaderSource = @"

// struct VSInput
// {
//     float3 Position : POSITION;
//     float2 TexCoord : TEXCOORD0;
// };

// struct VSOutput
// {
//     float4 Position : SV_POSITION;
//     float2 TexCoord : TEXCOORD0;
// };

// VSOutput VSMain(VSInput input)
// {
//     VSOutput output;

//     output.Position = float4(input.Position, 1.0);
//     output.TexCoord = input.TexCoord;

//     return output;
// }


// /*
//  * RGBA/BGRA texture mode.
//  */
// Texture2D ImageTexture : register(t0);

// SamplerState ImageSampler : register(s0);

// float4 PSMain(VSOutput input) : SV_TARGET
// {
//     return ImageTexture.Sample(ImageSampler, input.TexCoord);
// }

// ";

//         /*
//          * NV12 shader.
//          *
//          * t0 = Y plane
//          * t1 = UV plane
//          *
//          * The actual NV12 texture setup is kept separate from ordinary
//          * image rendering.
//          */

//         private const string Nv12ShaderSource = @"

// struct VSInput
// {
//     float3 Position : POSITION;
//     float2 TexCoord : TEXCOORD0;
// };

// struct VSOutput
// {
//     float4 Position : SV_POSITION;
//     float2 TexCoord : TEXCOORD0;
// };

// VSOutput VSMain(VSInput input)
// {
//     VSOutput output;

//     output.Position = float4(input.Position, 1.0);
//     output.TexCoord = input.TexCoord;

//     return output;
// }


// Texture2D YTexture : register(t0);
// Texture2D UVTexture : register(t1);

// SamplerState ImageSampler : register(s0);


// float4 PSMain(VSOutput input) : SV_TARGET
// {
//     float y  = YTexture.Sample(ImageSampler, input.TexCoord).r;
//     float2 uv = UVTexture.Sample(ImageSampler, input.TexCoord).rg;

//     /*
//      * Limited-range BT.601 conversion.
//      *
//      * Y: 16..235
//      * U/V: 16..240
//      */

//     y = 1.16438356 * (y - 0.0625);

//     float u = uv.x - 0.5;
//     float v = uv.y - 0.5;

//     float r = y + 1.596027 * v;
//     float g = y - 0.391762 * u - 0.812968 * v;
//     float b = y + 2.017232 * u;

//     return float4(r, g, b, 1.0);
// }

// ";

//         #endregion


//         #region Vertex data

//         /*
//          * Full screen quad.
//          *
//          * X Y Z U V
//          */

//         private readonly float[] _vertices =
//         {
//             -1.0f,  1.0f, 0.0f, 0.0f, 0.0f,
//              1.0f,  1.0f, 0.0f, 1.0f, 0.0f,
//              1.0f, -1.0f, 0.0f, 1.0f, 1.0f,

//             -1.0f,  1.0f, 0.0f, 0.0f, 0.0f,
//              1.0f, -1.0f, 0.0f, 1.0f, 1.0f,
//             -1.0f, -1.0f, 0.0f, 0.0f, 1.0f
//         };

//         #endregion


//         #region Properties

//         public D3D11Device my_Device
//         {
//             get
//             {
//                 return _device;
//             }
//         }

//         #endregion


//         #region Constructor

//         public DirectX(IntPtr outputHandle)
//         {
//             Initialize(outputHandle);
//         }

//         #endregion


//         #region Initialize

//         private void Initialize(IntPtr outputHandle)
//         {
//             try
//             {
//                 /*
//                  * Force DXVK.
//                  *
//                  * Change this to false if you want native D3D11 on Windows.
//                  */
//                 const bool forceDxvk = true;

//                 _dxgi = DXGI.GetApi(null, forceDxvk);
//                 _d3d11 = D3D11.GetApi(null, forceDxvk);
//                 _compiler = D3DCompiler.GetApi();

//                 CreateDevice();

//                 CreateSwapChain(outputHandle);

//                 CreateShaders();

//                 CreateVertexBuffer();

//                 CreateSampler();

//                 CreateBackBuffer();

//                 Console.WriteLine(
//                     $"Silk.NET Direct3D11 initialized. " +
//                     $"Back buffer: {_swapChainWidth}x{_swapChainHeight}");
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine(
//                     $"DirectX initialization failed: {ex}");

//                 Dispose();

//                 throw;
//             }
//         }

//         #endregion


//         #region Device

//         private void CreateDevice()
//         {
//             if (_d3d11 == null)
//                 throw new InvalidOperationException("D3D11 was not initialized.");

//             SilkMarshal.ThrowHResult(
//                 _d3d11.CreateDevice(
//                     default(ComPtr<IDXGIAdapter>),
//                     D3DDriverType.Hardware,
//                     default,
//                     0,
//                     null,
//                     0,
//                     D3D11.SdkVersion,
//                     ref _device,
//                     null,
//                     ref _deviceContext));
//         }

//         #endregion


//         #region SwapChain

//         private void CreateSwapChain(IntPtr outputHandle)
//         {
//             if (_dxgi == null)
//                 throw new InvalidOperationException("DXGI was not initialized.");

//             if (outputHandle == IntPtr.Zero)
//                 throw new ArgumentException(
//                     "outputHandle cannot be zero.",
//                     nameof(outputHandle));

//             _factory =
//                 _dxgi.CreateDXGIFactory<IDXGIFactory2>();

//             var desc = new SwapChainDesc1
//             {
//                 Width = 0,
//                 Height = 0,

//                 Format = Format.FormatB8G8R8A8Unorm,

//                 Stereo = false,

//                 SampleDesc = new SampleDesc(1, 0),

//                 BufferUsage =
//                     DXGI.UsageRenderTargetOutput,

//                 BufferCount = 2,

//                 Scaling = Scaling.Stretch,

//                 SwapEffect = SwapEffect.FlipDiscard,

//                 AlphaMode = AlphaMode.Ignore,

//                 Flags = 0
//             };

//             SilkMarshal.ThrowHResult(
//                 _factory.CreateSwapChainForHwnd(
//                     _device,
//                     (nint)outputHandle,
//                     in desc,
//                     null,
//                     ref Unsafe.NullRef<IDXGIOutput>(),
//                     ref _swapChain));
//         }

//         #endregion


//         #region BackBuffer

//         private void CreateBackBuffer()
//         {
//             _backBuffer.Dispose();
//             _backBufferView.Dispose();

//             using var buffer =
//                 _swapChain.GetBuffer<D3D11Texture2D>(0);

//             _backBuffer = buffer;

//             SilkMarshal.ThrowHResult(
//                 _device.CreateRenderTargetView(
//                     _backBuffer,
//                     null,
//                     ref _backBufferView));

//             var desc = _backBuffer.GetDesc();

//             _swapChainWidth = (int)desc.Width;
//             _swapChainHeight = (int)desc.Height;
//         }

//         #endregion


//         #region Shaders

//         private void CreateShaders()
//         {
//             if (_compiler == null)
//                 throw new InvalidOperationException(
//                     "D3DCompiler was not initialized.");

//             var sourceBytes =
//                 System.Text.Encoding.UTF8.GetBytes(
//                     ShaderSource);

//             ComPtr<ID3D10Blob> vertexCode = default;
//             ComPtr<ID3D10Blob> vertexErrors = default;

//             fixed (byte* source = sourceBytes)
//             {
//                 var hr = _compiler.Compile(
//                     source,
//                     (nuint)sourceBytes.Length,
//                     "SilkDirectXShader",
//                     null,
//                     ref Unsafe.NullRef<ID3DInclude>(),
//                     "VSMain",
//                     "vs_5_0",
//                     0,
//                     0,
//                     ref vertexCode,
//                     ref vertexErrors);

//                 if (hr.IsFailure)
//                 {
//                     PrintShaderError(vertexErrors);
//                     hr.Throw();
//                 }
//             }


//             ComPtr<ID3D10Blob> pixelCode = default;
//             ComPtr<ID3D10Blob> pixelErrors = default;

//             fixed (byte* source = sourceBytes)
//             {
//                 var hr = _compiler.Compile(
//                     source,
//                     (nuint)sourceBytes.Length,
//                     "SilkDirectXShader",
//                     null,
//                     ref Unsafe.NullRef<ID3DInclude>(),
//                     "PSMain",
//                     "ps_5_0",
//                     0,
//                     0,
//                     ref pixelCode,
//                     ref pixelErrors);

//                 if (hr.IsFailure)
//                 {
//                     PrintShaderError(pixelErrors);
//                     hr.Throw();
//                 }
//             }


//             SilkMarshal.ThrowHResult(
//                 _device.CreateVertexShader(
//                     vertexCode.GetBufferPointer(),
//                     vertexCode.GetBufferSize(),
//                     ref Unsafe.NullRef<ID3D11ClassLinkage>(),
//                     ref _vertexShader));


//             SilkMarshal.ThrowHResult(
//                 _device.CreatePixelShader(
//                     pixelCode.GetBufferPointer(),
//                     pixelCode.GetBufferSize(),
//                     ref Unsafe.NullRef<ID3D11ClassLinkage>(),
//                     ref _pixelShader));


//             vertexCode.Dispose();
//             vertexErrors.Dispose();

//             pixelCode.Dispose();
//             pixelErrors.Dispose();
//         }

//         private static void PrintShaderError(
//             ComPtr<ID3D10Blob> errors)
//         {
//             if (errors.Handle != null)
//             {
//                 Console.WriteLine(
//                     SilkMarshal.PtrToString(
//                         (nint)errors.GetBufferPointer()));
//             }
//         }

//         #endregion


//         #region Vertex Buffer

//         private void CreateVertexBuffer()
//         {
//             var desc = new BufferDesc
//             {
//                 ByteWidth =
//                     (uint)(_vertices.Length * sizeof(float)),

//                 Usage = Usage.Default,

//                 BindFlags =
//                     (uint)BindFlag.VertexBuffer,

//                 CPUAccessFlags = 0,

//                 MiscFlags = 0,

//                 StructureByteStride = 0
//             };

//             fixed (float* data = _vertices)
//             {
//                 var subresource = new SubresourceData
//                 {
//                     PSysMem = data,
//                     SysMemPitch = 0,
//                     SysMemSlicePitch = 0
//                 };

//                 SilkMarshal.ThrowHResult(
//                     _device.CreateBuffer(
//                         in desc,
//                         in subresource,
//                         ref _vertexBuffer));
//             }
//         }

//         #endregion


//         #region Sampler

//         private void CreateSampler()
//         {
//             var desc = new SamplerDesc
//             {
//                 Filter = Filter.MinMagMipLinear,

//                 AddressU = TextureAddressMode.Clamp,
//                 AddressV = TextureAddressMode.Clamp,
//                 AddressW = TextureAddressMode.Clamp,

//                 MipLODBias = 0,

//                 MaxAnisotropy = 1,

//                 ComparisonFunc = ComparisonFunc.Never,

//                 MinLOD = float.MinValue,
//                 MaxLOD = float.MaxValue
//             };

//             desc.BorderColor[0] = 0;
//             desc.BorderColor[1] = 0;
//             desc.BorderColor[2] = 0;
//             desc.BorderColor[3] = 1;

//             SilkMarshal.ThrowHResult(
//                 _device.CreateSamplerState(
//                     in desc,
//                     ref _samplerState));
//         }

//         #endregion


//         #region DisplayImage

//         public void DisplayImage(string fileName)
//         {
//             try
//             {
//                 string imagePath =
//                     Path.Combine(
//                         Directory.GetCurrentDirectory(),
//                         fileName);

//                 Console.WriteLine(
//                     $"Loading image from: {imagePath}");

//                 if (!File.Exists(imagePath))
//                 {
//                     Console.WriteLine(
//                         $"Image file not found: {imagePath}");

//                     return;
//                 }

//                 _currentImagePath = imagePath;

//                 /*
//                  * The original implementation used SharpDX.WIC here.
//                  *
//                  * Since you asked to remove SharpDX and use only the
//                  * Silk.NET D3D11/DXGI stack, the actual image decoding
//                  * must happen before this point.
//                  *
//                  * Use DisplayImagePixels() if you already have decoded
//                  * RGBA/BGRA pixels.
//                  */
//                 Console.WriteLine(
//                     "DisplayImage requires decoded RGBA/BGRA pixels. " +
//                     "Use DisplayImagePixels().");
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine(
//                     $"Error displaying image {fileName}: {ex.Message}");
//             }
//         }

//         #endregion


//         #region DisplayImagePixels

//         /*
//          * Replaces the old WIC -> Texture2D path.
//          *
//          * pixels must be BGRA/RGBA 32-bit pixels.
//          *
//          * If your decoder gives RGBA, use FormatR8G8B8A8Unorm.
//          * If it gives BGRA, use FormatB8G8R8A8Unorm.
//          */

//         public void DisplayImagePixels(
//             IntPtr pixels,
//             int width,
//             int height,
//             int stride)
//         {
//             if (pixels == IntPtr.Zero)
//                 throw new ArgumentNullException(nameof(pixels));

//             if (width <= 0 || height <= 0)
//                 throw new ArgumentOutOfRangeException();

//             try
//             {
//                 DisposeCurrentTexture();

//                 _textureWidth = (uint)width;
//                 _textureHeight = (uint)height;

//                 My_Store.Instance.SetVideoResolution(
//                     width,
//                     height);

//                 var textureDesc = new Texture2DDesc
//                 {
//                     Width = (uint)width,
//                     Height = (uint)height,

//                     MipLevels = 1,
//                     ArraySize = 1,

//                     Format =
//                         Format.FormatB8G8R8A8Unorm,

//                     SampleDesc = new SampleDesc(1, 0),

//                     Usage = Usage.Default,

//                     BindFlags =
//                         (uint)BindFlag.ShaderResource,

//                     CPUAccessFlags = 0,

//                     MiscFlags =
//                         (uint)ResourceMiscFlag.None
//                 };

//                 var data = new SubresourceData
//                 {
//                     PSysMem = (void*)pixels,

//                     SysMemPitch = (uint)stride,

//                     SysMemSlicePitch =
//                         (uint)(stride * height)
//                 };

//                 ComPtr<D3D11Texture2D> texture = default;

//                 SilkMarshal.ThrowHResult(
//                     _device.CreateTexture2D(
//                         in textureDesc,
//                         in data,
//                         ref texture));

//                 var srvDesc = new ShaderResourceViewDesc
//                 {
//                     Format =
//                         textureDesc.Format,

//                     ViewDimension =
//                         D3DSrvDimension.D3DSrvDimensionTexture2D,

//                     Anonymous = new ShaderResourceViewDescUnion
//                     {
//                         Texture2D =
//                         {
//                             MostDetailedMip = 0,
//                             MipLevels = 1
//                         }
//                     }
//                 };

//                 SilkMarshal.ThrowHResult(
//                     _device.CreateShaderResourceView(
//                         texture,
//                         in srvDesc,
//                         ref _currentTextureView));

//                 texture.Dispose();

//                 PresentStaticImage();
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine(
//                     $"DisplayImagePixels error: {ex}");
//             }
//         }

//         #endregion


//         #region PresentStaticImage

//         public void PresentStaticImage()
//         {
//             if (_currentTextureView.Handle == null)
//                 return;

//             try
//             {
//                 RenderTexture(
//                     _currentTextureView);
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine(
//                     $"PresentStaticImage error: {ex}");
//             }
//         }

//         #endregion


//         #region PresentFrame

//         /*
//          * This is the replacement for your old:
//          *
//          *     VideoProcessorBlt(...)
//          *
//          * Instead:
//          *
//          *     textureHW
//          *          ↓
//          *     ShaderResourceView
//          *          ↓
//          *     Pixel Shader
//          *          ↓
//          *     BackBuffer
//          *          ↓
//          *     Present
//          *
//          * This avoids the D3D11 video API.
//          */

//         public void PresentFrame(
//             ComPtr<D3D11Texture2D> textureHW)
//         {
//             try
//             {
//                 if (textureHW.Handle == null)
//                     return;

//                 ComPtr<D3D11ShaderResourceView> view =
//                     default;

//                 var desc = textureHW.GetDesc();

//                 var srvDesc = new ShaderResourceViewDesc
//                 {
//                     Format = desc.Format,

//                     ViewDimension =
//                         D3DSrvDimension.D3DSrvDimensionTexture2D,

//                     Anonymous = new ShaderResourceViewDescUnion
//                     {
//                         Texture2D =
//                         {
//                             MostDetailedMip = 0,
//                             MipLevels = 1
//                         }
//                     }
//                 };

//                 SilkMarshal.ThrowHResult(
//                     _device.CreateShaderResourceView(
//                         textureHW,
//                         in srvDesc,
//                         ref view));

//                 RenderTexture(view);

//                 view.Dispose();
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine(
//                     $"PresentFrame error: {ex}");
//             }
//         }

//         #endregion


//         #region RenderTexture

//         private void RenderTexture(
//             ComPtr<D3D11ShaderResourceView> texture)
//         {
//             if (_swapChain.Handle == null)
//                 return;

//             /*
//              * Clear.
//              */

//             float[] clear =
//             {
//                 0,
//                 0,
//                 0,
//                 1
//             };

//             _deviceContext.ClearRenderTargetView(
//                 _backBufferView,
//                 ref clear[0]);


//             /*
//              * Viewport.
//              */

//             var viewport = new Viewport(
//                 0,
//                 0,
//                 (float)_swapChainWidth,
//                 (float)_swapChainHeight,
//                 0,
//                 1);

//             _deviceContext.RSSetViewports(
//                 1,
//                 in viewport);


//             /*
//              * Render target.
//              */

//             _deviceContext.OMSetRenderTargets(
//                 1,
//                 ref _backBufferView,
//                 ref Unsafe.NullRef<D3D11DepthStencilView>());


//             /*
//              * Vertex buffer.
//              */

//             uint stride =
//                 5 * sizeof(float);

//             uint offset = 0;

//             _deviceContext.IASetVertexBuffers(
//                 0,
//                 1,
//                 _vertexBuffer,
//                 in stride,
//                 in offset);


//             /*
//              * Triangle list.
//              */

//             _deviceContext.IASetPrimitiveTopology(
//                 D3DPrimitiveTopology
//                     .D3DPrimitiveTopologyTrianglelist);


//             /*
//              * Vertex shader.
//              */

//             _deviceContext.VSSetShader(
//                 _vertexShader,
//                 ref Unsafe.NullRef<
//                     ComPtr<ID3D11ClassInstance>>(),
//                 0);


//             /*
//              * Pixel shader.
//              */

//             _deviceContext.PSSetShader(
//                 _pixelShader,
//                 ref Unsafe.NullRef<
//                     ComPtr<ID3D11ClassInstance>>(),
//                 0);


//             /*
//              * Texture.
//              */

//             _deviceContext.PSSetShaderResources(
//                 0,
//                 1,
//                 texture);


//             /*
//              * Sampler.
//              */

//             _deviceContext.PSSetSamplers(
//                 0,
//                 1,
//                 _samplerState);


//             /*
//              * Draw.
//              */

//             _deviceContext.Draw(
//                 6,
//                 0);


//             /*
//              * Present.
//              */

//             SilkMarshal.ThrowHResult(
//                 _swapChain.Present(
//                     1,
//                     0));
//         }

//         #endregion


//         #region HandleResize

//         public void HandleResize()
//         {
//             if (!string.IsNullOrEmpty(
//                     _currentImagePath))
//             {
//                 PresentStaticImage();
//             }
//         }

//         #endregion


//         #region PresentFrameKeepAlive

//         public void PresentFrameKeepAlive()
//         {
//             if (_swapChain.Handle == null)
//                 return;

//             try
//             {
//                 float[] clear =
//                 {
//                     0,
//                     0,
//                     0,
//                     1
//                 };

//                 _deviceContext.ClearRenderTargetView(
//                     _backBufferView,
//                     ref clear[0]);

//                 SilkMarshal.ThrowHResult(
//                     _swapChain.Present(
//                         1,
//                         0));
//             }
//             catch
//             {
//                 /*
//                  * Preserve your original behavior:
//                  * do not crash the rendering loop.
//                  */
//             }
//         }

//         #endregion


//         #region ResizeSwapChain

//         public void ResizeSwapChain(
//             int width,
//             int height)
//         {
//             if (_swapChain.Handle == null)
//                 return;

//             try
//             {
//                 width = Math.Max(width, 1);
//                 height = Math.Max(height, 1);

//                 /*
//                  * Release views referencing the old buffers.
//                  */

//                 _backBufferView.Dispose();
//                 _backBuffer.Dispose();


//                 /*
//                  * Resize.
//                  */

//                 SilkMarshal.ThrowHResult(
//                     _swapChain.ResizeBuffers(
//                         0,
//                         (uint)width,
//                         (uint)height,
//                         Format.FormatB8G8R8A8Unorm,
//                         0));


//                 /*
//                  * Obtain new backbuffer.
//                  */

//                 CreateBackBuffer();


//                 _swapChainWidth = width;
//                 _swapChainHeight = height;

//                 Console.WriteLine(
//                     $"DirectX resized to: {width}x{height}");


//                 /*
//                  * Re-present the current frame.
//                  */

//                 if (_currentTextureView.Handle != null)
//                     PresentStaticImage();
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine(
//                     $"Resize failed: {ex}");
//             }
//         }

//         #endregion


//         #region Dispose texture

//         private void DisposeCurrentTexture()
//         {
//             _currentTextureView.Dispose();

//             _textureWidth = 0;
//             _textureHeight = 0;
//         }

//         #endregion


//         #region Dispose

//         public void Dispose()
//         {
//             if (_disposed)
//                 return;

//             _disposed = true;

//             try
//             {
//                 DisposeCurrentTexture();

//                 _samplerState.Dispose();

//                 _vertexBuffer.Dispose();

//                 _vertexShader.Dispose();
//                 _pixelShader.Dispose();

//                 _backBufferView.Dispose();
//                 _backBuffer.Dispose();

//                 _swapChain.Dispose();
//                 _factory.Dispose();

//                 _deviceContext.Dispose();
//                 _device.Dispose();

//                 _compiler?.Dispose();
//                 _d3d11?.Dispose();
//                 _dxgi?.Dispose();
//             }
//             catch
//             {
//                 /*
//                  * Dispose should never bring down the application.
//                  */
//             }
//         }

//         #endregion
//     }
// }