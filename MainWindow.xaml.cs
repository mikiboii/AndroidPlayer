using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Androidplayer
{
    public partial class MainWindow : Window
    {
        
        private bool _isDragging;
        private Point _startPoint;
        
        private double _originX, _originY;
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Console.WriteLine("printing test");
            
            this.PreviewMouseDown += Global_PreviewMouseDown;
            
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("loaded ui");
        }

        private void Border_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Console.WriteLine($"Border Got Keyboard Focus - Previous: {e.OldFocus?.GetType().Name}, New: {e.NewFocus?.GetType().Name}");
        }

        private void Border_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Console.WriteLine($"Border Lost Keyboard Focus - Previous: {e.OldFocus?.GetType().Name}, New: {e.NewFocus?.GetType().Name}");
        }

        
        private void TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Console.WriteLine($"TextBox Got Keyboard Focus - Previous: {e.OldFocus?.GetType().Name}, New: {e.NewFocus?.GetType().Name}");
        }

        private void TextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Console.WriteLine($"TextBox Lost Keyboard Focus - Previous: {e.OldFocus?.GetType().Name}, New: {e.NewFocus?.GetType().Name}");
        }

        private void TextBox_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Console.WriteLine($"TextBox Preview Lost Keyboard Focus - Previous: {e.OldFocus?.GetType().Name}, New: {e.NewFocus?.GetType().Name}");
        }

        
        
        private void Global_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var clickedElement = e.OriginalSource as DependencyObject;

            Console.WriteLine(clickedElement.GetType().Name);
            // Fast visual walk to see if we clicked a TextBox or inside one
            if (clickedElement is TextBox)
                return;
            // while (clickedElement != null)
            // {
            //     // Console.WriteLine("running....");
            //     if (clickedElement is TextBox)
            //         return; // Don't clear focus if it's inside a TextBox
            //     clickedElement = VisualTreeHelper.GetParent(clickedElement);
            // }

            // Clear focus only if not on a TextBox
            Keyboard.ClearFocus();
        }
        
        
        
        
        
        
        // private void DraggableRectangle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        // {
        //     _isDragging = true;
        //     _startPoint = e.GetPosition(MyCanvas); // Get position relative to the Canvas
        //     DraggableRectangle.CaptureMouse();
        // }
        //
        // private void DraggableRectangle_PreviewMouseMove(object sender, MouseEventArgs e)
        // {
        //     if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        //     {
        //         Point currentPoint = e.GetPosition(MyCanvas);
        //         double deltaX = currentPoint.X - _startPoint.X;
        //         double deltaY = currentPoint.Y - _startPoint.Y;
        //
        //         Canvas.SetLeft(DraggableRectangle, Canvas.GetLeft(DraggableRectangle) + deltaX);
        //         Canvas.SetTop(DraggableRectangle, Canvas.GetTop(DraggableRectangle) + deltaY);
        //
        //         _startPoint = currentPoint; // Update start point for continuous dragging
        //     }
        // }
        //
        // private void DraggableRectangle_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        // {
        //     _isDragging = false;
        //     DraggableRectangle.ReleaseMouseCapture();
        // }
        //
        
      
        
        
        
        private void DraggableRectangle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _startPoint = e.GetPosition(MainGrid);

            // Store current transform position
            _originX = RectTransform.X;
            _originY = RectTransform.Y;

            DraggableRectangle.CaptureMouse();
        }
        
        
        private void DraggableRectangle_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || e.LeftButton != MouseButtonState.Pressed)
                return;

            var current = e.GetPosition(MainGrid);
            var deltaX = current.X - _startPoint.X;
            var deltaY = current.Y - _startPoint.Y;

            RectTransform.X = _originX + deltaX;
            RectTransform.Y = _originY + deltaY;
        }

        private void DraggableRectangle_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            DraggableRectangle.ReleaseMouseCapture();
        }
        
        
        
        
    }
}