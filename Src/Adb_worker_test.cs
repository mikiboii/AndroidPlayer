

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
using System.ComponentModel;
using System.Diagnostics;
using Androidplayer.windows;
using SharpAdbClient.DeviceCommands;

namespace Androidplayer.Src
{
    
    
//     public class ShellHelper_2
// {
//     public static string ExecuteCommand(string command)
//     {
//        
//             var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
//             {
//                 CreateNoWindow = true,
//                 UseShellExecute = false,
//                 RedirectStandardOutput = true,
//                 RedirectStandardError = true
//             };
//
//             using (var process = new Process())
//             {
//                 process.StartInfo = processInfo;
//                 process.Start();
//
//                 string output = process.StandardOutput.ReadToEnd();
//                 string error = process.StandardError.ReadToEnd();
//
//                 process.WaitForExit();
//
//                 if (!string.IsNullOrEmpty(error))
//                 {
//                     throw new Exception("error: " + error) ;
//                    
//                 }
//
//                 return output;
//             }
//         
//     }
//
//     // For PowerShell commands
//     
// }
//
//     
//     
    public class Adb_worker_test : IDisposable
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
        
        private DeviceMonitor monitor;
        
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

        public Adb_worker_test()
        {
           
            _isCounting = false;
            Console.WriteLine("AdbWorker initialized");
            
            try
            {
                AdbServer server = new AdbServer();
                
                
                string adbPath = "adb.exe";

                Console.WriteLine($"is this linux {OperatingSystem.IsLinux()}");

                if (OperatingSystem.IsWindows())
                {
                    adbPath = Path.Combine(AppContext.BaseDirectory,  "adb.exe");
                }
                else if (OperatingSystem.IsLinux())
                {
                    Console.WriteLine("im on Linux");
                    adbPath = Path.Combine(AppContext.BaseDirectory,  "adb");
                }
                
                StartServerResult result = server.StartServer(adbPath ,false);

                
                
                // StartServerResult result = server.StartServer(@"adb\adb.exe", false);
                // StartServerResult result = server.StartServer(@"adb\adb", false);
                
                
                
                
                
                
                
                if (result != StartServerResult.Started)
                {
                    Console.WriteLine($"Server start result: {result}");
                    // Console.WriteLine("Can't start adb server");
                }
                
                adbClient = new AdbClient();
                
               

                Console.WriteLine("running adb");


                

                UISettings.Instance.PropertyChanged += OnUISettingsPropertyChanged;
                
                
                monitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));
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

        private void OnUISettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UISettings.SelectedConnectionType))
            {
                Console.WriteLine($"Connection mode changed to: {UISettings.Instance.SelectedConnectionType}");
            }
        }

        private void OnDeviceConnected(object? sender, DeviceDataEventArgs e)
        {
            device = e.Device;
            Console.WriteLine("device connected");
            is_deviceconnected =  true;

            Console.WriteLine(e.Device.State);

            if (e.Device.State == DeviceState.Unauthorized)
            {
                Console.WriteLine("Device is UNAUTHORIZED! Please accept the RSA fingerprint on your device.");
                // ErrorOccurred?.Invoke("Device unauthorized - please accept the RSA fingerprint on your device");
                return;
            }


            // SwitchToTcpIp(device);

            // var receiver = new ConsoleOutputReceiver();
            //
            // adbClient.ExecuteRemoteCommand("ip route", e.Device, receiver);
            //
            // string output = receiver.ToString().Trim();
            //
            // // string ip = "";
            //
            // int index = output.IndexOf("src ");
            // if (index >= 0)
            // {
            //     ip = output.Substring(index + 4).Split(' ')[0];
            // }
            //
            // Console.WriteLine(ip);


            
            // adbClient.Connect($"192.168.100.77:5555");

            
            
            // try
            // {
            //
            //     adbClient.Disconnect(new DnsEndPoint("192.168.100.77",5555));
            //
            //     if (device != null)
            //     {
            //         Console.WriteLine("disconnected device is null");
            //         
            //         return;
            //     }
            // }
            // catch (Exception exception)
            // {
            //     // Console.WriteLine(exception.Message);
            //
            //     Console.WriteLine("Error disconnecting from wireless device");
            //   
            // }
            
            
            
            
            
            // adbClient.Connect($"192.168.100.77");
           
            
            
            
            
            
            
            
            // var wireless = adbClient.GetDevices()
            //     .FirstOrDefault(d => d.State == DeviceState.Online 
            //                          && d.Serial.Contains(":"));
            //
            //
            //
            //
            // Console.WriteLine($"Found wireless device {wireless.Serial}");
            //
            //
            // var usbDevice = adbClient.GetDevices()
            //     .FirstOrDefault(d => d.State == DeviceState.Online && !d.Serial.Contains(":"));
            //
            //
            // Console.WriteLine($"Found usb device {usbDevice.Serial}");
            //
            // var allDevices = adbClient.GetDevices();
            //
            //
            // Console.WriteLine("--- Connected devices ---");
            // foreach (var d in allDevices)
            // {
            //     Console.WriteLine(d.Serial);
            // }
            
            
            
            Start();
        }
       
        
        
        private bool SwitchToTcpIp(DeviceData targetDevice, int port = 5555)
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
        
        
        private void OnDeviceDisconnected(object? sender, DeviceDataEventArgs e)
        {
            Console.WriteLine("device disconnected");
            if (cts != null && !cts.IsCancellationRequested)
            {
                
                cts.Cancel();
            }
            
            Stop();
            
            
            is_deviceconnected =  false;
            
            ErrorOccurred?.Invoke("server exited");
            
            devicedisconnected?.Invoke();
        }

        public void Start()
        {
            try
            {
                Stop(); // Clean up existing
                
                StartThread();
            }
            catch (Exception ex)
            {
                // OnErrorOccurred($"Error starting counter: {ex.Message}");

                Console.WriteLine(ex);
            }
        }

        private void StartThread()
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
                Name = "AdbThread"
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

        
        //
        // private DeviceData GetDeviceBasedOnMode()
        // {
        //     
        //     lock(adbLock)
        //     {
        //         
        //         // string mode = UISettings.Instance.SelectedConnectionType;
        //         string mode = "Wireless";
        //         
        //         
        //         // Console.WriteLine($"Getting device in mode: {mode}");
        //
        //         if (mode == "USB")
        //         {
        //
        //             try
        //             {
        //
        //                 if (UISettings.Instance.DeviceIP != "")
        //                 {
        //                 
        //                     
        //                 var endpoint = new DnsEndPoint(UISettings.Instance.DeviceIP, 5555);
        //                 adbClient.Disconnect(endpoint);
        //                 
        //                 
        //                 }
        //             }
        //             catch (Exception e)
        //             {
        //                 Console.WriteLine(e);
        //                 
        //             }
        //             
        //             // Get USB connected devices
        //             var usbDevices = adbClient.GetDevices()
        //                 .Where(d => d.State == DeviceState.Online)
        //                 .ToList();
        //             
        //             Console.WriteLine($"Found {usbDevices.Count} USB device(s)");
        //             return usbDevices.FirstOrDefault();
        //         }
        //         else if (mode == "Wireless")
        //         {
        //             // Get wireless devices (TCP/IP connected)
        // string ip = UISettings.Instance.DeviceIP;
        //
        // Console.WriteLine("entered wireless mode");
        //
        // var devices = adbClient.GetDevices().FirstOrDefault();
        //
        // device = devices;
        //
        //
        // if (device == null)
        // {
        //
        //     Console.WriteLine("device is null");
        // }
        //
        // // Console.WriteLine(device.Usb);
        //
        //
        // if (ip != null)
        // {
        //
        //     Console.WriteLine("connecting to ip");
        //     adbClient.Connect($"{ip}:5555");
        //     devices = adbClient.GetDevices().FirstOrDefault();
        //
        //     device = devices;
        //     
        //     
        // }
        //             
        //
        //             if (device != null)
        //             {
        //                 
        //                 // adbClient.ExecuteShellCommand(device, "tcpip 5555",  null);
        //                 
        //                 // using (IAdbSocket socket = Factories.AdbSocketFactory(adbClient.EndPoint))
        //                 // {
        //                 //     socket.SendAdbRequest($"host-serial:{device.Serial}:tcpip:5555");
        //                 //     socket.ReadAdbResponse();
        //                 // }
        //                 //
        //                 
        //                 // // 1) ask the adb server to switch that specific device to TCP mode on port 5555
        //                 // using (IAdbSocket socket = Factories.AdbSocketFactory(adbClient.EndPoint))
        //                 // {
        //                 //     // DO NOT call socket.SetDevice(device) here.
        //                 //     socket.SendAdbRequest($"host-serial:{device.Serial}:127.0.0.1:5555");
        //                 //     var response = socket.ReadAdbResponse(); // throws on FAIL
        //                 //     // response.Okay == true => success
        //                 // }
        //
        //                 
        //                 // ShellHelper_2.ExecuteCommand($"adb\\adb.exe -s {device.Serial} tcpip 5555");
        //                 // ShellHelper_2.ExecuteCommand($"adb\\adb.exe tcpip 5555");
        //                 
        //                 
        //                 
        //                 bool isTcpIpMode = device != null &&
        //                                    device.Serial.Contains(":5555");
        //
        //
        //                 
        //                 Console.WriteLine(device.Serial);
        //                 Console.WriteLine(isTcpIpMode);
        //                 try
        //                 {
        //                     if (!isTcpIpMode)
        //                     {
        //                         Console.WriteLine("Switching device to TCP/IP mode...");
        //
        //                         ShellHelper_2.ExecuteCommand(
        //                             $"adb\\adb.exe -s \"{device.Serial}\" tcpip 5555");
        //
        //
        //                     }
        //                     else
        //                     {
        //                         Console.WriteLine("Device is already in TCP/IP mode.");
        //                     }
        //
        //                 }
        //                 catch (Exception e)
        //                 {
        //                     Console.WriteLine(e);
        //                     
        //                 }
        //                 
        //                 Console.WriteLine("using tcpip 5555");
        //                 
        //                 Thread.Sleep(2000);
        //                 
        //             }
        //
        //             // while (device == null)
        //             // {
        //             //     
        //             //     
        //             //     
        //             //     Thread.Sleep(1000);
        //             // }
        //             //
        //
        //             if (ip == null)
        //             {
        //                 return null;
        //             }
        //             
        //             adbClient.Connect($"{ip}:5555");
        //             
        //             Console.WriteLine("reached here connecting");
        //            
        //             // var wireDevices = device = adbClient.GetDevices().FirstOrDefault();
        //                 
        //             // Console.WriteLine($"Found {wireDevices.Model} wireless device(s)");
        //             
        //             
        //             var wireDevices = adbClient.GetDevices().FirstOrDefault();
        //
        //             if (wireDevices == null)
        //             {
        //                 Console.WriteLine("No wireless device found after connect.");
        //
        //                 foreach (var d in adbClient.GetDevices())
        //                 {
        //                     Console.WriteLine($"Found device: {d.Serial}  State={d.State}");
        //                 }
        //
        //                 return null;
        //             }
        //
        //             device = wireDevices;
        //
        //             Console.WriteLine($"Found {device.Model}");
        //             
        //             
        //             
        //             if (devices == null)
        //             {
        //                 // Console.WriteLine("No devices found");
        //                 // _isCounting = false;
        //                 return null;
        //             }
        //
        //
        //             Console.WriteLine(device.Name);
        //             Console.WriteLine(device.Serial);
        //             Console.WriteLine(device.Model);
        //             Console.WriteLine(device.State);
        //             
        //             
        //             if (string.IsNullOrEmpty(ip))
        //             {
        //                 Console.WriteLine("No IP address set for wireless mode!");
        //                 
        //                 
        //                 
        //                 ip = GetDeviceIp();
        //
        //                 if (UISettings.Instance.DeviceIP != ip)
        //                 {
        //                     UISettings.Instance.DeviceIP = ip;
        //                     UISettings.Instance.Save();
        //                     Console.WriteLine($"Device IP updated to: {ip}");
        //                 }
        //                
        //                 
        //                 
        //                 
        //                 // return null;
        //             }
        //             else
        //             {
        //                 var newip = GetDeviceIp();
        //                 if (ip != newip)
        //                 {
        //                     ip = newip;
        //                     
        //                 }
        //             }
        //
        //             // try
        //             // {
        //             //     
        //             //     // adbClient.ExecuteRemoteCommand("tcpip 5555", device, null);
        //             //     
        //             //     
        //             //   
        //             //     adbClient.ExecuteShellCommand(device, "tcpip 5555",  null);
        //             //     
        //             //     
        //             //     // Try to connect to the device via TCP/IP
        //             //     // var endpoint = new DnsEndPoint(ip, 5555);
        //             //     Console.WriteLine($"Attempting wireless connection to {ip}:5555");
        //             //     
        //             //     // adbClient.Connect(endpoint);
        //             //     
        //             //     adbClient.Connect($"{ip}:5555");
        //             //     
        //             //     // Now get the device from the list
        //             //     // var wirelessDevices = adbClient.GetDevices()
        //             //     //     .Where(d => d.State == DeviceState.Online && d.Serial.Contains(ip))
        //             //     //     .ToList();
        //             //     var wirelessDevices = device = adbClient.GetDevices().FirstOrDefault();
        //             //     
        //             //     Console.WriteLine($"Found {wirelessDevices.Model} wireless device(s)");
        //             //     // return wirelessDevices.FirstOrDefault();
        //             //     return wirelessDevices;
        //             // }
        //             // catch (Exception ex)
        //             // {
        //             //     Console.WriteLine($"Failed to connect wirelessly to {ip}: {ex.Message}");
        //             //     
        //             //     
        //             //     
        //             //     // ErrorOccurred?.Invoke($"Wireless connection failed: {ex.Message}");
        //             //     return null;
        //             // }
        //         }
        //         
        //         
        //         return null;
        //     }
        // }
        //
        
        
        
        
        
        
        
        
        // ---------------------------------------------------------------------
        // Replacement for GetDeviceBasedOnMode() + GetDeviceIp()
        // Drop these into Adb_worker_test and delete the old versions.
        // ---------------------------------------------------------------------
        
        // private DeviceData GetDeviceBasedOnMode()
        // {
        //     lock (adbLock)
        //     {
        //         // Respect the actual setting instead of hardcoding "Wireless".
        //         // string mode = UISettings.Instance.SelectedConnectionType;
        //         string mode = "Wireless";
        //
        //         if (mode == "USB")
        //         {
        //             return GetUsbDevice();
        //         }
        //         else if (mode == "Wireless")
        //         {
        //             return GetWirelessDevice();
        //         }
        //
        //         return null;
        //     }
        // }
        //
        // /// <summary>
        // /// Returns a device that is connected over a physical USB cable
        // /// (i.e. its serial is a hardware serial, not an "ip:port" pair).
        // /// </summary>
        // private DeviceData GetUsbDevice()
        // {
        //     var usbDevice = adbClient.GetDevices()
        //         .FirstOrDefault(d => d.State == DeviceState.Online && !d.Serial.Contains(":"));
        //
        //     device = usbDevice;
        //
        //     if (usbDevice == null)
        //         Console.WriteLine("No USB device found.");
        //
        //     return usbDevice;
        // }
        //
        // /// <summary>
        // /// Returns a device reachable over Wi-Fi (adb tcpip). If one is already
        // /// connected, reuse it. Otherwise, bootstrap the wireless connection
        // /// from a USB-attached device: fetch its IP *first* (while USB is still
        // /// stable), THEN switch it into tcpip mode, THEN connect over the network.
        // /// </summary>
        // private DeviceData GetWirelessDevice()
        // {
        //     // 1. Already connected wirelessly? Just use it.
        //     var existingWireless = adbClient.GetDevices()
        //         .FirstOrDefault(d => d.State == DeviceState.Online && d.Serial.Contains(":5555"));
        //
        //     if (existingWireless != null)
        //     {
        //         device = existingWireless;
        //         return device;
        //     }
        //
        //     // 2. Need a USB device to bootstrap from.
        //     var usbDevice = adbClient.GetDevices()
        //         .FirstOrDefault(d => d.State == DeviceState.Online && !d.Serial.Contains(":"));
        //
        //     if (usbDevice == null)
        //     {
        //         Console.WriteLine("No USB device available to bootstrap wireless connection. " +
        //                            "Plug the device in via USB at least once to set up wireless mode.");
        //         return null;
        //     }
        //
        //     // 3. Get the IP while the USB link is still stable — do this BEFORE
        //     //    touching tcpip mode, since that briefly kills the USB adb link.
        //     string ip = GetDeviceIp(usbDevice);
        //
        //     if (string.IsNullOrEmpty(ip))
        //     {
        //         Console.WriteLine("Could not determine device IP over USB. " +
        //                            "Make sure Wi-Fi is on and connected on the device.");
        //         return null;
        //     }
        //
        //     if (UISettings.Instance.DeviceIP != ip)
        //     {
        //         UISettings.Instance.DeviceIP = ip;
        //         UISettings.Instance.Save();
        //         Console.WriteLine($"Device IP updated to: {ip}");
        //     }
        //
        //     // 4. Now switch the device into tcpip mode (this is what causes the
        //     //    "device disconnected" / "device connected (Offline)" blip you saw).
        //     Console.WriteLine("Switching device to TCP/IP mode...");
        //     try
        //     {
        //         ShellHelper_2.ExecuteCommand($"adb\\adb.exe -s \"{usbDevice.Serial}\" tcpip 5555");
        //     }
        //     catch (Exception e)
        //     {
        //         Console.WriteLine($"Failed to switch to tcpip mode: {e.Message}");
        //         return null;
        //     }
        //
        //     // Give the adbd daemon time to restart in tcp mode. Poll instead of a
        //     // single fixed sleep so we don't race it.
        //     DeviceData wireless = null;
        //     for (int i = 0; i < 10 && wireless == null; i++)
        //     {
        //         Thread.Sleep(500);
        //
        //         try
        //         {
        //             adbClient.Connect($"{ip}:5555");
        //         }
        //         catch (Exception e)
        //         {
        //             Console.WriteLine($"Connect attempt failed: {e.Message}");
        //             continue;
        //         }
        //
        //         wireless = adbClient.GetDevices()
        //             .FirstOrDefault(d => d.State == DeviceState.Online && d.Serial == $"{ip}:5555");
        //     }
        //
        //     if (wireless == null)
        //     {
        //         Console.WriteLine("Failed to establish wireless connection after tcpip switch.");
        //         return null;
        //     }
        //
        //     Console.WriteLine($"Connected wirelessly to {wireless.Serial} ({wireless.Model})");
        //     device = wireless;
        //     return device;
        // }
        //
        // /// <summary>
        // /// Fetches a device's IP address over adb. Now takes the target device
        // /// explicitly instead of guessing "the first online device", which was
        // /// unreliable once multiple entries (USB + tcpip) could appear.
        // /// </summary>
        // private string GetDeviceIp(DeviceData targetDevice)
        // {
        //     try
        //     {
        //         if (targetDevice == null || targetDevice.State != DeviceState.Online)
        //         {
        //             Console.WriteLine("No online device for IP lookup");
        //             return null;
        //         }
        //
        //         var receiver = new ConsoleOutputReceiver();
        //         adbClient.ExecuteRemoteCommand("ip route", targetDevice, receiver);
        //
        //         string output = receiver.ToString();
        //         Console.WriteLine(output);
        //
        //         int index = output.IndexOf("src ");
        //         if (index >= 0)
        //         {
        //             string ip = output.Substring(index + 4).Split(' ')[0].Trim();
        //             return string.IsNullOrWhiteSpace(ip) ? null : ip;
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine($"GetDeviceIp failed: {ex.Message}");
        //     }
        //
        //     return null;
        // }
        //
        
        // ---------------------------------------------------------------------
// Replacement for GetDeviceBasedOnMode() + GetDeviceIp()
// Drop these into Adb_worker_test and delete the old versions.
// ---------------------------------------------------------------------

private DeviceData GetDeviceBasedOnMode()
{
    lock (adbLock)
    {
        // Respect the actual setting instead of hardcoding "Wireless".
        // string mode = UISettings.Instance.SelectedConnectionType;
        string mode = "USB";

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
     
        
        private void run()
        {


            
            
            
            
           
                // var devices = adbClient.GetDevices().FirstOrDefault();
                var devices = GetDeviceBasedOnMode();

                if (devices == null )
                {
                    Console.WriteLine("No devices connected.");
                    Thread.Sleep(2000);
                    // continue;
                    
                    return;
                    
                }


                Console.WriteLine("Found device");

                Console.WriteLine(devices.Serial);
                
                Console.WriteLine($"current audio is : {UISettings.Instance.AudioEnabled}");
                
                // Deploy_server();
            
                
                
            // while (_isCounting)
            // {
            //     
            //     // var devices = adbClient.GetDevices();
            //     
            //     
            //     break;
            //     
            // }
            
            
            // _isCounting = false;
                
            
            
            
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

                var ip = GetDeviceIp();

                if (UISettings.Instance.DeviceIP != ip)
                {
                    UISettings.Instance.DeviceIP = ip;
                    UISettings.Instance.Save();
                    Console.WriteLine($"Device IP updated to: {ip}");
                }
                else
                {
                    Console.WriteLine($"Device IP unchanged: {ip}");
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
                    // _ = adbClient.ExecuteRemoteCommandAsync(command, device, receiver, cts.Token, 10);
                    
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


        
        
        // private string GetDeviceIp()
        // {
        //     if (device == null )
        //     {
        //         
        //         // Console.WriteLine("device null");
        //         return string.Empty;
        //     }
        //
        //     if (device.State != DeviceState.Online)
        //     {
        //         Console.WriteLine($"Device {device.Serial} is not online. Current state: {device.State}");
        //         return string.Empty;
        //     }
        //     
        //     
        //     var receiver = new ConsoleOutputReceiver();
        //
        //         adbClient.ExecuteRemoteCommand("ip route", device, receiver);
        //     // try
        //     // {
        //     //
        //     // }
        //     // catch (Exception e)
        //     // {
        //     //
        //     //     Console.WriteLine(e);
        //     //     return string.Empty;
        //     // }
        //
        //
        //     string output = receiver.ToString().Trim();
        //
        //     int index = output.IndexOf("src ");
        //     if (index >= 0)
        //     {
        //         return output.Substring(index + 4).Split(' ')[0];
        //     }
        //
        //     return string.Empty;
        // }

        
        
        
        private string GetDeviceIp()
        {
            try
            {
                var currentDevice = adbClient.GetDevices()
                    .FirstOrDefault(d => d.State == DeviceState.Online);
        
                if (currentDevice == null)
                {
                    Console.WriteLine("No online device for IP lookup");
                    return null;
                }
        
        
                var receiver = new ConsoleOutputReceiver();
        
                adbClient.ExecuteRemoteCommand(
                    "ip route",
                    currentDevice,
                    receiver
                );
        
        
                string output = receiver.ToString();
        
                Console.WriteLine(output);
        
        
                int index = output.IndexOf("src ");
        
                if(index >= 0)
                {
                    string ip = output.Substring(index + 4)
                        .Split(' ')[0];
        
                    return ip;
                }
        
            }
            catch(Exception ex)
            {
                Console.WriteLine($"GetDeviceIp failed: {ex.Message}");
            }
        
        
            return null;
        }
        
        
        
        
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
        }
    }
}