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
using TextBox = System.Windows.Controls.TextBox;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
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
    private StackPanel? _manageCards;
    private string _manageQuery = "";
    private ManageFilter _manageFilter = ManageFilter.Active;
    private readonly Dictionary<DateTime, Border> _calendarDateBorders=[];
    private DateTime _date=DateTime.Today;
    private DateTime? _occurrenceDate;
    private bool _alarm,_editing,_batch,_manage,_details;
    private bool _calendarDragActive;
    private bool _calendarDragMoved;
    private bool _suppressCalendarClick;
    private DateTime? _calendarDragStartDay;
    private DateTime? _calendarDragLastDay;
    private Point _calendarDragLastPoint;
    private Vector? _calendarDragLastDirection;
    private static readonly string[] Repeats=["仅一次","每天","每周","指定日期","工作日","休息日"];
    private static readonly DayOfWeek[] Days=[DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday,DayOfWeek.Saturday,DayOfWeek.Sunday];
    public PlannerWindow(ReminderService service,bool alarm)
    {
        _service=service;_alarm=alarm;PlannerTheme.Apply(this);
        Title="洛天依 · 与你依起";Width=900;Height=700;
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        System.Windows.Shell.WindowChrome.SetWindowChrome(this,new(){CaptionHeight=0,ResizeBorderThickness=new Thickness(0),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(12)});
        _body.Margin=new Thickness(12,0,12,0);_footer.Margin=new Thickness(20,8,20,10);
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        FontFamily=new System.Windows.Media.FontFamily("Microsoft YaHei UI");FontSize=14;Foreground=PlannerTheme.Ink;Background=new LinearGradientBrush(Color.FromRgb(239,249,255),Colors.White,90);
        Language=System.Windows.Markup.XmlLanguage.GetLanguage("zh-CN");UseLayoutRounding=true;
        DockPanel.SetDock(_header,Dock.Top);_root.Children.Add(_header);DockPanel.SetDock(_footer,Dock.Bottom);_root.Children.Add(_footer);_root.Children.Add(_body);_shell.Children.Add(_root);Content=_shell;InitializeViewport();
        _service.Changed+=OnChanged;_clock.Tick+=(_,_)=>UpdateRemaining();IsVisibleChanged+=(_,_)=>{if(IsVisible)_clock.Start();else _clock.Stop();};
        Closed+=(_,_)=>{_clock.Stop();_service.Changed-=OnChanged;};Render();
    }
    public void Navigate(bool alarm){_alarm=alarm;_manage=false;_manageQuery="";_manageFilter=ManageFilter.Active;_editing=false;if(!alarm)_details=false;Render();Show();Activate();}
    private void OpenManage(){_manage=true;_manageQuery="";_manageFilter=ManageFilter.Active;Render();}
    internal void OpenItem(Guid id,DateTime? occurrence=null){var item=_service.Book.Items.FirstOrDefault(i=>i.Id==id);if(item!=null){_date=(occurrence??item.Start).Date;_occurrenceDate=occurrence??item.Start;Navigate(!item.Calendar);_details=true;Render();Edit(item,item.Calendar);}}
    private void OnChanged(){if(!_editing)Render();}
    private async Task Execute(Action<ReminderBook> action){try{await _service.ChangeAsync(action);_status.Text="已保存到本机";}catch{_status.Text="保存失败，原数据保留。";throw;}}
    private static TextBlock Text(string value,double size=14,FontWeight? weight=null,Brush? foreground=null)
    {
        var block=new TextBlock{Text=value,FontSize=PlannerTheme.TypeSize(size),TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(3),Foreground=foreground??(size<=12?PlannerTheme.Muted:PlannerTheme.Ink)};
        if(weight is FontWeight value2)block.FontWeight=value2;
        return block;
    }
    private static StackPanel Row()=>new(){Orientation=Orientation.Horizontal};
    private Button Action(string label,Action action){Button b=new(){Content=label,Margin=new Thickness(3),Padding=new Thickness(14,8,14,8),MinHeight=44,FontSize=16,VerticalAlignment=VerticalAlignment.Center};b.Click+=(_,_)=>action();return b;}
    private Button AsyncAction(string label,Func<Task> action)=>Action(label,async()=>{try{await action();}catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){_status.Text=e is ArgumentException?e.Message:"保存失败，原数据保留。";}});
    private static Button Primary(Button b){b.Background=PlannerTheme.PrimaryFill;b.Foreground=Brushes.White;b.BorderThickness=new Thickness(0);return b;}
    private Button Chip(string label,Action action){Button b=new(){Content=label,Style=(Style)FindResource("PlannerChip"),Margin=new Thickness(3),VerticalAlignment=VerticalAlignment.Center};b.Click+=(_,_)=>action();return b;}
    private Button AsyncChip(string label,Func<Task> action)=>Chip(label,async()=>{try{await action();}catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){_status.Text=e is ArgumentException?e.Message:"保存失败，原数据保留。";}});
    private Button IconButton(string kind,Action action,string? tip=null,double size=18,Brush? color=null)
    {
        Button b=new(){Style=(Style)FindResource("PlannerIconBtn"),Content=PlannerTheme.Icon(kind,size,color??PlannerTheme.Ink,0),Padding=new Thickness(6)};
        if(tip!=null){b.ToolTip=tip;System.Windows.Automation.AutomationProperties.SetName(b,tip);}
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
        _footer.Margin=Width<1200?new Thickness(16,4,16,5):new Thickness(20,8,20,10);
        _calendarDragLastDay=null;
        _calendarDragLastDirection=null;
        while(_shell.Children.Count>1)_shell.Children.RemoveAt(_shell.Children.Count-1);_root.IsEnabled=true;_header.Children.Clear();_footer.Children.Clear();_running.Clear();
        bool narrowHeader=Width<800;
        Grid head=new(){Name="PlannerTitleBar",Margin=new Thickness(narrowHeader?10:18,4,narrowHeader?8:14,4),Height=narrowHeader?56:Width<1200?62:76,Background=Brushes.Transparent};
        head.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});head.ColumnDefinitions.Add(new(){Width=GridLength.Auto});head.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        DockPanel brand=new();
        StackPanel windows=Row();windows.HorizontalAlignment=HorizontalAlignment.Right;
        var closeWindow=IconButton("close",Close,"关闭",15);closeWindow.Name="PlannerCloseWindow";closeWindow.Style=(Style)FindResource("PlannerCloseBtn");closeWindow.Width=44;closeWindow.Height=32;windows.Children.Add(closeWindow);
        try{var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.UriSource=RuntimeAssetLocator.PackUri("app/luotianyi-pet.png");bitmap.DecodePixelWidth=128;bitmap.EndInit();var avatar=new System.Windows.Controls.Image{Width=narrowHeader?38:50,Height=narrowHeader?38:50,Margin=new Thickness(0,0,narrowHeader?6:12,0),Source=bitmap};RenderOptions.SetBitmapScalingMode(avatar,BitmapScalingMode.HighQuality);DockPanel.SetDock(avatar,Dock.Left);brand.Children.Add(avatar);}catch{}
        StackPanel identity=new(){VerticalAlignment=VerticalAlignment.Center};identity.Children.Add(Text("洛天依 · 与你依起",narrowHeader?16:21,FontWeights.SemiBold));identity.Children.Add(Text("愿世界，如你我所愿~",narrowHeader?10:12,foreground:PlannerTheme.Muted));brand.Children.Add(identity);
        brand.MouseLeftButtonDown+=(_,e)=>{if(e.LeftButton==System.Windows.Input.MouseButtonState.Pressed)DragMove();};
        Grid.SetColumn(brand,0);head.Children.Add(brand);
        StackPanel navigation=Row();navigation.HorizontalAlignment=HorizontalAlignment.Center;navigation.VerticalAlignment=VerticalAlignment.Stretch;
        foreach(var entry in new[]{("calendar","日历",false),("clock","闹钟",true)})
        {
            bool selected=_alarm==entry.Item3;
            Button navButton=new(){Name=entry.Item3?"PlannerAlarmNavigation":"PlannerCalendarNavigation",Width=narrowHeader?88:120,Height=narrowHeader?56:76,Padding=new Thickness(0),Margin=new Thickness(3,0,3,0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Cursor=System.Windows.Input.Cursors.Hand};
            StackPanel navLabel=Row();navLabel.HorizontalAlignment=HorizontalAlignment.Center;navLabel.VerticalAlignment=VerticalAlignment.Center;navLabel.Children.Add(PlannerTheme.Icon(entry.Item1,narrowHeader?18:21,selected?PlannerTheme.Accent:PlannerTheme.Ink,narrowHeader?5:9));navLabel.Children.Add(Text(entry.Item2,narrowHeader?14:16,selected?FontWeights.SemiBold:null,selected?PlannerTheme.Accent:PlannerTheme.Ink));
            Grid navContent=new(){Width=narrowHeader?86:114,Height=narrowHeader?54:70};navContent.Children.Add(navLabel);if(selected)navContent.Children.Add(new Border{Height=3,Width=narrowHeader?48:62,CornerRadius=new CornerRadius(2),Background=PlannerTheme.Accent,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,narrowHeader?5:9)});navButton.Content=navContent;navButton.Click+=(_,_)=>{_alarm=entry.Item3;_manage=false;_editing=false;if(!_alarm)_details=false;Render();};navigation.Children.Add(navButton);
            if(entry.Item3==false)navigation.Children.Add(new Border{Width=1,Height=24,Background=PlannerTheme.Line,Margin=new Thickness(4,0,4,0)});
        }
        Grid.SetColumn(navigation,1);head.Children.Add(navigation);
        Grid.SetColumn(windows,2);head.Children.Add(windows);
        head.MouseLeftButtonDown+=(_,e)=>
        {
            if(e.LeftButton!=System.Windows.Input.MouseButtonState.Pressed||e.OriginalSource is not DependencyObject source)return;
            for(DependencyObject? current=source;current!=null&&current!=head;current=VisualTreeHelper.GetParent(current))
                if(current is Button||current==brand)return;
            DragMove();
            e.Handled=true;
        };
        _header.Children.Add(head);
        _body.VerticalScrollBarVisibility=_alarm||_manage||Height<470?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled;
        if(_manage)RenderGroups();else if(_alarm)RenderAlarms();else RenderCalendar();
        UpdateDateStatus(_date);UpdateRemaining();
    }
    private void RenderCalendar()
    {
        bool week=_service.Book.WeekView;
        if(week)_batch=false;
        _calendarDateBorders.Clear();
        bool side=!week&&(_details||_batch);
        double sideWidth=Math.Min(360,Math.Max(240,Width*.28));
        Grid layout=new();layout.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});if(side)layout.ColumnDefinitions.Add(new(){Width=new GridLength(sideWidth)});
        DockPanel main=new(){Margin=new Thickness(0,0,side?10:0,0)};
        DockPanel nav=new(){Margin=new Thickness(0,0,0,Width<1200?5:10)};StackPanel left=Row();
        var previous=Action("",()=>MoveDate(week?-7:-1,week));previous.Content=PlannerTheme.Icon("chevron-left",18,PlannerTheme.Ink,0);previous.ToolTip=week?"上一周":"上一月";previous.Width=32;previous.Height=38;previous.Padding=new Thickness(4);previous.Margin=new Thickness(0);previous.Style=(Style)FindResource("PlannerIconBtn");if(!week){previous.Name="PreviousMonth";previous.Style=(Style)FindResource("PlannerIconBtn");previous.IsEnabled=_date.Year>ReminderSchedule.MinimumDate.Year||_date.Month>1;}left.Children.Add(previous);
        if(week){var weekRange=Text($"{WeekStart(_date):yyyy年M月d日} - {WeekStart(_date).AddDays(6):M月d日}",18,FontWeights.SemiBold);weekRange.Width=286;weekRange.TextAlignment=TextAlignment.Center;left.Children.Add(weekRange);}
        else
        {
            System.Windows.Controls.Primitives.Popup monthPopup=null!;
            var monthJump=Action("",()=>{if(monthPopup.IsOpen)monthPopup.IsOpen=false;else Dispatcher.BeginInvoke(new Action(()=>monthPopup.IsOpen=true),DispatcherPriority.Background);});monthJump.Name="MonthJump";monthJump.Content=Text(_date.ToString("yyyy年M月"),18,FontWeights.SemiBold);monthJump.Style=(Style)FindResource("PlannerLink");monthJump.Background=Brushes.Transparent;monthJump.Padding=new Thickness(4,6,4,6);monthJump.Margin=new Thickness(0);monthJump.ToolTip="点击快速选择其他年份和月份";left.Children.Add(monthJump);
            monthPopup=CreateMonthPicker(monthJump);left.Children.Add(monthPopup);
        }
        var next=Action("",()=>MoveDate(week?7:1,week));next.Content=PlannerTheme.Icon("chevron-right",18,PlannerTheme.Ink,0);next.ToolTip=week?"下一周":"下一月";next.Width=32;next.Height=38;next.Padding=new Thickness(4);next.Margin=new Thickness(0);next.Style=(Style)FindResource("PlannerIconBtn");if(!week){next.Name="NextMonth";next.Style=(Style)FindResource("PlannerIconBtn");next.IsEnabled=_date.Year<ReminderSchedule.MaximumDate.Year||_date.Month<12;}left.Children.Add(next);
        var today=Chip("今天",()=>{_date=DateTime.Today;_details=true;Render();});today.Width=week?68:58;today.Height=week?42:34;today.Margin=week?new Thickness(8,3,3,3):new Thickness(8,0,3,0);today.FontSize=13;today.Background=Brushes.White;today.Foreground=PlannerTheme.Ink;today.BorderBrush=PlannerTheme.Accent;left.Children.Add(today);
        if(!week)
        {
            if(_batch){var done=Chip("完成班休",()=>{_batch=false;_selected.Clear();_details=false;Render();});done.Name="WorkdaysDoneTop";done.Height=34;done.Background=PlannerTheme.FunctionFill;done.Foreground=PlannerTheme.WorkForeground;done.BorderBrush=PlannerTheme.WorkLine;left.Children.Add(done);}
            else{var setWorkdays=Chip("设置班休",EditWorkdays);setWorkdays.Name="SetWorkdays";setWorkdays.Width=86;setWorkdays.Height=34;setWorkdays.FontSize=13;setWorkdays.Background=PlannerTheme.FunctionFill;setWorkdays.Foreground=PlannerTheme.WorkForeground;setWorkdays.BorderBrush=PlannerTheme.WorkLine;setWorkdays.Margin=new Thickness(4,0,0,0);setWorkdays.ToolTip="设置每周休息和指定日期班休";left.Children.Add(setWorkdays);}
        }
        StackPanel right=Row();
        Border viewCapsule=new(){Background=Brushes.White,CornerRadius=new CornerRadius(10),BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(1),Padding=new Thickness(3),VerticalAlignment=VerticalAlignment.Center};
        StackPanel views=Row();
        foreach(var entry in new[]{("月",false),("周",true)})
        {
            bool selected=week==entry.Item2;
            var view=new Button{Style=(Style)FindResource("PlannerSegment"),Content=Text(entry.Item1,14,selected?FontWeights.SemiBold:null,selected?Brushes.White:PlannerTheme.Ink),Margin=new Thickness(1),Name=entry.Item2?"ViewWeek":"ViewMonth",MinWidth=54};
            if(selected){view.Background=PlannerTheme.PrimaryFill;view.Foreground=Brushes.White;}
            bool target=entry.Item2;view.Click+=(_,_)=>SetView(target);views.Children.Add(view);
        }
        viewCapsule.Child=views;right.Children.Add(viewCapsule);
        var manage=Chip("管理日程",OpenManage);manage.Name="ManageSchedules";manage.Height=40;manage.Margin=new Thickness(6,3,3,3);right.Children.Add(manage);
        var add=Primary(Action("",()=>{_occurrenceDate=null;Edit(null,true,null);}));add.Name="CreateSchedule";StackPanel addLabel=Row();addLabel.Children.Add(Text("创建日程",14,FontWeights.SemiBold,Brushes.White));add.Content=addLabel;right.Children.Add(add);
        if(side&&Width-sideWidth<675){DockPanel.SetDock(left,Dock.Top);nav.Children.Add(left);right.HorizontalAlignment=HorizontalAlignment.Right;nav.Children.Add(right);}else{DockPanel.SetDock(right,Dock.Right);nav.Children.Add(right);nav.Children.Add(left);}DockPanel.SetDock(nav,Dock.Top);main.Children.Add(nav);
        Grid grid=new();for(int c=0;c<7;c++)grid.ColumnDefinitions.Add(new());grid.RowDefinitions.Add(new(){Height=week?new GridLength(0):new GridLength(Width<1200?30:36)});
        grid.AddHandler(UIElement.PreviewMouseMoveEvent,new System.Windows.Input.MouseEventHandler((_,e)=>ContinueCalendarDrag(grid,e)),true);
        grid.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent,new System.Windows.Input.MouseButtonEventHandler((_,e)=>EndCalendarDrag(e)),true);
        if(!week)for(int c=0;c<7;c++){var t=Text("周"+"一二三四五六日"[c],13,FontWeights.SemiBold,PlannerTheme.Ink);t.TextAlignment=TextAlignment.Center;t.Margin=new Thickness(0);var headCell=new Border{Child=t,Background=PlannerTheme.ChipBackground,BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(1,1,0,0)};Grid.SetColumn(headCell,c);grid.Children.Add(headCell);}
        DateTime first=week?WeekStart(_date):WeekStart(new DateTime(_date.Year,_date.Month,1));int count=week?7:((int)(new DateTime(_date.Year,_date.Month,DateTime.DaysInMonth(_date.Year,_date.Month))-first).TotalDays/7+1)*7;
        if(!week&&Height<470)grid.MinHeight=(Width<1200?30:36)+(count/7)*58;
        for(int r=0;r<count/7;r++)grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        for(int n=0;n<count;n++)
        {
            DateTime day=first.AddDays(n);
            bool overridden=_service.Book.RestOverrides.ContainsKey(day.ToString("yyyy-MM-dd"));
            bool rest=_service.Book.IsRest(day), todayDate=day==DateTime.Today;
            bool selected=_batch?_selected.Contains(day):day==_date;
            var items=DayItems(day).ToList();
            Grid cell=new();
            cell.RowDefinitions.Add(new(){Height=week?new GridLength(100):new GridLength(1,GridUnitType.Star)});
            if(week)cell.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
            Grid dates=new(){Margin=new Thickness(0,week?8:Width<1200?2:5,0,0)};
            dates.RowDefinitions.Add(new(){Height=GridLength.Auto});dates.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
            StackPanel dateTop=new();dates.Children.Add(dateTop);
            var number=Text(week?"周"+"日一二三四五六"[(int)day.DayOfWeek]:day.Month==_date.Month?day.Day.ToString():day.ToString("M/d"),week?14:Width<900?14:Width<1200?16:18,FontWeights.SemiBold,todayDate?PlannerTheme.Accent:day.Month!=_date.Month&&!week?PlannerTheme.Muted:PlannerTheme.Ink);
            number.HorizontalAlignment=HorizontalAlignment.Center;if(!week&&Width<1200)number.Margin=new Thickness(0);dateTop.Children.Add(number);
            if(week){var full=Text(day.ToString("M月d日"),15,FontWeights.SemiBold,todayDate?PlannerTheme.Accent:PlannerTheme.Ink);full.HorizontalAlignment=HorizontalAlignment.Center;dateTop.Children.Add(full);}
            string festival=CalendarLabels.Festivals(day),term=CalendarLabels.SolarTerm(day);
            string auxiliary=festival.Length>0?festival:term.Length>0?term:CalendarLabels.LunarDay(day);
            if(week&&(festival.Length>0||term.Length>0))auxiliary=CalendarLabels.LunarDay(day)+" · "+auxiliary;
            var aux=Text(auxiliary,!week&&Width<900?10:12,foreground:festival.Length>0?PlannerTheme.RestForeground:term.Length>0?PlannerTheme.TermForeground:PlannerTheme.Muted);
            aux.Name="CellAux"+day.ToString("yyyyMMdd");aux.HorizontalAlignment=HorizontalAlignment.Center;aux.TextWrapping=TextWrapping.NoWrap;aux.TextTrimming=TextTrimming.CharacterEllipsis;if(!week&&Width<1200)aux.Margin=new Thickness(0,1,0,0);dateTop.Children.Add(aux);
            if(!week&&items.Count>0)
            {
                int previewCount=Width>=1060&&Height>=780&&count/7<=5?Math.Min(2,items.Count):1;
                for(int p=0;p<previewCount;p++)
                {
                    var previewItem=items[p];
                    Grid preview=new(){Margin=new Thickness(2,Width<1200?2:5,2,0)};
                    preview.ColumnDefinitions.Add(new(){Width=new GridLength(3)});preview.ColumnDefinitions.Add(new());
                    preview.Children.Add(new Border{Background=PlannerTheme.SchedulePreview,CornerRadius=new CornerRadius(2),Height=17});
                    var caption=Text((previewItem.HasTime?previewItem.Start.ToString("HH:mm")+" ":"")+ReminderEngine.Label(previewItem),Width<900?10:12,foreground:PlannerTheme.Muted);
                    caption.Name="MonthPreview"+day.ToString("yyyyMMdd")+(p==0?"":"_"+p);caption.Margin=new Thickness(6,0,0,0);caption.TextWrapping=TextWrapping.NoWrap;caption.TextTrimming=TextTrimming.CharacterEllipsis;Grid.SetColumn(caption,1);preview.Children.Add(caption);dateTop.Children.Add(preview);
                }
                if(items.Count>previewCount){StackPanel dots=Row();dots.Name="MonthDots"+day.ToString("yyyyMMdd");dots.HorizontalAlignment=HorizontalAlignment.Center;dots.VerticalAlignment=VerticalAlignment.Center;dots.Margin=new Thickness(0,3,0,2);for(int i=0;i<Math.Min(5,items.Count-previewCount);i++)dots.Children.Add(new Ellipse{Width=8,Height=8,Fill=PlannerTheme.SchedulePreview,Margin=new Thickness(3,0,3,0)});Grid.SetRow(dots,1);dates.Children.Add(dots);}
            }
            Button dateButton=Action("",()=>OnCalendarDateClick(day));dateButton.Name="Day"+day.ToString("yyyyMMdd");dateButton.Content=dates;dateButton.Padding=new Thickness(0);dateButton.Margin=new Thickness(0);dateButton.BorderThickness=new Thickness(0);dateButton.Background=Brushes.Transparent;dateButton.HorizontalContentAlignment=HorizontalAlignment.Stretch;dateButton.VerticalContentAlignment=VerticalAlignment.Stretch;
            dateButton.ToolTip=$"{day:yyyy年M月d日} {CalendarLabels.FullLunar(day)}\n"+(overridden?"单独设置：":"跟随每周规则：")+(rest?"休息日":"工作日");
            dateButton.VerticalAlignment=VerticalAlignment.Stretch;
            dateButton.MinHeight=0;
            dateButton.IsEnabled=day>=ReminderSchedule.MinimumDate&&day<=ReminderSchedule.MaximumDate;
            if(_batch&&!week)AttachCalendarDragHandlers(dateButton,grid,day);
            if(!week)dateButton.MouseDoubleClick+=(_,_)=>{if(!_batch){_date=day;_occurrenceDate=null;Edit(null,true,day);}};
            cell.Children.Add(dateButton);
            if(overridden)
            {
                var glyph=Text(rest?"休":"班",11,FontWeights.SemiBold,rest?PlannerTheme.RestForeground:PlannerTheme.WorkForeground);glyph.Margin=new Thickness(0);glyph.HorizontalAlignment=HorizontalAlignment.Center;
                cell.Children.Add(new Border{Name="OverrideBadge"+day.ToString("yyyyMMdd"),Child=glyph,Width=23,Height=23,Background=rest?PlannerTheme.DangerSoft:PlannerTheme.WorkBadgeBackground,CornerRadius=new CornerRadius(5),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false});
            }
            if(week)
            {
                if(todayDate)cell.Children.Add(new Border{Height=2,Width=32,Background=PlannerTheme.Accent,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false});
                Grid body=new(){Background=Brushes.Transparent};
                StackPanel cards=new();foreach(var item in items)cards.Children.Add(EventCard(item,day,true));
                var scroller=new ScrollViewer{Content=cards,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
                body.Children.Add(scroller);
                if(items.Count==0)
                {
                    StackPanel empty=new(){HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false,Opacity=.46};
                    var icon=PlannerTheme.Icon("calendar",23,PlannerTheme.Muted,0);icon.HorizontalAlignment=HorizontalAlignment.Center;empty.Children.Add(icon);
                    var hint=Text("暂无日程",12,foreground:PlannerTheme.Muted);hint.Margin=new Thickness(0,7,0,0);empty.Children.Add(hint);body.Children.Add(empty);
                }
                var create=Chip(Width<980?"新建日程":"创建当天日程",()=>{_date=day;_occurrenceDate=null;Edit(null,true,day);});
                create.Name="WeekCreate"+day.ToString("yyyyMMdd");create.Visibility=Visibility.Collapsed;create.Height=42;create.FontSize=13;create.Margin=items.Count==0?new Thickness(2,110,2,0):new Thickness(2,8,2,4);create.Foreground=PlannerTheme.Accent;create.Background=PlannerTheme.Soft;
                create.ToolTip="创建当天日程";
                if(items.Count==0){create.VerticalAlignment=VerticalAlignment.Center;body.Children.Add(create);}
                else cards.Children.Add(create);
                bool IsBlank(DependencyObject? source){for(var current=source;current!=null&&current!=body;current=VisualTreeHelper.GetParent(current))if(current is Button||current is System.Windows.Controls.Primitives.ScrollBar)return false;return true;}
                body.MouseMove+=(_,e)=>create.Visibility=IsBlank(e.OriginalSource as DependencyObject)||create.IsMouseOver?Visibility.Visible:Visibility.Collapsed;
                body.MouseLeave+=(_,_)=>create.Visibility=Visibility.Collapsed;
                // Creation requires the explicit button; blank column clicks are harmless.
                Grid.SetRow(body,1);cell.Children.Add(body);
            }
            Brush background=selected?PlannerTheme.CalendarSelectionFill:rest?PlannerTheme.RestBackground:week?PlannerTheme.WeekColumnBackground:PlannerTheme.WorkBackground;
            Border border=new(){Child=cell,Padding=new Thickness(week?7:Width<1200?4:7),BorderBrush=selected?(week?PlannerTheme.WeekSelectionLine:PlannerTheme.CalendarSelection):week?PlannerTheme.WeekColumnLine:PlannerTheme.Line,BorderThickness=new Thickness(1),Background=background,CornerRadius=week?new CornerRadius(8):new CornerRadius(0),Margin=week?new Thickness(2,2,2,2):new Thickness(0)};
            _calendarDateBorders[day]=border;Grid.SetColumn(border,n%7);Grid.SetRow(border,n/7+1);grid.Children.Add(border);
        }
         main.Children.Add(week?new Border{Name="WeekPlannerBoard",Child=grid,Background=Brushes.White,BorderBrush=PlannerTheme.WeekColumnLine,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(13),Padding=new Thickness(4),Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=System.Windows.Media.Color.FromRgb(64,107,139),Opacity=.07,BlurRadius=10,ShadowDepth=2}}:grid);layout.Children.Add(main);
        if(side){var panel=_batch?RestControls():DayDetails();Grid.SetColumn(panel,1);layout.Children.Add(panel);}
        _body.Content=layout;
        DockPanel footDock=new();
        StackPanel foot=Row();
        StackPanel dotLegend=Row();dotLegend.Children.Add(new Ellipse{Width=8,Height=8,Fill=PlannerTheme.SchedulePreview,Margin=new Thickness(3,0,6,0),VerticalAlignment=VerticalAlignment.Center});dotLegend.Children.Add(Text("有日程",11));foot.Children.Add(dotLegend);
        foot.Children.Add(LegendBadge("","休息日",PlannerTheme.RestBackground,PlannerTheme.RestForeground));
        foot.Children.Add(LegendBadge("","选中日期",PlannerTheme.CalendarSelectionFill,PlannerTheme.CalendarSelection,true));
        foot.Children.Add(LegendBadge("班","手动工作日",PlannerTheme.WorkBadgeBackground,PlannerTheme.WorkForeground));
        foot.Children.Add(LegendBadge("休","手动休息日",PlannerTheme.DangerSoft,PlannerTheme.RestForeground));
        if(_status.Parent is System.Windows.Controls.Panel statusParent)statusParent.Children.Remove(_status);
        if(Width<900){StackPanel compactFooter=new();compactFooter.Children.Add(foot);compactFooter.Children.Add(_status);_footer.Children.Add(compactFooter);}
        else{DockPanel.SetDock(foot,Dock.Right);footDock.Children.Add(foot);footDock.Children.Add(_status);_footer.Children.Add(footDock);}
    }
    private static UIElement LegendBadge(string glyph,string label,Brush background,Brush foreground,bool outlined=false)
    {
        StackPanel row=Row();row.Margin=new Thickness(10,0,0,0);
        row.Children.Add(new Border{Child=new TextBlock{Text=glyph,FontSize=10,Foreground=foreground,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0)},Width=18,Height=18,Background=background,BorderBrush=outlined?foreground:Brushes.Transparent,BorderThickness=new Thickness(outlined?1:0),CornerRadius=new CornerRadius(4),Margin=new Thickness(0,0,6,0),VerticalAlignment=VerticalAlignment.Center});
        row.Children.Add(Text(label,11));
        return row;
    }
    private IEnumerable<ReminderItem> DayItems(DateTime day)
        => _service.Book.Items
            .Select((item,index)=>(item,index))
            .Where(pair=>pair.item.Calendar&&ReminderSchedule.OccursOn(pair.item,_service.Book,day))
            .OrderBy(pair=>pair.item.HasTime?0:1)
            .ThenBy(pair=>pair.item.HasTime?pair.item.Start.TimeOfDay:TimeSpan.Zero)
            .ThenBy(pair=>pair.index)
            .Select(pair=>pair.item);
    private UIElement EventCard(ReminderItem item,DateTime day,bool week)
    {
        bool narrowWeek=week&&Width<900;
        StackPanel info=new(){Margin=narrowWeek?new Thickness(5,7,2,7):new Thickness(10)};
        if(item.HasTime)
        {
            StackPanel timeLine=Row();
            var time=Text(item.Start.ToString("HH:mm"),narrowWeek?11:week?13:14,FontWeights.Medium,PlannerTheme.TimeInk);time.Name="ScheduleTime";time.Margin=new Thickness(0);timeLine.Children.Add(time);
            if(item.ReminderCreated==true){var alarm=PlannerTheme.Icon("alarm",narrowWeek?12:14,PlannerTheme.Accent,0);alarm.Name="ScheduleAlarmIcon";alarm.Margin=new Thickness(narrowWeek?4:6,0,0,0);alarm.ToolTip=item.Enabled?"已创建闹钟提醒":"闹钟提醒已暂停";timeLine.Children.Add(alarm);}
            info.Children.Add(timeLine);
        }
        var title=Text(ReminderEngine.Label(item),narrowWeek?14:week?15:16,FontWeights.Bold);title.Name="ScheduleTitle";title.Margin=new Thickness(0,item.HasTime?3:0,0,0);title.TextWrapping=TextWrapping.NoWrap;title.TextTrimming=TextTrimming.CharacterEllipsis;info.Children.Add(title);
        if(!string.IsNullOrWhiteSpace(item.Notes))
        {
            var notes=Text(item.Notes,narrowWeek?10:11,foreground:PlannerTheme.Muted);notes.Name="ScheduleNotes";notes.Margin=new Thickness(0,3,0,0);notes.TextWrapping=TextWrapping.Wrap;notes.LineHeight=narrowWeek?17:19;notes.MaxHeight=(narrowWeek?17:19)*3;notes.TextTrimming=TextTrimming.CharacterEllipsis;info.Children.Add(notes);
        }
        var button=Action("",()=>{_occurrenceDate=day;Edit(item,true);});button.Name="ScheduleCard"+item.Id.ToString("N");button.Content=info;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Padding=new Thickness(0);button.Margin=new Thickness(0,week?5:4,0,week?5:4);button.MinHeight=0;button.Background=Brushes.White;button.BorderBrush=week?PlannerTheme.WeekCardLine:PlannerTheme.Line;button.ToolTip="点击编辑日程";
        if(week)button.Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=System.Windows.Media.Color.FromRgb(60,93,126),Opacity=.06,BlurRadius=5,ShadowDepth=1};
        return button;
    }
    private UIElement DayDetails()
    {
        var items=DayItems(_date).ToList();DateTime detailDay=_date;
        DockPanel panel=new();DockPanel title=new(){Margin=new Thickness(0,0,0,8)};
        var close=IconButton("close",()=>{_details=false;Render();},"收起当日日程");DockPanel.SetDock(close,Dock.Right);title.Children.Add(close);title.Children.Add(Text($"{detailDay:M月d日}  周{WeekdayLabel(detailDay)}",20,FontWeights.SemiBold));DockPanel.SetDock(title,Dock.Top);panel.Children.Add(title);
        var lunar=Text(CalendarLabels.FullLunar(detailDay).Replace("农历","")+" · "+items.Count+"项安排",12,foreground:PlannerTheme.Muted);lunar.Name="DayScheduleCount";lunar.Margin=new Thickness(3,0,0,15);DockPanel.SetDock(lunar,Dock.Top);panel.Children.Add(lunar);
        var add=Chip("创建当天日程",()=>{_occurrenceDate=null;Edit(null,true,detailDay);});add.Name="AddSelectedDay";add.Height=44;add.HorizontalAlignment=HorizontalAlignment.Stretch;add.Background=PlannerTheme.Soft;add.Foreground=PlannerTheme.Accent;add.BorderBrush=PlannerTheme.Accent;DockPanel.SetDock(add,Dock.Bottom);panel.Children.Add(add);
        StackPanel list=new();foreach(var item in items)list.Children.Add(EventCard(item,detailDay,false));
        if(items.Count==0){var empty=Text("当日暂无日程",14,foreground:PlannerTheme.Muted);empty.HorizontalAlignment=HorizontalAlignment.Center;empty.Margin=new Thickness(0,40,0,0);list.Children.Add(empty);}
        panel.Children.Add(new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        return new Border{Child=panel,Background=Brushes.White,BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(16),Margin=new Thickness(4,0,2,0)};
    }
    private void ItemMenu(ReminderItem item,DateTime day)
    {
        System.Windows.Controls.ContextMenu menu=new(){Style=(Style)FindResource("PlannerContextMenu")};foreach(var label in new[]{"编辑日程","仅删除当天","删除整组…","管理日程…"}){var entry=new System.Windows.Controls.MenuItem{Header=label,Style=(Style)FindResource("PlannerMenuItem")};entry.Click+=(_,_)=>{if(label=="编辑日程"){_occurrenceDate=day;Edit(item,true);}else if(label=="管理日程…"){OpenManage();}else ConfirmDelete([item.Id],label=="仅删除当天"?day:null);};menu.Items.Add(entry);}menu.IsOpen=true;
    }
    private void SetView(bool week){if(week)_batch=false;else _details=false;_=Execute(b=>b.WeekView=week);}
    private static DateTime WeekStart(DateTime date)=>date.Date.AddDays(-((int)date.DayOfWeek+6)%7);
    private void MoveDate(int offset,bool week){DateTime next=week?_date.AddDays(offset):_date.AddMonths(offset);if(next>=ReminderSchedule.MinimumDate&&next<=ReminderSchedule.MaximumDate)_date=next;Render();}
    private System.Windows.Controls.Primitives.Popup CreateMonthPicker(Button anchor)
    {
        var popup=new System.Windows.Controls.Primitives.Popup{Name="MonthPickerPopup",PlacementTarget=anchor,Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom,VerticalOffset=5,StaysOpen=false,AllowsTransparency=true,PopupAnimation=System.Windows.Controls.Primitives.PopupAnimation.Fade};
        StackPanel panel=new(){Width=350};
        DockPanel yearRow=new(){LastChildFill=false,Margin=new Thickness(2,1,2,10)};
        StackPanel yearHeading=new();yearHeading.Children.Add(Text("选择年月",14,FontWeights.SemiBold));var yearError=Text("",10,foreground:PlannerTheme.Danger);yearError.Name="MonthPickerError";yearError.Visibility=Visibility.Collapsed;yearHeading.Children.Add(yearError);yearRow.Children.Add(yearHeading);
        StackPanel yearControls=Row();DockPanel.SetDock(yearControls,Dock.Right);
        TextBox yearInput=new(){Name="MonthPickerYear",Text=_date.Year.ToString(CultureInfo.InvariantCulture),MaxLength=4,Width=64,Height=32,Padding=new Thickness(3,5,3,5),FontSize=14,TextAlignment=TextAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(2,0,2,0),BorderBrush=PlannerTheme.Line,Background=PlannerTheme.ChipBackground};
        var yearBack=IconButton("chevron-left",()=>ChangeYear(-1),"上一年",15);yearBack.Name="MonthPickerPreviousYear";yearBack.Width=32;yearBack.Height=32;yearBack.Padding=new Thickness(3);
        var yearNext=IconButton("chevron-right",()=>ChangeYear(1),"下一年",15);yearNext.Name="MonthPickerNextYear";yearNext.Width=32;yearNext.Height=32;yearNext.Padding=new Thickness(3);
        yearControls.Children.Add(yearBack);yearControls.Children.Add(yearInput);yearControls.Children.Add(Text("年",13));yearControls.Children.Add(yearNext);yearRow.Children.Add(yearControls);panel.Children.Add(yearRow);
        Grid months=new();for(int c=0;c<4;c++)months.ColumnDefinitions.Add(new());for(int r=0;r<3;r++)months.RowDefinitions.Add(new(){Height=GridLength.Auto});
        List<Button> monthButtons=[];
        for(int month=1;month<=12;month++)
        {
            int targetMonth=month;var pick=Chip(month+"月",()=>
            {
                if(!int.TryParse(yearInput.Text,NumberStyles.None,CultureInfo.InvariantCulture,out int year)||year<ReminderSchedule.MinimumDate.Year||year>ReminderSchedule.MaximumDate.Year){yearInput.BorderBrush=PlannerTheme.Danger;yearError.Text="请输入2026～2099";yearError.Visibility=Visibility.Visible;return;}
                popup.IsOpen=false;_date=new DateTime(year,targetMonth,Math.Min(_date.Day,DateTime.DaysInMonth(year,targetMonth)));Render();
            });
            pick.Name="PickMonth"+month.ToString("00",CultureInfo.InvariantCulture);pick.Width=74;pick.Height=36;pick.Padding=new Thickness(4,2,4,2);pick.Margin=new Thickness(3);Grid.SetColumn(pick,(month-1)%4);Grid.SetRow(pick,(month-1)/4);months.Children.Add(pick);monthButtons.Add(pick);
        }
        panel.Children.Add(months);
        void Refresh()
        {
            bool valid=int.TryParse(yearInput.Text,NumberStyles.None,CultureInfo.InvariantCulture,out int year)&&year>=ReminderSchedule.MinimumDate.Year&&year<=ReminderSchedule.MaximumDate.Year;
            yearError.Text=valid?"":"请输入2026～2099";yearError.Visibility=valid?Visibility.Collapsed:Visibility.Visible;yearInput.BorderBrush=valid?PlannerTheme.Line:PlannerTheme.Danger;yearBack.IsEnabled=valid&&year>ReminderSchedule.MinimumDate.Year;yearNext.IsEnabled=valid&&year<ReminderSchedule.MaximumDate.Year;
            for(int n=0;n<monthButtons.Count;n++){bool selected=valid&&year==_date.Year&&n+1==_date.Month;monthButtons[n].Background=selected?PlannerTheme.PrimaryFill:Brushes.White;monthButtons[n].Foreground=selected?Brushes.White:PlannerTheme.Ink;monthButtons[n].BorderBrush=selected?PlannerTheme.PrimaryFill:PlannerTheme.Line;monthButtons[n].FontWeight=selected?FontWeights.SemiBold:FontWeights.Normal;}
        }
        void ChangeYear(int offset){int year=int.TryParse(yearInput.Text,NumberStyles.None,CultureInfo.InvariantCulture,out int typed)?typed:_date.Year;yearInput.Text=Math.Min(Math.Max(year+offset,ReminderSchedule.MinimumDate.Year),ReminderSchedule.MaximumDate.Year).ToString(CultureInfo.InvariantCulture);}
        yearInput.TextChanged+=(_,_)=>Refresh();Refresh();
        popup.Opened+=(_,_)=>{yearInput.Text=_date.Year.ToString(CultureInfo.InvariantCulture);Refresh();yearInput.Focus();yearInput.SelectAll();};
        panel.PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape){popup.IsOpen=false;e.Handled=true;}};
        popup.Child=new Border{Child=panel,Background=Brushes.White,BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(12)};
        return popup;
    }
    private void EditWorkdays(){_batch=true;_alarm=false;Render();}
    private UIElement RestControls()
    {
        Grid inner=new();
        foreach(var height in new[]{GridLength.Auto,GridLength.Auto,GridLength.Auto,GridLength.Auto,GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto})inner.RowDefinitions.Add(new(){Height=height});
        DockPanel title=new(){Margin=new Thickness(0,0,0,22)};var close=IconButton("close",()=>{_batch=false;_selected.Clear();_details=false;Render();},"关闭班休面板");close.Name="WorkdaysClose";DockPanel.SetDock(close,Dock.Right);title.Children.Add(close);
        title.Children.Add(Text("调整班休",22,FontWeights.SemiBold));Grid.SetRow(title,0);inner.Children.Add(title);
        var weeklyTitle=Text("每周休息",15,FontWeights.SemiBold);weeklyTitle.Margin=new Thickness(0,5,0,0);Grid.SetRow(weeklyTitle,1);inner.Children.Add(weeklyTitle);
        Grid days=new(){Margin=new Thickness(0,10,0,16)};for(int c=0;c<4;c++)days.ColumnDefinitions.Add(new());days.RowDefinitions.Add(new(){Height=GridLength.Auto});days.RowDefinitions.Add(new(){Height=GridLength.Auto});
        for(int n=0;n<Days.Length;n++)
        {
            DayOfWeek day=Days[n];bool rest=_service.Book.RestWeekdays.Contains(day);
            var btn=AsyncChip("",()=>Execute(b=>{var selected=b.RestWeekdays.ToList();if(!selected.Remove(day))selected.Add(day);ReminderSchedule.SetRestWeekdays(b,selected,DateTime.Now);}));
            btn.Name="RestWeekday"+n;btn.Margin=new Thickness(2);btn.Height=34;btn.Padding=new Thickness(2,4,2,4);btn.Background=rest?PlannerTheme.RestChoiceFill:Brushes.White;btn.Foreground=rest?PlannerTheme.RestForeground:PlannerTheme.Ink;btn.BorderBrush=rest?PlannerTheme.RestChoiceLine:PlannerTheme.Line;btn.Content="周"+"一二三四五六日"[n];Grid.SetColumn(btn,n%4);Grid.SetRow(btn,n/4);days.Children.Add(btn);
        }
        Grid.SetRow(days,2);inner.Children.Add(days);
        Border ruleDivider=new(){Height=1,Background=PlannerTheme.Line,Margin=new Thickness(0,4,0,10)};Grid.SetRow(ruleDivider,3);inner.Children.Add(ruleDivider);
        DockPanel specifiedHead=new(){LastChildFill=false};specifiedHead.Children.Add(Text("指定日期",15,FontWeights.SemiBold));specifiedHead.Children.Add(Text($"已选 {_selected.Count} 天",13,foreground:PlannerTheme.Muted));
        Button clearSelected=new(){Name="ClearSelectedDates",Style=(Style)FindResource("PlannerLink"),IsEnabled=_selected.Count>0,ToolTip="清空已选日期"};StackPanel clearLabel=Row();clearLabel.Children.Add(PlannerTheme.Icon("trash",13,PlannerTheme.Muted,4));clearLabel.Children.Add(Text("清空选择",12,foreground:PlannerTheme.Accent));clearSelected.Content=clearLabel;clearSelected.Click+=(_,_)=>{_selected.Clear();Render();};DockPanel.SetDock(clearSelected,Dock.Right);specifiedHead.Children.Add(clearSelected);Grid.SetRow(specifiedHead,4);inner.Children.Add(specifiedHead);
        System.Windows.Controls.Primitives.UniformGrid selectedGrid=new(){Columns=2,Margin=new Thickness(0,4,0,0),VerticalAlignment=VerticalAlignment.Top};
        int selectedCount=_selected.Count;
        foreach(var day in _selected.OrderBy(d=>d))
        {
            bool overridden=_service.Book.RestOverrides.TryGetValue(day.ToString("yyyy-MM-dd"),out bool rest);
            StackPanel entry=Row();var label=Text($"{day:M/d} 周{WeekdayLabel(day)}",12);label.Margin=new Thickness(0);entry.Children.Add(label);
            if(overridden)entry.Children.Add(new Ellipse{Width=6,Height=6,Fill=rest?PlannerTheme.Danger:PlannerTheme.WorkForeground,Margin=new Thickness(5,0,0,0),VerticalAlignment=VerticalAlignment.Center});
            var remove=IconButton("close",()=>{_selected.Remove(day);Render();},"移除该日期",12,PlannerTheme.Muted);remove.Width=23;remove.Height=24;remove.Padding=new Thickness(4);remove.Margin=new Thickness(2,0,0,0);entry.Children.Add(remove);
            selectedGrid.Children.Add(new Border{Child=entry,Background=Brushes.White,BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6),Padding=new Thickness(6,2,2,2),Margin=new Thickness(2,3,4,3),ToolTip=overridden?"单独设置："+(rest?"休息日":"工作日"):"跟随每周规则"});
        }
        ScrollViewer selectedScroll=new(){Name="WorkdaysSelectedScroll",Content=selectedGrid,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        UIElement selectedContent=selectedScroll;
        if(selectedCount==0)
        {
            StackPanel empty=new(){HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
            var icon=PlannerTheme.Icon("calendar",46,PlannerTheme.Muted,0);icon.HorizontalAlignment=HorizontalAlignment.Center;icon.Opacity=.45;empty.Children.Add(icon);
            var hint=Text("点击左侧日期，可多选或拖动选择\n再设为工作日或休息日",12,foreground:PlannerTheme.Muted);hint.TextAlignment=TextAlignment.Center;hint.Margin=new Thickness(0,14,0,0);empty.Children.Add(hint);selectedContent=empty;
        }
        Border selectedArea=new(){Name="WorkdaysSelectedArea",Child=selectedContent,Background=Brushes.White,BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(6),Margin=new Thickness(0,10,0,6),MinHeight=Width<900?80:130,VerticalAlignment=VerticalAlignment.Stretch};Grid.SetRow(selectedArea,5);inner.Children.Add(selectedArea);
        StackPanel actions=new(){Margin=new Thickness(0,8,0,0)};
        Button setRest=AsyncAction("设为休息日",()=>SetRest(true));setRest.Name="SetSelectedRestDays";setRest.Background=PlannerTheme.RestActionFill;setRest.Foreground=Brushes.White;setRest.BorderThickness=new Thickness(0);
        Button setWork=AsyncAction("设为工作日",()=>SetRest(false));setWork.Name="SetSelectedWorkdays";setWork.Background=PlannerTheme.WorkActionFill;setWork.Foreground=Brushes.White;setWork.BorderThickness=new Thickness(0);
        Button reset=AsyncAction("恢复每周安排",()=>SetRest(null));reset.Name="ResetSelectedRestDays";reset.Background=Brushes.White;reset.Foreground=PlannerTheme.Muted;reset.BorderBrush=PlannerTheme.Line;reset.ToolTip="取消所选日期的单独设置，恢复跟随每周规则。";
        foreach(var b in new[]{setWork,setRest,reset}){b.IsEnabled=_selected.Count>0;b.MinHeight=0;b.Height=40;b.HorizontalAlignment=HorizontalAlignment.Stretch;b.Margin=new Thickness(0,3,0,3);actions.Children.Add(b);}Grid.SetRow(actions,6);inner.Children.Add(actions);
        return new Border{Child=inner,Background=Brushes.White,BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(Width<900?10:16),Margin=new Thickness(4,0,2,0)};
    }
    private async Task SetRest(bool? rest)
    {
        if(_selected.Count==0)return;
        DateTime[] selected=_selected.OrderBy(d=>d).ToArray();
        await Execute(b=>{foreach(var d in selected)ReminderSchedule.SetRestOverride(b,d,rest,DateTime.Now);});
        _selected.Clear();
        Render();
        if(rest==null)_status.Text="已恢复每周安排，所选日期将跟随每周规则。";
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
            _calendarDragStartDay=day;
            _calendarDragLastDay=null;
            _calendarDragLastPoint=System.Windows.Input.Mouse.GetPosition(grid);
            _calendarDragLastDirection=null;
            System.Windows.Input.Mouse.Capture(dateButton,System.Windows.Input.CaptureMode.Element);
        };
    }

    private void ContinueCalendarDrag(Grid grid,System.Windows.Input.MouseEventArgs e)
    {
        if(!_calendarDragActive||!_batch||e.LeftButton!=System.Windows.Input.MouseButtonState.Pressed)return;
        Point point=System.Windows.Input.Mouse.GetPosition(grid);
        if((point-_calendarDragLastPoint).Length<1)return;
        Vector direction=point-_calendarDragLastPoint;
        if(_calendarDragLastDirection is Vector previousDirection&&previousDirection.X*direction.X+previousDirection.Y*direction.Y<0)_calendarDragLastDay=null;
        _calendarDragLastDirection=direction;
        AddCalendarDragPath(grid,_calendarDragLastPoint,point);
        _calendarDragLastPoint=point;
    }

    private void EndCalendarDrag(System.Windows.Input.MouseButtonEventArgs e)
    {
        if(!_calendarDragActive||e.ChangedButton!=System.Windows.Input.MouseButton.Left)return;
        bool moved=_calendarDragMoved;
        _calendarDragActive=false;
        _calendarDragMoved=false;
        _calendarDragStartDay=null;
        _calendarDragLastDay=null;
        _calendarDragLastDirection=null;
        if(!moved)return;
        System.Windows.Input.Mouse.Capture(null);
        _suppressCalendarClick=true;
        e.Handled=true;
        Render();
        Dispatcher.BeginInvoke(new Action(()=>_suppressCalendarClick=false),System.Windows.Threading.DispatcherPriority.Input);
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
                if(_calendarDragLastDay==day)continue;
                _calendarDragLastDay=day;
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
            pair.Value.BorderBrush=selected?PlannerTheme.CalendarSelection:PlannerTheme.Line;
            pair.Value.BorderThickness=new Thickness(1);
            bool rest=_service.Book.IsRest(pair.Key);
            pair.Value.Background=rest?PlannerTheme.RestBackground:PlannerTheme.WorkBackground;
        }
    }
    private void RenderAlarms()
    {
        DateTime now=DateTime.Now;
        StackPanel page=new(){Margin=new Thickness(4,0,4,12)};
        DockPanel actions=new(){Height=58,Margin=new Thickness(4,0,4,8),LastChildFill=false};
        var add=Primary(Action("",()=>Edit(null,false)));StackPanel addLabel=Row();addLabel.Children.Add(Text("新建提醒",16,FontWeights.SemiBold,Brushes.White));add.Content=addLabel;add.Name="NewAlarm";add.Width=190;add.Height=48;add.Padding=new Thickness(18,9,18,9);DockPanel.SetDock(add,Dock.Right);actions.Children.Add(add);page.Children.Add(actions);

        var running=_service.Book.Items.Where(IsRunningCountdown).OrderBy(i=>i.Start).ToList();
        var alarms=_service.Book.Items.Where(i=>!i.Relative&&!i.Calendar).OrderBy(i=>NextDisplayOccurrence(i,now)??DateTime.MaxValue).ThenBy(i=>i.Start).ToList();
        var calendar=_service.Book.Items.Where(i=>i.Calendar&&i.ReminderCreated==true&&HasVisibleCalendarReminder(i,now)).OrderBy(i=>NextDisplayOccurrence(i,now)??DateTime.MaxValue).ThenBy(i=>i.Start).ToList();
        if(running.Count>0)
        {
            page.Children.Add(AlarmSectionHeading("正在倒计时","hourglass",running.Count));
            foreach(ReminderItem item in running)page.Children.Add(CountdownPanel(item));
        }
        if(alarms.Count>0)
        {
            page.Children.Add(AlarmSectionHeading("我的闹钟","clock",alarms.Count));
            page.Children.Add(AlarmGrid(alarms,false));
        }
        if(calendar.Count>0)
        {
            page.Children.Add(AlarmSectionHeading("日历提醒","calendar",calendar.Count));
            var sourceHint=Text("显示已开启提醒的日历日程，来自你绑定的日历。",13,foreground:PlannerTheme.Muted);sourceHint.Margin=new Thickness(6,0,0,10);page.Children.Add(sourceHint);page.Children.Add(AlarmGrid(calendar,true));
        }
        if(running.Count+alarms.Count+calendar.Count==0)
        {
            var empty=Text("还没有提醒，点击「新建提醒」开始。",14,foreground:PlannerTheme.Muted);empty.Margin=new Thickness(8,30,8,8);page.Children.Add(empty);
        }
        _body.Content=page;
    }

    private static bool IsRunningCountdown(ReminderItem item) => item.Relative && (item.PausedSeconds!=null || item.Enabled && (item.Start>DateTime.Now));

    private UIElement AlarmSectionHeading(string title,string icon,int count)
    {
        StackPanel heading=Row();heading.Margin=new Thickness(8,8,8,6);heading.Children.Add(PlannerTheme.Icon(icon,22,PlannerTheme.Accent,8));heading.Children.Add(Text(title,18,FontWeights.SemiBold));var badgeText=Text(count.ToString(),11,FontWeights.SemiBold,PlannerTheme.Accent);badgeText.HorizontalAlignment=HorizontalAlignment.Center;badgeText.TextAlignment=TextAlignment.Center;heading.Children.Add(new Border{Name="AlarmCountBadge",Background=PlannerTheme.AccentSoft,CornerRadius=new CornerRadius(10),MinWidth=23,Height=19,Padding=new Thickness(6,0,6,0),Margin=new Thickness(7,1,0,0),Child=badgeText});return heading;
    }

    private UIElement CountdownPanel(ReminderItem item)
    {
        bool compact=Width<900;
        Grid content=new();content.ColumnDefinitions.Add(new(){Width=new GridLength(compact?210:290)});content.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});content.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        TextBlock remaining=Text("",compact?34:50,FontWeights.SemiBold,PlannerTheme.Accent);remaining.FontFamily=new System.Windows.Media.FontFamily("Segoe UI");_running.Add((remaining,item));UpdateRemaining();StackPanel clock=Row();clock.VerticalAlignment=VerticalAlignment.Center;clock.Children.Add(PlannerTheme.Icon("hourglass",compact?22:32,PlannerTheme.Accent,compact?7:16));clock.Children.Add(remaining);content.Children.Add(clock);
        StackPanel details=new(){VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(compact?4:12,0,compact?4:12,0)};details.Children.Add(Text(string.IsNullOrWhiteSpace(item.Title)?"倒计时":item.Title,compact?15:22,FontWeights.SemiBold));string meta=item.PausedSeconds!=null?$"共 {FormatDuration(item.DurationSeconds)} · 已暂停":$"共 {FormatDuration(item.DurationSeconds)} · 预计 {FormatCountdownEnd(item.Start)} 结束";details.Children.Add(Text(meta,compact?11:13,foreground:PlannerTheme.Muted));var detailsDivider=new Border{Child=details,BorderBrush=PlannerTheme.ControlLine,BorderThickness=new Thickness(1,0,0,0),Padding=new Thickness(compact?6:14,0,0,0)};Grid.SetColumn(detailsDivider,1);content.Children.Add(detailsDivider);
        StackPanel controls=Row();controls.VerticalAlignment=VerticalAlignment.Center;var pause=Chip(item.PausedSeconds==null?"暂停":"继续",()=>_=Execute(b=>{if(item.PausedSeconds==null)ReminderEngine.Pause(b,item.Id,DateTime.Now);else ReminderEngine.Resume(b,item.Id,DateTime.Now);}));pause.Name="PauseCountdown";pause.Width=compact?90:132;pause.Height=compact?44:54;StackPanel pauseLabel=Row();pauseLabel.Children.Add(PlannerTheme.Icon(item.PausedSeconds==null?"pause":"play",compact?17:22,PlannerTheme.Accent,compact?5:9));pauseLabel.Children.Add(Text(item.PausedSeconds==null?"暂停":"继续",compact?13:16,FontWeights.SemiBold,PlannerTheme.Accent));pause.Content=pauseLabel;controls.Children.Add(pause);var cancel=AsyncAction("",()=>Execute(b=>b.Items.RemoveAll(i=>i.Id==item.Id)));cancel.Style=(Style)FindResource("PlannerDanger");StackPanel cancelLabel=Row();cancelLabel.Children.Add(PlannerTheme.Icon("trash",compact?17:20,PlannerTheme.Danger,compact?5:9));cancelLabel.Children.Add(Text("取消",compact?13:16,FontWeights.SemiBold,PlannerTheme.Danger));cancel.Content=cancelLabel;cancel.Height=compact?44:54;cancel.Width=compact?90:132;controls.Children.Add(cancel);Grid.SetColumn(controls,2);content.Children.Add(controls);
        return new Border{Name="CountdownPanel",Child=content,Background=PlannerTheme.Soft,BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(compact?14:18,compact?10:14,compact?14:18,compact?10:14),Margin=new Thickness(4,0,4,12)};
    }

    private Grid AlarmGrid(IReadOnlyList<ReminderItem> items,bool calendar)
    {
        Grid grid=new(){Name=calendar?"CalendarAlarmGrid":"MyAlarmGrid",Margin=new Thickness(_pageSize=="mini"?10:16,0,_pageSize=="mini"?10:16,12)};grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(_pageSize=="mini"?18:22)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        for(int index=0;index<items.Count;index++)
        {
            int row=index/2;while(grid.RowDefinitions.Count<=row)grid.RowDefinitions.Add(new(){Height=GridLength.Auto});UIElement card=AlarmCard(items[index],calendar);Grid.SetRow(card,row);Grid.SetColumn(card,index%2==0?0:2);grid.Children.Add(card);
        }
        return grid;
    }

    private UIElement AlarmCard(ReminderItem item,bool calendar)
    {
        DateTime now=DateTime.Now;DateTime? structural=NextStructuralOccurrence(item,now);DateTime? next=NextDisplayOccurrence(item,now);bool ended=structural==null&&!HasActiveOccurrence(item);bool mini=_pageSize=="mini";
        double nextOccurrenceColumnWidth=mini?104:112;
        Grid layout=new(){Height=mini?70:76};layout.RowDefinitions.Add(new(){Height=new GridLength(mini?35:38)});layout.RowDefinitions.Add(new(){Height=new GridLength(mini?35:38)});layout.ColumnDefinitions.Add(new(){Width=new GridLength(mini?88:98)});layout.ColumnDefinitions.Add(new(){Width=new GridLength(1)});layout.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});layout.ColumnDefinitions.Add(new(){Width=new GridLength(nextOccurrenceColumnWidth)});layout.ColumnDefinitions.Add(new(){Width=new GridLength(0)});
        TextBlock time=Text(item.Start.ToString("HH:mm"),mini?26:29,FontWeights.SemiBold);time.Name="AlarmTime";time.FontFamily=new System.Windows.Media.FontFamily("Segoe UI");time.VerticalAlignment=VerticalAlignment.Center;time.Margin=new Thickness(5,0,5,0);Grid.SetRowSpan(time,2);layout.Children.Add(time);
        Border divider=new(){Background=PlannerTheme.Line,Width=1,Margin=new Thickness(0,9,0,9)};Grid.SetColumn(divider,1);Grid.SetRowSpan(divider,2);layout.Children.Add(divider);
        TextBlock name=Text(string.IsNullOrWhiteSpace(item.Title)?"闹钟":item.Title,mini?13:14,FontWeights.SemiBold);name.Name="AlarmName";name.TextWrapping=TextWrapping.NoWrap;name.TextTrimming=TextTrimming.CharacterEllipsis;name.Margin=new Thickness(mini?8:10,0,5,0);Grid.SetColumn(name,2);layout.Children.Add(name);
        var toggle=new CheckBox{Name="AlarmEnabled",IsChecked=ended&&!calendar?false:item.Enabled,IsEnabled=calendar?!ended:true,Style=(Style)FindResource("PlannerSwitch"),Margin=new Thickness(3),VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};toggle.Click+=async(_,_)=>{try{await Execute(b=>ReminderEngine.SetEnabled(b,item.Id,toggle.IsChecked==true,DateTime.Now));}catch{toggle.IsChecked=item.Enabled;}};Grid.SetColumn(toggle,3);layout.Children.Add(toggle);
        var more=IconButton("ellipsis",()=>{},calendar?"日程操作":"闹钟操作",17,PlannerTheme.Muted);more.Name="AlarmMore";more.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetColumn(more,4);// No overflow menu; card opens its matching editor.

        Grid meta=new(){Name="AlarmMeta",Margin=new Thickness(mini?8:10,0,2,0)};meta.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});meta.ColumnDefinitions.Add(new(){Width=new GridLength(nextOccurrenceColumnWidth)});
        UIElement period=calendar?CalendarSourceTag():AlarmPeriod(item);meta.Children.Add(period);
        Grid nextRow=new(){VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(mini?5:7,0,0,0)};nextRow.ColumnDefinitions.Add(new(){Width=GridLength.Auto});nextRow.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});nextRow.Children.Add(PlannerTheme.Icon("clock",mini?13:15,PlannerTheme.Muted,mini?4:5));string nextLabel=item.Enabled?(next is DateTime value?FormatNextOccurrence(value,now):"已结束"):(ended?"已结束":"已关闭");var nextText=Text(nextLabel,mini?10:12,foreground:PlannerTheme.Muted);nextText.Name="AlarmNext";nextText.TextWrapping=TextWrapping.NoWrap;nextText.TextTrimming=TextTrimming.CharacterEllipsis;nextText.ToolTip=nextLabel;Grid.SetColumn(nextText,1);nextRow.Children.Add(nextText);Grid.SetColumn(nextRow,1);meta.Children.Add(nextRow);var metaLine=new Border{Width=1,Height=mini?17:19,Background=PlannerTheme.Line,HorizontalAlignment=HorizontalAlignment.Left};Grid.SetColumn(metaLine,1);meta.Children.Add(metaLine);Grid.SetRow(meta,1);Grid.SetColumn(meta,2);Grid.SetColumnSpan(meta,3);layout.Children.Add(meta);

        if(!item.Enabled){time.Opacity=.58;name.Opacity=.62;meta.Opacity=.72;}
        Border card=new(){Name=(calendar?"CalendarAlarmCard":"AlarmCard")+item.Id.ToString("N"),Child=layout,Width=_pageSize switch{"mini"=>330,"standard"=>370,"comfortable"=>395,_=>420},HorizontalAlignment=HorizontalAlignment.Center,Background=Brushes.White,BorderBrush=PlannerTheme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(11),Padding=new Thickness(mini?7:8,mini?5:6,mini?7:8,mini?5:6),Margin=new Thickness(4,4,4,4),Cursor=System.Windows.Input.Cursors.Hand};
        card.MouseEnter+=(_,_)=>{card.BorderBrush=PlannerTheme.Accent;};card.MouseLeave+=(_,_)=>{card.BorderBrush=item.Enabled?PlannerTheme.Line:new SolidColorBrush(Color.FromRgb(226,237,245));};
        card.MouseLeftButtonUp+=(_,e)=>{if(IsInteractiveSource(e.OriginalSource as DependencyObject,card))return;_occurrenceDate=calendar?next:item.Start;Edit(item,calendar);e.Handled=true;};
        return card;
    }

    private void OpenAlarmMenu(Button target,ReminderItem item,bool calendar)
    {
        ContextMenu menu=new(){Style=(Style)FindResource("PlannerContextMenu"),PlacementTarget=target,Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom};
        MenuItem edit=new(){Header=calendar?"打开日程":"编辑闹钟",Style=(Style)FindResource("PlannerMenuItem")};edit.Click+=(_,_)=>{_occurrenceDate=calendar?NextDisplayOccurrence(item,DateTime.Now):null;Edit(item,calendar);};menu.Items.Add(edit);
        if(!calendar){MenuItem delete=new(){Header="删除提醒…",Style=(Style)FindResource("PlannerMenuItem"),Foreground=PlannerTheme.Danger};delete.Click+=(_,_)=>ConfirmDelete([item.Id],null);menu.Items.Add(delete);}
        menu.IsOpen=true;
    }

    private static bool IsInteractiveSource(DependencyObject? source,DependencyObject stop)
    {
        for(DependencyObject? current=source;current!=null&&current!=stop;current=VisualTreeHelper.GetParent(current))if(current is Button or CheckBox or MenuItem)return true;
        return false;
    }

    private UIElement AlarmPeriod(ReminderItem item)
    {
        string icon=item.Repeat switch{ReminderRepeat.Once=>"alarm",ReminderRepeat.Daily=>"repeat",ReminderRepeat.Weekly=>"repeat7",ReminderRepeat.Workdays=>"briefcase",ReminderRepeat.RestDays=>"coffee",ReminderRepeat.Dates=>"calendar-check",_=>"alarm"};string label=item.Repeat==ReminderRepeat.Weekly?WeeklySummary(item.Weekdays):item.Repeat switch{ReminderRepeat.Once=>"单次",ReminderRepeat.Daily=>"每天",ReminderRepeat.Workdays=>"工作日",ReminderRepeat.RestDays=>"休息日",ReminderRepeat.Dates=>"指定日期",_=>"单次"};Grid row=new(){VerticalAlignment=VerticalAlignment.Center};row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.Children.Add(PlannerTheme.Icon(icon,16,PlannerTheme.Muted,6));TextBlock text=Text(label,13,foreground:PlannerTheme.Muted);text.Name="AlarmPeriod";text.TextWrapping=TextWrapping.NoWrap;text.TextTrimming=TextTrimming.CharacterEllipsis;text.ToolTip=label;Grid.SetColumn(text,1);row.Children.Add(text);return row;
    }

    private static UIElement CalendarSourceTag()
    {
        StackPanel row=Row();row.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(PlannerTheme.Icon("calendar",16,PlannerTheme.Muted,6));row.Children.Add(Text("来自日历",13,foreground:PlannerTheme.Muted));return new Border{Name="CalendarSource",Background=Brushes.Transparent,Padding=new Thickness(0),HorizontalAlignment=HorizontalAlignment.Left,Child=row};
    }

    private static string WeeklySummary(IEnumerable<DayOfWeek> values)
    {
        HashSet<DayOfWeek> selected=new(values);if(selected.Count==7)return "每天";const string labels="一二三四五六日";return "每周"+string.Concat(Days.Select((day,index)=>(day,index)).Where(pair=>selected.Contains(pair.day)).Select(pair=>labels[pair.index]));
    }

    private bool HasVisibleCalendarReminder(ReminderItem item,DateTime now)=>HasActiveOccurrence(item)||NextStructuralOccurrence(item,now)!=null;
    private bool HasActiveOccurrence(ReminderItem item)=>_service.Book.Occurrences.Any(o=>o.RuleId==item.Id&&o.Phase is not ReminderPhase.Done and not ReminderPhase.Cancelled);
    private DateTime? NextDisplayOccurrence(ReminderItem item,DateTime now)
    {
        DateTime? active=_service.Book.Occurrences.Where(o=>o.RuleId==item.Id&&o.Phase is not ReminderPhase.Done and not ReminderPhase.Cancelled).Select(o=>(DateTime?)o.At).OrderBy(at=>at).FirstOrDefault();return active??(item.Enabled?ReminderSchedule.Next(item,_service.Book,now):NextStructuralOccurrence(item,now));
    }
    private DateTime? NextStructuralOccurrence(ReminderItem item,DateTime now)
    {
        if(!item.HasTime)return null;if(item.Enabled)return ReminderSchedule.Next(item,_service.Book,now);ReminderItem probe=new(){Id=item.Id,HasTime=item.HasTime,Enabled=true,Start=item.Start,Repeat=item.Repeat,Weekdays=item.Weekdays.ToList(),Dates=item.Dates.ToList(),ExcludedDates=item.ExcludedDates.ToList(),SkippedAt=item.SkippedAt,CheckedThrough=item.CheckedThrough};return ReminderSchedule.Next(probe,_service.Book,now);
    }

    private static string FormatNextOccurrence(DateTime at,DateTime now)
    {
        DateTime day=at.Date,today=now.Date;string time=at.ToString("HH:mm");if(day==today)return $"今天 {time}";if(day==today.AddDays(1))return $"明天 {time}";if(day==today.AddDays(2))return $"后天 {time}";int mondayOffset=((int)today.DayOfWeek+6)%7;DateTime monday=today.AddDays(-mondayOffset);string weekday="日一二三四五六"[(int)day.DayOfWeek].ToString();if(day<=monday.AddDays(6))return $"周{weekday} {time}";if(day<=monday.AddDays(13))return $"下周{weekday} {time}";return day.Year==today.Year?$"{day:M月d日} {time}":$"{day:yyyy/M/d} {time}";
    }

    private static string FormatCountdownEnd(DateTime end)=>end.Date==DateTime.Today?end.ToString("HH:mm"):end.Date==DateTime.Today.AddDays(1)?"明天 "+end.ToString("HH:mm"):end.ToString("M月d日 HH:mm");
    private static string FormatDuration(int seconds)
    {
        if(seconds%3600==0)return $"{seconds/3600}小时";if(seconds%60==0)return $"{seconds/60}分钟";TimeSpan value=TimeSpan.FromSeconds(seconds);return $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
    }
}
