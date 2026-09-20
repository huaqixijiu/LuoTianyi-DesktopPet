using System.Windows;
using System.Windows.Input;
using TextBox = System.Windows.Controls.TextBox;

namespace LuoTianyiPet.App;

// Keyboard routing for the three existing roller columns; values are validated on save.
internal sealed class PlannerDurationInput
{
    private readonly TextBox _hour;
    private readonly TextBox _minute;
    private readonly TextBox _second;
    private bool _routing;
    private string? _secondsSequence;
    private string _secondsOriginHour="00";
    private string _secondsOriginMinute="00";

    internal PlannerDurationInput(TextBox hour, TextBox minute, TextBox second)
    {
        _hour=hour;_minute=minute;_second=second;
        foreach(var box in new[]{hour,minute,second})
        {
            box.MaxLength=2;
            box.PreviewTextInput+=(_,e)=>{e.Handled=true;Input(box,e.Text);};
            box.PreviewKeyDown+=(_,e)=>
            {
                if(e.Key==Key.Back&&Backspace(box))e.Handled=true;
                else if(e.Key is Key.Delete or Key.Left or Key.Right or Key.Home or Key.End)_secondsSequence=null;
                if(e.Key==Key.Space)e.Handled=true;
            };
            box.GotKeyboardFocus+=(_,_)=>{if(!_routing){_secondsSequence=null;box.SelectAll();}};
            box.PreviewMouseLeftButtonDown+=(_,_)=>_secondsSequence=null;
            System.Windows.DataObject.AddPastingHandler(box,(_,e)=>
            {
                e.CancelCommand();
                if(e.DataObject.GetData(System.Windows.DataFormats.UnicodeText) is string text)Input(box,text);
            });
        }
    }

    private void Focus(TextBox box,bool selectAll=false)
    {
        _routing=true;
        try{box.Focus();if(selectAll)box.SelectAll();else{box.CaretIndex=box.Text.Length;box.SelectionLength=0;}}
        finally{_routing=false;}
    }

    internal void Input(TextBox source,string text)
    {
        if(text.Length==0||text.Any(c=>c<'0'||c>'9'))return;
        foreach(char digit in text)
        {
            if(source==_second&&_secondsSequence!=null)
            {
                if(_secondsSequence.Length>=6)continue;
                _secondsSequence+=digit;ApplySecondsSequence();continue;
            }
            string candidate=source.Text.Remove(source.SelectionStart,source.SelectionLength).Insert(source.SelectionStart,digit.ToString());
            if(source==_hour)
            {
                if(candidate.Length>2){_hour.Text=candidate.Substring(0,2);_minute.Text=candidate.Substring(2);Focus(_minute);source=_minute;}
                else{_hour.Text=candidate;_hour.CaretIndex=candidate.Length;if(candidate.Length==2){Focus(_minute,true);source=_minute;}}
            }
            else if(source==_minute)
            {
                if(candidate.Length>2){_minute.Text=candidate.Substring(0,2);_second.Text=candidate.Substring(2);Focus(_second);source=_second;}
                else{_minute.Text=candidate;_minute.CaretIndex=candidate.Length;if(candidate.Length==2){Focus(_second,true);source=_second;}}
            }
            else
            {
                if(_secondsSequence==null){_secondsOriginHour=_hour.Text;_secondsOriginMinute=_minute.Text;}
                _secondsSequence=candidate.Length>6?candidate.Substring(0,6):candidate;
                ApplySecondsSequence();
            }
        }
    }

    private void ApplySecondsSequence()
    {
        string digits=_secondsSequence??"";
        if(digits.Length<=2){_hour.Text=_secondsOriginHour;_minute.Text=_secondsOriginMinute;_second.Text=digits;}
        else if(digits.Length<=4)
        {
            _hour.Text=_secondsOriginHour;
            _minute.Text=digits.Substring(0,2);
            _second.Text=digits.Substring(2);
        }
        else
        {
            _hour.Text=digits.Substring(0,digits.Length-4);
            _minute.Text=digits.Substring(digits.Length-4,2);
            _second.Text=digits.Substring(digits.Length-2);
        }
        Focus(_second);
    }

    internal bool Backspace(TextBox source)
    {
        if(source==_second&&_secondsSequence!=null&&source.SelectionLength==0)
        {
            if(_secondsSequence.Length>0){_secondsSequence=_secondsSequence.Substring(0,_secondsSequence.Length-1);ApplySecondsSequence();return true;}
            _secondsSequence=null;
        }
        if(source.SelectionLength!=0||source.CaretIndex!=0)return false;
        var previous=source==_second?_minute:source==_minute?_hour:null;
        if(previous==null)return false;
        if(previous.Text.Length>0)previous.Text=previous.Text.Substring(0,previous.Text.Length-1);
        Focus(previous);return true;
    }
}
