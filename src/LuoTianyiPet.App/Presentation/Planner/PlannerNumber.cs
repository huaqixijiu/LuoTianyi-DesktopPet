using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TextBox = System.Windows.Controls.TextBox;
using FontFamily = System.Windows.Media.FontFamily;
namespace LuoTianyiPet.App;

internal sealed class PlannerNumber : StackPanel
{
    private readonly int _max;
    private readonly TextBlock _previous;
    private readonly TextBlock _next;
    private Point _dragOrigin;
    private int _dragValue;
    private bool _dragPending;
    private bool _dragging;
    internal TextBox Input { get; }
    internal int Value => int.TryParse(Input.Text,out int n)?n:-1;

    public PlannerNumber(string name,int value,int max)
    {
        _max=max;Width=140;Margin=new Thickness(4,0,4,0);HorizontalAlignment=System.Windows.HorizontalAlignment.Center;
        _previous=SideValue();_next=SideValue();
        Input=new TextBox{Name=name,Text=Math.Max(0,Math.Min(max,value)).ToString("00"),FontFamily=new FontFamily("Segoe UI"),FontWeight=FontWeights.SemiBold,Foreground=PlannerTheme.Ink,Background=PlannerTheme.AccentSoft,FontSize=25,TextAlignment=TextAlignment.Center,Padding=new Thickness(3,7,3,7),BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(1),Height=62};
        Children.Add(_previous);Children.Add(Input);Children.Add(_next);
        PreviewMouseWheel+=(_,e)=>{Step(e.Delta>0?1:-1);e.Handled=true;};
        PreviewMouseLeftButtonDown+=BeginDrag;PreviewMouseMove+=ContinueDrag;PreviewMouseLeftButtonUp+=EndDrag;LostMouseCapture+=(_,_)=>{_dragPending=false;_dragging=false;};
        Input.PreviewTextInput+=(_,e)=>e.Handled=e.Text.Any(c=>!char.IsDigit(c));
        System.Windows.DataObject.AddPastingHandler(Input,(_,e)=>{if(e.DataObject.GetData(System.Windows.DataFormats.Text) is not string text||text.Any(c=>!char.IsDigit(c)))e.CancelCommand();});
        Input.PreviewKeyDown+=(_,e)=>{if(e.Key is Key.Up or Key.Down){Step(e.Key==Key.Up?1:-1);e.Handled=true;}};
        Input.LostKeyboardFocus+=(_,_)=>Normalize();Input.TextChanged+=(_,_)=>RefreshNeighbors();RefreshNeighbors();
    }

    internal void SetEnabled(bool enabled){IsEnabled=enabled;Opacity=enabled?1:0.48;}
    private TextBlock SideValue()=>new(){Height=44,FontFamily=new FontFamily("Segoe UI"),FontSize=22,Foreground=PlannerTheme.Muted,Opacity=0.55,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private void Step(int delta){int current=Value<0?0:Value;Input.Text=Math.Max(0,Math.Min(_max,current+delta)).ToString("00");Input.CaretIndex=Input.Text.Length;}
    private void Normalize(){int value=Value<0?0:Math.Max(0,Math.Min(_max,Value));Input.Text=value.ToString("00");}
    private void RefreshNeighbors(){int value=Value<0?0:Math.Max(0,Math.Min(_max,Value));int previous=value==0?(_max==24?23:_max):value-1;int next=value>=_max?0:value+1;_previous.Text=previous.ToString("00");_next.Text=next.ToString("00");}
    private void BeginDrag(object sender,MouseButtonEventArgs e){if(e.ChangedButton!=MouseButton.Left)return;_dragOrigin=e.GetPosition(this);_dragValue=Math.Max(0,Value);_dragPending=true;if(!FindTextBox(e.OriginalSource as DependencyObject)){_dragPending=false;_dragging=true;CaptureMouse();e.Handled=true;}}
    private void ContinueDrag(object sender,MouseEventArgs e)
    {
        if((!_dragPending&&!_dragging)||e.LeftButton!=MouseButtonState.Pressed)return;
        double delta=_dragOrigin.Y-e.GetPosition(this).Y;
        if(_dragPending)
        {
            if(Math.Abs(delta)<6)return;
            _dragPending=false;_dragging=true;CaptureMouse();Keyboard.ClearFocus();
        }
        int steps=(int)Math.Round(delta/18d);int next=Math.Max(0,Math.Min(_max,_dragValue+steps));if(next!=Value)Input.Text=next.ToString("00");e.Handled=true;
    }
    private void EndDrag(object sender,MouseButtonEventArgs e){if(e.ChangedButton!=MouseButton.Left)return;if(_dragPending){_dragPending=false;return;}if(!_dragging)return;_dragging=false;ReleaseMouseCapture();e.Handled=true;}
    private static bool FindTextBox(DependencyObject? source){while(source is not null){if(source is TextBox)return true;source=VisualTreeHelper.GetParent(source);}return false;}
}
