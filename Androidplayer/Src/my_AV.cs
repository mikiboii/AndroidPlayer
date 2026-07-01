//
//
// using System;
// using System.Runtime.InteropServices;
// using FFmpeg.AutoGen;
// using SharpDX;
// using SharpDX.Direct3D11;
// using SharpDX.DXGI;
// using Device = SharpDX.Direct3D11.Device;
// using static FFmpeg.AutoGen.ffmpeg;
//
// namespace Androidplayer;
//
// public unsafe class my_AV : IDisposable
// {
//     private AVCodec* codec;
//     private AVCodecContext* codecCtx;
//     private AVBufferRef* hwDeviceCtx = null;
//     private AVCodecParserContext* parser;
//
//     private Device device;
//
//     private Texture2D hwTexture = null;
//     private Texture2D ffmpegTexture = null;
//
//     private bool hwInitialized = false;
//     private bool useSoftwareFallback = false;
//
//     public long FrameCount { get; private set; }
//     public int Width { get; private set; }
//     public int Height { get; private set; }
//
//     public my_AV(Device device)
//     {
//         this.device = device ?? throw new ArgumentNullException(nameof(device));
//         ffmpeg.RootPath = Environment.Is64BitProcess ? @"c:\deps\x64" : @"c:\deps\x32";
//
//         codec = avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264);
//         if (codec == null) throw new Exception("H264 codec not found");
//
//         codecCtx = avcodec_alloc_context3(codec);
//         if (codecCtx == null) throw new Exception("Failed to allocate codec context");
//         
//         codecCtx->flags |= AV_CODEC_FLAG_LOW_DELAY;
//         codecCtx->flags2 |= AV_CODEC_FLAG2_FAST;
//         codecCtx->skip_frame = AVDiscard.AVDISCARD_DEFAULT;
//         codecCtx->skip_loop_filter = AVDiscard.AVDISCARD_DEFAULT;
//         codecCtx->refs = 1;
//         
//         // Try D3D11VA HW acceleration
//         try
//         {
//             hwDeviceCtx = av_hwdevice_ctx_alloc(AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA);
//             AVHWDeviceContext* devCtx = (AVHWDeviceContext*)hwDeviceCtx->data;
//             AVD3D11VADeviceContext* d3d11 = (AVD3D11VADeviceContext*)devCtx->hwctx;
//             d3d11->device = (ID3D11Device*)device.NativePointer;
//
//             int ret = av_hwdevice_ctx_init(hwDeviceCtx);
//             if (ret < 0) 
//             { 
//                 Console.WriteLine($"HW device init failed: {ret}, using software fallback");
//                 useSoftwareFallback = true; 
//                 hwDeviceCtx = null; 
//             }
//             else 
//             { 
//                 hwInitialized = true;
//                 Console.WriteLine("D3D11VA hardware acceleration initialized");
//             }
//         }
//         catch (Exception ex) 
//         { 
//             Console.WriteLine($"HW acceleration setup failed: {ex.Message}, using software fallback");
//             useSoftwareFallback = true; 
//             hwDeviceCtx = null; 
//         }
//
//         if (hwInitialized && hwDeviceCtx != null)
//             codecCtx->hw_device_ctx = av_buffer_ref(hwDeviceCtx);
//
//         if (avcodec_open2(codecCtx, codec, null) < 0)
//             throw new Exception("Failed to open codec");
//
//         parser = av_parser_init((int)AVCodecID.AV_CODEC_ID_H264);
//         if (parser == null)
//             throw new Exception("Failed to initialize H264 parser");
//             
//         Console.WriteLine($"Decoder initialized. HW: {hwInitialized}, SW Fallback: {useSoftwareFallback}");
//     }
//
//     public Texture2D Decode(byte[] h264Data)
//     {
//         if (h264Data == null || h264Data.Length == 0) return null;
//
//         fixed (byte* pData = h264Data)
//         {
//             byte* ptr = pData;
//             int size = h264Data.Length;
//             Texture2D lastTexture = null;
//
//             while (size > 0)
//             {
//                 byte* outData = null;
//                 int outSize = 0;
//
//                 int consumed = av_parser_parse2(
//                     parser,
//                     codecCtx,
//                     &outData,
//                     &outSize,
//                     ptr,
//                     size,
//                     AV_NOPTS_VALUE,
//                     AV_NOPTS_VALUE,
//                     0
//                 );
//
//                 if (consumed < 0)
//                     break;
//
//                 if (outSize > 0)
//                 {
//                     Texture2D tex = DecodeParsedPacket(outData, outSize);
//                     if (tex != null) lastTexture = tex;
//                 }
//
//                 ptr += consumed;
//                 size -= consumed;
//             }
//
//             return lastTexture;
//         }
//     }
//
//     
//     private Texture2D DecodeParsedPacket(byte* data, int size)
// {
//     AVPacket* packet = av_packet_alloc();
//     av_init_packet(packet);
//     packet->data = data;
//     packet->size = size;
//
//     int ret = avcodec_send_packet(codecCtx, packet);
//     if (ret < 0) { av_packet_unref(packet); return null; }
//
//     AVFrame* frame = av_frame_alloc();
//     ret = avcodec_receive_frame(codecCtx, frame);
//     if (ret == AVERROR_EOF || ret == AVERROR(EAGAIN)) { av_packet_unref(packet); av_frame_free(&frame); return null; }
//     if (ret < 0) { av_packet_unref(packet); av_frame_free(&frame); return null; }
//
//     Texture2D result = null;
//
//     try
//     {
//         // ORIGINAL WORKING GPU PATH - DON'T CHANGE THIS
//         if (hwInitialized && frame->data[0] != null && frame->format == (int)AVPixelFormat.AV_PIX_FMT_D3D11)
//         {
//             // HW decoded frame
//             IntPtr ptr = (IntPtr)frame->data[0];
//             ffmpegTexture = new Texture2D(ptr);
//
//             // Use frame dimensions (original working code)
//             int videoWidth = frame->width;
//             int videoHeight = frame->height;
//
//             if (hwTexture == null || 
//                 hwTexture.Description.Width != videoWidth ||
//                 hwTexture.Description.Height != videoHeight)
//             {
//                 hwTexture?.Dispose();
//                 hwTexture = new Texture2D(device, new Texture2DDescription
//                 {
//                     Width = videoWidth,
//                     Height = videoHeight,
//                     MipLevels = 1,
//                     ArraySize = 1,
//                     Format = ffmpegTexture.Description.Format,
//                     SampleDescription = new SampleDescription(1, 0),
//                     Usage = ResourceUsage.Default,
//                     BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
//                     CpuAccessFlags = CpuAccessFlags.None,
//                     OptionFlags = ResourceOptionFlags.None
//                 });
//             }
//
//             int arrayIndex = (int)frame->data[1];
//
//             device.ImmediateContext.CopySubresourceRegion(
//                 ffmpegTexture,
//                 arrayIndex,
//                 new ResourceRegion(0, 0, 0, videoWidth, videoHeight, 1),
//                 hwTexture,
//                 0);
//
//             // Original working code does this extra copy
//             result = new Texture2D(device, hwTexture.Description);
//             device.ImmediateContext.CopyResource(hwTexture, result);
//
//             ffmpegTexture.Dispose();
//             ffmpegTexture = null;
//         }
//         else
//         {
//             // Software fallback - handles both software and D3D11 frames
//             result = SoftwareFrameToTexture(frame);
//         }
//
//         if (result != null)
//         {
//             FrameCount++;
//             Width = frame->width;
//             Height = frame->height;
//         }
//     }
//     finally
//     {
//         av_frame_free(&frame);
//         av_packet_unref(packet);
//     }
//
//     return result;
// }
//
//     
//     
//   private Texture2D SoftwareFrameToTexture(AVFrame* frame)
// {
//     if (frame == null) return null;
//     
//     // Check if this is a D3D11 hardware frame that needs transfer
//     if (frame->format == (int)AVPixelFormat.AV_PIX_FMT_D3D11)
//     {
//         // Hardware frame - need to transfer to CPU first
//         return HardwareFrameToSoftware(frame);
//     }
//
//     // Regular software frame - use existing working code
//     SwsContext* sws = sws_getContext(frame->width, frame->height, (AVPixelFormat)frame->format,
//                                      frame->width, frame->height, AVPixelFormat.AV_PIX_FMT_RGBA,
//                                      SWS_BILINEAR, null, null, null);
//     if (sws == null) return null;
//
//     try
//     {
//         int stride = frame->width * 4;
//         byte[] buffer = new byte[stride * frame->height];
//
//         fixed (byte* pBuffer = buffer)
//         {
//             byte_ptrArray4 dstData = new byte_ptrArray4();
//             int_array4 dstLine = new int_array4();
//             dstData[0] = pBuffer;
//             dstLine[0] = stride;
//
//             sws_scale(sws, frame->data, frame->linesize, 0, frame->height, dstData, dstLine);
//
//             var desc = new Texture2DDescription
//             {
//                 Width = frame->width,
//                 Height = frame->height,
//                 ArraySize = 1,
//                 MipLevels = 1,
//                 Format = Format.R8G8B8A8_UNorm,
//                 BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
//                 Usage = ResourceUsage.Default,
//                 CpuAccessFlags = CpuAccessFlags.None,
//                 OptionFlags = ResourceOptionFlags.None,
//                 SampleDescription = new SampleDescription(1, 0)
//             };
//
//             var tex = new Texture2D(device, desc);
//             device.ImmediateContext.UpdateSubresource(new DataBox((IntPtr)pBuffer, stride, 0), tex, 0);
//
//             return tex;
//         }
//     }
//     finally
//     {
//         sws_freeContext(sws);
//     }
// }
//
// // New method to handle D3D11 hardware frames in software mode
// private Texture2D HardwareFrameToSoftware(AVFrame* hwFrame)
// {
//     // Create a software frame to receive GPU data
//     AVFrame* swFrame = av_frame_alloc();
//     if (swFrame == null) return null;
//
//     try
//     {
//         // Transfer from GPU to CPU
//         int ret = av_hwframe_transfer_data(swFrame, hwFrame, 0);
//         if (ret < 0)
//         {
//             Console.WriteLine($"av_hwframe_transfer_data failed: {ret}");
//             return null;
//         }
//
//         // Now convert the CPU frame to texture using the working method
//         return SoftwareFrameToTexture(swFrame);
//     }
//     finally
//     {
//         if (swFrame != null)
//         {
//             av_frame_free(&swFrame);
//         }
//     }
// }
//
//     public void Dispose()
//     {
//         hwTexture?.Dispose();
//         ffmpegTexture?.Dispose();
//
//         if (parser != null)
//         {
//             av_parser_close(parser);
//             parser = null;
//         }
//
//         if (codecCtx != null)
//         {
//             AVCodecContext* tmp = codecCtx;
//             avcodec_free_context(&tmp);
//             codecCtx = null;
//         }
//
//         if (hwDeviceCtx != null)
//         {
//             // av_buffer_unref(&hwDeviceCtx);
//             hwDeviceCtx = null;
//         }
//     }
// }

//
/////////////////////////////////////////////////////////////////





/////////////////////////////////////////////////////////////////





using System;
using System.IO;
using System.Runtime.InteropServices;
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
    private AVCodecParserContext* parser;

    private Device device;

    private Texture2D hwTexture = null;
    private Texture2D ffmpegTexture = null;

    private bool hwInitialized = false;
    private bool useSoftwareFallback = false;

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

        ffmpeg.RootPath = ffmpegPath;
        
        // ffmpeg.RootPath = Environment.Is64BitProcess ? @"c:\deps\x64" : @"c:\deps\x32";

        codec = avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264);
        if (codec == null) throw new Exception("H264 codec not found");

        codecCtx = avcodec_alloc_context3(codec);
        if (codecCtx == null) throw new Exception("Failed to allocate codec context");
        
        codecCtx->flags |= AV_CODEC_FLAG_LOW_DELAY;
        codecCtx->flags2 |= AV_CODEC_FLAG2_FAST;
        codecCtx->skip_frame = AVDiscard.AVDISCARD_DEFAULT;
        codecCtx->skip_loop_filter = AVDiscard.AVDISCARD_DEFAULT;
        codecCtx->refs = 1;
        
        // Try D3D11VA HW acceleration
        try
        {
            hwDeviceCtx = av_hwdevice_ctx_alloc(AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA);
            AVHWDeviceContext* devCtx = (AVHWDeviceContext*)hwDeviceCtx->data;
            AVD3D11VADeviceContext* d3d11 = (AVD3D11VADeviceContext*)devCtx->hwctx;
            d3d11->device = (ID3D11Device*)device.NativePointer;

            int ret = av_hwdevice_ctx_init(hwDeviceCtx);
            if (ret < 0) 
            { 
                Console.WriteLine($"HW device init failed: {ret}, using software fallback");
                useSoftwareFallback = true; 
                hwDeviceCtx = null; 
            }
            else 
            { 
                hwInitialized = true;
                Console.WriteLine("D3D11VA hardware acceleration initialized");
            }
        }
        catch (Exception ex) 
        { 
            Console.WriteLine($"HW acceleration setup failed: {ex.Message}, using software fallback");
            useSoftwareFallback = true; 
            hwDeviceCtx = null; 
        }

        if (hwInitialized && hwDeviceCtx != null)
            codecCtx->hw_device_ctx = av_buffer_ref(hwDeviceCtx);

        if (avcodec_open2(codecCtx, codec, null) < 0)
            throw new Exception("Failed to open codec");

        parser = av_parser_init((int)AVCodecID.AV_CODEC_ID_H264);
        if (parser == null)
            throw new Exception("Failed to initialize H264 parser");
            
        Console.WriteLine($"Decoder initialized. HW: {hwInitialized}, SW Fallback: {useSoftwareFallback}");
    }

    public Texture2D Decode(byte[] h264Data)
    {
        if (h264Data == null || h264Data.Length == 0) return null;

        fixed (byte* pData = h264Data)
        {
            byte* ptr = pData;
            int size = h264Data.Length;
            Texture2D lastTexture = null;

            while (size > 0)
            {
                byte* outData = null;
                int outSize = 0;

                int consumed = av_parser_parse2(
                    parser, codecCtx, &outData, &outSize,
                    ptr, size, AV_NOPTS_VALUE, AV_NOPTS_VALUE, 0
                );

                if (consumed < 0) break;

                if (outSize > 0)
                {
                    Texture2D tex = DecodeParsedPacket(outData, outSize);
                    if (tex != null) lastTexture = tex;
                }

                ptr += consumed;
                size -= consumed;
            }

            return lastTexture;
        }
    }

  private Texture2D DecodeParsedPacket(byte* data, int size)
{
    AVPacket* packet = av_packet_alloc();
    if (packet == null) return null;
    
    Texture2D result = null;
    AVFrame* frame = null;
    
    try
    {
        if (hwInitialized)
        {
            // GPU MODE: Use direct pointer - NO COPY (zero-copy)
            av_init_packet(packet);
            packet->data = data;
            packet->size = size;
        }
        else
        {
            // CPU MODE: Copy data (as working my_AV_4 does)
            packet->data = (byte*)av_malloc((ulong)size);
            if (packet->data == null) return null;
            System.Buffer.MemoryCopy(data, packet->data, size, size);
            packet->size = size;
        }

        int ret = avcodec_send_packet(codecCtx, packet);
        if (ret < 0) return null;

        frame = av_frame_alloc();
        if (frame == null) return null;
        
        ret = avcodec_receive_frame(codecCtx, frame);
        if (ret == AVERROR_EOF || ret == AVERROR(EAGAIN)) return null;
        if (ret < 0) return null;

        bool isD3D11Frame = frame->format == (int)AVPixelFormat.AV_PIX_FMT_D3D11;
        
        if (hwInitialized && isD3D11Frame && frame->data[0] != null)
        {
            try
            {
                IntPtr ptr = (IntPtr)frame->data[0];
                int arrayIndex = (int)frame->data[1];
        
                ffmpegTexture = new Texture2D(ptr);
        
                if (ffmpegTexture == null)
                {
                    throw new Exception("Failed to wrap texture");
                }

                int videoWidth = codecCtx->width;
                int videoHeight = codecCtx->height;

                if (hwTexture == null || 
                    hwTexture.Description.Width != videoWidth ||
                    hwTexture.Description.Height != videoHeight ||
                    hwTexture.Description.Format != ffmpegTexture.Description.Format)
                {
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
                        BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,  // Original flags
                        CpuAccessFlags = CpuAccessFlags.None,
                        OptionFlags = ResourceOptionFlags.None
                    });
                }

                device.ImmediateContext.CopySubresourceRegion(
                    ffmpegTexture,
                    arrayIndex,
                    new ResourceRegion(0, 0, 0, videoWidth, videoHeight, 1),
                    hwTexture,
                    0);

                result = new Texture2D(device, hwTexture.Description);
                device.ImmediateContext.CopyResource(hwTexture, result);

                ffmpegTexture.Dispose();
                ffmpegTexture = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GPU path failed: {ex.Message}");
                ffmpegTexture?.Dispose();
                ffmpegTexture = null;
                result = ConvertFrameToTexture(frame);
            }
        }
        else
        {
            // CPU path
            result = ConvertFrameToTexture(frame);
        }

        if (result != null)
        {
            FrameCount++;
            Width = frame->width;
            Height = frame->height;
        }
    }
    finally
    {
        if (frame != null) av_frame_free(&frame);
        
        if (hwInitialized)
        {
            // GPU mode: just unref (data owned by caller)
            av_packet_unref(packet);
        }
        else
        {
            // CPU mode: free our copy
            if (packet->data != null) av_free(packet->data);
            av_packet_free(&packet);
        }
    }

    return result;
}
    // EXACT COPY of my_AV_4's ConvertFrameToTexture - proven working on CPU
    private Texture2D ConvertFrameToTexture(AVFrame* frame)
    {
        if (frame == null || frame->width <= 0 || frame->height <= 0)
            return null;

        // If it's a D3D11 frame, transfer to CPU first
        AVFrame* swFrame = null;
        AVFrame* frameToConvert = frame;
        
        if (frame->format == (int)AVPixelFormat.AV_PIX_FMT_D3D11)
        {
            swFrame = av_frame_alloc();
            if (swFrame == null) return null;
            
            int ret = av_hwframe_transfer_data(swFrame, frame, 0);
            if (ret < 0)
            {
                av_frame_free(&swFrame);
                return null;
            }
            frameToConvert = swFrame;
        }

        byte_ptrArray4 dst_data = new byte_ptrArray4();
        int_array4 dst_linesize = new int_array4();
        SwsContext* sws_ctx = null;

        try
        {
            sws_ctx = sws_getContext(
                frameToConvert->width, frameToConvert->height, (AVPixelFormat)frameToConvert->format,
                frameToConvert->width, frameToConvert->height, AVPixelFormat.AV_PIX_FMT_BGRA,
                SWS_POINT, null, null, null
            );

            if (sws_ctx == null) return null;

            int dst_bufsize = av_image_alloc(
                ref dst_data, ref dst_linesize,
                frameToConvert->width, frameToConvert->height,
                AVPixelFormat.AV_PIX_FMT_BGRA, 1
            );

            if (dst_bufsize < 0) return null;

            int result = sws_scale(
                sws_ctx,
                frameToConvert->data, frameToConvert->linesize, 0, frameToConvert->height,
                dst_data, dst_linesize
            );

            if (result <= 0) return null;

            var textureDesc = new Texture2DDescription()
            {
                Width = frameToConvert->width,
                Height = frameToConvert->height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };

            var dataBox = new DataRectangle((IntPtr)dst_data[0], dst_linesize[0]);
            Texture2D newTexture = new Texture2D(device, textureDesc, new[] { dataBox });

            return newTexture;
        }
        finally
        {
            if (dst_data[0] != null)
            {
                byte* ptr = dst_data[0];
                av_freep(&ptr);
            }
            if (sws_ctx != null)
            {
                sws_freeContext(sws_ctx);
            }
            if (swFrame != null)
            {
                av_frame_free(&swFrame);
            }
        }
    }

    public void Dispose()
    {
        hwTexture?.Dispose();
        ffmpegTexture?.Dispose();

        if (parser != null)
        {
            av_parser_close(parser);
            parser = null;
        }

        if (codecCtx != null)
        {
            AVCodecContext* tmp = codecCtx;
            avcodec_free_context(&tmp);
            codecCtx = null;
        }

        if (hwDeviceCtx != null)
        {
            // av_buffer_unref(&hwDeviceCtx);
            hwDeviceCtx = null;
        }
    }
}