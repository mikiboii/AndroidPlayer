

using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Androidplayer.windows
{
    public partial class UISettings : INotifyPropertyChanged
    {
        // ---- Singleton ----
        private static UISettings? _instance;
        private static readonly object _lock = new();

        private static string SettingsPath =>
            Path.Combine(AppContext.BaseDirectory, "ui-settings.json");

        public static UISettings Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                lock (_lock)
                {
                    if (_instance != null)
                        return _instance;

                    try
                    {
                        if (File.Exists(SettingsPath))
                        {
                            var json = File.ReadAllText(SettingsPath);
                            _instance = JsonSerializer.Deserialize(
                                            json,
                                            SettingsContext.Default.UISettings)
                                        ?? new UISettings();
                        }
                        else
                        {
                            _instance = new UISettings();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[UISettings] Load failed: {ex.Message}");
                        _instance = new UISettings();
                    }

                    return _instance;
                }
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(
                    this,
                    SettingsContext.Default.UISettings);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UISettings] Save failed: {ex.Message}");
            }
        }

        // ---- INotifyPropertyChanged ----
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // ---- Properties (unchanged surface, JSON-backed) ----

        private string _language = "English";
        public string Language
        {
            get => _language;
            set
            {
                if (_language == value) return;
                _language = value;
                OnPropertyChanged(nameof(Language));
            }
        }

        private string _theme = "Light";
        public string Theme
        {
            get => _theme;
            set
            {
                if (_theme == value) return;
                _theme = value;
                OnPropertyChanged(nameof(Theme));
            }
        }

        private string _currency = "$";
        public string Currency
        {
            get => _currency;
            set
            {
                if (_currency == value) return;
                _currency = value;
                OnPropertyChanged(nameof(Currency));
            }
        }

        private int _fontSize = 8;
        public int FontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize == value) return;
                _fontSize = value;
                OnPropertyChanged(nameof(FontSize));
            }
        }

        private string _resolution = "1080p";
        public string Resolution
        {
            get => _resolution;
            set
            {
                if (_resolution == value) return;
                _resolution = value;
                OnPropertyChanged(nameof(Resolution));
            }
        }

        private int _fps = 60;
        public int FPS
        {
            get => _fps;
            set
            {
                if (_fps == value) return;
                _fps = value;
                OnPropertyChanged(nameof(FPS));
            }
        }

        private int _bitrate = 8;
        public int Bitrate
        {
            get => _bitrate;
            set
            {
                if (_bitrate == value) return;
                _bitrate = value;
                OnPropertyChanged(nameof(Bitrate));
            }
        }

        private bool _audioEnabled = false;
        public bool AudioEnabled
        {
            get => _audioEnabled;
            set
            {
                if (_audioEnabled == value) return;
                _audioEnabled = value;
                OnPropertyChanged(nameof(AudioEnabled));
                OnPropertyChanged(nameof(AudioDisabled)); 
            }
        }
        
        [JsonIgnore]
        public bool AudioDisabled
        {
            get => !AudioEnabled;
            set => AudioEnabled = !value;
        }
         
#if DEBUG
        
        private bool _nativeview_mode = false;
#else

        
        private bool _nativeview_mode = false;
#endif
        
        
        public bool Nativeview_mode
        {
            get => _nativeview_mode;
            set
            {
                if (_nativeview_mode == value) return;
                _nativeview_mode = value;
                OnPropertyChanged(nameof(Nativeview_mode));
                OnPropertyChanged(nameof(IsGpuMode));   // keep inverse in sync
            }
        }

        [JsonIgnore]
        public bool IsGpuMode
        {
            get => !Nativeview_mode;
            set => Nativeview_mode = !value;
        }

        
        
        

        private string _selectedConnectionType = "USB";
        public string SelectedConnectionType
        {
            get => _selectedConnectionType;
            set
            {
                if (_selectedConnectionType == value) return;
                _selectedConnectionType = value;
                OnPropertyChanged(nameof(SelectedConnectionType));
            }
        }

        private string _deviceIP = "";
        public string DeviceIP
        {
            get => _deviceIP;
            set
            {
                if (_deviceIP == value) return;
                _deviceIP = value;
                OnPropertyChanged(nameof(DeviceIP));
            }
        }
        
        
        
        private string _currentCursor = "avares://Androidplayer/Icons/cursor/black_sword.png";
        public string CurrentCursor
        {
            get => _currentCursor;
            set
            {
                if (_currentCursor == value) return;
                _currentCursor = value;
                OnPropertyChanged(nameof(CurrentCursor));
            }
        }
        public void RefreshCurrentCursor()
        {
            OnPropertyChanged(nameof(CurrentCursor));
        }
        // ---- Source-generated JSON context (AOT-safe, no reflection) ----
        [JsonSerializable(typeof(UISettings))]
        internal partial class SettingsContext : JsonSerializerContext
        {
        }
    }
}