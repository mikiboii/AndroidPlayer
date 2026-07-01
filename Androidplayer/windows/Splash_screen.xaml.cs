// using System.Windows;
//
// namespace Androidplayer.windows;
//
// public partial class Splash_screen : Window
// {
//     public Splash_screen()
//     {
//         InitializeComponent();
//     }
// }


using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Androidplayer.windows
{
    public partial class Splash_screen : Window
    {
        // Global variables for controlling animation
        public double StartScale { get; set; } = 0.5;  // Start size
        public double EndScale { get; set; } = 1.0;    // End size
        public double AnimationDurationSeconds { get; set; } = 3.0; // Duration

        public Splash_screen()
        {
            InitializeComponent();

            Loaded += Splash_screen_Loaded;

            // Set initial scale to avoid jump
            ImageScale.ScaleX = StartScale;
            ImageScale.ScaleY = StartScale;
            
            // Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            // {
            //     AnimateImageZoom();
            // }));
        }

        private void Splash_screen_Loaded(object sender, RoutedEventArgs e)
        {
            // Start animation after layout is ready
            
            
            
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                AnimateImageZoom();
            }));
        }

        private void AnimateImageZoom()
        {
            SplashImage.Visibility = Visibility.Visible;
            var scaleXAnimation = new DoubleAnimation
            {
                From = StartScale,
                To = EndScale,
                Duration = TimeSpan.FromSeconds(AnimationDurationSeconds),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut  }
            };

            var scaleYAnimation = new DoubleAnimation
            {
                From = StartScale,
                To = EndScale,
                Duration = TimeSpan.FromSeconds(AnimationDurationSeconds),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut  }
            };

            ImageScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleXAnimation);
            ImageScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleYAnimation);
        }
    }
}
