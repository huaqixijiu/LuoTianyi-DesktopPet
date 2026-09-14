using System.Windows;
using Brush = System.Windows.Media.Brush;
using System.Windows.Markup;
using System.Windows.Media;
namespace LuoTianyiPet.App;
internal static class PlannerTheme
{
    public static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0, 157, 211));
    public static readonly Brush Line = new SolidColorBrush(Color.FromRgb(222, 237, 248));
    public static readonly Brush Soft = new SolidColorBrush(Color.FromRgb(234, 248, 255));
    public static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(27, 43, 88));
    public static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(118, 139, 167));
    public static readonly Brush PrimaryFill = new LinearGradientBrush(Color.FromRgb(29,183,228),Color.FromRgb(0,145,204),90);
    // Planner-only semantic tokens. Floating pet surfaces keep their own configured palette.
    public static readonly Brush Danger = new SolidColorBrush(Color.FromRgb(217, 83, 79));
    public static readonly Brush DangerSoft = new SolidColorBrush(Color.FromRgb(253, 236, 236));
    public static readonly Brush DangerLine = new SolidColorBrush(Color.FromRgb(245, 198, 198));
    public static readonly Brush RestForeground = Danger;
    public static readonly Brush RestBackground = new SolidColorBrush(Color.FromRgb(251, 227, 227));
    public static readonly Brush WorkForeground = new SolidColorBrush(Color.FromRgb(58, 123, 213));
    public static readonly Brush WorkBackground = new SolidColorBrush(Color.FromRgb(227, 238, 251));
    public static readonly Brush TermForeground = new SolidColorBrush(Color.FromRgb(74, 144, 217));
    public static readonly Brush AccentSoft = new SolidColorBrush(Color.FromRgb(221, 238, 251));
    public static readonly Brush HoverBackground = new SolidColorBrush(Color.FromRgb(229, 241, 250));
    public static readonly Brush ChipBackground = new SolidColorBrush(Color.FromRgb(242, 248, 252));
    // Stable per-item accent for list color bars; derived from the id so nothing is persisted.
    private static readonly Brush[] ItemAccents =
    [
        new SolidColorBrush(Color.FromRgb(41, 182, 246)),
        new SolidColorBrush(Color.FromRgb(38, 198, 166)),
        new SolidColorBrush(Color.FromRgb(149, 117, 205)),
        new SolidColorBrush(Color.FromRgb(38, 166, 154)),
        new SolidColorBrush(Color.FromRgb(242, 166, 90)),
    ];
    public static Brush ItemAccent(Guid id) => ItemAccents[id.ToByteArray()[0] % ItemAccents.Length];
    public static FrameworkElement Icon(string kind, double size=20, Brush? color=null, double gap=10) => new System.Windows.Shapes.Path{Width=size,Height=size,Margin=new Thickness(0,0,gap,0),Stretch=Stretch.Uniform,Stroke=color??Ink,StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Data=Geometry.Parse(IconGeometry(kind))};
    private static string IconGeometry(string kind) => kind switch
    {
        "calendar" => "M3,5 L19,5 Q21,5 21,7 L21,20 Q21,22 19,22 L3,22 Q1,22 1,20 L1,7 Q1,5 3,5 Z M1,10 L21,10 M6,2 L6,7 M16,2 L16,7 M6,14 L8,14 M13,14 L15,14 M6,18 L8,18",
        "clock" => "M12,1 A11,11 0 1 1 11.99,1 M12,5 L12,12 L17,15",
        "note" => "M4,1 L16,1 L21,6 L21,23 L3,23 L3,1 Z M15,1 L15,7 L21,7 M7,12 L17,12 M7,17 L17,17",
        "gear" => "M12,9 A3,3 0 1 1 11.99,9 M12,2 L12,5 M12,19 L12,22 M2,12 L5,12 M19,12 L22,12 M4.8,4.8 L7,7 M17,17 L19.2,19.2 M19.2,4.8 L17,7 M7,17 L4.8,19.2",
        "minimize" => "M5,12 L19,12",
        "maximize" => "M6,6 L18,6 L18,18 L6,18 Z",
        "close" => "M6,6 L18,18 M18,6 L6,18",
        "chevron-down" => "M7,10 L12,15 L17,10",
        "chevron-up" => "M7,14 L12,9 L17,14",
        "chevron-right" => "M10,7 L15,12 L10,17",
        "repeat" => "M4,9 Q4,5 8,5 L18,5 M15,2 L18,5 L15,8 M20,15 Q20,19 16,19 L6,19 M9,22 L6,19 L9,16",
        "ellipsis" => "M6,12 L6.01,12 M12,12 L12.01,12 M18,12 L18.01,12",
        "pause" => "M9,6 L9,18 M15,6 L15,18",
        "play" => "M9,6 L17,12 L9,18 Z",
        "trash" => "M5,7 L19,7 M9,7 L9,4 L15,4 L15,7 M8,7 L8,20 L16,20 L16,7 M10,10 L10,17 M14,10 L14,17",
        "info" => "M12,3 A9,9 0 1 1 11.99,3 M12,10 L12,17 M12,7 L12,7.01",
        "check" => "M5,13 L10,18 L19,7",
        "plus" => "M12,5 L12,19 M5,12 L19,12",
        "hourglass" => "M7,3 L17,3 L17,6 Q17,10 12,12 Q7,10 7,6 Z M7,21 L17,21 L17,18 Q17,14 12,12 Q7,14 7,18 Z",
        _ => "M6,10 L7,8 L7,6 C7,3 17,3 17,6 L17,8 L18,10 Z M10,13 Q12,15 14,13 M12,2 L12,3",
    };
    public static FrameworkElement Bell() => new System.Windows.Shapes.Path { Width=14,Height=14,Margin=new Thickness(0,0,4,0),Stretch=Stretch.Uniform,Stroke=Accent,StrokeThickness=1.5,Data=Geometry.Parse("M 3,11 L 4,9 L 4,6 C 4,2 10,2 10,6 L 10,9 L 11,11 Z M 6,13 Q 7,15 8,13 M 7,1 L 7,2") };
    public static void Apply(Window window)
    {
        window.FontSize=15;
        System.Windows.Media.TextOptions.SetTextFormattingMode(window,TextFormattingMode.Display);
        System.Windows.Media.RenderOptions.SetBitmapScalingMode(window,BitmapScalingMode.HighQuality);
        window.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse("""
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
 <Style TargetType="DatePicker">
  <Setter Property="Height" Value="42"/><Setter Property="FontSize" Value="15"/><Setter Property="Foreground" Value="#1B2B58"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="DatePicker">
   <Grid x:Name="PART_Root"><Border Background="White" BorderBrush="#CDDEE6" BorderThickness="1" CornerRadius="7"/>
    <DatePickerTextBox x:Name="PART_TextBox" Margin="8,2,38,2" VerticalContentAlignment="Center" BorderThickness="0" Background="Transparent" Padding="3" FontSize="15"/>
    <Button x:Name="PART_Button" Width="34" HorizontalAlignment="Right" Background="Transparent" BorderThickness="0" Padding="4" Focusable="False"><TextBlock Text="&#xE787;" FontFamily="Segoe MDL2 Assets" FontSize="18" Foreground="#009DD3"/></Button>
    <Popup x:Name="PART_Popup" Placement="Bottom" PlacementTarget="{Binding ElementName=PART_Root}" StaysOpen="False" AllowsTransparency="True"/>
   </Grid></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="Button"><Setter Property="FontSize" Value="15"/><Setter Property="VerticalContentAlignment" Value="Center"/><Setter Property="HorizontalContentAlignment" Value="Center"/>
  <Setter Property="Background" Value="White"/><Setter Property="Foreground" Value="#1B2B58"/><Setter Property="BorderBrush" Value="#DEEDF8"/><Setter Property="BorderThickness" Value="1"/><Setter Property="Padding" Value="10,7"/><Setter Property="Cursor" Value="Hand"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="B" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="8" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="B" Property="Opacity" Value="0.78"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="B" Property="Opacity" Value="0.4"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="B" Property="BorderBrush" Value="#009DD3"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="TextBox">
  <Setter Property="Padding" Value="12,10"/><Setter Property="FontSize" Value="15"/><Setter Property="BorderBrush" Value="#CDDEE6"/><Setter Property="Background" Value="White"/><Setter Property="BorderThickness" Value="1"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox"><Border x:Name="Frame" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="7"><ScrollViewer x:Name="PART_ContentHost" /></Border><ControlTemplate.Triggers><Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="Frame" Property="BorderBrush" Value="#009DD3"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="ComboBox">
  <Setter Property="FontSize" Value="15"/><Setter Property="MinHeight" Value="42"/><Setter Property="Background" Value="White"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox"><Grid><ToggleButton Focusable="False" IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"><ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border CornerRadius="7" Background="White" BorderBrush="#CDDEE6" BorderThickness="1"><TextBlock Text="⌄" HorizontalAlignment="Right" Margin="0,0,14,0" VerticalAlignment="Center" Foreground="#345569"/></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter Margin="12,8,34,8" VerticalAlignment="Center" IsHitTestVisible="False" Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"/><Popup x:Name="PART_Popup" Placement="Bottom" IsOpen="{TemplateBinding IsDropDownOpen}" AllowsTransparency="True" Focusable="False" PopupAnimation="Fade"><Border Background="White" BorderBrush="#CDDEE6" BorderThickness="1" CornerRadius="7" MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}" Padding="5"><ScrollViewer MaxHeight="270"><ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/></ScrollViewer></Border></Popup></Grid></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="ComboBoxItem"><Setter Property="Padding" Value="10,8"/><Setter Property="HorizontalContentAlignment" Value="Stretch"/></Style>
 <Style TargetType="CheckBox"><Setter Property="VerticalContentAlignment" Value="Center"/><Setter Property="Padding" Value="4"/><Setter Property="Foreground" Value="#345569"/>
 <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><StackPanel Orientation="Horizontal"><Border x:Name="Box" Width="16" Height="16" CornerRadius="4" BorderBrush="#ADCAD3" BorderThickness="1" Background="White" VerticalAlignment="Center"><TextBlock x:Name="Check" Text="✓" Foreground="White" FontSize="12" HorizontalAlignment="Center" VerticalAlignment="Center" Visibility="Collapsed"/></Border><ContentPresenter Margin="7,0,0,0" VerticalAlignment="Center"/></StackPanel><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Box" Property="Background" Value="#009DD3"/><Setter TargetName="Check" Property="Visibility" Value="Visible"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Box" Property="BorderBrush" Value="#1B2B58"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style x:Key="PlannerSwitch" TargetType="CheckBox"><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><StackPanel Orientation="Horizontal"><Border x:Name="Track" Width="44" Height="24" CornerRadius="12" Background="#C3D4DC"><Ellipse x:Name="Knob" Width="18" Height="18" Fill="White" HorizontalAlignment="Left" Margin="3"/></Border><ContentPresenter Margin="8,0,0,0" VerticalAlignment="Center"/></StackPanel><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Track" Property="Background" Value="#009DD3"/><Setter TargetName="Knob" Property="HorizontalAlignment" Value="Right"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="Track" Property="Opacity" Value="0.5"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Track" Property="BorderBrush" Value="#1B2B58"/><Setter TargetName="Track" Property="BorderThickness" Value="1"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style x:Key="PlannerChip" TargetType="Button"><Setter Property="Height" Value="32"/><Setter Property="Padding" Value="14,4"/><Setter Property="FontSize" Value="13"/><Setter Property="Cursor" Value="Hand"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="B" Background="White" BorderBrush="#DEEDF8" BorderThickness="1" CornerRadius="8" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="B" Property="Background" Value="#F2F8FC"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="B" Property="Opacity" Value="0.45"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style x:Key="PlannerSegment" TargetType="Button"><Setter Property="Height" Value="44"/><Setter Property="FontSize" Value="15"/><Setter Property="Cursor" Value="Hand"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="B" Background="#EEF5FB" BorderBrush="#E1EDF6" BorderThickness="1" CornerRadius="10" Padding="10,4"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="B" Property="Background" Value="#E3F0FA"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style x:Key="PlannerIconBtn" TargetType="Button"><Setter Property="MinWidth" Value="32"/><Setter Property="MinHeight" Value="32"/><Setter Property="Padding" Value="6"/><Setter Property="Cursor" Value="Hand"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="B" Background="Transparent" CornerRadius="8" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="B" Property="Background" Value="#E5F1FA"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="B" Property="Opacity" Value="0.45"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style x:Key="PlannerCloseBtn" TargetType="Button"><Setter Property="MinWidth" Value="40"/><Setter Property="MinHeight" Value="32"/><Setter Property="Padding" Value="6"/><Setter Property="Cursor" Value="Hand"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="B" Background="Transparent" CornerRadius="8" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="B" Property="Background" Value="#FDECEC"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style x:Key="PlannerDanger" TargetType="Button"><Setter Property="MinHeight" Value="36"/><Setter Property="Padding" Value="14,6"/><Setter Property="FontSize" Value="14"/><Setter Property="Foreground" Value="#D9534F"/><Setter Property="Cursor" Value="Hand"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="B" Background="#FDECEC" BorderBrush="#F5C6C6" BorderThickness="1" CornerRadius="8" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="B" Property="Background" Value="#FADDDD"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="B" Property="Opacity" Value="0.45"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style x:Key="PlannerLink" TargetType="Button"><Setter Property="Padding" Value="4,2"/><Setter Property="FontSize" Value="13"/><Setter Property="Foreground" Value="#009DD3"/><Setter Property="Cursor" Value="Hand"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="B" Background="Transparent" CornerRadius="6" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Left" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="B" Property="Background" Value="#E5F1FA"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>
"""));
    }
}
