using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using System.Windows.Input;
namespace Androidplayer.Src.Keymap;

public partial class Elements_view : UserControl
{
    
    private Point _startPoint;

    private bool ispressed = false;
    public Elements_view()
    {
        InitializeComponent();
    }
    
    
    
    
 
    private void Button_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton ==  MouseButtonState.Pressed)
        {
           
            
            if (sender is Button button)
            {
                // Use a simple data payload (string or a small DTO). Avoid passing the Button itself.
                var tag = button.Tag?.ToString() ?? "Unknown";
                var data = new DataObject("KeyElementFormat", tag);

                // Optionally set a drag adorner / visual via DragDrop effects (not covered here)
                DragDrop.DoDragDrop(button, data, DragDropEffects.Copy);
            }


            // Console.WriteLine("mouse moving in menuu");
            //
            // DataObject data = new DataObject(typeof(Button), my_Direction);
            // DragDrop.DoDragDrop(my_Direction, data, DragDropEffects.Move);
            
           
        }
        
       
        
    }
    
    
    
    


    // private void My_Direction_OnMouseDown(object sender, MouseButtonEventArgs e)
    // {
    //     ispressed = true;
    //
    //     Console.WriteLine(ispressed);
    // }
    //
    // private void My_Direction_OnMouseUp(object sender, MouseButtonEventArgs e)
    // {
    //     ispressed = false;
    //     Console.WriteLine(ispressed);
    // }

   
}