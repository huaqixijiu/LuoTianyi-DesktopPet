using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LuoTianyiPet.Core;
using Button = System.Windows.Controls.Button;

namespace LuoTianyiPet.App;

public partial class MainWindow
{
    private enum ReminderCardAction { KeepDue, SkipOccurrence, Finish, Snooze }

    private static string RemainingUntil(DateTime at)
    {
        int minutes=Math.Max(0,(int)Math.Ceiling((at-DateTime.Now).TotalMinutes));
        return minutes==0?"即将到点":$"还有{minutes}分钟";
    }

    private static string SnoozeRemaining(DateTime at)
    {
        int minutes=Math.Max(0,(int)Math.Ceiling((at-DateTime.Now).TotalMinutes));
        return minutes==0?"即将再次提醒":$"还有{minutes}分钟";
    }

    private void RenderReminderCard(ReminderBook book,IReadOnlyList<ReminderOccurrence> occurrences)
    {
        if(_reminderCard==null||occurrences.Count==0)return;
        _capsuleRemaining.Clear();
        var first=occurrences[0];
        var firstItem=book.Items.FirstOrDefault(i=>i.Id==first.RuleId);
        if(firstItem==null)return;
        bool due=first.Phase==ReminderPhase.Due;
        bool snoozed=first.Phase==ReminderPhase.DueSnoozed;
        _headerReminderTarget=due?null:first.SnoozeAt??first.At;
        _headerReminderSnoozed=snoozed;
        string status=due?"时间到了":snoozed?SnoozeRemaining(_headerReminderTarget!.Value):RemainingUntil(_headerReminderTarget!.Value);
        string title=ReminderEngine.Label(firstItem)+(occurrences.Count>1?$" · 还有{occurrences.Count-1}项":"");
        double s=_reminderCard.UiScale;
        StackPanel body=new(){Margin=new Thickness(22*s,0,22*s,27*s)};
        foreach(var o in occurrences)
        {
            var item=book.Items.FirstOrDefault(i=>i.Id==o.RuleId);
            if(item==null)continue;
            if(o!=first)
            {
                body.Children.Add(new Border{Height=1,Background=PlannerTheme.Line,Margin=new Thickness(0,17*s,0,20*s)});
                body.Children.Add(new TextBlock{Text=ReminderEngine.Label(item),FontSize=Math.Max(12,17*s),FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
                DateTime target=o.SnoozeAt??o.At;
                TextBlock remaining=new(){FontSize=Math.Max(11,13*s),Foreground=PlannerTheme.WorkActionFill,Margin=new Thickness(0,3*s,0,0)};
                body.Children.Add(remaining);_capsuleRemaining.Add((remaining,target,o.Phase==ReminderPhase.DueSnoozed));
            }
            else body.Children.Add(new Border{Height=1,Background=PlannerTheme.ReminderLine,Margin=new Thickness(0,0,0,23*s)});
            if(!string.IsNullOrWhiteSpace(item.Notes))
            {
                double lineHeight=Math.Max(17,21*s);
                body.Children.Add(new TextBlock{
                    Name="ReminderNotesPreview",Text=item.Notes,FontSize=Math.Max(12,16*s),
                    TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis,
                    LineStackingStrategy=LineStackingStrategy.BlockLineHeight,LineHeight=lineHeight,
                    MaxHeight=3*lineHeight,Foreground=PlannerTheme.Ink,
                    Margin=new Thickness(0,0,0,18*s)});
            }
            else if(o.Phase is ReminderPhase.Early or ReminderPhase.Due)
                body.Children.Add(new TextBlock{Text=o.Phase==ReminderPhase.Early?$"将于 {o.At:HH:mm} 正式提醒":$"设定时间 {o.At:HH:mm}",
                    FontSize=Math.Max(11,14*s),Foreground=PlannerTheme.Muted,Margin=new Thickness(0,0,0,28*s)});
            bool narrow=_reminderCard.ExpandedWidth<210;
            var actions=new Grid();
            if(narrow){actions.RowDefinitions.Add(new());actions.RowDefinitions.Add(new());}
            else {actions.ColumnDefinitions.Add(new());actions.ColumnDefinitions.Add(new());}
            ReminderCardAction left=o.Phase==ReminderPhase.Early?ReminderCardAction.KeepDue:
                o.Phase==ReminderPhase.Due?ReminderCardAction.Finish:ReminderCardAction.SkipOccurrence;
            string leftText=o.Phase==ReminderPhase.Early?"到点再提醒":o.Phase==ReminderPhase.Due?"结束本次":"跳过本次";
            AddReminderAction(actions,o,left,leftText,0,true,narrow);
            if(o.Phase is ReminderPhase.Early or ReminderPhase.Due)
            {
                ReminderCardAction right=o.Phase==ReminderPhase.Early?ReminderCardAction.SkipOccurrence:ReminderCardAction.Snooze;
                string rightText=o.Phase==ReminderPhase.Early?"本次不再提醒":"10分钟后提醒";
                AddReminderAction(actions,o,right,rightText,1,false,narrow);
            }
            else
            {
                if(!narrow)
                {
                    actions.ColumnDefinitions[0].Width=new GridLength(1,GridUnitType.Star);
                    actions.ColumnDefinitions[1].Width=new GridLength(1,GridUnitType.Star);
                }
            }
            body.Children.Add(actions);
        }
        _reminderCard.Present(title,status,body,_quickReminderExpanded);
    }

    private void AddReminderAction(Grid grid,ReminderOccurrence occurrence,ReminderCardAction action,string label,int column,bool primary,bool narrow)
    {
        Button button=new(){Name=action switch {
                ReminderCardAction.KeepDue=>"KeepDueReminder",ReminderCardAction.SkipOccurrence=>"SkipOccurrence",
                ReminderCardAction.Finish=>"AcknowledgeReminder",_=>"SnoozeReminder"},
            Content=label,MinHeight=Math.Max(36,50*_reminderCard!.UiScale),FontSize=Math.Max(_reminderCard.ExpandedWidth<210?11:12,15*_reminderCard.UiScale),FontWeight=FontWeights.SemiBold,
            Padding=new Thickness((_reminderCard.ExpandedWidth<210?6:8)*_reminderCard.UiScale,4*_reminderCard.UiScale,(_reminderCard.ExpandedWidth<210?6:8)*_reminderCard.UiScale,4*_reminderCard.UiScale),
            Margin=narrow?new Thickness(0,column==0?0:8*_reminderCard.UiScale,0,0):new Thickness(column==0?0:(_reminderCard.ExpandedWidth<210?4:7)*_reminderCard.UiScale,0,0,0),
            Background=primary?PlannerTheme.ReminderAccent:Brushes.White,
            Foreground=primary?Brushes.White:PlannerTheme.Ink,
            BorderBrush=primary?PlannerTheme.ReminderAccent:PlannerTheme.ReminderLine,
            BorderThickness=new Thickness(1)};
        button.ToolTip=action switch {
            ReminderCardAction.KeepDue=>"关闭这次提前提醒，设定时间仍会提醒",
            ReminderCardAction.SkipOccurrence=>"这一次提前和到点都不再提醒，不影响之后的重复提醒",
            ReminderCardAction.Finish=>"结束这一次到点提醒",
            _=>"十分钟后再次提醒这一次"};
        button.Click+=async(_,_)=>{button.IsEnabled=false;await ApplyReminderActionAsync(occurrence,action,button);};
        if(narrow)Grid.SetRow(button,column);else Grid.SetColumn(button,column);
        grid.Children.Add(button);
    }

    private async Task ApplyReminderActionAsync(ReminderOccurrence occurrence,ReminderCardAction action,Button button)
    {
        if(_reminders==null)return;
        var before=ReminderEngine.CaptureUndo(_reminders.Book,occurrence.RuleId,occurrence.At);
        if(before==null)return;
        ReminderPhase after=action switch {
            ReminderCardAction.KeepDue=>ReminderPhase.AcknowledgedEarly,
            ReminderCardAction.SkipOccurrence=>ReminderPhase.Cancelled,
            ReminderCardAction.Finish=>ReminderPhase.Done,
            _=>ReminderPhase.DueSnoozed};
        string message=action switch {
            ReminderCardAction.KeepDue=>"提前提醒已关闭",
            ReminderCardAction.SkipOccurrence=>"本次所有提醒已关闭",
            ReminderCardAction.Finish=>"本次提醒已结束",
            _=>"10分钟后再次提醒"};
        try
        {
            _reminderFeedback=new(message,before.Value,after,DateTime.Now.AddSeconds(5));
            await _reminders.ChangeAsync(b=>{
                switch(action)
                {
                    case ReminderCardAction.KeepDue:ReminderEngine.Acknowledge(b,occurrence.RuleId,occurrence.At,occurrence.Phase);break;
                    case ReminderCardAction.SkipOccurrence:ReminderEngine.Cancel(b,occurrence.RuleId,occurrence.At);break;
                    case ReminderCardAction.Finish:ReminderEngine.Acknowledge(b,occurrence.RuleId,occurrence.At,ReminderPhase.Due);break;
                    case ReminderCardAction.Snooze:ReminderEngine.Snooze(b,occurrence.RuleId,occurrence.At,ReminderPhase.Due,DateTime.Now);break;
                }
            });
            _reminderCardKey="";
            RefreshReminderCard();
        }
        catch
        {
            _reminderFeedback=null;button.Content="保存失败，请重试";button.IsEnabled=true;
            RefreshReminderCard();
        }
    }

    private void ShowReminderFeedback(ReminderFeedbackState feedback)
    {
        if(_reminderCard==null)return;
        _headerReminderTarget=null;
        Grid content=new(){Margin=new Thickness(18,15,16,15)};
        content.ColumnDefinitions.Add(new(){Width=new GridLength(31)});
        content.ColumnDefinitions.Add(new());
        content.ColumnDefinitions.Add(new(){Width=new GridLength(1)});
        content.ColumnDefinitions.Add(new(){Width=new GridLength(58)});
        content.Children.Add(new Border{Width=23,Height=23,CornerRadius=new CornerRadius(12),
            BorderBrush=PlannerTheme.ReminderAccent,BorderThickness=new Thickness(1.5),
            Child=new TextBlock{Text="✓",Foreground=PlannerTheme.ReminderAccent,
                HorizontalAlignment=System.Windows.HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}});
        TextBlock message=new(){Text=feedback.Message,FontSize=15,FontWeight=FontWeights.SemiBold,
            VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(message,1);content.Children.Add(message);
        Border divider=new(){Background=PlannerTheme.Line,Margin=new Thickness(0,4,0,4)};
        Grid.SetColumn(divider,2);content.Children.Add(divider);
        Button undo=new(){Name="UndoReminderAction",Content="撤销",Foreground=PlannerTheme.ReminderAccent,
            Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(3),FontSize=14};
        Grid.SetColumn(undo,3);content.Children.Add(undo);
        undo.Click+=async(_,_)=>{undo.IsEnabled=false;await UndoReminderActionAsync(feedback,undo);};
        _reminderCard.PresentFeedback(content);
        PositionReminderCard();_reminderCard.Show();_reminderCard.UpdateLayout();PositionReminderCard();
    }

    private async Task UndoReminderActionAsync(ReminderFeedbackState feedback,Button button)
    {
        if(_reminders==null||_reminderFeedback!=feedback||DateTime.Now>=feedback.Until)return;
        try
        {
            bool restored=false;
            await _reminders.ChangeAsync(b=>restored=ReminderEngine.TryUndo(b,feedback.Before,feedback.After,DateTime.Now));
            _reminderFeedback=null;_reminderCardKey="";
            _quickReminderExpanded=true;_restoreExpandedOnce=true;
            if(restored)RefreshReminderCard();
            else {button.Content="无法撤销";RefreshReminderCard();}
        }
        catch {button.Content="撤销失败";button.IsEnabled=true;}
    }
}
