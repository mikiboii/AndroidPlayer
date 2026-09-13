


using System;
using System.Net.Sockets;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Androidplayer.Store
{
    public partial class My_Store : ObservableObject
    {
        // Singleton instance
        private static readonly Lazy<My_Store> _instance = new Lazy<My_Store>(() => new My_Store());
        public static My_Store Instance => _instance.Value;

        private TcpClient _controlSocket;
        private int _deviceWidth;
        private int _deviceHeight;
        private int __videoWidth;
        private int __videoHeight;
        
        private int _displayWidth;
        private int _displayHeight;

        public TcpClient ControlSocket
        {
            get => _controlSocket;
            set => SetProperty(ref _controlSocket, value);
        }

        public int DeviceWidth
        {
            get => _deviceWidth;
            set
            {
                if (SetProperty(ref _deviceWidth, value))
                {
                    OnPropertyChanged(nameof(DeviceResolution));
                }
            }
        }

        public int DeviceHeight
        {
            get => _deviceHeight;
            set
            {
                if (SetProperty(ref _deviceHeight, value))
                {
                    OnPropertyChanged(nameof(DeviceResolution));
                }
            }
        }

        public int VideoWidth
        {
            get => __videoWidth;
            set
            {
                if (SetProperty(ref __videoWidth, value))
                {
                    OnPropertyChanged(nameof(VideoResolution));
                }
            }
        }

        public int VideoHeight
        {
            get => __videoHeight;
            set
            {
                if (SetProperty(ref __videoHeight, value))
                {
                    OnPropertyChanged(nameof(VideoResolution));
                }
            }
        }
        
        public int DisplayWidth
        {
            get => _displayWidth;
            set
            {
                if (SetProperty(ref _displayWidth, value))
                {
                    OnPropertyChanged(nameof(DisplayResolution));
                }
            }
        }

        public int DisplayHeight
        {
            get => _displayHeight;
            set
            {
                if (SetProperty(ref _displayHeight, value))
                {
                    OnPropertyChanged(nameof(DisplayResolution));
                }
            }
        }
        

        // Tuple properties
        public (int Width, int Height) DeviceResolution => (_deviceWidth, _deviceHeight);
        public (int Width, int Height) VideoResolution => (__videoWidth, __videoHeight);
        
        
        public (int Width, int Height) DisplayResolution => (_displayWidth, _displayHeight);

        // Private constructor for singleton
        private My_Store()
        {
            _deviceWidth = 0;
            _deviceHeight = 0;
            __videoWidth = 0;
            __videoHeight = 0;
            
            _displayWidth = 0;
            _displayHeight = 0;
            
            _controlSocket = null;
        }

        // Helper methods to update properties
        public void SetDeviceResolution(int width, int height)
        {
            DeviceWidth = width;
            DeviceHeight = height;
        }

        // public void SetDeviceResolution((int Width, int Height) resolution)
        // {
        //     DeviceWidth = resolution.Width;
        //     DeviceHeight = resolution.Height;
        // }

        public void SetVideoResolution(int width, int height)
        {
            VideoWidth = width;
            VideoHeight = height;
            
            
           
        }
        
        
        public void SetDisplayResolution(int width, int height)
        {
            DisplayWidth = width;
            DisplayHeight = height;
            
            
           
        }

        // public void SetVideoResolution((int Width, int Height) resolution)
        // {
        //     VideoWidth = resolution.Width;
        //     VideoHeight = resolution.Height;
        // }

        public void SetControlSocket(TcpClient socket)
        {
            ControlSocket = socket;
        }

        public void ClearControlSocket()
        {
            ControlSocket?.Close();
            ControlSocket = null;
        }

        // Dispose pattern for cleanup
        public void Dispose()
        {
            ControlSocket?.Dispose();
            ControlSocket = null;
        }
    }
}