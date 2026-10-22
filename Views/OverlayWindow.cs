using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using ScreenTimeTracker.Services;
using static ScreenTimeTracker.Views.UiKit;

namespace ScreenTimeTracker.Views;

/// <summary>
/// The floating timer: frameless, transparent window, always on top.
/// Everything about its look comes from the settings (Appearance page).
/// Dragging only works while edit mode is on. Double-click hides it (tray icon brings it back).
/// </summary>
public sealed class OverlayWindow : Window
{
    readonly TrackerService _t;
    readonly Border _pill;
    readonly TextBlock _text;
    readonly Grid _root;
    readonly SolidColorBrush _fg = new(Colors.White);
    Color _baseColor = Colors.White;
    Color _target = Colors.White;
    bool _edit;

    public event Action? VisibilityChanged;

    public bool EditMode
    {
        get => _edit;
        set
        {
            _edit = value;
            _pill.Cursor = value ? Cursors.SizeAll : Cursors.Arrow;
            ApplyBorder();
        }
    }

    public OverlayWindow(TrackerService t)
    {
        _t = t;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        _text = new TextBlock
        {
            Foreground = _fg,
            Text = "00:00:00",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _pill = new Border { Child = _text };
        _pill.SizeChanged += (_, _) => UpdateCorner();

        _root = new Grid { Margin = new Thickness(24) };   // room for the shadow
        _root.Children.Add(_pill);
        Content = _root;

        // restore position (and keep it on a visible screen)
        double minL = SystemParameters.VirtualScreenLeft, minT = SystemParameters.VirtualScreenTop;
        double maxL = Math.Max(minL, minL + SystemParameters.VirtualScreenWidth - 120);
        double maxT = Math.Max(minT, minT + SystemParameters.VirtualScreenHeight - 80);
        Left = Math.Clamp(_t.Settings.OverlayLeft, minL, maxL);
        Top = Math.Clamp(_t.Settings.OverlayTop, minT, maxT);

        SourceInitialized += (_, _) => NativeMethods.MakeToolWindow(this);
        MouseLeftButtonDown += OnMouseDown;

        Apply();
        _t.Updated += Update;
    }

    static Color WithAlpha(Color c, double factor) =>
        Color.FromArgb((byte)Math.Round(Math.Clamp(c.A * factor, 0, 255)), c.R, c.G, c.B);

    static Color Mix(Color a, Color b, double t) => Color.FromArgb(
        a.A,
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));

    /// <summary>Re-reads every look setting (font, colors, shape, border, shadow ...).</summary>
    public void Apply()
    {
        var s = _t.Settings;

        // text
        _text.FontFamily = new FontFamily(s.FontFamily);
        _text.FontSize = Math.Clamp(s.FontSize, 8, 160);
        _text.FontWeight = s.FontWeight switch
        {
            "Light" => FontWeights.Light,
            "SemiBold" => FontWeights.SemiBold,
            "Bold" => FontWeights.Bold,
            _ => FontWeights.Normal
        };
        _baseColor = Col(s.FontColor);
        Opacity = Math.Clamp(s.Opacity, 0.2, 1.0);

        // size
        _pill.Padding = new Thickness(Math.Max(0, s.PaddingH), Math.Max(0, s.PaddingV), Math.Max(0, s.PaddingH), Math.Max(0, s.PaddingV));

        // background: solid, or "liquid glass" gradient; BackgroundOpacity 0 (clear) ... 1 (opaque)
        var bg = Col(s.BackgroundColor);
        double op = Math.Clamp(s.BackgroundOpacity, 0, 1);
        if (s.GlassEffect)
        {
            var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            g.GradientStops.Add(new GradientStop(WithAlpha(Mix(bg, Colors.White, 0.22), op * 0.85), 0.0));
            g.GradientStops.Add(new GradientStop(WithAlpha(bg, op * 0.95), 0.5));
            g.GradientStops.Add(new GradientStop(WithAlpha(Mix(bg, Colors.Black, 0.35), op), 1.0));
            _pill.Background = g;
        }
        else
        {
            _pill.Background = new SolidColorBrush(WithAlpha(bg, op));
        }

        ApplyBorder();

        // shadow
        if (s.ShadowEnabled)
        {
            double k = Math.Clamp(s.ShadowStrength, 0, 100) / 100.0;
            _pill.Effect = new DropShadowEffect
            {
                BlurRadius = 6 + 30 * k,
                ShadowDepth = 2 + 3 * k,
                Direction = 270,
                Opacity = 0.15 + 0.5 * k,
                Color = Colors.Black
            };
        }
        else
        {
            _pill.Effect = null;
        }

        UpdateCorner();
        Update();
    }

    void ApplyBorder()
    {
        var s = _t.Settings;
        if (_edit)
        {
            _pill.BorderBrush = Br("#FF4DA3FF");
            _pill.BorderThickness = new Thickness(2);
        }
        else if (s.BorderEnabled)
        {
            _pill.BorderBrush = Br(s.BorderColor);
            _pill.BorderThickness = new Thickness(Math.Clamp(s.BorderWidth, 0, 12));
        }
        else
        {
            _pill.BorderThickness = new Thickness(0);
        }
    }

    /// <summary>CornerPercent: 0 = square corners ... 100 = fully round (pill).</summary>
    void UpdateCorner()
    {
        double pct = Math.Clamp(_t.Settings.CornerPercent, 0, 100) / 100.0;
        _pill.CornerRadius = new CornerRadius(pct * _pill.ActualHeight / 2.0);
    }

    void Update()
    {
        double secs = _t.Today.ActiveSeconds;
        var ts = TimeSpan.FromSeconds(Math.Max(0, secs));
        _text.Text = _t.Settings.ShowSeconds
            ? $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{(int)ts.TotalHours:00}:{ts.Minutes:00}";

        var target = _baseColor;
        foreach (var th in _t.Settings.Thresholds.OrderBy(x => x.Minutes))
            if (secs >= th.Minutes * 60.0) target = Col(th.Color);

        if (target != _target)
        {
            _target = target;
            _fg.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(target, TimeSpan.FromMilliseconds(500)));
        }
        _text.Opacity = _t.Paused ? 0.45 : 1.0;
    }

    void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            HideOverlay();
            return;
        }
        if (_edit && e.ChangedButton == MouseButton.Left)
        {
            try { DragMove(); } catch { }
            _t.Settings.OverlayLeft = Left;
            _t.Settings.OverlayTop = Top;
            _t.SaveSettings();
        }
    }

    public void ShowOverlay()
    {
        _t.Settings.OverlayVisible = true;
        Show();
        _root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250)));
        _t.SaveSettings();
        VisibilityChanged?.Invoke();
    }

    public void HideOverlay()
    {
        _t.Settings.OverlayVisible = false;
        Hide();
        _t.SaveSettings();
        VisibilityChanged?.Invoke();
    }
}
