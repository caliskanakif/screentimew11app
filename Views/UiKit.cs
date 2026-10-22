using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace ScreenTimeTracker.Views;

/// <summary>Small helpers that build the dark "glass" look in code (no XAML files needed).</summary>
public static class UiKit
{
    public const string TextC = "#F2FFFFFF";
    public const string DimC = "#A6FFFFFF";
    public const string AccentFill = "#E63B82F6";
    public const string SoftFill = "#26FFFFFF";

    public static Color Col(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Colors.White; }
    }

    public static SolidColorBrush Br(string hex) => new(Col(hex));

    public static string Fmt(double secs)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, secs));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{t.Minutes}m {t.Seconds:00}s";
    }

    public static string FmtClock(double secs)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, secs));
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
    }

    public static TextBlock Lbl(string text, double size = 13, bool dim = false, bool bold = false) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = Br(dim ? DimC : TextC),
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center
    };

    public static Border Card(UIElement child, Thickness? margin = null) => new()
    {
        Background = Br("#1AFFFFFF"),
        BorderBrush = Br("#26FFFFFF"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(14),
        Padding = new Thickness(16),
        Margin = margin ?? new Thickness(0, 0, 0, 12),
        Child = child
    };

    public static StackPanel Row(params UIElement[] items)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 4) };
        foreach (var i in items) sp.Children.Add(i);
        return sp;
    }

    public static StackPanel Field(string title, UIElement control)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        var l = Lbl(title, 12, dim: true);
        l.Margin = new Thickness(0, 0, 0, 6);
        sp.Children.Add(l);
        sp.Children.Add(control);
        return sp;
    }

    // ---------- buttons ----------
    static readonly ControlTemplate BtnTemplate = (ControlTemplate)XamlReader.Parse(
        "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
        "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>" +
        "<Border x:Name='bd' Background='{TemplateBinding Background}' CornerRadius='10' Padding='{TemplateBinding Padding}'>" +
        "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>" +
        "<ControlTemplate.Triggers>" +
        "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='Opacity' Value='0.8'/></Trigger>" +
        "<Trigger Property='IsPressed' Value='True'><Setter TargetName='bd' Property='Opacity' Value='0.6'/></Trigger>" +
        "</ControlTemplate.Triggers></ControlTemplate>");

    public static Button Btn(string text, Action onClick, bool accent = false)
    {
        var b = new Button
        {
            Content = text,
            Foreground = Br("#FFFFFFFF"),
            Background = Br(accent ? AccentFill : SoftFill),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = Cursors.Hand,
            FontSize = 13,
            Template = BtnTemplate
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    /// <summary>A row of buttons where exactly one is highlighted (used instead of ComboBoxes on the dark theme).</summary>
    public static StackPanel Segmented(string[] items, int selected, Action<int> onSelect)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        var btns = new List<Button>();
        for (int i = 0; i < items.Length; i++)
        {
            int idx = i;
            var b = Btn(items[i], () =>
            {
                for (int k = 0; k < btns.Count; k++) btns[k].Background = Br(k == idx ? AccentFill : SoftFill);
                onSelect(idx);
            });
            b.Background = Br(i == selected ? AccentFill : SoftFill);
            b.Margin = new Thickness(0, 0, 6, 0);
            btns.Add(b);
            sp.Children.Add(b);
        }
        return sp;
    }

    /// <summary>On/Off switch built from a two-button segmented control.</summary>
    public static StackPanel Toggle(bool value, Action<bool> onChange) =>
        Segmented(new[] { "Off", "On" }, value ? 1 : 0, i => onChange(i == 1));

    // ---------- inputs ----------
    public static TextBox Tb(string text, double width, Action<string>? onChanged = null)
    {
        var tb = new TextBox
        {
            Text = text,
            Width = width,
            Foreground = Br("#FFFFFFFF"),
            CaretBrush = Br("#FFFFFFFF"),
            Background = Br("#33000000"),
            BorderBrush = Br("#44FFFFFF"),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 13
        };
        if (onChanged != null) tb.TextChanged += (_, _) => onChanged(tb.Text);
        return tb;
    }

    public static Slider Sld(double min, double max, double value, Action<double> onChange, double width = 280)
    {
        var s = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            Width = width,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        s.ValueChanged += (_, _) => onChange(s.Value);
        return s;
    }

    static readonly string[] Presets = { "#FFFFFF", "#FFD740", "#FF5252", "#69F0AE", "#40C4FF", "#B388FF" };

    /// <summary>Color swatch + hex box + "Pick..." button (+ optional quick presets).</summary>
    public static StackPanel ColorField(string initial, Action<string> onChange, bool presets = false)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var sw = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(13),
            BorderBrush = Br("#66FFFFFF"),
            BorderThickness = new Thickness(1),
            Background = Br(initial),
            Margin = new Thickness(0, 0, 8, 0)
        };
        var tb = Tb(initial, 96);
        tb.TextChanged += (_, _) =>
        {
            if (Regex.IsMatch(tb.Text, "^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$"))
            {
                sw.Background = Br(tb.Text);
                onChange(tb.Text);
            }
        };
        row.Children.Add(sw);
        row.Children.Add(tb);
        row.Children.Add(Btn("Pick…", () =>
        {
            var r = PickColor(tb.Text);
            if (r != null) tb.Text = r;
        }));
        if (presets)
        {
            foreach (var hex in Presets)
            {
                var dot = new Border
                {
                    Width = 20,
                    Height = 20,
                    CornerRadius = new CornerRadius(10),
                    Background = Br(hex),
                    BorderBrush = Br("#66FFFFFF"),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = Cursors.Hand
                };
                string h = hex;
                dot.MouseLeftButtonUp += (_, _) => tb.Text = h;
                row.Children.Add(dot);
            }
        }
        return row;
    }

    static string? PickColor(string current)
    {
        using var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true };
        var c = Col(current);
        dlg.Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            return $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
        return null;
    }
}
