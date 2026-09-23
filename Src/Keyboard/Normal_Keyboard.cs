using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Input;
using Androidplayer.Src.Android;
using Androidplayer.Store;
using Androidplayer.Src.Keymap;

namespace Androidplayer.Src.Keyboard;

public class Normal_Keyboard
{
    private const byte TYPE_INJECT_KEYCODE = 0x00;
    private const byte TYPE_INJECT_TEXT = 0x01;

    // Key action constants
    private const byte ACTION_DOWN = 0x00;
    private const byte ACTION_UP = 0x01;

    // MetaState constants (shift, ctrl, etc.)
    private const int META_NONE = 0x0;

    public Normal_Keyboard()
    {
    }

    public void Key_pressed(object? source, KeyEventArgs e)
    {
        // Avalonia: e.Key is always the real key. No Key.System check needed.
        // If you want to skip Alt-combos, check e.KeyModifiers.HasFlag(KeyModifiers.Alt) instead.
        Key actualKey = e.Key;

        string keyStr = ConvertKeyToString(actualKey, e.KeyModifiers);

        Console.WriteLine(keyStr);

        if (IsModifierKey(actualKey))
        {
            // if (!e.IsRepeat)
            // {
            //     if (AndroidKeyCode_store.ModifierKeyMap.ContainsKey(actualKey))
            //     {
            //         AndroidKeyCode code = AndroidKeyCode_store.ModifierKeyMap[actualKey];
            //         SendKey((int)code, ACTION_DOWN); // send Android keycode
            //     }
            // }

            return;
        }

        SendText(keyStr);
    }

    public void Key_Released(object? source, KeyEventArgs e)
    {
        Key actualKey = e.Key;
        string keyStr = ConvertKeyToString(actualKey, e.KeyModifiers);

        if (IsModifierKey(actualKey))
        {
            // if (!e.IsRepeat)
            // {
            //     if (AndroidKeyCode_store.ModifierKeyMap.ContainsKey(actualKey))
            //     {
            //         AndroidKeyCode code = AndroidKeyCode_store.ModifierKeyMap[actualKey];
            //         SendKey((int)code, ACTION_UP); // send Android keycode
            //     }
            // }

            return;
        }
    }

    public void SendText(string text)
    {
        byte[] textBytes = Encoding.UTF8.GetBytes(text);
        byte[] buf = new byte[1 + 4 + textBytes.Length];
        buf[0] = TYPE_INJECT_TEXT;
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(1, 4), textBytes.Length);
        textBytes.CopyTo(buf, 5);
        SendData(buf);
    }

    public void SendKey(int keycode, byte action, int metastate = META_NONE)
    {
        byte[] down = new byte[14];
        down[0] = TYPE_INJECT_KEYCODE;
        down[1] = action;
        BinaryPrimitives.WriteInt32BigEndian(down.AsSpan(2, 4), keycode);
        BinaryPrimitives.WriteInt32BigEndian(down.AsSpan(6, 4), 0); // repeat
        BinaryPrimitives.WriteInt32BigEndian(down.AsSpan(10, 4), metastate);
        SendData(down);
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
                Console.WriteLine($"Error sending direction data: {ex.Message}");
            }
        }
    }

    private bool IsModifierKey(Key key)
    {
        switch (key)
        {
            // Modifier keys
            case Key.LeftShift:
            case Key.RightShift:
            case Key.LeftCtrl:
            case Key.RightCtrl:
            case Key.LeftAlt:
            case Key.RightAlt:
            case Key.LWin:
            case Key.RWin:
            case Key.CapsLock:
            case Key.NumLock:
            case Key.Scroll:
            case Key.Insert:
            case Key.Clear:
                return true;

            // Function keys
            case Key.F1: case Key.F2: case Key.F3: case Key.F4:
            case Key.F5: case Key.F6: case Key.F7: case Key.F8:
            case Key.F9: case Key.F10: case Key.F11: case Key.F12:
                return true;

            // Navigation keys
            case Key.Up:
            case Key.Down:
            case Key.Left:
            case Key.Right:
            case Key.Home:
            case Key.End:
            case Key.PageUp:
            case Key.PageDown:
            case Key.Add:
            case Key.Subtract:
                return true;

            // Editing keys
            case Key.Back:
            case Key.Delete:
            case Key.Enter:
            case Key.Tab:
            case Key.Escape:
                return true;

            // Media keys (optional)
            case Key.VolumeDown:
            case Key.VolumeUp:
            case Key.MediaNextTrack:
            case Key.MediaPreviousTrack:
            case Key.MediaPlayPause:
            case Key.MediaStop:
                return true;

            default:
                return false;
        }
    }

    private string ConvertKeyToString(Key key, KeyModifiers modifiers)
    {
        // Avalonia: modifier state comes from the event args, not a static Keyboard class.
        bool shift = modifiers.HasFlag(KeyModifiers.Shift);

        switch (key)
        {
            // Numbers and symbols
            case Key.D0: return shift ? ")" : "0";
            case Key.D1: return shift ? "!" : "1";
            case Key.D2: return shift ? "@" : "2";
            case Key.D3: return shift ? "#" : "3";
            case Key.D4: return shift ? "$" : "4";
            case Key.D5: return shift ? "%" : "5";
            case Key.D6: return shift ? "^" : "6";
            case Key.D7: return shift ? "&" : "7";
            case Key.D8: return shift ? "*" : "8";
            case Key.D9: return shift ? "(" : "9";

            // Letters
            case Key.A: return shift ? "A" : "a";
            case Key.B: return shift ? "B" : "b";
            case Key.C: return shift ? "C" : "c";
            case Key.D: return shift ? "D" : "d";
            case Key.E: return shift ? "E" : "e";
            case Key.F: return shift ? "F" : "f";
            case Key.G: return shift ? "G" : "g";
            case Key.H: return shift ? "H" : "h";
            case Key.I: return shift ? "I" : "i";
            case Key.J: return shift ? "J" : "j";
            case Key.K: return shift ? "K" : "k";
            case Key.L: return shift ? "L" : "l";
            case Key.M: return shift ? "M" : "m";
            case Key.N: return shift ? "N" : "n";
            case Key.O: return shift ? "O" : "o";
            case Key.P: return shift ? "P" : "p";
            case Key.Q: return shift ? "Q" : "q";
            case Key.R: return shift ? "R" : "r";
            case Key.S: return shift ? "S" : "s";
            case Key.T: return shift ? "T" : "t";
            case Key.U: return shift ? "U" : "u";
            case Key.V: return shift ? "V" : "v";
            case Key.W: return shift ? "W" : "w";
            case Key.X: return shift ? "X" : "x";
            case Key.Y: return shift ? "Y" : "y";
            case Key.Z: return shift ? "Z" : "z";

            // Punctuation and symbols
            case Key.OemMinus: return shift ? "_" : "-";
            case Key.OemPlus: return shift ? "+" : "=";
            case Key.OemTilde: return shift ? "~" : "`";
            case Key.OemOpenBrackets: return shift ? "{" : "[";
            case Key.OemCloseBrackets: return shift ? "}" : "]";
            case Key.OemPipe: return shift ? "|" : "\\";
            case Key.OemSemicolon: return shift ? ":" : ";";
            case Key.OemQuotes: return shift ? "\"" : "'";
            case Key.OemComma: return shift ? "<" : ",";
            case Key.OemPeriod: return shift ? ">" : ".";
            case Key.OemQuestion: return shift ? "?" : "/";

            // Whitespace and control keys
            case Key.Space: return " ";
            case Key.Enter: return "\n";
            case Key.Tab: return "\t";
            case Key.Back: return "[BACKSPACE]";
            case Key.Escape: return "[ESC]";
            case Key.Delete: return "[DEL]";
            case Key.Insert: return "[INS]";
            case Key.Home: return "[HOME]";
            case Key.End: return "[END]";
            case Key.PageUp: return "[PGUP]";
            case Key.PageDown: return "[PGDN]";

            default: return key.ToString();
        }
    }
}