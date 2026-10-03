// using System;
// using System.Buffers.Binary;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Net.Sockets;
// using System.Runtime.InteropServices;
// using System.Text;
// using Avalonia.Input;
// using Androidplayer.Src.Android;
// using Androidplayer.Store;
// using Androidplayer.Src.Keymap;
//
// namespace Androidplayer.Src.Keyboard;
//
// public class Normal_Keyboard
// {
//     private const byte TYPE_INJECT_KEYCODE = 0x00;
//     private const byte TYPE_INJECT_TEXT = 0x01;
//
//     // Key action constants
//     private const byte ACTION_DOWN = 0x00;
//     private const byte ACTION_UP = 0x01;
//
//     // MetaState constants (shift, ctrl, etc.)
//     private const int META_NONE = 0x0;
//
//     public Normal_Keyboard()
//     {
//     }
//
//     public void Key_pressed(object? source, KeyEventArgs e)
//     {
//         // Avalonia: e.Key is always the real key. No Key.System check needed.
//         // If you want to skip Alt-combos, check e.KeyModifiers.HasFlag(KeyModifiers.Alt) instead.
//         Key actualKey = e.Key;
//
//         string keyStr = ConvertKeyToString(actualKey, e.KeyModifiers);
//
//         Console.WriteLine(keyStr);
//
//         if (IsModifierKey(actualKey))
//         {
//             // if (!e.IsRepeat)
//             // {
//             //     if (AndroidKeyCode_store.ModifierKeyMap.ContainsKey(actualKey))
//             //     {
//             //         AndroidKeyCode code = AndroidKeyCode_store.ModifierKeyMap[actualKey];
//             //         SendKey((int)code, ACTION_DOWN); // send Android keycode
//             //     }
//             // }
//
//             return;
//         }
//
//         SendText(keyStr);
//     }
//
//     public void Key_Released(object? source, KeyEventArgs e)
//     {
//         Key actualKey = e.Key;
//         string keyStr = ConvertKeyToString(actualKey, e.KeyModifiers);
//
//         if (IsModifierKey(actualKey))
//         {
//             // if (!e.IsRepeat)
//             // {
//             //     if (AndroidKeyCode_store.ModifierKeyMap.ContainsKey(actualKey))
//             //     {
//             //         AndroidKeyCode code = AndroidKeyCode_store.ModifierKeyMap[actualKey];
//             //         SendKey((int)code, ACTION_UP); // send Android keycode
//             //     }
//             // }
//
//             return;
//         }
//     }
//
//     public void SendText(string text)
//     {
//         byte[] textBytes = Encoding.UTF8.GetBytes(text);
//         byte[] buf = new byte[1 + 4 + textBytes.Length];
//         buf[0] = TYPE_INJECT_TEXT;
//         BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(1, 4), textBytes.Length);
//         textBytes.CopyTo(buf, 5);
//         SendData(buf);
//     }
//
//     public void SendKey(int keycode, byte action, int metastate = META_NONE)
//     {
//         byte[] down = new byte[14];
//         down[0] = TYPE_INJECT_KEYCODE;
//         down[1] = action;
//         BinaryPrimitives.WriteInt32BigEndian(down.AsSpan(2, 4), keycode);
//         BinaryPrimitives.WriteInt32BigEndian(down.AsSpan(6, 4), 0); // repeat
//         BinaryPrimitives.WriteInt32BigEndian(down.AsSpan(10, 4), metastate);
//         SendData(down);
//     }
//
//     private void SendData(byte[] data)
//     {
//         TcpClient controlSocket = My_Store.Instance.ControlSocket;
//
//         if (controlSocket != null && controlSocket.Connected)
//         {
//             try
//             {
//                 Socket socket = controlSocket.Client;
//
//                 socket.NoDelay = true;
//                 socket.Blocking = false;
//                 socket.Send(data, 0, data.Length, SocketFlags.None);
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"Error sending direction data: {ex.Message}");
//             }
//         }
//     }
//
//     private bool IsModifierKey(Key key)
//     {
//         switch (key)
//         {
//             // Modifier keys
//             case Key.LeftShift:
//             case Key.RightShift:
//             case Key.LeftCtrl:
//             case Key.RightCtrl:
//             case Key.LeftAlt:
//             case Key.RightAlt:
//             case Key.LWin:
//             case Key.RWin:
//             case Key.CapsLock:
//             case Key.NumLock:
//             case Key.Scroll:
//             case Key.Insert:
//             case Key.Clear:
//                 return true;
//
//             // Function keys
//             case Key.F1: case Key.F2: case Key.F3: case Key.F4:
//             case Key.F5: case Key.F6: case Key.F7: case Key.F8:
//             case Key.F9: case Key.F10: case Key.F11: case Key.F12:
//                 return true;
//
//             // Navigation keys
//             case Key.Up:
//             case Key.Down:
//             case Key.Left:
//             case Key.Right:
//             case Key.Home:
//             case Key.End:
//             case Key.PageUp:
//             case Key.PageDown:
//             case Key.Add:
//             case Key.Subtract:
//                 return true;
//
//             // Editing keys
//             case Key.Back:
//             case Key.Delete:
//             case Key.Enter:
//             case Key.Tab:
//             case Key.Escape:
//                 return true;
//
//             // Media keys (optional)
//             case Key.VolumeDown:
//             case Key.VolumeUp:
//             case Key.MediaNextTrack:
//             case Key.MediaPreviousTrack:
//             case Key.MediaPlayPause:
//             case Key.MediaStop:
//                 return true;
//
//             default:
//                 return false;
//         }
//     }
//
//     private string ConvertKeyToString(Key key, KeyModifiers modifiers)
//     {
//         // Avalonia: modifier state comes from the event args, not a static Keyboard class.
//         bool shift = modifiers.HasFlag(KeyModifiers.Shift);
//
//         switch (key)
//         {
//             // Numbers and symbols
//             case Key.D0: return shift ? ")" : "0";
//             case Key.D1: return shift ? "!" : "1";
//             case Key.D2: return shift ? "@" : "2";
//             case Key.D3: return shift ? "#" : "3";
//             case Key.D4: return shift ? "$" : "4";
//             case Key.D5: return shift ? "%" : "5";
//             case Key.D6: return shift ? "^" : "6";
//             case Key.D7: return shift ? "&" : "7";
//             case Key.D8: return shift ? "*" : "8";
//             case Key.D9: return shift ? "(" : "9";
//
//             // Letters
//             case Key.A: return shift ? "A" : "a";
//             case Key.B: return shift ? "B" : "b";
//             case Key.C: return shift ? "C" : "c";
//             case Key.D: return shift ? "D" : "d";
//             case Key.E: return shift ? "E" : "e";
//             case Key.F: return shift ? "F" : "f";
//             case Key.G: return shift ? "G" : "g";
//             case Key.H: return shift ? "H" : "h";
//             case Key.I: return shift ? "I" : "i";
//             case Key.J: return shift ? "J" : "j";
//             case Key.K: return shift ? "K" : "k";
//             case Key.L: return shift ? "L" : "l";
//             case Key.M: return shift ? "M" : "m";
//             case Key.N: return shift ? "N" : "n";
//             case Key.O: return shift ? "O" : "o";
//             case Key.P: return shift ? "P" : "p";
//             case Key.Q: return shift ? "Q" : "q";
//             case Key.R: return shift ? "R" : "r";
//             case Key.S: return shift ? "S" : "s";
//             case Key.T: return shift ? "T" : "t";
//             case Key.U: return shift ? "U" : "u";
//             case Key.V: return shift ? "V" : "v";
//             case Key.W: return shift ? "W" : "w";
//             case Key.X: return shift ? "X" : "x";
//             case Key.Y: return shift ? "Y" : "y";
//             case Key.Z: return shift ? "Z" : "z";
//
//             // Punctuation and symbols
//             case Key.OemMinus: return shift ? "_" : "-";
//             case Key.OemPlus: return shift ? "+" : "=";
//             case Key.OemTilde: return shift ? "~" : "`";
//             case Key.OemOpenBrackets: return shift ? "{" : "[";
//             case Key.OemCloseBrackets: return shift ? "}" : "]";
//             case Key.OemPipe: return shift ? "|" : "\\";
//             case Key.OemSemicolon: return shift ? ":" : ";";
//             case Key.OemQuotes: return shift ? "\"" : "'";
//             case Key.OemComma: return shift ? "<" : ",";
//             case Key.OemPeriod: return shift ? ">" : ".";
//             case Key.OemQuestion: return shift ? "?" : "/";
//
//             // Whitespace and control keys
//             case Key.Space: return " ";
//             case Key.Enter: return "\n";
//             case Key.Tab: return "\t";
//             case Key.Back: return "[BACKSPACE]";
//             case Key.Escape: return "[ESC]";
//             case Key.Delete: return "[DEL]";
//             case Key.Insert: return "[INS]";
//             case Key.Home: return "[HOME]";
//             case Key.End: return "[END]";
//             case Key.PageUp: return "[PGUP]";
//             case Key.PageDown: return "[PGDN]";
//
//             default: return key.ToString();
//         }
//     }
// }








using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Avalonia.Input;
using Androidplayer.Store;

namespace Androidplayer.Src.Keyboard;

public class Normal_Keyboard
{
    // scrcpy control message types
    private const byte TYPE_INJECT_KEYCODE = 0x00;
    private const byte TYPE_INJECT_TEXT = 0x01;

    private const byte ACTION_DOWN = 0x00;
    private const byte ACTION_UP = 0x01;

    // scrcpy limits injected text to 300 bytes
    private const int MAX_TEXT_BYTES = 300;

    // Android KeyEvent meta state flags
    private const int META_SHIFT_ON = 0x1;
    private const int META_ALT_ON = 0x2;
    private const int META_SHIFT_LEFT_ON = 0x40;
    private const int META_SHIFT_RIGHT_ON = 0x80;
    private const int META_ALT_LEFT_ON = 0x10;
    private const int META_ALT_RIGHT_ON = 0x20;
    private const int META_CTRL_ON = 0x1000;
    private const int META_CTRL_LEFT_ON = 0x2000;
    private const int META_CTRL_RIGHT_ON = 0x4000;
    private const int META_META_ON = 0x10000;
    private const int META_META_LEFT_ON = 0x20000;
    private const int META_META_RIGHT_ON = 0x40000;

    private class HeldKey
    {
        public Key Key;
        public bool SentAsKeycode;
        public int AndroidKeycode;
        public int Repeat;
    }

    // Keyed by physical key so layout/shift changes can't desync down/up
    private readonly Dictionary<PhysicalKey, HeldKey> _held = new();
    private readonly object _sendLock = new();

    public void Key_pressed(object? source, KeyEventArgs e)
    {
        e.Handled = true; // stop Avalonia focus navigation on Tab/arrows

        Key key = e.Key;
        if (key == Key.None) return;

        // Repeat detection (Avalonia has no IsRepeat)
        if (_held.TryGetValue(e.PhysicalKey, out var existing))
        {
            existing.Repeat++;
            if (existing.SentAsKeycode)
            {
                SendKeycode(existing.AndroidKeycode, ACTION_DOWN, existing.Repeat, GetMetaState());
            }
            else
            {
                // Text keys repeat like SDL text input does
                string? t = ConvertKeyToString(key, e.KeyModifiers);
                if (!string.IsNullOrEmpty(t)) SendText(t);
            }
            return;
        }

        var held = new HeldKey { Key = key };
        _held[e.PhysicalKey] = held; // add BEFORE computing meta so a modifier includes itself

        int keycode = ConvertKeyToAndroidKeycode(key);
        bool isSpecial = keycode >= 0 && !IsPrintableKey(key);
        bool commandMod = e.KeyModifiers.HasFlag(KeyModifiers.Control)
                          || e.KeyModifiers.HasFlag(KeyModifiers.Alt)
                          || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        if (isSpecial || (keycode >= 0 && commandMod))
        {
            // Non-printable keys always; printable keys when Ctrl/Alt/Meta is held
            held.SentAsKeycode = true;
            held.AndroidKeycode = keycode;
            SendKeycode(keycode, ACTION_DOWN, 0, GetMetaState());
            return;
        }

        string? text = ConvertKeyToString(key, e.KeyModifiers);
        if (!string.IsNullOrEmpty(text))
            SendText(text);
    }

    public void Key_Released(object? source, KeyEventArgs e)
    {
        e.Handled = true;

        if (!_held.Remove(e.PhysicalKey, out var held)) return;

        if (held.SentAsKeycode)
        {
            // Meta computed AFTER removal, so a released modifier is excluded
            SendKeycode(held.AndroidKeycode, ACTION_UP, 0, GetMetaState());
        }
    }

    /// <summary>Call on window deactivated / lost focus so no key stays stuck on the device.</summary>
    public void ReleaseAll()
    {
        var keys = new List<HeldKey>(_held.Values);
        _held.Clear();
        foreach (var h in keys)
        {
            if (h.SentAsKeycode)
                SendKeycode(h.AndroidKeycode, ACTION_UP, 0, GetMetaState());
        }
    }

    // ---------- Meta state (like scrcpy's convert_meta_state) ----------

    private bool IsHeld(Key k)
    {
        foreach (var h in _held.Values)
            if (h.Key == k) return true;
        return false;
    }

    private int GetMetaState()
    {
        int meta = 0;

        bool ls = IsHeld(Key.LeftShift), rs = IsHeld(Key.RightShift);
        if (ls) meta |= META_SHIFT_ON | META_SHIFT_LEFT_ON;
        if (rs) meta |= META_SHIFT_ON | META_SHIFT_RIGHT_ON;

        bool la = IsHeld(Key.LeftAlt), ra = IsHeld(Key.RightAlt);
        if (la) meta |= META_ALT_ON | META_ALT_LEFT_ON;
        if (ra) meta |= META_ALT_ON | META_ALT_RIGHT_ON;

        bool lc = IsHeld(Key.LeftCtrl), rc = IsHeld(Key.RightCtrl);
        if (lc) meta |= META_CTRL_ON | META_CTRL_LEFT_ON;
        if (rc) meta |= META_CTRL_ON | META_CTRL_RIGHT_ON;

        bool lm = IsHeld(Key.LWin), rm = IsHeld(Key.RWin);
        if (lm) meta |= META_META_ON | META_META_LEFT_ON;
        if (rm) meta |= META_META_ON | META_META_RIGHT_ON;

        return meta;
    }

    // ---------- Senders ----------

    /// [type:1][action:1][keycode:4][repeat:4][metastate:4] = 14 bytes
    private void SendKeycode(int keycode, byte action, int repeat, int metastate)
    {
        byte[] buf = new byte[14];
        buf[0] = TYPE_INJECT_KEYCODE;
        buf[1] = action;
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(2, 4), keycode);
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(6, 4), repeat);
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(10, 4), metastate);
        SendData(buf);
    }

    /// [type:1][length:4][utf8 text:N]
    private void SendText(string text)
    {
        byte[] textBytes = Encoding.UTF8.GetBytes(text);
        if (textBytes.Length == 0) return;
        if (textBytes.Length > MAX_TEXT_BYTES)
            Array.Resize(ref textBytes, MAX_TEXT_BYTES);

        byte[] buf = new byte[1 + 4 + textBytes.Length];
        buf[0] = TYPE_INJECT_TEXT;
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(1, 4), textBytes.Length);
        textBytes.CopyTo(buf, 5);
        SendData(buf);
    }

    private void SendData(byte[] data)
    {
        TcpClient? controlSocket = My_Store.Instance.ControlSocket;
        if (controlSocket == null || !controlSocket.Connected)
        {
            Console.WriteLine("Control socket not connected");
            return;
        }

        try
        {
            lock (_sendLock) // keep messages from interleaving
            {
                Socket socket = controlSocket.Client;
                socket.NoDelay = true;
                // Blocking stays true: a non-blocking Send can partially write
                // and corrupt the stream framing.
                int sent = 0;
                while (sent < data.Length)
                    sent += socket.Send(data, sent, data.Length - sent, SocketFlags.None);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending keyboard data: {ex.Message}");
        }
    }

    // ---------- Key classification / mapping ----------

    private static bool IsPrintableKey(Key key)
    {
        return (key >= Key.A && key <= Key.Z)
            || (key >= Key.D0 && key <= Key.D9)
            || key is Key.Space or Key.OemMinus or Key.OemPlus or Key.OemTilde
                or Key.OemOpenBrackets or Key.OemCloseBrackets or Key.OemPipe
                or Key.OemSemicolon or Key.OemQuotes or Key.OemComma
                or Key.OemPeriod or Key.OemQuestion;
    }

    private string? ConvertKeyToString(Key key, KeyModifiers modifiers)
    {
        bool shift = modifiers.HasFlag(KeyModifiers.Shift);

        if (key >= Key.A && key <= Key.Z)
        {
            char c = (char)('a' + (key - Key.A));
            return (shift ? char.ToUpperInvariant(c) : c).ToString();
        }

        return key switch
        {
            Key.D0 => shift ? ")" : "0",
            Key.D1 => shift ? "!" : "1",
            Key.D2 => shift ? "@" : "2",
            Key.D3 => shift ? "#" : "3",
            Key.D4 => shift ? "$" : "4",
            Key.D5 => shift ? "%" : "5",
            Key.D6 => shift ? "^" : "6",
            Key.D7 => shift ? "&" : "7",
            Key.D8 => shift ? "*" : "8",
            Key.D9 => shift ? "(" : "9",

            Key.OemMinus => shift ? "_" : "-",
            Key.OemPlus => shift ? "+" : "=",
            Key.OemTilde => shift ? "~" : "`",
            Key.OemOpenBrackets => shift ? "{" : "[",
            Key.OemCloseBrackets => shift ? "}" : "]",
            Key.OemPipe => shift ? "|" : "\\",
            Key.OemSemicolon => shift ? ":" : ";",
            Key.OemQuotes => shift ? "\"" : "'",
            Key.OemComma => shift ? "<" : ",",
            Key.OemPeriod => shift ? ">" : ".",
            Key.OemQuestion => shift ? "?" : "/",
            Key.Space => " ",
            _ => null
        };
    }

    /// Android keycodes (android.view.KeyEvent)
    private int ConvertKeyToAndroidKeycode(Key key)
    {
        if (key >= Key.A && key <= Key.Z) return 29 + (key - Key.A);      // KEYCODE_A..Z
        if (key >= Key.D0 && key <= Key.D9) return 7 + (key - Key.D0);    // KEYCODE_0..9
        if (key >= Key.NumPad0 && key <= Key.NumPad9) return 144 + (key - Key.NumPad0);
        if (key >= Key.F1 && key <= Key.F12) return 131 + (key - Key.F1);

        return key switch
        {
            // Navigation
            Key.Up => 19, Key.Down => 20, Key.Left => 21, Key.Right => 22,
            Key.Home => 122, Key.End => 123,
            Key.PageUp => 92, Key.PageDown => 93,
            Key.Insert => 124,

            // Modifiers
            Key.LeftShift => 59, Key.RightShift => 60,
            Key.LeftCtrl => 113, Key.RightCtrl => 114,
            Key.LeftAlt => 57, Key.RightAlt => 58,
            Key.LWin => 117, Key.RWin => 118,

            // Editing
            Key.Back => 67,        // KEYCODE_DEL (backspace)
            Key.Delete => 112,     // KEYCODE_FORWARD_DEL
            Key.Enter => 66,
            Key.Tab => 61,
            Key.Escape => 111,
            Key.Space => 62,

            // Locks / misc
            Key.CapsLock => 115, Key.NumLock => 143, Key.Scroll => 116,
            Key.Pause => 121, Key.PrintScreen => 120, Key.Apps => 82,

            // Punctuation
            Key.OemTilde => 68, Key.OemMinus => 69, Key.OemPlus => 70,
            Key.OemOpenBrackets => 71, Key.OemCloseBrackets => 72,
            Key.OemPipe => 73, Key.OemSemicolon => 74, Key.OemQuotes => 75,
            Key.OemQuestion => 76, Key.OemComma => 55, Key.OemPeriod => 56,

            // Keypad operators
            Key.Multiply => 155, Key.Divide => 154,
            Key.Subtract => 156, Key.Add => 157, Key.Decimal => 158,

            _ => -1
        };
    }
}