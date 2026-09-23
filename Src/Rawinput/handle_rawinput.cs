using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using Androidplayer.Src.Mouse;
using Androidplayer.Store;

namespace Androidplayer.Src.Rawinput
{
    public class handle_rawinput : IDisposable
    {
        // --- Win32 constants ---
        private const int GWLP_WNDPROC = -4;
        private const int WM_INPUT = 0x00FF;

        // --- P/Invoke for subclassing ---
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd,
            uint msg, IntPtr wParam, IntPtr lParam);

        // 64-bit vs 32-bit helper
        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
            => IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
            => IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : new IntPtr(GetWindowLong32(hWnd, nIndex));

        // --- Delegate for WndProc ---
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // CRITICAL: keep a strong reference so the GC doesn't collect the delegate
        private WndProcDelegate _wndProcDelegate;
        private IntPtr _oldWndProc = IntPtr.Zero;
        private IntPtr _hwnd = IntPtr.Zero;

        private IntPtr rawInputBuffer;
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
            // Avalonia has no SourceInitialized. Hook Opened, then grab the HWND.
            window.Opened += OnWindowOpened;
            window.Closed += OnWindowClosed;
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            var window = (Window)sender!;

            // Get the native HWND from Avalonia's platform handle.
            var handle = window.TryGetPlatformHandle();
            if (handle == null)
            {
                Console.WriteLine("Could not get platform handle (not on Windows?)");
                return;
            }

            _hwnd = handle.Handle;

            // Install our WndProc by subclassing the window.
            // (This is what HwndSource.AddHook did internally in WPF.)
            _wndProcDelegate = new WndProcDelegate(WndProc);

            _oldWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC,
                Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

            if (_oldWndProc == IntPtr.Zero)
            {
                Console.WriteLine($"SetWindowLongPtr failed: {Marshal.GetLastWin32Error()}");
            }

            RegisterRawInput();
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            UnregisterRawInput();

            // Restore the original WndProc
            if (_oldWndProc != IntPtr.Zero && _hwnd != IntPtr.Zero)
            {
                SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _oldWndProc);
                _oldWndProc = IntPtr.Zero;
            }
        }

        private bool RegisterRawInput()
        {
            try
            {
                My_Win32Api.RAWINPUTDEVICE[] devices = new My_Win32Api.RAWINPUTDEVICE[2];

                devices[0] = new My_Win32Api.RAWINPUTDEVICE
                {
                    usUsagePage = 0x01, // Generic Desktop
                    usUsage = 0x06,     // Keyboard
                    dwFlags = 0x100,    // RIDEV_INPUTSINK
                    hwndTarget = _hwnd
                };

                devices[1] = new My_Win32Api.RAWINPUTDEVICE
                {
                    usUsagePage = 0x01, // Generic Desktop
                    usUsage = 0x02,     // Mouse
                    dwFlags = 0x100,    // RIDEV_INPUTSINK
                    hwndTarget = _hwnd
                };

                isRawInputRegistered = My_Win32Api.RegisterRawInputDevices(
                    devices, (uint)devices.Length,
                    (uint)Marshal.SizeOf(typeof(My_Win32Api.RAWINPUTDEVICE)));

                if (isRawInputRegistered && rawInputBuffer == IntPtr.Zero)
                {
                    rawInputBuffer = Marshal.AllocHGlobal(1024);
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

                    devices[0] = new My_Win32Api.RAWINPUTDEVICE
                    {
                        usUsagePage = 0x01,
                        usUsage = 0x06,
                        dwFlags = 0x00000001, // RIDEV_REMOVE
                        hwndTarget = IntPtr.Zero
                    };

                    devices[1] = new My_Win32Api.RAWINPUTDEVICE
                    {
                        usUsagePage = 0x01,
                        usUsage = 0x02,
                        dwFlags = 0x00000001, // RIDEV_REMOVE
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

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_INPUT)
            {
                uint size = 0;

                uint result = My_Win32Api.GetRawInputData(lParam, My_Win32Api.RID_INPUT,
                    IntPtr.Zero, ref size, (uint)Marshal.SizeOf(typeof(My_Win32Api.RAWINPUTHEADER)));

                if (result == 0 && size > 0 && rawInputBuffer != IntPtr.Zero && size <= 1024)
                {
                    result = My_Win32Api.GetRawInputData(lParam, My_Win32Api.RID_INPUT,
                        rawInputBuffer, ref size, (uint)Marshal.SizeOf(typeof(My_Win32Api.RAWINPUTHEADER)));

                    if (result == size)
                    {
                        My_Win32Api.RAWINPUT rawInput = (My_Win32Api.RAWINPUT)Marshal.PtrToStructure(
                            rawInputBuffer, typeof(My_Win32Api.RAWINPUT))!;

                        ProcessRawInput(rawInput);
                    }
                }

                return IntPtr.Zero;
            }

            // Forward everything else to the original WndProc
            return CallWindowProc(_oldWndProc, hwnd, msg, wParam, lParam);
        }

        private void ProcessRawInput(My_Win32Api.RAWINPUT rawInput)
        {
            switch (rawInput.header.dwType)
            {
                case My_Win32Api.RIM_TYPEMOUSE:
                    ProcessMouseInput(rawInput.mouse);
                    break;

                case My_Win32Api.RIM_TYPEKEYBOARD:
                    // ProcessKeyboardInput(rawInput.keyboard);
                    break;
            }
        }

        private void ProcessMouseInput(My_Win32Api.RAWMOUSE mouse)
        {
            int deltaX = mouse.lLastX;
            int deltaY = mouse.lLastY;

            bool isRelativeMovement = (mouse.usFlags & 0x01) == 0; // MOUSE_MOVE_RELATIVE

            if (isRelativeMovement && (deltaX != 0 || deltaY != 0))
            {
                if (my_info.Instance.IsMouseLocked)
                {
                    gaming_mouse.mouse_Move(deltaX, deltaY);
                }
            }

            if (mouse.ulButtons != 0)
            {
                if (my_info.Instance.IsMouseLocked)
                {
                    if (mouse.ulButtons == 1 || mouse.ulButtons == 4)
                    {
                        gaming_mouse.mouse_press((int)mouse.ulButtons);
                    }
                    else
                    {
                        gaming_mouse.mouse_release((int)mouse.ulButtons);
                    }

                    if (mouse.ulButtons == 4287104000)
                    {
                        gaming_mouse.mousewheel_down();
                    }
                    else if (mouse.ulButtons == 7865344)
                    {
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
                case My_Win32Api.WM_SYSKEYDOWN:
                    isKeyDown = true;
                    break;

                case My_Win32Api.WM_KEYUP:
                case My_Win32Api.WM_SYSKEYUP:
                    isKeyUp = true;
                    break;
            }

            if (isKeyDown)
            {
                Console.WriteLine($"Key DOWN: VirtualKey={vkCode} (0x{vkCode:X})");
            }
            else if (isKeyUp)
            {
                Console.WriteLine($"Key UP: VirtualKey={vkCode} (0x{vkCode:X})");
            }
        }

        public void Dispose()
        {
            UnregisterRawInput();

            if (_oldWndProc != IntPtr.Zero && _hwnd != IntPtr.Zero)
            {
                SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _oldWndProc);
                _oldWndProc = IntPtr.Zero;
            }
        }
    }
}