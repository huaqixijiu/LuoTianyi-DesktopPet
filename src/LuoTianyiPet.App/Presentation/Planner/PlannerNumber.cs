using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using FontFamily = System.Windows.Media.FontFamily;
using Cursors = System.Windows.Input.Cursors;
namespace LuoTianyiPet.App;
internal sealed class PlannerNumber : StackPanel
{
    internal TextBox Input {get;}
    internal int Value => int.TryParse(Input.Text,out int n)?n:-1;
    public PlannerNumber(string name,int value,int max)
    {
        Margin=new Thickness(7,0,7,0);
        Input=new TextBox { Name=name,Text=value.ToString("00"),FontFamily=new FontFamily("Segoe UI"),FontWeight=FontWeights.SemiBold,Foreground=PlannerTheme.Ink,Background=Brushes.Transparent,FontSize=28,TextAlignment=TextAlignment.Center,Padding=new Thickness(4),BorderThickness=new Thickness(0) };
        Button up=StepButton("chevron-up",1);
        Button down=StepButton("chevron-down",-1);
        void Step(int d) { Input.Text=Math.Max(0,Math.Min(max,Value+d)).ToString("00"); }
        Button StepButton(string kind,int delta)
        {
            Button b=new(){Content=PlannerTheme.Icon(kind,16,PlannerTheme.Accent,0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(4),Height=24,MinWidth=28,Cursor=Cursors.Hand};
            b.Click+=(_,_)=>Step(delta);
            return b;
        }
        Border card=new(){Background=PlannerTheme.Soft,CornerRadius=new CornerRadius(10),BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(1),Padding=new Thickness(6,4,6,4)};
        Children.Add(up);Children.Add(card);Children.Add(down);
        card.Child=Input;
        PreviewMouseWheel+=(_,e)=>{Step(e.Delta>0?1:-1);e.Handled=true;};
        Input.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Up || e.Key==Key.Down){Step(e.Key==Key.Up?1:-1);e.Handled=true;}};
    }
}
