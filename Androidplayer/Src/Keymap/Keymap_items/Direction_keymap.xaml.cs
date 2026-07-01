using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Androidplayer.Store;

namespace Androidplayer.Src.Keymap.Keymap_items;

public partial class Direction_keymap : UserControl , IKeymapElement
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

    
    public Direction_keymap()
    {
        InitializeComponent();
        
        Loaded += Directon_keymap_Loaded;


        TopLeftThumb.DragDelta += ResizeThumb_DragDelta;
        TopRightThumb.DragDelta += ResizeThumb_DragDelta;
        BottomLeftThumb.DragDelta += ResizeThumb_DragDelta;
        BottomRightThumb.DragDelta += ResizeThumb_DragDelta;


       
        
        
        //
        // TopLeftThumb.MouseEnter += ResizeThumb_mouseenter;
        // TopRightThumb.MouseEnter += ResizeThumb_mouseenter;
        // BottomLeftThumb.MouseEnter += ResizeThumb_mouseenter;
        // BottomRightThumb.MouseEnter += ResizeThumb_mouseenter;
        //
        //
        //
        // TopLeftThumb.MouseLeave += ResizeThumb_mouseleave;
        // TopRightThumb.MouseLeave += ResizeThumb_mouseleave;
        // BottomLeftThumb.MouseLeave += ResizeThumb_mouseleave;
        // BottomRightThumb.MouseLeave += ResizeThumb_mouseleave;
        
        
        
        
        
        

        // this.RootGrid.MouseLeftButtonDown += RootGrid_MouseLeftButtonDown;
        // this.RootGrid.MouseMove += RootGrid_MouseMove;
        // this.RootGrid.MouseLeftButtonUp += RootGrid_MouseLeftButtonUp;

        // this.RenderTransform = _translateTransform;
        //
        // // 231 : 180
        // // Set initial position to (400, 300)
        // _translateTransform.X = 231;
        // _translateTransform.Y = 180;

    }

    private void Directon_keymap_Loaded(object sender, RoutedEventArgs e)
    {
        if (this.Parent is Canvas c)
        {
            OriginalParentWidth = c.ActualWidth;
            OriginalParentHeight = c.ActualHeight;

            // store initial center position as OriginalX/Y similar to your Qt code (center)
            // OriginalX = Canvas.GetLeft(this) + (this.ActualWidth / 2.0);
            // OriginalY = Canvas.GetTop(this) + (this.ActualHeight / 2.0);
            //     
            c.SizeChanged += Parent_SizeChanged;

            double left = Canvas.GetLeft(this);
            double top = Canvas.GetTop(this);

            if (double.IsNaN(left))
                left = 0;

            if (double.IsNaN(top))
                top = 0;

            OriginalX = left + (this.ActualWidth / 2.0);
            OriginalY = top + (this.ActualHeight / 2.0);
            
            //
            // Console.WriteLine($"Left: {Canvas.GetLeft(this)}");
            // Console.WriteLine($"Top: {Canvas.GetTop(this)}");
           
            // Console.WriteLine(GetJsonData());
            
            string jsonString = JsonSerializer.Serialize(GetJsonData(), new JsonSerializerOptions { WriteIndented = true });
            // Console.WriteLine(jsonString);
            
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
    
    
    
    private void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Don’t start dragging if the click is on a Thumb or Button
        if (e.OriginalSource is Thumb || e.OriginalSource is Button)
            return;

        Console.WriteLine("mouse down ######3");

        _isDragging = true;
        _clickPosition = e.GetPosition(this);

        RootGrid.CaptureMouse();
        // CaptureMouse();
        e.Handled = true;
    }

    private void RootGrid_MouseMove(object sender, MouseEventArgs e)
    {
    

        
        if (!_isDragging || this.Parent is not Canvas parentCanvas)
            return;
    
        var currentPosition = e.GetPosition(parentCanvas);



     
        
        double newLeft = currentPosition.X - _clickPosition.X;
        double newTop = currentPosition.Y - _clickPosition.Y;
    
        // Constrain within bounds
        if (newLeft < 0) newLeft = 0;
        if (newTop < 0) newTop = 0;
        if (newLeft + ActualWidth > parentCanvas.ActualWidth)
            newLeft = parentCanvas.ActualWidth - ActualWidth;
        if (newTop + ActualHeight > parentCanvas.ActualHeight)
            newTop = parentCanvas.ActualHeight - ActualHeight;
    
        Canvas.SetLeft(this, newLeft);
        Canvas.SetTop(this, newTop);
        
        
        OriginalParentWidth = parentCanvas.ActualWidth;
        OriginalParentHeight = parentCanvas.ActualHeight;
        
        OriginalX = newLeft + (this.ActualWidth / 2.0);
        OriginalY = newTop + (this.ActualHeight / 2.0);
    
        e.Handled = true;
    }

    
    
  
    
    
    private void RootGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            // ReleaseMouseCapture();
            RootGrid.ReleaseMouseCapture();
            e.Handled = true;
        }
    }
    
    
    // private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    // {
    //     if (this.Parent is FrameworkElement parent)
    //     {
    //         double newSize = this.Width; // Start from current width (square)
    //         double leftChange = 0;
    //         double topChange = 0;
    //
    //         // Determine the delta based on which thumb is being dragged
    //         if (sender == TopLeftThumb)
    //         {
    //             // Use the larger of horizontal or vertical change to keep square
    //             double delta = Math.Max(e.HorizontalChange, e.VerticalChange);
    //             newSize -= delta;
    //             leftChange = delta;
    //             topChange = delta;
    //         }
    //         else if (sender == TopRightThumb)
    //         {
    //             double delta = Math.Max(e.HorizontalChange, -e.VerticalChange);
    //             newSize += delta;
    //             topChange = -delta;
    //         }
    //         else if (sender == BottomLeftThumb)
    //         {
    //             double delta = Math.Max(-e.HorizontalChange, e.VerticalChange);
    //             newSize += delta;
    //             leftChange = -delta;
    //         }
    //         else if (sender == BottomRightThumb)
    //         {
    //             double delta = Math.Max(e.HorizontalChange, e.VerticalChange);
    //             newSize += delta;
    //         }
    //
    //         // Minimum size
    //         if (newSize < 70)
    //
    //         {
    //             
    //             newSize = 70;
    //             
    //             return;
    //         }
    //
    //
    //
    //
    //         if (newSize > 190)
    //         {
    //             
    //             
    //             newSize = 190;
    //         
    //             return;
    //
    //         }    
    //
    //         
    //
    //         // Maximum size = parent size
    //         if (newSize > parent.ActualWidth) newSize = parent.ActualWidth;
    //         if (newSize > parent.ActualHeight) newSize = parent.ActualHeight;
    //
    //         // Apply new size
    //         this.Width = newSize;
    //         this.Height = newSize;
    //
    //         // Move the control if resizing from left/top corners
    //         if (this.Parent is Canvas)
    //         {
    //             Canvas.SetLeft(this, Canvas.GetLeft(this) + leftChange);
    //             Canvas.SetTop(this, Canvas.GetTop(this) + topChange);
    //         }
    //     }
    // }
    
    
    
    
    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
{
    if (this.Parent is not Canvas parentCanvas)
        return;

    double left = Canvas.GetLeft(this);
    double top = Canvas.GetTop(this);
    double width = this.Width;
    double height = this.Height;

    double deltaX = e.HorizontalChange;
    double deltaY = e.VerticalChange;
    double delta = 0;
    double newLeft = left;
    double newTop = top;

    // === Determine delta based on thumb ===
    if (sender == TopLeftThumb)
    {
        delta = Math.Min(deltaX, deltaY); // smaller drag direction dominates
        width -= delta;
        height = width;
        newLeft += delta;
        newTop += delta;
    }
    else if (sender == TopRightThumb)
    {
        delta = Math.Min(-deltaX, deltaY);
        width -= delta;
        height = width;
        newTop += delta;
    }
    else if (sender == BottomLeftThumb)
    {
        delta = Math.Min(deltaX, -deltaY);
        width -= delta;
        height = width;
        newLeft += delta;
    }
    else if (sender == BottomRightThumb)
    {
        delta = Math.Max(deltaX, deltaY);
        width += delta;
        height = width;
    }

    // === Clamp to min/max size ===
    if (width < 70)
    {
        width = 70;
        height = 70;
        return; // stop moving
    }

    if (width > 190)
    {
        width = 190;
        height = 190;
        return;
    }

    // === Clamp to canvas bounds ===
    if (newLeft < 0)
        newLeft = 0;
    if (newTop < 0)
        newTop = 0;

    // If it would overflow the right/bottom edges, shrink the size
    if (newLeft + width > parentCanvas.ActualWidth)
        width = parentCanvas.ActualWidth - newLeft;
    if (newTop + height > parentCanvas.ActualHeight)
        height = parentCanvas.ActualHeight - newTop;

    // Keep it square again after clamping
    double finalSize = Math.Min(width, height);

    // === Apply new size and position ===
    this.Width = finalSize;
    this.Height = finalSize;
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

        // double x = Canvas.GetLeft(this);
        // double y = Canvas.GetTop(this);
        //
        double x = Canvas.GetLeft(this);
        double y = Canvas.GetTop(this);

        if (double.IsNaN(x))
            x = 0;

        if (double.IsNaN(y))
            y = 0;

        List<string> keys = new();
        if (!string.IsNullOrEmpty(KeyName))
            keys = KeyName.Split('+', StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .ToList();

        
            
        int deviceW = (int)My_Store.Instance?.DeviceWidth;
        int deviceH = (int)My_Store.Instance?.DeviceHeight ;

        double scaledX = x / parentW * deviceW;
        double scaledY = y / parentH * deviceH;

        // Get 

        double scaled_width = this.ActualWidth / parentW * deviceW;
        double scaled_height = this.ActualHeight / parentH * deviceH;  


        
        
        return new Dictionary<string, object>
        {
            ["type"] = Name,
            ["keys"] = new List<string> { "W", "A", "S", "D" },
            ["x"] = scaledX,
            ["y"] = scaledY,
            ["width"] = this.ActualWidth,
            ["height"] = this.ActualHeight,
            ["parent_width"] = deviceW,
            ["parent_height"] = deviceH,
            ["scaled_width"] = scaled_width,   // ✅ scaled width ratio
            ["scaled_height"] = scaled_height, // ✅ scaled height ratio
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