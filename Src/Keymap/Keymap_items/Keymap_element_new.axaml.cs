using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Androidplayer.Src.Keymap.Keymap_items;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Androidplayer.Store;

namespace Androidplayer.Src.Keymap.Keymap_items
{
    public partial class KeymapElementNew : UserControl, IKeymapElement
    {
        public double OriginalParentWidth { get; private set; }
        public double OriginalParentHeight { get; private set; }
        public double OriginalX { get; private set; }
        public double OriginalY { get; private set; }

        public string Name { get; set; } = "";
        public string KeyName { get; set; } = "";

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
            RootGrid.PointerPressed += DisplayKey_MouseLeftButtonDown;

            InputField.LostFocus += InputField_LostKeyboardFocus;
            InputField.KeyDown += InputField_KeyDown;
            InputField.KeyUp += InputField_KeyUp;

            // Avalonia: TextInput is the preview-text-input equivalent
            InputField.TextInput += (s, e) => e.Handled = true;

            // Avalonia: tunneling handler replaces PreviewKeyDown
            InputField.AddHandler(InputElement.KeyDownEvent,
                InputField_KeyDown,
                RoutingStrategies.Tunnel);

            // Mouse events for dragging entire control
            this.PointerPressed += Control_MouseLeftButtonDown;
            this.PointerMoved += Control_MouseMove;
            this.PointerReleased += Control_MouseLeftButtonUp;

            // Visual tweaks
            this.Cursor = new Cursor(StandardCursorType.Arrow);
        }

        private void my_store_propertychanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
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

        private void DisplayKey_MouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                Console.WriteLine("Double-click detected!");
                DisplayKey_MouseDoubleClick(sender, e);
            }
            else if (e.ClickCount == 1)
            {
                Console.WriteLine("Single-click detected!");
            }
            Console.WriteLine("DisplayKey single click");
        }

        private void KeymapElementNew_Loaded(object? sender, RoutedEventArgs e)
        {
            if (this.Parent is Canvas c)
            {
                OriginalParentWidth = c.Bounds.Width;
                OriginalParentHeight = c.Bounds.Height;

                OriginalX = Canvas.GetLeft(this) + (this.Bounds.Width / 2.0);
                OriginalY = Canvas.GetTop(this) + (this.Bounds.Height / 2.0);

                c.SizeChanged += Parent_SizeChanged;

                if (KeyName == "")
                {
                    StartEditing();
                }
            }
        }

        private void Parent_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            double newWidth = e.NewSize.Width;
            double newHeight = e.NewSize.Height;

            FixPosOnParentResize(newWidth, newHeight);
        }

        #region Close / Remove

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            // string jsonString = JsonSerializer.Serialize(GetJsonData(),
            //     new JsonSerializerOptions { WriteIndented = true });
            
            string jsonString = JsonSerializer.Serialize(
                GetJsonData(),
                KeymapJsonContext.Default.KeymapElement);
            
            Console.WriteLine(jsonString);

            OverlayManager.Instance?.RemoveElement(this);
        }

        #endregion

        #region Editing (double-click -> show TextBox)

        private void DisplayKey_MouseDoubleClick(object? sender, PointerPressedEventArgs e)
        {
            StartEditing();
            e.Handled = true;
        }

        private void StartEditing()
        {
            DisplayKey.Text = KeyName;
            InputField.Text = KeyName;

            IsEditing = true;
            DisplayKey.IsVisible = false;
            InputField.IsVisible = true;
            InputField.Focus();
        }

        private void StopEditing()
        {
            IsEditing = false;
            InputField.IsVisible = false;
            DisplayKey.IsVisible = true;
            KeyName = InputField.Text?.Trim() ?? "";
            DisplayKey.Text = KeyName;
        }

        private bool key_already_exists()
        {
            Console.WriteLine($"size of elements : {OverlayManager.Instance!.DroppedElements.Count}");

            foreach (var element in OverlayManager.Instance!.DroppedElements)
            {
                if (element is IKeymapElement keyEl)
                {
                    List<string> KeymapElement_parts = new List<string>();
                    List<string> my_key_parts = new List<string>();

                    if (string.IsNullOrEmpty(keyEl.KeyName))
                    {
                        continue;
                    }

                    KeymapElement_parts = keyEl.KeyName.Split('+').ToList();
                    my_key_parts = (InputField.Text ?? "").Split('+').ToList();

                    if (my_key_parts.Count > 0 && KeymapElement_parts.Count > 0 &&
                        my_key_parts[0] == KeymapElement_parts[0])
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

        private void InputField_LostKeyboardFocus(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputField.Text))
            {
                OverlayManager.Instance?.RemoveElement(this);
                return;
            }

            if (key_already_exists())
            {
                OverlayManager.Instance?.RemoveElement(this);
                return;
            }

            Console.WriteLine($"length of the input {InputField.Text.Length} , value: {InputField.Text}");
            StopEditing();
        }

        #endregion

        private void DisplayKey_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            double newWidth = DisplayKey.Bounds.Width + 20;
            if (newWidth < 30)
            {
                this.Width = 30;
            }
            else
            {
                this.Width = newWidth;
            }
        }

        private void InputField_TextChanged(object? sender, TextChangedEventArgs e)
        {
            var formattedText = new FormattedText(
                InputField.Text ?? "",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(InputField.FontFamily, InputField.FontStyle, InputField.FontWeight, InputField.FontStretch),
                InputField.FontSize,
                Brushes.Black);

            this.Width = formattedText.Width + 20;
        }

        #region Shortcut capture (fixed version)

        private void BlockNavigationKeys(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left || e.Key == Key.Right ||
                e.Key == Key.Up || e.Key == Key.Down ||
                e.Key == Key.Back || e.Key == Key.Delete ||
                e.Key == Key.Space || e.Key == Key.Tab)
            {
                e.Handled = true;
            }
        }

        private void InputField_KeyDown(object? sender, KeyEventArgs e)
        {
            // Avalonia: no Key.System — e.Key is always the actual key
            Key actualKey = e.Key;

            // if (e.IsRepeat)
            //     return;

            string pressedKey = ConvertKeyToString(actualKey);

            if (is_notallowed_key(pressedKey))
            {
                e.Handled = true;
                return;
            }

            Console.WriteLine(pressedKey);

            my_keys.Add(pressedKey);

            if (my_keys.Count == 1)
            {
                InputField.Clear();
                InputField.Text = my_keys[0];

                if (string.IsNullOrEmpty(Name) || Name == "Multi key")
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

                my_keys.Clear();
            }

            e.Handled = true;
        }

        private void InputField_KeyUp(object? sender, KeyEventArgs e)
        {
            Key actualKey = e.Key;

            string releasedKey = ConvertKeyToString(actualKey);

            my_keys.Remove(releasedKey);

            Console.WriteLine(releasedKey);

            e.Handled = true;
        }

        #endregion

        #region Dragging (move inside Canvas)

        private void Control_MouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
        {
            if (e.ClickCount == 2) return;

            if (IsEditing) return;

            var parent = this.Parent as Canvas;
            if (parent == null) return;

            isDragging = true;
            dragStartPoint = e.GetPosition(parent);

            e.Pointer.Capture(this);

            OriginalParentWidth = parent.Bounds.Width;
            OriginalParentHeight = parent.Bounds.Height;
            OriginalX = Canvas.GetLeft(this) + (this.Bounds.Width / 2.0);
            OriginalY = Canvas.GetTop(this) + (this.Bounds.Height / 2.0);

            e.Handled = true;
        }

        private void Control_MouseMove(object? sender, PointerEventArgs e)
        {
            if (!isDragging) return;

            var parent = this.Parent as Canvas;
            if (parent == null) return;

            var pos = e.GetPosition(parent);
            var delta = pos - dragStartPoint;

            double newLeft = Canvas.GetLeft(this) + delta.X;
            double newTop = Canvas.GetTop(this) + delta.Y;

            newLeft = Math.Max(0, Math.Min(newLeft, parent.Bounds.Width - this.Bounds.Width));
            newTop = Math.Max(0, Math.Min(newTop, parent.Bounds.Height - this.Bounds.Height));

            Canvas.SetLeft(this, newLeft);
            Canvas.SetTop(this, newTop);

            dragStartPoint = pos;

            OriginalX = newLeft + (this.Bounds.Width / 2.0);
            OriginalY = newTop + (this.Bounds.Height / 2.0);
        }

        private void Control_MouseLeftButtonUp(object? sender, PointerReleasedEventArgs e)
        {
            if (!isDragging) return;
            isDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        #endregion

        #region Parent-resize repositioning (FixPos equivalent)

        public void FixPosOnParentResize(double newParentWidth, double newParentHeight)
        {
            if (OriginalParentWidth <= 0 || OriginalParentHeight <= 0)
                return;

            double scaleX = newParentWidth / OriginalParentWidth;
            double scaleY = newParentHeight / OriginalParentHeight;

            double newCenterX = OriginalX * scaleX;
            double newCenterY = OriginalY * scaleY;

            double newLeft = newCenterX - (Bounds.Width / 2.0);
            double newTop = newCenterY - (Bounds.Height / 2.0);

            if (Parent is Canvas c)
            {
                newLeft = Math.Max(0, Math.Min(newLeft, c.Bounds.Width - Bounds.Width));
                newTop = Math.Max(0, Math.Min(newTop, c.Bounds.Height - Bounds.Height));
            }

            Canvas.SetLeft(this, newLeft);
            Canvas.SetTop(this, newTop);
        }

        #endregion

        #region Serialization helpers

        // public object GetJsonData()
        // {
        //     double parentW = OriginalParentWidth;
        //     double parentH = OriginalParentHeight;
        //
        //     if (this.Parent is Canvas c)
        //     {
        //         parentW = c.Bounds.Width;
        //         parentH = c.Bounds.Height;
        //     }
        //
        //     double x = Canvas.GetLeft(this);
        //     double y = Canvas.GetTop(this);
        //
        //     List<string> keys = new();
        //     if (!string.IsNullOrEmpty(KeyName))
        //         keys = KeyName.Split('+', StringSplitOptions.RemoveEmptyEntries)
        //             .Select(k => k.Trim())
        //             .ToList();
        //
        //     int deviceW = (int)(My_Store.Instance?.DeviceWidth ?? 0);
        //     int deviceH = (int)(My_Store.Instance?.DeviceHeight ?? 0);
        //
        //     Console.WriteLine($"{x} : {y}");
        //     Console.WriteLine($"{deviceW} : {deviceH}");
        //     Console.WriteLine($"{parentW} : {parentH}");
        //
        //     double scaledX = x / parentW * deviceW;
        //     double scaledY = y / parentH * deviceH;
        //
        //     double scaled_width = this.Bounds.Width / parentW * deviceW;
        //     double scaled_height = this.Bounds.Height / parentH * deviceH;
        //
        //     var data = new Dictionary<string, object>
        //     {
        //         ["type"] = Name,
        //         ["keys"] = keys,
        //         ["x"] = scaledX,
        //         ["y"] = scaledY,
        //         ["width"] = this.Bounds.Width,
        //         ["height"] = this.Bounds.Height,
        //         ["parent_width"] = deviceW,
        //         ["parent_height"] = deviceH,
        //         ["scaled_width"] = scaled_width,
        //         ["scaled_height"] = scaled_height,
        //         ["Img path"] = null!,
        //         ["App name"] = my_info.Instance?.Appname!
        //     };
        //
        //     return data;
        // }
        
        public object GetJsonData()
        {
            double parentW = OriginalParentWidth;
            double parentH = OriginalParentHeight;

            if (this.Parent is Canvas c)
            {
                parentW = c.Bounds.Width;
                parentH = c.Bounds.Height;
            }

            double x = Canvas.GetLeft(this);
            double y = Canvas.GetTop(this);

            // Console.WriteLine($"getting item position {x} x {y}");
            
            if (double.IsNaN(x)) x = 0;
            if (double.IsNaN(y)) y = 0;

            List<string> keys = new();
            if (!string.IsNullOrEmpty(KeyName))
                keys = KeyName.Split('+', StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim())
                    .ToList();

            int deviceW = (int)(My_Store.Instance?.DeviceWidth ?? 0);
            int deviceH = (int)(My_Store.Instance?.DeviceHeight ?? 0);

            double scaledX = parentW > 0 ? x / parentW * deviceW : 0;
            double scaledY = parentH > 0 ? y / parentH * deviceH : 0;
            double scaledW = parentW > 0 ? this.Bounds.Width  / parentW * deviceW : 0;
            double scaledH = parentH > 0 ? this.Bounds.Height / parentH * deviceH : 0;

            return new KeymapElement
            {
                Type         = Name,
                Keys         = keys,
                X            = scaledX,
                Y            = scaledY,
                Width        = this.Bounds.Width,
                Height       = this.Bounds.Height,
                ParentWidth  = deviceW,
                ParentHeight = deviceH,
                ScaledWidth  = scaledW,
                ScaledHeight = scaledH,
                ImagePath    = null,
                AppName      = my_info.Instance?.Appname
            };
        }

        public void SetJsonData(KeymapElement data)
        {
            try
            {
                // Console.WriteLine($"[SetJsonData] got Type={data.Type}, X={data.X}, Y={data.Y}, W={data.Width}, H={data.Height}");
                // Console.WriteLine($"[SetJsonData]   Keys={(data.Keys == null ? "null" : string.Join(",", data.Keys))}");

      

              
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

                OriginalX = data.X + (this.Bounds.Width / 2.0);
                OriginalY = data.Y + (this.Bounds.Height / 2.0);

                if (this.Parent is Canvas parentCanvas)
                    FixPosOnParentResize(parentCanvas.Bounds.Width, parentCanvas.Bounds.Height);
                
                // Console.WriteLine($"[SetJsonData]   after apply → this.Bounds={Bounds}, Canvas.Left={Canvas.GetLeft(this)}, Canvas.Top={Canvas.GetTop(this)}");

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
    }
}