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
//         // Output audio format
//         public int SampleRate => _codec_ctx->sample_rate;
//         public int Channels => _codec_ctx->channels;
//         public AVSampleFormat SampleFormat => _codec_ctx->sample_fmt;
//
//         public my_audio()
//         {
//             // Set FFmpeg path
//             ffmpeg.RootPath = Environment.Is64BitProcess ? @"c:\deps\x64" : @"c:\deps\x32";
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
//             if (frame == null) return Array.Empty<byte>();
//
//             int nb_samples = frame->nb_samples;
//             int channels = frame->channels;
//
//             // Assuming AV_SAMPLE_FMT_FLT or AV_SAMPLE_FMT_FLTP (float planar)
//             // We'll convert everything to 16-bit interleaved PCM
//             int totalBytes = nb_samples * channels * 2;
//             byte[] pcm16 = new byte[totalBytes];
//
//             for (int c = 0; c < channels; c++)
//             {
//                 float* src = (float*)frame->extended_data[c];
//                 for (int n = 0; n < nb_samples; n++)
//                 {
//                     int sampleIndex = n * channels + c;
//                     float f = Math.Max(-1.0f, Math.Min(1.0f, src[n]));
//                     short s = (short)(f * short.MaxValue);
//                     pcm16[sampleIndex * 2 + 0] = (byte)(s & 0xFF);
//                     pcm16[sampleIndex * 2 + 1] = (byte)((s >> 8) & 0xFF);
//                 }
//             }
//
//             return pcm16;
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
        private AVCodecParserContext* _parser;

        // Output audio format - hardcoded for scrcpy
        public int SampleRate => 48000; // scrcpy uses 48kHz
        public int Channels => 2;       // scrcpy uses stereo
        public AVSampleFormat SampleFormat => _codec_ctx->sample_fmt;

        public my_audio()
        {
            // Set FFmpeg path
            // ffmpeg.RootPath = Environment.Is64BitProcess ? @"c:\deps\x64" : @"c:\deps\x32";
            
            string ffmpegPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "deps",
                Environment.Is64BitProcess ? "x64" : "x32");

            if (!Directory.Exists(ffmpegPath))
            {
                throw new DirectoryNotFoundException(
                    $"FFmpeg directory not found: {ffmpegPath}");
            }

            ffmpeg.RootPath = ffmpegPath;
            
            Console.WriteLine($"FFmpeg path: {ffmpeg.RootPath}");

            // Find Opus decoder
            _codec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_OPUS);
            if (_codec == null) throw new Exception("Failed to find Opus decoder");

            // Allocate codec context
            _codec_ctx = ffmpeg.avcodec_alloc_context3(_codec);
            if (_codec_ctx == null) throw new Exception("Failed to allocate codec context");

            // Open codec
            if (ffmpeg.avcodec_open2(_codec_ctx, _codec, null) < 0)
                throw new Exception("Failed to open Opus codec");

            // Initialize parser for Opus
            _parser = ffmpeg.av_parser_init((int)AVCodecID.AV_CODEC_ID_OPUS);
            if (_parser == null)
                throw new Exception("Failed to initialize Opus parser");

            Console.WriteLine("Opus decoder initialized successfully");
            Console.WriteLine($"Expected: 48kHz, {Channels} channels");
        }

        /// <summary>
        /// Decodes a raw Opus frame to PCM16 interleaved (suitable for XAudio2)
        /// </summary>
        public byte[]? Decode(byte[] frameData)
        {
            if (_disposed || frameData == null || frameData.Length == 0) 
                return null;

            try
            {
                byte[]? result = null;

                fixed (byte* in_data_ptr = frameData)
                {
                    byte* in_data = in_data_ptr;
                    int in_size = frameData.Length;
                    byte* out_data = null;
                    int out_size = 0;

                    while (in_size > 0)
                    {
                        int consumed = ffmpeg.av_parser_parse2(
                            _parser,
                            _codec_ctx,
                            &out_data,
                            &out_size,
                            in_data,
                            in_size,
                            ffmpeg.AV_NOPTS_VALUE,
                            ffmpeg.AV_NOPTS_VALUE,
                            0
                        );

                        if (consumed < 0)
                        {
                            Console.WriteLine("Parser error");
                            break;
                        }

                        if (out_size > 0)
                        {
                            result = ProcessPacketToPCM(out_data, out_size);
                        }

                        in_data += consumed;
                        in_size -= consumed;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Decode exception: {ex.Message}");
                return null;
            }
        }

        private byte[]? ProcessPacketToPCM(byte* out_data, int out_size)
        {
            AVPacket* packet = ffmpeg.av_packet_alloc();
            if (packet == null) return null;

            try
            {
                packet->data = (byte*)ffmpeg.av_malloc((ulong)out_size);
                if (packet->data == null) return null;

                Buffer.MemoryCopy(out_data, packet->data, out_size, out_size);
                packet->size = out_size;

                int ret = ffmpeg.avcodec_send_packet(_codec_ctx, packet);
                if (ret < 0)
                {
                    Console.WriteLine($"Error sending packet: {ret}");
                    return null;
                }

                AVFrame* frame = ffmpeg.av_frame_alloc();
                if (frame == null) return null;

                try
                {
                    ret = ffmpeg.avcodec_receive_frame(_codec_ctx, frame);
                    if (ret == 0)
                    {
                        return ConvertFrameToPCM16(frame);
                    }
                    else if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                    {
                        return null; // need more data
                    }
                    else
                    {
                        Console.WriteLine($"Error receiving frame: {ret}");
                        return null;
                    }
                }
                finally
                {
                    ffmpeg.av_frame_free(&frame);
                }
            }
            finally
            {
                if (packet->data != null)
                    ffmpeg.av_free(packet->data);
                ffmpeg.av_packet_free(&packet);
            }
        }

        /// <summary>
        /// Converts an AVFrame to 16-bit interleaved PCM for XAudio2 playback
        /// </summary>
        private byte[] ConvertFrameToPCM16(AVFrame* frame)
        {
            if (frame == null || frame->nb_samples <= 0) 
                return Array.Empty<byte>();

            int nb_samples = frame->nb_samples;
            int channels = 2; // Hardcoded for scrcpy stereo

            // Convert to 16-bit interleaved PCM
            int totalBytes = nb_samples * channels * 2; // 2 bytes per sample (16-bit)
            byte[] pcm16 = new byte[totalBytes];

            // Handle different sample formats that Opus might output
            switch ((AVSampleFormat)frame->format)
            {
                case AVSampleFormat.AV_SAMPLE_FMT_FLT:    // float, interleaved
                case AVSampleFormat.AV_SAMPLE_FMT_FLTP:   // float, planar
                    ConvertFloatToPCM16(frame, pcm16, nb_samples, channels);
                    break;
                case AVSampleFormat.AV_SAMPLE_FMT_S16:    // 16-bit, interleaved
                case AVSampleFormat.AV_SAMPLE_FMT_S16P:   // 16-bit, planar
                    ConvertS16ToPCM16(frame, pcm16, nb_samples, channels);
                    break;
                default:
                    Console.WriteLine($"Unsupported sample format: {frame->format}");
                    return Array.Empty<byte>();
            }

            return pcm16;
        }

        private void ConvertFloatToPCM16(AVFrame* frame, byte[] pcm16, int nb_samples, int channels)
        {
            if (ffmpeg.av_sample_fmt_is_planar((AVSampleFormat)frame->format) != 0)
            {
                // Planar format (FLTP)
                for (int c = 0; c < channels; c++)
                {
                    float* src = (float*)frame->extended_data[c];
                    for (int n = 0; n < nb_samples; n++)
                    {
                        int sampleIndex = n * channels + c;
                        float f = Math.Max(-1.0f, Math.Min(1.0f, src[n]));
                        short s = (short)(f * short.MaxValue);
                        pcm16[sampleIndex * 2] = (byte)(s & 0xFF);
                        pcm16[sampleIndex * 2 + 1] = (byte)((s >> 8) & 0xFF);
                    }
                }
            }
            else
            {
                // Interleaved format (FLT)
                float* src = (float*)frame->data[0];
                for (int i = 0; i < nb_samples * channels; i++)
                {
                    float f = Math.Max(-1.0f, Math.Min(1.0f, src[i]));
                    short s = (short)(f * short.MaxValue);
                    pcm16[i * 2] = (byte)(s & 0xFF);
                    pcm16[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
                }
            }
        }

        private void ConvertS16ToPCM16(AVFrame* frame, byte[] pcm16, int nb_samples, int channels)
        {
            if (ffmpeg.av_sample_fmt_is_planar((AVSampleFormat)frame->format) != 0)
            {
                // Planar format (S16P)
                for (int c = 0; c < channels; c++)
                {
                    short* src = (short*)frame->extended_data[c];
                    for (int n = 0; n < nb_samples; n++)
                    {
                        int sampleIndex = n * channels + c;
                        short s = src[n];
                        pcm16[sampleIndex * 2] = (byte)(s & 0xFF);
                        pcm16[sampleIndex * 2 + 1] = (byte)((s >> 8) & 0xFF);
                    }
                }
            }
            else
            {
                // Interleaved format (S16) - just copy
                Marshal.Copy((IntPtr)frame->data[0], pcm16, 0, pcm16.Length);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_codec_ctx != null)
                {
                    ffmpeg.avcodec_close(_codec_ctx);
                    AVCodecContext* ctx = _codec_ctx;
                    ffmpeg.avcodec_free_context(&ctx);
                    _codec_ctx = null;
                }

                if (_parser != null)
                {
                    ffmpeg.av_parser_close(_parser);
                    _parser = null;
                }

                _disposed = true;
                Console.WriteLine("Opus decoder disposed");
            }
        }
    }
}