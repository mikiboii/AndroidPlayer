using System;
using Androidplayer.Src.Rendering;

#if LINUX



namespace Androidplayer.Src;

public class Vulkan :IVideoRenderer
{
    public void Dispose()
    {
        
    }

    public string BackendName { get; }
    public void Initialize(IntPtr windowHandle, int width = 0, int height = 0)
    {
        
    }

    public void ResizeToClient(IntPtr windowHandle)
    {
       
    }

    public void ResizeSwapChain(int width, int height)
    {
        
    }

    public void HandleResize()
    {
        
    }

    public void PresentFrameKeepAlive()
    {
        
    }

    public void DisplayImage(string fileName)
    {
       
    }

    public void PresentStaticImage()
    {
        
    }
}







#endif
