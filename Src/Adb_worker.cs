

using System;
using System.Linq;
using System.Threading;
// using System.Windows.Threading;
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
        private readonly object adbLock = new object();
        
        
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
        
        
        private System.Timers.Timer _devicePollTimer;
        private readonly HashSet<string> _knownSerials = new HashSet<string>();
        private readonly object _pollLock = new object();

        public Adb_worker()
        {
           
            _isCounting = false;
            Console.WriteLine("AdbWorker initialized");
            
            try
            {
                AdbServer server = new AdbServer();
                // StartServerResult result = server.StartServer(@"adb\adb.exe", false);
                
                string adbPath = "adb\adb.exe";

                Console.WriteLine($"is this linux {OperatingSystem.IsLinux()}");

                if (OperatingSystem.IsWindows())
                {
                    adbPath = Path.Combine(AppContext.BaseDirectory, "adb.exe");
                }
                else if (OperatingSystem.IsLinux())
                {
                    Console.WriteLine("im on Linux");
                    adbPath = Path.Combine(AppContext.BaseDirectory, "adb");
                }
                
                StartServerResult result = server.StartServer(adbPath ,false);
                
                
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
                
                
                _devicePollTimer = new System.Timers.Timer(2000); // every 2 seconds
                _devicePollTimer.Elapsed += (s, e) => PollDevices();
                _devicePollTimer.AutoReset = true;
                _devicePollTimer.Start();
                

                SwitchToTcpIp();

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

        
        
        
        private void PollDevices()
        {
            lock (_pollLock)
            {
                try
                {
                    // var current = adbClient.GetDevices();


                    
        
                
                    var allDevices = adbClient.GetDevices();
            
            
                    Console.WriteLine("--- Connected devices ---");
                    foreach (var d in allDevices)
                    {
                        
                        if (device.Serial == d.Serial)
                        {
                            Console.WriteLine($"wireless device is {d.State}");


                            if (d.State.ToString() == "Offline")
                            {
                                
                                // break;
                            }
                        }
                    }
                    
        
                    // Disconnected
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PollDevices error: {ex.Message}");
                }
            }
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
            // adbClient.RemoveAllReverseForwards(device);
        }

        private void run()
        {

            Thread.Sleep(3000);
           
                // var devices = adbClient.GetDevices().FirstOrDefault();
                var devices = GetDeviceBasedOnMode();

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
              
                // var devices = adbClient.GetDevices().FirstOrDefault();
                var devices = GetDeviceBasedOnMode();
                
                
                

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
                    "send_frame_meta=true"
                };
var back_cmd = new List<string>
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
                    "send_frame_meta=false", 
                    "</dev/null >/dev/null 2>&1 &"
                };

                
                
                //
                // "audio_bit_rate=16000",
                // "audio_codec=opus",
                //
              
                Thread.Sleep(200);

                // Kill any leftover scrcpy server
                try
                {
                    var killReceiver = new ConsoleOutputReceiver();
                    adbClient.ExecuteRemoteCommand("pkill -f com.genymobile.scrcpy.Server", device, killReceiver);
                    Thread.Sleep(300);  // give it time to die
                }
                catch { /* ignore if nothing to kill */ }
             
                try
                {
                    var killReceiver = new ConsoleOutputReceiver();
                    adbClient.ExecuteRemoteCommand(
                        "for p in $(ps -A | grep scrcpy | awk '{print $2}'); do kill -9 $p; done",
                        device, killReceiver);
                }
                catch { }
                
                
                
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


                adbClient.CreateForward(device, 1011, "localabstract:scrcpy");
                adbClient.CreateForward(device, 1012, "localabstract:scrcpy");
                adbClient.CreateForward(device, 1013, "localabstract:scrcpy");
                
                // string result = ShellHelper_2.ExecuteCommand(adb_cmd);
                // Console.WriteLine(result);
                
                ProgressChanged?.Invoke(65, $"Staging server...");
                
                // adb forward tcp:1717 localabstract:minicap
                
                string command = string.Join(" ", cmd);
                // string command = string.Join(" ", back_cmd);
                
                
                // _ = adbClient.ExecuteRemoteCommandAsync(command, devices, receiver, cts.Token);
                

                
                
                
               

                // Console.WriteLine(command);
                
                

                ProgressChanged?.Invoke(88, $"Starting server...");
                
                Thread.Sleep(200);
                
                
                ProgressChanged?.Invoke(100, $"server started!");
                

                CountingCompleted?.Invoke();
               
                try
                {
                    // string adb_cmd2 = "cd adb && adb.exe  shell \"CLASSPATH=/data/local/tmp/scrcpy-server.jar app_process / com.genymobile.scrcpy.Server 3.3.2 log_level=info video=true max_size=1080 video_bit_rate=8000000 max_fps=30 video_codec=h264 audio=false tunnel_forward=true control=true cleanup=true send_device_meta=true send_codec_meta=true send_frame_meta=false </dev/null >/dev/null 2>&1 &\"";
                    //
                    // string result2 = ShellHelper_2.ExecuteCommand(adb_cmd2);
                    // Console.WriteLine(result2);
                    //
                    _ = adbClient.ExecuteRemoteCommandAsync(command, device, receiver, cts.Token);
                 
                    // _ = adbClient.ExecuteRemoteCommandAsync(command, device, receiver, cts.Token, 10);

                    
                    // adbClient.ExecuteRemoteCommandAsync(command, device, receiver, cts.Token).Wait(cts.Token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ADB command cancelled.");
                    ErrorOccurred?.Invoke("server exited");
                }

                Console.WriteLine("adb continued running ##########");


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

     
        
        
        
private DeviceData GetDeviceBasedOnMode()
{
    lock (adbLock)
    {
        // Respect the actual setting instead of hardcoding "Wireless".
        // string mode = UISettings.Instance.SelectedConnectionType;
        string mode = "Wireless";

        if (mode == "USB")
        {
            return GetUsbDevice();
        }
        else if (mode == "Wireless")
        {
            return GetWirelessDevice();
        }

        return null;
    }
}


private DeviceData GetUsbDevice()
{
    var usbDevice = adbClient.GetDevices()
        .FirstOrDefault(d => d.State == DeviceState.Online && !d.Serial.Contains(":"));

    device = usbDevice;

    if (usbDevice == null)
        Console.WriteLine("No USB device found.");

    return usbDevice;
}


private DeviceData GetWirelessDevice()
{
    string stored_ip = UISettings.Instance.DeviceIP;


    Console.WriteLine($"found ip {stored_ip}");
    
    
    // 1. Already connected wirelessly? Just use it.
    var existingWireless = adbClient.GetDevices()
        .FirstOrDefault(d => d.State == DeviceState.Online && d.Serial.Contains(":"));
    
    // var wireless = adbClient.GetDevices()
    //     .FirstOrDefault(d => d.State == DeviceState.Online 
    //                          && d.Serial.Contains(":"));

    if (existingWireless != null)
    {
        device = existingWireless;
        return device;
    }

    Console.WriteLine("Existing wireless device not found");
    
    //
    
    
        
        
    if (stored_ip != null)
    {
        try
        {

            Console.WriteLine("connecting to ip");
            // adbClient.Connect($"{stored_ip}:5555");
            adbClient.Connect($"{stored_ip}");
            device = adbClient.GetDevices()
                .FirstOrDefault(d => d.State == DeviceState.Online && d.Serial.Contains(":"));
        
            // device = devices;

            if (device != null)
            {
                return device;
            }
            
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            // throw;
        }
        
            
            
    }
    
    // 
    
    
    
    
    
    

    // 2. Need a USB device to bootstrap from.
    var usbDevice = adbClient.GetDevices()
        .FirstOrDefault(d => d.State == DeviceState.Online && !d.Serial.Contains(":"));

    if (usbDevice == null)
    {
        Console.WriteLine("No USB device available to bootstrap wireless connection. " +
                           "Plug the device in via USB at least once to set up wireless mode.");
        return null;
    }

    // 3. Get the IP while the USB link is still stable — do this BEFORE
    //    touching tcpip mode, since that briefly kills the USB adb link.
    string ip = GetDeviceIp(usbDevice);

    if (string.IsNullOrEmpty(ip))
    {
        Console.WriteLine("Could not determine device IP over USB. " +
                           "Make sure Wi-Fi is on and connected on the device.");
        return null;
    }

    if (UISettings.Instance.DeviceIP != ip)
    {
        UISettings.Instance.DeviceIP = ip;
        UISettings.Instance.Save();
        Console.WriteLine($"Device IP updated to: {ip}");
    }

    // 4. Now switch the device into tcpip mode (this is what causes the
    //    "device disconnected" / "device connected (Offline)" blip you saw).
    //    Done via SharpAdbClient's own socket instead of spawning a second
    //    adb.exe process, which was fighting with the running server.
    
    
    
    // Console.WriteLine("Switching device to TCP/IP mode...");
    // if (!SwitchToTcpIp(usbDevice, 5555))
    // {
    //     return null;
    // }

    // Give the adbd daemon time to restart in tcp mode. Poll instead of a
    // single fixed sleep so we don't race it.
    
    
    // DeviceData wireless = null;
    // for (int i = 0; i < 10 && wireless == null; i++)
    // {
    //     Thread.Sleep(500);
    //
    //     try
    //     {
    //         adbClient.Connect($"{ip}:5555");
    //     }
    //     catch (Exception e)
    //     {
    //         Console.WriteLine($"Connect attempt failed: {e.Message}");
    //         continue;
    //     }
    //
    //     wireless = adbClient.GetDevices()
    //         .FirstOrDefault(d => d.State == DeviceState.Online && d.Serial == $"{ip}:5555");
    // }
    
    
    // After SwitchToTcpIp(usbDevice, 5555):

    DeviceData wireless = null;
    int maxAttempts = 10; // 30 * 1s = 30 seconds max wait

    for (int i = 0; i < maxAttempts && wireless == null; i++)
    {
        Thread.Sleep(1000); // Poll once per second, not 500ms

        try
        {
            // Check if the wireless device has already appeared in the list
            wireless = adbClient.GetDevices()
                .FirstOrDefault(d => d.State == DeviceState.Online 
                                     && d.Serial.Contains($"{ip}"));

            if (wireless != null) break;

            // If not, try to initiate the connection
            Console.WriteLine($"Attempt {i + 1}: connecting to {ip}:5555...");
            adbClient.Connect($"{ip}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Attempt {i + 1} failed: {e.Message}");
            // Don't continue immediately — the catch already falls through to the sleep
        }
    }

    if (wireless == null)
    {
        Console.WriteLine("Failed to establish wireless connection after tcpip switch.");
        return null;
    }

    Console.WriteLine($"Connected wirelessly to {wireless.Serial} ({wireless.Model})");
    device = wireless;
    return device;
}






private string GetDeviceIp(DeviceData targetDevice)
{
    try
    {
        if (targetDevice == null || targetDevice.State != DeviceState.Online)
        {
            Console.WriteLine("No online device for IP lookup");
            return null;
        }

        var receiver = new ConsoleOutputReceiver();
        adbClient.ExecuteRemoteCommand("ip route", targetDevice, receiver);

        string output = receiver.ToString();
        Console.WriteLine(output);

        int index = output.IndexOf("src ");
        if (index >= 0)
        {
            string ip = output.Substring(index + 4).Split(' ')[0].Trim();
            return string.IsNullOrWhiteSpace(ip) ? null : ip;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"GetDeviceIp failed: {ex.Message}");
    }

    return null;
}
     
        
        
private bool SwitchToTcpIp(DeviceData targetDevice = null, int port = 5555)
{
    try
    {
        using (IAdbSocket socket = Factories.AdbSocketFactory(adbClient.EndPoint))
        {
            socket.SetDevice(targetDevice);           // <-- restore this
            socket.SendAdbRequest($"tcpip:{port}");
            var response = socket.ReadAdbResponse();   // throws on FAIL
        }

        Console.WriteLine($"Device acked tcpip:{port}");
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to switch device to tcpip mode: {ex.Message}");
        return false;
    }
}

        
        
        

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
        }
    }
}