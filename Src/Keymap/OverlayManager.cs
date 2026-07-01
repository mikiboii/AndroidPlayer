using System;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Androidplayer.Src.Keymap.K_store;
using Androidplayer.Src.Keymap.Keymap_items;
using Androidplayer.Store;
using LiteDB;

namespace Androidplayer.Src.Keymap
{
    public class OverlayManager
    {
        private readonly Canvas _canvas;
        
        public List<UIElement> DroppedElements { get; } = new List<UIElement>();

        public static OverlayManager? Instance { get; private set; }
        
        
        public double StartScale { get; set; } = 0.5;  // Start size
        public double EndScale { get; set; } = 1.0;    // End size
        
        public double AnimationDurationSeconds { get; set; } = 3.0;
        
        private  UserControl display_view;
        
        public OverlayManager(Canvas canvas ,UserControl display_view)
        {
            
            this.display_view = display_view;
            
            Instance = this;
            
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));

            // Ensure Canvas is ready for drag and drop
            _canvas.AllowDrop = true;
            // _canvas.DragEnter += Canvas_DragEnter;
            // _canvas.Drop += Canvas_Drop;

            // My_Store.Instance.PropertyChanged += My_Store_propertychanged;


            my_info.Instance.PropertyChanged += my_info_propertychanged;
            k_info.Instance.PropertyChanged += k_info_propertychanged;
            
            
            // if (_canvas.FindName("ModeOverlay") is not Border overlay ||
            //     _canvas.FindName("OverlayScale") is not ScaleTransform scale ||
            //     _canvas.FindName("ModeOverlayImage") is not Image image)
            // {
            //     Console.WriteLine("⚠️ ModeOverlay elements not found.");
            //     return;
            // }
            // image.ScaleX = StartScale;
            // image.ScaleY = StartScale;
            //
            // image.ScaleX 
            
        }

        private void my_info_propertychanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                // case nameof(My_Store.VideoWidth):
                case nameof(my_info.IsLandscapemode):

                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        rerender_overlay();
                        
                    });

                    
                    
                    break;
                
                case nameof(my_info.Typing_mode):

                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ShowToast("done","typing mode changed");

                        if (my_info.Instance.Typing_mode)
                        {
                            AnimateModeOverlay("keyboard");
                        }
                        else
                        {
                            AnimateModeOverlay("gaming");
                        }
                        
                        
                    });


                    
    
                    break;
                case nameof(my_info.TakeScreenshot):

                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // ShowToast("typing mode changed");

                        if (my_info.Instance.TakeScreenshot)
                        {
                            ShowToast("done","TakeScreenshot!");
                        }
                       
                        
                    });


                    
    
                    break;
                
 
            }
        }

        private void My_Store_propertychanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                // case nameof(My_Store.VideoWidth):
                case nameof(My_Store.VideoResolution):

                    
                   

                    Console.WriteLine("#### calling rerender  on video resolution changed ");
    
                    break;
                
                
 
            }
          
        }

       

        private void k_info_propertychanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                // case nameof(My_Store.VideoWidth):
                case nameof(k_info.KeymapMode):

                    
                    rerender_overlay();
    
                    break;
                
                case nameof(k_info.DefaultKeymap):
                    


                    keymap_worker.GetInstance().Restart();
                    
                    break;
                
                
 
            }
        }

        public void Canvas_DragEnter(object sender, DragEventArgs e)
        {
            // if (!e.Data.GetDataPresent("KeyElementFormat"))
            //     e.Effects = DragDropEffects.None;
            // else
            //     e.Effects = DragDropEffects.Copy;
            
            if (e.Data.GetDataPresent(DataFormats.Serializable))
            {
                e.Effects = DragDropEffects.Move; // Indicate a move effect
            }
            else
            {
                e.Effects = DragDropEffects.None; // Indicate no drop allowed
            }
            
            e.Handled = true;
        }

        public void Canvas_Drop(object sender, DragEventArgs e)
        {
            
            // if (e.Data.GetDataPresent(DataFormats.Serializable))
            // {
            //     // Retrieve the dropped data
            //     object droppedData = e.Data.GetData(DataFormats.Serializable); 
            //     // Process the data (e.g., add to a collection, update UI)
            // }
            
            
            e.Handled = true;
            
            
            
            
            if (e.Data.GetDataPresent("KeyElementFormat"))
            {
                string tag = e.Data.GetData("KeyElementFormat") as string;
                Console.WriteLine($"Element dropped $$$ — {tag}");

                Point dropPos = e.GetPosition(_canvas);

                if (tag == "Direction")
                {
                    if (key_already_exists("wasd"))
                    {
                        return;
                        
                    }
                }


                if (Name_already_exists(tag))
                {
                    
                    return;
                    
                }

                
                

                // Example: create different controls based on tag
                UIElement newElement = tag switch
                {
                    "Direction" => new Keymap_items.Direction_keymap
                    {
                        Name = tag,
                        KeyName = "wasd",
                        Width = 100, Height = 100
                    },
                    "Visual" => new Keymap_items.Visual_keymap
                    {
                        Name = tag,
                        KeyName = "",
                        Width = 100, Height = 100
                    },
                    
                    "Grenade" => new Keymap_items.Image_key
                    {
                        Name = tag,
                        KeyName = "",
                        Width = 100,
                        Height = 100,
                        ImageSource = "../K_icons/grenade_2.png" // ✅ Set your image here
                    },
                    "Mouse up" => new Keymap_items.Image_nokey
                    {
                        Name = tag,
                        KeyName = "",
                        Width = 100,
                        Height = 100,
                        ImageSource = "../K_icons/mouse_up.png" // ✅ Set your image here
                    },
                    "Mouse down" => new Keymap_items.Image_nokey
                    {
                        Name = tag,
                        KeyName = "",
                        Width = 100,
                        Height = 100,
                        ImageSource = "../K_icons/mouse_down.png" // ✅ Set your image here
                    },
                    "Mouse right" => new Keymap_items.Image_nokey
                    {
                        Name = tag,
                        KeyName = "",
                        Width = 100,
                        Height = 100,
                        ImageSource = "../K_icons/mouse_right.png" // ✅ Set your image here
                    },
                     "Grenade vision" => new Keymap_items.Image_nokey
                                        {
                                            Name = tag,
                                            KeyName = "",
                                            Width = 100,
                                            Height = 100,
                                            ImageSource = "../K_icons/eye.png" // ✅ Set your image here
                                        },
                    
                    "Fire" => new Keymap_items.Image_nokey
                    {
                        Name = tag,
                        KeyName = "",
                        Width = 100,
                        Height = 100,
                        ImageSource = "../K_icons/bullet_2.png" // ✅ Set your image here
                    },
                    "Peak left" => new Keymap_items.Image_key
                    {
                        Name = tag,
                        KeyName = "",
                        Width = 100,
                        Height = 100,
                        ImageSource = "../K_icons/arrow-left.png" // ✅ Set your image here
                    },
                    
                    "Peak right" => new Keymap_items.Image_key
                    {
                        Name = tag,
                        KeyName = "",
                        Width = 100,
                        Height = 100,
                        ImageSource = "../K_icons/arrow-right.png" // ✅ Set your image here
                    },
                    // _ => new Button { Content = tag, Width = 100, Height = 30 }
                };

                // Position and add it to the Canvas
                Canvas.SetLeft(newElement, dropPos.X);
                Canvas.SetTop(newElement, dropPos.Y);

                _canvas.Children.Add(newElement);
                
                DroppedElements.Add(newElement);
                
                
            }
        }
        
        private bool key_already_exists(string my_key)
        {

            foreach (var element in OverlayManager.Instance.DroppedElements)
            {
                if (element is IKeymapElement keyEl)
                {
                    // Console.WriteLine($"Key: {keyEl.KeyName}");
                    // Console.WriteLine($"Key: {keyEl.Name}");

                    // string[] KeymapElement_parts;
                    List<string> KeymapElement_parts = new List<string>();
                    List<string> my_key_parts = new List<string>();


                    if (keyEl.KeyName == null || keyEl.KeyName == "")
                    {
                        return false;
                    }

            

                    
                    
                    KeymapElement_parts = keyEl.KeyName.Split('+').ToList();
                    my_key_parts = my_key.Split('+').ToList();
                    
                    
                  
                    if ( my_key_parts[0] == KeymapElement_parts[0])
                    {
                        return true;
                    }
                    
                    if (my_key == "wasd")
                    {
                        if (KeymapElement_parts.Any(k => k == "W" || k == "A" || k == "S" || k == "D"))
                        {
                            Console.WriteLine("Contains a WASD key!");

                            return true;
                        }
                                    
                                    
                    }
            
           
                    
                   
                    
                }
            }

                
    

            return false;


        }



        private bool Name_already_exists(string Name)
        {

            foreach (var element in OverlayManager.Instance.DroppedElements)
            {
                if (element is IKeymapElement keyEl)
                {
                    
                    if ( Name == keyEl.Name)
                    {
                        return true;
                    }
                    
                }
            }

            return false;
        }
        
        
        
        
        
        public void Canvas_OnMouseDown(object? sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(_canvas);
        
            // double x = pos.X;
            // double y = pos.Y;

            // Console.WriteLine($"from overlay manager {x} : {y}");

            // Console.WriteLine($" {MainImage.ActualHeight} printing from display view");
        
            var item = new KeymapElementNew();
            item.Width = 30; item.Height = 30;
            
            
            double x = Math.Max(0, Math.Min(pos.X, _canvas.ActualWidth - 30));
            double y = Math.Max(0, Math.Min(pos.Y, _canvas.ActualHeight - 30));
            
            Canvas.SetLeft(item, x);
            Canvas.SetTop(item, y);
            _canvas.Children.Add(item);
            DroppedElements.Add(item);

            // item.RemoveRequested += (s, ev) =>
            // {
            //     _canvas.Children.Remove(item);
            //     DroppedElements.Remove(item);
            // };

        
        
        }





        #region handling Keymap Json data


     
        
        public void Save_keymap_data()
        {
            Console.WriteLine("called on overlaymanager");

            var allData = new List<Dictionary<string, object>>();

            foreach (var element in DroppedElements)
            {
                if (element is IKeymapElement keyEl)
                {
                    var elementData = keyEl.GetJsonData() as Dictionary<string, object>;
                    if (elementData != null)
                        allData.Add(elementData);
                }
            }

            var editor = my_info.Instance.Dataeditor;
            if (editor == null)
            {
                Console.WriteLine("❌ Dataeditor is null!");
                return;
            }

            var defaultKeymapName = editor.Get("default_keymap")?.AsString;
            if (string.IsNullOrEmpty(defaultKeymapName))
            {
                Console.WriteLine("❌ No default keymap set.");
                return;
            }

            var keymaps = editor.Get("keymaps");
            if (keymaps.IsNull || !keymaps.IsArray)
            {
                Console.WriteLine("❌ No keymaps array found.");
                return;
            }

            // ✅ Convert List<Dictionary> → BsonArray (direct array of elements)
            var bsonArray = new BsonArray();
            foreach (var elementDict in allData)
                bsonArray.Add(BsonMapper.Global.ToDocument(elementDict));

            // ✅ Update the correct keymap entry
            var keymapsArray = keymaps.AsArray;
            bool updated = false;

            foreach (var entry in keymapsArray)
            {
                if (entry.IsDocument && entry.AsDocument.ContainsKey(defaultKeymapName))
                {
                    entry.AsDocument[defaultKeymapName] = bsonArray; // directly store array
                    updated = true;
                    break;
                }
            }

            if (!updated)
            {
                var newKeymap = new BsonDocument { [defaultKeymapName] = bsonArray };
                keymapsArray.Add(newKeymap);
            }

            editor.Set("keymaps", keymapsArray);
            editor.Save();

            Console.WriteLine($"✅ Saved keymap '{defaultKeymapName}' with {allData.Count} items.");

            keymap_worker.GetInstance().Restart();
            
            
            
            
            ShowToast("done","Keymap saved!");
            
        }



        #region animation

         



        
//      public void AnimateModeOverlay(string mode)
// {
//     if (_canvas.FindName("ModeOverlay") is not Border overlay ||
//         _canvas.FindName("OverlayScale") is not ScaleTransform scale ||
//         _canvas.FindName("ModeOverlayImage") is not Image image)
//     {
//         Console.WriteLine("⚠️ ModeOverlay elements not found.");
//         return;
//     }
//
//     // Set icon
//     image.Source = new BitmapImage(new Uri(
//         mode.Equals("keyboard", StringComparison.OrdinalIgnoreCase)
//             ? "pack://application:,,,/Icons/Keyboard_test.png"
//             : "pack://application:,,,/Icons/Controller.png"
//     ));
//
//     overlay.Visibility = Visibility.Visible;
//
//     // Reset starting values
//     overlay.Opacity = 1;
//     scale.ScaleX = 0.5;
//     scale.ScaleY = 0.5;
//
//     // --- Zoom-in animation ---
//     var zoomX = new DoubleAnimation
//     {
//         From = 0.5,
//         To = 1.0,
//         Duration = TimeSpan.FromMilliseconds(450),
//         EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
//         // FillBehavior = FillBehavior.Stop
//     };
//
//     var zoomY = new DoubleAnimation
//     {
//         From = 0.5,
//         To = 1.0,
//         Duration = TimeSpan.FromMilliseconds(450),
//         EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
//         // FillBehavior = FillBehavior.Stop
//     };
//
//     scale.BeginAnimation(ScaleTransform.ScaleXProperty, zoomX);
//     scale.BeginAnimation(ScaleTransform.ScaleYProperty, zoomY);
//
//     // --- Delay before fade-out (replaces BeginTime which breaks Completed) ---
//     var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
//     delay.Tick += (s, e) =>
//     {
//         delay.Stop();
//
//         // --- Fade-out animation ---
//         var fadeOut = new DoubleAnimation
//         {
//             From = 1,
//             To = 0,
//             Duration = TimeSpan.FromMilliseconds(250),
//             EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
//             FillBehavior = FillBehavior.Stop
//         };
//
//         fadeOut.Completed += (s2, e2) =>
//         {
//             Console.WriteLine("✔ fade out complete");
//
//             // Hide overlay so reset is invisible
//             overlay.Visibility = Visibility.Collapsed;
//
//             // Remove animations
//             scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
//             scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
//             overlay.BeginAnimation(Border.OpacityProperty, null);
//
//             // Reset scale on next UI tick -> 100% invisible
//             Application.Current.Dispatcher.BeginInvoke(new Action(() =>
//             {
//                 scale.ScaleX = 0.5;
//                 scale.ScaleY = 0.5;
//             }),
//             DispatcherPriority.Background);
//         };
//
//         overlay.BeginAnimation(Border.OpacityProperty, fadeOut);
//     };
//
//     delay.Start();
// }





        

        public void AnimateModeOverlay(string mode)
        {
            // if (_canvas.FindName("ModeOverlay") is not Border overlay ||
            //     _canvas.FindName("OverlayScale") is not ScaleTransform scale ||
            //     _canvas.FindName("ModeOverlayImage") is not Image image)
            // {
            //     Console.WriteLine("⚠️ ModeOverlay elements not found.");
            //     return;
            // }
        
            var overlay = (Border)display_view.FindName("ModeOverlay");
            var scale   = (ScaleTransform)display_view.FindName("OverlayScale");
            var image   = (Image)display_view.FindName("ModeOverlayImage");
            if (overlay == null || scale == null || image == null)
            {
                            Console.WriteLine("⚠️ ModeOverlay elements not found.");
                            return;
            }
            // Set icon
            // image.Source = new BitmapImage(new Uri(
            //     mode.Equals("keyboard", StringComparison.OrdinalIgnoreCase)
            //         ? "pack://application:,,,/Icons/Keyboard_test.png"
            //         : "pack://application:,,,/Icons/Controller.png"
            // ));

            // Console.WriteLine($"overlay width = {
            //     overlay.ActualWidth
            // }");
            // Console.WriteLine($"display width = {
            //     display_view.
            // }");
            
            overlay.BeginAnimation(Border.OpacityProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            
            // image.Source = new BitmapImage(new Uri(
            //     mode.Equals("keyboard", StringComparison.OrdinalIgnoreCase)
            //         ? "pack://application:,,,/Icons/Keyboard_test.png"
            //         : "pack://application:,,,/Icons/Controller.png"
            // ));
            
            image.Source = new BitmapImage(new Uri(
                mode.Equals("keyboard", StringComparison.OrdinalIgnoreCase)
                    ? "pack://application:,,,/Icons/Keyboard_2.png"
                    : "pack://application:,,,/Icons/Controller_2.png"
            ));
        
            overlay.Visibility = Visibility.Visible;
        
        
            
            
            // Reset overlay opacity and scale
            overlay.Opacity = 1;
            // scale.ScaleX = 0.5; // start smaller for smooth zoom
            // scale.ScaleY = 0.5;
            
            
            
            Console.WriteLine($"image size {scale.ScaleX}x{scale.ScaleY}");
        
            // --- Fade in ---
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            // overlay.BeginAnimation(Border.OpacityProperty, fadeIn);
        
            // --- Fade out ---
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                BeginTime = TimeSpan.FromMilliseconds(150), // stays visible ~1.6 sec
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            
            fadeOut.Completed += (s2, e2) =>
             {
                 Console.WriteLine("✔ fade out complete");
        
                 // Hide overlay so reset is invisible
                 overlay.Visibility = Visibility.Collapsed;
        
                 // Remove animations
                 scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                 scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                 overlay.BeginAnimation(Border.OpacityProperty, null);
        
                 // Reset scale on next UI tick -> 100% invisible
                 Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                 {
                     scale.ScaleX = 0.2;
                     scale.ScaleY = 0.2;
                 }),
                 DispatcherPriority.Background);
             };
            overlay.BeginAnimation(Border.OpacityProperty, fadeOut);
        
            // --- Zoom-in animation (like Splash screen) ---
            var scaleXAnimation = new DoubleAnimation
            {
                From = scale.ScaleX,
                To = 0.4, // final scale
                Duration = TimeSpan.FromMilliseconds(450),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                // FillBehavior = FillBehavior.Stop
               
            };
            var scaleYAnimation = new DoubleAnimation
            {
                From = scale.ScaleY,
                To = 0.4,
                Duration = TimeSpan.FromMilliseconds(450),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                // FillBehavior = FillBehavior.Stop
                
            };
        
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);
            //
            // scaleYAnimation.Completed += (s, e) =>
            // {
            //
            //     scale.ScaleX = 0.2;
            //     scale.ScaleY = 0.2;
            // };
            //
        }
        
        

//
//
// public void AnimateModeOverlay(string mode)
// {
//     // Find XAML elements
//     if (_canvas.FindName("ModeOverlay") is not Border overlay ||
//         _canvas.FindName("OverlayScale") is not ScaleTransform scale ||
//         _canvas.FindName("OverlayText") is not TextBlock text)
//     {
//         Console.WriteLine("⚠️ Overlay elements not found.");
//         return;
//     }
//
//     text.Text = mode;
//
//     // 🔥 1. CANCEL ALL PREVIOUS ANIMATIONS IMMEDIATELY
//     overlay.BeginAnimation(Border.OpacityProperty, null);
//     scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
//     scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
//
//     // 🔥 2. SET STARTING VALUES BEFORE ANY ANIMATION
//     overlay.Visibility = Visibility.Visible;
//     overlay.Opacity = 1;
//     scale.ScaleX = 0.5;
//     scale.ScaleY = 0.5;
//
//     // 🔥 3. ZOOM-IN ANIMATION (450ms)
//     DoubleAnimation scaleAnim = new DoubleAnimation
//     {
//         From = 0.5,
//         To = 1.0,
//         Duration = TimeSpan.FromMilliseconds(450),
//         EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
//         FillBehavior = FillBehavior.Stop   // important!
//     };
//
//     // 🔥 4. When zoom finishes → DO NOT REVERT SCALE
//     scaleAnim.Completed += (s, e) =>
//     {
//         // Freeze at final value instead of reverting
//         scale.ScaleX = 1.0;
//         scale.ScaleY = 1.0;
//
//         // 🔥 Start fade-out AFTER zoom completes
//         DoubleAnimation fade = new DoubleAnimation
//         {
//             From = 1,
//             To = 0,
//             Duration = TimeSpan.FromMilliseconds(350),
//             EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
//             FillBehavior = FillBehavior.Stop
//         };
//
//         fade.Completed += (s2, e2) =>
//         {
//             overlay.Opacity = 0;
//             overlay.Visibility = Visibility.Collapsed;
//         };
//
//         overlay.BeginAnimation(Border.OpacityProperty, fade);
//     };
//
//     // Start the zoom
//     scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
//     scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
// }
//



            

        #endregion
        
        
        
        
        private T FindChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i) as FrameworkElement;
        
                if (child != null)
                {
                    if (child is T element && child.Name == childName)
                        return element;
                
                    var result = FindChild<T>(child, childName);
                    if (result != null)
                        return result;
                }
            }
            return null;
        }
        

        public void ShowToast(string mode , string message)
        {
            
            if (!_canvas.IsInitialized)
            {
                

                Console.WriteLine("canvas not initialized");
                return;
            
            }
            
            var toastBorder = (Border)display_view.FindName("ToastMessage");
            var toastText   = (TextBlock)display_view.FindName("ToastText");
            var toasticon   = (Image)display_view.FindName("Toasticon");
            // Find other elements similarly...
    
            if (toastBorder == null || toastText == null || toasticon == null)
            {
                Console.WriteLine("⚠️ Toast elements not found in canvas.");
                return;
            }
            else
            {
                Console.WriteLine("showing Toast message.");
            }
            
            // var toastBorder1 = (Border)this.FindName("ToastMessage");
            // var toastText1   = (TextBlock)this.FindName("ToastText");
            // var toasticon1   = (Image)this.FindName("Toasticon");
            // // Find other elements similarly...
            //
            // if (toastBorder1 == null || toastText1 == null || toasticon1 == null)
            // {
            //     Console.WriteLine("⚠️ Toast elements not found in canvas.");
            //     return;
            // }
            //
            // Find the Border and TextBlock defined in XAML
            // if (_canvas.FindName("ToastMessage") is not Border toastBorder ||
            //     _canvas.FindName("ToastText") is not TextBlock toastText || _canvas.FindName("Toasticon") is not Image toasticon)
            // {
            //     Console.WriteLine("⚠️ Toast elements not found in canvas.");
            //     return;
            // }

            toasticon.Source = new BitmapImage(new Uri(
                mode.Equals("warning", StringComparison.OrdinalIgnoreCase)
                    ? "pack://application:,,,/Icons/warning.png"
                    : "pack://application:,,,/Icons/checkmark.png"
            ));
            
            // Set the message text
            toastText.Text = message;

            // Measure and center horizontally
            toastBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double left = (_canvas.ActualWidth - toastBorder.DesiredSize.Width) / 2;
            double top = 20; // fixed top margin
            Canvas.SetLeft(toastBorder, left);
            Canvas.SetTop(toastBorder, top);

            // Bring to front
            Panel.SetZIndex(toastBorder, 9999);

            // Cancel previous animations
            toastBorder.BeginAnimation(UIElement.OpacityProperty, null);

            // Create fade in/out
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400))
            {
                BeginTime = TimeSpan.FromSeconds(2)
            };

            var sb = new Storyboard();
            sb.Children.Add(fadeIn);
            sb.Children.Add(fadeOut);
            Storyboard.SetTarget(fadeIn, toastBorder);
            Storyboard.SetTarget(fadeOut, toastBorder);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));

            sb.Begin();
        }

        
        
        
        




        public void rerender_overlay()
        {

            if (k_info.Instance.DefaultKeymap == null ||  k_info.Instance.DefaultKeymap == "")
            {
                
                return;
            }

            
            var allElements = KeyMapManager.Instance?.GetAllElements();
            if (allElements == null)
            {
                Console.WriteLine("⚠️ No keymap loaded yet.");
                return;
            }

            // Console.WriteLine($"🔹 Total elements: {allElements.Count}");

            // Console.WriteLine($" canvas size  {_canvas.ActualWidth } x {_canvas.ActualHeight}");
            
            
            foreach (var element in DroppedElements.ToList())
            {
                _canvas.Children.Remove(element);
            }
            DroppedElements.Clear();


            int index = 0;
            // Example: Loop through all
            foreach (var el in allElements)
            {
                // string keys = el.Keys != null && el.Keys.Count > 0 ? string.Join("+", el.Keys) : "(none)";
                //
                //
                //
                // Console.WriteLine($"[{el.Type}]  Keys={keys}  Pos=({el.X}, {el.Y})  Size=({el.Width}x{el.Height})");

                index++;
                
                
                if (my_info.Instance?.IsLandscapemode != true && 
                    my_info.Instance?.DeveloperMode != true)
                {
                    // your code
                    
                    Console.WriteLine("######## blocked rerender call");
                    
                    return;
                }
                
                
                
                
                if (el.ParentHeight != My_Store.Instance?.DeviceHeight)
                {

                    // Console.WriteLine($" json height : {el.ParentHeight}, device height : {My_Store.Instance?.DeviceHeight}");
                    
                    // string json = System.Text.Json.JsonSerializer.Serialize(allElements, new JsonSerializerOptions { WriteIndented = true });
                    // Console.WriteLine(json);
                    //


                    if (index == 1)
                    {
                        ShowToast("warning","The current Keymap data was created For a different device. " +
                                  "please create a new keymap. or select a diffrent one");
                        
                    }

               
                }

                

                // Console.WriteLine("######## rerender called");
                
                
                if (!k_info.Instance.KeymapMode)
                {
                    
                    
                    IKeymapElement newElement = el.Type switch
                    {
                        "Direction" => new Keymap_items.Direction_keymap_normal(),
                        "Visual" => new Keymap_items.Keymap_element_normal(),
                        "Grenade" => new Keymap_items.Keymap_element_normal(),
                        "Regular key" => new Keymap_items.Keymap_element_normal(),
                        "Multi key" => new Keymap_items.Keymap_element_normal(),
                        "Mouse up" => new Keymap_items.Image_nokey_normal(),
                        "Mouse down" => new Keymap_items.Image_nokey_normal(),
                        "Mouse right" => new Keymap_items.Image_nokey_normal(),
                        "Grenade vision" => new Keymap_items.Image_nokey_normal(),
                        "Fire" => new Keymap_items.Image_nokey_normal(),
                        "Peak left" => new Keymap_items.Keymap_element_normal(),
                        "Peak right" => new Keymap_items.Keymap_element_normal(),
                        // _ => new Keymap_items.Image_nokey_normal()
                    };

                    // ✅ Pass JSON data to UI element

                    // Add to canvas
                    var control = newElement as UIElement;
                    if (control != null)
                    {
                        // Console.WriteLine("elements added");
                        _canvas.Children.Add(control);
                        DroppedElements.Add(control);
                        newElement.SetJsonData(el);
                    }
                    
                    
                    
                    
                }
                else
                {
                    
                
                
                    IKeymapElement newElement = el.Type switch
                    {
                        "Direction" => new Keymap_items.Direction_keymap(),
                        "Visual" => new Keymap_items.Visual_keymap(),
                        "Grenade" => new Keymap_items.Image_key(),
                        "Regular key" => new Keymap_items.KeymapElementNew(),
                        "Multi key" => new Keymap_items.KeymapElementNew(),
                        "Mouse up" => new Keymap_items.Image_nokey(),
                        "Mouse down" => new Keymap_items.Image_nokey(),
                        "Mouse right" => new Keymap_items.Image_nokey(),
                        "Grenade vision" => new Keymap_items.Image_nokey(),
                        "Fire" => new Keymap_items.Image_nokey(),
                        "Peak left" => new Keymap_items.Image_key(),
                        "Peak right" => new Keymap_items.Image_key(),
                        // _ => new Keymap_items.Image_nokey()
                    };

                    // ✅ Pass JSON data to UI element

                    // Add to canvas
                    var control = newElement as UIElement;
                    if (control != null)
                    {
                        // Console.WriteLine("elements added");
                        _canvas.Children.Add(control);
                        DroppedElements.Add(control);
                        newElement.SetJsonData(el);
                    }

                // Console.WriteLine($"[Overlay] Added {el.Type} at ({el.X},{el.Y})");
              
                }
                
                
            }
            
            
            
            
           
            
            
        }
        
        
        
        
        

        #endregion
        
        
        
        
        
        
        

        // Optional: utility methods for managing elements later
        public void ClearAllElements() => _canvas.Children.Clear();

        public void RemoveElement(UIElement element)
        {
            if (_canvas.Children.Contains(element))
            {
                
                _canvas.Children.Remove(element);
            
                DroppedElements.Remove(element);
            }
            
            
            
            
        }
    }
}
