using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Androidplayer.Src.Android;
using Androidplayer.Store;

// using WinFormsMouseEventArgs  = System.Windows.Forms.MouseEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

using my_flyleaf = FlyleafLib_01.Controls.WPF.FlyleafHost;

namespace Androidplayer.Src.Mouse;

public class Mouse_normal
{
    
    private const byte ACTION_MOVE = 0x02;
    private const byte ACTION_DOWN = 0x00;
    private const byte ACTION_UP = 0x01;
    
    private int? _previousX = null;
    private int? _previousY = null;
    
    public Mouse_normal()
    {
        
        
        
    }
    
    
    public void mouse_Move(MouseEventArgs  e, my_flyleaf my_MainImage)
    {
        var pos = e.GetPosition(my_MainImage.Surface);
        
        double x = pos.X;
        double y = pos.Y;
        if (e.LeftButton != MouseButtonState.Pressed)
            return;
        
        // WinForms coordinates are relative to the control
       
    
        
        var (scaledX, scaledY) = ScaleCoordinates(x, y);
        
        // Smooth coordinates (optional)
        // var (smoothedX, smoothedY) = SmoothCoordinates(scaledX, scaledY);
        // Check bounds
        if (!IsWithinBounds(scaledX, scaledY))
            return;
        // Console.WriteLine($"{scaledX}: {scaledY}");
            
        // Encode and send
        byte[] data = MyEncoder_2(scaledX, scaledY, ACTION_MOVE);
        SendData(data);
        // Do something with the coordinates
        // Console.WriteLine($"Mouse at: X={x:F1}, Y={y:F1}");
            
        // Console.WriteLine($"Mouse moved to from class: {x}, {y}");
        // Add your mouse movement logic here
    }
    
    public void OnMouseDown(MouseEventArgs  e, my_flyleaf my_MainImage)
    {
        
      
        
        
        
        var pos = e.GetPosition(my_MainImage.Surface);
        
        double x = pos.X;
        double y = pos.Y;

        
        // Console.WriteLine($" from normal mouse {x} : {y}");
      
        // WinForms coordinates are relative to the control
    
        if (e.LeftButton == MouseButtonState.Pressed)
        {

            // Stopwatch stopwatch = Stopwatch.StartNew();
            // var res = My_Store.Instance.DeviceResolution;

            // Console.WriteLine($"device res from normal_mouse class {res.Width } , {res.Height}");
            //
            
            // Console.WriteLine($"Left mouse button PRESSED at ({x:F1}, {y:F1})");
            
            var (scaledX, scaledY) = ScaleCoordinates(x, y);

            
            
            // Reset previous coordinates for smoothing
            _previousX = scaledX;
            _previousY = scaledY;


            // Console.WriteLine($"device location ({scaledX}, {scaledY})");
            // Encode and send
            byte[] data = MyEncoder_2(scaledX, scaledY, ACTION_DOWN);
            SendData(data);
            
            
            // 0.002 milliseconds (ms)
            
            
            
            // stopwatch.Stop();
            // double microseconds = stopwatch.ElapsedTicks * 1000000.0 / Stopwatch.Frequency;
            // double milliseconds = microseconds / 1000.0;
            // Console.WriteLine($"{milliseconds:F3} milliseconds (ms)");
            
            
            
            // Console.WriteLine($"MouseDown took: {stopwatch.ElapsedMilliseconds}ms | {stopwatch.ElapsedTicks} ticks");
        }
        else if (e.RightButton == MouseButtonState.Pressed)
        {
            // Console.WriteLine($"Right mouse button PRESSED at ({x:F1}, {y:F1})");
        }
    }

    public void OnMouseUp(MouseEventArgs  e, my_flyleaf my_MainImage)
    {
        var pos = e.GetPosition(my_MainImage.Surface);
        
        double x = pos.X;
        double y = pos.Y;

        if (e.LeftButton == MouseButtonState.Released)
        {
            // Console.WriteLine($"Left mouse button RELEASED at ({x:F1}, {y:F1})");
            
            var (scaledX, scaledY) = ScaleCoordinates(x, y);
            
            // Check bounds
            // if (!IsWithinBounds(scaledX, scaledY))
            //     return;
            
            var deviceRes = My_Store.Instance.DeviceResolution;
        
            if (scaledX > deviceRes.Width || scaledX < 0)
                // return false;
                if (scaledX > deviceRes.Width)
                {
                    scaledX = deviceRes.Width;
                }else if (scaledX < 0)
                {
                    scaledX = 0;
                }
            
            if (scaledY > deviceRes.Height || scaledY < 0)
                // return false;
                if (scaledY > deviceRes.Height)
                {
                    scaledY = deviceRes.Height;
                }else if (scaledY < 0)
                {
                    scaledY = 0;
                }
            
            // Console.WriteLine($" released at {scaledX}: {scaledY}");
            // Encode and send
            byte[] data = MyEncoder_2(scaledX, scaledY, ACTION_UP);
            SendData(data);
            
            
        }
        else if (e.RightButton == MouseButtonState.Released)
        {
            // Console.WriteLine($"Right mouse button RELEASED at ({x:F1}, {y:F1})");
        }
    }
    
    
    private bool IsWithinBounds(int x, int y)
    {
        var deviceRes = My_Store.Instance.DeviceResolution;
        
        if (x > deviceRes.Width || x < 0)
            return false;
            
        if (y > deviceRes.Height || y < 0)
            return false;
            
        return true;
    }



    public void mouse_Wheel(MouseWheelEventArgs e, my_flyleaf my_MainImage)
    {
        
        
        
        var pos = e.GetPosition(my_MainImage.Surface);
        var (x, y) = ScaleCoordinates(pos.X, pos.Y);

        // WPF gives delta: +120, -120
        float delta = e.Delta / 120f;

        // Console.WriteLine($"{delta}");
        if (e.Delta > 0)
        {
            
        }
        
        
        float vscroll = delta;  // invert for Android
        float hscroll = 0;       // horizontal scroll unused here
         
        var data = EncodeScrollEvent(x, y, hscroll, vscroll);
        SendData(data);
        
    }
    
    
    
    private (int x, int y) ScaleCoordinates(double displayX, double displayY)
    {
        var deviceRes = My_Store.Instance.DeviceResolution;
        var displayRes = My_Store.Instance.DisplayResolution;
        
        // If no display resolution stored yet, use current values

        
           
        if (displayRes.Width == 0 || displayRes.Height == 0)
        {
            // Console.WriteLine($"{deviceRes} : {displayRes}");
            return ((int)displayX, (int)displayY);
            
        }

        // Console.WriteLine(deviceRes);
        // Calculate scaling factors (same as Python code)
        double scaleX = (double)deviceRes.Width / displayRes.Width;
        double scaleY = (double)deviceRes.Height / displayRes.Height;
        
        // Apply scaling and round to nearest integer
        int targetX = (int)Math.Round(displayX * scaleX);
        int targetY = (int)Math.Round(displayY * scaleY);
        
        return (targetX, targetY);
    }
    
    
    // private byte[] MyEncoder(int x, int y, byte action, ushort pressure = 32768)
    // {
    //     using (var stream = new MemoryStream())
    //     using (var writer = new BinaryWriter(stream))
    //     {
    //         // Message type (0x02 for mouse)
    //         writer.Write((byte)0x02);
    //     
    //         // Action (move/down/up)
    //         writer.Write(action);
    //     
    //         // Fixed value (0x1234567887654321 in big-endian)
    //         writer.Write((long)0x1234567887654321);
    //     
    //         // X and Y coordinates (big-endian)
    //         writer.Write(ToBigEndian(BitConverter.GetBytes((uint)x)));
    //         writer.Write(ToBigEndian(BitConverter.GetBytes((uint)y)));
    //     
    //         // Screen dimensions (big-endian)
    //         var deviceRes = My_Store.Instance.DeviceResolution;
    //         writer.Write(ToBigEndian(BitConverter.GetBytes((short)deviceRes.Width)));
    //         writer.Write(ToBigEndian(BitConverter.GetBytes((short)deviceRes.Height)));
    //     
    //         // Pressure (clamped to 0-65535) - SIMPLER APPROACH
    //         if (pressure > 65535) pressure = 65535;
    //         if (pressure < 0) pressure = 0;
    //         writer.Write(ToBigEndian(BitConverter.GetBytes(pressure)));
    //     
    //         // Event button primary (0x00000001)
    //         writer.Write((int)1);
    //     
    //         return stream.ToArray();
    //     }
    // }
    
    private byte[] ToBigEndian(byte[] data)
    {
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(data);
        }
        return data;
    }
    
    
    private byte[] MyEncoder_2(int x, int y, byte action, float pressure = 1.0f)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((byte)0x02); // Message type
        
            writer.Write(action); // Action
        
            // Pointer ID (64-bit big-endian)
            // WriteBigEndian(writer, 0x1234567887654321UL);
        
            WriteBigEndian(writer, (ulong)Touch_id.normal_mouse);
            
            // Coordinates (32-bit big-endian)
            WriteBigEndian(writer, (uint)x);
            WriteBigEndian(writer, (uint)y);
        
            // Screen size (16-bit big-endian)
            var deviceRes = My_Store.Instance.DeviceResolution;
            WriteBigEndian(writer, (ushort)deviceRes.Width);
            WriteBigEndian(writer, (ushort)deviceRes.Height);
        
            // Pressure (16-bit big-endian)
            ushort pressureEncoded = (ushort)(pressure * 0xFFFF);
            WriteBigEndian(writer, pressureEncoded);
        
            // Buttons (32-bit big-endian)
            WriteBigEndian(writer, 0x00000001U); // action_button
            WriteBigEndian(writer, 0x00000001U); // buttons
        
            return stream.ToArray();
        }
    }

// Single helper method for all types
    private void WriteBigEndian<T>(BinaryWriter writer, T value) where T : struct
    {
        byte[] bytes = new byte[Marshal.SizeOf<T>()];
        MemoryMarshal.Write(bytes, ref value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        writer.Write(bytes);
    }
    
    
    
    
    
    
    private byte[] EncodeScrollEvent(int x, int y, float hScroll, float vScroll)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            // 1) message type
            writer.Write((byte)0x03); // SC_CONTROL_MSG_TYPE_INJECT_SCROLL_EVENT

            // 2) position (X, Y) - big endian
            WriteBigEndian(writer, (uint)x);
            WriteBigEndian(writer, (uint)y);

            // 3) screen size (width, height)
            var deviceRes = My_Store.Instance.DeviceResolution;
            WriteBigEndian(writer, (ushort)deviceRes.Width);
            WriteBigEndian(writer, (ushort)deviceRes.Height);

            // 4) scroll values encoded as int16
            short hEncoded = (short)Math.Clamp(hScroll / 16f * short.MaxValue, short.MinValue, short.MaxValue);
            short vEncoded = (short)Math.Clamp(vScroll / 16f * short.MaxValue, short.MinValue, short.MaxValue);

            WriteBigEndian(writer, (ushort)hEncoded);
            WriteBigEndian(writer, (ushort)vEncoded);

            // 5) buttons pressed (usually 0 unless middle-mouse button scrolling)
            WriteBigEndian(writer, 0x00000001U);

            return stream.ToArray();
        }
    }

    
    private void SendData(byte[] data)
    {
        TcpClient controlSocket = My_Store.Instance.ControlSocket;
    
        if (controlSocket != null && controlSocket.Connected)
        {
            try
            {
                Socket socket = controlSocket.Client;
            
                // OPTIMIZATION: Direct socket send with no delay
                socket.NoDelay = true;
                socket.Blocking = false; 
                socket.Send(data, 0, data.Length, SocketFlags.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending mouse data: {ex.Message}");
            }
        }
    }
    
}