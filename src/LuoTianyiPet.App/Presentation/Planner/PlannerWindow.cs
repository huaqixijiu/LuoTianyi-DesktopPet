using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using LuoTianyiPet.Core;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Brush = System.Windows.Media.Brush;
namespace LuoTianyiPet.App;
internal sealed partial class PlannerWindow : Window
{
    private readonly ReminderService _service;
    private readonly Grid _shell=new();
    private readonly DockPanel _root=new();
    private readonly StackPanel _header=new();
    private readonly ScrollViewer _body=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    private readonly StackPanel _footer=new();
    private readonly TextBlock _status=Text("",12);
    private readonly DispatcherTimer _clock=new(){Interval=TimeSpan.FromSeconds(1)};
    private readonly List<(TextBlock Text,ReminderItem Item)> _running=[];
    private readonly HashSet<DateTime> _selected=[];
    private readonly HashSet<Guid> _selectedGroups=[];
    private readonly Dictionary<DateTime, Border> _calendarDateBorders=[];
    private DateTime _date=DateTime.Today;
    private DateTime? _occurrenceDate;
    private bool _alarm,_editing,_batch,_manage,_details=true;
    private bool _calendarDragActive;
    private bool _calendarDragMoved;
    private readonly HashSet<DateTime> _calendarDragVisited=[];
    private bool _suppressCalendarClick;
    private DateTime? _calendarDragStartDay;
    private Point _calendarDragLastPoint;
    private static readonly string[] Repeats=["仅一次","每天","每周指定","指定日期","工作日 · 跟随日历","休息日 · 跟随日历"];
    private static readonly DayOfWeek[] Days=[DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday,DayOfWeek.Saturday,DayOfWeek.Sunday];
    public event Action? ReminderSettingsRequested;
    public PlannerWindow(ReminderService service,bool alarm)
    {
        _service=service;_alarm=alarm;PlannerTheme.Apply(this);
        Title="洛天依 · 小天的时光";Width=Math.Min(1200,SystemParameters.WorkArea.Width-24);Height=Math.Min(810,SystemParameters.WorkArea.Height-24);
        WindowStyle=WindowStyle.None;
        System.Windows.Shell.WindowChrome.SetWindowChrome(this,new(){CaptionHeight=0,ResizeBorderThickness=new Thickness(5),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(12)});
        _body.Margin=new Thickness(12,0,12,0);_footer.Margin=new Thickness(20,8,20,10);
        MinWidth=Math.Min(1040,Width);MinHeight=Math.Min(640,Height);WindowStartupLocation=WindowStartupLocation.CenterScreen;
        FontFamily=new System.Windows.Media.FontFamily("Microsoft YaHei UI");FontSize=14;Foreground=PlannerTheme.Ink;Background=new LinearGradientBrush(Color.FromRgb(239,249,255),Colors.White,90);
        Language=System.Windows.Markup.XmlLanguage.GetLanguage("zh-CN");UseLayoutRounding=true;
        DockPanel.SetDock(_header,Dock.Top);_root.Children.Add(_header);DockPanel.SetDock(_footer,Dock.Bottom);_root.Children.Add(_footer);_root.Children.Add(_body);_shell.Children.Add(_root);Content=_shell;
        _service.Changed+=OnChanged;_clock.Tick+=(_,_)=>UpdateRemaining();IsVisibleChanged+=(_,_)=>{if(IsVisible)_clock.Start();else _clock.Stop();};
        Closed+=(_,_)=>{_clock.Stop();_service.Changed-=OnChanged;};Render();
    }
    public void Navigate(bool alarm){_alarm=alarm;_manage=false;_editing=false;Render();Show();Activate();}
    internal void OpenItem(Guid id,DateTime? occurrence=null){var item=_service.Book.Items.FirstOrDefault(i=>i.Id==id);if(item!=null){_date=(occurrence??item.Start).Date;_occurrenceDate=occurrence??item.Start;_details=true;Navigate(!item.Calendar);Edit(item,item.Calendar);}}
    private void OnChanged(){if(!_editing)Render();}
    private async Task Execute(Action<ReminderBook> action){try{await _service.ChangeAsync(action);_status.Text="已保存到本机";}catch{_status.Text="保存失败，原数据保留。";throw;}}
    private static TextBlock Text(string value,double size=14,FontWeight? weight=null,Brush? foreground=null)
    {
        var block=new TextBlock{Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(3),Foreground=foreground??(size<=12?PlannerTheme.Muted:PlannerTheme.Ink)};
        if(weight is FontWeight value2)block.FontWeight=value2;
        return block;
    }
    private static StackPanel Row()=>new(){Orientation=Orientation.Horizontal};
    private Button Action(string label,Action action){Button b=new(){Content=label,Margin=new Thickness(3),Padding=new Thickness(14,8,14,8),MinHeight=40,FontSize=14,VerticalAlignment=VerticalAlignment.Center};b.Click+=(_,_)=>action();return b;}
    private Button AsyncAction(string label,Func<Task> action)=>Action(label,async()=>{try{await action();}catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){_status.Text=e is ArgumentException?e.Message:"保存失败，原数据保留。";}});
    private static Button Primary(Button b){b.Background=PlannerTheme.PrimaryFill;b.Foreground=Brushes.White;b.BorderThickness=new Thickness(0);return b;}
    private Button Chip(string label,Action action){Button b=new(){Content=label,Style=(Style)FindResource("PlannerChip"),Margin=new Thickness(3),VerticalAlignment=VerticalAlignment.Center};b.Click+=(_,_)=>action();return b;}
    private Button AsyncChip(string label,Func<Task> action)=>Chip(label,async()=>{try{await action();}catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){_status.Text=e is ArgumentException?e.Message:"保存失败，原数据保留。";}});
    private Button IconButton(string kind,Action action,string? tip=null,double size=18,Brush? color=null)
    {
        Button b=new(){Style=(Style)FindResource("PlannerIconBtn"),Content=PlannerTheme.Icon(kind,size,color??PlannerTheme.Ink,0),Padding=new Thickness(6)};
        if(tip!=null)b.ToolTip=tip;
        b.Click+=(_,_)=>action();
        return b;
    }
    private Button Link(string kind,string label,Action action)
    {
        Button b=new(){Style=(Style)FindResource("PlannerLink"),Margin=new Thickness(0,6,0,2)};
        StackPanel row=Row();row.Children.Add(PlannerTheme.Icon(kind,14,PlannerTheme.Accent,6));row.Children.Add(Text(label,13,foreground:PlannerTheme.Accent));
        b.Content=row;b.Click+=(_,_)=>action();
        return b;
    }
    private static Border Card(UIElement child)=>new(){Child=child,Background=Brushes.White,BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(9),Margin=new Thickness(2,5,2,5)};
    internal static string Remaining(DateTime at){var t=at-DateTime.Now;return t<=TimeSpan.Zero?"已到时间":t.TotalDays>=1?$"还有{(int)t.TotalDays}天{t.Hours}小时":$"剩余{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";}
    private void UpdateRemaining(){foreach(var pair in _running){var sec=ReminderEngine.Remaining(pair.Item,DateTime.Now);var t=TimeSpan.FromSeconds(sec);pair.Text.Text=$"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";}}
    private void UpdateDateStatus(DateTime day)=>_status.Text=$"{day:yyyy年M月d日 dddd}  {CalendarLabels.FullLunar(day)}";
    private void Render()
    {
        _calendarDragVisited.Clear();
        while(_shell.Children.Count>1)_shell.Children.RemoveAt(_shell.Children.Count-1);_root.IsEnabled=true;_header.Children.Clear();_footer.Children.Clear();_running.Clear();
        DockPanel brand=new(){Margin=new Thickness(18,10,14,8)};
        StackPanel windows=Row();
        foreach(var entry in new[]{("minimize",(Action)(()=>WindowState=WindowState.Minimized),""),("maximize",(Action)(()=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized),""),("close",(Action)Close,"close")})
        {
            var b=IconButton(entry.Item1,entry.Item2,entry.Item1=="minimize"?"最小化":entry.Item1=="maximize"?"最大化":"关闭",16);
            if(entry.Item3=="close")b.Style=(Style)FindResource("PlannerCloseBtn");
            b.Width=44;b.Height=32;windows.Children.Add(b);
        }
        DockPanel.SetDock(windows,Dock.Right);brand.Children.Add(windows);
        try{var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.UriSource=RuntimeAssetLocator.PackUri("app/luotianyi-pet.png");bitmap.DecodePixelWidth=128;bitmap.EndInit();var avatar=new System.Windows.Controls.Image{Width=50,Height=50,Margin=new Thickness(0,0,12,0),Source=bitmap};RenderOptions.SetBitmapScalingMode(avatar,BitmapScalingMode.HighQuality);DockPanel.SetDock(avatar,Dock.Left);brand.Children.Add(avatar);}catch{}
        StackPanel identity=new();var brandName=Text("洛天依 · 小天的时光",19,FontWeights.SemiBold);identity.Children.Add(brandName);identity.Children.Add(Text("在每一个平凡的日子里，与你相遇。",13));brand.Children.Add(identity);brand.MouseLeftButtonDown+=(_,e)=>{if(e.ClickCount==2)WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;else if(e.LeftButton==System.Windows.Input.MouseButtonState.Pressed)DragMove();};_header.Children.Add(brand);
        DockPanel tabs=new(){Margin=new Thickness(16,0,16,0)};var settings=IconButton("gear",()=>ShowPlannerSettings(),"通知与闹钟设置",20);settings.Margin=new Thickness(6,0,0,0);DockPanel.SetDock(settings,Dock.Right);tabs.Children.Add(settings);
        StackPanel pages=Row();
        foreach(var entry in new[]{("日历",false,"calendar"),("闹钟",true,"clock")})
        {
            bool selected=_alarm==entry.Item2;
            var tab=Action(entry.Item1,()=>Navigate(entry.Item2));tab.Width=118;tab.Height=45;tab.Background=Brushes.Transparent;tab.BorderThickness=new Thickness(0);tab.FontSize=16;tab.FontWeight=FontWeights.SemiBold;
            StackPanel label=Row();label.Children.Add(PlannerTheme.Icon(entry.Item3,20,selected?PlannerTheme.Accent:PlannerTheme.Ink,8));label.Children.Add(Text(entry.Item1,16,FontWeights.SemiBold,selected?PlannerTheme.Accent:PlannerTheme.Ink));tab.Content=label;
            pages.Children.Add(new Border{Child=tab,BorderThickness=new Thickness(0,0,0,2),BorderBrush=selected?PlannerTheme.Accent:Brushes.Transparent,Margin=new Thickness(0,0,12,0)});
        }
        tabs.Children.Add(pages);_header.Children.Add(new Border{Child=tabs,BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(0,1,0,1),Margin=new Thickness(0,0,0,12)});
        _body.VerticalScrollBarVisibility=_alarm||_manage?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled;
        if(_manage)RenderGroups();else if(_alarm)RenderAlarms();else RenderCalendar();
        if(!_alarm)_footer.Children.Add(_status);UpdateDateStatus(_date);UpdateRemaining();
    }
    private void RenderCalendar()
    {
        bool week=_service.Book.WeekView;
        if(week)_batch=false;
        _calendarDateBorders.Clear();
        bool side=!week&&(_details||_batch);
        Grid layout=new();layout.ColumnDefinitions.Add(new(){Width=new GridLength(7,GridUnitType.Star)});if(side)layout.ColumnDefinitions.Add(new(){Width=new GridLength(3,GridUnitType.Star)});
        DockPanel main=new(){Margin=new Thickness(0,0,side?10:0,0)};
        DockPanel nav=new(){Margin=new Thickness(0,0,0,10)};StackPanel left=Row();
        left.Children.Add(IconButton("chevron-left",()=>MoveDate(week?-7:-1,week),"上一页",18));
        left.Children.Add(Text(week?$"{WeekStart(_date):M月d日} — {WeekStart(_date).AddDays(6):M月d日}":_date.ToString("yyyy年M月"),18,FontWeights.SemiBold));
        left.Children.Add(IconButton("chevron-right",()=>MoveDate(week?7:1,week),"下一页",18));
        left.Children.Add(Chip("今天",()=>{_date=DateTime.Today;_details=true;Render();}));
        if(!week){var rest=Chip(_batch?"完成班休":"设置班休",()=>{_batch=!_batch;_selected.Clear();Render();});rest.Name="SetWorkdays";if(_batch){rest.Background=PlannerTheme.AccentSoft;rest.Foreground=PlannerTheme.Accent;rest.BorderBrush=PlannerTheme.Accent;}left.Children.Add(rest);}
        StackPanel right=Row();
        var month=Chip("月",()=>SetView(false));var weekBtn=Chip("周",()=>SetView(true));
        if(week){Primary(weekBtn);}else{Primary(month);}
        right.Children.Add(month);right.Children.Add(weekBtn);
        var add=Primary(Action("",()=>{_occurrenceDate=null;Edit(null,true);}));StackPanel addLabel=Row();addLabel.Children.Add(PlannerTheme.Icon("plus",15,Brushes.White,6));addLabel.Children.Add(Text("新增事项",14,FontWeights.SemiBold,Brushes.White));add.Content=addLabel;right.Children.Add(add);
        if(Width<1000&&!week&&_details){DockPanel.SetDock(left,Dock.Top);nav.Children.Add(left);right.HorizontalAlignment=HorizontalAlignment.Right;nav.Children.Add(right);}else{DockPanel.SetDock(right,Dock.Right);nav.Children.Add(right);nav.Children.Add(left);}DockPanel.SetDock(nav,Dock.Top);main.Children.Add(nav);
        Grid grid=new();for(int c=0;c<7;c++)grid.ColumnDefinitions.Add(new());grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        for(int c=0;c<7;c++){var t=Text("周"+"一二三四五六日"[c],13,FontWeights.SemiBold,PlannerTheme.Muted);t.TextAlignment=TextAlignment.Center;t.Margin=new Thickness(0,8,0,8);Grid.SetColumn(t,c);grid.Children.Add(t);}
        DateTime first=week?WeekStart(_date):WeekStart(new DateTime(_date.Year,_date.Month,1));int count=week?7:((int)(new DateTime(_date.Year,_date.Month,DateTime.DaysInMonth(_date.Year,_date.Month))-first).TotalDays/7+1)*7;
        for(int r=0;r<count/7;r++)grid.RowDefinitions.Add(new());
        for(int n=0;n<count;n++)
        {
            DateTime day=first.AddDays(n);string dayKey=day.ToString("yyyy-MM-dd");bool overrideExists=_service.Book.RestOverrides.ContainsKey(dayKey);bool isRest=_service.Book.IsRest(day);var items=DayItems(day).ToList();DockPanel cell=new();
            StackPanel dates=new();DockPanel heading=new();var number=Text(week?day.ToString("M月d日"):day.Month==_date.Month?day.Day.ToString():day.ToString("M/d"),week?16:15,FontWeights.SemiBold);
            if(day==DateTime.Today){number.Foreground=PlannerTheme.Accent;number.TextDecorations=TextDecorations.Underline;}
            var badge=Text(isRest?"休":"班",11,foreground:isRest?PlannerTheme.RestForeground:PlannerTheme.WorkForeground);badge.TextAlignment=TextAlignment.Center;badge.Margin=new Thickness(0);Border badgeBox=new(){Child=badge,Width=22,Height=22,Background=isRest?PlannerTheme.RestBackground:PlannerTheme.WorkBackground,CornerRadius=new CornerRadius(4),Margin=new Thickness(1)};
            if(week||overrideExists)heading.Children.Add(badgeBox);heading.Children.Add(number);dates.Children.Add(heading);
            var lunar=Text(CalendarLabels.LunarDay(day),12);dates.Children.Add(lunar);
            dates.Children.Add(HolidayText(day));
            if(!week&&items.Count>0){StackPanel dots=Row();dots.Margin=new Thickness(2,1,2,0);for(int d=0;d<Math.Min(3,items.Count);d++)dots.Children.Add(new Ellipse{Width=6,Height=6,Fill=PlannerTheme.Accent,Margin=new Thickness(0,0,3,0)});dates.Children.Add(dots);}
            foreach(var label in dates.Children.OfType<TextBlock>())label.Margin=new Thickness(2,0,2,0);
            Button dateButton=Action("",()=>OnCalendarDateClick(day));dateButton.Name="Day"+day.ToString("yyyyMMdd");dateButton.Content=dates;dateButton.Padding=new Thickness(0);dateButton.Margin=new Thickness(0);dateButton.BorderThickness=new Thickness(0);dateButton.Background=Brushes.Transparent;dateButton.HorizontalContentAlignment=HorizontalAlignment.Stretch;dateButton.VerticalAlignment=VerticalAlignment.Stretch;dateButton.VerticalContentAlignment=VerticalAlignment.Top;
            dateButton.ToolTip=overrideExists?$"单独设置：{(isRest?"休息日":"工作日")}":"未单独设置，跟随每周规则";
            dateButton.IsEnabled=day>=ReminderSchedule.MinimumDate&&day<=ReminderSchedule.MaximumDate;
            if(_batch&&!week)AttachCalendarDragHandlers(dateButton,grid,day);
            dateButton.MouseDoubleClick+=(_,_)=>{if(!_batch){_date=day;_occurrenceDate=null;Edit(null,true);}};
            if(week){DockPanel.SetDock(dateButton,Dock.Top);cell.Children.Add(dateButton);StackPanel cards=new();foreach(var item in items)cards.Children.Add(EventCard(item,day,true));cell.Children.Add(new ScrollViewer{Content=cards,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});}else cell.Children.Add(dateButton);
            bool highlighted=_selected.Contains(day)||!_batch&&day==_date;
            Border border=new(){Child=cell,Padding=new Thickness(7),CornerRadius=new CornerRadius(6),BorderBrush=highlighted?PlannerTheme.Accent:PlannerTheme.Line,BorderThickness=new Thickness(1),Background=highlighted?PlannerTheme.Soft:week&&day==DateTime.Today?PlannerTheme.AccentSoft:Brushes.White};_calendarDateBorders[day]=border;Grid.SetColumn(border,n%7);Grid.SetRow(border,n/7+1);grid.Children.Add(border);
        }
        main.Children.Add(grid);layout.Children.Add(main);
        if(side){var panel=_batch?RestControls():DayDetails();Grid.SetColumn(panel,1);layout.Children.Add(panel);}
        _body.Content=layout;
        var foot=Row();
        StackPanel dotLegend=Row();dotLegend.Children.Add(new Ellipse{Width=8,Height=8,Fill=PlannerTheme.Accent,Margin=new Thickness(3,0,6,0),VerticalAlignment=VerticalAlignment.Center});dotLegend.Children.Add(Text("有事项",11));foot.Children.Add(dotLegend);
        foot.Children.Add(LegendBadge("休","休息日",PlannerTheme.RestBackground,PlannerTheme.RestForeground));
        foot.Children.Add(LegendBadge("班","工作日",PlannerTheme.WorkBackground,PlannerTheme.WorkForeground));
        _footer.Children.Add(foot);
    }
    private static TextBlock HolidayText(DateTime day)
    {
        TextBlock block=new(){FontSize=12,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,0,2,0),LineHeight=15};
        string festivals=CalendarLabels.Festivals(day),term=CalendarLabels.SolarTerm(day);
        if(festivals.Length>0)block.Inlines.Add(new System.Windows.Documents.Run(festivals){Foreground=PlannerTheme.RestForeground});
        if(festivals.Length>0&&term.Length>0)block.Inlines.Add(new System.Windows.Documents.Run(" · "){Foreground=PlannerTheme.Muted});
        if(term.Length>0)block.Inlines.Add(new System.Windows.Documents.Run(term){Foreground=PlannerTheme.TermForeground});
        return block;
    }
    private static UIElement LegendBadge(string glyph,string label,Brush background,Brush foreground)
    {
        StackPanel row=Row();row.Margin=new Thickness(10,0,0,0);
        row.Children.Add(new Border{Child=new TextBlock{Text=glyph,FontSize=10,Foreground=foreground,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0)},Width=18,Height=18,Background=background,CornerRadius=new CornerRadius(4),Margin=new Thickness(0,0,6,0),VerticalAlignment=VerticalAlignment.Center});
        row.Children.Add(Text(label,11));
        return row;
    }
    private IEnumerable<ReminderItem> DayItems(DateTime day)=>_service.Book.Items.Where(i=>i.Calendar&&ReminderSchedule.OccursOn(i,_service.Book,day)).OrderBy(i=>i.HasTime?1:0).ThenBy(i=>i.Start.TimeOfDay);
    private UIElement EventCard(ReminderItem item,DateTime day,bool week)
    {
        DockPanel content=new();
        if(!week){var more=IconButton("ellipsis",()=>ItemMenu(item,day),"更多",16,PlannerTheme.Muted);more.MinWidth=24;more.MinHeight=24;more.Padding=new Thickness(4);DockPanel.SetDock(more,Dock.Right);content.Children.Add(more);}
        StackPanel info=new();var title=Text((item.HasTime?item.Start.ToString("HH:mm")+(week?"\n":"  "):"")+ReminderEngine.Label(item),14,FontWeights.SemiBold);
        if(item.Enabled&&item.HasTime)title.Inlines.InsertBefore(title.Inlines.FirstInline,new System.Windows.Documents.InlineUIContainer(PlannerTheme.Bell()));
        info.Children.Add(title);
        if(!string.IsNullOrWhiteSpace(item.Notes)){var notes=Text(item.Notes,13,foreground:PlannerTheme.Muted);notes.MaxHeight=week?65:42;notes.TextTrimming=TextTrimming.CharacterEllipsis;info.Children.Add(notes);}content.Children.Add(info);
        var button=Action("",()=>{_occurrenceDate=day;Edit(item,true);});button.Content=new Border{Child=content,BorderBrush=PlannerTheme.ItemAccent(item.Id),BorderThickness=new Thickness(3,0,0,0),Padding=new Thickness(7,3,0,3)};button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Padding=new Thickness(8);button.Margin=new Thickness(0,5,0,5);button.Background=Brushes.White;return button;
    }
    private UIElement DayDetails()
    {
        DockPanel panel=new(){Margin=new Thickness(14,0,2,0)};DockPanel title=new();var close=IconButton("close",()=>{_details=false;Render();},"收起当天事项");DockPanel.SetDock(close,Dock.Right);title.Children.Add(close);title.Children.Add(Text(_date.ToString("M月d日 dddd"),18,FontWeights.SemiBold));DockPanel.SetDock(title,Dock.Top);panel.Children.Add(title);
        var lunar=Text(CalendarLabels.FullLunar(_date)+" "+CalendarLabels.Get(_date),12);DockPanel.SetDock(lunar,Dock.Top);panel.Children.Add(lunar);
        var add=Chip("＋ 添加当天事项",()=>{_occurrenceDate=null;Edit(null,true);});add.Name="AddSelectedDay";add.Height=38;add.HorizontalAlignment=HorizontalAlignment.Stretch;DockPanel.SetDock(add,Dock.Bottom);panel.Children.Add(add);
        StackPanel list=new();foreach(var item in DayItems(_date))list.Children.Add(EventCard(item,_date,false));if(list.Children.Count==0)list.Children.Add(Text("当天暂无事项",14));panel.Children.Add(new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});return panel;
    }
    private void ItemMenu(ReminderItem item,DateTime day)
    {
        System.Windows.Controls.ContextMenu menu=new();foreach(var label in new[]{"编辑事项","仅删除当天","删除整组…","批量管理…"}){var entry=new System.Windows.Controls.MenuItem{Header=label};entry.Click+=(_,_)=>{if(label=="编辑事项"){_occurrenceDate=day;Edit(item,true);}else if(label=="批量管理…"){_manage=true;Render();}else ConfirmDelete([item.Id],label=="仅删除当天"?day:null);};menu.Items.Add(entry);}menu.IsOpen=true;
    }
    private void SetView(bool week){if(week)_batch=false;_=Execute(b=>b.WeekView=week);}
    private static DateTime WeekStart(DateTime date)=>date.Date.AddDays(-((int)date.DayOfWeek+6)%7);
    private void MoveDate(int offset,bool week){DateTime next=week?_date.AddDays(offset):_date.AddMonths(offset);if(next>=ReminderSchedule.MinimumDate&&next<=ReminderSchedule.MaximumDate)_date=next;Render();}
    private void EditWorkdays(){_batch=true;_alarm=false;Render();}
    private UIElement RestControls()
    {
        Grid inner=new();
        inner.RowDefinitions.Add(new(){Height=GridLength.Auto});
        inner.RowDefinitions.Add(new(){Height=GridLength.Auto});
        inner.RowDefinitions.Add(new(){Height=GridLength.Auto});
        inner.RowDefinitions.Add(new(){Height=GridLength.Auto});
        inner.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star),MinHeight=120});
        inner.RowDefinitions.Add(new(){Height=GridLength.Auto});
        inner.RowDefinitions.Add(new(){Height=GridLength.Auto});
        DockPanel title=new();var close=IconButton("close",()=>{_batch=false;_selected.Clear();Render();},"完成班休");DockPanel.SetDock(close,Dock.Right);title.Children.Add(close);title.Children.Add(Text("调整班休",18,FontWeights.SemiBold));Grid.SetRow(title,0);inner.Children.Add(title);
        var weeklyTitle=Text("每周休息",13,FontWeights.SemiBold);Grid.SetRow(weeklyTitle,1);inner.Children.Add(weeklyTitle);
        WrapPanel days=new(){Margin=new Thickness(0,2,0,6)};
        foreach(var d in Days)
        {
            var day=d;
            var btn=AsyncChip("周"+"日一二三四五六"[(int)d],()=>Execute(b=>{var selected=b.RestWeekdays.ToList();if(!selected.Remove(day))selected.Add(day);ReminderSchedule.SetRestWeekdays(b,selected,DateTime.Now);}));
            if(_service.Book.RestWeekdays.Contains(d)){btn.Background=PlannerTheme.AccentSoft;btn.Foreground=PlannerTheme.Accent;btn.BorderBrush=PlannerTheme.Accent;btn.FontWeight=FontWeights.SemiBold;}
            btn.Margin=new Thickness(0,0,6,6);days.Children.Add(btn);
        }
        Grid.SetRow(days,2);inner.Children.Add(days);
        var priorityHint=Text("指定日期会覆盖每周设置；清除指定设置后不再单独指定。",11,foreground:PlannerTheme.Muted);priorityHint.Margin=new Thickness(3,0,3,6);Grid.SetRow(priorityHint,3);inner.Children.Add(priorityHint);
        DockPanel head=new(){LastChildFill=false};
        head.Children.Add(Text("指定日期",13,FontWeights.SemiBold));
        head.Children.Add(Text($"已选 {_selected.Count} 天",12));
        Button clearSelected=new(){Name="ClearSelectedDates",Style=(Style)FindResource("PlannerLink"),IsEnabled=_selected.Count>0,ToolTip="清空已选日期"};
        StackPanel clearLabel=Row();
        clearLabel.Children.Add(PlannerTheme.Icon("close",12,PlannerTheme.Muted,4));
        clearLabel.Children.Add(Text("清空选择",12,foreground:PlannerTheme.Muted));
        clearSelected.Content=clearLabel;
        clearSelected.Click+=(_,_)=>{_selected.Clear();Render();};
        DockPanel.SetDock(clearSelected,Dock.Right);
        head.Children.Add(clearSelected);
        Grid.SetRow(head,4);inner.Children.Add(head);
        StackPanel rows=new();
        foreach(var day in _selected.OrderBy(d=>d))
        {
            DockPanel row=new(){Margin=new Thickness(0,1,0,1)};
            var remove=IconButton("close",()=>{_selected.Remove(day);Render();},"移除该日期",13,PlannerTheme.Muted);remove.MinWidth=26;remove.MinHeight=26;DockPanel.SetDock(remove,Dock.Right);row.Children.Add(remove);
            row.Children.Add(Text($"{day:M月d日（ddd）}",13));
            rows.Children.Add(row);
        }
        if(rows.Children.Count==0)rows.Children.Add(Text("单击日期可选中或取消；按住左键拖动可连续选择日期。",12));
        ScrollViewer selectedDates=new(){Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalAlignment=VerticalAlignment.Top,Height=144,Margin=new Thickness(0,2,0,4)};Grid.SetRow(selectedDates,5);inner.Children.Add(selectedDates);
        Button[] batchActions=[
            Primary(AsyncAction("设为休息日",()=>SetRest(true))),
            AsyncAction("设为工作日",()=>SetRest(false)),
            AsyncAction("清除指定设置",()=>SetRest(null))
        ];
        batchActions[0].Name="SetSelectedRestDays";
        batchActions[1].Name="SetSelectedWorkdays";
        batchActions[2].Name="ResetSelectedRestDays";
        foreach(var b in batchActions)
        {
            b.IsEnabled=_selected.Count>0;
            b.HorizontalAlignment=HorizontalAlignment.Stretch;
            b.Margin=new Thickness(0,4,0,4);
            if(b==batchActions[2])b.ToolTip="清除该日期的单独设置，回到未单独指定状态";
        }
        StackPanel actions=new();foreach(var b in batchActions)actions.Children.Add(b);Grid.SetRow(actions,6);inner.Children.Add(actions);
        return new Border{Child=inner,Background=Brushes.White,BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(14),Margin=new Thickness(14,0,2,0)};
    }
    private async Task SetRest(bool? rest)
    {
        if(_selected.Count==0)return;
        DateTime[] selected=_selected.OrderBy(d=>d).ToArray();
        await Execute(b=>{foreach(var d in selected)ReminderSchedule.SetRestOverride(b,d,rest,DateTime.Now);});
        _selected.Clear();
        Render();
        if(rest==null)_status.Text="已清除指定设置，当前日期恢复为未单独设置。";
    }

    private void OnCalendarDateClick(DateTime day)
    {
        if(_suppressCalendarClick)
        {
            _suppressCalendarClick=false;
            return;
        }

        if(_batch)
        {
            if(!_selected.Add(day))_selected.Remove(day);
        }
        else
        {
            _details=!(_details&&_date==day);
            _date=day;
        }
        Render();
    }

    private void AttachCalendarDragHandlers(Button dateButton,Grid grid,DateTime day)
    {
        dateButton.PreviewMouseLeftButtonDown+=(_,e)=>
        {
            if(!_batch||e.ChangedButton!=System.Windows.Input.MouseButton.Left||!dateButton.IsEnabled)return;
            _calendarDragActive=true;
            _calendarDragMoved=false;
            _calendarDragVisited.Clear();
            _calendarDragStartDay=day;
            _calendarDragLastPoint=System.Windows.Input.Mouse.GetPosition(grid);
            System.Windows.Input.Mouse.Capture(dateButton,System.Windows.Input.CaptureMode.Element);
        };
        dateButton.PreviewMouseMove+=(_,e)=>
        {
            if(!_calendarDragActive||!_batch||e.LeftButton!=System.Windows.Input.MouseButtonState.Pressed)return;
            Point point=System.Windows.Input.Mouse.GetPosition(grid);
            if((point-_calendarDragLastPoint).Length<1)return;
            AddCalendarDragPath(grid,_calendarDragLastPoint,point);
            _calendarDragLastPoint=point;
        };
        dateButton.PreviewMouseLeftButtonUp+=(_,e)=>
        {
            if(!_calendarDragActive||e.ChangedButton!=System.Windows.Input.MouseButton.Left)return;
            bool moved=_calendarDragMoved;
            _calendarDragActive=false;
            _calendarDragMoved=false;
            _calendarDragVisited.Clear();
            _calendarDragStartDay=null;
            System.Windows.Input.Mouse.Capture(null);
            if(!moved)return;
            _suppressCalendarClick=true;
            e.Handled=true;
            Render();
            Dispatcher.BeginInvoke(new Action(()=>_suppressCalendarClick=false),System.Windows.Threading.DispatcherPriority.Input);
        };
    }

    private void AddCalendarDragPath(Grid grid,Point from,Point to)
    {
        double distance=(to-from).Length;
        int samples=Math.Max(1,(int)Math.Ceiling(distance/6));
        for(int i=0;i<=samples;i++)
        {
            double ratio=i/(double)samples;
            Point point=new(from.X+(to.X-from.X)*ratio,from.Y+(to.Y-from.Y)*ratio);
            if(TryGetCalendarDate(grid,point,out DateTime day))
            {
                if(_calendarDragStartDay is DateTime start&&day!=start)_calendarDragMoved=true;
                if(!_calendarDragVisited.Add(day))continue;
                if(!_selected.Add(day))_selected.Remove(day);
            }
        }
        UpdateCalendarDateSelectionVisuals();
    }

    private bool TryGetCalendarDate(Grid grid,Point point,out DateTime day)
    {
        DependencyObject? current=VisualTreeHelper.HitTest(grid,point)?.VisualHit;
        while(current is not null&&current!=grid)
        {
            if(current is Button button&&button.Name.StartsWith("Day",StringComparison.Ordinal)&&button.IsEnabled&&
                DateTime.TryParseExact(button.Name.Substring(3),"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out day))
            {
                return true;
            }
            current=VisualTreeHelper.GetParent(current);
        }

        DateTime? nearest=null;
        double nearestDistance=double.MaxValue;
        foreach(var pair in _calendarDateBorders)
        {
            if(pair.Key<ReminderSchedule.MinimumDate||pair.Key>ReminderSchedule.MaximumDate||pair.Value.ActualWidth<=0||pair.Value.ActualHeight<=0)continue;
            Rect bounds=pair.Value.TransformToAncestor(grid).TransformBounds(new Rect(0,0,pair.Value.ActualWidth,pair.Value.ActualHeight));
            bounds.Inflate(8,8);
            if(!bounds.Contains(point))continue;
            Point center=new(bounds.X+bounds.Width/2,bounds.Y+bounds.Height/2);
            double distance=(center-point).Length;
            if(distance<nearestDistance){nearestDistance=distance;nearest=pair.Key;}
        }
        if(nearest is DateTime candidate){day=candidate;return true;}
        day=default;
        return false;
    }

    private void UpdateCalendarDateSelectionVisuals()
    {
        foreach(var pair in _calendarDateBorders)
        {
            bool selected=_selected.Contains(pair.Key);
            pair.Value.BorderBrush=selected?PlannerTheme.Accent:PlannerTheme.Line;
            pair.Value.Background=selected?PlannerTheme.Soft:Brushes.White;
        }
    }
    private void RenderAlarms()
    {
        DockPanel header=new(){Margin=new Thickness(20,0,20,10)};var add=Primary(Action("",()=>Edit(null,false)));StackPanel addLabel=Row();addLabel.Children.Add(PlannerTheme.Icon("plus",15,Brushes.White,6));addLabel.Children.Add(Text("新建提醒",14,FontWeights.SemiBold,Brushes.White));add.Content=addLabel;add.Name="NewAlarm";DockPanel.SetDock(add,Dock.Right);header.Children.Add(add);header.Children.Add(Text("闹钟",22,FontWeights.SemiBold));_header.Children.Add(header);
        StackPanel list=new();
        var groups=new[]{("正在倒计时","hourglass",_service.Book.Items.Where(i=>i.Relative && (i.PausedSeconds!=null || i.Enabled && (i.Start>DateTime.Now || _service.Book.Occurrences.Any(o=>o.RuleId==i.Id&&o.Phase is ReminderPhase.Due or ReminderPhase.DueSnoozed))))),("我的闹钟","clock",_service.Book.Items.Where(i=>!i.Relative&&!i.Calendar)),("日历提醒","calendar",_service.Book.Items.Where(i=>i.Calendar&&i.ReminderCreated==true))};
        foreach(var group in groups)
        {
            var items=group.Item3.OrderBy(i=>ReminderSchedule.Next(i,_service.Book,DateTime.Now)??DateTime.MaxValue).ToList();if(items.Count==0)continue;
            StackPanel section=new();
            StackPanel heading=Row();heading.Children.Add(PlannerTheme.Icon(group.Item2,18,PlannerTheme.Accent,8));heading.Children.Add(Text($"{group.Item1} ({items.Count})",15,FontWeights.SemiBold));heading.Margin=new Thickness(4,2,4,10);section.Children.Add(heading);
            for(int i=0;i<items.Count;i++){var rowBorder=(Border)AlarmRow(items[i]);if(i==items.Count-1)rowBorder.BorderThickness=new Thickness(0);section.Children.Add(rowBorder);}
            var card=Card(section);card.Padding=new Thickness(12);if(group.Item1=="正在倒计时")card.Background=PlannerTheme.Soft;list.Children.Add(card);
        }
        if(list.Children.Count==0)list.Children.Add(Text("暂无提醒，点击右上角「新建提醒」开始。",16));_body.Content=list;
    }
    private UIElement AlarmRow(ReminderItem item)
    {
        Grid row=new(){Margin=new Thickness(8,8,8,8)};
        row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});row.ColumnDefinitions.Add(new(){Width=new GridLength(150)});row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=new GridLength(230)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        row.Children.Add(new Border{Width=4,CornerRadius=new CornerRadius(2),Background=PlannerTheme.ItemAccent(item.Id),Margin=new Thickness(2,8,14,8),VerticalAlignment=VerticalAlignment.Stretch});
        TextBlock clock=Text(item.Start.ToString("HH:mm"),26);clock.FontFamily=new System.Windows.Media.FontFamily("Segoe UI");clock.FontWeight=FontWeights.SemiBold;
        if(item.Relative){_running.Add((clock,item));clock.Foreground=PlannerTheme.Accent;clock.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetColumn(row.Children.Count==0?clock:clock,1);row.Children.Add(new Border{Child=clock,Background=PlannerTheme.AccentSoft,CornerRadius=new CornerRadius(10),Padding=new Thickness(10,12,10,12),Margin=new Thickness(0,4,12,4),HorizontalAlignment=HorizontalAlignment.Stretch});}
        else row.Children.Add(clock);
        Grid.SetColumn(row.Children[row.Children.Count-1],1);
        StackPanel details=new();var name=Text(ReminderEngine.Label(item),15,FontWeights.SemiBold);details.Children.Add(name);details.Children.Add(Text(item.Relative?$"共 {TimeSpan.FromSeconds(item.DurationSeconds).TotalMinutes:0.##} 分钟" : item.Calendar?"来自日历 · "+item.Start.ToString("M月d日"):item.Repeat==ReminderRepeat.Weekly?"每周"+string.Join("、",item.Weekdays.Select(d=>"日一二三四五六"[(int)d])):Repeats[(int)item.Repeat],12));
        Grid.SetColumn(details,2);row.Children.Add(details);
        var next=ReminderSchedule.Next(item,_service.Book,DateTime.Now);
        StackPanel nextRow=Row();nextRow.Children.Add(PlannerTheme.Icon("repeat",14,PlannerTheme.Muted,6));
        nextRow.Children.Add(Text(item.PausedSeconds!=null?"已暂停":!item.Enabled?"已关闭":next is DateTime at?(item.Relative?"结束：":"下一次：")+at.ToString("M月d日 HH:mm"):"已到时间",12));
        Grid.SetColumn(nextRow,3);row.Children.Add(nextRow);
        StackPanel controls=Row();
        if(item.Relative)
        {
            controls.Children.Add(Chip(item.PausedSeconds==null?"暂停":"继续",()=>_=Execute(b=>{if(item.PausedSeconds==null)ReminderEngine.Pause(b,item.Id,DateTime.Now);else ReminderEngine.Resume(b,item.Id,DateTime.Now);})));
            var cancel=AsyncAction("",()=>Execute(b=>b.Items.RemoveAll(i=>i.Id==item.Id)));cancel.Style=(Style)FindResource("PlannerDanger");StackPanel cancelLabel=Row();cancelLabel.Children.Add(PlannerTheme.Icon("trash",15,PlannerTheme.Danger,6));cancelLabel.Children.Add(Text("取消",13,FontWeights.SemiBold,PlannerTheme.Danger));cancel.Content=cancelLabel;cancel.MinHeight=32;controls.Children.Add(cancel);
        }
        else
        {
            var enabled=new CheckBox{IsChecked=item.Enabled,Style=(Style)FindResource("PlannerSwitch"),Margin=new Thickness(8),VerticalAlignment=VerticalAlignment.Center};enabled.Click+=async(_,_)=>{try{await Execute(b=>ReminderEngine.SetEnabled(b,item.Id,enabled.IsChecked==true,DateTime.Now));}catch{}};controls.Children.Add(enabled);
            controls.Children.Add(IconButton("ellipsis",()=>{_occurrenceDate=null;Edit(item,item.Calendar);},"编辑",16,PlannerTheme.Muted));
        }
        Grid.SetColumn(controls,4);row.Children.Add(controls);
        return new Border{Child=row,BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(0,0,0,1),Margin=new Thickness(4,0,4,0)};
    }
    private void ShowPlannerSettings(){if(ReminderSettingsRequested!=null)ReminderSettingsRequested();else {var settings=new SettingsWindow(new(),new(),new(),new(),new(),false,null,_service.Book.Preferences);settings.NavigateNotifications();if(settings.ShowDialog()==true)_=Execute(b=>b.Preferences=settings.SelectedReminderPreferences);}}
}
