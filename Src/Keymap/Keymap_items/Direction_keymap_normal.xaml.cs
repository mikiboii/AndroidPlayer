using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Androidplayer.Store;

namespace Androidplayer.Src.Keymap.Keymap_items;

public partial class Direction_keymap_normal : UserControl , IKeymapElement
{
    
    
    public double OriginalParentWidth { get; private set; }
    public double OriginalParentHeight { get; private set; }
    public double OriginalX { get; private set; }
    public double OriginalY { get; private set; }






    public string Name { get; set; } = "Direction";
    public string KeyName { get; set; } = "wasd";
    
    
    
    
    
    private bool _isDragging = false;
    private Point _clickPosition;
    
  

 
    private TranslateTransform _translateTransform = new TranslateTransform();

    
    public Direction_keymap_normal()
    {
        InitializeComponent();
        
        Loaded += Directon_keymap_Loaded;



    }

    private void Directon_keymap_Loaded(object sender, RoutedEventArgs e)
    {
        if (this.Parent is Canvas c)
        {
            OriginalParentWidth = c.ActualWidth;
            OriginalParentHeight = c.ActualHeight;

            // store initial center position as OriginalX/Y similar to your Qt code (center)
            OriginalX = Canvas.GetLeft(this) + (this.ActualWidth / 2.0);
            OriginalY = Canvas.GetTop(this) + (this.ActualHeight / 2.0);
                
            c.SizeChanged += Parent_SizeChanged;


            // Console.WriteLine(GetJsonData());
         
            
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

        return new Dictionary<string, object>
        {
            ["type"] = Name,
            ["keys"] = new List<string> { "W", "A", "S", "D" },
            ["x"] = x,
            ["y"] = y,
            ["width"] = this.ActualWidth,
            ["height"] = this.ActualHeight,
            ["parent_width"] = parentW,
            ["parent_height"] = parentH,
            ["Img path"] = null,
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
                
            // OriginalX = data.X;
            // OriginalY = data.Y;
            
            OriginalX = data.X  + (this.ActualWidth / 2.0);
            OriginalY = data.Y  + (this.ActualHeight / 2.0);

                
            

                
            if (this.Parent is not Canvas parentCanvas)
                return;
                
            FixPosOnParentResize(parentCanvas.ActualWidth, parentCanvas.ActualHeight);

            // ✅ Load image if available
            // if (!string.IsNullOrEmpty(data.ImagePath))
            //     ImageSource = data.ImagePath;

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