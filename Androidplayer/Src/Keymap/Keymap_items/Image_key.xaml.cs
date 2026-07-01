using System.Globalization;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Androidplayer.Store;


namespace Androidplayer.Src.Keymap.Keymap_items;

public partial class Image_key : UserControl , IKeymapElement
{
    
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
    
    public double OriginalParentWidth { get; private set; }
    public double OriginalParentHeight { get; private set; }
    public double OriginalX { get; private set; }
    public double OriginalY { get; private set; }
    
    
   
    public string Name { get; set; }
    public string KeyName { get; set; } = "";
    
    
    
    
    
    public bool IsEditing { get; private set; } = false;
        
    private List<string> my_keys = new List<string>();
    
    
    
    private bool _isDragging = false;
    private Point _clickPosition;


    
    
    public Image_key()
    {
        InitializeComponent();
        Loaded += ui_Loaded;
        
        RootGrid.MouseLeftButtonDown += DisplayKey_MouseLeftButtonDown;
        
        
        
        InputField.LostKeyboardFocus += InputField_LostKeyboardFocus;
        InputField.KeyDown += InputField_KeyDown;
        InputField.KeyUp += InputField_KeyUp;
            
            
        InputField.PreviewTextInput += (s, e) => e.Handled = true;
        InputField.PreviewKeyDown += InputField_KeyDown;
        
        
        
        
        TopLeftThumb.DragDelta += ResizeThumb_DragDelta;
        TopRightThumb.DragDelta += ResizeThumb_DragDelta;
        BottomLeftThumb.DragDelta += ResizeThumb_DragDelta;
        BottomRightThumb.DragDelta += ResizeThumb_DragDelta;
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

            if (KeyName == "")
            {
                StartEditing();
                
            }
            
            
        }
    }
    
    

    
    
    private void Parent_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double newWidth = e.NewSize.Width;
        double newHeight = e.NewSize.Height;

        // update element position
        FixPosOnParentResize(newWidth, newHeight);
    }
    
    
    
    
    private void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Don’t start dragging if the click is on a Thumb or Button
        if (e.OriginalSource is Thumb || e.OriginalSource is Button)
            return;
        
        if (e.ClickCount == 2)
        {
            // Handle double-click
            Console.WriteLine("Double-click detected!");
            DisplayKey_MouseDoubleClick(sender, e);
        }

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
    if (width < 50)
    {
        width = 50;
        height = 50;
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
    
    
    
    
    
    
    #region Editing (double-click -> show TextBox)


    private void DisplayKey_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Handle single-click logic here
        // Example:
            
        if (e.ClickCount == 2)
        {
            // Handle double-click
            Console.WriteLine("Double-click detected!");
            DisplayKey_MouseDoubleClick(sender, e);
        }
        else if (e.ClickCount == 1)
        {
            // Handle single-click
            Console.WriteLine("Single-click detected!");
        }
        Console.WriteLine("DisplayKey single click");
    }






private void DisplayKey_SizeChanged(object sender, SizeChangedEventArgs e)
{
    // Add padding space
    double newWidth = DisplayKey.ActualWidth + 20;
    if (newWidth < 60)
    {
        this.Width = 60;
    }
    else
    {
                
        this.Width = newWidth;
                
    }

    // Console.WriteLine("size changed !");
}

        
// private void InputField_TextChanged(object sender, TextChangedEventArgs e)
// {
//     // Measure how wide the text is
//     var formattedText = new FormattedText(
//         InputField.Text,
//         CultureInfo.CurrentCulture,
//         FlowDirection.LeftToRight,
//         new Typeface(InputField.FontFamily, InputField.FontStyle, InputField.FontWeight, InputField.FontStretch),
//         InputField.FontSize,
//         Brushes.Black,
//         new NumberSubstitution(),
//         1);
//
//     // Add a small padding
//     // this.Width = formattedText.Width + 20;
//     
//     
//     
// }



    private void InputField_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Get a safe reference
        var tb = InputField;
        
        

        if (tb == null || string.IsNullOrEmpty(tb.Text))
            return;

        // Base font size (your default)
        double baseFontSize = 42;

        // Maximum available width inside your circle (adjust if needed)
        double maxWidth = 80; // since circle width is ~95px with margins

        // Measure the text at base font size
        var formattedText = new FormattedText(
            tb.Text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
            baseFontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        // If text is too wide, shrink font proportionally
        double scale = maxWidth / formattedText.Width;

        // Limit scaling to avoid font being too small
        if (scale < 0.4) scale = 0.4; // don’t shrink below 40%
        if (scale > 1.0) scale = 1.0; // don’t grow beyond base size

        tb.FontSize = baseFontSize * scale;
        
        DisplayKey.FontSize = baseFontSize * scale;
    }








private void DisplayKey_MouseDoubleClick(object sender, MouseButtonEventArgs e)
{
    StartEditing();
    e.Handled = true;
}

private void StartEditing()
{
   
    DisplayKey.Text = KeyName;
    InputField.Text = KeyName;
    
    
    IsEditing = true;
    DisplayKey.Visibility = Visibility.Collapsed;
    InputField.Visibility = Visibility.Visible;
    InputField.Focus();
    // InputField.SelectAll();
}

private void StopEditing()
{
    IsEditing = false;
    InputField.Visibility = Visibility.Collapsed;
    DisplayKey.Visibility = Visibility.Visible;
    KeyName = InputField.Text?.Trim() ?? "";
    DisplayKey.Text = KeyName;
}


private bool key_already_exists()
{

    foreach (var element in OverlayManager.Instance.DroppedElements)
    {
        if (element is IKeymapElement keyEl)
        {
            // Console.WriteLine($"Key: {keyEl.KeyName}");
            // Console.WriteLine($"Key: {keyEl.Name}");

            // string[] KeymapElement_parts;
            List<string> KeymapElement_parts = new List<string>();
            List<string> my_key_parts = new List<string>();


            if (keyEl.KeyName == null || keyEl.KeyName == "")
            {
                continue;
            }
            
            
            var disallowedKeys = K_store.k_info.Instance.DisallowedKeys;

            if (my_key_parts.Any(k => disallowedKeys.Contains(k.Trim())))
            {
                Console.WriteLine("This key is not allowed.");
                return true;
            }

            

                    
                    
            KeymapElement_parts = keyEl.KeyName.Split('+').ToList();
            my_key_parts = InputField.Text.Split('+').ToList();
                    
                    
                  
            if ( my_key_parts[0] == KeymapElement_parts[0])
            {
                if (my_key_parts.Count == 2 && KeymapElement_parts.Count == 2)
                {

                    
                    if (my_key_parts[1] == KeymapElement_parts[1])
                    {
                        return true;
                                
                    }
                    
                    continue;
                    
                    
                }
                
                return true;
            }
                    
            if (keyEl.KeyName == "wasd")
            {
                if (my_key_parts.Any(k => k == "W" || k == "A" || k == "S" || k == "D"))
                {
                    Console.WriteLine("Contains a WASD key!");

                    return true;
                }
                                    
                                    
            }
            
            
            
           
                    
                   
                    
        }
    }

                
    

    return false;


}



private void InputField_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
{
    // If empty, request removal (same behavior as your Python code)
    if (string.IsNullOrWhiteSpace(InputField.Text))
    {
        // RaiseEvent(new RoutedEventArgs(RemoveRequestedEvent, this));
                
        OverlayManager.Instance?.RemoveElement(this);
        return;
    }
    
    
    
    
    if ( key_already_exists())
    {
        OverlayManager.Instance?.RemoveElement(this);
        return;
    }
    

    // Console.WriteLine($"length of the input {InputField.Text.Length} , value: {InputField.Text}");
    StopEditing();
}


private void InputField_KeyDown(object sender, KeyEventArgs e)
{

    Key actualKey = (e.Key == Key.System) ? e.SystemKey : e.Key;
    
    if (e.IsRepeat)
        return;
    
    string pressedKey = ConvertKeyToString(actualKey);


    if (is_notallowed_key(pressedKey))
    {
        
        e.Handled = true;
        return;
    }
    
    
    
    Console.WriteLine(pressedKey);
    
    
    my_keys.Add(pressedKey);

    // Console.WriteLine(my_keys.Count);

    
    if (my_keys.Count == 1)
    {

        InputField.Clear();
        InputField.Text = my_keys[0];
        
        if (Name == null || Name == "Multi key" )
        {
            Name = "Regular key";
        }

    }
    

    if (my_keys.Count == 2)
    {
        string shortcut = string.Join("+", my_keys);
        
        
        InputField.Clear();
        InputField.Text = shortcut;
        
        if (Name == null || Name == "Regular key" )
        {
            Name = "Multi key";
        }

        my_keys.Clear();
    }
    
   
    
    
    e.Handled = true;
}

private void InputField_KeyUp(object sender, KeyEventArgs e)
{
    // NOTE: do NOT remove modifiers on release like before.
    // The shortcut text should remain until the user finishes.
    
    Key actualKey = (e.Key == Key.System) ? e.SystemKey : e.Key;
    
    string releasedKey = ConvertKeyToString(actualKey);

    // Remove released key if it exists in the list
    my_keys.Remove(releasedKey);
    
    Console.WriteLine(ConvertKeyToString(e.Key));
    
    
    
    e.Handled = true;
}







#endregion
    
    
    #region Key converter 

    private bool is_notallowed_key(string key)
    {
        var disallowedKeys = K_store.k_info.Instance.DisallowedKeys;

        if (disallowedKeys.Contains(key))
        {

            return true;
            // Console.WriteLine("key not allowed");
        }
                
                
        return false;
                
      
                
                
    }
        
        
            private string ConvertKeyToString(Key key)
            {
               
                
                switch (key)
                {
                    case Key.A: return "A";
                    case Key.B: return "B";
                    case Key.C: return "C";
                    case Key.D: return "D";
                    case Key.E: return "E";
                    case Key.F: return "F";
                    case Key.G: return "G";
                    case Key.H: return "H";
                    case Key.I: return "I";
                    case Key.J: return "J";
                    case Key.K: return "K";
                    case Key.L: return "L";
                    case Key.M: return "M";
                    case Key.N: return "N";
                    case Key.O: return "O";
                    case Key.P: return "P";
                    case Key.Q: return "Q";
                    case Key.R: return "R";
                    case Key.S: return "S";
                    case Key.T: return "T";
                    case Key.U: return "U";
                    case Key.V: return "V";
                    case Key.W: return "W";
                    case Key.X: return "X";
                    case Key.Y: return "Y";
                    case Key.Z: return "Z";
                    case Key.D0: return "0";
                    case Key.D1: return "1";
                    case Key.D2: return "2";
                    case Key.D3: return "3";
                    case Key.D4: return "4";
                    case Key.D5: return "5";
                    case Key.D6: return "6";
                    case Key.D7: return "7";
                    case Key.D8: return "8";
                    case Key.D9: return "9";
                    case Key.Space: return "Space";
                    case Key.Enter: return "Enter";
                    case Key.Escape: return "Escape";
                    case Key.Back: return "Backspace";
                    case Key.Tab: return "Tab";
                    case Key.CapsLock: return "CapsLock";
                    case Key.LeftShift: return "LShift";
                    case Key.RightShift: return "RShift";
                    case Key.LeftCtrl: return "LCtrl";
                    case Key.RightCtrl: return "RCtrl";
                    case Key.LeftAlt: return "LAlt";
                    case Key.RightAlt: return "RAlt";
                    case Key.Left: return "Left";
                    case Key.Right: return "Right";
                    case Key.Up: return "Up";
                    case Key.Down: return "Down";
                    case Key.Insert: return "Insert";
                    case Key.Delete: return "Delete";
                    case Key.Home: return "Home";
                    case Key.End: return "End";
                    case Key.PageUp: return "PageUp";
                    case Key.PageDown: return "PageDown";
                    case Key.OemComma: return ",";
                    case Key.OemPeriod: return ".";
                    case Key.OemQuestion: return "?";
                    case Key.OemSemicolon: return ";";
                    case Key.OemQuotes: return "'";
                    case Key.OemOpenBrackets: return "[";
                    case Key.OemCloseBrackets: return "]";
                    case Key.OemPipe: return "\\";
                    case Key.OemMinus: return "-";
                    case Key.OemPlus: return "=";
                    case Key.OemTilde: return "`";
                    default: return key.ToString();
                }
            }
            

        #endregion
    
    
    
    
    
        
        
        
    // public object GetJsonData()
    // {
    //         // get parent sizes if parent is Canvas
    //         double parentW = OriginalParentWidth;
    //         double parentH = OriginalParentHeight;
    //
    //         if (this.Parent is Canvas c)
    //         {
    //             parentW = c.ActualWidth;
    //             parentH = c.ActualHeight;
    //         }
    //
    //         double x = Canvas.GetLeft(this);
    //         double y = Canvas.GetTop(this);
    //
    //         var data = new Dictionary<string, object>
    //         {
    //             [Name] = new Dictionary<string, object>
    //             {
    //                 ["KeyName"] = KeyName,
    //                 ["parent_width"] = parentW,
    //                 ["parent_height"] = parentH,
    //                 ["x"] = x,
    //                 ["y"] = y,
    //                 ["width"] = this.ActualWidth,
    //                 ["height"] = this.ActualHeight,
    //             
    //             }
    //         };
    //
    //         return data;
    // }

    
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
            ["keys"] = keys,
            ["x"] = scaledX,
            ["y"] = scaledY,
            ["width"] = this.ActualWidth,
            ["height"] = this.ActualHeight,
            ["parent_width"] = deviceW,
            ["parent_height"] = deviceH,
            ["scaled_width"] = scaled_width,   // ✅ scaled width ratio
            ["scaled_height"] = scaled_height, // ✅ scaled height ratio
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
            
            
            DisplayKey.Text = KeyName;
            InputField.Text = KeyName;

            // Set position
            // Store parent dimensions for resizing logic
            OriginalParentWidth = data.ParentWidth;
            OriginalParentHeight = data.ParentHeight;
                
            OriginalX = data.X  + (this.ActualWidth / 2.0);
            OriginalY = data.Y  + (this.ActualHeight / 2.0);

                
         
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