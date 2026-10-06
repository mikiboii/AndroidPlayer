#if WINDOWS

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using FFmpeg.AutoGen;
using static FFmpeg.AutoGen.ffmpeg;

namespace Androidplayer.Src
{
    /// <summary>
    /// Muxes raw scrcpy H264 + Opus packets into an MP4 file (no decode, no re-encode).
    ///
    /// - Video is dropped until the first keyframe; that PTS becomes time zero for both streams.
    /// - The video stream is declared as 30 fps. scrcpy only sends frames when the screen changes
    ///   (sometimes ~1 fps), so every frame gets a real duration (time until the next frame) and
    ///   the last frame is held until the end of the recording. Playback is therefore real-time.
    /// - Audio is included by default. If the Opus config never arrives (or is in scrcpy's wrapped
    ///   format), a valid OpusHead for 48 kHz stereo is built automatically.
    /// </summary>
    public sealed unsafe class Video_recorder : IDisposable
    {
        private enum Kind { Video, Audio, VideoConfig, AudioConfig, Stop }

        private readonly struct Item
        {
            public readonly Kind Kind;
            public readonly byte[] Data;
            public readonly long PtsUs;
            public readonly bool IsKeyFrame;

            public Item(Kind kind, byte[] data, long ptsUs = 0, bool isKeyFrame = false)
            {
                Kind = kind;
                Data = data;
                PtsUs = ptsUs;
                IsKeyFrame = isKeyFrame;
            }
        }
        
        
        public string Path { get; }

        // ---- constants ----
        private const int TargetFps = 30;
        private const int VideoTimeBase = 90000;
        private const long FrameTicks = VideoTimeBase / TargetFps;   // 3000 ticks = 1/30 s
        private const int AudioSampleRate = 48000;
        private const int AudioFrameSamples = 960;                   // 20 ms Opus frame
        private const long AudioResyncThreshold = 4800;              // 100 ms
        private const long UsPerSecond = 1_000_000;
        private const int AV_INPUT_BUFFER_PADDING_SIZE = 64;
        private const long AudioWaitMs = 2000;
        private const int MaxPending = 3000;

        // private readonly string _path;
        private readonly int _videoWidth;
        private readonly int _videoHeight;
        private readonly bool _includeAudio;
        private readonly Stopwatch _sinceStart = Stopwatch.StartNew();

        private readonly BlockingCollection<Item> _queue =
            new BlockingCollection<Item>(new ConcurrentQueue<Item>());
        private readonly Thread _writer;

        // FFmpeg state (writer thread only)
        private AVFormatContext* _fmt;
        private AVStream* _videoStream;
        private AVStream* _audioStream;
        private AVPacket* _pkt;

        private byte[]? _videoExtradata;   // Annex-B SPS+PPS
        private byte[]? _audioExtradata;   // OpusHead (normalized when header is written)
        private bool _sawAudio;

        private bool _headerWritten;
        private bool _headerFailed;
        private volatile bool _disposed;

        // Timestamp state (writer thread only)
        private bool _haveBase;
        private long _basePtsUs;
        private long _maxPtsUs;
        private long _lastVideoDts = -1;
        private long _lastAudioDts = -1;
        private long _audioNextPts = -1;

        // Video packet held back one step so it can be written with a real duration
        private bool _hasHeld;
        private byte[]? _heldData;
        private long _heldPts;
        private bool _heldKey;

        // Packets that arrived while waiting for the header
        private readonly List<Item> _pending = new List<Item>();
        private bool _pendingHasKey;

        /// <param name="includeAudio">Add an Opus audio track when audio packets are arriving.</param>
        public Video_recorder(string path, int videoWidth, int videoHeight, bool includeAudio = true)
        {
            // _path = path;
            Path = path;
        
        
            _videoWidth = videoWidth > 0 ? videoWidth : 1280;
            _videoHeight = videoHeight > 0 ? videoHeight : 720;
            _includeAudio = includeAudio;

            _writer = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "VideoRecorderWriter"
            };
            _writer.Start();
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        public void SetVideoExtradata(byte[] annexB)
        {
            if (_disposed || annexB == null || annexB.Length == 0) return;
            try { _queue.Add(new Item(Kind.VideoConfig, (byte[])annexB.Clone())); }
            catch (InvalidOperationException) { }
        }

        public void SetAudioExtradata(byte[] opusHead)
        {
            if (_disposed || opusHead == null || opusHead.Length == 0) return;
            try { _queue.Add(new Item(Kind.AudioConfig, (byte[])opusHead.Clone())); }
            catch (InvalidOperationException) { }
        }

        public void WriteVideoPacket(byte[] data, long ptsUs, bool isKeyFrame)
        {
            if (_disposed || data == null || data.Length == 0) return;
            try { _queue.Add(new Item(Kind.Video, data, ptsUs, isKeyFrame)); }
            catch (InvalidOperationException) { }
        }

        public void WriteAudioPacket(byte[] data, long ptsUs)
        {
            if (_disposed || data == null || data.Length == 0) return;
            try { _queue.Add(new Item(Kind.Audio, data, ptsUs)); }
            catch (InvalidOperationException) { }
        }

        // ------------------------------------------------------------------
        // Writer thread
        // ------------------------------------------------------------------

        private void WriterLoop()
        {
            try
            {
                foreach (var item in _queue.GetConsumingEnumerable())
                {
                    if (item.Kind == Kind.Stop) break;

                    switch (item.Kind)
                    {
                        case Kind.VideoConfig:
                            _videoExtradata = item.Data;
                            TryWriteHeaderAndFlush();
                            break;

                        case Kind.AudioConfig:
                            _audioExtradata = item.Data;
                            _sawAudio = true;
                            TryWriteHeaderAndFlush();
                            break;

                        case Kind.Video:
                        case Kind.Audio:
                            if (item.Kind == Kind.Audio) _sawAudio = true;

                            if (!_headerWritten)
                                TryWriteHeaderAndFlush();

                            if (_headerWritten)
                                ProcessMedia(item);
                            else if (!_headerFailed)
                                BufferPending(item);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[recorder] writer thread failed: {ex}");
            }
            finally
            {
                CloseFile();
            }
        }

        private void BufferPending(in Item item)
        {
            if (item.Kind == Kind.Video)
            {
                if (item.IsKeyFrame)
                {
                    _pending.RemoveAll(i => i.Kind == Kind.Video);
                    _pendingHasKey = true;
                }
                else if (!_pendingHasKey)
                {
                    return; // can't start on a P-frame
                }
            }

            _pending.Add(item);
            if (_pending.Count > MaxPending)
                _pending.RemoveAt(0);
        }

        private void TryWriteHeaderAndFlush()
        {
            TryWriteHeader();

            if (_headerWritten && _pending.Count > 0)
            {
                var copy = _pending.ToArray();
                _pending.Clear();
                _pendingHasKey = false;
                foreach (var it in copy)
                    ProcessMedia(it);
            }
        }

        private void ProcessMedia(in Item item)
        {
            if (item.Kind == Kind.Video)
            {
                if (!_haveBase)
                {
                    if (!item.IsKeyFrame) return;   // wait for first keyframe
                    _basePtsUs = item.PtsUs;
                    _haveBase = true;
                }

                QueueVideo(item);
            }
            else if (item.Kind == Kind.Audio)
            {
                if (_audioStream == null) return;
                if (!_haveBase || item.PtsUs < _basePtsUs) return; // sync with video start

                if (item.PtsUs > _maxPtsUs) _maxPtsUs = item.PtsUs;

                long ticks = UsToTicks(item.PtsUs - _basePtsUs, AudioSampleRate);

                // Use a clean sample-counter timeline; only resync if we drifted >100 ms
                if (_audioNextPts >= 0 && Math.Abs(ticks - _audioNextPts) <= AudioResyncThreshold)
                    ticks = _audioNextPts;

                if (ticks <= _lastAudioDts) ticks = _lastAudioDts + 1;
                _lastAudioDts = ticks;
                _audioNextPts = ticks + AudioFrameSamples;

                EmitAudio(item.Data, ticks);
            }
        }

        // ------------------------------------------------------------------
        // Header / streams
        // ------------------------------------------------------------------

        private void TryWriteHeader()
        {
            if (_headerWritten || _headerFailed) return;
            if (_videoExtradata == null) return;
            if (_fmt != null) return;

            bool audioKnown = _audioExtradata != null || _sawAudio;

            // Give audio a moment to show up, then fall back to video-only
            if (_includeAudio && !audioKnown && _sinceStart.ElapsedMilliseconds < AudioWaitMs)
                return;

            bool wantAudio = _includeAudio && audioKnown;

            AVFormatContext* fmt = null;
            int ret = avformat_alloc_output_context2(&fmt, null, "mp4", Path);
            if (ret < 0 || fmt == null)
            {
                Console.WriteLine($"[recorder] avformat_alloc_output_context2 failed: {FFErr(ret)}");
                _headerFailed = true;
                return;
            }
            _fmt = fmt;

            // ---- Video stream: H264 passthrough, declared as 30 fps ----
            _videoStream = avformat_new_stream(_fmt, null);
            if (_videoStream == null)
            {
                Console.WriteLine("[recorder] avformat_new_stream(video) returned null");
                _headerFailed = true;
                ResetFormat();
                return;
            }

            _videoStream->time_base = new AVRational { num = 1, den = VideoTimeBase };
            _videoStream->avg_frame_rate = new AVRational { num = TargetFps, den = 1 };
            _videoStream->r_frame_rate = new AVRational { num = TargetFps, den = 1 };

            var vp = _videoStream->codecpar;
            vp->codec_type = AVMediaType.AVMEDIA_TYPE_VIDEO;
            vp->codec_id   = AVCodecID.AV_CODEC_ID_H264;
            vp->width      = _videoWidth;
            vp->height     = _videoHeight;
            vp->format     = (int)AVPixelFormat.AV_PIX_FMT_YUV420P;
            vp->framerate  = new AVRational { num = TargetFps, den = 1 };

            vp->extradata = (byte*)av_mallocz(
                (ulong)(_videoExtradata.Length + AV_INPUT_BUFFER_PADDING_SIZE));
            Marshal.Copy(_videoExtradata, 0, (IntPtr)vp->extradata, _videoExtradata.Length);
            vp->extradata_size = _videoExtradata.Length;

            // ---- Audio stream: Opus passthrough ----
            if (wantAudio)
            {
                byte[] opusHead = NormalizeOpusHead(_audioExtradata);

                _audioStream = avformat_new_stream(_fmt, null);
                if (_audioStream == null)
                {
                    Console.WriteLine("[recorder] avformat_new_stream(audio) returned null");
                    _headerFailed = true;
                    ResetFormat();
                    return;
                }

                _audioStream->time_base = new AVRational { num = 1, den = AudioSampleRate };

                var ap = _audioStream->codecpar;
                ap->codec_type  = AVMediaType.AVMEDIA_TYPE_AUDIO;
                ap->codec_id    = AVCodecID.AV_CODEC_ID_OPUS;
                ap->sample_rate = AudioSampleRate;
                ap->format      = (int)AVSampleFormat.AV_SAMPLE_FMT_FLT;
                ap->frame_size  = AudioFrameSamples;

                ap->ch_layout = default;
                av_channel_layout_default(&ap->ch_layout, 2);

                ap->extradata = (byte*)av_mallocz(
                    (ulong)(opusHead.Length + AV_INPUT_BUFFER_PADDING_SIZE));
                Marshal.Copy(opusHead, 0, (IntPtr)ap->extradata, opusHead.Length);
                ap->extradata_size = opusHead.Length;
            }

            // ---- Open output ----
            if ((_fmt->oformat->flags & AVFMT_NOFILE) == 0)
            {
                ret = avio_open(&_fmt->pb, Path, AVIO_FLAG_WRITE);
                if (ret < 0)
                {
                    Console.WriteLine($"[recorder] avio_open failed: {FFErr(ret)}");
                    _headerFailed = true;
                    ResetFormat();
                    return;
                }
            }

            ret = avformat_write_header(_fmt, null);
            if (ret < 0)
            {
                Console.WriteLine($"[recorder] avformat_write_header failed: {FFErr(ret)}");
                _headerFailed = true;
                ResetFormat();
                return;
            }

            _pkt = av_packet_alloc();
            if (_pkt == null)
            {
                Console.WriteLine("[recorder] av_packet_alloc failed");
                _headerFailed = true;
                ResetFormat();
                return;
            }

            _headerWritten = true;

            Console.WriteLine(
                $"[recorder] opened OK ({_videoWidth}x{_videoHeight} @ {TargetFps}fps, video" +
                (wantAudio ? " + audio)" : ")"));
        }

        /// <summary>
        /// Returns a valid 'OpusHead' blob. Handles: raw OpusHead, scrcpy/Android wrapped
        /// config ("AOPUSHDR" + len + OpusHead ...), or missing config (builds 48 kHz stereo).
        /// </summary>
        private static byte[] NormalizeOpusHead(byte[]? cfg)
        {
            if (cfg != null && cfg.Length >= 8)
            {
                string magic = Encoding.ASCII.GetString(cfg, 0, 8);

                if (magic == "OpusHead")
                    return cfg;

                if (magic == "AOPUSHDR" && cfg.Length >= 16)
                {
                    long len = BitConverter.ToInt64(cfg, 8);
                    if (len > 0 && 16 + len <= cfg.Length)
                    {
                        var head = new byte[len];
                        Buffer.BlockCopy(cfg, 16, head, 0, (int)len);
                        return head;
                    }
                }
            }

            Console.WriteLine("[recorder] audio config missing/unknown, using default OpusHead (48kHz stereo)");

            var h = new byte[19];
            Encoding.ASCII.GetBytes("OpusHead").CopyTo(h, 0);
            h[8] = 1;                       // version
            h[9] = 2;                       // channels
            h[10] = 312 & 0xFF;             // pre-skip (LE)
            h[11] = (312 >> 8) & 0xFF;
            h[12] = 48000 & 0xFF;           // input sample rate (LE)
            h[13] = (48000 >> 8) & 0xFF;
            h[14] = (48000 >> 16) & 0xFF;
            h[15] = (48000 >> 24) & 0xFF;
            h[16] = 0;                      // output gain
            h[17] = 0;
            h[18] = 0;                      // mapping family 0
            return h;
        }

        // ------------------------------------------------------------------
        // Packet writes
        // ------------------------------------------------------------------

        /// <summary>
        /// Video packets are held back by one so each one is written with its real duration
        /// (time until the next frame). This keeps 1 fps static screens at the right speed.
        /// </summary>
        private void QueueVideo(in Item item)
        {
            if (item.PtsUs > _maxPtsUs) _maxPtsUs = item.PtsUs;

            long ticks = UsToTicks(item.PtsUs - _basePtsUs, VideoTimeBase);
            if (ticks <= _lastVideoDts) ticks = _lastVideoDts + 1;
            _lastVideoDts = ticks;

            if (_hasHeld && _heldData != null)
                EmitVideo(_heldData, _heldPts, _heldKey, Math.Max(1, ticks - _heldPts));

            _heldData = item.Data;
            _heldPts = ticks;
            _heldKey = item.IsKeyFrame;
            _hasHeld = true;
        }

        private void FlushHeldVideo()
        {
            if (!_hasHeld || _heldData == null) return;

            // Hold the last frame until the end of the recording (at least 1/30 s)
            long endTicks = UsToTicks(_maxPtsUs - _basePtsUs, VideoTimeBase);
            long dur = Math.Max(FrameTicks, endTicks - _heldPts);

            EmitVideo(_heldData, _heldPts, _heldKey, dur);
            _hasHeld = false;
            _heldData = null;
        }

        private void EmitVideo(byte[] data, long pts, bool key, long duration)
        {
            if (_videoStream == null || _pkt == null || _fmt == null) return;

            av_packet_unref(_pkt);

            int ret = av_new_packet(_pkt, data.Length);
            if (ret < 0)
            {
                Console.WriteLine($"[recorder] av_new_packet(video) failed: {FFErr(ret)}");
                return;
            }

            Marshal.Copy(data, 0, (IntPtr)_pkt->data, data.Length);

            _pkt->pts = pts;
            _pkt->dts = pts;
            _pkt->duration = duration;
            _pkt->stream_index = _videoStream->index;
            _pkt->flags = key ? AV_PKT_FLAG_KEY : 0;

            ret = av_interleaved_write_frame(_fmt, _pkt);
            if (ret < 0)
                Console.WriteLine($"[recorder] video write failed: {FFErr(ret)}");
        }

        private void EmitAudio(byte[] data, long pts)
        {
            if (_audioStream == null || _pkt == null || _fmt == null) return;

            av_packet_unref(_pkt);

            int ret = av_new_packet(_pkt, data.Length);
            if (ret < 0)
            {
                Console.WriteLine($"[recorder] av_new_packet(audio) failed: {FFErr(ret)}");
                return;
            }

            Marshal.Copy(data, 0, (IntPtr)_pkt->data, data.Length);

            _pkt->pts = pts;
            _pkt->dts = pts;
            _pkt->duration = AudioFrameSamples;
            _pkt->stream_index = _audioStream->index;
            _pkt->flags = AV_PKT_FLAG_KEY;

            ret = av_interleaved_write_frame(_fmt, _pkt);
            if (ret < 0)
                Console.WriteLine($"[recorder] audio write failed: {FFErr(ret)}");
        }

        private static long UsToTicks(long us, int timeBaseDen)
        {
            if (us < 0) us = 0;
            return us * timeBaseDen / UsPerSecond;
        }

        // ------------------------------------------------------------------
        // Cleanup
        // ------------------------------------------------------------------

        private void ResetFormat()
        {
            if (_fmt == null) return;

            if ((_fmt->oformat->flags & AVFMT_NOFILE) == 0 && _fmt->pb != null)
                avio_closep(&_fmt->pb);

            avformat_free_context(_fmt);
            _fmt = null;
            _videoStream = null;
            _audioStream = null;
        }

        private void CloseFile()
        {
            if (_fmt != null && _headerWritten)
            {
                try { FlushHeldVideo(); } catch { }
                try { av_write_trailer(_fmt); } catch { }
            }

            if (_pkt != null)
            {
                AVPacket* tmp = _pkt;
                av_packet_free(&tmp);
                _pkt = null;
            }

            if (_fmt != null)
            {
                if ((_fmt->oformat->flags & AVFMT_NOFILE) == 0 && _fmt->pb != null)
                    avio_closep(&_fmt->pb);

                avformat_free_context(_fmt);
                _fmt = null;
            }

            _videoStream = null;
            _audioStream = null;
            _headerWritten = false;
        }

        private static string FFErr(int err)
        {
            byte* buf = stackalloc byte[AV_ERROR_MAX_STRING_SIZE];
            av_strerror(err, buf, AV_ERROR_MAX_STRING_SIZE);
            return Marshal.PtrToStringAnsi((IntPtr)buf) ?? err.ToString();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _queue.Add(new Item(Kind.Stop, Array.Empty<byte>())); }
            catch (InvalidOperationException) { }

            try { _queue.CompleteAdding(); }
            catch (ObjectDisposedException) { }

            // Needs time to write the trailer (moov atom); without it the MP4 won't play.
            if (_writer.IsAlive)
                _writer.Join(5000);
        }
    }
}

#endif
















//
//
//
//
// #if WINDOWS
//
// using System;
// using System.Collections.Concurrent;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Runtime.InteropServices;
// using System.Text;
// using System.Threading;
// using FFmpeg.AutoGen;
// using static FFmpeg.AutoGen.ffmpeg;
//
// namespace Androidplayer.Src
// {
//     /// <summary>
//     /// Muxes raw scrcpy H264 + Opus packets into an MP4 file (no decode, no re-encode).
//     ///
//     /// - Video is dropped until the first keyframe; that PTS becomes time zero for both streams.
//     /// - The video stream is declared as 30 fps. scrcpy only sends frames when the screen changes
//     ///   (sometimes ~1 fps), so every frame gets a real duration (time until the next frame) and
//     ///   the last frame is held until the end of the recording. Playback is therefore real-time.
//     /// - Audio is included by default. If the Opus config never arrives (or is in scrcpy's wrapped
//     ///   format), a valid OpusHead for 48 kHz stereo is built automatically.
//     /// </summary>
//     public sealed unsafe class Video_recorder : IDisposable
//     {
//         private enum Kind { Video, Audio, VideoConfig, AudioConfig, SeedVideo, Stop }
//
//         private readonly struct Item
//         {
//             public readonly Kind Kind;
//             public readonly byte[] Data;
//             public readonly long PtsUs;
//             public readonly bool IsKeyFrame;
//
//             public Item(Kind kind, byte[] data, long ptsUs = 0, bool isKeyFrame = false)
//             {
//                 Kind = kind;
//                 Data = data;
//                 PtsUs = ptsUs;
//                 IsKeyFrame = isKeyFrame;
//             }
//         }
//
//         // ---- constants ----
//         private const int TargetFps = 30;
//         private const int VideoTimeBase = 90000;
//         private const long FrameTicks = VideoTimeBase / TargetFps;   // 3000 ticks = 1/30 s
//         private const int AudioSampleRate = 48000;
//         private const int AudioFrameSamples = 960;                   // 20 ms Opus frame
//         private const long AudioResyncThreshold = 4800;              // 100 ms
//         private const long UsPerSecond = 1_000_000;
//         private const int AV_INPUT_BUFFER_PADDING_SIZE = 64;
//         private const long AudioWaitMs = 2000;
//         private const int MaxPending = 3000;
//
//         
//
//         public string Path { get; }
//         
//         
//         private readonly int _videoWidth;
//         private readonly int _videoHeight;
//         private readonly bool _includeAudio;
//         private readonly Stopwatch _sinceStart = Stopwatch.StartNew();
//
//         private readonly BlockingCollection<Item> _queue =
//             new BlockingCollection<Item>(new ConcurrentQueue<Item>());
//         private readonly Thread _writer;
//
//         // FFmpeg state (writer thread only)
//         private AVFormatContext* _fmt;
//         private AVStream* _videoStream;
//         private AVStream* _audioStream;
//         private AVPacket* _pkt;
//
//         private byte[]? _videoExtradata;   // Annex-B SPS+PPS
//         private byte[]? _audioExtradata;   // OpusHead (normalized when header is written)
//         private bool _sawAudio;
//
//         private bool _headerWritten;
//         private bool _headerFailed;
//         private volatile bool _disposed;
//
//         // Timestamp state (writer thread only)
//         private bool _haveBase;
//         private bool _videoStarted;
//         private readonly List<Item> _seeds = new List<Item>();
//         private long _basePtsUs;
//         private long _maxPtsUs;
//         private long _lastVideoDts = -1;
//         private long _lastAudioDts = -1;
//         private long _audioNextPts = -1;
//
//         // Video packet held back one step so it can be written with a real duration
//         private bool _hasHeld;
//         private byte[]? _heldData;
//         private long _heldPts;
//         private bool _heldKey;
//
//         // Packets that arrived while waiting for the header
//         private readonly List<Item> _pending = new List<Item>();
//         private bool _pendingHasKey;
//
//         // ------------------------------------------------------------------
//         // Static GOP cache: keeps the packets since the last keyframe so a recording can
//         // start on the current image even when the screen is static (no new frames).
//         // The worker calls Video_recorder.FeedVideo() for EVERY video packet, always.
//         // ------------------------------------------------------------------
//         private const long CacheMaxBytes = 48L * 1024 * 1024;
//         private static readonly object CacheLock = new object();
//         private static readonly List<(byte[] Data, bool Key)> CacheGop = new List<(byte[], bool)>();
//         private static long _cacheBytes;
//         private static byte[]? _cacheConfig;
//         private static Video_recorder? _activeRecorder;
//
//         /// <summary>Call for every video packet from scrcpy (recording or not).</summary>
//         public static void FeedVideo(byte[] data, long ptsUs, bool isConfig, bool isKeyFrame)
//         {
//             if (data == null || data.Length == 0) return;
//
//             lock (CacheLock)
//             {
//                 if (isConfig)
//                 {
//                     _cacheConfig = data;
//                     _activeRecorder?.SetVideoExtradata(data);
//                     return;
//                 }
//
//                 if (isKeyFrame)
//                 {
//                     CacheGop.Clear();
//                     _cacheBytes = 0;
//                 }
//
//                 if (CacheGop.Count > 0 || isKeyFrame)
//                 {
//                     CacheGop.Add((data, isKeyFrame));
//                     _cacheBytes += data.Length;
//
//                     if (_cacheBytes > CacheMaxBytes)   // pathological GOP: wait for next keyframe
//                     {
//                         CacheGop.Clear();
//                         _cacheBytes = 0;
//                     }
//                 }
//
//                 _activeRecorder?.WriteVideoPacket(data, ptsUs, isKeyFrame);
//             }
//         }
//
//         /// <summary>Seeds this recorder with the cached GOP and starts live forwarding (atomic).</summary>
//         private void AttachToCache()
//         {
//             lock (CacheLock)
//             {
//                 if (_cacheConfig != null)
//                     SetVideoExtradata(_cacheConfig);
//
//                 foreach (var (data, key) in CacheGop)
//                     SeedVideoFrame(data, key);
//
//                 _activeRecorder = this;
//             }
//         }
//
//         private void DetachFromCache()
//         {
//             lock (CacheLock)
//             {
//                 if (ReferenceEquals(_activeRecorder, this))
//                     _activeRecorder = null;
//             }
//         }
//
//         /// <param name="includeAudio">Add an Opus audio track when audio packets are arriving.</param>
//         public Video_recorder(string path, int videoWidth, int videoHeight, bool includeAudio = true)
//         {
//             path = path;
//             _videoWidth = videoWidth > 0 ? videoWidth : 1280;
//             _videoHeight = videoHeight > 0 ? videoHeight : 720;
//             _includeAudio = includeAudio;
//
//             _writer = new Thread(WriterLoop)
//             {
//                 IsBackground = true,
//                 Name = "VideoRecorderWriter"
//             };
//             _writer.Start();
//
//             AttachToCache();
//         }
//
//         // ------------------------------------------------------------------
//         // Public API
//         // ------------------------------------------------------------------
//
//         public void SetVideoExtradata(byte[] annexB)
//         {
//             if (_disposed || annexB == null || annexB.Length == 0) return;
//             try { _queue.Add(new Item(Kind.VideoConfig, (byte[])annexB.Clone())); }
//             catch (InvalidOperationException) { }
//         }
//
//         public void SetAudioExtradata(byte[] opusHead)
//         {
//             if (_disposed || opusHead == null || opusHead.Length == 0) return;
//             try { _queue.Add(new Item(Kind.AudioConfig, (byte[])opusHead.Clone())); }
//             catch (InvalidOperationException) { }
//         }
//
//         public void WriteVideoPacket(byte[] data, long ptsUs, bool isKeyFrame)
//         {
//             if (_disposed || data == null || data.Length == 0) return;
//             try { _queue.Add(new Item(Kind.Video, data, ptsUs, isKeyFrame)); }
//             catch (InvalidOperationException) { }
//         }
//
//         /// <summary>
//         /// Pre-roll packet from the GOP cache (keyframe first, then following P-frames).
//         /// All seed packets are placed at time zero so the file opens on the current image.
//         /// </summary>
//         public void SeedVideoFrame(byte[] data, bool isKeyFrame)
//         {
//             if (_disposed || data == null || data.Length == 0) return;
//             try { _queue.Add(new Item(Kind.SeedVideo, data, 0, isKeyFrame)); }
//             catch (InvalidOperationException) { }
//         }
//
//         public void WriteAudioPacket(byte[] data, long ptsUs)
//         {
//             if (_disposed || data == null || data.Length == 0) return;
//             try { _queue.Add(new Item(Kind.Audio, data, ptsUs)); }
//             catch (InvalidOperationException) { }
//         }
//
//         // ------------------------------------------------------------------
//         // Writer thread
//         // ------------------------------------------------------------------
//
//         private void WriterLoop()
//         {
//             try
//             {
//                 foreach (var item in _queue.GetConsumingEnumerable())
//                 {
//                     if (item.Kind == Kind.Stop) break;
//
//                     switch (item.Kind)
//                     {
//                         case Kind.VideoConfig:
//                             _videoExtradata = item.Data;
//                             TryWriteHeaderAndFlush();
//                             break;
//
//                         case Kind.AudioConfig:
//                             _audioExtradata = item.Data;
//                             _sawAudio = true;
//                             TryWriteHeaderAndFlush();
//                             break;
//
//                         case Kind.SeedVideo:
//                             _seeds.Add(item);
//                             break;
//
//                         case Kind.Video:
//                         case Kind.Audio:
//                             if (item.Kind == Kind.Audio) _sawAudio = true;
//
//                             if (!_headerWritten)
//                                 TryWriteHeaderAndFlush();
//
//                             if (_headerWritten)
//                                 ProcessMedia(item);
//                             else if (!_headerFailed)
//                                 BufferPending(item);
//                             break;
//                     }
//                 }
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"[recorder] writer thread failed: {ex}");
//             }
//             finally
//             {
//                 CloseFile();
//             }
//         }
//
//         private void BufferPending(in Item item)
//         {
//             if (item.Kind == Kind.Video)
//             {
//                 if (item.IsKeyFrame)
//                 {
//                     _pending.RemoveAll(i => i.Kind == Kind.Video);
//                     _pendingHasKey = true;
//                 }
//                 else if (!_pendingHasKey && _seeds.Count == 0)
//                 {
//                     return; // can't start on a P-frame
//                 }
//             }
//
//             _pending.Add(item);
//             if (_pending.Count > MaxPending)
//                 _pending.RemoveAt(0);
//         }
//
//         private void TryWriteHeaderAndFlush()
//         {
//             TryWriteHeader();
//
//             if (_headerWritten && _pending.Count > 0)
//             {
//                 var copy = _pending.ToArray();
//                 _pending.Clear();
//                 _pendingHasKey = false;
//
//                 // Time zero = earliest buffered audio packet or video keyframe
//                 if (!_haveBase)
//                 {
//                     long min = long.MaxValue;
//                     foreach (var it in copy)
//                     {
//                         if (it.Kind == Kind.Audio || (it.Kind == Kind.Video && (it.IsKeyFrame || _seeds.Count > 0)))
//                             if (it.PtsUs < min) min = it.PtsUs;
//                     }
//                     if (min != long.MaxValue)
//                     {
//                         _basePtsUs = min;
//                         _haveBase = true;
//                     }
//                 }
//                 foreach (var it in copy)
//                     ProcessMedia(it);
//             }
//         }
//
//         /// <summary>Writes the cached GOP (if any) at time zero, once the timeline exists.</summary>
//         private void TryEmitSeeds()
//         {
//             if (_seeds.Count == 0 || !_haveBase || _videoStarted) return;
//
//             // Must begin with a keyframe
//             int first = _seeds.FindIndex(i => i.IsKeyFrame);
//             if (first < 0) { _seeds.Clear(); return; }
//
//             for (int i = first; i < _seeds.Count; i++)
//                 QueueVideo(new Item(Kind.Video, _seeds[i].Data, _basePtsUs, _seeds[i].IsKeyFrame));
//
//             _seeds.Clear();
//             _videoStarted = true;
//         }
//
//         private void ProcessMedia(in Item item)
//         {
//             if (!_haveBase &&
//                 (item.Kind == Kind.Audio || item.IsKeyFrame || _seeds.Count > 0))
//             {
//                 _basePtsUs = item.PtsUs;
//                 _haveBase = true;
//             }
//             TryEmitSeeds();
//
//             if (item.Kind == Kind.Video)
//             {
//                 if (!_videoStarted)
//                 {
//                     if (!item.IsKeyFrame) return;   // video must start on a keyframe
//                     _videoStarted = true;
//                     if (!_haveBase)
//                     {
//                         _basePtsUs = item.PtsUs;
//                         _haveBase = true;
//                     }
//                 }
//
//                 QueueVideo(item);
//             }
//             else if (item.Kind == Kind.Audio)
//             {
//                 if (_audioStream == null) return;
//
//                 // Audio does NOT wait for video: a static screen sends no video frames at all.
//                 if (!_haveBase)
//                 {
//                     _basePtsUs = item.PtsUs;
//                     _haveBase = true;
//                 }
//                 if (item.PtsUs < _basePtsUs) return;
//
//                 if (item.PtsUs > _maxPtsUs) _maxPtsUs = item.PtsUs;
//
//                 long ticks = UsToTicks(item.PtsUs - _basePtsUs, AudioSampleRate);
//
//                 // Use a clean sample-counter timeline; only resync if we drifted >100 ms
//                 if (_audioNextPts >= 0 && Math.Abs(ticks - _audioNextPts) <= AudioResyncThreshold)
//                     ticks = _audioNextPts;
//
//                 if (ticks <= _lastAudioDts) ticks = _lastAudioDts + 1;
//                 _lastAudioDts = ticks;
//                 _audioNextPts = ticks + AudioFrameSamples;
//
//                 EmitAudio(item.Data, ticks);
//             }
//         }
//
//         // ------------------------------------------------------------------
//         // Header / streams
//         // ------------------------------------------------------------------
//
//         private void TryWriteHeader()
//         {
//             if (_headerWritten || _headerFailed) return;
//             if (_videoExtradata == null) return;
//             if (_fmt != null) return;
//
//             bool audioKnown = _audioExtradata != null || _sawAudio;
//
//             // Give audio a moment to show up, then fall back to video-only
//             if (_includeAudio && !audioKnown && _sinceStart.ElapsedMilliseconds < AudioWaitMs)
//                 return;
//
//             bool wantAudio = _includeAudio && audioKnown;
//
//             AVFormatContext* fmt = null;
//             int ret = avformat_alloc_output_context2(&fmt, null, "mp4", Path);
//             if (ret < 0 || fmt == null)
//             {
//                 Console.WriteLine($"[recorder] avformat_alloc_output_context2 failed: {FFErr(ret)}");
//                 _headerFailed = true;
//                 return;
//             }
//             _fmt = fmt;
//
//             // ---- Video stream: H264 passthrough, declared as 30 fps ----
//             _videoStream = avformat_new_stream(_fmt, null);
//             if (_videoStream == null)
//             {
//                 Console.WriteLine("[recorder] avformat_new_stream(video) returned null");
//                 _headerFailed = true;
//                 ResetFormat();
//                 return;
//             }
//
//             _videoStream->time_base = new AVRational { num = 1, den = VideoTimeBase };
//             _videoStream->avg_frame_rate = new AVRational { num = TargetFps, den = 1 };
//             _videoStream->r_frame_rate = new AVRational { num = TargetFps, den = 1 };
//
//             var vp = _videoStream->codecpar;
//             vp->codec_type = AVMediaType.AVMEDIA_TYPE_VIDEO;
//             vp->codec_id   = AVCodecID.AV_CODEC_ID_H264;
//             vp->width      = _videoWidth;
//             vp->height     = _videoHeight;
//             vp->format     = (int)AVPixelFormat.AV_PIX_FMT_YUV420P;
//             vp->framerate  = new AVRational { num = TargetFps, den = 1 };
//
//             vp->extradata = (byte*)av_mallocz(
//                 (ulong)(_videoExtradata.Length + AV_INPUT_BUFFER_PADDING_SIZE));
//             Marshal.Copy(_videoExtradata, 0, (IntPtr)vp->extradata, _videoExtradata.Length);
//             vp->extradata_size = _videoExtradata.Length;
//
//             // ---- Audio stream: Opus passthrough ----
//             if (wantAudio)
//             {
//                 byte[] opusHead = NormalizeOpusHead(_audioExtradata);
//
//                 _audioStream = avformat_new_stream(_fmt, null);
//                 if (_audioStream == null)
//                 {
//                     Console.WriteLine("[recorder] avformat_new_stream(audio) returned null");
//                     _headerFailed = true;
//                     ResetFormat();
//                     return;
//                 }
//
//                 _audioStream->time_base = new AVRational { num = 1, den = AudioSampleRate };
//
//                 var ap = _audioStream->codecpar;
//                 ap->codec_type  = AVMediaType.AVMEDIA_TYPE_AUDIO;
//                 ap->codec_id    = AVCodecID.AV_CODEC_ID_OPUS;
//                 ap->sample_rate = AudioSampleRate;
//                 ap->format      = (int)AVSampleFormat.AV_SAMPLE_FMT_FLT;
//                 ap->frame_size  = AudioFrameSamples;
//
//                 ap->ch_layout = default;
//                 av_channel_layout_default(&ap->ch_layout, 2);
//
//                 ap->extradata = (byte*)av_mallocz(
//                     (ulong)(opusHead.Length + AV_INPUT_BUFFER_PADDING_SIZE));
//                 Marshal.Copy(opusHead, 0, (IntPtr)ap->extradata, opusHead.Length);
//                 ap->extradata_size = opusHead.Length;
//             }
//
//             // ---- Open output ----
//             if ((_fmt->oformat->flags & AVFMT_NOFILE) == 0)
//             {
//                 ret = avio_open(&_fmt->pb, Path, AVIO_FLAG_WRITE);
//                 if (ret < 0)
//                 {
//                     Console.WriteLine($"[recorder] avio_open failed: {FFErr(ret)}");
//                     _headerFailed = true;
//                     ResetFormat();
//                     return;
//                 }
//             }
//
//             ret = avformat_write_header(_fmt, null);
//             if (ret < 0)
//             {
//                 Console.WriteLine($"[recorder] avformat_write_header failed: {FFErr(ret)}");
//                 _headerFailed = true;
//                 ResetFormat();
//                 return;
//             }
//
//             _pkt = av_packet_alloc();
//             if (_pkt == null)
//             {
//                 Console.WriteLine("[recorder] av_packet_alloc failed");
//                 _headerFailed = true;
//                 ResetFormat();
//                 return;
//             }
//
//             _headerWritten = true;
//
//             Console.WriteLine(
//                 $"[recorder] opened OK ({_videoWidth}x{_videoHeight} @ {TargetFps}fps, video" +
//                 (wantAudio ? " + audio)" : ")"));
//         }
//
//         /// <summary>
//         /// Returns a valid 'OpusHead' blob. Handles: raw OpusHead, scrcpy/Android wrapped
//         /// config ("AOPUSHDR" + len + OpusHead ...), or missing config (builds 48 kHz stereo).
//         /// </summary>
//         private static byte[] NormalizeOpusHead(byte[]? cfg)
//         {
//             if (cfg != null && cfg.Length >= 8)
//             {
//                 string magic = Encoding.ASCII.GetString(cfg, 0, 8);
//
//                 if (magic == "OpusHead")
//                     return cfg;
//
//                 if (magic == "AOPUSHDR" && cfg.Length >= 16)
//                 {
//                     long len = BitConverter.ToInt64(cfg, 8);
//                     if (len > 0 && 16 + len <= cfg.Length)
//                     {
//                         var head = new byte[len];
//                         Buffer.BlockCopy(cfg, 16, head, 0, (int)len);
//                         return head;
//                     }
//                 }
//             }
//
//             Console.WriteLine("[recorder] audio config missing/unknown, using default OpusHead (48kHz stereo)");
//
//             var h = new byte[19];
//             Encoding.ASCII.GetBytes("OpusHead").CopyTo(h, 0);
//             h[8] = 1;                       // version
//             h[9] = 2;                       // channels
//             h[10] = 312 & 0xFF;             // pre-skip (LE)
//             h[11] = (312 >> 8) & 0xFF;
//             h[12] = 48000 & 0xFF;           // input sample rate (LE)
//             h[13] = (48000 >> 8) & 0xFF;
//             h[14] = (48000 >> 16) & 0xFF;
//             h[15] = (48000 >> 24) & 0xFF;
//             h[16] = 0;                      // output gain
//             h[17] = 0;
//             h[18] = 0;                      // mapping family 0
//             return h;
//         }
//
//         // ------------------------------------------------------------------
//         // Packet writes
//         // ------------------------------------------------------------------
//
//         /// <summary>
//         /// Video packets are held back by one so each one is written with its real duration
//         /// (time until the next frame). This keeps 1 fps static screens at the right speed.
//         /// </summary>
//         private void QueueVideo(in Item item)
//         {
//             if (item.PtsUs > _maxPtsUs) _maxPtsUs = item.PtsUs;
//
//             long ticks = UsToTicks(item.PtsUs - _basePtsUs, VideoTimeBase);
//             if (ticks <= _lastVideoDts) ticks = _lastVideoDts + 1;
//             _lastVideoDts = ticks;
//
//             if (_hasHeld && _heldData != null)
//                 EmitVideo(_heldData, _heldPts, _heldKey, Math.Max(1, ticks - _heldPts));
//
//             _heldData = item.Data;
//             _heldPts = ticks;
//             _heldKey = item.IsKeyFrame;
//             _hasHeld = true;
//         }
//
//         private void FlushHeldVideo()
//         {
//             if (!_hasHeld || _heldData == null) return;
//
//             // Hold the last frame until the end of the recording (at least 1/30 s)
//             long endTicks = UsToTicks(_maxPtsUs - _basePtsUs, VideoTimeBase);
//             long dur = Math.Max(FrameTicks, endTicks - _heldPts);
//
//             EmitVideo(_heldData, _heldPts, _heldKey, dur);
//             _hasHeld = false;
//             _heldData = null;
//         }
//
//         private void EmitVideo(byte[] data, long pts, bool key, long duration)
//         {
//             if (_videoStream == null || _pkt == null || _fmt == null) return;
//
//             av_packet_unref(_pkt);
//
//             int ret = av_new_packet(_pkt, data.Length);
//             if (ret < 0)
//             {
//                 Console.WriteLine($"[recorder] av_new_packet(video) failed: {FFErr(ret)}");
//                 return;
//             }
//
//             Marshal.Copy(data, 0, (IntPtr)_pkt->data, data.Length);
//
//             _pkt->pts = pts;
//             _pkt->dts = pts;
//             _pkt->duration = duration;
//             _pkt->stream_index = _videoStream->index;
//             _pkt->flags = key ? AV_PKT_FLAG_KEY : 0;
//
//             ret = av_interleaved_write_frame(_fmt, _pkt);
//             if (ret < 0)
//                 Console.WriteLine($"[recorder] video write failed: {FFErr(ret)}");
//         }
//
//         private void EmitAudio(byte[] data, long pts)
//         {
//             if (_audioStream == null || _pkt == null || _fmt == null) return;
//
//             av_packet_unref(_pkt);
//
//             int ret = av_new_packet(_pkt, data.Length);
//             if (ret < 0)
//             {
//                 Console.WriteLine($"[recorder] av_new_packet(audio) failed: {FFErr(ret)}");
//                 return;
//             }
//
//             Marshal.Copy(data, 0, (IntPtr)_pkt->data, data.Length);
//
//             _pkt->pts = pts;
//             _pkt->dts = pts;
//             _pkt->duration = AudioFrameSamples;
//             _pkt->stream_index = _audioStream->index;
//             _pkt->flags = AV_PKT_FLAG_KEY;
//
//             ret = av_interleaved_write_frame(_fmt, _pkt);
//             if (ret < 0)
//                 Console.WriteLine($"[recorder] audio write failed: {FFErr(ret)}");
//         }
//
//         private static long UsToTicks(long us, int timeBaseDen)
//         {
//             if (us < 0) us = 0;
//             return us * timeBaseDen / UsPerSecond;
//         }
//
//         // ------------------------------------------------------------------
//         // Cleanup
//         // ------------------------------------------------------------------
//
//         private void ResetFormat()
//         {
//             if (_fmt == null) return;
//
//             if ((_fmt->oformat->flags & AVFMT_NOFILE) == 0 && _fmt->pb != null)
//                 avio_closep(&_fmt->pb);
//
//             avformat_free_context(_fmt);
//             _fmt = null;
//             _videoStream = null;
//             _audioStream = null;
//         }
//
//         private void CloseFile()
//         {
//             if (_fmt != null && _headerWritten)
//             {
//                 try { FlushHeldVideo(); } catch { }
//                 try { av_write_trailer(_fmt); } catch { }
//             }
//
//             if (_pkt != null)
//             {
//                 AVPacket* tmp = _pkt;
//                 av_packet_free(&tmp);
//                 _pkt = null;
//             }
//
//             if (_fmt != null)
//             {
//                 if ((_fmt->oformat->flags & AVFMT_NOFILE) == 0 && _fmt->pb != null)
//                     avio_closep(&_fmt->pb);
//
//                 avformat_free_context(_fmt);
//                 _fmt = null;
//             }
//
//             _videoStream = null;
//             _audioStream = null;
//             _headerWritten = false;
//         }
//
//         private static string FFErr(int err)
//         {
//             byte* buf = stackalloc byte[AV_ERROR_MAX_STRING_SIZE];
//             av_strerror(err, buf, AV_ERROR_MAX_STRING_SIZE);
//             return Marshal.PtrToStringAnsi((IntPtr)buf) ?? err.ToString();
//         }
//
//         public void Dispose()
//         {
//             if (_disposed) return;
//             DetachFromCache();
//             _disposed = true;
//
//             try { _queue.Add(new Item(Kind.Stop, Array.Empty<byte>())); }
//             catch (InvalidOperationException) { }
//
//             try { _queue.CompleteAdding(); }
//             catch (ObjectDisposedException) { }
//
//             // Needs time to write the trailer (moov atom); without it the MP4 won't play.
//             if (_writer.IsAlive)
//                 _writer.Join(5000);
//         }
//     }
// }
//
// #endif