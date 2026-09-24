


using System;
using System.IO;
using Androidplayer.Src.Keymap.K_store;
using FFmpeg.AutoGen;





using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;



using static FFmpeg.AutoGen.ffmpeg;

namespace Androidplayer;

public unsafe class my_AV : IDisposable
{
    private AVCodec* codec;
    private AVCodecContext* codecCtx;
    private AVBufferRef* hwDeviceCtx = null;

    private Device device;

    private Texture2D hwTexture = null;
    private Texture2D ffmpegTexture = null;

    private bool hwInitialized = false;
    private bool useSoftwareFallback = false;

    /*
     * -------------------------------------------------------------
     * SCRCPY CONFIG PACKET
     *
     * scrcpy 3.3.2 stores the H264 config packet and prepends it
     * to the next non-config packet.
     *
     * We reproduce that behavior here.
     * -------------------------------------------------------------
     */
    private byte[] configPacket = null;
    
   

    public long FrameCount { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public my_AV(Device device)
    {
        this.device = device ?? throw new ArgumentNullException(nameof(device));

        string ffmpegPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "deps",
            Environment.Is64BitProcess ? "x64" : "x32");

        if (!Directory.Exists(ffmpegPath))
        {
            throw new DirectoryNotFoundException(
                $"FFmpeg directory not found: {ffmpegPath}");
        }

        // ffmpeg.RootPath = ffmpegPath;
        ffmpeg.RootPath = AppContext.BaseDirectory;

        codec = avcodec_find_decoder(
            AVCodecID.AV_CODEC_ID_H264);

        if (codec == null)
            throw new Exception("H264 codec not found");

        codecCtx = avcodec_alloc_context3(codec);

        if (codecCtx == null)
            throw new Exception(
                "Failed to allocate codec context");

        /*
         * ---------------------------------------------------------
         * SAME LOW-LATENCY FLAG AS SCRCPY
         * ---------------------------------------------------------
         */
        codecCtx->flags |= AV_CODEC_FLAG_LOW_DELAY;

        /*
         * Keep your existing low-latency settings.
         */
        codecCtx->flags2 |= AV_CODEC_FLAG2_FAST;
        codecCtx->skip_frame = AVDiscard.AVDISCARD_DEFAULT;
        codecCtx->skip_loop_filter = AVDiscard.AVDISCARD_DEFAULT;
        codecCtx->refs = 1;

        /*
         * ---------------------------------------------------------
         * TRY D3D11VA
         * ---------------------------------------------------------
         */
        try
        {
            hwDeviceCtx = av_hwdevice_ctx_alloc(
                AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA);

            if (hwDeviceCtx == null)
            {
                Console.WriteLine(
                    "Failed to allocate D3D11VA device, using software fallback");

                useSoftwareFallback = true;
            }
            else
            {
                AVHWDeviceContext* devCtx =
                    (AVHWDeviceContext*)hwDeviceCtx->data;

                AVD3D11VADeviceContext* d3d11 =
                    (AVD3D11VADeviceContext*)devCtx->hwctx;

                d3d11->device =
                    (ID3D11Device*)device.NativePointer;

                int ret =
                    av_hwdevice_ctx_init(hwDeviceCtx);

                if (ret < 0)
                {
                    Console.WriteLine(
                        $"HW device init failed: {ret}, using software fallback");

                    useSoftwareFallback = true;

                    hwDeviceCtx = null;
                }
                else
                {
                    hwInitialized = true;

                    Console.WriteLine(
                        "D3D11VA hardware acceleration initialized");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"HW acceleration setup failed: {ex.Message}, using software fallback");

            useSoftwareFallback = true;

            if (hwDeviceCtx != null)
            {
                hwDeviceCtx = null;
            }
        }

        if (hwInitialized && hwDeviceCtx != null)
        {
            codecCtx->hw_device_ctx =
                av_buffer_ref(hwDeviceCtx);
        }

        /*
         * ---------------------------------------------------------
         * OPEN H264 DECODER
         * ---------------------------------------------------------
         */
        if (avcodec_open2(codecCtx, codec, null) < 0)
            throw new Exception("Failed to open codec");

        Console.WriteLine(
            $"Decoder initialized. HW: {hwInitialized}, SW Fallback: {useSoftwareFallback}");
    }


    // =============================================================
    // PUBLIC API
    //
    // KEEPING YOUR EXISTING PUBLIC FUNCTION
    // =============================================================
    // public Texture2D Decode(byte[] h264Data)
    // {
    //     /*
    //      * Normal media packet.
    //      *
    //      * This preserves your existing public API.
    //      */
    //     return DecodeInternal(
    //         h264Data,
    //         isConfig: false);
    // }


    
    public Texture2D Decode(byte[] h264Data)
    {
        if (h264Data == null ||
            h264Data.Length == 0)
        {
            return null;
        }

        return DecodeH264Packet(
            h264Data,
            0);
    }
    
    
    
    public Texture2D DecodePacket(
        byte[] h264Data,
        long pts,
        bool isConfig)
    {
        if (h264Data == null ||
            h264Data.Length == 0)
        {
            return null;
        }

        // ---------------------------------------------------------
        // SCRCPY CONFIG PACKET
        //
        // Do not send the config packet by itself.
        // Save it for the next H264 packet.
        // ---------------------------------------------------------
        if (isConfig)
        {
            configPacket =
                new byte[h264Data.Length];

            System.Buffer.BlockCopy(
                h264Data,
                0,
                configPacket,
                0,
                h264Data.Length);

            Console.WriteLine(
                $"Stored H264 config packet: {configPacket.Length} bytes");

            return null;
        }

        // ---------------------------------------------------------
        // SCRCPY PACKET MERGER
        //
        // config + normal H264 packet
        // ---------------------------------------------------------
        byte[] packetData;

        if (configPacket != null)
        {
            packetData =
                new byte[
                    configPacket.Length +
                    h264Data.Length];

            System.Buffer.BlockCopy(
                configPacket,
                0,
                packetData,
                0,
                configPacket.Length);

            System.Buffer.BlockCopy(
                h264Data,
                0,
                packetData,
                configPacket.Length,
                h264Data.Length);

            Console.WriteLine(
                $"Merged config ({configPacket.Length}) + " +
                $"H264 ({h264Data.Length}) = " +
                $"{packetData.Length} bytes");

            configPacket = null;
        }
        else
        {
            packetData = h264Data;
        }

        return DecodeH264Packet(
            packetData,
            pts);
    }
    
    
    
    private Texture2D DecodeH264Packet(
    byte[] h264Data,
    long pts)
{
    AVPacket* packet =
        av_packet_alloc();

    if (packet == null)
        return null;

    Texture2D lastTexture = null;

    try
    {
        fixed (byte* pData = h264Data)
        {
            packet->data = pData;
            packet->size = h264Data.Length;

            packet->pts = pts;
            packet->dts = pts;

            int ret =
                avcodec_send_packet(
                    codecCtx,
                    packet);

            if (ret < 0 &&
                ret != AVERROR(EAGAIN))
            {
                Console.WriteLine(
                    $"avcodec_send_packet failed: " +
                    $"{ret} ({FFmpegError(ret)})");

                return null;
            }

            while (true)
            {
                AVFrame* frame =
                    av_frame_alloc();

                if (frame == null)
                    break;

                try
                {
                    ret =
                        avcodec_receive_frame(
                            codecCtx,
                            frame);

                    if (ret == AVERROR(EAGAIN) ||
                        ret == AVERROR_EOF)
                    {
                        break;
                    }

                    if (ret < 0)
                    {
                        Console.WriteLine(
                            $"avcodec_receive_frame failed: " +
                            $"{ret} ({FFmpegError(ret)})");

                        break;
                    }

                    Texture2D texture;

                    bool isD3D11Frame =
                        frame->format ==
                        (int)AVPixelFormat.AV_PIX_FMT_D3D11;

                    if (hwInitialized &&
                        isD3D11Frame &&
                        frame->data[0] != null)
                    {
                        texture =
                            ConvertD3D11FrameToTexture(
                                frame);
                    }
                    else
                    {
                        texture =
                            ConvertFrameToTexture(
                                frame);
                    }

                    if (texture != null)
                    {
                        lastTexture?.Dispose();

                        lastTexture = texture;

                        FrameCount++;

                        Width = frame->width;
                        Height = frame->height;
                    }
                    
                    // Console.WriteLine(
                    //     $"Decoded frame: {frame->width}x{frame->height}, " +
                    //     $"PixelFormat={(AVPixelFormat)frame->format}, " +
                    //     $"HW={(frame->format == (int)AVPixelFormat.AV_PIX_FMT_D3D11)}");
                }
                finally
                {
                    av_frame_free(
                        &frame);
                }
            }
        }
    }
    finally
    {
        av_packet_unref(packet);
        av_packet_free(&packet);
    }

    return lastTexture;
}
    
    
    
    
    
    
    
    
    
    
    
    // =============================================================
    // INTERNAL SCRCPY PACKET ENTRY POINT
    //
    // ReceiveVideoData() should call this for config packets.
    //
    // This method does NOT change your existing public API.
    // =============================================================
    internal Texture2D DecodeConfig(byte[] h264Data)
    {
        if (h264Data == null || h264Data.Length == 0)
            return null;

        /*
         * This is equivalent to scrcpy packet_merger:
         *
         * merger->config = copy(packet->data)
         */
        configPacket = new byte[h264Data.Length];

        System.Buffer.BlockCopy(
            h264Data,
            0,
            configPacket,
            0,
            h264Data.Length);

        return null;
    }


    // =============================================================
    // INTERNAL DECODE
    // =============================================================
    private Texture2D DecodeInternal(
        byte[] h264Data,
        bool isConfig)
    {
        if (h264Data == null ||
            h264Data.Length == 0)
        {
            return null;
        }

        /*
         * ---------------------------------------------------------
         * If this is a config packet, store it and DO NOT send it
         * to FFmpeg yet.
         *
         * This matches scrcpy packet_merger.c.
         * ---------------------------------------------------------
         */
        if (isConfig)
        {
            DecodeConfig(h264Data);
            return null;
        }

        /*
         * ---------------------------------------------------------
         * If we have a config packet, prepend it to this media
         * packet.
         *
         * This is exactly what scrcpy packet_merger_merge()
         * does for H.264/H.265.
         * ---------------------------------------------------------
         */
        byte[] packetData;

        if (configPacket != null)
        {
            packetData =
                new byte[
                    configPacket.Length +
                    h264Data.Length];

            System.Buffer.BlockCopy(
                configPacket,
                0,
                packetData,
                0,
                configPacket.Length);

            System.Buffer.BlockCopy(
                h264Data,
                0,
                packetData,
                configPacket.Length,
                h264Data.Length);

            /*
             * scrcpy clears merger->config after merging.
             */
            configPacket = null;
        }
        else
        {
            packetData = h264Data;
        }

        AVPacket* packet = av_packet_alloc();

        if (packet == null)
            return null;

        Texture2D lastTexture = null;

        try
        {
            /*
             * -----------------------------------------------------
             * Pin packet data only for the duration of send/receive.
             * -----------------------------------------------------
             */
            fixed (byte* pData = packetData)
            {
                packet->data = pData;
                packet->size = packetData.Length;

                /*
                 * We do not currently have the real scrcpy PTS
                 * passed into Decode().
                 *
                 * The important part for H264 config handling is
                 * that the config was already merged above.
                 */
                packet->pts = 0;
                packet->dts = 0;

                /*
                 * -------------------------------------------------
                 * EXACT SAME CORE CALL AS SCRCPY
                 * -------------------------------------------------
                 */
                int ret =
                    avcodec_send_packet(
                        codecCtx,
                        packet);

                /*
                 * Same logic as scrcpy:
                 *
                 * EAGAIN is not fatal.
                 */
                if (ret < 0 &&
                    ret != AVERROR(EAGAIN))
                {
                    // Console.WriteLine(
                    //     $"avcodec_send_packet failed: {ret}");
                    
                    Console.WriteLine(
                        $"avcodec_send_packet failed: {ret} ({FFmpegError(ret)})");

                    return null;
                }

                /*
                 * -------------------------------------------------
                 * RECEIVE ALL AVAILABLE FRAMES
                 * -------------------------------------------------
                 */
                while (true)
                {
                    AVFrame* frame =
                        av_frame_alloc();

                    if (frame == null)
                        break;

                    try
                    {
                        ret =
                            avcodec_receive_frame(
                                codecCtx,
                                frame);

                        if (ret == AVERROR(EAGAIN) ||
                            ret == AVERROR_EOF)
                        {
                            break;
                        }

                        if (ret < 0)
                        {
                            Console.WriteLine(
                                $"avcodec_receive_frame failed: {ret}");

                            break;
                        }

                        Texture2D texture;

                        bool isD3D11Frame =
                            frame->format ==
                            (int)AVPixelFormat.AV_PIX_FMT_D3D11;

                        /*
                         * -------------------------------------------------
                         * GPU
                         * -------------------------------------------------
                         */
                        if (hwInitialized &&
                            isD3D11Frame &&
                            frame->data[0] != null)
                        {
                            texture =
                                ConvertD3D11FrameToTexture(frame);
                        }
                        else
                        {
                            /*
                             * -------------------------------------------------
                             * CPU FALLBACK
                             * -------------------------------------------------
                             */
                            texture =
                                ConvertFrameToTexture(frame);
                        }

                        if (texture != null)
                        {
                            lastTexture?.Dispose();

                            lastTexture = texture;

                            FrameCount++;

                            Width = frame->width;
                            Height = frame->height;
                        }
                    }
                    finally
                    {
                        av_frame_free(&frame);
                    }
                }
            }
        }
        finally
        {
            /*
             * packet->data points to the pinned managed array.
             *
             * packet->buf == NULL, so this does not attempt to
             * free the managed memory.
             */
            av_packet_unref(packet);
            av_packet_free(&packet);
        }

        return lastTexture;
    }


    // =============================================================
    // D3D11 FRAME → SHARPDX TEXTURE
    // =============================================================
    // private Texture2D ConvertD3D11FrameToTexture(
    //     AVFrame* frame)
    // {
    //     if (frame == null)
    //         return null;
    //
    //     Texture2D result = null;
    //
    //     try
    //     {
    //         IntPtr ptr =
    //             (IntPtr)frame->data[0];
    //
    //         int arrayIndex =
    //             (int)frame->data[1];
    //
    //         ffmpegTexture =
    //             new Texture2D(ptr);
    //
    //         if (ffmpegTexture == null)
    //             throw new Exception(
    //                 "Failed to wrap texture");
    //
    //         int videoWidth =
    //             codecCtx->width;
    //
    //         int videoHeight =
    //             codecCtx->height;
    //
    //         if (videoWidth <= 0 ||
    //             videoHeight <= 0)
    //         {
    //             return null;
    //         }
    //
    //         /*
    //          * ---------------------------------------------------------
    //          * Create/recreate our D3D11 texture.
    //          * ---------------------------------------------------------
    //          */
    //         if (hwTexture == null ||
    //             hwTexture.Description.Width != videoWidth ||
    //             hwTexture.Description.Height != videoHeight ||
    //             hwTexture.Description.Format !=
    //                 ffmpegTexture.Description.Format)
    //         {
    //             hwTexture?.Dispose();
    //
    //             hwTexture =
    //                 new Texture2D(
    //                     device,
    //                     new Texture2DDescription
    //                     {
    //                         Width = videoWidth,
    //                         Height = videoHeight,
    //
    //                         MipLevels = 1,
    //                         ArraySize = 1,
    //
    //                         Format =
    //                             ffmpegTexture
    //                                 .Description
    //                                 .Format,
    //
    //                         SampleDescription =
    //                             new SampleDescription(
    //                                 1,
    //                                 0),
    //
    //                         Usage =
    //                             ResourceUsage.Default,
    //
    //                         BindFlags =
    //                             BindFlags.ShaderResource |
    //                             BindFlags.RenderTarget,
    //
    //                         CpuAccessFlags =
    //                             CpuAccessFlags.None,
    //
    //                         OptionFlags =
    //                             ResourceOptionFlags.None
    //                     });
    //         }
    //
    //         /*
    //          * ---------------------------------------------------------
    //          * Copy FFmpeg D3D11 frame → our texture.
    //          * ---------------------------------------------------------
    //          */
    //         // device.ImmediateContext
    //         //     .CopySubresourceRegion(
    //         //         ffmpegTexture,
    //         //         arrayIndex,
    //         //         new ResourceRegion(
    //         //             0,
    //         //             0,
    //         //             0,
    //         //             videoWidth,
    //         //             videoHeight,
    //         //             1),
    //         //         hwTexture,
    //         //         0);
    //         //
    //         k_info.Instance.directx.RunOnContext(ctx =>
    //         {
    //             ctx.CopySubresourceRegion(ffmpegTexture, arrayIndex,
    //                 new ResourceRegion(0, 0, 0, videoWidth, videoHeight, 1),
    //                 hwTexture, 0);
    //
    //             result = new Texture2D(device, hwTexture.Description);
    //
    //             ctx.CopyResource(hwTexture, result);
    //         });
    //         
    //         
    //
    //         /*
    //          * ---------------------------------------------------------
    //          * Independent texture for caller.
    //          * ---------------------------------------------------------
    //          */
    //         result =
    //             new Texture2D(
    //                 device,
    //                 hwTexture.Description);
    //
    //         device.ImmediateContext.CopyResource(
    //             hwTexture,
    //             result);
    //
    //         return result;
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine(
    //             $"GPU path failed: {ex.Message}");
    //
    //         result?.Dispose();
    //         result = null;
    //
    //         /*
    //          * Preserve CPU fallback.
    //          */
    //         try
    //         {
    //             return ConvertFrameToTexture(frame);
    //         }
    //         catch (Exception fallbackEx)
    //         {
    //             Console.WriteLine(
    //                 $"CPU fallback after GPU failure failed: {fallbackEx.Message}");
    //
    //             return null;
    //         }
    //     }
    //     finally
    //     {
    //         ffmpegTexture?.Dispose();
    //         ffmpegTexture = null;
    //     }
    // }
    
    
    
    
    
    
    
    private Texture2D ConvertD3D11FrameToTexture(AVFrame* frame)
{
    if (frame == null)
        return null;

    Texture2D result = null;

    try
    {
        IntPtr ptr = (IntPtr)frame->data[0];
        int arrayIndex = (int)frame->data[1];

        ffmpegTexture = new Texture2D(ptr);

        if (ffmpegTexture == null)
            throw new Exception("Failed to wrap texture");

        // Use the FRAME's dimensions, not the codec context's.
        // The codec context can change during streaming; the frame is what we're copying.
        int videoWidth = frame->width;
        int videoHeight = frame->height;

        if (videoWidth <= 0 || videoHeight <= 0)
            return null;

        if (hwTexture == null ||
            hwTexture.Description.Width != videoWidth ||
            hwTexture.Description.Height != videoHeight ||
            hwTexture.Description.Format != ffmpegTexture.Description.Format)
        {
            Console.WriteLine($"[my_AV] Recreate hwTexture: {videoWidth}x{videoHeight}");

            hwTexture?.Dispose();

            hwTexture = new Texture2D(device, new Texture2DDescription
            {
                Width = videoWidth,
                Height = videoHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = ffmpegTexture.Description.Format,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });
        }

        // Do everything under the lock — and only once.
        k_info.Instance.directx.RunOnContext(ctx =>
        {
            ctx.CopySubresourceRegion(
                ffmpegTexture,
                arrayIndex,
                new ResourceRegion(0, 0, 0, videoWidth, videoHeight, 1),
                hwTexture,
                0);

            result = new Texture2D(device, hwTexture.Description);

            ctx.CopyResource(hwTexture, result);

            ctx.Flush();
        });

        return result;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"GPU path failed: {ex.Message}");

        result?.Dispose();
        result = null;

        try
        {
            return ConvertFrameToTexture(frame);
        }
        catch (Exception fallbackEx)
        {
            Console.WriteLine($"CPU fallback after GPU failure failed: {fallbackEx.Message}");
            return null;
        }
    }
    finally
    {
        // Do NOT dispose — FFmpeg owns this texture.
        ffmpegTexture = null;
    }
}
    


    // =============================================================
    // CPU FALLBACK
    // =============================================================
    private Texture2D ConvertFrameToTexture(
        AVFrame* frame)
    {
        if (frame == null ||
            frame->width <= 0 ||
            frame->height <= 0)
        {
            return null;
        }

        AVFrame* swFrame = null;

        AVFrame* frameToConvert =
            frame;

        /*
         * D3D11 → CPU frame.
         */
        if (frame->format ==
            (int)AVPixelFormat.AV_PIX_FMT_D3D11)
        {
            swFrame =
                av_frame_alloc();

            if (swFrame == null)
                return null;

            int ret =
                av_hwframe_transfer_data(
                    swFrame,
                    frame,
                    0);

            if (ret < 0)
            {
                av_frame_free(
                    &swFrame);

                return null;
            }

            frameToConvert =
                swFrame;
        }

        byte_ptrArray4 dst_data =
            new byte_ptrArray4();

        int_array4 dst_linesize =
            new int_array4();

        SwsContext* sws_ctx =
            null;

        try
        {
            sws_ctx =
                sws_getContext(
                    frameToConvert->width,
                    frameToConvert->height,
                    (AVPixelFormat)
                        frameToConvert->format,

                    frameToConvert->width,
                    frameToConvert->height,
                    AVPixelFormat
                        .AV_PIX_FMT_BGRA,

                    SWS_POINT,

                    null,
                    null,
                    null);

            if (sws_ctx == null)
                return null;

            int dst_bufsize =
                av_image_alloc(
                    ref dst_data,
                    ref dst_linesize,
                    frameToConvert->width,
                    frameToConvert->height,
                    AVPixelFormat
                        .AV_PIX_FMT_BGRA,
                    1);

            if (dst_bufsize < 0)
                return null;

            int result =
                sws_scale(
                    sws_ctx,
                    frameToConvert->data,
                    frameToConvert->linesize,
                    0,
                    frameToConvert->height,
                    dst_data,
                    dst_linesize);

            if (result <= 0)
                return null;

            var textureDesc =
                new Texture2DDescription
                {
                    Width =
                        frameToConvert->width,

                    Height =
                        frameToConvert->height,

                    MipLevels = 1,
                    ArraySize = 1,

                    Format =
                        Format.B8G8R8A8_UNorm,

                    SampleDescription =
                        new SampleDescription(
                            1,
                            0),

                    Usage =
                        ResourceUsage.Default,

                    BindFlags =
                        BindFlags.ShaderResource |
                        BindFlags.RenderTarget,

                    CpuAccessFlags =
                        CpuAccessFlags.None,

                    OptionFlags =
                        ResourceOptionFlags.None
                };

            var dataBox =
                new DataRectangle(
                    (IntPtr)dst_data[0],
                    dst_linesize[0]);

            return new Texture2D(
                device,
                textureDesc,
                new[] { dataBox });
        }
        finally
        {
            if (dst_data[0] != null)
            {
                byte* ptr =
                    dst_data[0];

                av_freep(&ptr);
            }

            if (sws_ctx != null)
            {
                sws_freeContext(
                    sws_ctx);
            }

            if (swFrame != null)
            {
                av_frame_free(
                    &swFrame);
            }
        }
    }

    
    private static string FFmpegError(int error)
    {
        byte[] buffer = new byte[256];

        fixed (byte* ptr = buffer)
        {
            av_strerror(
                error,
                ptr,
                (ulong)buffer.Length);
        }

        return System.Text.Encoding.UTF8
            .GetString(buffer)
            .TrimEnd('\0');
    }

    // =============================================================
    // DISPOSE
    // =============================================================
    public void Dispose()
    {
        /*
         * Release stored config packet.
         */
        configPacket = null;

        hwTexture?.Dispose();
        hwTexture = null;

        ffmpegTexture?.Dispose();
        ffmpegTexture = null;

        if (codecCtx != null)
        {
            AVCodecContext* tmp =
                codecCtx;

            avcodec_free_context(
                &tmp);

            codecCtx = null;
        }

        if (hwDeviceCtx != null)
        {
            /*
             * Keep your existing behavior.
             */
            hwDeviceCtx = null;
        }

        codec = null;
    }
}