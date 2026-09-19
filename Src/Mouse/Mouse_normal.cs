using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Avalonia.Input;
using Androidplayer.Src.Android;
using Androidplayer.Store;

using Canvas = Avalonia.Controls.Canvas ;

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

    public void mouse_Move(PointerEventArgs e, Canvas my_MainImage)
    {
        var pos = e.GetPosition(my_MainImage);

        double x = pos.X;
        double y = pos.Y;

        // Avalonia: get button state from the current pointer point.
        var point = e.GetCurrentPoint(my_MainImage);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        var (scaledX, scaledY) = ScaleCoordinates(x, y);

        // Check bounds
        if (!IsWithinBounds(scaledX, scaledY))
            return;

        byte[] data = MyEncoder_2(scaledX, scaledY, ACTION_MOVE);
        SendData(data);
    }

    public void OnMouseDown(PointerPressedEventArgs e, Canvas my_MainImage)
    {
        var pos = e.GetPosition(my_MainImage);

        double x = pos.X;
        double y = pos.Y;

        var point = e.GetCurrentPoint(my_MainImage);

        if (point.Properties.IsLeftButtonPressed)
        {
            var (scaledX, scaledY) = ScaleCoordinates(x, y);

            // Reset previous coordinates for smoothing
            _previousX = scaledX;
            _previousY = scaledY;

            byte[] data = MyEncoder_2(scaledX, scaledY, ACTION_DOWN);
            SendData(data);
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            // Console.WriteLine($"Right mouse button PRESSED at ({x:F1}, {y:F1})");
        }
    }

    public void OnMouseUp(PointerReleasedEventArgs e, Canvas my_MainImage)
    {
        var pos = e.GetPosition(my_MainImage);

        double x = pos.X;
        double y = pos.Y;

        // On release, the "Released" button is in e.InitialPressMouseButton
        // (Avalonia doesn't expose a "Released" flag like WPF does).
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            var (scaledX, scaledY) = ScaleCoordinates(x, y);

            var deviceRes = My_Store.Instance.DeviceResolution;

            if (scaledX > deviceRes.Width || scaledX < 0)
            {
                if (scaledX > deviceRes.Width)
                    scaledX = deviceRes.Width;
                else if (scaledX < 0)
                    scaledX = 0;
            }

            if (scaledY > deviceRes.Height || scaledY < 0)
            {
                if (scaledY > deviceRes.Height)
                    scaledY = deviceRes.Height;
                else if (scaledY < 0)
                    scaledY = 0;
            }

            byte[] data = MyEncoder_2(scaledX, scaledY, ACTION_UP);
            SendData(data);
        }
        else if (e.InitialPressMouseButton == MouseButton.Right)
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

    public void mouse_Wheel(PointerWheelEventArgs e, Canvas my_MainImage)
    {
        var pos = e.GetPosition(my_MainImage);
        var (x, y) = ScaleCoordinates(pos.X, pos.Y);

        // WPF gave delta: +120, -120 on a single axis.
        // Avalonia gives a Vector Delta; wheel is usually on Y.
        float delta = (float)(e.Delta.Y / 120.0);

        float vscroll = delta;  // invert for Android if needed
        float hscroll = 0;      // horizontal scroll unused here

        var data = EncodeScrollEvent(x, y, hscroll, vscroll);
        SendData(data);
    }

    private (int x, int y) ScaleCoordinates(double displayX, double displayY)
    {
        var deviceRes = My_Store.Instance.DeviceResolution;
        var displayRes = My_Store.Instance.DisplayResolution;

        if (displayRes.Width == 0 || displayRes.Height == 0)
        {
            return ((int)displayX, (int)displayY);
        }

        double scaleX = (double)deviceRes.Width / displayRes.Width;
        double scaleY = (double)deviceRes.Height / displayRes.Height;

        int targetX = (int)Math.Round(displayX * scaleX);
        int targetY = (int)Math.Round(displayY * scaleY);

        return (targetX, targetY);
    }

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

            WriteBigEndian(writer, (ulong)Touch_id.normal_mouse);

            WriteBigEndian(writer, (uint)x);
            WriteBigEndian(writer, (uint)y);

            var deviceRes = My_Store.Instance.DeviceResolution;
            WriteBigEndian(writer, (ushort)deviceRes.Width);
            WriteBigEndian(writer, (ushort)deviceRes.Height);

            ushort pressureEncoded = (ushort)(pressure * 0xFFFF);
            WriteBigEndian(writer, pressureEncoded);

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

            // 5) buttons pressed
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