using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Androidplayer.Src.Keymap;

namespace Androidplayer.Src.Keymap;

public partial class Elements_view : UserControl
{
    private Point _startPoint;
    private bool ispressed = false;

    public Elements_view()
    {
        InitializeComponent();
    }

    // Avalonia: DoDragDrop is async and takes PointerEventArgs.
    // The handler becomes async and accepts PointerEventArgs (not MouseEventArgs).
    private async void Button_MouseMove(object? sender, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        if (sender is Button button)
        {
            var tag = button.Tag?.ToString() ?? "Unknown";

            // Avalonia: construct DataObject, then Set with a key
            var data = new DataObject();
            data.Set("KeyElementFormat", tag);

            // Avalonia: DoDragDrop takes the PointerEventArgs and is awaited
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
        }
    }
}