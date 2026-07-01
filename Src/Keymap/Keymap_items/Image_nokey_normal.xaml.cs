using System.Windows.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Androidplayer.Store;


namespace Androidplayer.Src.Keymap.Keymap_items;

public partial class Image_nokey_normal : UserControl , IKeymapElement
{
    
    public double OriginalParentWidth { get; private set; }
    public double OriginalParentHeight { get; private set; }
    public double OriginalX { get; private set; }
    public double OriginalY { get; private set; }
    
    public string ImageSource
    {
        get => ImageDisplay.Source?.ToString();
        set
        {
            try
            {
                ImageDisplay.Source = new BitmapImage(new Uri(value, UriKind.RelativeOrAbsolute));
            }
            catch
            {
                Console.WriteLine("⚠️ Invalid image source: " + value);
            }
        }
    }
    
    
    
    public string Name { get; set; }
    public string KeyName { get; set; } = "";
    
    
    
    private bool _isDragging = false;
    private Point _clickPosition;
    
    public Image_nokey_normal()
    {
        InitializeComponent();
        Loaded += ui_Loaded;
        
       
    }
    
    
     private void ui_Loaded(object sender, RoutedEventArgs e)
    {
        if (this.Parent is Canvas c)
        {
            OriginalParentWidth = c.ActualWidth;
            OriginalParentHeight = c.ActualHeight;

            // store initial center position as OriginalX/Y similar to your Qt code (center)
            OriginalX = Canvas.GetLeft(this) + (this.ActualWidth / 2.0);
            OriginalY = Canvas.GetTop(this) + (this.ActualHeight / 2.0);
                
            c.SizeChanged += Parent_SizeChanged;
                
            
        }
    }
    
    

    
    
    private void Parent_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double newWidth = e.NewSize.Width;
        double newHeight = e.NewSize.Height;

        // update element position
        FixPosOnParentResize(newWidth, newHeight);
    }
    
    
  

    
    public void FixPosOnParentResize(double newParentWidth, double newParentHeight)
    {
        // Validate we have valid stored values
        if (OriginalParentWidth <= 0 || OriginalParentHeight <= 0)
            return;

        // Compute scaling factors
        double scaleX = newParentWidth / OriginalParentWidth;
        double scaleY = newParentHeight / OriginalParentHeight;

        // Scale original center position
        double newCenterX = OriginalX * scaleX;
        double newCenterY = OriginalY * scaleY;

        // Convert to top-left position
        double newLeft = newCenterX - (ActualWidth / 2.0);
        double newTop = newCenterY - (ActualHeight / 2.0);

        // Keep element inside canvas boundaries
        if (Parent is Canvas c)
        {
            newLeft = Math.Max(0, Math.Min(newLeft, c.ActualWidth - ActualWidth));
            newTop = Math.Max(0, Math.Min(newTop, c.ActualHeight - ActualHeight));
        }

        Canvas.SetLeft(this, newLeft);
        Canvas.SetTop(this, newTop);
            
            
            
            
    }
    

  
    
    
    
    
    
    public object GetJsonData()
    {
        double parentW = OriginalParentWidth;
        double parentH = OriginalParentHeight;

        if (this.Parent is Canvas c)
        {
            parentW = c.ActualWidth;
            parentH = c.ActualHeight;
        }

        double x = Canvas.GetLeft(this);
        double y = Canvas.GetTop(this);

        List<string> keys = new();
        if (!string.IsNullOrEmpty(KeyName))
            keys = KeyName.Split('+', StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .ToList();
        
        string? imgPath = null;
        if (ImageDisplay?.Source is BitmapImage bmp && bmp.UriSource != null)
        {
            imgPath = bmp.UriSource.ToString();
        }
        else if (!string.IsNullOrEmpty(ImageSource))
        {
            imgPath = ImageSource;
        }


        return new Dictionary<string, object>
        {
            ["type"] = Name,
            ["keys"] = keys,
            ["x"] = x,
            ["y"] = y,
            ["width"] = this.ActualWidth,
            ["height"] = this.ActualHeight,
            ["parent_width"] = parentW,
            ["parent_height"] = parentH,
            ["Img path"] = imgPath,
            ["App name"] = my_info.Instance?.Appname
        };
    }

    public void SetJsonData(KeymapElement data)
    {
        try
        {
            Name = data.Type;
            KeyName = string.Join("+", data.Keys);
            Width = data.Width;
            Height = data.Height;

            // Set position
            // Store parent dimensions for resizing logic
            OriginalParentWidth = data.ParentWidth;
            OriginalParentHeight = data.ParentHeight;
                
            OriginalX = data.X  + (this.ActualWidth / 2.0);
            OriginalY = data.Y  + (this.ActualHeight / 2.0);

                
            // if (Parent is Canvas canvas)
            // {
            //     Canvas.SetLeft(this, data.X);
            //     Canvas.SetTop(this, data.Y);
            //     
            // }
            
            // Console.WriteLine($"path of img: {data.ImagePath}");

                
            if (this.Parent is not Canvas parentCanvas)
                return;
                
            FixPosOnParentResize(parentCanvas.ActualWidth, parentCanvas.ActualHeight);

            // ✅ Load image if available
            if (!string.IsNullOrEmpty(data.ImagePath))
                ImageSource = data.ImagePath;

            // Console.WriteLine($"[Image_nokey] Loaded element: {Name} ({KeyName}) at ({data.X}, {data.Y})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ SetJsonData failed for {Name}: {ex.Message}");
        }
    }

    
    private void remove_btn_OnClick(object sender, RoutedEventArgs e)
    {
        Console.WriteLine("remove element");
        
        
        OverlayManager.Instance?.RemoveElement(this);
    }
    
    
 
    
}