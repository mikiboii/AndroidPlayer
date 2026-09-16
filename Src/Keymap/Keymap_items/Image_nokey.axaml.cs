using System;
using System.Collections.Generic;
using System.Linq;
using Androidplayer.Src.Keymap.Keymap_items;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Androidplayer.Store;

namespace Androidplayer.Src.Keymap.Keymap_items;

public partial class Image_nokey : UserControl, IKeymapElement
{
    // Avalonia's Bitmap doesn't preserve the source URI like WPF's BitmapImage,
    // so store it ourselves for GetJsonData round-tripping.
    private string? _imagePath;

    public double OriginalParentWidth { get; private set; }
    public double OriginalParentHeight { get; private set; }
    public double OriginalX { get; private set; }
    public double OriginalY { get; private set; }

    public string ImageSource
    {
        get => _imagePath ?? "";
        set
        {
            try
            {
                _imagePath = value;
                if (string.IsNullOrEmpty(value))
                {
                    ImageDisplay.Source = null;
                    return;
                }

                // Accept avares://, file://, http(s):// as-is; convert relative to avares://
                Uri uri = value.StartsWith("avares://") || value.StartsWith("file://") || value.StartsWith("http")
                    ? new Uri(value)
                    : new Uri($"avares://Androidplayer/{value.TrimStart('/', '.')}");

                ImageDisplay.Source = new Bitmap(AssetLoader.Open(uri));
            }
            catch (Exception ex)
            {
                Console.WriteLine("⚠️ Invalid image source: " + value + " — " + ex.Message);
            }
        }
    }

    public string Name { get; set; } = "";
    public string KeyName { get; set; } = "";

    private bool _isDragging = false;
    private Point _clickPosition;

    public Image_nokey()
    {
        InitializeComponent();
        Loaded += ui_Loaded;

        TopLeftThumb.DragDelta += ResizeThumb_DragDelta;
        TopRightThumb.DragDelta += ResizeThumb_DragDelta;
        BottomLeftThumb.DragDelta += ResizeThumb_DragDelta;
        BottomRightThumb.DragDelta += ResizeThumb_DragDelta;
    }

    private void ui_Loaded(object? sender, RoutedEventArgs e)
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

    private void RootGrid_MouseLeftButtonDown(object? sender, PointerPressedEventArgs e)
    {
        // Don't start dragging if the click is on a Thumb or Button
        if (e.Source is Thumb || e.Source is Button)
            return;

        Console.WriteLine("mouse down ######3");

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

        if (width < 50)
        {
            width = 50; height = 50;
            return;
        }

        if (width > 190)
        {
            width = 190; height = 190;
            return;
        }

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

        // Avalonia: no BitmapImage.UriSource — use our stored path
        string? imgPath = _imagePath;

        int deviceW = (int)(My_Store.Instance?.DeviceWidth ?? 0);
        int deviceH = (int)(My_Store.Instance?.DeviceHeight ?? 0);

        double scaledX = x / parentW * deviceW;
        double scaledY = y / parentH * deviceH;

        double scaled_width = this.Bounds.Width / parentW * deviceW;
        double scaled_height = this.Bounds.Height / parentH * deviceH;

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
            ["Img path"] = imgPath!,
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

            if (!string.IsNullOrEmpty(data.ImagePath))
                ImageSource = data.ImagePath;
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