#if LINUX

namespace Androidplayer.Audio;

public sealed class LinuxAudioPlayer : IAudioPlayer
{
    public void Initialize()
    {
        // TODO:
        // PipeWire / PulseAudio / ALSA implementation
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