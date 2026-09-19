using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Androidplayer.windows;

namespace Androidplayer.Src.Pages
{
    public partial class Loading_page : UserControl
    {
        private double progressValue = 0;
        private readonly double progressMax = 100;

        public Loading_page()
        {
            InitializeComponent();
            SetSelectedText(UsbText);

            this.DataContext = UISettings.Instance;

            LoadSavedPreference();

            this.Loaded += (s, e) =>
            {
                // UpdateProgress(0, "No device found. please reconnect your device");
            };
        }

        private void UsbText_OnClick(object? sender, PointerReleasedEventArgs e)
        {
            SetSelectedText(UsbText);
            SavePreference("USB");
            Console.WriteLine("USB clicked");
        }

        private void WirelessText_OnClick(object? sender, PointerReleasedEventArgs e)
        {
            SetSelectedText(WirelessText);
            SavePreference("Wireless");
            Console.WriteLine("Wireless clicked");
        }

        private void SetSelectedText(TextBlock selected)
        {
            UsbText.Foreground = Brushes.Black;
            UsbText.TextDecorations = null;

            WirelessText.Foreground = Brushes.Black;
            WirelessText.TextDecorations = null;

            selected.Foreground = Brush.Parse("#1a7c80");
            selected.TextDecorations = TextDecorations.Underline;
        }

        #region Persistence Methods using UISettings

        private void SavePreference(string selected)
        {
            try
            {
                if (this.DataContext is UISettings settings)
                {
                    settings.SelectedConnectionType = selected;
                    settings.Save();
                    Console.WriteLine($"Preference saved: {selected}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving preference: {ex.Message}");
            }
        }

        private void LoadSavedPreference()
        {
            try
            {
                if (this.DataContext is UISettings settings)
                {
                    Console.WriteLine("loading ##############");
                    string savedPreference = settings.SelectedConnectionType;

                    if (!string.IsNullOrEmpty(savedPreference))
                    {
                        Console.WriteLine($"Loaded preference: {savedPreference}");

                        if (savedPreference == "USB")
                        {
                            SetSelectedText(UsbText);
                        }
                        else if (savedPreference == "Wireless")
                        {
                            SetSelectedText(WirelessText);
                        }
                        else
                        {
                            SetSelectedText(UsbText);
                            settings.SelectedConnectionType = "USB";
                            settings.Save();
                        }
                    }
                    else
                    {
                        SetSelectedText(UsbText);
                        settings.SelectedConnectionType = "USB";
                        settings.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading preference: {ex.Message}");
                SetSelectedText(UsbText);
            }
        }

        #endregion

        public void UpdateProgress(double value, string status)
        {
            if (value == 0)
            {
                if (Home.Instance != null)
                {
                    Home.Instance.loadingpage.IsVisible = true;
                    Home.Instance.displayView.IsVisible = false;
                }

                progress_view.IsVisible = false;
                guide_view.IsVisible = true;
            }
            else
            {
                progress_view.IsVisible = true;
                guide_view.IsVisible = false;
            }

            progressValue = Math.Min(Math.Max(value, 0), progressMax);
            ProgressPercent.Text = $"{(int)progressValue}%";

            StatusLabel.Text = status;

            double angle = 359.999 * (progressValue / progressMax);

            ProgressArc.Data = CreateArcGeometry(100, 100, 94, -90, angle);
        }

        private Geometry CreateArcGeometry(double cx, double cy, double radius,
                                            double startAngle, double sweepAngle)
        {
            double startRad = startAngle * Math.PI / 180;
            double endRad = (startAngle + sweepAngle) * Math.PI / 180;

            Point startPoint = new Point(
                cx + radius * Math.Cos(startRad),
                cy + radius * Math.Sin(startRad));

            Point endPoint = new Point(
                cx + radius * Math.Cos(endRad),
                cy + radius * Math.Sin(endRad));

            bool isLarge = sweepAngle > 180;

            var figure = new PathFigure { StartPoint = startPoint, IsClosed = false };
            figure.Segments!.Add(new ArcSegment
            {
                Point = endPoint,
                Size = new Size(radius, radius),
                IsLargeArc = isLarge,
                SweepDirection = SweepDirection.Clockwise
            });

            var geometry = new PathGeometry();
            geometry.Figures!.Add(figure);
            return geometry;
        }
    }
}