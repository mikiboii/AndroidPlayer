using System;

namespace Androidplayer.Audio;


public interface IAudioPlayer : IDisposable
{
    void Initialize();
    void Play(byte[] pcm);
    void Cleanup();
}