// using Avalonia;
// using Avalonia.Controls;
// using Avalonia.Markup.Xaml;
//
// namespace Androidplayer.windows.settings_view;
//
// public partial class AboutSettings : UserControl
// {
//     public AboutSettings()
//     {
//         InitializeComponent();
//     }
// }

using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;

namespace Androidplayer.windows.settings_view;

public partial class AboutSettings : UserControl
{
    private const string GitHubUrl   = "https://github.com/mikiboii/AndroidPlayer";
    private const string IssuesUrl   = "https://github.com/mikiboii/AndroidPlayer/issues";
    private const string PayPalUrl   = "https://paypal.me/mikiyasweldetinsay21";

    public AboutSettings()
    {
        InitializeComponent();

        GithubButton.Click += (_, _) => OpenUrl(GitHubUrl);
        IssuesButton.Click += (_, _) => OpenUrl(IssuesUrl);
        PayPalButton.Click += (_, _) => OpenUrl(PayPalUrl);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { /* optionally show a toast */ }
    }
}