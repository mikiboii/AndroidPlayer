using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Androidplayer.windows.settings_view;

public partial class MouseSettings : UserControl
{
    public MouseSettings()
    {
        InitializeComponent();
    }
    private void CursorCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border card) return;

        var rb = card.GetVisualDescendants()
            .OfType<RadioButton>()
            .FirstOrDefault();

        if (rb == null) return;

        if (rb.IsChecked != true)
            rb.IsChecked = true;

        // Stop the event from bubbling to the RadioButton's own handler,
        // so we don't get double-toggle behavior.
        e.Handled = true;
    }
    private void Cursor_Checked(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;

        // Clear 'selected' from all cursor cards
        foreach (var card in this.GetVisualDescendants()
                     .OfType<Border>()
                     .Where(b => b.Classes.Contains("cursorCard")))
        {
            card.Classes.Remove("selected");
        }

        // Mark the card that contains this radio as selected
        var parentCard = rb.FindAncestorOfType<Border>();
        parentCard?.Classes.Add("selected");

        string key = rb.Tag?.ToString() ?? "default";
        Console.WriteLine($"[cursor] selected: {key}");
    }
}