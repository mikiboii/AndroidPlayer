

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Androidplayer.Src.Android;
using Androidplayer.Src.Keymap;
using Androidplayer.Store;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Androidplayer.Src.Keyboard
{
    public class Gaming_Keyboard
    {
        private HashSet<string> activeKeys = new HashSet<string>();
        
        private HashSet<string> current_combo_Keys = new HashSet<string>();
        
       
        private HashSet<string> current_single_Keys = new HashSet<string>();
        
        private List<string> directionKeys = new List<string>();
        
        private bool Direction_Pressed = false;
        
        
        
        
        
        // Get direction properties from store instead of hardcoding
        
     
        // private readonly HashSet<string> handledCombos = new(); // prevent double-triggers

   
        
        
        
        // private double directionX = 312;  // x + width/2
        // private double directionY = 534;  // y + height/2
        // private double directionWidth = 89;
        // private double directionHeight = 89;
        //
        
        private double directionX = 0;  // x + width/2
        private double directionY = 0;  // y + height/2
        private double directionWidth = 0;
        private double directionHeight = 0;
        
        
        
        
        
        
        private double D_x = 212;
        private double D_Y = 36;
        
        
        private const byte ACTION_MOVE = 0x02;
        private const byte ACTION_DOWN = 0x00;
        private const byte ACTION_UP = 0x01;

        public Gaming_Keyboard()
        {
            // Initialize direction properties from store
            // var directionProps = My_Store.Instance.GetKeyMap("Direction");
            // if (directionProps != null)
            // {
            //     directionX = directionProps.X + (directionProps.Width / 2);
            //     directionY = directionProps.Y + (directionProps.Height / 2);
            //     directionWidth = directionProps.Width;
            //     directionHeight = directionProps.Height;
            //     
            //     // Initialize to center (exactly like Python)
            //     D_x = directionX;
            //     D_Y = directionY;
            // }
        }

        
     

       
        public void Key_pressed(UIElement source, WpfKeyEventArgs e)
        
        {
            Key actualKey = (e.Key == Key.System) ? e.SystemKey : e.Key;
            string keyStr = ConvertKeyToString(actualKey);
            
            if (!directionKeys.Contains(keyStr))
            {
                directionKeys.Add(keyStr);
            }
            if (activeKeys.Contains(keyStr))
            {
                return;
            }
            activeKeys.Add(keyStr);
            
            Direction_Press(e);



            if (!e.IsRepeat)
            {
                
                
                
                var demo_key = KeyMapManager.Instance?.Getmultikey_ElementsByKey(keyStr);
                
                Console.WriteLine($"length of the multi key {demo_key.Count}");

                foreach (var item in demo_key)
                {

                    // Console.WriteLine(item.Type);
                    
                    

                    if (activeKeys.Contains(item.Keys[0]))
                    {
                        if (item.Keys[1] == keyStr)
                        {


                            current_combo_Keys.Add(item.Keys[1]);
                            
                            Console.WriteLine($" pressed : {item.Type} : {item.Keys[0]} : {item.Keys[1]}");
                            
                            if (item.Type == "Visual")
                            {
                                my_info.Instance.Toggle_mouseLock();
                            
                                continue;
                            
                            }
                            
                            var x = (int)item.X + ((int)item.ScaledWidth / 2);
                            var y = (int)item.Y + ((int)item.ScaledHeight / 2);
                        
                        
                            byte[] data = MyEncoder(x, y, ACTION_DOWN);
                            SendData(data);
                            
                            
                            // Console.WriteLine($"{item.Keys[0]} : {item.Keys[1]}");
                        }
                    }
                    
                }
                
                
                
                
                var single_key = KeyMapManager.Instance?.GetElementsByKey(keyStr);
                
                if (single_key != null && single_key.Count != 0 )
                {
                    // Console.WriteLine($"length of the single key {single_key.Count}");

                    if (!current_combo_Keys.Contains(single_key[0].Keys[0]))
                    {
                        current_single_Keys.Add(keyStr);
                        
                        
                        Console.WriteLine($" pressed single key  : {single_key[0].Keys[0]}");

                        if (single_key[0].Type == "Visual")
                        {
                            my_info.Instance.Toggle_mouseLock();
                            
                            return;
                            
                        }
                        
                        
                        
                        var x = (int)single_key[0].X + ((int)single_key[0].ScaledWidth / 2);
                        var y = (int)single_key[0].Y + ((int)single_key[0].ScaledHeight / 2);
                        
                        
                        byte[] data = MyEncoder(x, y, ACTION_DOWN);
                        SendData(data);
                        
                        
                    }
                        
                }
                
                
                
               
               

                    e.Handled = true;

            }
        }

        public void Key_Released(UIElement source, WpfKeyEventArgs e)
        {
            Key actualKey = (e.Key == Key.System) ? e.SystemKey : e.Key;
            string keyStr = ConvertKeyToString(actualKey);
            if (!string.IsNullOrEmpty(keyStr))
            {
                if (directionKeys.Contains(keyStr))
                {
                    directionKeys.Remove(keyStr);
                }
                
                if (activeKeys.Contains(keyStr))
                {
                    activeKeys.Remove(keyStr);
                }

                // Console.WriteLine(activeKeys);
                
                
                
                Direction_Release(e);
                
                
                
                
                
                
                
                // string keyStr = ConvertKeyToString(e.Key);

                if (string.IsNullOrEmpty(keyStr))
                    return;

                // activeKeys.Remove(keyStr);

                // remove combos that include this key
                // handledCombos.RemoveWhere(c => c.Contains(keyStr));
                
                
                
                
                
                
                
                
                
                if (!e.IsRepeat)
                {
                  
                    
                    
                    var demo_key = KeyMapManager.Instance?.Getmultikey_ElementsByKey(keyStr);
                
                    // Console.WriteLine($"length of the multi key 
                    
                    var should_return = false;
                    
                    
                    

                    foreach (var item in demo_key)
                    {

                        // Console.WriteLine(item.Type);
                    
                        
                    

                        if (activeKeys.Contains(item.Keys[0]))
                        {
                            if (item.Keys[1] == keyStr)
                            {
                            
                                // current_combo_Keys.Remove(item.Keys[1]);

                                // just_released.Add(keyStr);
                                
                                
                                if (current_single_Keys.Contains(item.Keys[1]))
                                {
                                    Console.WriteLine("#2 key found");
                                    continue;
                                    
                                    
                                }
                                
                            
                                should_return = true;
                                
                                
                                Console.WriteLine($" released : {item.Type} : {item.Keys[0]} : {item.Keys[1]}");
                                
                                var x = (int)item.X + ((int)item.ScaledWidth / 2);
                                var y = (int)item.Y + ((int)item.ScaledHeight / 2);
                                
                                byte[] data = MyEncoder(x, y, ACTION_UP);
                                SendData(data);
                                // Console.WriteLine($"{item.Keys[0]} : {item.Keys[1]}");
                            }
                        }
                        else if (activeKeys.Contains(item.Keys[1]))
                        {
                            
                            if (item.Keys[0] == keyStr)
                            {

                                if (current_single_Keys.Contains(item.Keys[1]))
                                {
                                    Console.WriteLine("#1 key found");
                                    continue;
                                    
                                    Console.WriteLine("#1 did not return");
                                }
                                
                                
                                
                                should_return = true;



                                // activeKeys.Remove(item.Keys[1]);
                                
                                Console.WriteLine($" released : {item.Type} : {item.Keys[0]} : {item.Keys[1]}");
                                
                                var x = (int)item.X + ((int)item.ScaledWidth / 2);
                                var y = (int)item.Y + ((int)item.ScaledHeight / 2);
                                
                                byte[] data = MyEncoder(x, y, ACTION_UP);
                                SendData(data);
                            }
                        }
                        
                        
                        if (!activeKeys.Contains(item.Keys[1]) &&  !activeKeys.Contains(item.Keys[0]))
                        {

                            if (current_combo_Keys.Contains(item.Keys[1]))
                            {
                                current_combo_Keys.Remove(item.Keys[1]);
                                
                                should_return = true;
                            }
                            
                                
                                
                                // Console.WriteLine($"All keys: {string.Join(", ", activeKeys)}, ");
                                //
                                // Console.WriteLine($" the current item key : {item.Keys[0] } : {item.Keys[1]}");
                                //
                                // Console.WriteLine("removed current_combo_Keys from multi release ");
                                //
                                
                                
                                
                                
                                
                            // if (item.Keys[0] == keyStr)
                            // {
                            //
                            //
                            //     
                            // }
                            
                        }
                        
                    
                    }


                    // Console.WriteLine($"sould return is = {should_return}");

                    if (should_return)
                    {
                        return;
                    }


                    // if (just_released.Contains(keyStr))
                    // {
                    //     just_released.Remove(keyStr);
                    //     return;
                    // }
                    
                     
                    var single_key = KeyMapManager.Instance?.GetElementsByKey(keyStr);
                
                    if (single_key != null && single_key.Count != 0 )
                    {
                        // Console.WriteLine($"length of the single key {single_key.Count}");

                        if (!current_combo_Keys.Contains(single_key[0].Keys[0]))
                        {

                            current_single_Keys.Remove(keyStr);
                        
                            Console.WriteLine($" released single key  : {single_key[0].Keys[0]}");
                            
                            // Console.WriteLine($" released : {item.Type} : {item.Keys[0]} : {item.Keys[1]}");

                            var x = (int)single_key[0].X + ((int)single_key[0].ScaledWidth / 2);
                            var y = (int)single_key[0].Y + ((int)single_key[0].ScaledHeight / 2);
                        
                            byte[] data = MyEncoder(x, y, ACTION_UP);
                            SendData(data);
                            
                        }
                        
                    }
                    
                 
                    

                    

                   
                    
                }
            }
        }


        #region Direcion_area

        

       
        
        private void Direction_Press(WpfKeyEventArgs eventParam)
        {
            string keyStr = ConvertKeyToString(eventParam.Key);
            
            // Exact Python logic conversion
            List<string> letters = new List<string> { "W", "A", "S", "D" };

            var demo_key = KeyMapManager.Instance?.GetElementsByType("Direction");

       
            if (demo_key == null || demo_key.Count == 0)
            {
                
                return;
            }

            if (directionWidth == 0)
            {
                
                directionWidth = demo_key[0].ScaledWidth;

                directionHeight = demo_key[0].ScaledHeight;

                directionX = demo_key[0].X;
                directionY = demo_key[0].Y;
                
                
            }

            
            
            
            if (letters.Contains(keyStr))
            {
                if (!directionKeys.Contains(keyStr))
                {
                    Console.WriteLine($"single direction press: {keyStr} ");
                    
                    if (keyStr == "A")
                    {
                        if (D_x != directionX - ((directionWidth / 2) / 2))
                        {
                            D_x = directionX - ((directionWidth / 2) / 2);
                        }
                    }
                }
                
                if (directionKeys.Contains(keyStr))
                {
                    if (Direction_Pressed == false)
                    {
                        Console.WriteLine("direction set to True");   
                        
                        // EXACTLY like Python: send ACTION_DOWN to center position
                        byte[] data = My_D_Encoder((int)directionX, (int)directionY, ACTION_DOWN);
                        SendData(data);
                        
                        Direction_Pressed = true;    
                    }
                    
                    // Check all combinations exactly like Python
                    if (new HashSet<string> { "A", "W" }.IsSubsetOf(directionKeys) && Direction_Pressed == true)
                    {
                        double new_x = directionX - (directionWidth / 2);
                        double new_y = directionY - (directionHeight / 2);
                        
                        if (D_x != new_x || D_Y != new_y)
                        {
                            Console.WriteLine("A and W pressed");
                            D_x = new_x;
                            D_Y = new_y;
                            
                            byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_MOVE);
                            SendData(data);
                        }
                    }
                    else if (new HashSet<string> { "W", "D" }.IsSubsetOf(directionKeys) && Direction_Pressed == true)
                    {
                        double new_x = directionX + (directionWidth / 2);
                        double new_y = directionY - (directionHeight / 2);
                        
                        if (D_x != new_x || D_Y != new_y)
                        {
                            Console.WriteLine("W and D pressed");
                            D_x = new_x;
                            D_Y = new_y;
                            
                            byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_MOVE);
                            SendData(data);
                        }
                    }
                    else if (new HashSet<string> { "S", "D" }.IsSubsetOf(directionKeys) && Direction_Pressed == true)
                    {
                        double new_x = directionX + (directionWidth / 2);
                        double new_y = directionY + (directionHeight / 2);
                        
                        if (D_x != new_x || D_Y != new_y)
                        {
                            Console.WriteLine("S and D pressed");    
                            D_x = new_x;
                            D_Y = new_y;
                            
                            byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_MOVE);
                            SendData(data);
                        }
                    }
                    else if (new HashSet<string> { "A", "S" }.IsSubsetOf(directionKeys) && Direction_Pressed == true)
                    {
                        double new_x = directionX - (directionWidth / 2);
                        double new_y = directionY + (directionHeight / 2);
                        
                        if (D_x != new_x || D_Y != new_y)
                        {
                            Console.WriteLine("A and S pressed");
                            D_x = new_x;
                            D_Y = new_y;
                            
                            byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_MOVE);
                            SendData(data);
                        }
                    }
                    
                    // Single key checks - EXACT Python logic
                    bool A_pressed = new HashSet<string> { "W", "S", "D" }.Any(x => directionKeys.Contains(x));
                    
                    if (!A_pressed && directionKeys.Contains("A") && Direction_Pressed == true)
                    {
                        double new_x = directionX - (directionWidth / 2);
                        double new_y = directionY;
                        
                        if (D_x != new_x || D_Y != new_y)
                        {
                            Console.WriteLine("A pressed");        
                            D_x = new_x;
                            D_Y = new_y;
                            
                            byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_MOVE);
                            SendData(data);
                        }
                    }
                    
                    bool W_pressed = new HashSet<string> { "A", "S", "D" }.Any(x => directionKeys.Contains(x));
                    
                    if (!W_pressed && directionKeys.Contains("W") && Direction_Pressed == true)
                    {
                        double new_x = directionX;
                        double new_y = directionY - (directionHeight / 2);
                        
                        if (D_x != new_x || D_Y != new_y)
                        {
                            Console.WriteLine("W pressed __");        
                            D_x = new_x;
                            D_Y = new_y;
                            
                            byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_MOVE);
                            SendData(data);
                        }
                    }
                    
                    bool S_pressed = new HashSet<string> { "W", "A", "D" }.Any(x => directionKeys.Contains(x));
                    
                    if (!S_pressed && directionKeys.Contains("S") && Direction_Pressed == true)
                    {
                        double new_x = directionX;
                        double new_y = directionY + (directionHeight / 2);
                        
                        if (D_x != new_x || D_Y != new_y)
                        {
                            Console.WriteLine("S pressed");        
                            D_x = new_x;
                            D_Y = new_y;
                            
                            byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_MOVE);
                            SendData(data);
                        }
                    }
                    
                    bool D_pressed = new HashSet<string> { "W", "A", "S" }.Any(x => directionKeys.Contains(x));
                    
                    if (!D_pressed && directionKeys.Contains("D") && Direction_Pressed == true)
                    {
                        double new_x = directionX + (directionWidth / 2);
                        double new_y = directionY;
                        
                        if (D_x != new_x || D_Y != new_y)
                        {
                            Console.WriteLine("D pressed");        
                            D_x = new_x;
                            D_Y = new_y;
                            
                            byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_MOVE);
                            SendData(data);
                        }
                    }
                }
            }
        }

        private void Direction_Release(WpfKeyEventArgs eventParam)
        {
            string keyStr = ConvertKeyToString(eventParam.Key);
            
            bool key_pressed = new HashSet<string> { "A", "W", "S", "D" }.Any(x => directionKeys.Contains(x));
            
             
            
            var demo_key = KeyMapManager.Instance?.GetElementsByType("Direction");
            
            if (demo_key == null || demo_key.Count == 0)
            {
                
                return;
            }
            
            if (directionWidth == 0)
            {
                            
                directionWidth = demo_key[0].ScaledWidth;
            
                directionHeight = demo_key[0].ScaledHeight;
            
                directionX = demo_key[0].X;
                directionY = demo_key[0].Y;
                            
                            
            }
            
            
            
            
            
            if (key_pressed == false)
            {
                if (Direction_Pressed == true)
                {
                    // EXACTLY like Python: send ACTION_UP to current position
                    byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_UP);
                    SendData(data);
                    
                    Direction_Pressed = false;
                    Console.WriteLine("all direction released");
                }
                
                D_x = directionX;
                D_Y = directionY;
            }
            
            // Re-check all combinations after release - EXACT Python logic
            if (new HashSet<string> { "A", "W" }.IsSubsetOf(directionKeys) && Direction_Pressed == true)
            {
                double new_x = directionX - (directionWidth / 2);
                double new_y = directionY - (directionHeight / 2);
                
                if (D_x != new_x || D_Y != new_y)
                {
                    Console.WriteLine("A and W pressed");
                    D_x = new_x;
                    D_Y = new_y;
                    
                    byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_MOVE);
                    SendData(data);
                }
            }
            else if (new HashSet<string> { "W", "D" }.IsSubsetOf(directionKeys) && Direction_Pressed == true)
            {
                double new_x = directionX + (directionWidth / 2);
                double new_y = directionY - (directionHeight / 2);
                
                if (D_x != new_x || D_Y != new_y)
                {
                    Console.WriteLine("W and D pressed");
                    D_x = new_x;
                    D_Y = new_y;
                    
                    byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_MOVE);
                    SendData(data);
                }
            }
            else if (new HashSet<string> { "S", "D" }.IsSubsetOf(directionKeys) && Direction_Pressed == true)
            {
                double new_x = directionX + (directionWidth / 2);
                double new_y = directionY + (directionHeight / 2);
                
                if (D_x != new_x || D_Y != new_y)
                {
                    Console.WriteLine("S and D pressed");    
                    D_x = new_x;
                    D_Y = new_y;
                    
                    byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_MOVE);
                    SendData(data);
                }
            }
            else if (new HashSet<string> { "A", "S" }.IsSubsetOf(directionKeys) && Direction_Pressed == true)
            {
                double new_x = directionX - (directionWidth / 2);
                double new_y = directionY + (directionHeight / 2);
                
                if (D_x != new_x || D_Y != new_y)
                {
                    Console.WriteLine("A and S pressed");
                    D_x = new_x;
                    D_Y = new_y;
                    
                    byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_MOVE);
                    SendData(data);
                }
            }
            
            // Single key checks after release - EXACT Python logic
            bool A_pressed = new HashSet<string> { "W", "S", "D" }.Any(x => directionKeys.Contains(x));
            
            if (!A_pressed && directionKeys.Contains("A") && Direction_Pressed == true)
            {
                double new_x = directionX - (directionWidth / 2);
                double new_y = directionY;
                
                if (D_x != new_x || D_Y != new_y)
                {
                    Console.WriteLine("A pressed");        
                    D_x = new_x;
                    D_Y = new_y;
                    
                    byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_MOVE);
                    SendData(data);
                }
            }
            
            bool W_pressed = new HashSet<string> { "A", "S", "D" }.Any(x => directionKeys.Contains(x));
            
            if (!W_pressed && directionKeys.Contains("W") && Direction_Pressed == true)
            {
                double new_x = directionX;
                double new_y = directionY - (directionHeight / 2);
                
                if (D_x != new_x || D_Y != new_y)
                {
                    Console.WriteLine("W pressed");        
                    D_x = new_x;
                    D_Y = new_y;
                    
                    byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_MOVE);
                    SendData(data);
                }
            }
            
            bool S_pressed = new HashSet<string> { "W", "A", "D" }.Any(x => directionKeys.Contains(x));
            
            if (!S_pressed && directionKeys.Contains("S") && Direction_Pressed == true)
            {
                double new_x = directionX;
                double new_y = directionY + (directionHeight / 2);
                
                if (D_x != new_x || D_Y != new_y)
                {
                    Console.WriteLine("S pressed");        
                    D_x = new_x;
                    D_Y = new_y;
                    
                    byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_MOVE);
                    SendData(data);
                }
            }
            
            bool D_pressed = new HashSet<string> { "W", "A", "S" }.Any(x => directionKeys.Contains(x));
            
            if (!D_pressed && directionKeys.Contains("D") && Direction_Pressed == true)
            {
                double new_x = directionX + (directionWidth / 2);
                double new_y = directionY;
                
                if (D_x != new_x || D_Y != new_y)
                {
                    Console.WriteLine("D pressed");        
                    D_x = new_x;
                    D_Y = new_y;
                    
                    byte[] data = My_D_Encoder((int)D_x, (int)D_Y, ACTION_MOVE);
                    SendData(data);
                }
            }
        }

        
        
        
        #endregion
        
        
        private byte[] My_D_Encoder(int x, int y, byte action, float pressure = 1.0f)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)0x02); // Message type
        
                writer.Write(action); // Action
        
                // Pointer ID (64-bit big-endian)
                WriteBigEndian(writer, (ulong)Touch_id.direction);
        
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
        
       
        
        private byte[] ToBigEndian(byte[] data)
        {
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            return data;
        }
        
        
        
        
        
        private byte[] MyEncoder(int x, int y, byte action, float pressure = 1.0f)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)0x02); // Message type
        
                writer.Write(action); // Action
        
                // Pointer ID (64-bit big-endian)
                WriteBigEndian(writer, (ulong)Touch_id.normal_press );
        
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
        
        
        
        private void SendData(byte[] data)
        {
            TcpClient controlSocket = My_Store.Instance.ControlSocket;

            // Console.WriteLine("from gaming keyboard");
        
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
                    Console.WriteLine($"Error sending direction data: {ex.Message}");
                }
            }
        }

        // Your existing ConvertKeyToString method (keep as is)
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
    }
}