using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SnipCanvas;

/// <summary>Colour tokens and control styles shared by every window, popup and menu.</summary>
internal static class Theme
{
    internal static readonly ResourceDictionary Palette = new();
    internal static bool IsDark { get; private set; } = true;
    private static bool installed;

    private static readonly string[] Names = {
        "Page", "Surface", "Canvas", "Field", "Hover", "Pressed", "Line", "LineStrong", "Ink", "Muted", "Subtle",
        "Accent", "AccentHover", "AccentPressed", "AccentText", "Selected", "Success", "Warning", "WarningSoft", "Danger" };
    private static readonly string[] Dark = {
        "#0F1015", "#171922", "#0B0C10", "#0E0F14", "#20222D", "#282B38", "#262935", "#363A4A", "#F1F2F8", "#A0A5BA", "#7D839A",
        "#6C5CF6", "#7F70FF", "#5A4BE0", "#B8AEFF", "#2A2550", "#3DD68C", "#F5B74A", "#2B2213", "#FF6B6B" };
    private static readonly string[] Light = {
        "#F3F4F8", "#FFFFFF", "#E8EAF1", "#F6F7FB", "#EEF0F6", "#E4E7F0", "#E0E3EC", "#CDD1DE", "#171A2B", "#5B6178", "#6E748D",
        "#5B4BEB", "#6B5CF5", "#4A3BD6", "#5844D9", "#ECE9FF", "#12915A", "#B26A00", "#FFF4DF", "#D63B3B" };

    internal static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI");

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    internal static void Install()
    {
        if (installed || Application.Current == null) return;
        installed = true;
        Apply(IsDark);
        Application.Current.Resources.MergedDictionaries.Add(Palette);
        Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(StyleXaml));
    }

    internal static void Apply(bool dark)
    {
        IsDark = dark;
        var values = dark ? Dark : Light;
        for (int i = 0; i < Names.Length; i++)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[i]));
            brush.Freeze();
            Palette[Names[i]] = brush;
        }
    }

    internal static Color Color(string name) => ((SolidColorBrush)Palette[name]).Color;
    internal static Brush Brush(string name) => (Brush)Palette[name];

    /// <summary>Follows the app theme in the title bar (Windows 11 also recolours the caption to match the page).</summary>
    internal static void StyleTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        int dark = IsDark ? 1 : 0, caption = Bgr("Page"), text = Bgr("Ink"), border = Bgr("Line");
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
        DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
        DwmSetWindowAttribute(handle, 34, ref border, sizeof(int));
    }

    private static int Bgr(string name) { var c = Color(name); return c.R | c.G << 8 | c.B << 16; }

    /// <summary>Asks Windows 11 to round the corners of a WinForms popup; older versions ignore it.</summary>
    internal static void RoundCorners(IntPtr handle)
    {
        int rounded = 2;
        DwmSetWindowAttribute(handle, 33, ref rounded, sizeof(int));
    }

    /// <summary>True when Windows apps are set to the dark theme.</summary>
    internal static bool SystemPrefersDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException || ex is System.IO.IOException) { return true; }
    }

    private const string StyleXaml = """
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- ===== Buttons ===== -->
  <Style x:Key="BaseButton" TargetType="Button">
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Foreground" Value="{DynamicResource Ink}"/>
    <Setter Property="Background" Value="{DynamicResource Surface}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource Line}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="14,0"/>
    <Setter Property="MinHeight" Value="36"/>
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
    <Setter Property="HorizontalContentAlignment" Value="Center"/>
    <Setter Property="VerticalContentAlignment" Value="Center"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="Chrome" CornerRadius="9" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
            <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Chrome" Property="Background" Value="{DynamicResource Hover}"/></Trigger>
            <Trigger Property="IsPressed" Value="True"><Setter TargetName="Chrome" Property="Background" Value="{DynamicResource Pressed}"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource Accent}"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType="Button" BasedOn="{StaticResource BaseButton}"/>

  <Style x:Key="PrimaryButton" TargetType="Button" BasedOn="{StaticResource BaseButton}">
    <Setter Property="Foreground" Value="White"/>
    <Setter Property="Background" Value="{DynamicResource Accent}"/>
    <Setter Property="BorderBrush" Value="Transparent"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="Chrome" CornerRadius="9" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
            <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Chrome" Property="Background" Value="{DynamicResource AccentHover}"/></Trigger>
            <Trigger Property="IsPressed" Value="True"><Setter TargetName="Chrome" Property="Background" Value="{DynamicResource AccentPressed}"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource AccentText}"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="SplitLeftButton" TargetType="Button" BasedOn="{StaticResource PrimaryButton}">
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="Chrome" CornerRadius="9,0,0,9" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
            <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Chrome" Property="Background" Value="{DynamicResource AccentHover}"/></Trigger>
            <Trigger Property="IsPressed" Value="True"><Setter TargetName="Chrome" Property="Background" Value="{DynamicResource AccentPressed}"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource AccentText}"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style x:Key="SplitRightButton" TargetType="Button" BasedOn="{StaticResource PrimaryButton}">
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="Chrome" CornerRadius="0,9,9,0" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
            <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Chrome" Property="Background" Value="{DynamicResource AccentHover}"/></Trigger>
            <Trigger Property="IsPressed" Value="True"><Setter TargetName="Chrome" Property="Background" Value="{DynamicResource AccentPressed}"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource AccentText}"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="GhostButton" TargetType="Button" BasedOn="{StaticResource BaseButton}">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderBrush" Value="Transparent"/>
    <Setter Property="MinHeight" Value="32"/>
    <Setter Property="Padding" Value="10,0"/>
  </Style>

  <Style x:Key="IconButton" TargetType="Button" BasedOn="{StaticResource GhostButton}">
    <Setter Property="Width" Value="36"/>
    <Setter Property="Height" Value="36"/>
    <Setter Property="Padding" Value="0"/>
  </Style>

  <Style x:Key="ThumbButton" TargetType="Button" BasedOn="{StaticResource BaseButton}">
    <Setter Property="Padding" Value="0"/>
    <Setter Property="Background" Value="{DynamicResource Canvas}"/>
    <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
    <Setter Property="VerticalContentAlignment" Value="Stretch"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="Chrome" CornerRadius="10" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
            <ContentPresenter/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource Accent}"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource Accent}"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="LinkButton" TargetType="Button" BasedOn="{StaticResource BaseButton}">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderBrush" Value="Transparent"/>
    <Setter Property="Foreground" Value="{DynamicResource AccentText}"/>
    <Setter Property="MinHeight" Value="28"/>
    <Setter Property="Padding" Value="8,0"/>
  </Style>

  <Style x:Key="CardButton" TargetType="Button" BasedOn="{StaticResource BaseButton}">
    <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
    <Setter Property="VerticalContentAlignment" Value="Stretch"/>
    <Setter Property="Padding" Value="18"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Border x:Name="Chrome" CornerRadius="16" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
            <ContentPresenter Margin="{TemplateBinding Padding}"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource Hover}"/>
              <Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource LineStrong}"/>
            </Trigger>
            <Trigger Property="IsPressed" Value="True"><Setter TargetName="Chrome" Property="Background" Value="{DynamicResource Pressed}"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource Accent}"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ===== Toggle-like controls ===== -->
  <Style x:Key="ToolButton" TargetType="RadioButton">
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Foreground" Value="{DynamicResource Ink}"/>
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderBrush" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Height" Value="36"/>
    <Setter Property="Padding" Value="11,0"/>
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="RadioButton">
          <Border x:Name="Chrome" CornerRadius="9" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
            <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="Center" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Chrome" Property="Background" Value="{DynamicResource Hover}"/></Trigger>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource Selected}"/>
              <Setter Property="Foreground" Value="{DynamicResource AccentText}"/>
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource Accent}"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="SegmentItem" TargetType="RadioButton" BasedOn="{StaticResource ToolButton}">
    <Setter Property="Foreground" Value="{DynamicResource Muted}"/>
    <Setter Property="Height" Value="30"/>
    <Setter Property="Padding" Value="14,0"/>
    <Setter Property="MinWidth" Value="72"/>
  </Style>

  <Style x:Key="SwatchButton" TargetType="RadioButton">
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Margin" Value="0,0,2,0"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="RadioButton">
          <Grid Width="28" Height="28" Background="Transparent" SnapsToDevicePixels="False">
            <Ellipse x:Name="Ring" Width="26" Height="26" StrokeThickness="2" Stroke="Transparent"/>
            <Ellipse Width="18" Height="18" Fill="{TemplateBinding Background}" Stroke="{DynamicResource LineStrong}" StrokeThickness="1"/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Ring" Property="Stroke" Value="{DynamicResource LineStrong}"/></Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Ring" Property="Stroke" Value="{DynamicResource Muted}"/></Trigger>
            <Trigger Property="IsChecked" Value="True"><Setter TargetName="Ring" Property="Stroke" Value="{DynamicResource AccentText}"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="Switch" TargetType="CheckBox">
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="CheckBox">
          <Grid Width="44" Height="26" Background="Transparent" SnapsToDevicePixels="True">
            <Border x:Name="Track" CornerRadius="13" Background="{DynamicResource LineStrong}" BorderThickness="2" BorderBrush="Transparent"/>
            <Ellipse x:Name="Thumb" Width="20" Height="20" Fill="White" Stroke="#22000000" StrokeThickness="0.5" HorizontalAlignment="Left" Margin="3,0,0,0">
              <Ellipse.RenderTransform><TranslateTransform x:Name="Shift" X="0"/></Ellipse.RenderTransform>
            </Ellipse>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="Track" Property="Background" Value="{DynamicResource Accent}"/>
              <Trigger.EnterActions>
                <BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="Shift" Storyboard.TargetProperty="X" To="18" Duration="0:0:0.12"/></Storyboard></BeginStoryboard>
              </Trigger.EnterActions>
              <Trigger.ExitActions>
                <BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="Shift" Storyboard.TargetProperty="X" To="0" Duration="0:0:0.12"/></Storyboard></BeginStoryboard>
              </Trigger.ExitActions>
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Track" Property="BorderBrush" Value="{DynamicResource AccentText}"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ===== Text input ===== -->
  <Style TargetType="TextBox">
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Foreground" Value="{DynamicResource Ink}"/>
    <Setter Property="Background" Value="{DynamicResource Field}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource Line}"/>
    <Setter Property="CaretBrush" Value="{DynamicResource Ink}"/>
    <Setter Property="SelectionBrush" Value="{DynamicResource Accent}"/>
    <Setter Property="SelectionOpacity" Value="0.45"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="10,8"/>
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="TextBox">
          <Border x:Name="Chrome" CornerRadius="9" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" SnapsToDevicePixels="True">
            <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}" Focusable="False"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource Accent}"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.5"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ===== Scroll bars ===== -->
  <Style TargetType="ScrollBar">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ScrollBar">
          <Grid x:Name="Root" Width="10" Background="Transparent">
            <Track x:Name="PART_Track" Orientation="Vertical" IsDirectionReversed="True">
              <Track.DecreaseRepeatButton>
                <RepeatButton Command="ScrollBar.PageUpCommand" Focusable="False" IsTabStop="False">
                  <RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate></RepeatButton.Template>
                </RepeatButton>
              </Track.DecreaseRepeatButton>
              <Track.Thumb>
                <Thumb>
                  <Thumb.Template>
                    <ControlTemplate TargetType="Thumb">
                      <Border x:Name="Bar" Margin="2" CornerRadius="3" Background="{DynamicResource LineStrong}"/>
                      <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Bar" Property="Background" Value="{DynamicResource Subtle}"/></Trigger>
                      </ControlTemplate.Triggers>
                    </ControlTemplate>
                  </Thumb.Template>
                </Thumb>
              </Track.Thumb>
              <Track.IncreaseRepeatButton>
                <RepeatButton Command="ScrollBar.PageDownCommand" Focusable="False" IsTabStop="False">
                  <RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate></RepeatButton.Template>
                </RepeatButton>
              </Track.IncreaseRepeatButton>
            </Track>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="Orientation" Value="Horizontal">
              <Setter TargetName="Root" Property="Width" Value="NaN"/>
              <Setter TargetName="Root" Property="Height" Value="10"/>
              <Setter TargetName="PART_Track" Property="Orientation" Value="Horizontal"/>
              <Setter TargetName="PART_Track" Property="IsDirectionReversed" Value="False"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ===== Tooltips and menus ===== -->
  <Style TargetType="ToolTip">
    <Setter Property="Foreground" Value="{DynamicResource Ink}"/>
    <Setter Property="Background" Value="{DynamicResource Surface}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource LineStrong}"/>
    <Setter Property="FontSize" Value="12"/>
    <Setter Property="MaxWidth" Value="340"/>
    <Setter Property="HasDropShadow" Value="False"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ToolTip">
          <Border Margin="8" Padding="10,6" CornerRadius="8" BorderThickness="1" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}">
            <Border.Effect><DropShadowEffect BlurRadius="14" ShadowDepth="3" Opacity="0.3" Color="Black"/></Border.Effect>
            <ContentPresenter>
              <ContentPresenter.Resources><Style TargetType="TextBlock"><Setter Property="TextWrapping" Value="Wrap"/></Style></ContentPresenter.Resources>
            </ContentPresenter>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="ContextMenu">
    <Setter Property="Foreground" Value="{DynamicResource Ink}"/>
    <Setter Property="Background" Value="{DynamicResource Surface}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource LineStrong}"/>
    <Setter Property="HasDropShadow" Value="False"/>
    <Setter Property="Padding" Value="6"/>
    <Setter Property="MinWidth" Value="230"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ContextMenu">
          <Border Margin="8" Padding="{TemplateBinding Padding}" CornerRadius="12" BorderThickness="1" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}">
            <Border.Effect><DropShadowEffect BlurRadius="18" ShadowDepth="4" Opacity="0.35" Color="Black"/></Border.Effect>
            <ItemsPresenter/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="MenuItem">
    <Setter Property="Foreground" Value="{DynamicResource Ink}"/>
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Padding" Value="8,7,14,7"/>
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="MenuItem">
          <Border x:Name="Chrome" CornerRadius="8" Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}" SnapsToDevicePixels="True">
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/>
              </Grid.ColumnDefinitions>
              <ContentPresenter ContentSource="Icon" Width="26" Height="26" Margin="0,0,12,0" VerticalAlignment="Center"/>
              <ContentPresenter Grid.Column="1" ContentSource="Header" VerticalAlignment="Center" RecognizesAccessKey="True"/>
              <TextBlock Grid.Column="2" Text="{TemplateBinding InputGestureText}" Margin="28,0,0,0" FontSize="12" Foreground="{DynamicResource Subtle}" VerticalAlignment="Center"/>
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsHighlighted" Value="True"><Setter TargetName="Chrome" Property="Background" Value="{DynamicResource Hover}"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="{x:Static MenuItem.SeparatorStyleKey}" TargetType="Separator">
    <Setter Property="Height" Value="1"/>
    <Setter Property="Margin" Value="10,5"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Separator"><Border Background="{DynamicResource Line}"/></ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>
""";
}

/// <summary>Hand-drawn 24×24 line icons (stroke only, round caps) so every glyph shares one weight and inherits its parent's colour.</summary>
internal static class Icons
{
    private static readonly Dictionary<string, string> Data = new()
    {
        ["area"] = "M3 7V5a2 2 0 0 1 2-2h2 M17 3h2a2 2 0 0 1 2 2v2 M21 17v2a2 2 0 0 1-2 2h-2 M7 21H5a2 2 0 0 1-2-2v-2",
        ["window"] = "M5 4h14a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z M3 9h18 M6.5 6.5h.01 M9.5 6.5h.01",
        ["screen"] = "M4 4h16a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z M8 21h8 M12 17v4",
        ["arrow"] = "M6 18L18 6 M8.5 6H18v9.5",
        ["box"] = "M5 5h14a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1z",
        ["text"] = "M5 7V5h14v2 M12 5v14 M9 19h6",
        ["blur"] = "M12 3.5C12 3.5 18 9.6 18 14a6 6 0 0 1-12 0C6 9.6 12 3.5 12 3.5z M9.5 14.5a2.5 2.5 0 0 0 2 2",
        ["crop"] = "M6 2v14a2 2 0 0 0 2 2h14 M18 22V8a2 2 0 0 0-2-2H2",
        ["undo"] = "M9 14L4 9l5-5 M4 9h10.5a5.5 5.5 0 0 1 0 11H11",
        ["redo"] = "M15 14l5-5-5-5 M20 9H9.5a5.5 5.5 0 0 0 0 11H13",
        ["copy"] = "M10 9h9a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2h-9a2 2 0 0 1-2-2v-9a2 2 0 0 1 2-2z M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1",
        ["share"] = "M12 15V3 M7.5 7.5L12 3l4.5 4.5 M5 12v7a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-7",
        ["save"] = "M12 4v11 M7.5 10.5L12 15l4.5-4.5 M5 20h14",
        ["trash"] = "M4 7h16 M9 7V4.5h6V7 M6 7l1 13h10l1-13 M10 11v5 M14 11v5",
        ["settings"] = "M4 7h9 M17 7h3 M4 17h3 M11 17h9 M13 4.5v5 M7 14.5v5",
        ["folder"] = "M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z",
        ["check"] = "M5 12.5l4.5 4.5L19 7.5",
        ["close"] = "M6 6l12 12 M18 6L6 18",
        ["plus"] = "M12 5v14 M5 12h14",
        ["chevron-down"] = "M6 9l6 6 6-6",
        ["chevron-left"] = "M15 5l-7 7 7 7",
        ["keyboard"] = "M4 6h16a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2z M6.5 10h.01 M10 10h.01 M14 10h.01 M17.5 10h.01 M7.5 14h9",
        ["image"] = "M5 4h14a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z M3 16l5-5 4 4 3-3 6 6 M15.5 9h.01",
        ["edit"] = "M4 20l1-4L16.5 4.5a2.1 2.1 0 0 1 3 3L8 19l-4 1z M14 7l3 3",
        ["alert"] = "M12 4L2.8 19.5h18.4z M12 10v4.5 M12 17.5h.01",
        ["info"] = "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18z M12 11v5 M12 7.5h.01",
        ["chat"] = "M21 12a8.5 8.5 0 0 1-12.4 7.5L3 21l1.6-5.3A8.5 8.5 0 1 1 21 12z",
        ["plane"] = "M21 3L3 10.5l7 2.5 2.5 7z M10 13l11-10",
        ["mail"] = "M4 5.5h16a1.5 1.5 0 0 1 1.5 1.5v10a1.5 1.5 0 0 1-1.5 1.5H4A1.5 1.5 0 0 1 2.5 17V7A1.5 1.5 0 0 1 4 5.5z M3 7l9 6.5L21 7",
        ["power"] = "M12 3v9 M6.5 6.5a8 8 0 1 0 11 0",
        ["refresh"] = "M20 12a8 8 0 1 1-2.5-5.8 M20 4v5h-5",
        ["line"] = "M5 12H19",
    };

    private static readonly Dictionary<string, Geometry> Cache = new();

    internal static Geometry Get(string name)
    {
        if (Cache.TryGetValue(name, out var geometry)) return geometry;
        geometry = Geometry.Parse(Data[name]);
        geometry.Freeze();
        return Cache[name] = geometry;
    }

    internal static IEnumerable<string> Names => Data.Keys;
}

internal static class Ui
{
    /// <summary>Icon that takes its colour from the surrounding control's Foreground unless a palette key is given.</summary>
    internal static FrameworkElement Icon(string name, double size = 18, string? brush = null, double stroke = 1.75)
    {
        var path = new Path
        {
            Data = Icons.Get(name), StrokeThickness = stroke, StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        };
        if (brush != null) path.SetResourceReference(Shape.StrokeProperty, brush);
        else path.SetBinding(Shape.StrokeProperty, new Binding { Path = new PropertyPath("(TextElement.Foreground)"), RelativeSource = RelativeSource.Self });
        var box = new Canvas { Width = 24, Height = 24 };
        box.Children.Add(path);
        return new Viewbox { Width = size, Height = size, Child = box, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center };
    }

    internal static TextBlock Text(string text, double size = 13, string ink = "Ink", FontWeight? weight = null, bool wrap = false)
    {
        var label = new TextBlock { Text = text, FontSize = size, FontWeight = weight ?? FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center };
        if (wrap) label.TextWrapping = TextWrapping.Wrap;
        label.SetResourceReference(TextBlock.ForegroundProperty, ink);
        return label;
    }

    /// <summary>Row of key caps for a shortcut such as "Ctrl + Shift + A".</summary>
    internal static FrameworkElement Kbd(string label, double size = 11.5)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (string part in label.Split(" + "))
        {
            var cap = new Border { CornerRadius = new CornerRadius(Math.Round(size * 0.42)), BorderThickness = new Thickness(1, 1, 1, 2), Padding = new Thickness(size * 0.52, size * 0.09, size * 0.52, size * 0.09), Margin = new Thickness(row.Children.Count == 0 ? 0 : 4, 0, 0, 0), Child = Text(part, size, "Muted", FontWeights.SemiBold) };
            cap.SetResourceReference(Border.BackgroundProperty, "Field");
            cap.SetResourceReference(Border.BorderBrushProperty, "LineStrong");
            row.Children.Add(cap);
        }
        return row;
    }

    internal static Border Card(UIElement child, double radius = 16, Thickness? padding = null)
    {
        var card = new Border { CornerRadius = new CornerRadius(radius), BorderThickness = new Thickness(1), Padding = padding ?? new Thickness(0), Child = child };
        card.SetResourceReference(Border.BackgroundProperty, "Surface");
        card.SetResourceReference(Border.BorderBrushProperty, "Line");
        return card;
    }

    internal static Border Divider(double height = 20)
    {
        var line = new Border { Width = 1, Height = height, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        line.SetResourceReference(Border.BackgroundProperty, "Line");
        return line;
    }

    internal static Button Styled(string style, object content, Action? click = null, string? tip = null, string? name = null)
    {
        var button = new Button { Content = content };
        if (style != "Button") button.SetResourceReference(FrameworkElement.StyleProperty, style); // "Button" is the implicit default
        if (click != null) button.Click += (_, _) => click();
        if (tip != null) button.ToolTip = tip;
        System.Windows.Automation.AutomationProperties.SetName(button, name ?? tip ?? (content as string) ?? "");
        return button;
    }

    /// <summary>Icon on the left, label on the right, sharing the button's Foreground.</summary>
    internal static StackPanel IconLabel(string icon, string label, out TextBlock text, double iconSize = 17)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(Icon(icon, iconSize));
        text = new TextBlock { Text = label, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(text);
        return row;
    }
}
