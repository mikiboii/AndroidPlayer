// Src/Rendering/IVideoRenderer.cs
using System;

namespace Androidplayer.Src.Rendering;

public interface IVideoRenderer : IDisposable
{
    string BackendName { get; }

    void Initialize(nint windowHandle, int width = 0, int height = 0);
    void ResizeToClient(nint windowHandle);
    void ResizeSwapChain(int width, int height);
    void HandleResize();

    // void PresentFrame(nint textureHandle, int width = 0, int height = 0);
    void PresentFrameKeepAlive();

    void DisplayImage(string fileName);
    void PresentStaticImage();
}