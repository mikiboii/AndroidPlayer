using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    public partial class Visual_keymap : UserControl, IKeymapElement
    {
        public double OriginalParentWidth { get; private set; }
        public double OriginalParentHeight { get; private set; }
        public double OriginalX { get; private set; }
        public double OriginalY { get; private set; }

        public string Name { get; set; } = "Visual";
        public string KeyName { get; set; } = "";

        public bool IsEditing { get; private set; } = false;

        private List<string> my_keys = new List<string>();

        private bool _isDragging = false;
        private Point _clickPosition;

        public Visual_keymap()
        {
            InitializeComponent();

            Loaded += Visual_keymap_Loaded;

            Circle_Grid.PointerPressed += DisplayKey_MouseLeftButtonDown;

            TopLeftThumb.DragDelta += ResizeThumb_DragDelta;
            TopRightThumb.DragDelta += ResizeThumb_DragDelta;
            BottomLeftThumb.DragDelta += ResizeThumb_DragDelta;
            BottomRightThumb.DragDelta += ResizeThumb_DragDelta;

            InputField.LostFocus += InputField_LostKeyboardFocus;
            InputField.KeyDown += InputField_KeyDown;
            InputField.KeyUp += InputField_KeyUp;

            InputField.TextInput += (s, e) => e.Handled = true;
            InputField.AddHandler(InputElement.KeyDownEvent, InputField_KeyDown, RoutingStrategies.Tunnel);
        }

        private void Visual_keymap_Loaded(object? sender, RoutedEventArgs e)
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

        private void RootGrid_MouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source is Thumb || e.Source is Button)
                return;

            _isDragging = true;
            _clickPosition = e.GetPosition(this);
            e.Pointer.Capture(RootGrid);
            e.Handled = true;
        }

        private void RootGrid_MouseMove(object? sender, PointerEventArgs e)
        {
            if (!_isDragging || this.Parent is not Canvas parentCanvas)
                return;

            var currentPosition = e.GetPosition(parentCanvas);

            double newLeft = currentPosition.X - _clickPosition.X;
            double newTop = currentPosition.Y - _clickPosition.Y;

            if (newLeft < 0) newLeft = 0;
            if (newTop < 0) newTop = 0;
            if (newLeft + Bounds.Width > parentCanvas.Bounds.Width)
                newLeft = parentCanvas.Bounds.Width - Bounds.Width;
            if (newTop + Bounds.Height > parentCanvas.Bounds.Height)
                newTop = parentCanvas.Bounds.Height - Bounds.Height;

            Canvas.SetLeft(this, newLeft);
            Canvas.SetTop(this, newTop);

            OriginalParentWidth = parentCanvas.Bounds.Width;
            OriginalParentHeight = parentCanvas.Bounds.Height;

            OriginalX = newLeft + (this.Bounds.Width / 2.0);
            OriginalY = newTop + (this.Bounds.Height / 2.0);

            e.Handled = true;
        }

        private void RootGrid_MouseLeftButtonUp(object? sender, PointerReleasedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }

        private void ResizeThumb_DragDelta(object? sender, VectorEventArgs e)
        {
            if (this.Parent is not Canvas parentCanvas)
                return;

            double left = Canvas.GetLeft(this);
            double top = Canvas.GetTop(this);
            double width = this.Width;
            double height = this.Height;

            double deltaX = e.Vector.X;
            double deltaY = e.Vector.Y;
            double delta = 0;
            double newLeft = left;
            double newTop = top;

            if (sender == TopLeftThumb)
            {
                delta = Math.Min(deltaX, deltaY);
                width -= delta; height = width;
                newLeft += delta; newTop += delta;
            }
            else if (sender == TopRightThumb)
            {
                delta = Math.Min(-deltaX, deltaY);
                width -= delta; height = width;
                newTop += delta;
            }
            else if (sender == BottomLeftThumb)
            {
                delta = Math.Min(deltaX, -deltaY);
                width -= delta; height = width;
                newLeft += delta;
            }
            else if (sender == BottomRightThumb)
            {
                delta = Math.Max(deltaX, deltaY);
                width += delta; height = width;
            }

            if (width < 70) { width = 70; height = 70; return; }
            if (width > 190) { width = 190; height = 190; return; }

            if (newLeft < 0) newLeft = 0;
            if (newTop < 0) newTop = 0;

            if (newLeft + width > parentCanvas.Bounds.Width)
                width = parentCanvas.Bounds.Width - newLeft;
            if (newTop + height > parentCanvas.Bounds.Height)
                height = parentCanvas.Bounds.Height - newTop;

            double finalSize = Math.Min(width, height);

            this.Width = finalSize;
            this.Height = finalSize;
            Canvas.SetLeft(this, newLeft);
            Canvas.SetTop(this, newTop);
        }

        #region Editing

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
        }

        private void DisplayKey_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            double newWidth = DisplayKey.Bounds.Width + 20;
            if (newWidth < 60)
                this.Width = 60;
            else
                this.Width = newWidth;
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
            foreach (var element in OverlayManager.Instance!.DroppedElements)
            {
                if (element is IKeymapElement keyEl)
                {
                    List<string> KeymapElement_parts = new List<string>();
                    List<string> my_key_parts = new List<string>();

                    if (string.IsNullOrEmpty(keyEl.KeyName))
                        continue;

                    KeymapElement_parts = keyEl.KeyName.Split('+').ToList();
                    my_key_parts = (InputField.Text ?? "").Split('+').ToList();

                    if (my_key_parts.Count > 0 && KeymapElement_parts.Count > 0 &&
                        my_key_parts[0] == KeymapElement_parts[0])
                    {
                        if (my_key_parts.Count == 2 && KeymapElement_parts.Count == 2)
                        {
                            if (my_key_parts[1] == KeymapElement_parts[1])
                                return true;
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

            StopEditing();
        }

        private void InputField_KeyDown(object? sender, KeyEventArgs e)
        {
            Key actualKey = e.Key;

            // if (e.IsRepeat)
            //     return;

            string pressedKey = ConvertKeyToString(actualKey);

            if (is_notallowed_key(pressedKey))
            {
                e.Handled = true;
                return;
            }

            my_keys.Add(pressedKey);

            if (my_keys.Count == 1)
            {
                InputField.Clear();
                InputField.Text = my_keys[0];

                if (string.IsNullOrEmpty(Name) || Name == "Multi key")
                    Name = "Regular key";
            }

            if (my_keys.Count == 2)
            {
                string shortcut = string.Join("+", my_keys);

                InputField.Clear();
                InputField.Text = shortcut;

                if (string.IsNullOrEmpty(Name) || Name == "Regular key")
                    Name = "Multi key";

                my_keys.Clear();
            }

            e.Handled = true;
        }

        private void InputField_KeyUp(object? sender, KeyEventArgs e)
        {
            Key actualKey = e.Key;

            string releasedKey = ConvertKeyToString(actualKey);

            my_keys.Remove(releasedKey);

            e.Handled = true;
        }

        #endregion

        #region Key converter

        private bool is_notallowed_key(string key)
        {
            var disallowedKeys = K_store.k_info.Instance.DisallowedKeys;
            return disallowedKeys.Contains(key);
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

        public object GetJsonData()
        {
            double parentW = OriginalParentWidth;
            double parentH = OriginalParentHeight;

            if (this.Parent is Canvas c)
            {
                parentW = c.Bounds.Width;
                parentH = c.Bounds.Height;
            }

            int deviceW = (int)(My_Store.Instance?.DeviceWidth ?? 0);
            int deviceH = (int)(My_Store.Instance?.DeviceHeight ?? 0);

            double x = Canvas.GetLeft(this);
            double y = Canvas.GetTop(this);

            double scaledX = x / parentW * deviceW;
            double scaledY = y / parentH * deviceH;

            double scaled_width = this.Bounds.Width / parentW * deviceW;
            double scaled_height = this.Bounds.Height / parentH * deviceH;

            List<string> keys = new();
            if (!string.IsNullOrEmpty(KeyName))
                keys = KeyName.Split('+', StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim())
                    .ToList();

            return new Dictionary<string, object>
            {
                ["type"] = Name,
                ["keys"] = keys,
                ["x"] = scaledX,
                ["y"] = scaledY,
                ["width"] = this.Bounds.Width,
                ["height"] = this.Bounds.Height,
                ["parent_width"] = deviceW,
                ["parent_height"] = deviceH,
                ["scaled_width"] = scaled_width,
                ["scaled_height"] = scaled_height,
                ["Img path"] = null!,
                ["App name"] = my_info.Instance?.Appname!
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

                this.Width = data.Width;
                this.Height = data.Height;

                DisplayKey.Text = KeyName;
                InputField.Text = KeyName;

                OriginalParentWidth = data.ParentWidth;
                OriginalParentHeight = data.ParentHeight;

                OriginalX = data.X + (this.Bounds.Width / 2.0);
                OriginalY = data.Y + (this.Bounds.Height / 2.0);

                if (this.Parent is not Canvas parentCanvas)
                    return;

                FixPosOnParentResize(parentCanvas.Bounds.Width, parentCanvas.Bounds.Height);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ SetJsonData failed for {Name}: {ex.Message}");
            }
        }

        private void remove_btn_OnClick(object? sender, RoutedEventArgs e)
        {
            Console.WriteLine("remove element");
            OverlayManager.Instance?.RemoveElement(this);
        }

        private void PlusButton_Click(object? sender, PointerPressedEventArgs e)
        {
            Console.WriteLine("Plus clicked!");
        }
    }
}