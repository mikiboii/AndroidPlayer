using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Androidplayer.Src.Keymap.Keymap_items;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Androidplayer.Src.Keymap.K_store;
using Androidplayer.Src.Keymap.Keymap_items;
using Androidplayer.Store;
using LiteDB;

namespace Androidplayer.Src.Keymap
{
    public class OverlayManager
    {
        private readonly Canvas _canvas;

        public List<Control> DroppedElements { get; } = new List<Control>();

        public static OverlayManager? Instance { get; private set; }

        public double StartScale { get; set; } = 0.5;
        public double EndScale { get; set; } = 1.0;

        public double AnimationDurationSeconds { get; set; } = 3.0;

        private UserControl display_view;

        public OverlayManager(Canvas canvas, UserControl display_view)
        {
            this.display_view = display_view;

            Instance = this;

            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));

            // Avalonia: Enable drop on the canvas
            DragDrop.SetAllowDrop(_canvas, true);

            my_info.Instance.PropertyChanged += my_info_propertychanged;
            k_info.Instance.PropertyChanged += k_info_propertychanged;
        }

        private void my_info_propertychanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(my_info.IsLandscapemode):
                    Dispatcher.UIThread.Post(() =>
                    {
                        rerender_overlay();
                    });
                    break;

                case nameof(my_info.Typing_mode):
                    Dispatcher.UIThread.Post(() =>
                    {
                        // ShowToast("done", "typing mode changed");

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
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (my_info.Instance.TakeScreenshot)
                        {
                            ShowToast("done", "TakeScreenshot!");
                        }
                    });
                    break;
            }
        }

        private void My_Store_propertychanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(My_Store.VideoResolution):
                    Console.WriteLine("#### calling rerender on video resolution changed");
                    break;
            }
        }

        private void k_info_propertychanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(k_info.KeymapMode):
                    rerender_overlay();
                    break;

                case nameof(k_info.DefaultKeymap):
                    keymap_worker.GetInstance().Restart();
                    break;
            }
        }

        // Avalonia drag-enter handler (wire this from XAML or code)
        public void Canvas_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains("KeyElementFormat"))
            {
                e.DragEffects = DragDropEffects.Move;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        // Avalonia drop handler
        public void Canvas_Drop(object? sender, DragEventArgs e)
        {
            e.Handled = true;

            if (e.Data.Contains("KeyElementFormat"))
            {
                string tag = e.Data.Get("KeyElementFormat") as string ?? "";
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

                Control? newElement = tag switch
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
                        Name = tag, KeyName = "",
                        Width = 100, Height = 100,
                        ImageSource = "/Src/Keymap/K_icons/grenade_2.png"
                    },
                    "Mouse up" => new Keymap_items.Image_nokey
                    {
                        Name = tag, KeyName = "",
                        Width = 100, Height = 100,
                        ImageSource = "/Src/Keymap/K_icons/mouse_up.png"
                    },
                    "Mouse down" => new Keymap_items.Image_nokey
                    {
                        Name = tag, KeyName = "",
                        Width = 100, Height = 100,
                        ImageSource = "/Src/Keymap/K_icons/mouse_down.png"
                    },
                    "Mouse right" => new Keymap_items.Image_nokey
                    {
                        Name = tag, KeyName = "",
                        Width = 100, Height = 100,
                        ImageSource = "/Src/Keymap/K_icons/mouse_right.png"
                    },
                    "Grenade vision" => new Keymap_items.Image_nokey
                    {
                        Name = tag, KeyName = "",
                        Width = 100, Height = 100,
                        ImageSource = "/Src/Keymap/K_icons/eye.png"
                    },
                    "Fire" => new Keymap_items.Image_nokey
                    {
                        Name = tag, KeyName = "",
                        Width = 100, Height = 100,
                        ImageSource = "/Src/Keymap/K_icons/bullet_2.png"
                    },
                    "Peak left" => new Keymap_items.Image_key
                    {
                        Name = tag, KeyName = "",
                        Width = 100, Height = 100,
                        ImageSource = "/Src/Keymap/K_icons/arrow-left.png"
                    },
                    "Peak right" => new Keymap_items.Image_key
                    {
                        Name = tag, KeyName = "",
                        Width = 100, Height = 100,
                        ImageSource = "/Src/Keymap/K_icons/arrow-right.png"
                    },
                    _ => null
                };

                if (newElement == null) return;

                Canvas.SetLeft(newElement, dropPos.X);
                Canvas.SetTop(newElement, dropPos.Y);

                _canvas.Children.Add(newElement);

                DroppedElements.Add(newElement);
            }
        }

        private bool key_already_exists(string my_key)
        {
            foreach (var element in OverlayManager.Instance!.DroppedElements)
            {
                if (element is IKeymapElement keyEl)
                {
                    List<string> KeymapElement_parts = new List<string>();
                    List<string> my_key_parts = new List<string>();

                    if (string.IsNullOrEmpty(keyEl.KeyName))
                    {
                        return false;
                    }

                    KeymapElement_parts = keyEl.KeyName.Split('+').ToList();
                    my_key_parts = my_key.Split('+').ToList();

                    if (my_key_parts[0] == KeymapElement_parts[0])
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
            foreach (var element in OverlayManager.Instance!.DroppedElements)
            {
                if (element is IKeymapElement keyEl)
                {
                    if (Name == keyEl.Name)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void Canvas_OnMouseDown(object? sender, PointerPressedEventArgs e)
        {
            var pos = e.GetPosition(_canvas);

            var item = new KeymapElementNew();
            item.Width = 30; item.Height = 30;

            double x = Math.Max(0, Math.Min(pos.X, _canvas.Bounds.Width - 30));
            double y = Math.Max(0, Math.Min(pos.Y, _canvas.Bounds.Height - 30));

            Canvas.SetLeft(item, x);
            Canvas.SetTop(item, y);
            _canvas.Children.Add(item);
            DroppedElements.Add(item);
        }

        #region handling Keymap Json data

        // public void Save_keymap_data()
        // {
        //     Console.WriteLine("called on overlaymanager");
        //
        //     // var allData = new List<Dictionary<string, object>>();
        //
        //     // foreach (var element in DroppedElements)
        //     // {
        //     //     if (element is IKeymapElement keyEl)
        //     //     {
        //     //         var elementData = keyEl.GetJsonData() as Dictionary<string, object>;
        //     //         Console.WriteLine(elementData);
        //     //         if (elementData != null)
        //     //             allData.Add(elementData);
        //     //     }
        //     // }
        //     //
        //     
        //     
        //     
        //     
        //     
        //     var allData = new List<BsonDocument>();
        //
        //     Console.WriteLine($"[Save] DroppedElements count = {DroppedElements.Count}");
        //
        //     foreach (var element in DroppedElements)
        //     {
        //         Console.WriteLine($"[Save] element type = {element?.GetType().FullName}");
        //
        //         if (element is IKeymapElement keyEl)
        //         {
        //             object raw = keyEl.GetJsonData();
        //
        //             if (raw == null)
        //             {
        //                 Console.WriteLine($"[Save] ⚠ GetJsonData returned null for {element.GetType().Name}");
        //                 continue;
        //             }
        //
        //             BsonDocument doc = raw is BsonDocument bd
        //                 ? bd
        //                 : BsonMapper.Global.ToDocument(raw);
        //
        //             Console.WriteLine($"[Save] GetJsonData → {doc.Count} keys");
        //             allData.Add(doc);
        //         }
        //     }
        //     
        //    
        //     
        //     
        //     
        //     // Console.WriteLine($"[Save] DroppedElements count = {DroppedElements.Count}");
        //     //
        //     // foreach (var element in DroppedElements)
        //     // {
        //     //     Console.WriteLine($"[Save] element type = {element?.GetType().FullName}");
        //     //     Console.WriteLine($"[Save] is IKeymapElement = {element is IKeymapElement}");
        //     //
        //     //     if (element is IKeymapElement keyEl)
        //     //     {
        //     //         var elementData = keyEl.GetJsonData() as Dictionary<string, object>;
        //     //         Console.WriteLine($"[Save] GetJsonData returned: {(elementData == null ? "null" : elementData.Count + " keys")}");
        //     //
        //     //         if (elementData != null)
        //     //             allData.Add(elementData);
        //     //     }
        //     // }
        //     
        //     
        //
        //     var editor = my_info.Instance.Dataeditor;
        //     if (editor == null)
        //     {
        //         Console.WriteLine("❌ Dataeditor is null!");
        //         return;
        //     }
        //
        //     var defaultKeymapName = editor.Get("default_keymap")?.AsString;
        //     if (string.IsNullOrEmpty(defaultKeymapName))
        //     {
        //         Console.WriteLine("❌ No default keymap set.");
        //         return;
        //     }
        //
        //     var keymaps = editor.Get("keymaps");
        //     if (keymaps.IsNull || !keymaps.IsArray)
        //     {
        //         Console.WriteLine("❌ No keymaps array found.");
        //         return;
        //     }
        //     //
        //     // var bsonArray = new BsonArray();
        //     // foreach (var elementDict in allData)
        //     //     bsonArray.Add(BsonMapper.Global.ToDocument(elementDict));
        //
        //     
        //     var bsonArray = new BsonArray();
        //     foreach (var doc in allData)
        //         bsonArray.Add(doc);
        //     
        //     
        //     var json = LiteDB.JsonSerializer.Serialize(bsonArray);
        //     Console.WriteLine("=== Keymap JSON ===");
        //     Console.WriteLine(json);
        //     Console.WriteLine("===================");
        //     
        //     var keymapsArray = keymaps.AsArray;
        //     bool updated = false;
        //
        //     foreach (var entry in keymapsArray)
        //     {
        //         if (entry.IsDocument && entry.AsDocument.ContainsKey(defaultKeymapName))
        //         {
        //             entry.AsDocument[defaultKeymapName] = bsonArray;
        //             updated = true;
        //             break;
        //         }
        //     }
        //
        //     if (!updated)
        //     {
        //         var newKeymap = new BsonDocument { [defaultKeymapName] = bsonArray };
        //         keymapsArray.Add(newKeymap);
        //     }
        //
        //     editor.Set("keymaps", keymapsArray);
        //     editor.Save();
        //
        //     Console.WriteLine($"✅ Saved keymap '{defaultKeymapName}' with {allData.Count} items.");
        //
        //     keymap_worker.GetInstance().Restart();
        //
        //     ShowToast("done", "Keymap saved!");
        // }


        
        
        
        public void Save_keymap_data()
{
    Console.WriteLine("called on overlaymanager");

    // 1. Collect the KeymapElement objects
    var allData = new List<KeymapElement>();

    foreach (var element in DroppedElements)
    {
        if (element is IKeymapElement keyEl)
        {
            object raw = keyEl.GetJsonData();
            
            
    
            if (raw is KeymapElement ke)
                allData.Add(ke);
            else if (raw != null)
                Console.WriteLine($"[Save] ⚠ Unexpected type: {raw.GetType().Name}");
        }
    }
    
    
    
    //
    // foreach (var element in DroppedElements)
    // {
    //     if (element is IKeymapElement keyEl)
    //     {
    //         object raw = keyEl.GetJsonData();
    //
    //         Console.WriteLine($"[Save] source={element.GetType().FullName} → raw={raw?.GetType().Name}");
    //
    //         if (raw is KeymapElement ke)
    //         {
    //             Console.WriteLine($"[Save]   Type='{ke.Type}' X={ke.X} Y={ke.Y}");
    //             allData.Add(ke);
    //         }
    //         else if (raw != null)
    //             Console.WriteLine($"[Save] ⚠ Unexpected type: {raw.GetType().Name}");
    //     }
    // }
    
    
    
    // 2. Serialize with System.Text.Json, using the source-generated context
    string json = System.Text.Json.JsonSerializer.Serialize(
        allData,
        KeymapJsonContext.Default.ListKeymapElement);


    // Console.WriteLine(json);

    // 3. Convert JSON string → BsonArray so it fits into LiteDB's config
    var bsonArray = LiteDB.JsonSerializer.Deserialize(json).AsArray;

    var editor = my_info.Instance.Dataeditor;
    if (editor == null) { Console.WriteLine("❌ Dataeditor is null!"); return; }

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

    // 4. Update the correct entry
    var keymapsArray = keymaps.AsArray;
    bool updated = false;

    foreach (var entry in keymapsArray)
    {
        if (entry.IsDocument && entry.AsDocument.ContainsKey(defaultKeymapName))
        {
            entry.AsDocument[defaultKeymapName] = bsonArray;
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
    ShowToast("done", "Keymap saved!");
}
        
        
        
        
        
        #endregion

        #region animation

        // NOTE: Avalonia animation APIs are different from WPF's BeginAnimation.
        //       This is a stub that just toggles visibility/opacity without animating.
        //       Replace with Avalonia's Animation / Transitions later.

        // public void AnimateModeOverlay(string mode)
        // {
        //     var overlay = display_view.FindControl<Border>("ModeOverlay");
        //     var scale = display_view.FindControl<ScaleTransform>("OverlayScale");
        //     var image = display_view.FindControl<Image>("ModeOverlayImage");
        //
        //     if (overlay == null || scale == null || image == null)
        //     {
        //         Console.WriteLine("⚠️ ModeOverlay elements not found.");
        //         return;
        //     }
        //
        //     // Asset URI: avares://<AssemblyName>/<path>
        //     image.Source = new Bitmap(AssetLoader.Open(new Uri(
        //         mode.Equals("keyboard", StringComparison.OrdinalIgnoreCase)
        //             ? "avares://Androidplayer/Icons/Keyboard_2.png"
        //             : "avares://Androidplayer/Icons/Controller_2.png"
        //     )));
        //
        //     overlay.IsVisible = true;
        //     overlay.Opacity = 1;
        //     scale.ScaleX = 0.4;
        //     scale.ScaleY = 0.4;
        //
        //     // TODO: Port the actual animation using Avalonia.Animation.Animation + KeyFrames.
        //     //       See: https://docs.avaloniaui.net/docs/guides/graphics-and-animation/animations
        //
        //     Console.WriteLine($"image size {scale.ScaleX}x{scale.ScaleY}");
        // }
        
        
        public void AnimateModeOverlay(string mode)
        {
            var overlay = display_view.FindControl<Border>("ModeOverlay");
            var image = display_view.FindControl<Image>("ModeOverlayImage");
        
            if (overlay == null || image == null)
            {
                Console.WriteLine("⚠️ ModeOverlay elements not found.");
                return;
            }
        
            image.Source = new Bitmap(AssetLoader.Open(new Uri(
                mode.Equals("keyboard", StringComparison.OrdinalIgnoreCase)
                    ? "avares://Androidplayer/Icons/Keyboard_2.png"
                    : "avares://Androidplayer/Icons/Controller_2.png"
            )));
        
            overlay.IsVisible = true;
            overlay.Opacity = 1;
        
            if (overlay.RenderTransform is ScaleTransform scale)
            {
                scale.ScaleX = 0.4;
                scale.ScaleY = 0.4;
            }
        
            Console.WriteLine("Mode overlay shown.");
        }

        #endregion

        private T? FindChild<T>(Visual parent, string childName) where T : Control
        {
            if (parent == null) return null;

            foreach (var child in parent.GetVisualChildren())
            {
                if (child is Control c)
                {
                    if (c is T element && c.Name == childName)
                        return element;

                    var result = FindChild<T>(c, childName);
                    if (result != null)
                        return result;
                }
            }
            return null;
        }

        public void ShowToast(string mode, string message)
        {
            if (!_canvas.IsInitialized)
            {
                Console.WriteLine("canvas not initialized");
                return;
            }

            var toastBorder = display_view.FindControl<Border>("ToastMessage");
            var toastText = display_view.FindControl<TextBlock>("ToastText");
            var toasticon = display_view.FindControl<Image>("Toasticon");

            if (toastBorder == null || toastText == null || toasticon == null)
            {
                Console.WriteLine("⚠️ Toast elements not found in canvas.");
                return;
            }

            toasticon.Source = new Bitmap(AssetLoader.Open(new Uri(
                mode.Equals("warning", StringComparison.OrdinalIgnoreCase)
                    ? "avares://Androidplayer/Icons/warning.png"
                    : "avares://Androidplayer/Icons/checkmark.png"
            )));

            toastText.Text = message;

            toastBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double left = (_canvas.Bounds.Width - toastBorder.DesiredSize.Width) / 2;
            double top = 20;
            Canvas.SetLeft(toastBorder, left);
            Canvas.SetTop(toastBorder, top);

            // Panel.SetZIndex(toastBorder, 9999);
           
            
        
            toastBorder.ZIndex = 9999;

            // TODO: Avalonia toast animation — use Animation.RunAsync or Transitions.
            //       For now just make it visible and set opacity.
            toastBorder.Opacity = 1;
            toastBorder.IsVisible = true;

            // Optional: hide after a delay using DispatcherTimer
            DispatcherTimer.RunOnce(() =>
            {
                toastBorder.IsVisible = false;
            }, TimeSpan.FromSeconds(2.5));
        }

        public void rerender_overlay()
        {
            if (string.IsNullOrEmpty(k_info.Instance.DefaultKeymap))
            {
                return;
            }

            var allElements = KeyMapManager.Instance?.GetAllElements();
            if (allElements == null)
            {
                Console.WriteLine("⚠️ No keymap loaded yet.");
                return;
            }

            
            // var list = allElements.ToList();
            // Console.WriteLine($"[Rerender] allElements.Count = {list.Count}");
            //
            // for (int i = 0; i < list.Count; i++)
            // {
            //     var el = list[i];
            //     Console.WriteLine($"[Rerender] el[{i}] Type='{el.Type}', X={el.X}, Y={el.Y}, W={el.Width}, H={el.Height}, Keys=[{(el.Keys == null ? "null" : string.Join(",", el.Keys))}]");
            // }
            //
            //
            
            foreach (var element in DroppedElements.ToList())
            {
                _canvas.Children.Remove(element);
            }
            DroppedElements.Clear();

            int index = 0;
            foreach (var el in allElements)
            {
                index++;

                if (my_info.Instance?.IsLandscapemode != true &&
                    my_info.Instance?.DeveloperMode != true)
                {
                    Console.WriteLine("######## blocked rerender call");
                    return;
                }

                if (el.ParentHeight != My_Store.Instance?.DeviceHeight)
                {
                    if (index == 1)
                    {
                        ShowToast("warning", "The current Keymap data was created For a different device. " +
                                  "please create a new keymap. or select a diffrent one");
                    }
                }

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
                        _ => new Keymap_items.Keymap_element_normal()
                    };

                    var control = newElement as Control;
                    if (control != null)
                    {
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
                        _ => new Keymap_items.Image_nokey()
                    };

                    var control = newElement as Control;
                    if (control != null)
                    {
                        _canvas.Children.Add(control);
                        DroppedElements.Add(control);
                     
                        newElement.SetJsonData(el);
                    }
                }
            }
        }

        public void ClearAllElements() => _canvas.Children.Clear();

        public void RemoveElement(Control element)
        {
            if (_canvas.Children.Contains(element))
            {
                _canvas.Children.Remove(element);
                DroppedElements.Remove(element);
            }
        }
    }
}