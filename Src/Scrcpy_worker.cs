using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Androidplayer.Audio;
using Avalonia.Threading;

#if WINDOWS
using Androidplayer.Rendering.win;
using SharpDX.Direct3D11;
using SharpDX.XAudio2;
using SharpDX.Multimedia;
using SharpDX;


using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SharpDX.Direct3D11;
#endif





using Buffer = System.Buffer;
using Androidplayer.Src.Keymap.K_store;
using Androidplayer.Store;
using Androidplayer.windows;
using Androidplayer.Src.Keymap;
using Avalonia;
using Avalonia.Media;

namespace Androidplayer.Src
{
    public class Scrcpy_worker : IDisposable
    {

   
        private IAudioPlayer? _audioPlayer;
        private Video_recorder? _recorder;

        ///////////////////

        private Thread scrcpy_thread;
        private Thread audio_thread;

        private ManualResetEventSlim audioReadyEvent = new ManualResetEventSlim(false);

      
        private bool isrunning;

        private bool recive_audio = false;
        private bool isDisposed = false;

        private string host = "127.0.0.1";
        private int port = 1234;

        private TcpClient? videoClient;
        private TcpClient? controlClient;
        private TcpClient? audioClient;

        private CancellationTokenSource? cts;

        private bool screenshot = false;

        public string DeviceName { get; private set; } = "";
        public int Width { get; private set; }
        public int Height { get; private set; }

        private int timeoutMs = 15000;

        private int VideoWidth = 0;
        private int VideoHeight = 0;

        private int device_width = 0;
        private int device_height = 0;

        // private my_AV? _decoder;
#if WINDOWS          
        private my_AV_win? _decoder;
        public Device dx_Device { get; set; }
        
                    
#endif
        
        
        private my_audio? _audio_decoder;


        private static readonly ArrayPool<byte> pool = ArrayPool<byte>.Shared;

        public event Action Frame_almostready;
        // public event Action<Texture2D> FrameReady;
        public event Action<string> ErrorOccurred;
        public event Action scrcpy_desposed;

        public event Action<(int Width, int Height)> DeviceResolutionReady;
        public event Action<(int Width, int Height)> videosizeReady;
        public event Action<TcpClient> ControlSocketReady;


        private Stopwatch _frameTimer;
        private long _lastFrameTime;
        private int _framesDropped;
        private const double TARGET_FRAME_TIME_MS = 30;
        private const double MAX_FRAME_TIME_MS = 16.67;
        
        
#if WINDOWS
           
        private Texture2D _previousFrame = null;
        private Texture2D _pendingFrame;
#endif

        private sealed class ScrcpyVideoPacket
        {
            public byte[] Data { get; init; }
            public long Pts { get; init; }
            public bool IsConfig { get; init; }
            public bool IsKeyFrame { get; init; }
        }

        public Scrcpy_worker()
        {
            
            isrunning = false;

            _audio_decoder = new my_audio();

            #if WINDOWS
                _audioPlayer = new WindowsAudioPlayer();
            #elif LINUX
                _audioPlayer = new LinuxAudioPlayer();
            #elif MACOS
                _audioPlayer = new MacOSAudioPlayer();
            #endif
            
                _audioPlayer?.Initialize();
            
                
             my_info.Instance.PropertyChanged += my_infoOnPropertyChanged;

             
        }

        private void my_infoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(my_info.Recording))
            {


                if (my_info.Instance.Recording)
                {
                    StartRecording();
                }
                else
                {
                    
                    StopRecording();
                    
                }
                
                
                
            }

            if (e.PropertyName == nameof(my_info.TakeScreenshot))
            {
                
                if (my_info.Instance.TakeScreenshot)
                {
                    // SaveScreenshot(_previousFrame);
                    
                    
                    #if WINDOWS
                            if (k_info.Instance.my_renderer is DirectX dx)
                            {
                                // SaveScreenshot runs entirely under _renderLock,
                                // so the decoder can't dispose _previousFrame mid-copy.
                                dx.RunOnContext(_ =>
                                {
                                    if (_previousFrame != null && !_previousFrame.IsDisposed)
                                    {
                                        SaveScreenshot(_previousFrame);
                                    }
                                    else
                                    {
                                        Console.WriteLine("[Screenshot] no frame available yet");
                                    }
                                });
                            }
                    #endif
                    
                    
                    
                    
                    my_info.Instance.TakeScreenshot = false;
                }
                
                
            }
            
            
            
        }

        public void Start()
        {
            isrunning = true;

            scrcpy_thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "VideoPlaybackThread"
            };

            audio_thread = new Thread(start_audio)
            {
                IsBackground = true,
                Name = "AudioPlaybackThread"
            };

            scrcpy_thread.Start();
            audio_thread.Start();
        }

      
        
        
        
        public void Stop()
        {
            isrunning = false;

            // Close sockets first to unblock Read()
            try { videoClient?.Close(); }   catch { }
            try { audioClient?.Close(); }   catch { }
            try { controlClient?.Close(); } catch { }
            videoClient = null;
            audioClient = null;
            controlClient = null;

            audioReadyEvent.Set();

            if (scrcpy_thread != null && scrcpy_thread.IsAlive)
            {
                scrcpy_thread.Join(200);   // give it a bit more time now that Read throws
                scrcpy_thread = null;
            }

            if (audio_thread != null && audio_thread.IsAlive)
            {
                audio_thread.Join(200);
                audio_thread = null;
            }
        }
        
        

        

        public void TakeScreenshot(string word)
        {
            if (word == "screenshot")
            {
                screenshot = true;
            }
        }

        #region Audio Player & VideoRecorder

        
        

        // private void StartRecording()
        // {
        //     if (_recorder != null) return;
        //
        //     var dir = Path.Combine(
        //         Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        //         "Androidplayer");
        //     Directory.CreateDirectory(dir);
        //
        //     var file = Path.Combine(dir, $"rec_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
        //     _recorder = new Video_recorder(file);
        //     Console.WriteLine($"[recorder] recording to {file}");
        // }

        // In Scrcpy_worker.cs

      

        private void StartRecording()
        {
            if (_recorder != null) return;

            // Wait for decoder to be ready with actual dimensions
            if (_decoder == null || _decoder.Width <= 0 || _decoder.Height <= 0)
            {
                Console.WriteLine("[recorder] ERROR: Decoder not ready or invalid dimensions");
                return;
            }

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "Androidplayer");
            Directory.CreateDirectory(dir);

            var file = Path.Combine(dir, $"rec_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

            // Use actual decoder dimensions
            int w = _decoder.Width;
            int h = _decoder.Height;

            _recorder = new Video_recorder(file, w, h);

            // IMPORTANT: Seed extradata from the live decoders
            var videoConfig = _decoder?.LastConfig;
            if (videoConfig != null && videoConfig.Length > 0)
            {
                Console.WriteLine($"[recorder] Seeding video config: {videoConfig.Length} bytes");
                _recorder.SetVideoExtradata(videoConfig);
            }
            else
            {
                Console.WriteLine("[recorder] WARNING: no video config available at start");
            }

            var audioConfig = _audio_decoder?.PendingExtradata;
            if (audioConfig != null && audioConfig.Length > 0)
            {
                Console.WriteLine($"[recorder] Seeding audio config: {audioConfig.Length} bytes");
                _recorder.SetAudioExtradata(audioConfig);
            }

            Console.WriteLine($"[recorder] recording to {file} ({w}x{h})");
        }

      
        
        
        private void StopRecording()
        {
            string finishedPath = _recorder.Path;
            _recorder.Dispose();
            _recorder = null;

            Console.WriteLine($"[recorder] stopped, file at: {finishedPath}");

            Dispatcher.UIThread.Post(() =>
            {
                OverlayManager.Instance.ShowToast("done", $"Recording saved: {finishedPath}");
            });
        }
        
 

        private void start_audio()
        {
            const long PACKET_FLAG_CONFIG = 1L << 62;
            const long PACKET_FLAG_KEY_FRAME = 1L << 61;

            while (isrunning)
            {
                audioReadyEvent.Wait();

                if (audioClient == null)
                {
                    audioClient = new TcpClient();

                    if (!audioClient.ConnectAsync(host, 1012).Wait(1000))
                    {
                        ErrorOccurred?.Invoke("Control connection timeout");
                        audioClient = null;
                        continue;
                    }

                    Console.WriteLine("audio socket connected");
                    continue;
                }

                try
                {
                    Console.WriteLine("started reciving audio....");

                    NetworkStream audioStream = audioClient.GetStream();
                    audioStream.ReadTimeout = Timeout.Infinite;

                    byte[] codecBuffer = new byte[4];
                    if (!ReadExact(audioStream, codecBuffer, 4))
                        throw new IOException("codec header read failed");

                    uint codecId = BinaryPrimitives.ReadUInt32BigEndian(codecBuffer);
                    Console.WriteLine($"Audio codec ID: {codecId}");

                    byte[] frameMeta = new byte[12];

                    while (isrunning && audioClient.Connected)
                    {
                        if (!ReadExact(audioStream, frameMeta, 12))
                        {
                            Console.WriteLine("audio stream closed (frame meta)");
                            break;
                        }

                        long ptsAndFlags =
                            BinaryPrimitives.ReadInt64BigEndian(
                                frameMeta.AsSpan(0, 8));

                        int packetSize =
                            (int)BinaryPrimitives.ReadUInt32BigEndian(
                                frameMeta.AsSpan(8, 4));

                        bool isConfig =
                            (ptsAndFlags & PACKET_FLAG_CONFIG) != 0;

                        if (packetSize <= 0 || packetSize > 1_000_000)
                        {
                            Console.WriteLine($"Invalid audio packet size: {packetSize}");
                            break;
                        }

                        byte[] payload = new byte[packetSize];
                        if (!ReadExact(audioStream, payload, packetSize))
                        {
                            Console.WriteLine("audio stream closed (payload)");
                            break;
                        }

                        if (isConfig)
                        {
                            Console.WriteLine(
                                $"Audio config packet: {payload.Length} bytes");
                            _audio_decoder?.SetExtradata(payload);
                            _recorder?.SetAudioExtradata(payload);
                            continue;
                        }

                        long ptsUs = ptsAndFlags & ((1L << 62) - 1);
                        _recorder?.WriteAudioPacket(payload, ptsUs);
                        
                        
                        byte[]? pcm = _audio_decoder?.Decode(payload);
                        

           
                        if (pcm != null && pcm.Length > 0)
                        {
                            _audioPlayer?.Play(pcm);
                        }

                    }
                }
                catch (IOException ioEx) when (ioEx.InnerException is SocketException sockEx)
                {
                    Console.WriteLine($"Audio socket error: {sockEx.SocketErrorCode}");
                    ErrorOccurred?.Invoke(sockEx.Message);
                    audioReadyEvent.Reset();

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Audio receive error: {ex.Message}");
                    ErrorOccurred?.Invoke(ex.Message);
                    audioReadyEvent.Reset();

                }
                finally
                {
                    try { audioClient?.Close(); } catch { }
                    audioClient = null;
                }

                Thread.Sleep(16);
            }
        }

        private static bool ReadExact(NetworkStream stream, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read;
                try { read = stream.Read(buffer, offset, count - offset); }
                catch { return false; }

                if (read <= 0) return false;
                offset += read;
            }
            return true;
        }

        #endregion

        private void Run()
        {
            while (isrunning)
            {
                try
                {
                    Thread.Sleep(2000);
                    videoClient = new TcpClient();
                    if (!videoClient.ConnectAsync(host, 1011).Wait(timeoutMs))
                    {
                        ErrorOccurred?.Invoke("Connection timeout");
                    }

                    videoClient.NoDelay = true;

                    var infoStream = videoClient.GetStream();
                    infoStream.ReadTimeout = 2000;

                    byte[] dummyByte = new byte[1];
                    int dummyRead = infoStream.Read(dummyByte, 0, 1);
                    Console.WriteLine($"Dummy byte read: {dummyRead} bytes, value: {dummyByte[0]}");

                    if (dummyRead != 1)
                    {
                        ErrorOccurred?.Invoke($"Expected to read dummy byte (1 byte), but got {dummyRead} bytes.");
                        
                       
                        Thread.Sleep(1000);
                        
                        continue;
                    }

                    // Thread.Sleep(500);

                    if (UISettings.Instance.AudioEnabled)
                    {
                        audioReadyEvent.Set();
                    }

                    Thread.Sleep(500);

                    controlClient = new TcpClient();
                    if (!controlClient.ConnectAsync(host, 1013).Wait(timeoutMs))
                    {
                        ErrorOccurred?.Invoke("Control connection timeout");
                    }

                    ControlSocketReady?.Invoke(controlClient);

                    ReadDeviceInfo();

                    ReceiveVideoData();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    ErrorOccurred?.Invoke(e.Message);
                }
            }
        }

        private void ReadDeviceInfo()
        {
            if (videoClient == null) throw new InvalidOperationException("Not connected");

            var infoStream = videoClient.GetStream();
            infoStream.ReadTimeout = 2000;

            var deviceInfoBuf = pool.Rent(64);
            int bytesRead = infoStream.Read(deviceInfoBuf, 0, 64);

            Console.WriteLine("recived device info");
            Console.WriteLine($"Received device info: {bytesRead} bytes");

            if (bytesRead != 64)
            {
                ErrorOccurred?.Invoke($"Expected to read exactly 64 bytes for device name, but got {bytesRead} bytes.");
                return;
            }

            DeviceName = Encoding.UTF8.GetString(deviceInfoBuf, 0, 64).TrimEnd('\0');
            Console.WriteLine("Device name: " + DeviceName);

            byte[] codecMeta = new byte[12];
            int codecBytesRead = infoStream.Read(codecMeta, 0, 12);
            if (codecBytesRead != 12)
            {
                ErrorOccurred?.Invoke($"Expected 12 bytes for codec metadata, got {codecBytesRead}");
                return;
            }

            uint codecId = BinaryPrimitives.ReadUInt32BigEndian(codecMeta.AsSpan(0, 4));
            Width = (int)BinaryPrimitives.ReadUInt32BigEndian(codecMeta.AsSpan(4, 4));
            Height = (int)BinaryPrimitives.ReadUInt32BigEndian(codecMeta.AsSpan(8, 4));

            Console.WriteLine($"Codec ID: {codecId}, Resolution: {Width}x{Height}");

            device_width = Width;
            device_height = Height;

            
            
            DeviceResolutionReady?.Invoke((Width, Height));
        }

        private short ReadInt16BigEndian(byte[] buffer, int offset)
        {
            if (BitConverter.IsLittleEndian)
            {
                return (short)((buffer[offset] << 8) | buffer[offset + 1]);
            }

            return BitConverter.ToInt16(buffer, offset);
        }
#if WINDOWS
        private void RenderTexture(Texture2D frame)
        {
            if (_frameTimer == null)
            {
                _frameTimer = new Stopwatch();
                _frameTimer.Start();
            }

            if (frame == null) return;

            _pendingFrame?.Dispose();
            _pendingFrame = frame;

            long currentTime = _frameTimer.ElapsedMilliseconds;
            long timeSinceLastFrame = currentTime - _lastFrameTime;

            if (_lastFrameTime == 0 || timeSinceLastFrame >= MAX_FRAME_TIME_MS)
            {
                if (VideoHeight != _decoder.Height || VideoWidth != _decoder.Width)
                {
                    VideoHeight = _decoder.Height;
                    VideoWidth = _decoder.Width;

                    if (VideoWidth > VideoHeight)
                    {
                        if (device_height > device_width)
                        {
                            DeviceResolutionReady?.Invoke((device_height, device_width));
                        }
                        else if (device_width > device_height)
                        {
                            DeviceResolutionReady?.Invoke((device_width, device_height));
                        }
                    }
                    else
                    {
                        if (device_height > device_width)
                        {
                            DeviceResolutionReady?.Invoke((device_width, device_height));
                        }
                        else if (device_width > device_height)
                        {
                            DeviceResolutionReady?.Invoke((device_height, device_width));
                        }
                    }

                    Console.WriteLine($"from scrcpy worker : {VideoWidth} , {VideoHeight}");
                    videosizeReady?.Invoke((VideoWidth, VideoHeight));
                }
                
                dynamic  my_directx = null ;
                // Console.WriteLine("presenting frames");
                  
                if (UISettings.Instance.Nativeview_mode)
                {

                    var renderer = k_info.Instance.my_renderer as DirectX;
                    // my_directx = k_info.Instance.directx;
                    renderer?.PresentFrame(_pendingFrame);
                }
                else
                {
                    // my_directx = D11InteropRenderer.Instance;
                    
                    long decodeTimestamp = D11InteropRenderer.Instance.NowTicks;

                    if (_pendingFrame != null)
                        D11InteropRenderer.Instance?.PresentFrame(_pendingFrame, decodeTimestamp);

                }



                
             
                
                
                
                _lastFrameTime = currentTime;
                _pendingFrame = null;

                if (_decoder.FrameCount % 100 == 0)
                {
                    Console.WriteLine($"Frames: {_decoder.FrameCount}");
                }
            }
            else
            {
                _framesDropped++;
            }
        }

        

           
#endif
        
        
        
        private bool ShouldSkipFrame()
        {
            if (_lastFrameTime == 0) return false;

            long currentTime = _frameTimer.ElapsedMilliseconds;
            long timeSinceLastFrame = currentTime - _lastFrameTime;

            return timeSinceLastFrame < TARGET_FRAME_TIME_MS;
        }
        
        
     #if WINDOWS

private void SaveScreenshot(Texture2D frame)
{
    try
    {
        if (frame == null || frame.IsDisposed || frame.NativePointer == IntPtr.Zero)
        {
            Console.WriteLine("[Screenshot] frame is null/disposed");
            return;
        }

        var device = frame.Device;
        if (device == null || device.IsDisposed || device.NativePointer == IntPtr.Zero)
        {
            Console.WriteLine("[Screenshot] device is null/disposed");
            return;
        }

        int width = frame.Description.Width;
        int height = frame.Description.Height;

        using var staging = new Texture2D(
            device,
            new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = frame.Description.Format,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CpuAccessFlags = CpuAccessFlags.Read,
                OptionFlags = ResourceOptionFlags.None
            });

        // IMPORTANT: do NOT dispose the ImmediateContext.
        // It is owned by the Device and shared with the renderer/decoder.
        var context = device.ImmediateContext;

        context.CopyResource(frame, staging);

        var mapped = context.MapSubresource(
            staging,
            0,
            MapMode.Read,
            MapFlags.None);

        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "Androidplayer");
            Directory.CreateDirectory(directory);

            string filePath = Path.Combine(
                directory,
                $"Screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.jpg");

            using var bitmap = new Bitmap(
                width,
                height,
                PixelFormat.Format32bppArgb);

            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                int rowBytes = width * 4;

                if (mapped.RowPitch == rowBytes && bitmapData.Stride == rowBytes)
                {
                    // Fast path: contiguous rows on both sides, one memcpy.
                    long total = (long)rowBytes * height;
                    byte[] all = new byte[total];
                    Marshal.Copy(mapped.DataPointer, all, 0, (int)total);
                    Marshal.Copy(all, 0, bitmapData.Scan0, (int)total);
                }
                else
                {
                    // Slow path: honor each side's stride, row by row.
                    byte[] row = new byte[rowBytes];

                    for (int y = 0; y < height; y++)
                    {
                        IntPtr source = mapped.DataPointer + y * mapped.RowPitch;
                        IntPtr destination = bitmapData.Scan0 + y * bitmapData.Stride;

                        Marshal.Copy(source, row, 0, rowBytes);
                        Marshal.Copy(row, 0, destination, rowBytes);
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            bitmap.Save(filePath, ImageFormat.Jpeg);

            Console.WriteLine($"[Screenshot] Saved: {filePath}");

            Dispatcher.UIThread.Post(() =>
            {
                OverlayManager.Instance.ShowToast("done", $"[Screenshot] Saved: {filePath}");
            });
        }
        finally
        {
            context.UnmapSubresource(staging, 0);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Screenshot] Error: {ex}");
    }
}

#endif

        private void ReceiveVideoData()
        {
            if (videoClient == null || !videoClient.Connected)
                return;

            NetworkStream stream = videoClient.GetStream();

            stream.ReadTimeout = Timeout.Infinite;

            byte[] buffer = new byte[0x100000];

            Console.WriteLine("Starting to receive H264 video data...");

            #if WINDOWS
            
            _decoder = new my_AV_win(dx_Device);
            #endif

            Frame_almostready.Invoke();

            int num = 8;
            string mm = null;

            Console.WriteLine("im reciving data");
            // Console.WriteLine(isrunning);
            // Console.WriteLine(videoClient.Connected);

            while (isrunning && videoClient.Connected)
            {
                try
                {
                    ScrcpyVideoPacket packet = ReadScrcpyVideoPacket(stream);

                    if (packet == null ||
                        packet.Data == null ||
                        packet.Data.Length == 0)
                    {
                        continue;
                    }

                    Stopwatch sw = new Stopwatch();

                    try
                    {
                        sw.Restart();
                        
                              
#if WINDOWS


                        Texture2D frame = null;
                        dynamic  my_directx = null ;

                        if (packet.IsConfig)
                        {
                            _recorder?.SetVideoExtradata( packet.Data);
                        }
                        else
                            _recorder?.WriteVideoPacket( packet.Data, packet.Pts, packet.IsKeyFrame);
                  
                        if (UISettings.Instance.Nativeview_mode)
                        {

                           // my_directx = k_info.Instance.directx;
                           
                           var renderer = k_info.Instance.my_renderer as DirectX;

                           // Console.WriteLine($"[decode] native renderer={(renderer is null ? "NULL" : "ok")} " +
                           //                   $"device={(renderer?.my_Device is null ? "NULL" : renderer.my_Device.NativePointer.ToString("X"))}");
                           
                           renderer?.RunOnContext(_ =>
                                                   {
                                                       
                                                   frame = _decoder.DecodePacket(
                                                       packet.Data,
                                                       packet.Pts,
                                                       packet.IsConfig);
                           
                                                   if (frame == null)
                                                   {
                                                       Console.WriteLine("frame is null");
                                                       return;
                                                       // continue;
                                                   }

                                              
                                                   
                           
                                                   // ---- Avalonia: ImageContainer is an Avalonia.Controls.Canvas.
                                                   // Use Bounds instead of ActualWidth/ActualHeight, and null-guard.
                                                   if (k_info.Instance.ImageContainer is { } container)
                                                   {
                                                       double cw = container.Bounds.Width;
                                                       double ch = container.Bounds.Height;
                           
                                                       if (My_Store.Instance.DisplayHeight == 0 ||
                                                           My_Store.Instance.DisplayHeight != (int)cw)
                                                       {
                                                           My_Store.Instance.SetDisplayResolution((int)cw, (int)ch);
                                                       }
                                                   }
                           
                                                   if (My_Store.Instance.VideoHeight == 0 || My_Store.Instance.VideoWidth == 0)
                                                   {
                                                       My_Store.Instance.SetVideoResolution(frame.Description.Width, frame.Description.Height);
                                                   }
                           
                                                   if (My_Store.Instance?.DeviceHeight == 0 ||
                                                       My_Store.Instance?.DeviceWidth == 0 && my_info.Instance.DeveloperMode)
                                                   {
                                                       My_Store.Instance.SetDeviceResolution(frame.Description.Width, frame.Description.Height);
                                                   }
                           
                                                   if (_previousFrame != null && !_previousFrame.IsDisposed)
                                                   {
                                                       _previousFrame.Dispose();
                                                   }
                           
                                                   _previousFrame = frame;
                                                       
                                                   });
                                                   
                        }
                        else
                        {
                            // my_directx = D11InteropRenderer.Instance;

                            // D11InteropRenderer.Instance?.RunOnContext(new Action<object>(_ =>
                                D11InteropRenderer.Instance?.RunOnContext(new Action<DeviceContext>(_ =>
                            {
                                
                            frame = _decoder.DecodePacket(
                                packet.Data,
                                packet.Pts,
                                packet.IsConfig);

                            if (frame == null)
                            {
                                return;
                                // continue;
                            }

                            // ---- Avalonia: ImageContainer is an Avalonia.Controls.Canvas.
                            // Use Bounds instead of ActualWidth/ActualHeight, and null-guard.
                            if (k_info.Instance.ImageContainer is { } container)
                            {
                                double cw = container.Bounds.Width;
                                double ch = container.Bounds.Height;

                                if (My_Store.Instance.DisplayHeight == 0 ||
                                    My_Store.Instance.DisplayHeight != (int)cw)
                                {
                                    My_Store.Instance.SetDisplayResolution((int)cw, (int)ch);
                                }
                            }

                            if (My_Store.Instance.VideoHeight == 0 || My_Store.Instance.VideoWidth == 0)
                            {
                                My_Store.Instance.SetVideoResolution(frame.Description.Width, frame.Description.Height);
                            }

                            if (My_Store.Instance?.DeviceHeight == 0 ||
                                My_Store.Instance?.DeviceWidth == 0 && my_info.Instance.DeveloperMode)
                            {
                                My_Store.Instance.SetDeviceResolution(frame.Description.Width, frame.Description.Height);
                            }

                            if (_previousFrame != null && !_previousFrame.IsDisposed)
                            {
                                _previousFrame.Dispose();
                            }

                            _previousFrame = frame;
                                
                            }));
                            
                        }


                        // if (k_info.Instance.directx._device == null)
                        // {
                        //     Console.WriteLine("device is null $$$$$$$$");
                        // }
                        //
                        
                        // Console.WriteLine($"decoding finished {frame}");
                        
                        // Console.WriteLine($"decoding finished ok: {frame.NativePointer:X}");
                        
                        // Console.WriteLine(frame is null
                        //     ? "[frame] NULL"
                        //     : $"[frame] ptr=0x{frame.NativePointer:X} {frame.Description.Width}x{frame.Description.Height}");
                        //
                        //
                        
                        

                        RenderTexture(frame);
                        
                        
                        
                        
#endif
                        

                        sw.Stop();

                        continue;
                    }
                    catch (Exception decodeEx)
                    {
                        // Console.WriteLine($"Decoder error (non-fatal): {decodeEx.StackTrace}");
                        Console.WriteLine($"[decode] EXCEPTION: {decodeEx}");
                        continue;
                    }
                }
                catch (IOException ex) when (ex.InnerException is SocketException sockEx)
                {
                    switch (sockEx.SocketErrorCode)
                    {
                        case SocketError.ConnectionReset:
                        case SocketError.ConnectionAborted:
                            Console.WriteLine($"Connection reset by peer: {sockEx.SocketErrorCode}");
                            ErrorOccurred?.Invoke(sockEx.Message);
                            break;

                        default:
                            continue;
                    }

                    break;
                }
            }

            Console.WriteLine("Video data receiving loop ended");
            ErrorOccurred?.Invoke("Video data receiving loop ended");
        }

        private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int read = stream.Read(buffer, offset, count);

                if (read <= 0)
                    throw new IOException("Connection closed while reading video packet.");

                offset += read;
                count -= read;
            }
        }

        protected virtual void OnErrorOccurred(string errorMessage)
        {
            Console.WriteLine($"Error occurred: {errorMessage}");
            // Avalonia: Dispatcher.UIThread.Post replaces BeginInvoke + DispatcherPriority
            Dispatcher.UIThread.Post(() =>
            {
                ErrorOccurred?.Invoke(errorMessage);
            });
        }

        private ScrcpyVideoPacket ReadScrcpyVideoPacket(Stream stream)
        {
            byte[] header = new byte[12];

            ReadExactly(stream, header, 0, 12);

            ulong ptsFlags =
                ((ulong)header[0] << 56) |
                ((ulong)header[1] << 48) |
                ((ulong)header[2] << 40) |
                ((ulong)header[3] << 32) |
                ((ulong)header[4] << 24) |
                ((ulong)header[5] << 16) |
                ((ulong)header[6] << 8) |
                header[7];

            int size =
                (header[8] << 24) |
                (header[9] << 16) |
                (header[10] << 8) |
                header[11];

            if (size <= 0 || size > 10 * 1024 * 1024)
                throw new InvalidDataException(
                    $"Invalid scrcpy video packet size: {size}");

            byte[] data = new byte[size];

            ReadExactly(stream, data, 0, size);

            bool isConfig =
                (ptsFlags & (1UL << 63)) != 0;

            bool isKeyFrame =
                (ptsFlags & (1UL << 62)) != 0;

            long pts =
                (long)(ptsFlags & ((1UL << 62) - 1));

            return new ScrcpyVideoPacket
            {
                Data = data,
                Pts = pts,
                IsConfig = isConfig,
                IsKeyFrame = isKeyFrame
            };
        }

        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            Stop();

            _frameTimer?.Stop();

            

            _audio_decoder.Dispose();

            
            
            
            _audioPlayer?.Dispose();
            _audioPlayer = null;
            
            
            
        }
    }
}