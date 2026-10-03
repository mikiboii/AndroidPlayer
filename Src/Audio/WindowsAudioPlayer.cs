#if WINDOWS

using System;
using System.Collections.Generic;
using Androidplayer.Audio;
using SharpDX;
using SharpDX.Multimedia;
using SharpDX.XAudio2;

namespace Androidplayer.Audio;

public sealed class WindowsAudioPlayer : IAudioPlayer
{
    private XAudio2? _xaudio;
    private MasteringVoice? _masteringVoice;
    private SourceVoice? _sourceVoice;
    private WaveFormat? _waveFormat;

    private readonly object _audioLock = new();
    private readonly Queue<DataStream> _pendingStreams = new();

    private const int MAX_QUEUED_BUFFERS = 20;

    private bool _started;

    public void Initialize()
    {
        lock (_audioLock)
        {
            if (_xaudio != null)
                return;

            _xaudio = new XAudio2();

            _masteringVoice = new MasteringVoice(_xaudio);

            // scrcpy audio:
            // 48 kHz, stereo, 16-bit PCM
            _waveFormat = new WaveFormat(48000, 16, 2);

            _sourceVoice = new SourceVoice(
                _xaudio,
                _waveFormat);

            _sourceVoice.SetVolume(0.2f); 
            _sourceVoice.Start();

            _pendingStreams.Clear();
            _started = true;

            Console.WriteLine(
                "XAudio2 initialized for real-time playback");
        }
    }

    public void Play(byte[] pcm)
    {
        if (pcm == null || pcm.Length == 0)
            return;

        lock (_audioLock)
        {
            if (_sourceVoice == null ||
                !_started ||
                _waveFormat == null)
                return;

            var state = _sourceVoice.State;

            if (state.BuffersQueued >= MAX_QUEUED_BUFFERS)
            {
                Console.WriteLine(
                    $"Audio backlog: {state.BuffersQueued} buffers queued, flushing");

                _sourceVoice.Stop();
                _sourceVoice.FlushSourceBuffers();
                _sourceVoice.Start();
            }

            int blockAlign = _waveFormat.BlockAlign;

            if (pcm.Length % blockAlign != 0)
            {
                int paddedLength =
                    ((pcm.Length + blockAlign - 1) / blockAlign)
                    * blockAlign;

                Array.Resize(ref pcm, paddedLength);
            }

            var stream = new DataStream(
                pcm.Length,
                true,
                true);

            stream.Write(
                pcm,
                0,
                pcm.Length);

            stream.Position = 0;

            var buffer = new AudioBuffer
            {
                Stream = stream,
                AudioBytes = pcm.Length,
                Flags = BufferFlags.None
            };

            try
            {
                _sourceVoice.SubmitSourceBuffer(
                    buffer,
                    null);

                _pendingStreams.Enqueue(stream);
            }
            catch (SharpDX.SharpDXException ex)
            {
                Console.WriteLine(
                    $"XAudio2 submit error: {ex.ResultCode} / {ex.Message}");

                stream.Dispose();
            }
        }
    }

    public void Cleanup()
    {
        lock (_audioLock)
        {
            try
            {
                if (_sourceVoice != null)
                {
                    _sourceVoice.Stop();
                    _sourceVoice.FlushSourceBuffers();
                }

                while (_pendingStreams.Count > 0)
                {
                    _pendingStreams.Dequeue().Dispose();
                }

                _sourceVoice?.DestroyVoice();
                _sourceVoice = null;

                _masteringVoice?.Dispose();
                _masteringVoice = null;

                _xaudio?.Dispose();
                _xaudio = null;

                _started = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Error disposing audio: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        Cleanup();
    }
}

#endif

















