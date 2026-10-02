#if MACOS

namespace Androidplayer.Audio;

public sealed class MacOSAudioPlayer : IAudioPlayer
{
    public void Initialize()
    {
        // TODO:
        // CoreAudio implementation
    }

    public void Play(byte[] pcm)
    {
        // TODO
    }

    public void Cleanup()
    {
        // TODO
    }

    public void Dispose()
    {
        Cleanup();
    }
}

#endif