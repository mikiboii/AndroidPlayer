// using System;
// using System.ComponentModel;
// using System.Configuration;
//
// namespace Androidplayer.windows
// {
//     internal class UISettings : ConfigurationSection, INotifyPropertyChanged
//     {
//         // INotifyPropertyChanged implementation
//         public event PropertyChangedEventHandler PropertyChanged;
//         private void OnPropertyChanged(string propertyName)
//         {
//             PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
//         }
//
//         [ConfigurationProperty("language", DefaultValue = "English", IsRequired = false)]
//         public string Language
//         {
//             get => (string)this["language"];
//             set
//             {
//                 this["language"] = value;
//                 OnPropertyChanged(nameof(Language));
//             }
//         }
//
//         [ConfigurationProperty("theme", DefaultValue = "Light", IsRequired = false)]
//         public string Theme
//         {
//             get => (string)this["theme"];
//             set
//             {
//                 this["theme"] = value;
//                 OnPropertyChanged(nameof(Theme));
//             }
//         }
//
//         [ConfigurationProperty("currency", DefaultValue = "$", IsRequired = false)]
//         public string Currency
//         {
//             get => (string)this["currency"];
//             set
//             {
//                 this["currency"] = value;
//                 OnPropertyChanged(nameof(Currency));
//             }
//         }
//
//         [ConfigurationProperty("fontsize", DefaultValue = 8, IsRequired = false)]
//         [IntegerValidator(MaxValue = 100, MinValue = 5)]
//         public int FontSize
//         {
//             get => (int)this["fontsize"];
//             set
//             {
//                 this["fontsize"] = value;
//                 OnPropertyChanged(nameof(FontSize));
//             }
//         }
//
//         
//         [ConfigurationProperty("Resolution", DefaultValue = "1080p", IsRequired = false)]
//         public string Resolution
//         {
//             get => (string)this["Resolution"];
//             set
//             {
//                 this["Resolution"] = value;
//                 OnPropertyChanged(nameof(Resolution));
//             }
//         }
//
//         [ConfigurationProperty("FPS", DefaultValue = 60, IsRequired = false)]
//         public int FPS
//         {
//             get => (int)this["FPS"];
//             set
//             {
//                 this["FPS"] = value;
//                 OnPropertyChanged(nameof(FPS));
//             }
//         }
//
//         [ConfigurationProperty("Bitrate", DefaultValue = 8, IsRequired = false)]
//         public int Bitrate
//         {
//             get => (int)this["Bitrate"];
//             set
//             {
//                 this["Bitrate"] = value;
//                 OnPropertyChanged(nameof(Bitrate));
//             }
//         }
//
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
//     }
// }


using System;
using System.ComponentModel;
using System.Configuration;

namespace Androidplayer.windows
{
    internal class UISettings : ConfigurationSection, INotifyPropertyChanged
    {
        // Singleton instance
        private static UISettings _instance;
        public static UISettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    _instance = config.GetSection("UISettings") as UISettings;

                    if (_instance == null)
                    {
                        _instance = new UISettings();
                        // {
                        //     Resolution = "1080p",
                        //     FPS = 60,
                        //     Bitrate = 8,
                        //     AudioEnabled = true,
                        //     Language = "English",
                        //     Theme = "Light",
                        //     Currency = "$",
                        //     FontSize = 8
                        // };

                        config.Sections.Add("UISettings", _instance);
                        config.Save(ConfigurationSaveMode.Full);
                    }
                }
                return _instance;
            }
        }

        
        
        public void Save()
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var section = (UISettings)config.GetSection("UISettings");

            if (section == null)
            {
                config.Sections.Add("UISettings", this);
            }
            else
            {
                section.Resolution = this.Resolution;
                section.FPS = this.FPS;
                section.Bitrate = this.Bitrate;
                section.AudioEnabled = this.AudioEnabled;
            }

            config.Save(ConfigurationSaveMode.Full);
            ConfigurationManager.RefreshSection("UISettings");
        }


        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // All your properties...
        [ConfigurationProperty("language", DefaultValue = "English", IsRequired = false)]
        public string Language { get => (string)this["language"]; set { this["language"] = value; OnPropertyChanged(nameof(Language)); } }

        [ConfigurationProperty("theme", DefaultValue = "Light", IsRequired = false)]
        public string Theme { get => (string)this["theme"]; set { this["theme"] = value; OnPropertyChanged(nameof(Theme)); } }

        [ConfigurationProperty("currency", DefaultValue = "$", IsRequired = false)]
        public string Currency { get => (string)this["currency"]; set { this["currency"] = value; OnPropertyChanged(nameof(Currency)); } }

        [ConfigurationProperty("fontsize", DefaultValue = 8, IsRequired = false)]
        [IntegerValidator(MaxValue = 100, MinValue = 5)]
        public int FontSize { get => (int)this["fontsize"]; set { this["fontsize"] = value; OnPropertyChanged(nameof(FontSize)); } }

        [ConfigurationProperty("Resolution", DefaultValue = "1080p", IsRequired = false)]
        public string Resolution { get => (string)this["Resolution"]; set { this["Resolution"] = value; OnPropertyChanged(nameof(Resolution)); } }

        [ConfigurationProperty("FPS", DefaultValue = 60, IsRequired = false)]
        public int FPS { get => (int)this["FPS"]; set { this["FPS"] = value; OnPropertyChanged(nameof(FPS)); } }

        [ConfigurationProperty("Bitrate", DefaultValue = 8, IsRequired = false)]
        public int Bitrate { get => (int)this["Bitrate"]; set { this["Bitrate"] = value; OnPropertyChanged(nameof(Bitrate)); } }

        [ConfigurationProperty("AudioEnabled", DefaultValue = true, IsRequired = false)]
        public bool AudioEnabled { get => (bool)this["AudioEnabled"];
            set
            {
                
                this["AudioEnabled"] = value; 
                OnPropertyChanged(nameof(AudioEnabled));
            } }
    }
}
