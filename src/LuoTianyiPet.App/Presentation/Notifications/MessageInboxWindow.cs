using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using LuoTianyiPet.Core;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace LuoTianyiPet.App;

public sealed class MessageInboxWindow : Window
{
    private readonly MessageInbox _inbox;
    private readonly Canvas _canvas = new();
    private readonly StackPanel _icons = new();
    private readonly StackPanel _rows = new();
    private readonly ScrollViewer _scroll;
    private readonly Border _panel;
    private readonly Polygon _fold = new() { IsHitTestVisible = false };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly Dictionary<MessageProvider, FrameworkElement> _iconElements = [];
    private readonly Dictionary<string, Border> _rowElements = [];
    private MessageProvider? _renderedProvider;
    private double _preferredPanelHeight=68;
    private bool _contextOpen, _leftDown;
    private Rect _iconBounds, _panelBounds, _bridge;
    private MessageRailLayout? _layout;
    public MessageInboxInteraction Interaction { get; } = new();
    public event Action? PositionRequested;
    public event Action? Changed;
    public event Func<MessageProvider,bool>? ProviderActivationRequested;
    public bool PointerInside { get; private set; }
    public bool AutomationMode { get; set; }
    public Rect PanelRectangle => _panelBounds;
    public double PreferredPanelHeight => _preferredPanelHeight;
    public Point IconCenter(MessageProvider provider) => new(_iconBounds.Left+17,
        _iconBounds.Top+(provider==MessageProvider.Qq && _inbox.Total(MessageProvider.WeChat)>0 ? 40 : 0)+20);
    public IReadOnlyList<string> VisibleOrder => _rows.Children.OfType<Border>().Select(row => (string)row.Tag).ToArray();

    public MessageInboxWindow(MessageInbox inbox)
    {
        _inbox = inbox;
        Title = "QQ／微信待处理提醒"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false;
        Content = _canvas;
        UseLayoutRounding=true; SnapsToDevicePixels=true;
        _scroll = new ScrollViewer { Content = _rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, PanningMode = PanningMode.VerticalOnly };
        _panel = new() { Child = _scroll, CornerRadius = new(12), BorderThickness = new(1), Padding = new(10,8,10,8),
            Effect = new DropShadowEffect { BlurRadius = 9, ShadowDepth = 2, Opacity = .14 } };
        _canvas.Children.Add(_panel); _canvas.Children.Add(_fold); _canvas.Children.Add(_icons);
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x08000000 | 0x80);
            HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        };
        _timer.Tick += (_, _) => Tick();
        Closed += (_, _) => _timer.Stop();
        Refresh();
    }

    public void Refresh()
    {
        var providers = new[] { MessageProvider.WeChat, MessageProvider.Qq }.Where(p => _inbox.Total(p) > 0).ToArray();
        if (providers.Length == 0) { Interaction.Close(); Hide(); _timer.Stop(); }
        if (Interaction.OpenProvider is MessageProvider open && _inbox.Total(open) == 0) Interaction.Close();
        foreach (var stale in _iconElements.Keys.Except(providers).ToArray())
        { _icons.Children.Remove(_iconElements[stale]); _iconElements.Remove(stale); }
        for (int i = 0; i < providers.Length; i++)
        {
            var provider = providers[i];
            if (!_iconElements.TryGetValue(provider, out var element))
            {
                element = MakeIcon(provider); _iconElements[provider] = element;
                _icons.Children.Insert(i, element);
            }
            var badge = (Border)((Grid)element).Children[1];
            long? unread=_inbox.KnownUnreadCount(provider);
            badge.Width=unread is null?9:double.NaN;badge.Height=unread is null?9:16;
            badge.MinWidth=unread is null?9:16;badge.CornerRadius=new(unread is null?5:8);
            ((TextBlock)badge.Child).Text=unread is long count?MessageInbox.Badge(count):string.Empty;
            System.Windows.Automation.AutomationProperties.SetName(element,
                unread is long reliable ? $"{MessageProviderMatcher.GetDisplayName(provider)}，{reliable}条未读消息" :
                $"{MessageProviderMatcher.GetDisplayName(provider)}，有新消息");
        }
        _panel.Visibility = _fold.Visibility = Interaction.OpenProvider is null ? Visibility.Collapsed : Visibility.Visible;
        if (Interaction.OpenProvider is MessageProvider selected)
        {
            bool newlyOpened=_renderedProvider!=selected;
            RenderRows(selected);_renderedProvider=selected;
            if(newlyOpened)_scroll.ScrollToTop();
        }
        else { _renderedProvider=null;_preferredPanelHeight=68;_rowElements.Clear();_rows.Children.Clear(); }
        PositionRequested?.Invoke();
    }

    private FrameworkElement MakeIcon(MessageProvider provider)
    {
        Grid element = new() { Width = 46, Height = 40, Background = Brushes.Transparent, Cursor = Cursors.Hand };
        var art = new Image { Source = ProviderDrawing(provider), Width = 30, Height = 30, Stretch=Stretch.Uniform,
            SnapsToDevicePixels=true };
        RenderOptions.SetBitmapScalingMode(art,BitmapScalingMode.HighQuality);
        element.Children.Add(new Border { Width=35, Height=35, CornerRadius=new(18), Background=Brushes.White,
            BorderBrush=new SolidColorBrush(Color.FromRgb(0xD2,0xE6,0xF4)), BorderThickness=new(1),
            HorizontalAlignment=HorizontalAlignment.Left, VerticalAlignment=VerticalAlignment.Center,
            Padding=new(2), Child=art });
        Border badge = new() { Background = new SolidColorBrush(Color.FromRgb(0xF4,0x55,0x62)), CornerRadius = new(7),
            Padding = new(3,0,3,0), MinWidth = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false,
            Child = new TextBlock { Foreground = Brushes.White, FontSize = 10, FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center } };
        element.Children.Add(badge);
        element.MouseLeftButtonUp += (_, e) => { ActivateOrPin(provider); e.Handled = true; };
        element.PreviewMouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            ClearApplicationReminders(provider);
        };
        return element;
    }

    private void ClearApplicationReminders(MessageProvider provider)
    {
        _inbox.Clear(provider, DateTimeOffset.Now);
        Refresh();
        Changed?.Invoke();
    }

    private ContextMenu Menu(string label, Action action)
    {
        ContextMenu menu = new(); MenuItem item = new() { Header = label }; menu.Items.Add(item);
        item.Click += (_, _) => action(); menu.Opened += (_, _) => _contextOpen = true;
        menu.Closed += (_, _) => _contextOpen = false; return menu;
    }

    private void RenderRows(MessageProvider provider)
    {
        // Keep one compact summary per conversation, newest first. The panel
        // grows to its maximum height, then scrolls without hiding older rows.
        var conversations=_inbox.Rows(provider);
        _rows.Children.Clear();_rowElements.Clear();
        Color accent=provider==MessageProvider.WeChat?Color.FromRgb(0x24,0xA0,0x7C):Color.FromRgb(0x2B,0x88,0xC5);
        _panel.Background=new SolidColorBrush(Color.FromRgb(0xFB,0xFD,0xFF));
        _panel.BorderBrush=new SolidColorBrush(Color.FromRgb(0xD3,0xE5,0xF1));
        _fold.Fill=new SolidColorBrush(Color.FromRgb(0xD3,0xE5,0xF1));
        if(conversations.Count==0)return;

        string source=MessageProviderMatcher.GetDisplayName(provider);
        bool single=conversations.Count==1;
        foreach(var conversation in conversations)
        {
            string title=conversation.Name;
            string body=conversation.Preview;
            int? perConversation=_inbox.KnownConversationUnreadCount(conversation);
            string quantity=perConversation is int unread?$"{unread} 条未读":$"{conversation.Count} 条提醒";
            StackPanel content=new(){Margin=new Thickness(2,5,2,5)};
            Grid heading=new();heading.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            heading.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            TextBlock sender=new(){Name="InboxSender",Text=title,FontSize=single?14:13,FontWeight=FontWeights.SemiBold,
                Foreground=new SolidColorBrush(Color.FromRgb(0x1D,0x3A,0x53)),TextTrimming=TextTrimming.CharacterEllipsis};
            heading.Children.Add(sender);
            if (!single)
            {
                TextBlock count=new(){Name="InboxRowCount",Text=quantity,FontSize=10.5,
                    Foreground=new SolidColorBrush(accent),Margin=new Thickness(6,1,0,0)};
                Grid.SetColumn(count,1);heading.Children.Add(count);
            }
            TextBlock preview=new(){Name="InboxPreview",Text=body,FontSize=12.5,LineHeight=18,
                Foreground=new SolidColorBrush(Color.FromRgb(0x47,0x63,0x79)),TextWrapping=TextWrapping.Wrap,
                TextTrimming=TextTrimming.CharacterEllipsis,MaxHeight=single?36:18,Margin=new Thickness(0,3,0,0)};
            string origin=provider==MessageProvider.WeChat?"来自微信":"来自 QQ";
            string footer=$"{origin} · {quantity}";
            TextBlock provenance=new(){Name="InboxSource",Text=footer,FontSize=10.5,
                Foreground=new SolidColorBrush(accent),Margin=new Thickness(0,5,0,0),
                Visibility=single && (perConversation is not null || title!=source+"提醒")?Visibility.Visible:Visibility.Collapsed};
            content.Children.Add(heading);content.Children.Add(preview);content.Children.Add(provenance);
            Border row=new(){Name="InboxPreviewCard",Child=content,Background=Brushes.Transparent,Tag=conversation.Key,
                BorderBrush=new SolidColorBrush(Color.FromRgb(0xE5,0xEE,0xF5)),
                BorderThickness=new Thickness(0,0,0,conversation==conversations[conversations.Count-1]?0:1),Cursor=Cursors.Hand};
            row.MouseLeftButtonUp+=(_,e)=>{ActivateOrPin(provider);e.Handled=true;};
            string key=conversation.Key;
            row.ContextMenu=Menu("忽略该会话提醒",()=>{_inbox.Ignore(key);Refresh();Changed?.Invoke();});
            _rows.Children.Add(row);_rowElements[key]=row;
        }
        _rows.Measure(new System.Windows.Size(MessageRailPlacement.PanelWidth-24,double.PositiveInfinity));
        _preferredPanelHeight=Numeric.Clamp(Math.Ceiling(_rows.DesiredSize.Height+18),68,MessageRailPlacement.PanelHeight);
    }

    public void SetLayout(MessageRailLayout layout, double scale)
    {
        _layout = layout;
        bool expanded = Interaction.OpenProvider is not null;
        var icons = layout.Icons; var panel = layout.Panel;
        double x = expanded ? Math.Min(icons.Left,panel.Left) - 5 * scale : icons.Left - 2 * scale;
        double y = expanded ? Math.Min(icons.Top,panel.Top) - 5 * scale : icons.Top - 2 * scale;
        double right = expanded ? Math.Max(icons.Right,panel.Right) + 5 * scale : icons.Right + 2 * scale;
        double bottom = expanded ? Math.Max(icons.Bottom,panel.Bottom) + 5 * scale : icons.Bottom + 2 * scale;
        Width = (right-x)/scale; Height = (bottom-y)/scale;
        _iconBounds = new((icons.Left-x)/scale,(icons.Top-y)/scale,icons.Width/scale,icons.Height/scale);
        _panelBounds = new((panel.Left-x)/scale,(panel.Top-y)/scale,panel.Width/scale,panel.Height/scale);
        Canvas.SetLeft(_icons,_iconBounds.Left); Canvas.SetTop(_icons,_iconBounds.Top);
        Canvas.SetLeft(_panel,_panelBounds.Left); Canvas.SetTop(_panel,_panelBounds.Top);
        _panel.Width = _panelBounds.Width; _panel.Height = _panelBounds.Height;
        double bridgeLeft = layout.Side == MessageRailSide.Left ? _panelBounds.Right : _iconBounds.Right;
        double bridgeRight = layout.Side == MessageRailSide.Left ? _iconBounds.Left : _panelBounds.Left;
        _bridge = new Rect(bridgeLeft,_iconBounds.Top+ActiveIconIndex()*40,Math.Max(0,bridgeRight-bridgeLeft),40);
        double foldX = layout.Side == MessageRailSide.Left ? _panelBounds.Right - 1 : _panelBounds.Left - 4;
        double foldY = Numeric.Clamp(_iconBounds.Top + ActiveIconIndex() * 40 + 15, _panelBounds.Top+5, _panelBounds.Bottom-10);
        _fold.Points = layout.Side == MessageRailSide.Left
            ? new(new[] {new Point(foldX,foldY),new Point(foldX+5,foldY+3),new Point(foldX,foldY+6)})
            : new(new[] {new Point(foldX+5,foldY),new Point(foldX,foldY+3),new Point(foldX+5,foldY+6)});
        if (!IsVisible) { Opacity = 0; Show(); }
        SetWindowPos(new WindowInteropHelper(this).Handle,0,(int)Math.Round(x),(int)Math.Round(y),
            (int)Math.Ceiling(right-x),(int)Math.Ceiling(bottom-y),0x14);
        Opacity = 1; _timer.Start();
    }

    public int ActiveIconIndex() => Interaction.OpenProvider == MessageProvider.Qq && _inbox.Total(MessageProvider.WeChat) > 0 ? 1 : 0;
    private void ActivateOrPin(MessageProvider provider)
    {
        if(ProviderActivationRequested?.Invoke(provider)==true)Interaction.Close();
        else Interaction.Click(provider); // Keep preview usable when the client has no activatable window.
        Refresh();
    }
    public void Suspend() { Interaction.Close(); Hide(); _timer.Stop(); PointerInside = false; }
    public void ExposeAutomationWindow()
    {
        ShowInTaskbar = true;
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowLong(handle,-20,(GetWindowLong(handle,-20)&~0x80)|0x40000);
    }
    internal void QaClickIcon(MessageProvider provider) => _iconElements[provider].RaiseEvent(
        new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left) {RoutedEvent=MouseLeftButtonUpEvent});
    internal void QaClickPreviewCard() => ((Border)_rows.Children[0]).RaiseEvent(
        new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left) {RoutedEvent=MouseLeftButtonUpEvent});
    internal (string Title,string Body,string Footer) QaPreviewContent()
    {
        var content=(StackPanel)((Border)_rows.Children[0]).Child;
        return (((TextBlock)((Grid)content.Children[0]).Children[0]).Text,((TextBlock)content.Children[1]).Text,
            ((TextBlock)content.Children[2]).Visibility==Visibility.Visible?((TextBlock)content.Children[2]).Text:string.Empty);
    }
    internal double QaPreviewBodyHeight => ((TextBlock)((StackPanel)((Border)_rows.Children[0]).Child).Children[1]).ActualHeight;
    internal double QaScrollExtent => _scroll.ExtentHeight;
    internal double QaScrollViewport => _scroll.ViewportHeight;
    internal double QaScrollOffset => _scroll.VerticalOffset;
    internal void QaScrollToEnd() => _scroll.ScrollToEnd();
    internal IReadOnlyList<(string Title,string Body,string? Count)> QaConversationSummaries() => _rows.Children
        .OfType<Border>().Select(row =>
        {
            var content=(StackPanel)row.Child;
            var heading=(Grid)content.Children[0];
            return (((TextBlock)heading.Children[0]).Text,((TextBlock)content.Children[1]).Text,
                heading.Children.Count>1?((TextBlock)heading.Children[1]).Text:null);
        }).ToArray();
    internal string QaBadge(MessageProvider provider) => ((TextBlock)((Border)((Grid)_iconElements[provider]).Children[1]).Child).Text;
    internal void QaIgnoreConversation(string key) => ((MenuItem)_rowElements[key].ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    internal bool QaIconHasContextMenu(MessageProvider provider) => _iconElements[provider].ContextMenu is not null;
    internal void QaRightClickIcon(MessageProvider provider) => _iconElements[provider].RaiseEvent(
        new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Right)
        {RoutedEvent=PreviewMouseRightButtonUpEvent});
    internal bool QaNativeHitTest(Point point)
    {
        Point screen=PointToScreen(point); long packed=((long)(ushort)(short)screen.Y<<16)|(ushort)(short)screen.X;
        return SendMessage(new WindowInteropHelper(this).Handle,0x84,0,(nint)packed)==1;
    }
    public bool IsInteractive(Point point) => _iconBounds.Contains(point) ||
        (Interaction.OpenProvider is not null && (_panelBounds.Contains(point) || _bridge.Contains(point)));

    private void Tick()
    {
        if (AutomationMode || !IsVisible || _layout is null || !GetCursorPos(out var cursor)) return;
        Point point = PointFromScreen(new(cursor.X,cursor.Y));
        ProcessPointer(point,(GetAsyncKeyState(1) & 0x8000) != 0,DateTimeOffset.Now);
    }
    public void ProcessPointer(Point point, bool down, DateTimeOffset now)
    {
        PointerInside = IsInteractive(point);
        var before = Interaction.OpenProvider;
        MessageProvider? icon = null;
        if (_iconBounds.Contains(point))
        {
            int index = (int)((point.Y-_iconBounds.Top)/40);
            var providers = new[] {MessageProvider.WeChat,MessageProvider.Qq}.Where(p=>_inbox.Total(p)>0).ToArray();
            if (index >= 0 && index < providers.Length) icon = providers[index];
        }
        if (down && !_leftDown && !PointerInside && !_contextOpen) Interaction.OutsideClick();
        _leftDown = down;
        if (!_contextOpen) Interaction.Move(icon, Interaction.OpenProvider is not null && (_panelBounds.Contains(point) || _bridge.Contains(point)), now);
        if (before != Interaction.OpenProvider) Refresh();
        PositionRequested?.Invoke();
    }
    private nint WndProc(nint hwnd,int msg,nint wp,nint lp,ref bool handled)
    {
        if (msg == 0x21) { handled = true; return 3; } // MA_NOACTIVATE
        if (msg == 0x84)
        {
            Point point = PointFromScreen(new((short)((long)lp & 0xffff),(short)(((long)lp >> 16)&0xffff)));
            if (!IsInteractive(point)) { handled = true; return -1; }
        }
        return 0;
    }

    private static readonly ImageSource QqArtwork = LoadProviderArtwork("notification-qq.png");
    private static readonly ImageSource WeChatArtwork = LoadProviderArtwork("notification-wechat.png");
    private static ImageSource ProviderDrawing(MessageProvider provider) =>
        provider == MessageProvider.WeChat ? WeChatArtwork : QqArtwork;

    private static ImageSource LoadProviderArtwork(string filename)
    {
        var image = new System.Windows.Media.Imaging.BitmapImage();
        image.BeginInit();
        image.UriSource = RuntimeAssetLocator.PackUri("ui/" + filename);
        image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 128;
        image.EndInit();
        image.Freeze();
        return image;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X,Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd,int message,nint wp,nint lp);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")] private static extern int GetWindowLong(nint hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW")] private static extern int SetWindowLong(nint hwnd,int index,int value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int width,int height,uint flags);
}
