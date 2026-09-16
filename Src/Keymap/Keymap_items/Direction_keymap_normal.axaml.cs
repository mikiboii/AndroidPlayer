using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Androidplayer.Src.Keymap.Keymap_items;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Androidplayer.Store;
using Avalonia.Media;

namespace Androidplayer.Src.Keymap.Keymap_items;

public partial class Direction_keymap_normal : UserControl, IKeymapElement
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

    private void Directon_keymap_Loaded(object? sender, RoutedEventArgs e)
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

        return new Dictionary<string, object>
        {
            ["type"] = Name,
            ["keys"] = new List<string> { "W", "A", "S", "D" },
            ["x"] = x,
            ["y"] = y,
            ["width"] = this.Bounds.Width,
            ["height"] = this.Bounds.Height,
            ["parent_width"] = parentW,
            ["parent_height"] = parentH,
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
}