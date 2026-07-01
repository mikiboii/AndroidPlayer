

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
namespace Androidplayer.Src
{
    
 
    
    public class app_worker : IDisposable
    {
        private Thread _adbThread;
        
        private bool _isCounting;
        private bool _isDisposed = false;

        public event Action CountingCompleted;  // Only this event - no progress updates
        public event Action<string> ErrorOccurred;
        public event Action<int, string> ProgressChanged;

        
        private readonly AdbClient adbClient;
        private DeviceData device;
        
        
        
        
        public string JAR = "scrcpy-server.jar";
        
        public string VERSION = "1.20";
        public int max_size = 1080;
        // public int bitrate = 8000000;
        public int bitrate = 20000;
        public int max_fps = 60;
        public bool block_frame = true;
        public bool stay_awake = false;
        public int lock_screen_orientation = -1;
        public bool skip_same_frame = false;
        public double min_frame_interval => 1.0 / max_fps;

        public app_worker()
        {
           
            _isCounting = false;
            Console.WriteLine("AdbWorker initialized");
            
           
            
            
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

            _isCounting = true;
            _adbThread = new Thread(Deploy_server)
            {
                IsBackground = true,
                Name = "CounterThread"
            };
            _adbThread.Start();
        }

        public void Stop()
        {
            _isCounting = false;
            
            if (_adbThread != null && _adbThread.IsAlive)
            {
                _adbThread.Join(1000);
                if (_adbThread.IsAlive)
                {
                    _adbThread.Abort();
                }
                _adbThread = null;
            }
        }

     
  
        // private void Deploy_server()
        // {
        //
        //
        //     try
        //     {
        //       
        //         
        //         
        //         
        //         CountingCompleted?.Invoke();
        //         
        //         
        //         
        //         
        //
        //         Console.WriteLine("Server deployment completed!");
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine($"Error in DeployServer: {ex}");
        //         ErrorOccurred?.Invoke(ex.Message);
        //     }
        //     finally
        //     {
        //         _isCounting = false;
        //     }
        // }
        
        
        private void Deploy_server()
        {
            try
            {
                for (int i = 0; i <= 100; i += 10)
                {
                    if (!_isCounting) break;

                    // simulate work
                    Thread.Sleep(200);

                    // notify progress
                    ProgressChanged?.Invoke(i, "uploading server");

                    //
                    // if (i == 100)
                    // {
                    //     Console.WriteLine("sleeping ####");
                    //     Thread.Sleep(2000);
                    //     
                    //     
                    //     
                    // }
                }

                CountingCompleted?.Invoke();
                Console.WriteLine("Server deployment completed!");
                
                
                
                
                
                
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DeployServer: {ex}");
                ErrorOccurred?.Invoke(ex.Message);
            }
            finally
            {
                _isCounting = false;
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