

using System;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
// using SharpAdbClient;
    
using System.Collections.Generic;
using System.IO;
using System.Net;

// using AdvancedSharpAdbClient;
// using AdvancedSharpAdbClient.Models;
// using AdvancedSharpAdbClient.Receivers;
using SharpAdbClient;

using System;
using System.Diagnostics;
using Androidplayer.windows;

namespace Androidplayer.Src
{
    
    
    public class ShellHelper_2
{
    public static string ExecuteCommand(string command)
    {
       
            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = new Process())
            {
                process.StartInfo = processInfo;
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (!string.IsNullOrEmpty(error))
                {
                    throw new Exception("error: " + error) ;
                   
                }

                return output;
            }
        
    }

    // For PowerShell commands
    
}

    
    
    public class Adb_worker : IDisposable
    {
        private Thread _adbThread;
        
        private bool _isCounting;
        private bool _isDisposed = false;
        
        private CancellationTokenSource cts;

        public event Action<int, string> ProgressChanged;
        public event Action CountingCompleted;  // Only this event - no progress updates
        public event Action devicedisconnected;
        public event Action<string> ErrorOccurred;
        
        private readonly AdbClient adbClient;
        private DeviceData device;
        
        public bool is_deviceconnected = false;
        
        
        
        
        public string JAR = "scrcpy-server.jar";
        
       
        
        public string VERSION = "1.20";
        public int max_size = 1080;
        public int bitrate = 8000000;
        // public int bitrate = 20000;
        public int max_fps = 60;
        public bool block_frame = true;
        public bool stay_awake = false;
        public int lock_screen_orientation = -1;
        public bool skip_same_frame = false;
        public double min_frame_interval => 1.0 / max_fps;

        public Adb_worker()
        {
           
            _isCounting = false;
            Console.WriteLine("AdbWorker initialized");
            
            try
            {
                AdbServer server = new AdbServer();
                StartServerResult result = server.StartServer(@"adb\adb.exe", false);
                
                if (result != StartServerResult.Started)
                {
                    Console.WriteLine($"Server start result: {result}");
                    Console.WriteLine("Can't start adb server");
                }
                
                adbClient = new AdbClient();

                Console.WriteLine("running adb");


               
                
                
                var monitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));
                monitor.DeviceDisconnected += this.OnDeviceDisconnected;
                monitor.DeviceConnected += this.OnDeviceConnected;
                monitor.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error initializing ADB: {e}");
                ErrorOccurred?.Invoke($"Error initializing ADB: {e}");

            }
            
            
        }

        private void OnDeviceConnected(object? sender, DeviceDataEventArgs e)
        {
            
           
            is_deviceconnected =  true;
            
            StartCounting();
        }

        private void OnDeviceDisconnected(object? sender, DeviceDataEventArgs e)
        {
            // Console.WriteLine("device disconnected event is working");
            if (cts != null && !cts.IsCancellationRequested)
            {
                
                cts.Cancel();
            }
            
            is_deviceconnected =  false;
            
            ErrorOccurred?.Invoke("server exited");
            
            devicedisconnected.Invoke();
        }

        public void StartCounting()
        {
            try
            {
                Stop(); // Clean up existing
                
                StartCountThread();
            }
            catch (Exception ex)
            {
                // OnErrorOccurred($"Error starting counter: {ex.Message}");

                Console.WriteLine(ex);
            }
        }

        private void StartCountThread()
        {
            if (_isCounting) return;

            if (_adbThread?.IsAlive == true)
            {
                return;
            }

            _isCounting = true;
            _adbThread = new Thread(run)
            {
                IsBackground = true,
                Name = "CounterThread"
            };
            _adbThread.Start();
            
        }

        public void Stop()
        {
            _isCounting = false;

            if (cts != null && !cts.IsCancellationRequested)
            {
                
                cts.Cancel();
            }
            
            // Disconnect a specific device (TCP/IP)
            // if (device != null)
            // {
            //     adbClient.Disconnect(new DnsEndPoint(device.Serial, 5555)); 
            // }


            
            if (_adbThread != null && _adbThread.IsAlive)
            {
                _adbThread.Join(1000);
                // while (_adbThread.IsAlive)
                // {
                //     
                //     Thread.Sleep(2000);
                //     
                //     
                //
                //     Console.WriteLine("Adb worker still Alive....");
                //     
                //     // _adbThread.Abort();
                //     // _adbThread = null;
                // }
                // _adbThread = null;
            }
        }

        // private void UploadMobileServer()
        // {
        //     using SyncService service = new(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)), device);
        //     using Stream stream = File.OpenRead(JAR);
        //     service.Push(stream, "/data/local/tmp/scrcpy-server.jar", 444, DateTime.Now, null, CancellationToken.None);
        // }
        
        private void UploadMobileServer()
        {
            using SyncService service = new(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)), device);
            using Stream stream = File.OpenRead(JAR);
            service.Push(stream, "/data/local/tmp/scrcpy-server.jar", 444, DateTime.Now, null, CancellationToken.None);
        }
        
        private void MobileServerCleanup()
        {
            // Remove any existing network stuff.
            adbClient.RemoveAllForwards(device);
            adbClient.RemoveAllReverseForwards(device);
        }

        private void run()
        {

           
                var devices = adbClient.GetDevices().FirstOrDefault();

                if (devices == null )
                {
                    Console.WriteLine("No devices connected.");
                    Thread.Sleep(2000);
                    // continue;
                    
                    return;
                    
                }
                
                Console.WriteLine($"current audio is : {UISettings.Instance.AudioEnabled}");
                
                Deploy_server();
            
            // while (_isCounting)
            // {
            //     
            //     // var devices = adbClient.GetDevices();
            //     
            //     
            //     break;
            //     
            // }
            
            
            
        }
        
        private void Deploy_server()
        {


            try
            {
                
                
                if ( adbClient == null)
                {
                    
                    return;
                }
              
                var devices = adbClient.GetDevices().FirstOrDefault();

                device = devices;
                
                // var devices = adbClient.Instance.GetDevices().First();

                
                
                
                if (devices == null)
                {
                    Console.WriteLine("No devices found");
                    _isCounting = false;
                    return;
                }
                
                

                Console.WriteLine($"Found device: {devices}");
                
                ProgressChanged?.Invoke(20, $"connecting to : {devices}");
                
                
              
                
                // var cmd = new List<string>
                // {
                //     "CLASSPATH=/data/local/tmp/scrcpy-server.jar",
                //     "app_process",
                //     "/",
                //     "com.genymobile.scrcpy.Server",
                //     "3.3.2",
                //     "log_level=info",
                //     "video=true",
                //     
                //     "audio=true",
                //     
                //     
                //     
                //     $"max_size={max_size}",
                //     $"video_bit_rate={bitrate}",
                //     
                //     
                //     $"max_fps={max_fps}",
                //     "tunnel_forward=true",
                //     "control=true",
                //     
                //     "video_codec=h264",
                //     
                //     
                //     "cleanup=true",
                //     "send_device_meta=true",
                //     "send_codec_meta=true",
                //     "send_frame_meta=false"
                // };
                
                
                
                var cmd = new List<string>
                {
                    "CLASSPATH=/data/local/tmp/scrcpy-server.jar",
                    "app_process",
                    "/",
                    "com.genymobile.scrcpy.Server",
                    "3.3.2",

                    "log_level=info",

                    "video=true",

                    // ---- VIDEO SETTINGS FIRST ----
                    $"max_size={max_size}",
                
                    $"video_bit_rate={1000000 * UISettings.Instance.Bitrate}",
                    

                    $"max_fps={UISettings.Instance.FPS}",
                    "video_codec=h264",

                    // ---- AUDIO SETTINGS AFTER VIDEO ----
                    $"audio={UISettings.Instance.AudioEnabled}",
                    


                    // ---- CONTROL + TUNNEL ----
                    "tunnel_forward=true",
                    "control=true",

                    "cleanup=true",
                    "send_device_meta=true",
                    "send_codec_meta=true",
                    "send_frame_meta=false"
                };

                
                
                //
                // "audio_bit_rate=16000",
                // "audio_codec=opus",
                //
              
                Thread.Sleep(200);

             
                
                UploadMobileServer();
                
            
                Console.WriteLine("File pushed successfully");
                
                ProgressChanged?.Invoke(40, $"Pushing server file...");

             

                Thread.Sleep(200);

                
                cts = new CancellationTokenSource();
                var receiver = new ConsoleOutputReceiver();
                // var receiver = new LiveOutputReceiver();
                // adbClient.CreateForwardAsync(devices, 1234, "localabstract:scrcpy",cts.Token).Wait();
              
                
                MobileServerCleanup();
                
                
             

                // string adb_cmd = "cd adb && adb.exe forward tcp:1234 localabstract:scrcpy && adb.exe forward tcp:12345 localabstract:scrcpy && adb forward tcp:1717 localabstract:minicap";
                
                string adb_cmd = "cd adb && adb.exe forward tcp:1011 localabstract:scrcpy && adb.exe forward tcp:1012 localabstract:scrcpy && adb.exe forward tcp:1013 localabstract:scrcpy";
                
                string result = ShellHelper_2.ExecuteCommand(adb_cmd);
                Console.WriteLine(result);
                
                ProgressChanged?.Invoke(65, $"Staging server...");
                
                // adb forward tcp:1717 localabstract:minicap
                
                string command = string.Join(" ", cmd);
                // _ = adbClient.ExecuteRemoteCommandAsync(command, devices, receiver, cts.Token);
                

                
                
                
               

                // Console.WriteLine(command);
                
                

                ProgressChanged?.Invoke(88, $"Starting server...");
                
                Thread.Sleep(200);
                
                
                ProgressChanged?.Invoke(100, $"server started!");
                

                CountingCompleted?.Invoke();
               
                try
                {
                    _ = adbClient.ExecuteRemoteCommandAsync(command, device, receiver, cts.Token);
                    // adbClient.ExecuteRemoteCommandAsync(command, device, receiver, cts.Token).Wait(cts.Token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ADB command cancelled.");
                    ErrorOccurred?.Invoke("server exited");
                }


                // adbClient.ExecuteRemoteCommand(command, device, receiver);

                // Console.WriteLine("Server deployment completed!");

                // ErrorOccurred?.Invoke("server exited");

                // Invoke completion event (thread-safe for console app)
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeployServer: {ex}");
                ErrorOccurred?.Invoke(ex.Message);
            }
           
        }

        public bool IsCounting => _isCounting;

     

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
        }
    }
}