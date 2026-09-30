// namespace Androidplayer.Rendering;
//
// public class D3D11Swapchain
// {
//     
// }




using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Rendering.Composition;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using D3DDevice = SharpDX.Direct3D11.Device;
using DxgiResource = SharpDX.DXGI.Resource;

namespace Androidplayer.Rendering;

/// <summary>
/// Standalone D3D11 -> Avalonia composition swapchain. Does not inherit
/// from SwapchainBase&lt;T&gt; and does not require ISwapchainImage; the
/// image-pooling / lifetime logic is implemented inline.
/// </summary>
class D3D11Swapchain : IAsyncDisposable
{
    private readonly D3DDevice _device;
    private readonly ICompositionGpuInterop _interop;
    private readonly CompositionDrawingSurface _target;
    private readonly List<D3D11SwapchainImage> _pendingImages = new();

    public D3D11Swapchain(D3DDevice device,
        ICompositionGpuInterop interop,
        CompositionDrawingSurface target)
    {
        _device  = device;
        _interop = interop;
        _target  = target;
    }

    // ------------------------------------------------------------------
    // Image pool management (previously SwapchainBase<T>)
    // ------------------------------------------------------------------

    static bool IsBroken(D3D11SwapchainImage image) =>
        image.LastPresent?.IsFaulted == true;

    static bool IsReady(D3D11SwapchainImage image) =>
        image.LastPresent == null ||
        image.LastPresent.Status == TaskStatus.RanToCompletion;

    D3D11SwapchainImage? CleanupAndFindNextImage(PixelSize size)
    {
        D3D11SwapchainImage? firstFound = null;
        var foundMultiple = false;

        for (var c = _pendingImages.Count - 1; c > -1; c--)
        {
            var image   = _pendingImages[c];
            var ready   = IsReady(image);
            var matches = image.Size == size;

            if (IsBroken(image) || (!matches && ready))
            {
                _ = image.DisposeAsync();
                _pendingImages.RemoveAt(c);
            }

            if (matches && ready)
            {
                if (firstFound == null)
                    firstFound = image;
                else
                    foundMultiple = true;
            }
        }

        // Only reuse when at least two images of this size exist, so one
        // stays on screen while the other is being drawn (double-buffer
        // invariant). Matches the original SwapchainBase behavior.
        return foundMultiple ? firstFound : null;
    }

    D3D11SwapchainImage CreateImage(PixelSize size)
    {
        // D3D11 rejects 0-width/height textures with E_INVALIDARG.
        // Clamp to a legal minimum as a last line of defence.
        if (size.Width  < 1) size = new PixelSize(1, size.Height);
        if (size.Height < 1) size = new PixelSize(size.Width, 1);

        return new D3D11SwapchainImage(_device, size, _interop, _target);
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    public IDisposable BeginDraw(PixelSize size, out RenderTargetView view)
    {
        var img = CleanupAndFindNextImage(size) ?? CreateImage(size);

        img.BeginDraw();
        _pendingImages.Remove(img);
        view = img.RenderTargetView;

        return new BeginDrawScope(this, img);
    }

    public async ValueTask DisposeAsync()
    {
        // Snapshot: DisposeAsync on an image can (indirectly) cause
        // callbacks that mutate _pendingImages, so don't iterate the
        // live list.
        var images = _pendingImages.ToArray();
        _pendingImages.Clear();

        foreach (var img in images)
            await img.DisposeAsync();
    }

    private sealed class BeginDrawScope : IDisposable
    {
        private readonly D3D11Swapchain _owner;
        private readonly D3D11SwapchainImage _image;
        private bool _disposed;

        public BeginDrawScope(D3D11Swapchain owner, D3D11SwapchainImage image)
        {
            _owner = owner;
            _image = image;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _image.Present();
            _owner._pendingImages.Add(_image);
        }
    }
}


public class D3D11SwapchainImage : IAsyncDisposable
{
    public PixelSize Size { get; }
    private readonly ICompositionGpuInterop _interop;
    private readonly CompositionDrawingSurface _target;
    private readonly Texture2D _texture;
    private readonly KeyedMutex _mutex;
    private readonly IntPtr _handle;
    private PlatformGraphicsExternalImageProperties _properties;
    private ICompositionImportedGpuImage? _imported;
    public Task? LastPresent { get; private set; }
    public RenderTargetView RenderTargetView { get; }

    public D3D11SwapchainImage(D3DDevice device, PixelSize size,
        ICompositionGpuInterop interop,
        CompositionDrawingSurface target)
    {
        Size = size;
        _interop = interop;
        _target = target;
        _texture = new Texture2D(device,
            new Texture2DDescription
            {
                Format = Format.R8G8B8A8_UNorm,
                Width = size.Width,
                Height = size.Height,
                ArraySize = 1,
                MipLevels = 1,
                SampleDescription = new SampleDescription { Count = 1, Quality = 0 },
                CpuAccessFlags = default,
                OptionFlags = ResourceOptionFlags.SharedKeyedmutex,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource
            });
        _mutex = _texture.QueryInterface<KeyedMutex>();
        using (var res = _texture.QueryInterface<DxgiResource>())
            _handle = res.SharedHandle;
        _properties = new PlatformGraphicsExternalImageProperties
        {
            Width = size.Width,
            Height = size.Height,
            Format = PlatformGraphicsExternalImageFormat.B8G8R8A8UNorm
        };

        RenderTargetView = new RenderTargetView(device, _texture);
    }

    public void BeginDraw()
    {
        _mutex.Acquire(0, int.MaxValue);
    }

    public void Present()
    {
        _mutex.Release(1);
        _imported ??= _interop.ImportImage(
            new PlatformHandle(_handle,
                KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureGlobalSharedHandle),
            _properties);
        LastPresent = _target.UpdateWithKeyedMutexAsync(_imported, 1, 0);
    }

    public async ValueTask DisposeAsync()
    {
        if (LastPresent != null)
            try { await LastPresent; } catch { /* ignore */ }

        if (_imported is not null)
            try { await _imported.DisposeAsync(); } catch { /* ignore */ }

        RenderTargetView.Dispose();
        _mutex.Dispose();
        _texture.Dispose();
    }
}