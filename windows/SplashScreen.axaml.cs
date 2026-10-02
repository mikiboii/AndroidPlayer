
using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Androidplayer.windows
{
    public partial class SplashScreen : Window
    {
        public double StartScale { get; set; } = 0.5;
        public double EndScale { get; set; } = 1.0;
        public double AnimationDurationSeconds { get; set; } = 3.0;

        private readonly ScaleTransform _imageScale = new();

        public SplashScreen()
        {
            InitializeComponent();

            _imageScale.ScaleX = StartScale;
            _imageScale.ScaleY = StartScale;
            SplashImage.RenderTransform = _imageScale;

            Opened += OnOpened;
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(AnimateImageZoom, DispatcherPriority.Loaded);
        }

        private void AnimateImageZoom()
        {
            SplashImage.IsVisible = true;

            var duration = TimeSpan.FromSeconds(AnimationDurationSeconds);
            var easing = new CubicEaseOut();

            var scaleXAnimation = new Animation
            {
                Duration = duration,
                Easing = easing,
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0d),
                        Setters = { new Setter(ScaleTransform.ScaleXProperty, StartScale) } },
                    new KeyFrame { Cue = new Cue(1d),
                        Setters = { new Setter(ScaleTransform.ScaleXProperty, EndScale) } }
                }
            };

            var scaleYAnimation = new Animation
            {
                Duration = duration,
                Easing = easing,
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0d),
                        Setters = { new Setter(ScaleTransform.ScaleYProperty, StartScale) } },
                    new KeyFrame { Cue = new Cue(1d),
                        Setters = { new Setter(ScaleTransform.ScaleYProperty, EndScale) } }
                }
            };

            // ✅ Run against the Image (a Visual), NOT against _imageScale
            _ = scaleXAnimation.RunAsync(SplashImage);
            _ = scaleYAnimation.RunAsync(SplashImage);
        }
    }
}