// // my_av_linux.cs
// #if LINUX
//
// using System;
// using System.Collections.Generic;
// using System.Runtime.InteropServices;
// using System.Text;
//
// using Androidplayer.Src.Keymap.K_store;
//
// using FFmpeg.AutoGen;
// using static FFmpeg.AutoGen.ffmpeg;
//
// using Silk.NET.Vulkan;
// using Silk.NET.Vulkan.Extensions.EXT;
// using Silk.NET.Vulkan.Extensions.KHR;
//
// using VkDevice = Silk.NET.Vulkan.Device;
// using VkFormat = Silk.NET.Vulkan.Format;
// using VkImage  = Silk.NET.Vulkan.Image;
//
// namespace Androidplayer;
//
// public unsafe class my_AV_linux : IDisposable
// {
//     // -------------------------------------------------------------
//     // FFmpeg
//     // -------------------------------------------------------------
//     private AVCodec*        codec;
//     private AVCodecContext* codecCtx;
//     private AVBufferRef*    hwDeviceCtx = null;
//
//     private bool hwInitialized       = false;
//     private bool useSoftwareFallback = false;
//
//     // -------------------------------------------------------------
//     // Vulkan / Silk.NET
//     // -------------------------------------------------------------
//     private Vk                     vk;
//     private Instance               instance;
//     private PhysicalDevice         physicalDevice;
//     private VkDevice               device;
//
//     private KhrExternalMemoryFd              extMemFd;
//     private ExtExternalMemoryDmaBuf          extDmaBuf;
//     private ExtImageDrmFormatModifier        extDrmMod;
//
//     // -------------------------------------------------------------
//     // scrcpy config packet
//     // -------------------------------------------------------------
//     private byte[] configPacket = null;
//
//     public long FrameCount { get; private set; }
//     public int  Width      { get; private set; }
//     public int  Height     { get; private set; }
//
//     // -------------------------------------------------------------
//     // ctor
//     // -------------------------------------------------------------
//     public my_AV_linux(
//         Vk vk,
//         Instance instance,
//         PhysicalDevice physicalDevice,
//         VkDevice device)
//     {
//         this.vk             = vk             ?? throw new ArgumentNullException(nameof(vk));
//         this.instance       = instance;
//         this.physicalDevice = physicalDevice;
//         this.device         = device;
//
//         ffmpeg.RootPath = AppContext.BaseDirectory;
//
//         // Load the Vulkan extensions we need for DMA-BUF import.
//         if (!vk.TryGetDeviceExtension(instance, device, out extMemFd))
//             throw new Exception("VK_KHR_external_memory_fd not available");
//
//         if (!vk.TryGetDeviceExtension(instance, device, out extDmaBuf))
//             throw new Exception("VK_EXT_external_memory_dma_buf not available");
//
//         if (!vk.TryGetDeviceExtension(instance, device, out extDrmMod))
//             throw new Exception("VK_EXT_image_drm_format_modifier not available");
//
//         codec = avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264);
//         if (codec == null)
//             throw new Exception("H264 codec not found");
//
//         codecCtx = avcodec_alloc_context3(codec);
//         if (codecCtx == null)
//             throw new Exception("Failed to allocate codec context");
//
//         codecCtx->flags           |= AV_CODEC_FLAG_LOW_DELAY;
//         codecCtx->flags2          |= AV_CODEC_FLAG2_FAST;
//         codecCtx->skip_frame       = AVDiscard.AVDISCARD_DEFAULT;
//         codecCtx->skip_loop_filter = AVDiscard.AVDISCARD_DEFAULT;
//         codecCtx->refs             = 1;
//
//         TryInitVaapiHwAccel();
//
//         if (hwInitialized && hwDeviceCtx != null)
//             codecCtx->hw_device_ctx = av_buffer_ref(hwDeviceCtx);
//
//         if (avcodec_open2(codecCtx, codec, null) < 0)
//             throw new Exception("Failed to open codec");
//
//         Console.WriteLine(
//             $"[my_av_linux] Decoder initialized. HW: {hwInitialized}, SW fallback: {useSoftwareFallback}");
//     }
//
//     // -------------------------------------------------------------
//     // VAAPI hw accel init
//     // -------------------------------------------------------------
//     private void TryInitVaapiHwAccel()
//     {
//         try
//         {
//             hwDeviceCtx = av_hwdevice_ctx_alloc(
//                 AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI);
//
//             if (hwDeviceCtx == null)
//             {
//                 Console.WriteLine(
//                     "[my_av_linux] av_hwdevice_ctx_alloc(VAAPI) failed, using software fallback");
//                 useSoftwareFallback = true;
//                 return;
//             }
//
//             // No extra fields needed: FFmpeg opens /dev/dri/renderD128 by default.
//             // If you want a specific device:
//             //   var ctx = (AVHWDeviceContext*)hwDeviceCtx->data;
//             //   var va  = (AVVAAPIDeviceContext*)ctx->hwctx;
//             //   va->device_name = (byte*)Marshal.StringToHGlobalAnsi("/dev/dri/renderD128");
//
//             int ret = av_hwdevice_ctx_init(hwDeviceCtx);
//             if (ret < 0)
//             {
//                 Console.WriteLine(
//                     $"[my_av_linux] av_hwdevice_ctx_init failed: {FFmpegError(ret)}");
//                 useSoftwareFallback = true;
//                 hwDeviceCtx = null;
//                 return;
//             }
//
//             if (!CheckVaapiCodecSupport(codec))
//             {
//                 Console.WriteLine(
//                     "[my_av_linux] codec does not advertise VAAPI hwaccel, using software fallback");
//                 useSoftwareFallback = true;
//                 return;
//             }
//
//             hwInitialized = true;
//             Console.WriteLine("[my_av_linux] VAAPI hardware acceleration initialized");
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine(
//                 $"[my_av_linux] VAAPI setup failed: {ex.Message}, using software fallback");
//             useSoftwareFallback = true;
//             hwDeviceCtx = null;
//         }
//     }
//
//     private static bool CheckVaapiCodecSupport(AVCodec* codec)
//     {
//         for (int i = 0; ; i++)
//         {
//             AVCodecHWConfig* cfg = avcodec_get_hw_config(codec, i);
//             if (cfg == null) break;
//
//             if ((cfg->methods & 0x01) == 0) continue;                 // HW_DEVICE_CTX
//             if (cfg->pix_fmt == AVPixelFormat.AV_PIX_FMT_NONE) continue;
//
//             if (cfg->device_type == AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI &&
//                 cfg->pix_fmt     == AVPixelFormat.AV_PIX_FMT_VAAPI)
//                 return true;
//         }
//         return false;
//     }
//
//     // -------------------------------------------------------------
//     // Public API — same shape as Windows, but returns VkImage
//     // -------------------------------------------------------------
//     public VkImage Decode(byte[] h264Data)
//     {
//         if (h264Data == null || h264Data.Length == 0)
//             return default;
//
//         return DecodeH264Packet(h264Data, 0);
//     }
//
//     public VkImage DecodePacket(byte[] h264Data, long pts, bool isConfig)
//     {
//         if (h264Data == null || h264Data.Length == 0)
//             return default;
//
//         if (isConfig)
//         {
//             configPacket = new byte[h264Data.Length];
//             Buffer.BlockCopy(h264Data, 0, configPacket, 0, h264Data.Length);
//             Console.WriteLine(
//                 $"[my_av_linux] Stored H264 config packet: {configPacket.Length} bytes");
//             return default;
//         }
//
//         byte[] packetData;
//         if (configPacket != null)
//         {
//             packetData = new byte[configPacket.Length + h264Data.Length];
//             Buffer.BlockCopy(configPacket, 0, packetData, 0, configPacket.Length);
//             Buffer.BlockCopy(h264Data,     0, packetData, configPacket.Length, h264Data.Length);
//             configPacket = null;
//         }
//         else
//         {
//             packetData = h264Data;
//         }
//
//         return DecodeH264Packet(packetData, pts);
//     }
//
//     // -------------------------------------------------------------
//     // Decode loop
//     // -------------------------------------------------------------
//     private VkImage DecodeH264Packet(byte[] h264Data, long pts)
//     {
//         AVPacket* packet = av_packet_alloc();
//         if (packet == null) return default;
//
//         VkImage lastImage = default;
//
//         try
//         {
//             fixed (byte* pData = h264Data)
//             {
//                 packet->data = pData;
//                 packet->size = h264Data.Length;
//                 packet->pts  = pts;
//                 packet->dts  = pts;
//
//                 int ret = avcodec_send_packet(codecCtx, packet);
//                 if (ret < 0 && ret != AVERROR(EAGAIN))
//                 {
//                     Console.WriteLine(
//                         $"[my_av_linux] avcodec_send_packet failed: {ret} ({FFmpegError(ret)})");
//                     return default;
//                 }
//
//                 while (true)
//                 {
//                     AVFrame* frame = av_frame_alloc();
//                     if (frame == null) break;
//
//                     try
//                     {
//                         ret = avcodec_receive_frame(codecCtx, frame);
//                         if (ret == AVERROR(EAGAIN) || ret == AVERROR_EOF) break;
//                         if (ret < 0)
//                         {
//                             Console.WriteLine(
//                                 $"[my_av_linux] avcodec_receive_frame failed: {ret} ({FFmpegError(ret)})");
//                             break;
//                         }
//
//                         bool isVaapiFrame =
//                             frame->format == (int)AVPixelFormat.AV_PIX_FMT_VAAPI;
//
//                         VkImage image = default;
//
//                         if (hwInitialized && isVaapiFrame)
//                         {
//                             image = ConvertVaapiFrameToVkImage(frame);
//                         }
//
//                         if (image.Handle != 0)
//                         {
//                             lastImage = image;
//                             FrameCount++;
//                             Width  = frame->width;
//                             Height = frame->height;
//                         }
//                     }
//                     finally
//                     {
//                         av_frame_free(&frame);
//                     }
//                 }
//             }
//         }
//         finally
//         {
//             av_packet_unref(packet);
//             av_packet_free(&packet);
//         }
//
//         return lastImage;
//     }
//
//     // =============================================================
//     // VAAPI frame -> DRM PRIME -> VkImage
//     // =============================================================
//     private VkImage ConvertVaapiFrameToVkImage(AVFrame* vaapiFrame)
//     {
//         if (vaapiFrame == null)
//             return default;
//
//         AVFrame* drmFrame = av_frame_alloc();
//         if (drmFrame == null)
//             return default;
//
//         VkImage result = default;
//         DeviceMemory memory = default;
//
//         try
//         {
//             // Map the VAAPI surface to a DRM PRIME frame (dma_buf fd + modifier).
//             // This is a ZERO-COPY operation — same physical surface.
//             int ret = av_hwframe_map(
//                 drmFrame,
//                 vaapiFrame,
//                 (int)AV_HWFRAME_MAP_READ);
//
//             if (ret < 0)
//             {
//                 Console.WriteLine(
//                     $"[my_av_linux] av_hwframe_map(DRM_PRIME) failed: {ret} ({FFmpegError(ret)})");
//                 return default;
//             }
//
//             if (drmFrame->format != (int)AVPixelFormat.AV_PIX_FMT_DRM_PRIME)
//             {
//                 Console.WriteLine(
//                     $"[my_av_linux] expected DRM_PRIME, got {(AVPixelFormat)drmFrame->format}");
//                 return default;
//             }
//
//             AVDRMFrameDescriptor* desc = (AVDRMFrameDescriptor*)drmFrame->data[0];
//             if (desc == null || desc->nb_objects == 0 || desc->nb_layers == 0)
//                 return default;
//
//             // NV12 = 2 planes; but scrcpy H.264 usually lands here as 1 or 2 layers.
//             uint planeCount = desc->nb_layers;
//             if (planeCount > 2) planeCount = 2;
//
//             VkFormat vkFormat = MapDrmFormatToVkFormat(
//                 desc->layers[0].format, planeCount);
//
//             if (vkFormat == VkFormat.Undefined)
//             {
//                 Console.WriteLine(
//                     $"[my_av_linux] unsupported DRM format 0x{desc->layers[0].format:X}");
//                 return default;
//             }
//
//             // ---- Build VkImageCreateInfo (DRM modifier tiling) ----
//             var drmModifier = new ImageDrmFormatModifierExplicitCreateInfoEXT
//             {
//                 SType              = StructureType.ImageDrmFormatModifierExplicitCreateInfoExt,
//                 DrmFormatModifier  = desc->objects[0].format_modifier,
//                 DrmFormatModifierPlaneCount = planeCount,
//             };
//
//             var planeLayouts = stackalloc ImagePlaneLayoutEXT[(int)planeCount];
//
//             for (int i = 0; i < planeCount; i++)
//             {
//                 var layer = desc->layers[i];
//
//                 planeLayouts[i] = new ImagePlaneLayoutEXT
//                 {
//                     SType     = StructureType.ImagePlaneLayoutExt,
//                     Offset    = (nuint)layer.planes[0].offset,
//                     RowPitch  = (nuint)layer.planes[0].pitch,
//                 };
//             }
//
//             drmModifier.PPlaneLayouts = planeLayouts;
//
//             // If multiple DRM objects (rare for NV12), use VkExternalMemoryImageCreateInfo
//             // with VK_EXTERNAL_MEMORY_HANDLE_TYPE_DMA_BUF_BIT_EXT.
//             var extImageInfo = new ExternalMemoryImageCreateInfo
//             {
//                 SType       = StructureType.ExternalMemoryImageCreateInfo,
//                 HandleTypes = ExternalMemoryHandleTypeFlags.DmaBufBitExt,
//             };
//
//             // DRM modifier chain
//             drmModifier.PNext = extImageInfo.PNext;
//             extImageInfo.PNext = &drmModifier;   // correct order: extImageInfo -> drmModifier
//
//             // Fix: proper chain order
//             extImageInfo.PNext = &drmModifier;
//             drmModifier.PNext  = null;
//
//             // Mutable format needed for DMA-BUF images
//             var formatList = stackalloc VkFormat[1];
//             formatList[0] = vkFormat;
//
//             var mutableInfo = new ImageFormatListCreateInfo
//             {
//                 SType           = StructureType.ImageFormatListCreateInfo,
//                 ViewFormatCount = 1,
//                 PViewFormats    = formatList,
//             };
//
//             extImageInfo.PNext = &mutableInfo;
//             mutableInfo.PNext  = &drmModifier;
//
//             var imageInfo = new ImageCreateInfo
//             {
//                 SType       = StructureType.ImageCreateInfo,
//                 PNext       = &extImageInfo,
//                 ImageType   = ImageType.Type2D,
//                 Format      = vkFormat,
//                 Extent      = new Extent3D((uint)drmFrame->width, (uint)drmFrame->height, 1),
//                 MipLevels   = 1,
//                 ArrayLayers = 1,
//                 Samples     = SampleCountFlags.Count1Bit,
//                 Tiling      = ImageTiling.DrmFormatModifierExt,
//                 Usage       = ImageUsageFlags.SampledBit,
//                 SharingMode = SharingMode.Exclusive,
//                 InitialLayout = ImageLayout.Undefined,
//             };
//
//             fixed (VkImage* pImage = &result)
//             {
//                 if (vk.CreateImage(device, &imageInfo, null, pImage) != Result.Success)
//                 {
//                     Console.WriteLine("[my_av_linux] vkCreateImage failed");
//                     return default;
//                 }
//             }
//
//             // ---- Memory requirements + bind ----
//             MemoryRequirements memReq;
//             vk.GetImageMemoryRequirements(device, result, &memReq);
//
//             MemoryFdPropertiesKHR fdProps;
//             extMemFd.GetMemoryFdProperties(
//                 device,
//                 ExternalMemoryHandleTypeFlags.DmaBufBitExt,
//                 desc->objects[0].fd,
//                 &fdProps);
//
//             uint memoryTypeBits = memReq.MemoryTypeBits & fdProps.MemoryTypeBits;
//
//             uint memoryTypeIndex;
//             if (!FindMemoryType(memoryTypeBits, MemoryPropertyFlags.DeviceLocalBit, out memoryTypeIndex))
//             {
//                 Console.WriteLine("[my_av_linux] no suitable memory type for import");
//                 vk.DestroyImage(device, result, null);
//                 result = default;
//                 return default;
//             }
//
//             // Import the dma_buf fd as Vulkan memory
//             var importInfo = new ImportMemoryFdInfoKHR
//             {
//                 SType      = StructureType.ImportMemoryFdInfoKhr,
//                 HandleType = ExternalMemoryHandleTypeFlags.DmaBufBitExt,
//                 Fd         = desc->objects[0].fd,
//             };
//
//             var allocInfo = new MemoryAllocateInfo
//             {
//                 SType           = StructureType.MemoryAllocateInfo,
//                 PNext           = &importInfo,
//                 AllocationSize  = memReq.Size,
//                 MemoryTypeIndex = memoryTypeIndex,
//             };
//
//             fixed (DeviceMemory* pMem = &memory)
//             {
//                 if (vk.AllocateMemory(device, &allocInfo, null, pMem) != Result.Success)
//                 {
//                     Console.WriteLine("[my_av_linux] vkAllocateMemory (import) failed");
//                     vk.DestroyImage(device, result, null);
//                     result = default;
//                     return default;
//                 }
//             }
//
//             if (vk.BindImageMemory(device, result, memory, 0) != Result.Success)
//             {
//                 Console.WriteLine("[my_av_linux] vkBindImageMemory failed");
//                 vk.FreeMemory(device, memory, null);
//                 vk.DestroyImage(device, result, null);
//                 result = default;
//                 return default;
//             }
//
//             // NOTE: memory ownership has now been transferred to the VkImage.
//             // The dma_buf fd from desc->objects[0].fd is still owned by FFmpeg.
//             // Do not close it here.
//
//             return result;
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"[my_av_linux] VAAPI->VkImage failed: {ex.Message}");
//             return default;
//         }
//         finally
//         {
//             av_frame_free(&drmFrame);
//         }
//     }
//
//     // -------------------------------------------------------------
//     // DRM fourcc -> VkFormat
//     // -------------------------------------------------------------
//     private static VkFormat MapDrmFormatToVkFormat(uint fourcc, uint planeCount)
//     {
//         // DRM_FORMAT_NV12 = 'N','V','1','2' = 0x3231564E
//         // DRM_FORMAT_P010 = 0x30313050
//         switch (fourcc)
//         {
//             case 0x3231564E:    // NV12
//                 return VkFormat.G8B8R8_2Plane420Unorm;
//             case 0x30313050:    // P010
//                 return VkFormat.G10X6B10X6R10X6_2Plane420Unorm3Pack16;
//             // Add more as needed (DRM_FORMAT_YUV420, etc.)
//             default:
//                 return VkFormat.Undefined;
//         }
//     }
//
//     // -------------------------------------------------------------
//     // Find memory type
//     // -------------------------------------------------------------
//     private bool FindMemoryType(
//         uint typeFilter,
//         MemoryPropertyFlags properties,
//         out uint index)
//     {
//         index = 0;
//
//         PhysicalDeviceMemoryProperties memProps;
//         vk.GetPhysicalDeviceMemoryProperties(physicalDevice, &memProps);
//
//         for (uint i = 0; i < memProps.MemoryTypeCount; i++)
//         {
//             if ((typeFilter & (1u << (int)i)) != 0 &&
//                 (memProps.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
//             {
//                 index = i;
//                 return true;
//             }
//         }
//         return false;
//     }
//
//     // -------------------------------------------------------------
//     // Error string
//     // -------------------------------------------------------------
//     private static string FFmpegError(int error)
//     {
//         byte[] buffer = new byte[256];
//         fixed (byte* ptr = buffer)
//         {
//             av_strerror(error, ptr, (ulong)buffer.Length);
//         }
//         return Encoding.UTF8.GetString(buffer).TrimEnd('\0');
//     }
//
//     // -------------------------------------------------------------
//     // Dispose
//     // -------------------------------------------------------------
//     public void Dispose()
//     {
//         configPacket = null;
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
//             AVBufferRef* tmp = hwDeviceCtx;
//             av_buffer_unref(&tmp);
//             hwDeviceCtx = null;
//         }
//
//         codec = null;
//     }
// }
//
// #endif














// my_av_linux.cs
#if LINUX

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

using Androidplayer.Src.Keymap.K_store;

using FFmpeg.AutoGen;
using static FFmpeg.AutoGen.ffmpeg;

using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;

using VkDevice = Silk.NET.Vulkan.Device;
using VkFormat = Silk.NET.Vulkan.Format;
using VkImage  = Silk.NET.Vulkan.Image;

namespace Androidplayer;

// -----------------------------------------------------------------
// Manual interop shim for libavutil's DRM hwcontext structs.
// FFmpeg.AutoGen's default generated bindings do not include
// hwcontext_drm.h, so these are hand-declared to match the stable
// public ABI in libavutil/hwcontext_drm.h. If you upgrade
// FFmpeg.AutoGen to a version that generates these natively, delete
// this block and use the generated types instead.
// -----------------------------------------------------------------
public unsafe struct AVDRMObjectDescriptor
{
    public int    fd;
    public uint   format_modifier_lo;
    public uint   format_modifier_hi;
    public nuint  size;

    public ulong format_modifier
    {
        get => ((ulong)format_modifier_hi << 32) | format_modifier_lo;
        set
        {
            format_modifier_lo = (uint)(value & 0xFFFFFFFF);
            format_modifier_hi = (uint)(value >> 32);
        }
    }
}

public unsafe struct AVDRMPlaneDescriptor
{
    public int    object_index;
    public int    offset;
    public int    pitch;
}

public unsafe struct AVDRMLayerDescriptor
{
    public uint                 format;      // DRM fourcc
    public int                  nb_planes;
    public fixed byte           _planes[4 * 12]; // AVDRMPlaneDescriptor[4], manually indexed below
}

public unsafe struct AVDRMFrameDescriptor
{
    public int nb_objects;
    public fixed byte _objects[4 * 24]; // AVDRMObjectDescriptor[4], manually indexed below
    public int nb_layers;
    public fixed byte _layers[4 * (4 + 4 + 4 * 12)]; // AVDRMLayerDescriptor[4], manually indexed below
}

public unsafe class my_AV_linux : IDisposable
{
    // -------------------------------------------------------------
    // FFmpeg
    // -------------------------------------------------------------
    private AVCodec*        codec;
    private AVCodecContext* codecCtx;
    private AVBufferRef*    hwDeviceCtx = null;

    private bool hwInitialized       = false;
    private bool useSoftwareFallback = false;

    // -------------------------------------------------------------
    // Vulkan / Silk.NET
    // -------------------------------------------------------------
    private Vk                     vk;
    private Instance               instance;
    private PhysicalDevice         physicalDevice;
    private VkDevice               device;

    // NOTE: VK_EXT_external_memory_dma_buf and VK_EXT_image_drm_format_modifier
    // must still be enabled on VkDeviceCreateInfo at device-creation time.
    // Only VK_KHR_external_memory_fd exposes loadable device-level functions
    // (vkGetMemoryFdPropertiesKHR), so it's the only one of the three fetched
    // via TryGetDeviceExtension.
    private KhrExternalMemoryFd              extMemFd;

    // -------------------------------------------------------------
    // scrcpy config packet
    // -------------------------------------------------------------
    private byte[] configPacket = null;

    public long FrameCount { get; private set; }
    public int  Width      { get; private set; }
    public int  Height     { get; private set; }

    // -------------------------------------------------------------
    // ctor
    // -------------------------------------------------------------
    public my_AV_linux(
        Vk vk,
        Instance instance,
        PhysicalDevice physicalDevice,
        VkDevice device)
    {
        this.vk             = vk             ?? throw new ArgumentNullException(nameof(vk));
        this.instance       = instance;
        this.physicalDevice = physicalDevice;
        this.device         = device;

        ffmpeg.RootPath = AppContext.BaseDirectory;

        // Load the Vulkan extension we need for DMA-BUF import.
        // VK_EXT_external_memory_dma_buf / VK_EXT_image_drm_format_modifier add
        // no device-level functions of their own (only enum values consumed by
        // other extensions' functions), so Silk.NET has no loader type for them
        // and they must simply be enabled at device-creation time instead.
        if (!vk.TryGetDeviceExtension(instance, device, out extMemFd))
            throw new Exception("VK_KHR_external_memory_fd not available");

        codec = avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264);
        if (codec == null)
            throw new Exception("H264 codec not found");

        codecCtx = avcodec_alloc_context3(codec);
        if (codecCtx == null)
            throw new Exception("Failed to allocate codec context");

        codecCtx->flags           |= AV_CODEC_FLAG_LOW_DELAY;
        codecCtx->flags2          |= AV_CODEC_FLAG2_FAST;
        codecCtx->skip_frame       = AVDiscard.AVDISCARD_DEFAULT;
        codecCtx->skip_loop_filter = AVDiscard.AVDISCARD_DEFAULT;
        codecCtx->refs             = 1;

        TryInitVaapiHwAccel();

        if (hwInitialized && hwDeviceCtx != null)
            codecCtx->hw_device_ctx = av_buffer_ref(hwDeviceCtx);

        if (avcodec_open2(codecCtx, codec, null) < 0)
            throw new Exception("Failed to open codec");

        Console.WriteLine(
            $"[my_av_linux] Decoder initialized. HW: {hwInitialized}, SW fallback: {useSoftwareFallback}");
    }

    // -------------------------------------------------------------
    // VAAPI hw accel init
    // -------------------------------------------------------------
    private void TryInitVaapiHwAccel()
    {
        try
        {
            hwDeviceCtx = av_hwdevice_ctx_alloc(
                AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI);

            if (hwDeviceCtx == null)
            {
                Console.WriteLine(
                    "[my_av_linux] av_hwdevice_ctx_alloc(VAAPI) failed, using software fallback");
                useSoftwareFallback = true;
                return;
            }

            // No extra fields needed: FFmpeg opens /dev/dri/renderD128 by default.
            // If you want a specific device:
            //   var ctx = (AVHWDeviceContext*)hwDeviceCtx->data;
            //   var va  = (AVVAAPIDeviceContext*)ctx->hwctx;
            //   va->device_name = (byte*)Marshal.StringToHGlobalAnsi("/dev/dri/renderD128");

            int ret = av_hwdevice_ctx_init(hwDeviceCtx);
            if (ret < 0)
            {
                Console.WriteLine(
                    $"[my_av_linux] av_hwdevice_ctx_init failed: {FFmpegError(ret)}");
                useSoftwareFallback = true;
                hwDeviceCtx = null;
                return;
            }

            if (!CheckVaapiCodecSupport(codec))
            {
                Console.WriteLine(
                    "[my_av_linux] codec does not advertise VAAPI hwaccel, using software fallback");
                useSoftwareFallback = true;
                return;
            }

            hwInitialized = true;
            Console.WriteLine("[my_av_linux] VAAPI hardware acceleration initialized");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[my_av_linux] VAAPI setup failed: {ex.Message}, using software fallback");
            useSoftwareFallback = true;
            hwDeviceCtx = null;
        }
    }

    private static bool CheckVaapiCodecSupport(AVCodec* codec)
    {
        for (int i = 0; ; i++)
        {
            AVCodecHWConfig* cfg = avcodec_get_hw_config(codec, i);
            if (cfg == null) break;

            if ((cfg->methods & 0x01) == 0) continue;                 // HW_DEVICE_CTX
            if (cfg->pix_fmt == AVPixelFormat.AV_PIX_FMT_NONE) continue;

            if (cfg->device_type == AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI &&
                cfg->pix_fmt     == AVPixelFormat.AV_PIX_FMT_VAAPI)
                return true;
        }
        return false;
    }

    // -------------------------------------------------------------
    // Public API — same shape as Windows, but returns VkImage
    // -------------------------------------------------------------
    public VkImage Decode(byte[] h264Data)
    {
        if (h264Data == null || h264Data.Length == 0)
            return default;

        return DecodeH264Packet(h264Data, 0);
    }

    public VkImage DecodePacket(byte[] h264Data, long pts, bool isConfig)
    {
        if (h264Data == null || h264Data.Length == 0)
            return default;

        if (isConfig)
        {
            configPacket = new byte[h264Data.Length];
            System.Buffer.BlockCopy(h264Data, 0, configPacket, 0, h264Data.Length);
            Console.WriteLine(
                $"[my_av_linux] Stored H264 config packet: {configPacket.Length} bytes");
            return default;
        }

        byte[] packetData;
        if (configPacket != null)
        {
            packetData = new byte[configPacket.Length + h264Data.Length];
            System.Buffer.BlockCopy(configPacket, 0, packetData, 0, configPacket.Length);
            System.Buffer.BlockCopy(h264Data,     0, packetData, configPacket.Length, h264Data.Length);
            configPacket = null;
        }
        else
        {
            packetData = h264Data;
        }

        return DecodeH264Packet(packetData, pts);
    }

    // -------------------------------------------------------------
    // Decode loop
    // -------------------------------------------------------------
    private VkImage DecodeH264Packet(byte[] h264Data, long pts)
    {
        AVPacket* packet = av_packet_alloc();
        if (packet == null) return default;

        VkImage lastImage = default;

        try
        {
            fixed (byte* pData = h264Data)
            {
                packet->data = pData;
                packet->size = h264Data.Length;
                packet->pts  = pts;
                packet->dts  = pts;

                int ret = avcodec_send_packet(codecCtx, packet);
                if (ret < 0 && ret != AVERROR(EAGAIN))
                {
                    Console.WriteLine(
                        $"[my_av_linux] avcodec_send_packet failed: {ret} ({FFmpegError(ret)})");
                    return default;
                }

                while (true)
                {
                    AVFrame* frame = av_frame_alloc();
                    if (frame == null) break;

                    try
                    {
                        ret = avcodec_receive_frame(codecCtx, frame);
                        if (ret == AVERROR(EAGAIN) || ret == AVERROR_EOF) break;
                        if (ret < 0)
                        {
                            Console.WriteLine(
                                $"[my_av_linux] avcodec_receive_frame failed: {ret} ({FFmpegError(ret)})");
                            break;
                        }

                        bool isVaapiFrame =
                            frame->format == (int)AVPixelFormat.AV_PIX_FMT_VAAPI;

                        VkImage image = default;

                        if (hwInitialized && isVaapiFrame)
                        {
                            image = ConvertVaapiFrameToVkImage(frame);
                        }

                        if (image.Handle != 0)
                        {
                            lastImage = image;
                            FrameCount++;
                            Width  = frame->width;
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
            av_packet_unref(packet);
            av_packet_free(&packet);
        }

        return lastImage;
    }

    // =============================================================
    // VAAPI frame -> DRM PRIME -> VkImage
    // =============================================================
    private VkImage ConvertVaapiFrameToVkImage(AVFrame* vaapiFrame)
    {
        if (vaapiFrame == null)
            return default;

        AVFrame* drmFrame = av_frame_alloc();
        if (drmFrame == null)
            return default;

        VkImage result = default;
        DeviceMemory memory = default;

        try
        {
            const int AV_HWFRAME_MAP_READ_FLAG = 1;
            // The destination frame's format must be set before calling
            // av_hwframe_map so FFmpeg knows what to map into.
            drmFrame->format = (int)AVPixelFormat.AV_PIX_FMT_DRM_PRIME;

            // Map the VAAPI surface to a DRM PRIME frame (dma_buf fd + modifier).
            // This is a ZERO-COPY operation — same physical surface.
            int ret = av_hwframe_map(
                drmFrame,
                vaapiFrame,
                (int)AV_HWFRAME_MAP_READ_FLAG);

            if (ret < 0)
            {
                Console.WriteLine(
                    $"[my_av_linux] av_hwframe_map(DRM_PRIME) failed: {ret} ({FFmpegError(ret)})");
                return default;
            }

            if (drmFrame->format != (int)AVPixelFormat.AV_PIX_FMT_DRM_PRIME)
            {
                Console.WriteLine(
                    $"[my_av_linux] expected DRM_PRIME, got {(AVPixelFormat)drmFrame->format}");
                return default;
            }

            AVDRMFrameDescriptor* desc = (AVDRMFrameDescriptor*)drmFrame->data[0];
            if (desc == null)
                return default;

            int nbObjects = desc->nb_objects;
            int nbLayers  = desc->nb_layers;
            if (nbObjects == 0 || nbLayers == 0)
                return default;

            AVDRMObjectDescriptor* objects =
                (AVDRMObjectDescriptor*)Unsafe_AsPointer(desc, ObjectsOffset);
            AVDRMLayerDescriptorView layer0 =
                ReadLayer((byte*)desc + LayersOffset, 0);

            // NV12 = 2 planes; but scrcpy H.264 usually lands here as 1 or 2 layers.
            uint planeCount = (uint)layer0.nb_planes;
            if (planeCount > 2) planeCount = 2;

            VkFormat vkFormat = MapDrmFormatToVkFormat(layer0.format, planeCount);

            if (vkFormat == VkFormat.Undefined)
            {
                Console.WriteLine(
                    $"[my_av_linux] unsupported DRM format 0x{layer0.format:X}");
                return default;
            }

            AVDRMObjectDescriptor obj0 = objects[0];

            // ---- Build VkImageCreateInfo (DRM modifier tiling) ----
            var planeLayouts = stackalloc SubresourceLayout[(int)planeCount];

            for (int i = 0; i < planeCount; i++)
            {
                var plane = layer0.planes[i];

                planeLayouts[i] = new SubresourceLayout
                {
                    Offset     = (ulong)plane.offset,
                    Size       = 0,
                    RowPitch   = (ulong)plane.pitch,
                    ArrayPitch = 0,
                    DepthPitch = 0,
                };
            }

            var drmModifier = new ImageDrmFormatModifierExplicitCreateInfoEXT
            {
                SType                       = StructureType.ImageDrmFormatModifierExplicitCreateInfoExt,
                DrmFormatModifier           = obj0.format_modifier,
                DrmFormatModifierPlaneCount = planeCount,
                PPlaneLayouts               = planeLayouts,
                PNext                       = null,
            };

            // Mutable format needed for DMA-BUF images
            var formatList = stackalloc VkFormat[1];
            formatList[0] = vkFormat;

            var mutableInfo = new ImageFormatListCreateInfo
            {
                SType           = StructureType.ImageFormatListCreateInfo,
                ViewFormatCount = 1,
                PViewFormats    = formatList,
                PNext           = &drmModifier,
            };

            var extImageInfo = new ExternalMemoryImageCreateInfo
            {
                SType       = StructureType.ExternalMemoryImageCreateInfo,
                HandleTypes = ExternalMemoryHandleTypeFlags.DmaBufBitExt,
                PNext       = &mutableInfo,
            };

            var imageInfo = new ImageCreateInfo
            {
                SType         = StructureType.ImageCreateInfo,
                PNext         = &extImageInfo,
                ImageType     = ImageType.Type2D,
                Format        = vkFormat,
                Extent        = new Extent3D((uint)drmFrame->width, (uint)drmFrame->height, 1),
                MipLevels     = 1,
                ArrayLayers   = 1,
                Samples       = SampleCountFlags.Count1Bit,
                Tiling        = ImageTiling.DrmFormatModifierExt,
                Usage         = ImageUsageFlags.SampledBit,
                SharingMode   = SharingMode.Exclusive,
                InitialLayout = ImageLayout.Undefined,
            };

            // `result` is a local variable (stack-resident, never GC-moved), so
            // its address can be taken directly — no `fixed` statement needed
            // or allowed here.
            VkImage* pImage = &result;
            if (vk.CreateImage(device, &imageInfo, null, pImage) != Result.Success)
            {
                Console.WriteLine("[my_av_linux] vkCreateImage failed");
                return default;
            }

            // ---- Memory requirements + bind ----
            MemoryRequirements memReq;
            vk.GetImageMemoryRequirements(device, result, &memReq);

            MemoryFdPropertiesKHR fdProps;
            extMemFd.GetMemoryFdProperties(
                device,
                ExternalMemoryHandleTypeFlags.DmaBufBitExt,
                obj0.fd,
                &fdProps);

            uint memoryTypeBits = memReq.MemoryTypeBits & fdProps.MemoryTypeBits;

            uint memoryTypeIndex;
            if (!FindMemoryType(memoryTypeBits, MemoryPropertyFlags.DeviceLocalBit, out memoryTypeIndex))
            {
                Console.WriteLine("[my_av_linux] no suitable memory type for import");
                vk.DestroyImage(device, result, null);
                result = default;
                return default;
            }

            // Import the dma_buf fd as Vulkan memory
            var importInfo = new ImportMemoryFdInfoKHR
            {
                SType      = StructureType.ImportMemoryFDInfoKhr,
                HandleType = ExternalMemoryHandleTypeFlags.DmaBufBitExt,
                Fd         = obj0.fd,
            };

            var allocInfo = new MemoryAllocateInfo
            {
                SType           = StructureType.MemoryAllocateInfo,
                PNext           = &importInfo,
                AllocationSize  = memReq.Size,
                MemoryTypeIndex = memoryTypeIndex,
            };

            DeviceMemory* pMem = &memory;
            if (vk.AllocateMemory(device, &allocInfo, null, pMem) != Result.Success)
            {
                Console.WriteLine("[my_av_linux] vkAllocateMemory (import) failed");
                vk.DestroyImage(device, result, null);
                result = default;
                return default;
            }

            if (vk.BindImageMemory(device, result, memory, 0) != Result.Success)
            {
                Console.WriteLine("[my_av_linux] vkBindImageMemory failed");
                vk.FreeMemory(device, memory, null);
                vk.DestroyImage(device, result, null);
                result = default;
                return default;
            }

            // NOTE: memory ownership has now been transferred to the VkImage.
            // The dma_buf fd from obj0.fd is still owned by FFmpeg.
            // Do not close it here.

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[my_av_linux] VAAPI->VkImage failed: {ex.Message}");
            return default;
        }
        finally
        {
            av_frame_free(&drmFrame);
        }
    }

    // -------------------------------------------------------------
    // DRM fourcc -> VkFormat
    // -------------------------------------------------------------
    private static VkFormat MapDrmFormatToVkFormat(uint fourcc, uint planeCount)
    {
        // DRM_FORMAT_NV12 = 'N','V','1','2' = 0x3231564E
        // DRM_FORMAT_P010 = 0x30313050
        switch (fourcc)
        {
            case 0x3231564E:    // NV12
                // return VkFormat.G8B8R8_2Plane420Unorm;
                return VkFormat.G8B8R82Plane420Unorm;
            case 0x30313050:    // P010
                return VkFormat.G10X6B10X6R10X62Plane420Unorm3Pack16;
            // Add more as needed (DRM_FORMAT_YUV420, etc.)
            default:
                return VkFormat.Undefined;
        }
    }

    // -------------------------------------------------------------
    // Find memory type
    // -------------------------------------------------------------
    private bool FindMemoryType(
        uint typeFilter,
        MemoryPropertyFlags properties,
        out uint index)
    {
        index = 0;

        PhysicalDeviceMemoryProperties memProps;
        vk.GetPhysicalDeviceMemoryProperties(physicalDevice, &memProps);

        for (uint i = 0; i < memProps.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1u << (int)i)) != 0 &&
                (memProps.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
            {
                index = i;
                return true;
            }
        }
        return false;
    }

    // -------------------------------------------------------------
    // Manual DRM descriptor readers
    // (see AVDRMFrameDescriptor shim note at top of file)
    // -------------------------------------------------------------
    private const int ObjectsOffset = 4;                    // sizeof(int) nb_objects
    private const int ObjectStride  = 24;                    // sizeof(AVDRMObjectDescriptor)
    private const int LayersOffset  = ObjectsOffset + 4 * ObjectStride + 4; // + nb_layers field
    private const int LayerStride   = 4 + 4 + 4 * 12;         // format + nb_planes + planes[4]
    private const int PlaneStride   = 12;                     // sizeof(AVDRMPlaneDescriptor)

    private struct AVDRMPlaneView
    {
        public int object_index;
        public int offset;
        public int pitch;
    }

    private struct AVDRMLayerDescriptorView
    {
        public uint format;
        public int  nb_planes;
        public AVDRMPlaneView[] planes;
    }

    private static void* Unsafe_AsPointer(AVDRMFrameDescriptor* desc, int offset)
        => (byte*)desc + offset;

    private static AVDRMLayerDescriptorView ReadLayer(byte* layersBase, int index)
    {
        byte* p = layersBase + index * LayerStride;

        uint format    = *(uint*)p;
        int  nb_planes = *(int*)(p + 4);
        if (nb_planes > 4) nb_planes = 4;

        var planes = new AVDRMPlaneView[nb_planes];
        byte* planesBase = p + 8;

        for (int i = 0; i < nb_planes; i++)
        {
            byte* pp = planesBase + i * PlaneStride;
            planes[i] = new AVDRMPlaneView
            {
                object_index = *(int*)pp,
                offset       = *(int*)(pp + 4),
                pitch        = *(int*)(pp + 8),
            };
        }

        return new AVDRMLayerDescriptorView
        {
            format    = format,
            nb_planes = nb_planes,
            planes    = planes,
        };
    }

    // -------------------------------------------------------------
    // Error string
    // -------------------------------------------------------------
    private static string FFmpegError(int error)
    {
        byte[] buffer = new byte[256];
        fixed (byte* ptr = buffer)
        {
            av_strerror(error, ptr, (ulong)buffer.Length);
        }
        return Encoding.UTF8.GetString(buffer).TrimEnd('\0');
    }

    // -------------------------------------------------------------
    // Dispose
    // -------------------------------------------------------------
    public void Dispose()
    {
        configPacket = null;

        if (codecCtx != null)
        {
            AVCodecContext* tmp = codecCtx;
            avcodec_free_context(&tmp);
            codecCtx = null;
        }

        if (hwDeviceCtx != null)
        {
            AVBufferRef* tmp = hwDeviceCtx;
            av_buffer_unref(&tmp);
            hwDeviceCtx = null;
        }

        codec = null;
    }
}

#endif