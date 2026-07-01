using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Input;
using Androidplayer.Store;


namespace Androidplayer.Src.Keymap.Keymap_items
{
    
    // public interface IKeymapElement
    // {
    //     string Name { get; set; }
    //     string KeyName { get; set; }
    //     
    //     object GetJsonData();
    // }

    public partial class Keymap_element_normal : UserControl , IKeymapElement
    {
        // Public properties for parent tracking (similar to original_width/original_height/original_x/original_y)
        public double OriginalParentWidth { get; private set; }
        public double OriginalParentHeight { get; private set; }
        public double OriginalX { get; private set; }
        public double OriginalY { get; private set; }

        
        public double my_width { get; private set; }
        public double my_height { get; private set; }
        
        public string Name { get; set; }
        public string KeyName { get; set; } = "";
        //
        
        
        
        public bool IsEditing { get; private set; } = false;
        
        private List<string> my_keys = new List<string>();

        // Shortcut capture
        private HashSet<Key> modifiersPressed = new HashSet<Key>();
        private Key? firstKey = null;
        private Key? secondKey = null;
        private int maxKeys = 2;

        // Dragging
        private bool isDragging = false;
        private Point dragStartPoint;

        
        public Keymap_element_normal()
        {
            InitializeComponent();
            Loaded += KeymapElementNew_Loaded;

      
            
            // Visual tweaks
            this.Cursor = Cursors.Arrow;


        }

      


        private void KeymapElementNew_Loaded(object sender, RoutedEventArgs e)
        {
            // If parent is a Canvas, capture original parent size
            if (this.Parent is Canvas c)
            {
                OriginalParentWidth = c.ActualWidth;
                OriginalParentHeight = c.ActualHeight;

                // store initial center position as OriginalX/Y similar to your Qt code (center)
                OriginalX = Canvas.GetLeft(this) + (this.ActualWidth / 2.0);
                OriginalY = Canvas.GetTop(this) + (this.ActualHeight / 2.0);
                
                c.SizeChanged += Parent_SizeChanged;
                
                
            }
        }
        
        
        private void Parent_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double newWidth = e.NewSize.Width;
            double newHeight = e.NewSize.Height;

            // update element position
            FixPosOnParentResize(newWidth, newHeight);
        }
        
        

      
       
        
    
      

      
        
        private void DisplayKey_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Add padding space
            double newWidth = DisplayKey.ActualWidth + 20;
            if (newWidth < 30)
            {
                this.Width = 30;
            }
            else
            {
                
                this.Width = newWidth;
                
            }

            // Console.WriteLine("size changed !");
        }

        
       

     


      

        #region Parent-resize repositioning (FixPos equivalent)
        /// <summary>
        /// Call this from the parent when parent canvas size changes to reposition the element similar to your Qt fix_pos logic.
        /// </summary>
        /// <param name="newParentWidth"></param>
        /// <param name="newParentHeight"></param>
        ///
        
        
        
      

        
        // public void FixPosOnParentResize(double newParentWidth, double newParentHeight)
        // {
        //     // Validate we have valid stored values
        //     if (OriginalParentWidth <= 0 || OriginalParentHeight <= 0)
        //         return;
        //
        //     // Compute scaling factors
        //     double scaleX = newParentWidth / OriginalParentWidth;
        //     double scaleY = newParentHeight / OriginalParentHeight;
        //
        //     // Scale original center position
        //     double newCenterX = OriginalX * scaleX;
        //     double newCenterY = OriginalY * scaleY;
        //
        //     // Convert to top-left position
        //     double newLeft = newCenterX - (my_width / 2.0);
        //     double newTop = newCenterY - (my_height / 2.0);
        //
        //
        //     // Keep element inside canvas boundaries
        //     if (Parent is Canvas c)
        //     {
        //         newLeft = Math.Max(0, Math.Min(newLeft, c.ActualWidth - my_width));
        //         newTop = Math.Max(0, Math.Min(newTop, c.ActualHeight - my_height));
        //     }
        //
        //
        //
        //
        // if (Name != "Multi key" && Name != "Regular key")
        // {
        //     
        //      Console.WriteLine($"{KeyName} : {Name}");
        //     double offsetX = (my_width - this.Width) / 2.0;
        //     double offsetY = (my_height / 2.0);
        //     
        //     double d_width = DisplayKey.ActualWidth;
        //     
        //     // Console.WriteLine($"{my_height} : {d_width}");
        //
        //     double demo_offset = (my_height - 30) / 2.0;
        //
        //     newLeft += offsetX;
        //     newTop += demo_offset;
        //     
        //     
        //     
        //     
        // }
        //     
        //     //
        //     // if (my_height > ActualHeight || my_width > ActualWidth)
        //     // {
        //     // double offsetX = (my_width - ActualWidth) / 2.0;
        //     // double offsetY = (my_height / 3.0);
        //     //
        //     // double demo_offset = (my_height - ActualHeight) / 2.0;
        //     //
        //     // // newLeft += offsetX;
        //     // newTop += demo_offset;
        //     // }
        //
        //     Canvas.SetLeft(this, newLeft);
        //     Canvas.SetTop(this, newTop);
        //     
        //     
        //     
        //     
        // }

        
        public void FixPosOnParentResize(double newParentWidth, double newParentHeight)
        {
            // Validate we have valid stored values
            if (OriginalParentWidth <= 0 || OriginalParentHeight <= 0)
                return;

            // Compute scaling factors
            double scaleX = newParentWidth / OriginalParentWidth;
            double scaleY = newParentHeight / OriginalParentHeight;

            // Scale original center position
            double newCenterX = OriginalX * scaleX;
            double newCenterY = OriginalY * scaleY;

            // Convert to top-left position
            double newLeft = newCenterX - (ActualWidth / 2.0);
            double newTop = newCenterY - (ActualHeight / 2.0);

            // Keep element inside canvas boundaries
            if (Parent is Canvas c)
            {
                newLeft = Math.Max(0, Math.Min(newLeft, c.ActualWidth - ActualWidth));
                newTop = Math.Max(0, Math.Min(newTop, c.ActualHeight - ActualHeight));
            }
            
            
            if (Name != "Multi key" && Name != "Regular key")
            {
            
                // Console.WriteLine($"{KeyName} : {Name}");
                double offsetX = (my_width - this.Width) / 2.0;
                double offsetY = (my_height / 2.0);
            
                double d_width = DisplayKey.ActualWidth;
            
                // Console.WriteLine($"{my_height} : {d_width}");
        
                double demo_offset = (my_height - 30) / 2.0;
        
                newLeft += offsetX;
                newTop += demo_offset;
            
            
            
            
            }

            Canvas.SetLeft(this, newLeft);
            Canvas.SetTop(this, newTop);
            
            
            
            
        }
        
        
        
        
        #endregion

        #region Serialization helpers (get_json_data / set_info)
      
        
      
        
        public object GetJsonData()
        {
            double parentW = OriginalParentWidth;
            double parentH = OriginalParentHeight;

            if (this.Parent is Canvas c)
            {
                parentW = c.ActualWidth;
                parentH = c.ActualHeight;
            }

            double x = Canvas.GetLeft(this);
            double y = Canvas.GetTop(this);

            List<string> keys = new();
            if (!string.IsNullOrEmpty(KeyName))
                keys = KeyName.Split('+', StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim())
                    .ToList();

            var data = new Dictionary<string, object>
            {
                ["type"] = Name,
                ["keys"] = keys,
                ["x"] = x,
                ["y"] = y,
                ["width"] = this.ActualWidth,
                ["height"] = this.ActualHeight,
                ["parent_width"] = parentW,
                ["parent_height"] = parentH,
                ["Img path"] = null,
                ["App name"] = my_info.Instance?.Appname
            };

         
            return data;
        }

        
        
        
       
        public void SetJsonData(KeymapElement data)
        {
            try
            {
         
                Name = data.Type;
                KeyName = string.Join("+", data.Keys);
                // Width = data.Width;
                // Height = data.Height;

                
                // this.Width = data.Width;



                my_width = data.Width;
                my_height = data.Height;
                
                
                if (data.Type == "Regular key")
                {
                    this.Width = data.Width;
                    this.Height = data.Height;
                    
                    my_width = data.ScaledWidth;
                    my_height = data.ScaledHeight;
                }
                
                
                
                DisplayKey.Text = KeyName;
               

                OriginalParentWidth = data.ParentWidth;
                OriginalParentHeight = data.ParentHeight;

                // OriginalX = data.X + (data.Width / 2.0);
                // OriginalY = data.Y + (data.Height / 2.0);
               
                
                
                
                
                
                // OriginalX = data.X  + (my_width / 2.0);
                // OriginalY = data.Y  + (my_height / 2.0);
                
                OriginalX = data.X  + (this.ActualWidth / 2.0);
                OriginalY = data.Y  + (this.ActualHeight / 2.0);
                
                // OriginalX = data.X ;
                // OriginalY = data.Y ;
                

                // if (Parent is Canvas canvas)
                // {
                //     Canvas.SetLeft(this, data.X);
                //     Canvas.SetTop(this, data.Y);
                // }
                
                // FixPosOnParentResize(OriginalParentWidth, OriginalParentHeight);

                if (this.Parent is Canvas parentCanvas)
                    FixPosOnParentResize(parentCanvas.ActualWidth, parentCanvas.ActualHeight);

                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ SetJsonData failed for {Name}: {ex.Message}");
            }
        }


        

        
        #endregion


        private void Keymap_element_normal_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Console.WriteLine("km pressed");
        }
    }
}



