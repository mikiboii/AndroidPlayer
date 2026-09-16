using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Androidplayer.Src.Keymap.Keymap_items;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Androidplayer.Store;

namespace Androidplayer.Src.Keymap.Keymap_items
{
    public partial class Keymap_element_normal : UserControl, IKeymapElement
    {
        // Public properties for parent tracking
        public double OriginalParentWidth { get; private set; }
        public double OriginalParentHeight { get; private set; }
        public double OriginalX { get; private set; }
        public double OriginalY { get; private set; }

        public double my_width { get; private set; }
        public double my_height { get; private set; }

        public string Name { get; set; } = "";
        public string KeyName { get; set; } = "";

        public bool IsEditing { get; private set; } = false;

        private List<string> my_keys = new List<string>();

        // Shortcut capture (unused here, kept for parity)
        private HashSet<Key> modifiersPressed = new HashSet<Key>();
        private Key? firstKey = null;
        private Key? secondKey = null;
        private int maxKeys = 2;

        // Dragging (unused here)
        private bool isDragging = false;
        private Point dragStartPoint;

        public Keymap_element_normal()
        {
            InitializeComponent();
            Loaded += KeymapElementNew_Loaded;

            // Visual tweaks
            this.Cursor = new Cursor(StandardCursorType.Arrow);
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
            }
        }

        private void Parent_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            double newWidth = e.NewSize.Width;
            double newHeight = e.NewSize.Height;

            FixPosOnParentResize(newWidth, newHeight);
        }

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

            if (Name != "Multi key" && Name != "Regular key")
            {
                double offsetX = (my_width - this.Width) / 2.0;
                double offsetY = (my_height / 2.0);

                double d_width = DisplayKey.Bounds.Width;

                double demo_offset = (my_height - 30) / 2.0;

                newLeft += offsetX;
                newTop += demo_offset;
            }

            Canvas.SetLeft(this, newLeft);
            Canvas.SetTop(this, newTop);
        }

        #endregion

        #region Serialization helpers

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

            List<string> keys = new();
            if (!string.IsNullOrEmpty(KeyName))
                keys = KeyName.Split('+', StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim())
                    .ToList();

            var data = new Dictionary<string, object>
            {
                ["type"] = Name,
                ["keys"] = keys,
                ["x"] = x,
                ["y"] = y,
                ["width"] = this.Bounds.Width,
                ["height"] = this.Bounds.Height,
                ["parent_width"] = parentW,
                ["parent_height"] = parentH,
                ["Img path"] = null!,
                ["App name"] = my_info.Instance?.Appname!
            };

            return data;
        }

        public void SetJsonData(KeymapElement data)
        {
            try
            {
                Name = data.Type;
                KeyName = string.Join("+", data.Keys);

                my_width = data.Width;
                my_height = data.Height;

                if (data.Type == "Regular key")
                {
                    this.Width = data.Width;
                    this.Height = data.Height;

                    my_width = data.ScaledWidth;
                    my_height = data.ScaledHeight;
                }

                DisplayKey.Text = KeyName;

                OriginalParentWidth = data.ParentWidth;
                OriginalParentHeight = data.ParentHeight;

                OriginalX = data.X + (this.Bounds.Width / 2.0);
                OriginalY = data.Y + (this.Bounds.Height / 2.0);

                if (this.Parent is Canvas parentCanvas)
                    FixPosOnParentResize(parentCanvas.Bounds.Width, parentCanvas.Bounds.Height);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ SetJsonData failed for {Name}: {ex.Message}");
            }
        }

        #endregion

        private void Keymap_element_normal_OnMouseDown(object? sender, PointerPressedEventArgs e)
        {
            Console.WriteLine("km pressed");
        }
    }
}