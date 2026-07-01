using System;
using Androidplayer.Src.Keymap.K_store;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Androidplayer.Store
{
    public partial class my_info : ObservableObject
    {
        // Singleton instance
        private static readonly Lazy<my_info> _instance = new Lazy<my_info>(() => new my_info());
        public static my_info Instance => _instance.Value;

        private bool _developerMode = true;
        
        
        
        private bool _isMouseLocked;
        
        private bool _isTakeScreenshot;
        
        private bool _isLandscapemode;
        
        private bool _window_resizing = false;
        
        private bool _auto_resizing = true;
        
        private bool _typing_mode = true;
        
        
        
        
        
        private String _appname;
        

        private LiteDbEditor data_editor;

        
        public LiteDbEditor Dataeditor
        {
            get => data_editor;
            set => SetProperty(ref data_editor, value);
        }

        public String Appname
        {
            get => _appname;
            set => SetProperty(ref _appname, value);
        }
        
        public bool DeveloperMode
        {
            get => _developerMode;
            set => SetProperty(ref _developerMode, value);
        }

        public bool IsMouseLocked
        {
            get => _isMouseLocked;
            set => SetProperty(ref _isMouseLocked, value);
        }
        
        public bool TakeScreenshot
        {
            get => _isTakeScreenshot;
            set => SetProperty(ref _isTakeScreenshot, value);
        }
        
        
        public bool IsLandscapemode
        {
            get => _isLandscapemode;
            set => SetProperty(ref _isLandscapemode, value);
        }
        
        
        
        public bool Window_resizing
        {
            get => _window_resizing;
            set => SetProperty(ref _window_resizing, value);
        }
        
        
        public bool Auto_resizing
        {
            get => _auto_resizing;
            set => SetProperty(ref _auto_resizing, value);
        }
        
        public bool Typing_mode
        {
            get => _typing_mode;
            set => SetProperty(ref _typing_mode, value);
        }

        public void Toggle_typing_mode() => Typing_mode = !Typing_mode;
        public void Toggle_mouseLock() => IsMouseLocked = !IsMouseLocked;
        // Private constructor for singleton
        private my_info()
        {
        }
    }
}