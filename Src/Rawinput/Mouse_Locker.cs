using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Androidplayer.Store;

using Canvas = Avalonia.Controls.Canvas ;

namespace Androidplayer.Src.Rawinput;

public class Mouse_Locker
{
    [DllImport("user32.dll")]
    private static extern bool ClipCursor(ref RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClipCursor(IntPtr lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private Canvas my_image;
    private bool isMouseClipped = false;

    public Mouse_Locker(Canvas image)
    {
        my_image = image;

        if (my_image != null)
        {
            int width = (int)my_image.Bounds.Width;    // Avalonia: Bounds instead of ActualWidth/Height
            int height = (int)my_image.Bounds.Height;
            // Console.WriteLine($"Form size: {width}x{height}");
        }

        my_info.Instance.PropertyChanged += my_info_propertychangeed;
    }

    private void my_info_propertychangeed(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(my_info.IsMouseLocked):

                bool ismouselocked = my_info.Instance.IsMouseLocked;

                Console.WriteLine("from mouse lock");

                if (ismouselocked == true)
                {
                    ClipMouseToImage();
                }
                else
                {
                    ReleaseMouseClip();
                }

                break;
        }
    }

    public void ClipMouseToImage()
    {
        if (my_image == null) return;

        if (!OperatingSystem.IsWindows())
        {
            // ClipCursor is Windows-only
            return;
        }

        try
        {
            double width90Percent = my_image.Bounds.Width * 0.9;
            double height90Percent = my_image.Bounds.Height * 0.9;

            double marginX = my_image.Bounds.Width * 0.05;
            double marginY = my_image.Bounds.Height * 0.05;

            // Avalonia: PointToScreen returns PixelPoint (physical pixels),
            // which is exactly what ClipCursor's RECT expects.
            PixelPoint topLeft = my_image.PointToScreen(new Point(marginX, marginY));
            PixelPoint bottomRight = my_image.PointToScreen(new Point(
                marginX + width90Percent,
                marginY + height90Percent));

            RECT clipRect = new RECT
            {
                Left = topLeft.X,
                Top = topLeft.Y,
                Right = bottomRight.X,
                Bottom = bottomRight.Y
            };

            if (ClipCursor(ref clipRect))
            {
                isMouseClipped = true;

                // WPF: Mouse.OverrideCursor = Cursors.None;
                // Avalonia: hide cursor on the control itself.
                my_image.Cursor = new Cursor(StandardCursorType.None);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clipping mouse: {ex.Message}");
        }
    }

    public void ReleaseMouseClip()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            if (ClipCursor(IntPtr.Zero))
            {
                isMouseClipped = false;

                // WPF: Mouse.OverrideCursor = null;
                // Avalonia: restore default cursor.
                my_image.Cursor = Cursor.Default;
                Console.WriteLine("Mouse clip released");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error releasing mouse clip: {ex.Message}");
        }
    }

    public bool IsMouseClipped => isMouseClipped;
}