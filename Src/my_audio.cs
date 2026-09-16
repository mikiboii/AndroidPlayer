//
// using System;
// using System.IO;
// using System.Runtime.InteropServices;
// using FFmpeg.AutoGen;
// using SharpDX.XAudio2;
//
// namespace Androidplayer
// {
//     public unsafe class my_audio : IDisposable
//     {
//         private bool _disposed = false;
//
//         private AVCodec* _codec;
//         private AVCodecContext* _codec_ctx;
//         private AVCodecParserContext* _parser;
//
//         // Output audio format - hardcoded for scrcpy
//         public int SampleRate => 48000; // scrcpy uses 48kHz
//         public int Channels => 2;       // scrcpy uses stereo
//         public AVSampleFormat SampleFormat => _codec_ctx->sample_fmt;
//
//         public my_audio()
//         {
//             // Set FFmpeg path
//             // ffmpeg.RootPath = Environment.Is64BitProcess ? @"c:\deps\x64" : @"c:\deps\x32";
//             
//             string ffmpegPath = Path.Combine(
//                 Directory.GetCurrentDirectory(),
//                 "deps",
//                 Environment.Is64BitProcess ? "x64" : "x32");
//
//             if (!Directory.Exists(ffmpegPath))
//             {
//                 throw new DirectoryNotFoundException(
//                     $"FFmpeg directory not found: {ffmpegPath}");
//             }
//
//             ffmpeg.RootPath = ffmpegPath;
//             
//             Console.WriteLine($"FFmpeg path: {ffmpeg.RootPath}");
//
//             // Find Opus decoder
//             _codec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_OPUS);
//             if (_codec == null) throw new Exception("Failed to find Opus decoder");
//
//             // Allocate codec context
//             _codec_ctx = ffmpeg.avcodec_alloc_context3(_codec);
//             if (_codec_ctx == null) throw new Exception("Failed to allocate codec context");
//
//             // Open codec
//             if (ffmpeg.avcodec_open2(_codec_ctx, _codec, null) < 0)
//                 throw new Exception("Failed to open Opus codec");
//
//             // Initialize parser for Opus
//             _parser = ffmpeg.av_parser_init((int)AVCodecID.AV_CODEC_ID_OPUS);
//             if (_parser == null)
//                 throw new Exception("Failed to initialize Opus parser");
//
//             Console.WriteLine("Opus decoder initialized successfully");
//             Console.WriteLine($"Expected: 48kHz, {Channels} channels");
//         }
//
//         /// <summary>
//         /// Decodes a raw Opus frame to PCM16 interleaved (suitable for XAudio2)
//         /// </summary>
//         public byte[]? Decode(byte[] frameData)
//         {
//             if (_disposed || frameData == null || frameData.Length == 0) 
//                 return null;
//
//             try
//             {
//                 byte[]? result = null;
//
//                 fixed (byte* in_data_ptr = frameData)
//                 {
//                     byte* in_data = in_data_ptr;
//                     int in_size = frameData.Length;
//                     byte* out_data = null;
//                     int out_size = 0;
//
//                     while (in_size > 0)
//                     {
//                         int consumed = ffmpeg.av_parser_parse2(
//                             _parser,
//                             _codec_ctx,
//                             &out_data,
//                             &out_size,
//                             in_data,
//                             in_size,
//                             ffmpeg.AV_NOPTS_VALUE,
//                             ffmpeg.AV_NOPTS_VALUE,
//                             0
//                         );
//
//                         if (consumed < 0)
//                         {
//                             Console.WriteLine("Parser error");
//                             break;
//                         }
//
//                         if (out_size > 0)
//                         {
//                             result = ProcessPacketToPCM(out_data, out_size);
//                         }
//
//                         in_data += consumed;
//                         in_size -= consumed;
//                     }
//                 }
//
//                 return result;
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"Decode exception: {ex.Message}");
//                 return null;
//             }
//         }
//
//         private byte[]? ProcessPacketToPCM(byte* out_data, int out_size)
//         {
//             AVPacket* packet = ffmpeg.av_packet_alloc();
//             if (packet == null) return null;
//
//             try
//             {
//                 packet->data = (byte*)ffmpeg.av_malloc((ulong)out_size);
//                 if (packet->data == null) return null;
//
//                 Buffer.MemoryCopy(out_data, packet->data, out_size, out_size);
//                 packet->size = out_size;
//
//                 int ret = ffmpeg.avcodec_send_packet(_codec_ctx, packet);
//                 if (ret < 0)
//                 {
//                     Console.WriteLine($"Error sending packet: {ret}");
//                     return null;
//                 }
//
//                 AVFrame* frame = ffmpeg.av_frame_alloc();
//                 if (frame == null) return null;
//
//                 try
//                 {
//                     ret = ffmpeg.avcodec_receive_frame(_codec_ctx, frame);
//                     if (ret == 0)
//                     {
//                         return ConvertFrameToPCM16(frame);
//                     }
//                     else if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN))
//                     {
//                         return null; // need more data
//                     }
//                     else
//                     {
//                         Console.WriteLine($"Error receiving frame: {ret}");
//                         return null;
//                     }
//                 }
//                 finally
//                 {
//                     ffmpeg.av_frame_free(&frame);
//                 }
//             }
//             finally
//             {
//                 if (packet->data != null)
//                     ffmpeg.av_free(packet->data);
//                 ffmpeg.av_packet_free(&packet);
//             }
//         }
//
//         /// <summary>
//         /// Converts an AVFrame to 16-bit interleaved PCM for XAudio2 playback
//         /// </summary>
//         private byte[] ConvertFrameToPCM16(AVFrame* frame)
//         {
//             if (frame == null || frame->nb_samples <= 0) 
//                 return Array.Empty<byte>();
//
//             int nb_samples = frame->nb_samples;
//             int channels = 2; // Hardcoded for scrcpy stereo
//
//             // Convert to 16-bit interleaved PCM
//             int totalBytes = nb_samples * channels * 2; // 2 bytes per sample (16-bit)
//             byte[] pcm16 = new byte[totalBytes];
//
//             // Handle different sample formats that Opus might output
//             switch ((AVSampleFormat)frame->format)
//             {
//                 case AVSampleFormat.AV_SAMPLE_FMT_FLT:    // float, interleaved
//                 case AVSampleFormat.AV_SAMPLE_FMT_FLTP:   // float, planar
//                     ConvertFloatToPCM16(frame, pcm16, nb_samples, channels);
//                     break;
//                 case AVSampleFormat.AV_SAMPLE_FMT_S16:    // 16-bit, interleaved
//                 case AVSampleFormat.AV_SAMPLE_FMT_S16P:   // 16-bit, planar
//                     ConvertS16ToPCM16(frame, pcm16, nb_samples, channels);
//                     break;
//                 default:
//                     Console.WriteLine($"Unsupported sample format: {frame->format}");
//                     return Array.Empty<byte>();
//             }
//
//             return pcm16;
//         }
//
//         private void ConvertFloatToPCM16(AVFrame* frame, byte[] pcm16, int nb_samples, int channels)
//         {
//             if (ffmpeg.av_sample_fmt_is_planar((AVSampleFormat)frame->format) != 0)
//             {
//                 // Planar format (FLTP)
//                 for (int c = 0; c < channels; c++)
//                 {
//                     float* src = (float*)frame->extended_data[c];
//                     for (int n = 0; n < nb_samples; n++)
//                     {
//                         int sampleIndex = n * channels + c;
//                         float f = Math.Max(-1.0f, Math.Min(1.0f, src[n]));
//                         short s = (short)(f * short.MaxValue);
//                         pcm16[sampleIndex * 2] = (byte)(s & 0xFF);
//                         pcm16[sampleIndex * 2 + 1] = (byte)((s >> 8) & 0xFF);
//                     }
//                 }
//             }
//             else
//             {
//                 // Interleaved format (FLT)
//                 float* src = (float*)frame->data[0];
//                 for (int i = 0; i < nb_samples * channels; i++)
//                 {
//                     float f = Math.Max(-1.0f, Math.Min(1.0f, src[i]));
//                     short s = (short)(f * short.MaxValue);
//                     pcm16[i * 2] = (byte)(s & 0xFF);
//                     pcm16[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
//                 }
//             }
//         }
//
//         private void ConvertS16ToPCM16(AVFrame* frame, byte[] pcm16, int nb_samples, int channels)
//         {
//             if (ffmpeg.av_sample_fmt_is_planar((AVSampleFormat)frame->format) != 0)
//             {
//                 // Planar format (S16P)
//                 for (int c = 0; c < channels; c++)
//                 {
//                     short* src = (short*)frame->extended_data[c];
//                     for (int n = 0; n < nb_samples; n++)
//                     {
//                         int sampleIndex = n * channels + c;
//                         short s = src[n];
//                         pcm16[sampleIndex * 2] = (byte)(s & 0xFF);
//                         pcm16[sampleIndex * 2 + 1] = (byte)((s >> 8) & 0xFF);
//                     }
//                 }
//             }
//             else
//             {
//                 // Interleaved format (S16) - just copy
//                 Marshal.Copy((IntPtr)frame->data[0], pcm16, 0, pcm16.Length);
//             }
//         }
//
//         public void Dispose()
//         {
//             if (!_disposed)
//             {
//                 if (_codec_ctx != null)
//                 {
//                     ffmpeg.avcodec_close(_codec_ctx);
//                     AVCodecContext* ctx = _codec_ctx;
//                     ffmpeg.avcodec_free_context(&ctx);
//                     _codec_ctx = null;
//                 }
//
//                 if (_parser != null)
//                 {
//                     ffmpeg.av_parser_close(_parser);
//                     _parser = null;
//                 }
//
//                 _disposed = true;
//                 Console.WriteLine("Opus decoder disposed");
//             }
//         }
//     }
// }




using System;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using SharpDX.XAudio2;

namespace Androidplayer
{
    public unsafe class my_audio : IDisposable
    {
        private bool _disposed = false;

        private AVCodec* _codec;
        private AVCodecContext* _codec_ctx;

        // scrcpy 3.3.2 server: AudioConfig.SAMPLE_RATE = 48000, CHANNELS = 2
        public int SampleRate => 48000;
        public int Channels   => 2;

        private byte[] _pendingExtradata = null;
        private const int AV_INPUT_BUFFER_PADDING_SIZE = 64;

        public my_audio()
        {
            string ffmpegPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "deps",
                Environment.Is64BitProcess ? "x64" : "x32");

            if (!Directory.Exists(ffmpegPath))
                throw new DirectoryNotFoundException(
                    $"FFmpeg directory not found: {ffmpegPath}");

            ffmpeg.RootPath = ffmpegPath;
            Console.WriteLine($"FFmpeg path: {ffmpeg.RootPath}");

            _codec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_OPUS);
            if (_codec == null)
                throw new Exception("Failed to find Opus decoder");

            AllocContext();
        }

        private void AllocContext()
        {
            _codec_ctx = ffmpeg.avcodec_alloc_context3(_codec);
            if (_codec_ctx == null)
                throw new Exception("Failed to allocate codec context");

            // ---------------------------------------------------------
            // Opus parameters must be set BEFORE avcodec_open2.
            // scrcpy 3.3.2 sends 48 kHz stereo.
            // ---------------------------------------------------------
            _codec_ctx->sample_rate = SampleRate;
            _codec_ctx->ch_layout.nb_channels = Channels;
            _codec_ctx->ch_layout.order = AVChannelOrder.AV_CHANNEL_ORDER_UNSPEC;
        }

        /// <summary>
        /// THE REAL open-state check. avcodec_alloc_context3(codec) sets
        /// ctx->codec immediately on allocation, BEFORE the codec is
        /// actually opened — so checking ctx->codec != null is always
        /// true and is NOT a valid "is open" test. avcodec_is_open() is
        /// the correct FFmpeg API for this.
        /// </summary>
        private bool IsOpen()
        {
            return _codec_ctx != null && ffmpeg.avcodec_is_open(_codec_ctx) != 0;
        }

        /// <summary>
        /// Called by start_audio when a PACKET_FLAG_CONFIG frame arrives.
        /// This is Opus extradata (OpusHead). We set it on the codec
        /// context and (re)open the decoder.
        /// </summary>
        public void SetExtradata(byte[] extradata)
        {
            if (extradata == null || extradata.Length == 0)
                return;

            _pendingExtradata = extradata;

            // If the codec was already opened (e.g. Decode() fell back to
            // opening without extradata before this config packet arrived),
            // we must free and reallocate the context — reusing an opened
            // AVCodecContext to change extradata isn't reliably supported.
            if (IsOpen())
            {
                AVCodecContext* oldCtx = _codec_ctx;
                ffmpeg.avcodec_free_context(&oldCtx);
                AllocContext();
            }

            if (_codec_ctx->extradata != null)
            {
                ffmpeg.av_freep(&_codec_ctx->extradata);
                _codec_ctx->extradata_size = 0;
            }

            // Copy extradata into FFmpeg-owned memory (with required padding)
            _codec_ctx->extradata =
                (byte*)ffmpeg.av_mallocz((ulong)(extradata.Length + AV_INPUT_BUFFER_PADDING_SIZE));

            fixed (byte* src = extradata)
            {
                Buffer.MemoryCopy(
                    src,
                    _codec_ctx->extradata,
                    extradata.Length,
                    extradata.Length);
            }

            _codec_ctx->extradata_size = extradata.Length;

            int ret = ffmpeg.avcodec_open2(_codec_ctx, _codec, null);
            if (ret < 0)
                throw new Exception($"Failed to open Opus codec: {ret}");

            Console.WriteLine(
                $"Opus decoder opened with extradata ({extradata.Length} bytes). " +
                $"Expected: {SampleRate} Hz, {Channels} ch");
        }

        public byte[]? Decode(byte[] frameData)
        {
            if (_disposed || frameData == null || frameData.Length == 0)
                return null;

            // If SetExtradata hasn't been called yet, open on first frame
            // so we don't just drop everything.
            if (!IsOpen())
            {
                int openRet = ffmpeg.avcodec_open2(_codec_ctx, _codec, null);
                if (openRet < 0)
                {
                    Console.WriteLine($"Failed to open Opus codec: {openRet}");
                    return null;
                }
                Console.WriteLine("Opus decoder opened without extradata (fallback)");
            }

            AVPacket* packet = ffmpeg.av_packet_alloc();
            if (packet == null) return null;

            AVFrame* frame = null;

            try
            {
                // av_new_packet allocates the buffer WITH the padding FFmpeg's
                // bitstream readers require past the end of real data, and
                // sets packet->buf so av_packet_free cleans it up correctly.
                int ret = ffmpeg.av_new_packet(packet, frameData.Length);
                if (ret < 0)
                {
                    Console.WriteLine($"Failed to allocate packet: {ret}");
                    return null;
                }

                fixed (byte* src = frameData)
                {
                    Buffer.MemoryCopy(
                        src, packet->data,
                        frameData.Length, frameData.Length);
                }

                ret = ffmpeg.avcodec_send_packet(_codec_ctx, packet);
                if (ret < 0)
                {
                    Console.WriteLine($"Error sending packet: {ret}");
                    return null;
                }

                frame = ffmpeg.av_frame_alloc();
                if (frame == null) return null;

                ret = ffmpeg.avcodec_receive_frame(_codec_ctx, frame);
                if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                    return null;
                if (ret < 0)
                {
                    Console.WriteLine($"Error receiving frame: {ret}");
                    return null;
                }

                return ConvertFrameToPCM16(frame);
            }
            finally
            {
                // packet->data is now owned via packet->buf (from av_new_packet),
                // so av_packet_free handles freeing it — no manual av_free needed.
                ffmpeg.av_packet_free(&packet);

                if (frame != null)
                    ffmpeg.av_frame_free(&frame);
            }
        }

        private byte[] ConvertFrameToPCM16(AVFrame* frame)
        {
            if (frame == null || frame->nb_samples <= 0)
                return Array.Empty<byte>();

            int nb_samples = frame->nb_samples;
            int channels   = frame->ch_layout.nb_channels;
            if (channels <= 0) channels = Channels;

            int totalBytes = nb_samples * channels * 2;
            byte[] pcm16 = new byte[totalBytes];

            switch ((AVSampleFormat)frame->format)
            {
                case AVSampleFormat.AV_SAMPLE_FMT_FLT:
                case AVSampleFormat.AV_SAMPLE_FMT_FLTP:
                    ConvertFloatToPCM16(frame, pcm16, nb_samples, channels);
                    break;
                case AVSampleFormat.AV_SAMPLE_FMT_S16:
                case AVSampleFormat.AV_SAMPLE_FMT_S16P:
                    ConvertS16ToPCM16(frame, pcm16, nb_samples, channels);
                    break;
                default:
                    Console.WriteLine($"Unsupported sample format: {frame->format}");
                    return Array.Empty<byte>();
            }

            return pcm16;
        }

        private void ConvertFloatToPCM16(
            AVFrame* frame, byte[] pcm16, int nb_samples, int channels)
        {
            if (ffmpeg.av_sample_fmt_is_planar((AVSampleFormat)frame->format) != 0)
            {
                for (int c = 0; c < channels; c++)
                {
                    float* src = (float*)frame->extended_data[c];
                    for (int n = 0; n < nb_samples; n++)
                    {
                        int sampleIndex = n * channels + c;
                        float f = Math.Max(-1.0f, Math.Min(1.0f, src[n]));
                        short s = (short)(f * short.MaxValue);
                        pcm16[sampleIndex * 2]     = (byte)(s & 0xFF);
                        pcm16[sampleIndex * 2 + 1] = (byte)((s >> 8) & 0xFF);
                    }
                }
            }
            else
            {
                float* src = (float*)frame->data[0];
                for (int i = 0; i < nb_samples * channels; i++)
                {
                    float f = Math.Max(-1.0f, Math.Min(1.0f, src[i]));
                    short s = (short)(f * short.MaxValue);
                    pcm16[i * 2]     = (byte)(s & 0xFF);
                    pcm16[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
                }
            }
        }

        private void ConvertS16ToPCM16(
            AVFrame* frame, byte[] pcm16, int nb_samples, int channels)
        {
            if (ffmpeg.av_sample_fmt_is_planar((AVSampleFormat)frame->format) != 0)
            {
                for (int c = 0; c < channels; c++)
                {
                    short* src = (short*)frame->extended_data[c];
                    for (int n = 0; n < nb_samples; n++)
                    {
                        int sampleIndex = n * channels + c;
                        short s = src[n];
                        pcm16[sampleIndex * 2]     = (byte)(s & 0xFF);
                        pcm16[sampleIndex * 2 + 1] = (byte)((s >> 8) & 0xFF);
                    }
                }
            }
            else
            {
                Marshal.Copy((IntPtr)frame->data[0], pcm16, 0, pcm16.Length);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            if (_codec_ctx != null)
            {
                if (IsOpen())
                {
                    // Flush: send NULL packet, drain remaining frames
                    ffmpeg.avcodec_send_packet(_codec_ctx, null);
                    AVFrame* flushFrame = ffmpeg.av_frame_alloc();
                    if (flushFrame != null)
                    {
                        while (ffmpeg.avcodec_receive_frame(_codec_ctx, flushFrame) == 0)
                        {
                            // discard
                        }
                        ffmpeg.av_frame_free(&flushFrame);
                    }

                    ffmpeg.avcodec_close(_codec_ctx);
                }

                AVCodecContext* ctx = _codec_ctx;
                ffmpeg.avcodec_free_context(&ctx);
                _codec_ctx = null;
            }

            _disposed = true;
            Console.WriteLine("Opus decoder disposed");
        }
    }
}