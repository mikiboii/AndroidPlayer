using System;
using System.Collections.Generic;
using System.Linq;
using Androidplayer.Src.Keymap.Keymap_items;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Androidplayer.Store;

namespace Androidplayer.Src.Keymap.Keymap_items;

public partial class Image_nokey_normal : UserControl, IKeymapElement
{
    // Avalonia's Bitmap doesn't preserve the source URI, so track it ourselves.
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

    public Image_nokey_normal()
    {
        InitializeComponent();
        Loaded += ui_Loaded;
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

        // Avalonia: no BitmapImage.UriSource — use our stored path
        string? imgPath = _imagePath;

        // return new Dictionary<string, object>
        // {
        //     ["type"] = Name,
        //     ["keys"] = keys,
        //     ["x"] = x,
        //     ["y"] = y,
        //     ["width"] = this.Bounds.Width,
        //     ["height"] = this.Bounds.Height,
        //     ["parent_width"] = parentW,
        //     ["parent_height"] = parentH,
        //     ["Img path"] = imgPath!,
        //     ["App name"] = my_info.Instance?.Appname!
        // };
        
        
        return new KeymapElement
        {
            Type         = Name,
            Keys         = keys,
            X            = x,
            Y            = y,
            Width        = this.Bounds.Width,
            Height       = this.Bounds.Height,
            ParentWidth  = parentW,
            ParentHeight = parentH,
            ScaledWidth  = 0,
            ScaledHeight = 0,
            ImagePath    = imgPath,
            AppName      = my_info.Instance?.Appname
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