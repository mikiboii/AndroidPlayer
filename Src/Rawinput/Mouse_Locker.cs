using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows; // For Point struct
using System.Windows.Controls;  // <-- WPF Image
using System.Windows;
using System.Windows.Input;
using Androidplayer.Store; // For Form class



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
    
    private System.Windows.Controls.Image my_image;
    private bool isMouseClipped = false;
    
    public Mouse_Locker(System.Windows.Controls.Image image)
    {
        my_image = image;
        
        if (my_image != null)
        {
            int width = (int)my_image.ActualWidth;
            int height = (int)my_image.ActualHeight;
            // Console.WriteLine($"Form size: {width}x{height}");
        }

        my_info.Instance.PropertyChanged += my_info_propertychangeed;

    }

    private void my_info_propertychangeed(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // case nameof(My_Store.VideoWidth):
            case nameof(my_info.IsMouseLocked):


                bool ismouselocked = my_info.Instance.IsMouseLocked;

                // Console.WriteLine("from mouse lock");

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
        if (my_image == null ) return;

        try
        {
            // Calculate 90% of the form dimensions
            double width90Percent = my_image.ActualWidth * 0.9;
            double height90Percent = my_image.ActualHeight * 0.9;
    
            // Calculate 5% margin on each side (to center the 90% area)
            double marginX = my_image.ActualWidth * 0.05;
            double marginY = my_image.ActualHeight * 0.05;

            // Convert form-relative coordinates to screen coordinates
            System.Windows.Point topLeft = my_image.PointToScreen(new System.Windows.Point((int)marginX, (int)marginY));
            System.Windows.Point bottomRight = my_image.PointToScreen(new System.Windows.Point((int)(marginX + width90Percent), (int)(marginY + height90Percent)));

           
            
            RECT clipRect = new RECT
            {
                Left = (int)topLeft.X,
                Top = (int)topLeft.Y,
                Right = (int)bottomRight.X,
                Bottom = (int)bottomRight.Y
            };

            if (ClipCursor(ref clipRect))
            {
                isMouseClipped = true;
                // Cursor.Hide();
                // my_winForm.Cursor = Cursors.;
                System.Windows.Input.Mouse.OverrideCursor = Cursors.None;
                // Console.WriteLine($"Mouse clipped to area: {clipRect.Left},{clipRect.Top} - {clipRect.Right},{clipRect.Bottom}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clipping mouse: {ex.Message}");
        }
    }

    public void ReleaseMouseClip()
    {
        try
        {
            if (ClipCursor(IntPtr.Zero))
            {
                isMouseClipped = false;
                // my_winForm.Cursor = Cursors.Arrow;
                System.Windows.Input.Mouse.OverrideCursor = null;
                Console.WriteLine("Mouse clip released");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error releasing mouse clip: {ex.Message}");
        }
    }
    
    // Helper method to check if mouse is currently clipped
    public bool IsMouseClipped => isMouseClipped;
}