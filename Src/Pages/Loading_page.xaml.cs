// using System;
// using System.Windows;
// using System.Windows.Controls;
// using System.Windows.Input;
// using System.Windows.Media;
// using System.Windows.Shapes;
//
// namespace Androidplayer.Src.Pages
// {
//     public partial class Loading_page : UserControl
//     {
//         private double progressValue = 0;
//         private readonly double progressMax = 100;
//
//         public Loading_page()
//         {
//             InitializeComponent();
//             SetSelectedText(UsbText);
//             
//             double currentWidth = this.Width;
//             double currentHeight = this.Height;
//
//             Console.WriteLine($"Loading page size: {currentWidth} x {currentHeight}");
//
//
//             this.Loaded += (sender, args) =>
//             {
//
//                 Home.Instance.Width = currentWidth;
//                 
//                 
//                 UpdateProgress((double)30, "pushed seerver");
//             };
//
//         }
//
//         private void UsbText_OnClick(object sender, MouseButtonEventArgs e)
//         {
//             SetSelectedText(UsbText);
//             // do your USB logic here
//             Console.WriteLine("USB clicked");
//         }
//
//         private void WirelessText_OnClick(object sender, MouseButtonEventArgs e)
//         {
//             SetSelectedText(WirelessText);
//             // do your Wireless logic here
//             Console.WriteLine("Wireless clicked");
//         }
//
//         private void SetSelectedText(TextBlock selected)
//         {
//             // Reset both
//             UsbText.Foreground = Brushes.Black;
//             UsbText.TextDecorations = null;
//
//             WirelessText.Foreground = Brushes.Black;
//             WirelessText.TextDecorations = null;
//
//             // Highlight selected
//             selected.Foreground = (Brush)new BrushConverter().ConvertFrom("#1a7c80");
//             selected.TextDecorations = TextDecorations.Underline;
//         }
//
//
//        
//
//         public void UpdateProgress(double value, string status)
//         {
//             progressValue = Math.Min(Math.Max(value, 0), progressMax);
//             ProgressPercent.Text = $"{(int)progressValue}%";
//             ProgressStatus.Text = status;
//
//             double angle = 360 * (progressValue / progressMax);
//             ProgressArc.Data = CreateArcGeometry(60, 60, 54, -90, angle);
//         }
//
//         private Geometry CreateArcGeometry(double cx, double cy, double radius, double startAngle, double sweepAngle)
//         {
//             double startRad = startAngle * Math.PI / 180;
//             double endRad = (startAngle + sweepAngle) * Math.PI / 180;
//
//             Point startPoint = new Point(cx + radius * Math.Cos(startRad), cy + radius * Math.Sin(startRad));
//             Point endPoint = new Point(cx + radius * Math.Cos(endRad), cy + radius * Math.Sin(endRad));
//
//             bool isLarge = sweepAngle > 180;
//
//             var figure = new PathFigure { StartPoint = startPoint };
//             figure.Segments.Add(new ArcSegment
//             {
//                 Point = endPoint,
//                 Size = new Size(radius, radius),
//                 IsLargeArc = isLarge,
//                 SweepDirection = SweepDirection.Clockwise
//             });
//
//             var geometry = new PathGeometry();
//             geometry.Figures.Add(figure);
//             return geometry;
//         }
//     }
// }


using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

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

            
            
            this.Loaded += (s, e) =>
            {
                UpdateProgress(0 , "No device found. please reconnect your device");
            };
        }

        private void UsbText_OnClick(object sender, MouseButtonEventArgs e)
        {
            SetSelectedText(UsbText);
            Console.WriteLine("USB clicked");
        }

        private void WirelessText_OnClick(object sender, MouseButtonEventArgs e)
        {
            SetSelectedText(WirelessText);
            Console.WriteLine("Wireless clicked");
        }

        private void SetSelectedText(TextBlock selected)
        {
            UsbText.Foreground = Brushes.Black;
            UsbText.TextDecorations = null;
            WirelessText.Foreground = Brushes.Black;
            WirelessText.TextDecorations = null;

            selected.Foreground = (Brush)new BrushConverter().ConvertFrom("#1a7c80");
            selected.TextDecorations = TextDecorations.Underline;
        }

        public void UpdateProgress(double value, string status)
        {


            if (value == 0)
            {
                Home.Instance.loadingpage.Visibility = Visibility.Visible;
            
                Home.Instance.displayView.Visibility = Visibility.Collapsed;

                progress_view.Visibility = Visibility.Collapsed;

                guide_view.Visibility = Visibility.Visible;


            }
            else
            {
                
                progress_view.Visibility = Visibility.Visible;

                guide_view.Visibility = Visibility.Collapsed;
                
            }
            
            
            
            progressValue = Math.Min(Math.Max(value, 0), progressMax);
            ProgressPercent.Text = $"{(int)progressValue}%";


            StatusLabel.Text = status;
            

            // double angle = 360 * (progressValue / progressMax);
            double angle = 359.999 * (progressValue / progressMax);
            
            
            
            // ProgressArc.Data = CreateArcGeometry(100, 100, 88, -90, angle);
            
            ProgressArc.Data = CreateArcGeometry(100, 100, 94, -90, angle);


        }

        private Geometry CreateArcGeometry(double cx, double cy, double radius, double startAngle, double sweepAngle)
        {
            double startRad = startAngle * Math.PI / 180;
            double endRad = (startAngle + sweepAngle) * Math.PI / 180;

            Point startPoint = new Point(cx + radius * Math.Cos(startRad), cy + radius * Math.Sin(startRad));
            Point endPoint = new Point(cx + radius * Math.Cos(endRad), cy + radius * Math.Sin(endRad));

            bool isLarge = sweepAngle > 180;

            var figure = new PathFigure { StartPoint = startPoint };
            figure.Segments.Add(new ArcSegment
            {
                Point = endPoint,
                Size = new Size(radius, radius),
                IsLargeArc = isLarge,
                SweepDirection = SweepDirection.Clockwise
            });

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }
    }
}
