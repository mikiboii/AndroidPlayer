using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Androidplayer.Src.Keymap.K_store
{
    public partial class k_info : ObservableObject
    {
        // Singleton instance
        private static readonly Lazy<k_info> _instance = new Lazy<k_info>(() => new k_info());
        public static k_info Instance => _instance.Value;

        // Global string
       
        private string _defaultKeymap = string.Empty;
        
        
        private bool _keymapMode  = false;
        
        private bool _isCollapsed = false;
        
        
        public string DefaultKeymap 
        {
            get => _defaultKeymap;
            set => SetProperty(ref _defaultKeymap, value);
        }
        
        
        public bool Collapsed 
        {
            get => _isCollapsed;
            set => SetProperty(ref _isCollapsed, value);
        }
        public bool KeymapMode 
        {
            get => _keymapMode;
            set => SetProperty(ref _keymapMode, value);
        }
        public void Toggle_Keymap_mode() => KeymapMode = !KeymapMode;
        
        private keymap_menu? _keymapMenu;
        public keymap_menu? KeymapMenu
        {
            get => _keymapMenu;
            set => SetProperty(ref _keymapMenu, value);
        }
        
        
        
        
        private OverlayManager? _overlayManager;
        public OverlayManager? overlayManager
        {
            get => _overlayManager;
            set => SetProperty(ref _overlayManager, value);
        }
        
        private DirectX? _directx;
        public DirectX? directx
        {
            get => _directx;
            set => SetProperty(ref _directx, value);
        }
        

        private Canvas? _imageContainer;
        public Canvas? ImageContainer
        {
            get => _imageContainer;
            set => SetProperty(ref _imageContainer, value);
        }

        
        public HashSet<string> DisallowedKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "F9", "F10", "F11", "F12","Backspace","RWin","LWin"
        };
        
        
        // Private constructor to enforce singleton
        private k_info()
        {
        }
    }
}