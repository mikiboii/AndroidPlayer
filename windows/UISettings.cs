// using System;
// using System.ComponentModel;
// using System.Configuration;
//
// namespace Androidplayer.windows
// {
//     internal class UISettings : ConfigurationSection, INotifyPropertyChanged
//     {
//         // Singleton instance
//         private static UISettings _instance;
//         public static UISettings Instance
//         {
//             get
//             {
//                 if (_instance == null)
//                 {
//                     var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
//                     _instance = config.GetSection("UISettings") as UISettings;
//
//                     if (_instance == null)
//                     {
//                         _instance = new UISettings();
//                         // {
//                         //     Resolution = "1080p",
//                         //     FPS = 60,
//                         //     Bitrate = 8,
//                         //     AudioEnabled = true,
//                         //     Language = "English",
//                         //     Theme = "Light",
//                         //     Currency = "$",
//                         //     FontSize = 8
//                         // };
//
//                         config.Sections.Add("UISettings", _instance);
//                         config.Save(ConfigurationSaveMode.Full);
//                     }
//                 }
//                 return _instance;
//             }
//         }
//
//         public void Save()
//         {
//             var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
//             var section = (UISettings)config.GetSection("UISettings");
//
//             if (section == null)
//             {
//                 config.Sections.Add("UISettings", this);
//             }
//             else
//             {
//                 section.Resolution = this.Resolution;
//                 section.FPS = this.FPS;
//                 section.Bitrate = this.Bitrate;
//                 section.AudioEnabled = this.AudioEnabled;
//                 section.SelectedConnectionType = this.SelectedConnectionType;
//
//                 section.DeviceIP = this.DeviceIP;
//             }
//
//             config.Save(ConfigurationSaveMode.Full);
//             ConfigurationManager.RefreshSection("UISettings");
//         }
//
//         // INotifyPropertyChanged implementation
//         public event PropertyChangedEventHandler PropertyChanged;
//         private void OnPropertyChanged(string propertyName)
//         {
//             PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
//         }
//
//         // All your properties...
//         [ConfigurationProperty("language", DefaultValue = "English", IsRequired = false)]
//         public string Language { get => (string)this["language"]; set { this["language"] = value; OnPropertyChanged(nameof(Language)); } }
//
//         [ConfigurationProperty("theme", DefaultValue = "Light", IsRequired = false)]
//         public string Theme { get => (string)this["theme"]; set { this["theme"] = value; OnPropertyChanged(nameof(Theme)); } }
//
//         [ConfigurationProperty("currency", DefaultValue = "$", IsRequired = false)]
//         public string Currency { get => (string)this["currency"]; set { this["currency"] = value; OnPropertyChanged(nameof(Currency)); } }
//
//         [ConfigurationProperty("fontsize", DefaultValue = 8, IsRequired = false)]
//         [IntegerValidator(MaxValue = 100, MinValue = 5)]
//         public int FontSize { get => (int)this["fontsize"]; set { this["fontsize"] = value; OnPropertyChanged(nameof(FontSize)); } }
//
//         [ConfigurationProperty("Resolution", DefaultValue = "1080p", IsRequired = false)]
//         public string Resolution { get => (string)this["Resolution"]; set { this["Resolution"] = value; OnPropertyChanged(nameof(Resolution)); } }
//
//         [ConfigurationProperty("FPS", DefaultValue = 60, IsRequired = false)]
//         public int FPS { get => (int)this["FPS"]; set { this["FPS"] = value; OnPropertyChanged(nameof(FPS)); } }
//
//         [ConfigurationProperty("Bitrate", DefaultValue = 8, IsRequired = false)]
//         public int Bitrate { get => (int)this["Bitrate"]; set { this["Bitrate"] = value; OnPropertyChanged(nameof(Bitrate)); } }
//
//         [ConfigurationProperty("AudioEnabled", DefaultValue = true, IsRequired = false)]
//         public bool AudioEnabled
//         {
//             get => (bool)this["AudioEnabled"];
//             set
//             {
//                 this["AudioEnabled"] = value;
//                 OnPropertyChanged(nameof(AudioEnabled));
//             }
//         }
//
//         [ConfigurationProperty("SelectedConnectionType", DefaultValue = "USB", IsRequired = false)]
//         public string SelectedConnectionType
//         {
//             get => (string)this["SelectedConnectionType"];
//             set
//             {
//                 this["SelectedConnectionType"] = value;
//                 OnPropertyChanged(nameof(SelectedConnectionType));
//             }
//         }
//
//         [ConfigurationProperty("DeviceIP", DefaultValue = "", IsRequired = false)]
//         public string DeviceIP
//         {
//             get => (string)this["DeviceIP"];
//             set
//             {
//                 this["DeviceIP"] = value;
//                 OnPropertyChanged(nameof(DeviceIP));
//             }
//         }
//     }
// }



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

        private bool _audioEnabled = true;
        public bool AudioEnabled
        {
            get => _audioEnabled;
            set
            {
                if (_audioEnabled == value) return;
                _audioEnabled = value;
                OnPropertyChanged(nameof(AudioEnabled));
            }
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

        // ---- Source-generated JSON context (AOT-safe, no reflection) ----
        [JsonSerializable(typeof(UISettings))]
        internal partial class SettingsContext : JsonSerializerContext
        {
        }
    }
}