using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Androidplayer.Src.Mouse;
using Androidplayer.Store;

namespace Androidplayer.Src.Rawinput
{
    public class handle_rawinput
    {
        private IntPtr rawInputBuffer;
        private HwndSource hwndSource;
        private bool isRawInputRegistered = false;

        private Gaming_mouse gaming_mouse;
        
        // Events for raw input
        public event Action<My_Win32Api.RAWMOUSE> MouseInputReceived;
        public event Action<My_Win32Api.RAWKEYBOARD> KeyboardInputReceived;

        public handle_rawinput(Window window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));
                
            Initialize(window);
            
            gaming_mouse = new Gaming_mouse();
        }

        private void Initialize(Window window)
        {
            // Hook into window events
            window.SourceInitialized += OnWindowSourceInitialized;
            window.Closed += OnWindowClosed;
        }

        private void OnWindowSourceInitialized(object sender, EventArgs e)
        {
            hwndSource = PresentationSource.FromVisual((Window)sender) as HwndSource;
            if (hwndSource != null)
            {
                hwndSource.AddHook(WndProc);
                RegisterRawInput();
            }
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            UnregisterRawInput();
        }

        private bool RegisterRawInput()
        {
            try
            {
                My_Win32Api.RAWINPUTDEVICE[] devices = new My_Win32Api.RAWINPUTDEVICE[2];
                
                // Register for keyboard input
                devices[0] = new My_Win32Api.RAWINPUTDEVICE
                {
                    usUsagePage = 0x01, // Generic Desktop
                    usUsage = 0x06,     // Keyboard
                    dwFlags = 0x100,    // RIDEV_INPUTSINK - receive input even when not in foreground
                    hwndTarget = hwndSource.Handle
                };
                
                // Register for mouse input
                devices[1] = new My_Win32Api.RAWINPUTDEVICE
                {
                    usUsagePage = 0x01, // Generic Desktop
                    usUsage = 0x02,     // Mouse
                    dwFlags = 0x100,    // RIDEV_INPUTSINK - receive input even when not in foreground
                    hwndTarget = hwndSource.Handle
                };
                
                isRawInputRegistered = My_Win32Api.RegisterRawInputDevices(
                    devices, (uint)devices.Length, 
                    (uint)Marshal.SizeOf(typeof(My_Win32Api.RAWINPUTDEVICE)));
                
                // Allocate buffer for raw input data
                if (isRawInputRegistered && rawInputBuffer == IntPtr.Zero)
                {
                    rawInputBuffer = Marshal.AllocHGlobal(1024); // 1KB buffer
                    Console.WriteLine("Raw input registered successfully");
                }
                
                return isRawInputRegistered;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to register raw input: {ex.Message}");
                return false;
            }
        }

        private void UnregisterRawInput()
        {
            if (isRawInputRegistered)
            {
                try
                {
                    My_Win32Api.RAWINPUTDEVICE[] devices = new My_Win32Api.RAWINPUTDEVICE[2];
                    
                    // Unregister keyboard
                    devices[0] = new My_Win32Api.RAWINPUTDEVICE
                    {
                        usUsagePage = 0x01,
                        usUsage = 0x06,
                        dwFlags = 0x00000001, // RIDEV_REMOVE - remove the device
                        hwndTarget = IntPtr.Zero
                    };
                    
                    // Unregister mouse
                    devices[1] = new My_Win32Api.RAWINPUTDEVICE
                    {
                        usUsagePage = 0x01,
                        usUsage = 0x02,
                        dwFlags = 0x00000001, // RIDEV_REMOVE - remove the device
                        hwndTarget = IntPtr.Zero
                    };
                    
                    My_Win32Api.RegisterRawInputDevices(devices, (uint)devices.Length, 
                        (uint)Marshal.SizeOf(typeof(My_Win32Api.RAWINPUTDEVICE)));
                    
                    isRawInputRegistered = false;
                    
                    if (rawInputBuffer != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(rawInputBuffer);
                        rawInputBuffer = IntPtr.Zero;
                    }
                    
                    Console.WriteLine("Raw input unregistered");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to unregister raw input: {ex.Message}");
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_INPUT = 0x00FF;
            
            if (msg == WM_INPUT)
            {
                uint size = 0;
                
                // First call to get required buffer size
                uint result = My_Win32Api.GetRawInputData(lParam, My_Win32Api.RID_INPUT, 
                    IntPtr.Zero, ref size, (uint)Marshal.SizeOf(typeof(My_Win32Api.RAWINPUTHEADER)));
                
                if (result == 0 && size > 0 && rawInputBuffer != IntPtr.Zero && size <= 1024)
                {
                    // Get the actual raw input data
                    result = My_Win32Api.GetRawInputData(lParam, My_Win32Api.RID_INPUT, 
                        rawInputBuffer, ref size, (uint)Marshal.SizeOf(typeof(My_Win32Api.RAWINPUTHEADER)));
                    
                    if (result == size)
                    {
                        // Parse the raw input data
                        My_Win32Api.RAWINPUT rawInput = (My_Win32Api.RAWINPUT)Marshal.PtrToStructure(
                            rawInputBuffer, typeof(My_Win32Api.RAWINPUT));
                        
                        ProcessRawInput(rawInput);
                    }
                }
                
                handled = true;
                return IntPtr.Zero;
            }
            
            return IntPtr.Zero;
        }

        private void ProcessRawInput(My_Win32Api.RAWINPUT rawInput)
        {
            switch (rawInput.header.dwType)
            {
                case My_Win32Api.RIM_TYPEMOUSE:
                    // MouseInputReceived?.Invoke(rawInput.mouse);
                    ProcessMouseInput(rawInput.mouse);
                    break;
                    
                case My_Win32Api.RIM_TYPEKEYBOARD:
                    // ProcessKeyboardInput(rawInput.keyboard);
                    break;
            }
        }
        
        
        private void ProcessMouseInput(My_Win32Api.RAWMOUSE mouse)
        {
            // This gives you raw mouse delta without acceleration
            int deltaX = mouse.lLastX;
            int deltaY = mouse.lLastY;
            
            
            
            // Check if this is relative movement (not absolute)
            bool isRelativeMovement = (mouse.usFlags & 0x01) == 0; // MOUSE_MOVE_RELATIVE
            
            if (isRelativeMovement && (deltaX != 0 || deltaY != 0))
            {
                // Process the raw mouse movement

                if (my_info.Instance.IsMouseLocked)
                {
                    // Console.WriteLine($"Raw Mouse Delta: X={deltaX}, Y={deltaY}");
                    
                    gaming_mouse.mouse_Move(deltaX, deltaY);
                }


                
            }

            if (mouse.ulButtons != 0)
            {
                if (my_info.Instance.IsMouseLocked)
                {
                    
                    // Console.WriteLine(mouse.ulButtons);


                    if (mouse.ulButtons == 1 || mouse.ulButtons == 4)
                    {
                        gaming_mouse.mouse_press((int)mouse.ulButtons);
                    }
                    else
                    {
                        gaming_mouse.mouse_release((int)mouse.ulButtons);
                    }


                    // Console.WriteLine(mouse.ulExtraInformation);

                    if (mouse.ulButtons == 4287104000)
                    {
                        // Console.WriteLine("mouse down");
                        
                        gaming_mouse.mousewheel_down();
                        
                    }

                    else if (mouse.ulButtons == 7865344)
                    {
                        
                        // Console.WriteLine("mouse up");
                        gaming_mouse.mousewheel_up();
                    }
                    
                    
                }
                
            }

           
        }
        
        
        private void ProcessKeyboardInput(My_Win32Api.RAWKEYBOARD keyboard)
        {
            ushort vkCode = keyboard.VKey;
            uint message = keyboard.Message;
            
            bool isKeyDown = false;
            bool isKeyUp = false;
    
            switch (message)
            {
                case My_Win32Api.WM_KEYDOWN:
                case My_Win32Api.WM_SYSKEYDOWN: // System key down (like Alt key combinations)
                    isKeyDown = true;
                    break;
            
                case My_Win32Api.WM_KEYUP:
                case My_Win32Api.WM_SYSKEYUP: // System key up
                    isKeyUp = true;
                    break;
            }
            // Console.WriteLine($"Key {message}");
            
            if (isKeyDown)
            {
                Console.WriteLine($"Key DOWN: VirtualKey={vkCode} (0x{vkCode:X})");
        
                // You can add specific key handling here
                
            }
            else if (isKeyUp)
            {
                Console.WriteLine($"Key UP: VirtualKey={vkCode} (0x{vkCode:X})");
        
                // Handle key release if needed
               
            }
            
            
            // Log keyboard activity for debugging
            string keyAction = message == My_Win32Api.WM_KEYDOWN ? "DOWN" : "UP";
            
        }

        // Helper method to manually trigger cleanup if needed
        public void Dispose()
        {
            UnregisterRawInput();
        }
    }
}