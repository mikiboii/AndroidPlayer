

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Androidplayer.Src.Android;
using Androidplayer.Src.Keymap;
using Androidplayer.Store;

namespace Androidplayer.Src.Mouse;

// Improved ExponentialSmoother based on the actual movement patterns


public class Gaming_mouse
{
    // ... your existing constants and fields remain the same ...
    private const byte ACTION_MOVE = 0x02;
    private const byte ACTION_DOWN = 0x00;
    private const byte ACTION_UP = 0x01;

    private int? _previousX = null;
    private int? _previousY = null;
    private int? old_x = null;
    private int? old_y = null;
    
    // private int center_x = 641;
    // private int center_y = 198;
    // private int android_x = 641;
    // private int android_y = 198;
    
    
    private int center_x = 967;
    private int center_y = 201;
    private int android_x = 967;
    private int android_y = 201;
         
         
             // 967, 201
    
    
    private int mainX;
    private int mainY;
    private int xMoved;
    private int yMoved;
    private Stopwatch stopwatch = null;
    private long last_time = 0;

    // Improved timing and smoothing
   
    private Stopwatch frameTimer = Stopwatch.StartNew();
    private long lastFrameTime = 0;
    private const long TARGET_FRAME_TIME_MS = 16; // ~60Hz matching the data

    public Gaming_mouse()
    {
        my_info.Instance.PropertyChanged += OnMouseStoreChanged;
    }

    private void OnMouseStoreChanged(object sender, PropertyChangedEventArgs e)
    {
        // ... your existing code remains the same ...
        if (e.PropertyName == nameof(my_info.IsMouseLocked))
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                bool isLocked = my_info.Instance.IsMouseLocked;
                // Console.WriteLine($"Gaming_mouse: Mouse lock changed to {isLocked}");
                
                if (my_info.Instance.IsMouseLocked)
                {
                    PressMouse_demo();
                }
                else 
                {
                    UnPressMouse_demo();
                }
            });
        }
    }

    // Your existing methods remain the same until mouse_Move...
    public void mouse_press(int num)
    {

        if (num == 1)
        {
            
            // Console.WriteLine("left pressed");
            
            
            var fire = KeyMapManager.Instance?.GetElement("Fire");
            if (fire != null)
            {
                Console.WriteLine($"🔥 Fire: X={fire.X}, Y={fire.Y}");
            
            var x = (int)fire.X + ((int)fire.ScaledWidth / 2);
            var y = (int)fire.Y + ((int)fire.ScaledHeight / 2);
            
            byte[] data = MyEncoder_click(x, y, ACTION_DOWN, (ulong)Touch_id.fire);
            SendData(data);
            
            }
        }
        else
        {
            
            // Console.WriteLine("right pressed");
            
            var mouse_right = KeyMapManager.Instance?.GetElement("Mouse right");
            if (mouse_right != null)
            {
               

                var x = (int)mouse_right.X + ((int)mouse_right.ScaledWidth / 2);
                var y = (int)mouse_right.Y + ((int)mouse_right.ScaledHeight / 2);


                byte[] data = MyEncoder_click(x, y, ACTION_DOWN, (ulong)Touch_id.Mouse_right);
                SendData(data);
            }
            
            // byte[] data = MyEncoder_click(1502, 390, ACTION_DOWN);
            // SendData(data);
            
        }
        
        
    }
    
    
    public void mouse_release(int num)
    {

        if (num == 2)
        {

            Console.WriteLine("left released");
            
            var fire = KeyMapManager.Instance?.GetElement("Fire");
            if (fire != null)
            {
                

                var x = (int)fire.X + ((int)fire.ScaledWidth / 2);
                var y = (int)fire.Y + ((int)fire.ScaledHeight / 2);


                byte[] data = MyEncoder_click(x, y, ACTION_UP, (ulong)Touch_id.fire);
                SendData(data);
            }


        }
        else
        {
            // Mouse right
            // Console.WriteLine("right released");
            
            
            var mouse_right = KeyMapManager.Instance?.GetElement("Mouse right");
            if (mouse_right != null)
            {
               

                var x = (int)mouse_right.X + ((int)mouse_right.ScaledWidth / 2);
                var y = (int)mouse_right.Y + ((int)mouse_right.ScaledHeight / 2);


                byte[] data = MyEncoder_click(x, y, ACTION_UP, (ulong)Touch_id.Mouse_right);
                SendData(data);
            }
            
        }
        
        
    }

    public void mouse_Move(double dx, double dy)
    {
        // // Frame rate limiting to match the data timing
        // long currentTime = frameTimer.ElapsedMilliseconds;
        // long timeSinceLastFrame = currentTime - lastFrameTime;
        //
        // if (timeSinceLastFrame < TARGET_FRAME_TIME_MS)
        // {
        //     return; // Skip frame to maintain consistent timing
        // }
        // lastFrameTime = currentTime;

        // Your existing timing logic for mouse press reset
        if (stopwatch != null)
        {
            long time_passed = stopwatch.ElapsedMilliseconds - last_time;
            if (time_passed > 1000)
            {
                reset_mousepress();
                Console.WriteLine($"1 second passed reseting game mouse");
            }

            if (stopwatch.ElapsedMilliseconds > 4000)
            {
                reset_mousepress();
                stopwatch.Restart();
            }
            last_time = stopwatch.ElapsedMilliseconds;
        }
        else
        {
            stopwatch = Stopwatch.StartNew();
            last_time = stopwatch.ElapsedMilliseconds;
        }


        // Process with sensitivity - using the smoothed values
        int sensitivity = 6; // Slightly reduced for more natural movement
        int scaledDx = ProcessSingleAxis((int)dx, sensitivity);
        int scaledDy = ProcessSingleAxis((int)dy, sensitivity);

        // REST OF YOUR EXISTING CODE FOR BOUNDARY CHECKS AND MOVEMENT
        // ... continues exactly as you have it ...
        
        if (old_x == null)
        {
            old_x = android_x;
            old_y = android_y;
        }
        
        int? x = old_x + scaledDx;
        int? y = old_y + scaledDy;
        
        // Your existing boundary checks remain exactly the same...
        var deviceRes = My_Store.Instance.DeviceResolution;
        int source_width = deviceRes.Width;
        int source_height = deviceRes.Height;

        int boundary_width = (int)(0.9 * source_width);
        int boundary_height = (int)(0.9 * source_height);

        int left = (android_x - boundary_width) + 5;
        int top = (android_y - boundary_height) + 5;
        int right = (android_x + boundary_width) - 5;
        int bottom = (android_y + boundary_height) - 5;

        if (top < 0) top = 5;
        if (bottom > source_height) bottom = source_height;
        if (left < 0) left = 5;
        if (right > source_width) right = source_width;

        // Your boundary checking logic continues exactly as before...
        if (left > x || x > right)
        {
            if (left > x)
                mainX = left;
            if (x > right)
                mainX = right;
                
            byte[] data = MyEncoder(mainX, mainY, ACTION_UP);
            SendData(data);
            PressMouse_demo();
            old_x = android_x;
            old_y = android_y;
            return;
        }
        
        if (top > y || y > bottom)
        {
            if (top > y)
                mainY = top;
            if (y > bottom)
                mainY = bottom;
                
            byte[] data = MyEncoder(mainX, mainY, ACTION_UP);
            SendData(data);
            PressMouse_demo();
            old_x = android_x;
            old_y = android_y;
            return;
        }
        
        old_x = x;
        old_y = y;
        mainX = x.Value;
        mainY = y.Value;
        
        if (x > source_width || x < 0 || y > source_height || y < 0)
            return;
            
        byte[] moveData = MyEncoder(mainX, mainY, ACTION_MOVE);
        SendData(moveData);
    }

    // Your existing ProcessSingleAxis method remains exactly the same
    public int ProcessSingleAxis(int offset, float speed)
    {
        float coefficient = 1.5f;

        if (Math.Abs(offset) > 1)
        {
            float num = coefficient / speed;
            int num2 = (int)Math.Round(offset * num);
        
            if (Math.Abs(num2) < 1)
            {
                if (offset < 0)
                    return -1;
                else
                    return 1;
            }
            else
            {
                return num2;
            }
        }
        return offset;
    }







    #region mousewheel


    public void mousewheel_up()
    {
        var wheel_up = KeyMapManager.Instance?.GetElement("Mouse up");
        if (wheel_up != null)
        {
           

            var x = (int)wheel_up.X + ((int)wheel_up.ScaledWidth / 2);
            var y = (int)wheel_up.Y + ((int)wheel_up.ScaledHeight / 2);

            byte[] data = MyEncoder_click(x, y, ACTION_DOWN, (ulong)Touch_id.mousewheel);
            SendData(data);
            
            // Thread.Sleep(100);
            
            byte[] data2 = MyEncoder_click(x, y, ACTION_UP, (ulong)Touch_id.mousewheel);
            SendData(data2);

        }

    }
    
    
    public void mousewheel_down()
    {
        var wheel_down = KeyMapManager.Instance?.GetElement("Mouse down");
        if (wheel_down != null)
        {
           

            var x = (int)wheel_down.X + ((int)wheel_down.ScaledWidth / 2);
            var y = (int)wheel_down.Y + ((int)wheel_down.ScaledHeight / 2);

            byte[] data = MyEncoder_click(x, y, ACTION_DOWN, (ulong)Touch_id.mousewheel);
            SendData(data);
            
            // Thread.Sleep(33);
            
            byte[] data2 = MyEncoder_click(x, y, ACTION_UP, (ulong)Touch_id.mousewheel);
            SendData(data2);

        }
            
    }
    

    #endregion
    
    
    
    
    
    // All your other existing methods remain exactly the same...
    private void PressMouse_demo()
    {
        
        var visual = KeyMapManager.Instance?.GetElement("Visual");


        if (visual != null)
        {

            android_x = (int)visual.X;
            android_y = (int)visual.Y;
        }
        
        
        
        byte[] data = MyEncoder(android_x, android_y, ACTION_DOWN);
        SendData(data);
        
        old_x = android_x;
        old_y = android_y;
        
    }

    private void UnPressMouse_demo()
    {
        
        byte[] data = MyEncoder(old_x.Value, old_y.Value, ACTION_UP);
        SendData(data);
        
        old_x = android_x;
        old_y = android_y;
        
    }

    private void reset_mousepress()
    {
        
        byte[] data = MyEncoder(mainX, mainY, ACTION_UP);
        SendData(data);
        PressMouse_demo();
            
            
        old_x = android_x;
        old_y = android_y;
    }
    
      
    
      
    private byte[] MyEncoder(int x, int y, byte action, float pressure = 1.0f)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((byte)0x02); // Message type
        
            writer.Write(action); // Action
        
            // Pointer ID (64-bit big-endian)
            WriteBigEndian(writer, (ulong)Touch_id.mouse_move);
        
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
      
      
    
      
      
      
    private byte[] ToBigEndian(byte[] data)
    {
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(data);
        }
        return data;
    }
    
    
    
    private byte[] MyEncoder_click(int x, int y, byte action, ulong id, float pressure = 1.0f)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((byte)0x02); // Message type
        
            writer.Write(action); // Action
        
            // Pointer ID (64-bit big-endian)
            WriteBigEndian(writer, id);
        
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
    
    
    
    
    
    // private void SendData(byte[] data)
    // {
    //     TcpClient controlSocket = My_Store.Instance.ControlSocket;
    //     
    //     if (controlSocket != null && controlSocket.Connected)
    //     {
    //         try
    //         {
    //             NetworkStream stream = controlSocket.GetStream();
    //             stream.Write(data, 0, data.Length);
    //         }
    //         catch (Exception ex)
    //         {
    //             Console.WriteLine($"Error sending mouse data: {ex.Message}");
    //         }
    //     }
    // }
    
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