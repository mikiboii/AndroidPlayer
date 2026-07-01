using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Input;
using Androidplayer.Store;


namespace Androidplayer.Src.Keymap.Keymap_items
{
    
    // public interface IKeymapElement
    // {
    //     string Name { get; set; }
    //     string KeyName { get; set; }
    //     
    //     object GetJsonData();
    // }

    public partial class KeymapElementNew : UserControl , IKeymapElement
    {
        // Public properties for parent tracking (similar to original_width/original_height/original_x/original_y)
        public double OriginalParentWidth { get; private set; }
        public double OriginalParentHeight { get; private set; }
        public double OriginalX { get; private set; }
        public double OriginalY { get; private set; }

        
        
        
        
        public string Name { get; set; }
        public string KeyName { get; set; } = "";
        //
        
        
        
        public bool IsEditing { get; private set; } = false;
        
        private List<string> my_keys = new List<string>();

        // Shortcut capture
        private HashSet<Key> modifiersPressed = new HashSet<Key>();
        private Key? firstKey = null;
        private Key? secondKey = null;
        private int maxKeys = 2;

        // Dragging
        private bool isDragging = false;
        private Point dragStartPoint;

        
        public KeymapElementNew()
        {
            InitializeComponent();
            Loaded += KeymapElementNew_Loaded;

            CloseButton.Click += CloseButton_Click;
            RootGrid.MouseLeftButtonDown += DisplayKey_MouseLeftButtonDown;
            // DisplayKey.MouseDoubleClick += DisplayKey_MouseDoubleClick;
            
            

            InputField.LostKeyboardFocus += InputField_LostKeyboardFocus;
            InputField.KeyDown += InputField_KeyDown;
            InputField.KeyUp += InputField_KeyUp;
            
            
            InputField.PreviewTextInput += (s, e) => e.Handled = true;
            InputField.PreviewKeyDown += InputField_KeyDown;

            
            // InputField.PreviewKeyDown += (s, e) => e.Handled = true;
            // InputField.IsReadOnly = true;

            // ResizeThumb.DragDelta += ResizeThumb_DragDelta;

            // Mouse events for dragging entire control
            this.MouseLeftButtonDown += Control_MouseLeftButtonDown;
            this.MouseMove += Control_MouseMove;
            this.MouseLeftButtonUp += Control_MouseLeftButtonUp;

            // Visual tweaks
            this.Cursor = Cursors.Arrow;


            // My_Store.Instance.PropertyChanged += my_store_propertychanged;
            
            
            // StartEditing();

        }

        private void my_store_propertychanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                // case nameof(My_Store.VideoWidth):
                case nameof(My_Store.DisplayResolution):

                    int d_width = My_Store.Instance.DisplayWidth;
                
                    int d_height = My_Store.Instance.DisplayHeight;

                    if (d_height > 0 && d_width > 0 && d_width != d_height)
                    {
                        Console.WriteLine($"Video resolution changed: {d_width}x{d_height}");
                    
                       
                    
                       FixPosOnParentResize(d_width, d_height);
                    
                    }
    
                    break;
 
            }
        }

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

     


        private void KeymapElementNew_Loaded(object sender, RoutedEventArgs e)
        {
            // If parent is a Canvas, capture original parent size
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
        
        

        #region Close / Remove
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Bubble a remove event so parent can remove and update array
            // RaiseEvent(new RoutedEventArgs(RemoveRequestedEvent, this));
            
            string jsonString = JsonSerializer.Serialize(GetJsonData(), new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(jsonString);
            
            OverlayManager.Instance?.RemoveElement(this);
        }
        #endregion

        #region Editing (double-click -> show TextBox)
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

            Console.WriteLine($"size of elements : {OverlayManager.Instance.DroppedElements.Count}");
            
            foreach (var element in OverlayManager.Instance.DroppedElements)
            {
                if (element is IKeymapElement keyEl)
                {
                    // Console.WriteLine($"Key: {keyEl.KeyName}");
                    // Console.WriteLine($"Key: {keyEl.Name}");

                    // Console.WriteLine("called key_already_exists function");

                    // string[] KeymapElement_parts;
                    List<string> KeymapElement_parts = new List<string>();
                    List<string> my_key_parts = new List<string>();
                    
                    
                    


                    if (keyEl.KeyName == null || keyEl.KeyName == "")
                    {
                        continue;
                    }
                    
                    // var disallowedKeys = K_store.k_info.Instance.DisallowedKeys;
                    //
                    // if (my_key_parts.Any(k => disallowedKeys.Contains(k.Trim())))
                    // {
                    //     Console.WriteLine("This key is not allowed.");
                    //     return true;
                    // }

                    
                    
                    
                    KeymapElement_parts = keyEl.KeyName.Split('+').ToList();
                    my_key_parts = InputField.Text.Split('+').ToList();
                    
                    // Console.WriteLine($"{my_key_parts[0]} ::: {KeymapElement_parts[0]}");
                  
                    if ( my_key_parts[0] == KeymapElement_parts[0])
                    {

                        // Console.WriteLine($"{my_key_parts.Count} ::: {KeymapElement_parts.Count}");

                        if (my_key_parts.Count == 2 && KeymapElement_parts.Count == 2)
                        {

                            // Console.WriteLine($"{my_key_parts[1]} ::: {KeymapElement_parts[1]}");
                            
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

            Console.WriteLine($"length of the input {InputField.Text.Length} , value: {InputField.Text}");
            StopEditing();
        }
        #endregion

      
        
        private void DisplayKey_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Add padding space
            double newWidth = DisplayKey.ActualWidth + 20;
            if (newWidth < 30)
            {
                this.Width = 30;
            }
            else
            {
                
                this.Width = newWidth;
                
            }

            // Console.WriteLine("size changed !");
        }

        
        private void InputField_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Measure how wide the text is
            var formattedText = new FormattedText(
                InputField.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(InputField.FontFamily, InputField.FontStyle, InputField.FontWeight, InputField.FontStretch),
                InputField.FontSize,
                Brushes.Black,
                new NumberSubstitution(),
                1);

            // Add a small padding
            this.Width = formattedText.Width + 20;
        }

        
        
        
        
        #region Shortcut capture (fixed version)
        
        
        
        
        private void BlockNavigationKeys(object sender, KeyEventArgs e)
        {
            // Block navigation and editing keys from doing anything in the TextBox
            if (e.Key == Key.Left || e.Key == Key.Right ||
                e.Key == Key.Up || e.Key == Key.Down ||
                e.Key == Key.Back || e.Key == Key.Delete ||
                e.Key == Key.Space || e.Key == Key.Tab)
            {
                e.Handled = true;
            }
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
        
        Name = "Regular key";
        
        // if (Name == null || Name == "Regular key" )
        // {
        //     Name = "Multi key";
        // }
        

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
    
    Console.WriteLine(releasedKey);
    
    
    
    e.Handled = true;
}



#endregion

        
    
        










        
        

        #region Dragging (move inside Canvas)
        private void Control_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) return; // double-click handled separately

            // if editing, don't start drag
            if (IsEditing) return;

            var parent = this.Parent as Canvas;
            if (parent == null) return;

            isDragging = true;
            dragStartPoint = e.GetPosition(parent);

            this.CaptureMouse();

            // update originals
            OriginalParentWidth = parent.ActualWidth;
            OriginalParentHeight = parent.ActualHeight;
            OriginalX = Canvas.GetLeft(this) + (this.ActualWidth / 2.0);
            OriginalY = Canvas.GetTop(this) + (this.ActualHeight / 2.0);

            e.Handled = true;
        }

        private void Control_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging) return;
            
            
            
             // double parentW = OriginalParentWidth;
             // double parentH = OriginalParentHeight;
             //
             //            if (this.Parent is Canvas c)
             //            {
             //                parentW = c.ActualWidth;
             //                parentH = c.ActualHeight;
             //            }
             //
             //            double x = Canvas.GetLeft(this);
             //            double y = Canvas.GetTop(this);
             //
             //
             //
             //
             
            

            var parent = this.Parent as Canvas;
            if (parent == null) return;

            var pos = e.GetPosition(parent);
            var delta = pos - dragStartPoint;

            double newLeft = Canvas.GetLeft(this) + delta.X;
            double newTop = Canvas.GetTop(this) + delta.Y;

            // constrain inside parent
            newLeft = Math.Max(0, Math.Min(newLeft, parent.ActualWidth - this.ActualWidth));
            newTop = Math.Max(0, Math.Min(newTop, parent.ActualHeight - this.ActualHeight));

            Canvas.SetLeft(this, newLeft);
            Canvas.SetTop(this, newTop);

            dragStartPoint = pos;

            // update stored original_x/y as in your Python code
            
            
            
            
            OriginalX = newLeft + (this.ActualWidth / 2.0);
            OriginalY = newTop + (this.ActualHeight / 2.0);
        }

        private void Control_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDragging) return;
            isDragging = false;
            this.ReleaseMouseCapture();
            e.Handled = true;
        }
        #endregion

        #region Resize handling
      
        #endregion

        #region Parent-resize repositioning (FixPos equivalent)
        /// <summary>
        /// Call this from the parent when parent canvas size changes to reposition the element similar to your Qt fix_pos logic.
        /// </summary>
        /// <param name="newParentWidth"></param>
        /// <param name="newParentHeight"></param>
        ///
        
        
        
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

        
        
        
        
        
        
        #endregion

        #region Serialization helpers (get_json_data / set_info)
      
        
     
        
        
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
            
            
                   
            int deviceW = (int)My_Store.Instance?.DeviceWidth;
            int deviceH = (int)My_Store.Instance?.DeviceHeight ;

            Console.WriteLine($"{x} : {y}");
            Console.WriteLine($"{deviceW} : {deviceH}");
            Console.WriteLine($"{parentW} : {parentH}");

            double scaledX = x / parentW * deviceW;
            double scaledY = y / parentH * deviceH;

            // Get 

            double scaled_width = this.ActualWidth / parentW * deviceW;
            double scaled_height = this.ActualHeight / parentH * deviceH;  


            var data = new Dictionary<string, object>
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
                ["Img path"] = null,
                ["App name"] = my_info.Instance?.Appname
            };

         
            return data;
        }

        
        
        
       
        public void SetJsonData(KeymapElement data)
        {
            try
            {
         
                Name = data.Type;
                KeyName = string.Join("+", data.Keys);
                Width = data.Width;
                Height = data.Height;

                
                this.Width = data.Width;
                this.Height = data.Height;
                
                DisplayKey.Text = KeyName;
                InputField.Text = KeyName;

                OriginalParentWidth = data.ParentWidth;
                OriginalParentHeight = data.ParentHeight;

                // OriginalX = data.X + (data.Width / 2.0);
                // OriginalY = data.Y + (data.Height / 2.0);
               
                
                
                
                
                
                OriginalX = data.X  + (this.ActualWidth / 2.0);
                OriginalY = data.Y  + (this.ActualHeight / 2.0);
                
                // OriginalX = data.X ;
                // OriginalY = data.Y ;
                

                // if (Parent is Canvas canvas)
                // {
                //     Canvas.SetLeft(this, data.X);
                //     Canvas.SetTop(this, data.Y);
                // }
                
                // FixPosOnParentResize(OriginalParentWidth, OriginalParentHeight);

                if (this.Parent is Canvas parentCanvas)
                    FixPosOnParentResize(parentCanvas.ActualWidth, parentCanvas.ActualHeight);

                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ SetJsonData failed for {Name}: {ex.Message}");
            }
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
                
                // switch (key)
                // {
                //     case "Backspace": return true;
                //     case "RWin": return true;
                //     case "LWin": return true;
                //     default: return false;
                //     
                //     
                // }
                
                
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
        
    }
}



