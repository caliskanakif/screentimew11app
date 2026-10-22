using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ScreenTimeTracker.Models;
using ScreenTimeTracker.Services;
using Shapes = System.Windows.Shapes;
using static ScreenTimeTracker.Views.UiKit;

namespace ScreenTimeTracker.Views;

/// <summary>Settings + statistics window. Built completely in code. Closing it never stops tracking.</summary>
public sealed class DashboardWindow : Window
{
    readonly TrackerService _t;
    readonly OverlayWindow _o;
    readonly ScrollViewer _scroll;
    readonly List<Button> _nav = new();
    readonly DispatcherTimer _save = new() { Interval = TimeSpan.FromMilliseconds(500) };
    int _page = -1;

    Button _btnEdit = null!, _btnPause = null!, _btnVis = null!;

    // live widgets of the overview page
    TextBlock? _ovClock, _ovStatus, _ovIdle, _ovYesterday, _ovAvg, _ovFocus;
    StackPanel? _ovApps;
    int _ovTick;

    // statistics page
    StackPanel? _statsHost;

    static readonly string[] Ranges = { "Day", "Week", "Month", "Year", "All" };
    static readonly string[] Charts = { "Bar", "Line", "Pie" };
    static readonly string[] Palette = { "#3B82F6", "#A78BFA", "#34D399", "#FBBF24", "#F87171", "#22D3EE", "#F472B6", "#94A3B8" };

    record Bucket(string Label, double Secs);

    public DashboardWindow(TrackerService t, OverlayWindow o)
    {
        _t = t;
        _o = o;

        Title = "Screen Time Tracker";
        Width = 1040;
        Height = 720;
        MinWidth = 820;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Br("#B8101018");
        Foreground = Br("#FFFFFFFF");
        FontFamily = new FontFamily("Segoe UI");
        var icon = AppIcon.Image();
        if (icon != null) Icon = icon;

        SourceInitialized += (_, _) => NativeMethods.ApplyGlass(this);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(212) });
        root.ColumnDefinitions.Add(new ColumnDefinition());

        // ---- sidebar ----
        var side = new StackPanel { Margin = new Thickness(14, 18, 14, 14) };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 20) };
        if (icon != null)
            brand.Children.Add(new Image { Source = icon, Width = 34, Height = 34, Margin = new Thickness(0, 0, 10, 0) });
        brand.Children.Add(Lbl("Screen Time", 17, bold: true));
        side.Children.Add(brand);

        string[] names = { "Overview", "Statistics", "Appearance", "Timer & Idle", "General" };
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            var b = Btn(names[i], () => Navigate(idx));
            b.Margin = new Thickness(0, 0, 0, 6);
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Padding = new Thickness(14, 9, 14, 9);
            b.FontSize = 14;
            _nav.Add(b);
            side.Children.Add(b);
        }
        var sideBg = new Border { Background = Br("#33000000"), Child = side };
        Grid.SetColumn(sideBg, 0);
        root.Children.Add(sideBg);

        // ---- right side: action bar + page ----
        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition());

        _btnEdit = Btn("Edit position", ToggleEdit);
        _btnPause = Btn("Pause", () => { _t.Paused = !_t.Paused; _t.Refresh(); UpdateHeader(); });
        _btnVis = Btn("Hide timer", () => { if (_o.IsVisible) _o.HideOverlay(); else _o.ShowOverlay(); UpdateHeader(); });
        var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 24, 0) };
        bar.Children.Add(_btnEdit);
        bar.Children.Add(_btnPause);
        bar.Children.Add(_btnVis);
        Grid.SetRow(bar, 0);
        right.Children.Add(bar);

        _scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(_scroll, 1);
        right.Children.Add(_scroll);

        Grid.SetColumn(right, 1);
        root.Children.Add(right);
        Content = root;

        _save.Tick += (_, _) => { _save.Stop(); _t.SaveSettings(); };
        _t.Updated += OnTick;
        _o.VisibilityChanged += UpdateHeader;

        UpdateHeader();
        Navigate(0);
    }

    protected override void OnClosed(EventArgs e)
    {
        _t.Updated -= OnTick;
        _o.VisibilityChanged -= UpdateHeader;
        _save.Stop();
        _o.EditMode = false;
        _t.SaveSettings();
        base.OnClosed(e);
    }

    // ------------------------------------------------------------ helpers
    void QueueSave() { _save.Stop(); _save.Start(); }
    void Changed() { _o.Apply(); QueueSave(); }

    void ToggleEdit()
    {
        if (!_o.EditMode && !_o.IsVisible) _o.ShowOverlay();
        _o.EditMode = !_o.EditMode;
        UpdateHeader();
    }

    void UpdateHeader()
    {
        _btnEdit.Content = _o.EditMode ? "Lock position" : "Edit position";
        _btnEdit.Background = Br(_o.EditMode ? AccentFill : SoftFill);
        _btnPause.Content = _t.Paused ? "Resume" : "Pause";
        _btnVis.Content = _o.IsVisible ? "Hide timer" : "Show timer";
    }

    void Navigate(int i)
    {
        _page = i;
        for (int k = 0; k < _nav.Count; k++) _nav[k].Background = Br(k == i ? AccentFill : "#00000000");
        _ovClock = _ovStatus = _ovIdle = _ovYesterday = _ovAvg = _ovFocus = null;
        _ovApps = null;
        _statsHost = null;

        UIElement page = i switch
        {
            0 => BuildOverview(),
            1 => BuildStats(),
            2 => BuildAppearance(),
            3 => BuildTimer(),
            _ => BuildGeneral()
        };
        _scroll.Content = page;
        _scroll.ScrollToTop();
        page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
    }

    static StackPanel NewPage(string title)
    {
        var sp = new StackPanel { Margin = new Thickness(28, 6, 28, 28) };
        var l = Lbl(title, 26, bold: true);
        l.Margin = new Thickness(0, 0, 0, 16);
        sp.Children.Add(l);
        return sp;
    }

    static StackPanel CardTitle(string title, params UIElement[] content)
    {
        var sp = new StackPanel();
        var l = Lbl(title, 15, bold: true);
        l.Margin = new Thickness(0, 0, 0, 12);
        sp.Children.Add(l);
        foreach (var c in content) sp.Children.Add(c);
        return sp;
    }

    static TextBlock Hint(string text)
    {
        var l = Lbl(text, 12, dim: true);
        l.Margin = new Thickness(0, 0, 0, 10);
        return l;
    }

    static Border StatCard(string title, TextBlock value)
    {
        value.FontSize = 22;
        value.FontWeight = FontWeights.SemiBold;
        value.Margin = new Thickness(0, 4, 0, 0);
        var sp = new StackPanel();
        sp.Children.Add(Lbl(title, 12, dim: true));
        sp.Children.Add(value);
        return Card(sp, new Thickness(0, 0, 10, 12));
    }

    static Grid CardRow(params UIElement[] cards)
    {
        var g = new Grid();
        for (int i = 0; i < cards.Length; i++)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(cards[i], i);
            g.Children.Add(cards[i]);
        }
        return g;
    }

    UIElement SliderField(string title, double min, double max, double val, Func<double, string> fmt, Action<double> apply)
    {
        var lbl = Lbl(fmt(val), 13);
        lbl.Width = 90;
        var s = Sld(min, max, val, v => { lbl.Text = fmt(v); apply(v); Changed(); });
        return Field(title, Row(s, lbl));
    }

    /// <summary>One bar row: name on the left, filled bar, text on the right.</summary>
    static UIElement BarRow(string name, double frac, string right, string color = "#3B82F6")
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });

        var n = Lbl(name, 13);
        n.TextTrimming = TextTrimming.CharacterEllipsis;
        n.TextWrapping = TextWrapping.NoWrap;
        Grid.SetColumn(n, 0);

        frac = Math.Clamp(frac, 0, 1);
        var track = new Grid { Height = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(frac, 0.0001), GridUnitType.Star) });
        track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1 - frac, 0.0001), GridUnitType.Star) });
        var back = new Border { Background = Br("#1AFFFFFF"), CornerRadius = new CornerRadius(5) };
        Grid.SetColumnSpan(back, 2);
        var fill = new Border { Background = Br(color), CornerRadius = new CornerRadius(5) };
        track.Children.Add(back);
        track.Children.Add(fill);
        Grid.SetColumn(track, 1);

        var r = Lbl(right, 12, dim: true);
        r.TextWrapping = TextWrapping.NoWrap;
        r.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(r, 2);

        g.Children.Add(n);
        g.Children.Add(track);
        g.Children.Add(r);
        return g;
    }

    static void FillApps(StackPanel host, Dictionary<string, double> apps, int max)
    {
        host.Children.Clear();
        double total = apps.Values.Sum();
        if (total <= 0)
        {
            host.Children.Add(Lbl("No data yet.", 13, dim: true));
            return;
        }
        double top = apps.Values.Max();
        int i = 0;
        foreach (var kv in apps.OrderByDescending(k => k.Value).Take(max))
        {
            host.Children.Add(BarRow(kv.Key, kv.Value / top, $"{Fmt(kv.Value)} · {kv.Value / total * 100:0}%", Palette[i % Palette.Length]));
            i++;
        }
    }

    string FocusText(Dictionary<string, double> apps)
    {
        double total = apps.Values.Sum();
        if (total < 60) return "Not enough data yet";
        var set = new HashSet<string>(
            (_t.Settings.FocusApps ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
        double focus = apps.Where(kv => set.Contains(kv.Key)).Sum(kv => kv.Value);
        double pct = focus / total * 100;
        string label = pct >= _t.Settings.FocusHighPercent ? "Focused"
                     : pct >= _t.Settings.FocusLowPercent ? "Balanced"
                     : "Leisure / distracted";
        return $"{label} — {pct:0}% of active time in your focus apps";
    }

    static Dictionary<string, double> Aggregate(IEnumerable<DayRecord> recs)
    {
        var d = new Dictionary<string, double>();
        foreach (var r in recs)
            foreach (var kv in r.Apps)
                d[kv.Key] = d.GetValueOrDefault(kv.Key) + kv.Value;
        return d;
    }

    List<(DateTime date, DayRecord rec)> DaysInRange(DateTime start, DateTime end)
    {
        var list = new List<(DateTime, DayRecord)>();
        foreach (var kv in _t.Days)
            if (DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                && d >= start && d <= end)
                list.Add((d, kv.Value));
        return list;
    }

    // ------------------------------------------------------------ overview
    void OnTick()
    {
        if (_page == 0) UpdateOverview();
        UpdateHeader();
    }

    UIElement BuildOverview()
    {
        var sp = NewPage("Overview");

        _ovClock = new TextBlock { FontSize = 56, FontWeight = FontWeights.Light, Foreground = Br("#FFFFFFFF") };
        _ovStatus = Lbl("", 13, dim: true);
        var hero = new StackPanel();
        hero.Children.Add(Lbl("Active screen time today", 13, dim: true));
        hero.Children.Add(_ovClock);
        hero.Children.Add(_ovStatus);
        sp.Children.Add(Card(hero));

        _ovIdle = Lbl("", 22, bold: true);
        _ovYesterday = Lbl("", 22, bold: true);
        _ovAvg = Lbl("", 22, bold: true);
        sp.Children.Add(CardRow(
            StatCard("Idle today (not counted)", _ovIdle),
            StatCard("Yesterday", _ovYesterday),
            StatCard("Average, last 7 days", _ovAvg)));

        _ovApps = new StackPanel();
        sp.Children.Add(Card(CardTitle("Top apps & games today", _ovApps)));

        _ovFocus = Lbl("", 13);
        sp.Children.Add(Card(CardTitle("Focus today", _ovFocus)));

        _ovTick = 0;
        UpdateOverview();
        return sp;
    }

    void UpdateOverview()
    {
        if (_ovClock == null) return;
        var d = _t.Today;
        _ovClock.Text = FmtClock(d.ActiveSeconds);
        _ovStatus!.Text = _t.Paused ? "Paused"
            : _t.IsIdle ? "Idle - not counting"
            : "Counting" + (_t.CurrentApp.Length > 0 ? " · " + _t.CurrentApp : "");
        _ovIdle!.Text = Fmt(d.IdleSeconds);

        if (_ovTick % 5 == 0)
        {
            var today = _t.TrackingDate(DateTime.Now);
            _ovYesterday!.Text = Fmt(_t.FindDay(today.AddDays(-1))?.ActiveSeconds ?? 0);
            var week = DaysInRange(today.AddDays(-6), today).Where(x => x.rec.ActiveSeconds > 0).ToList();
            _ovAvg!.Text = week.Count == 0 ? "–" : Fmt(week.Average(x => x.rec.ActiveSeconds));
            FillApps(_ovApps!, d.Apps, 8);
            _ovFocus!.Text = FocusText(d.Apps);
        }
        _ovTick++;
    }

    // ------------------------------------------------------------ statistics
    UIElement BuildStats()
    {
        var sp = NewPage("Statistics");

        var seg1 = Segmented(Ranges, Math.Max(0, Array.IndexOf(Ranges, _t.Settings.StatsRange)), i =>
        {
            _t.Settings.StatsRange = Ranges[i];
            QueueSave();
            RefreshStats();
        });
        var seg2 = Segmented(Charts, Math.Max(0, Array.IndexOf(Charts, _t.Settings.StatsChart)), i =>
        {
            _t.Settings.StatsChart = Charts[i];
            QueueSave();
            RefreshStats();
        });
        seg2.Margin = new Thickness(28, 0, 0, 0);
        sp.Children.Add(Card(Row(seg1, seg2)));

        _statsHost = new StackPanel();
        sp.Children.Add(_statsHost);
        RefreshStats();
        return sp;
    }

    (DateTime start, DateTime end) GetRange(string range)
    {
        var today = _t.TrackingDate(DateTime.Now);
        switch (range)
        {
            case "Day": return (today, today);
            case "Week": return (today.AddDays(-6), today);
            case "Month": return (today.AddDays(-29), today);
            case "Year": return (new DateTime(today.Year, today.Month, 1).AddMonths(-11), today);
            default:
                var first = _t.Days.Where(kv => kv.Value.ActiveSeconds > 0)
                    .Select(kv => DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : today)
                    .DefaultIfEmpty(today).Min();
                return (first, today);
        }
    }

    List<Bucket> BuildBuckets(string range, DateTime start, DateTime end, List<(DateTime date, DayRecord rec)> recs)
    {
        var list = new List<Bucket>();
        switch (range)
        {
            case "Day":
                var rec = recs.Count > 0 ? recs[0].rec : new DayRecord();
                for (int h = 0; h < 24; h++) list.Add(new Bucket(h.ToString("00"), rec.Hours[h]));
                break;
            case "Week":
            case "Month":
                for (var d = start; d <= end; d = d.AddDays(1))
                    list.Add(new Bucket(range == "Week" ? d.ToString("ddd") : d.Day.ToString(), _t.FindDay(d)?.ActiveSeconds ?? 0));
                break;
            case "Year":
                for (int m = 0; m < 12; m++)
                {
                    var ms = start.AddMonths(m);
                    list.Add(new Bucket(ms.ToString("MMM"),
                        recs.Where(x => x.date.Year == ms.Year && x.date.Month == ms.Month).Sum(x => x.rec.ActiveSeconds)));
                }
                break;
            default:
                int months = (end.Year - start.Year) * 12 + end.Month - start.Month + 1;
                if (months <= 36)
                {
                    var ms0 = new DateTime(start.Year, start.Month, 1);
                    for (int m = 0; m < months; m++)
                    {
                        var ms = ms0.AddMonths(m);
                        list.Add(new Bucket(ms.ToString("MMM yy"),
                            recs.Where(x => x.date.Year == ms.Year && x.date.Month == ms.Month).Sum(x => x.rec.ActiveSeconds)));
                    }
                }
                else
                {
                    for (int y = start.Year; y <= end.Year; y++)
                        list.Add(new Bucket(y.ToString(), recs.Where(x => x.date.Year == y).Sum(x => x.rec.ActiveSeconds)));
                }
                break;
        }
        return list;
    }

    void RefreshStats()
    {
        if (_statsHost == null) return;
        _statsHost.Children.Clear();

        string range = Ranges.Contains(_t.Settings.StatsRange) ? _t.Settings.StatsRange : "Week";
        string chart = Charts.Contains(_t.Settings.StatsChart) ? _t.Settings.StatsChart : "Bar";
        var (start, end) = GetRange(range);
        var recs = DaysInRange(start, end);
        var buckets = BuildBuckets(range, start, end, recs);
        var apps = Aggregate(recs.Select(x => x.rec));

        // ---- chart ----
        var canvas = new Canvas { Height = 290, ClipToBounds = true };
        bool first = true;
        canvas.SizeChanged += (_, _) =>
        {
            DrawChart(canvas, chart, buckets, apps, first);
            first = false;
        };
        _statsHost.Children.Add(Card(canvas));

        // ---- summary ----
        double total = recs.Sum(x => x.rec.ActiveSeconds);
        var withData = recs.Where(x => x.rec.ActiveSeconds > 0).ToList();
        double avg = withData.Count == 0 ? 0 : total / withData.Count;
        string best = "–";
        if (withData.Count > 0)
        {
            var b = withData.OrderByDescending(x => x.rec.ActiveSeconds).First();
            best = $"{Fmt(b.rec.ActiveSeconds)} ({b.date:d MMM})";
        }
        string vs = "–";
        if (range != "All")
        {
            int len = (end - start).Days + 1;
            double prev = DaysInRange(start.AddDays(-len), start.AddDays(-1)).Sum(x => x.rec.ActiveSeconds);
            if (prev > 0)
            {
                double ch = (total - prev) / prev * 100;
                vs = (ch >= 0 ? "+" : "") + ch.ToString("0") + "%";
            }
        }
        _statsHost.Children.Add(CardRow(
            StatCard("Total", Lbl(Fmt(total), 22)),
            StatCard(range == "Day" ? "Average / hour of day" : "Average / active day", Lbl(range == "Day" ? Fmt(total / 24) : Fmt(avg), 22)),
            StatCard("Best day", Lbl(best, 22)),
            StatCard("vs. previous period", Lbl(vs, 22))));

        // ---- apps + focus ----
        var appsHost = new StackPanel();
        FillApps(appsHost, apps, 12);
        _statsHost.Children.Add(Card(CardTitle("Apps & games in this range", appsHost)));
        _statsHost.Children.Add(Card(CardTitle("Focus", Lbl(FocusText(apps), 13))));

        // ---- history ----
        var hist = new StackPanel();
        var all = _t.Days
            .Select(kv => DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? (date: d, rec: kv.Value) : (date: DateTime.MinValue, rec: kv.Value))
            .Where(x => x.date != DateTime.MinValue && x.rec.ActiveSeconds > 0)
            .OrderByDescending(x => x.date).ToList();
        if (all.Count == 0)
        {
            hist.Children.Add(Lbl("No days recorded yet.", 13, dim: true));
        }
        else
        {
            hist.Children.Add(Hint($"{all.Count} days stored · tracking since {all[^1].date:d MMM yyyy}. Every day is kept forever (showing the latest 60)."));
            var show = all.Take(60).ToList();
            double mx = show.Max(x => x.rec.ActiveSeconds);
            foreach (var x in show)
                hist.Children.Add(BarRow(x.date.ToString("ddd, d MMM yyyy"), x.rec.ActiveSeconds / mx, Fmt(x.rec.ActiveSeconds)));
        }
        _statsHost.Children.Add(Card(CardTitle("Daily history", hist)));
    }

    // ------------------------------------------------------------ charts
    static TextBlock Small(string text)
    {
        var l = Lbl(text, 11, dim: true);
        l.TextWrapping = TextWrapping.NoWrap;
        return l;
    }

    static void Put(Canvas c, UIElement e, double x, double y)
    {
        Canvas.SetLeft(e, x);
        Canvas.SetTop(e, y);
        c.Children.Add(e);
    }

    void DrawChart(Canvas c, string kind, List<Bucket> data, Dictionary<string, double> apps, bool anim)
    {
        if (c.ActualWidth < 120 || c.ActualHeight < 100) return;
        c.Children.Clear();
        if (kind == "Pie") DrawPie(c, apps, anim);
        else if (data.Sum(b => b.Secs) <= 0)
            Put(c, Lbl("No data in this range yet.", 13, dim: true), 20, c.ActualHeight / 2 - 10);
        else if (kind == "Line") DrawLine(c, data, anim);
        else DrawBars(c, data, anim);
    }

    /// <summary>Grid lines + y labels (minutes if small, else hours). Returns the axis maximum in seconds.</summary>
    static double DrawAxes(Canvas c, double L, double T, double plotW, double plotH, double maxSecs)
    {
        bool mins = maxSecs < 3600;
        double unit = mins ? 60 : 3600;
        double maxV = Math.Max(maxSecs / unit, mins ? 1 : 0.25);
        double[] steps = { 0.25, 0.5, 1, 2, 4, 5, 10, 20, 50, 100, 200, 500, 1000 };
        double step = steps.FirstOrDefault(s => maxV / s <= 5);
        if (step <= 0) step = Math.Ceiling(maxV / 5);
        double axisMax = Math.Ceiling(maxV / step - 1e-9) * step;

        for (double v = 0; v <= axisMax + 1e-9; v += step)
        {
            double y = T + plotH - v / axisMax * plotH;
            c.Children.Add(new Shapes.Line
            {
                X1 = L, X2 = L + plotW, Y1 = y, Y2 = y,
                Stroke = Br("#22FFFFFF"), StrokeThickness = 1
            });
            var lb = Small(v.ToString("0.##", CultureInfo.InvariantCulture) + (mins ? "m" : "h"));
            lb.Width = L - 8;
            lb.TextAlignment = TextAlignment.Right;
            Put(c, lb, 0, y - 8);
        }
        return axisMax * unit;
    }

    static void XLabels(Canvas c, List<Bucket> d, double L, double slot, double y)
    {
        int k = Math.Max(1, (int)Math.Ceiling(46 / slot));
        for (int i = 0; i < d.Count; i += k)
        {
            var lb = Small(d[i].Label);
            lb.Width = 46;
            lb.TextAlignment = TextAlignment.Center;
            Put(c, lb, L + slot * i + slot / 2 - 23, y);
        }
    }

    void DrawBars(Canvas c, List<Bucket> d, bool anim)
    {
        double W = c.ActualWidth, H = c.ActualHeight;
        double L = 48, T = 10, B = 26, R = 8;
        double plotW = W - L - R, plotH = H - T - B;
        double axisMax = DrawAxes(c, L, T, plotW, plotH, d.Max(b => b.Secs));

        int n = d.Count;
        double slot = plotW / n;
        double bw = Math.Min(44, slot * 0.62);
        var brush = new LinearGradientBrush(Col("#FF7DB4FF"), Col("#FF3B82F6"), 90);
        for (int i = 0; i < n; i++)
        {
            double v = d[i].Secs;
            double h = axisMax > 0 ? v / axisMax * plotH : 0;
            if (v > 0) h = Math.Max(h, 2);
            var r = new Shapes.Rectangle
            {
                Width = bw,
                Height = h,
                RadiusX = 4,
                RadiusY = 4,
                Fill = brush,
                ToolTip = d[i].Label + ": " + Fmt(v)
            };
            if (anim && h > 0)
            {
                var st = new ScaleTransform(1, 0);
                r.RenderTransformOrigin = new Point(0.5, 1);
                r.RenderTransform = st;
                st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(450))
                {
                    BeginTime = TimeSpan.FromMilliseconds(Math.Min(i * 12, 400)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            }
            Put(c, r, L + slot * i + (slot - bw) / 2, T + plotH - h);
        }
        XLabels(c, d, L, slot, H - B + 6);
    }

    void DrawLine(Canvas c, List<Bucket> d, bool anim)
    {
        double W = c.ActualWidth, H = c.ActualHeight;
        double L = 48, T = 10, B = 26, R = 8;
        double plotW = W - L - R, plotH = H - T - B;
        double axisMax = DrawAxes(c, L, T, plotW, plotH, d.Max(b => b.Secs));

        int n = d.Count;
        double slot = plotW / n;
        var pts = new PointCollection();
        for (int i = 0; i < n; i++)
            pts.Add(new Point(L + slot * i + slot / 2, T + plotH - (axisMax > 0 ? d[i].Secs / axisMax * plotH : 0)));

        var area = new PointCollection(pts);
        area.Add(new Point(pts[n - 1].X, T + plotH));
        area.Add(new Point(pts[0].X, T + plotH));
        var poly = new Shapes.Polygon { Points = area, Fill = Br("#333B82F6") };
        var line = new Shapes.Polyline
        {
            Points = pts,
            Stroke = Br("#FF60A5FA"),
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round
        };
        c.Children.Add(poly);
        c.Children.Add(line);
        if (anim)
        {
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600));
            poly.BeginAnimation(OpacityProperty, fade);
            line.BeginAnimation(OpacityProperty, fade);
        }
        for (int i = 0; i < n; i++)
        {
            if (n > 40 && d[i].Secs <= 0) continue;
            var dot = new Shapes.Ellipse
            {
                Width = 8, Height = 8,
                Fill = Br("#FFFFFFFF"),
                Stroke = Br("#FF3B82F6"),
                StrokeThickness = 2,
                ToolTip = d[i].Label + ": " + Fmt(d[i].Secs)
            };
            Put(c, dot, pts[i].X - 4, pts[i].Y - 4);
        }
        XLabels(c, d, L, slot, H - B + 6);
    }

    void DrawPie(Canvas c, Dictionary<string, double> apps, bool anim)
    {
        double W = c.ActualWidth, H = c.ActualHeight;
        double total = apps.Values.Sum();
        if (total <= 0)
        {
            Put(c, Lbl("No app data in this range yet.", 13, dim: true), 20, H / 2 - 10);
            return;
        }

        var ordered = apps.OrderByDescending(k => k.Value).ToList();
        var slices = ordered.Take(7).Select(k => (name: k.Key, secs: k.Value)).ToList();
        double rest = ordered.Skip(7).Sum(k => k.Value);
        if (rest > 0) slices.Add(("Other", rest));

        double size = Math.Min(H - 20, W * 0.5);
        double r = size / 2, cx = r + 16, cy = H / 2;
        double a0 = -90;
        for (int i = 0; i < slices.Count; i++)
        {
            double sweep = slices[i].secs / total * 360;
            string color = Palette[i % Palette.Length];
            UIElement shape;
            if (sweep >= 359.99)
            {
                var e = new Shapes.Ellipse { Width = 2 * r, Height = 2 * r, Fill = Br(color) };
                Canvas.SetLeft(e, cx - r);
                Canvas.SetTop(e, cy - r);
                shape = e;
            }
            else
            {
                double a1 = a0 + sweep;
                Point P(double deg) => new(cx + r * Math.Cos(deg * Math.PI / 180), cy + r * Math.Sin(deg * Math.PI / 180));
                var fig = new PathFigure { StartPoint = new Point(cx, cy), IsClosed = true };
                fig.Segments.Add(new LineSegment(P(a0), true));
                fig.Segments.Add(new ArcSegment(P(a1), new Size(r, r), 0, sweep > 180, SweepDirection.Clockwise, true));
                var geo = new PathGeometry();
                geo.Figures.Add(fig);
                shape = new Shapes.Path { Data = geo, Fill = Br(color), Stroke = Br("#33000000"), StrokeThickness = 1 };
                a0 = a1;
            }
            ((FrameworkElement)shape).ToolTip = $"{slices[i].name}: {Fmt(slices[i].secs)}";
            if (anim) shape.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400)) { BeginTime = TimeSpan.FromMilliseconds(i * 70) });
            c.Children.Add(shape);
        }

        // legend
        double lx = cx + r + 36;
        if (W - lx < 160) return;
        double ly = Math.Max(8, cy - slices.Count * 12);
        for (int i = 0; i < slices.Count; i++)
        {
            var sq = new Shapes.Rectangle { Width = 12, Height = 12, RadiusX = 3, RadiusY = 3, Fill = Br(Palette[i % Palette.Length]) };
            Put(c, sq, lx, ly + i * 24 + 3);
            var t = Lbl($"{slices[i].name}  ·  {Fmt(slices[i].secs)}  ·  {slices[i].secs / total * 100:0}%", 12);
            t.TextWrapping = TextWrapping.NoWrap;
            Put(c, t, lx + 20, ly + i * 24);
        }
    }

    // ------------------------------------------------------------ appearance
    UIElement BuildAppearance()
    {
        var s = _t.Settings;
        var sp = NewPage("Appearance");

        // presets
        void Preset(Action a) { a(); Changed(); Navigate(2); }
        sp.Children.Add(Card(CardTitle("Presets",
            Hint("One click sets many options at once. You can fine-tune everything afterwards."),
            Row(
                Btn("Classic black", () => Preset(() =>
                {
                    s.FontFamily = "Segoe UI"; s.FontSize = 24; s.FontWeight = "Regular"; s.FontColor = "#FFFFFF"; s.ShowSeconds = true;
                    s.BackgroundColor = "#000000"; s.BackgroundOpacity = 1; s.GlassEffect = false; s.CornerPercent = 65;
                    s.PaddingH = 18; s.PaddingV = 8; s.Opacity = 1; s.BorderEnabled = false; s.ShadowEnabled = true; s.ShadowStrength = 50;
                })),
                Btn("Liquid glass", () => Preset(() =>
                {
                    s.FontWeight = "Light"; s.FontColor = "#FFFFFF"; s.BackgroundColor = "#FFFFFF"; s.BackgroundOpacity = 0.28;
                    s.GlassEffect = true; s.CornerPercent = 100; s.BorderEnabled = true; s.BorderColor = "#66FFFFFF"; s.BorderWidth = 1.2;
                    s.ShadowEnabled = true; s.ShadowStrength = 40; s.Opacity = 1;
                })),
                Btn("Light", () => Preset(() =>
                {
                    s.FontWeight = "SemiBold"; s.FontColor = "#111111"; s.BackgroundColor = "#FFFFFF"; s.BackgroundOpacity = 1;
                    s.GlassEffect = false; s.CornerPercent = 40; s.BorderEnabled = false; s.ShadowEnabled = true; s.ShadowStrength = 35; s.Opacity = 1;
                })),
                Btn("Minimal ghost", () => Preset(() =>
                {
                    s.FontWeight = "Light"; s.FontColor = "#FFFFFF"; s.BackgroundColor = "#000000"; s.BackgroundOpacity = 0;
                    s.GlassEffect = false; s.CornerPercent = 0; s.BorderEnabled = false; s.ShadowEnabled = true; s.ShadowStrength = 70; s.Opacity = 1;
                }))))));

        // text
        var fonts = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(x => x).ToList();
        var combo = new ComboBox { Width = 260, ItemsSource = fonts, HorizontalAlignment = HorizontalAlignment.Left };
        combo.SelectedItem = fonts.Contains(s.FontFamily) ? s.FontFamily : null;
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is string f) { s.FontFamily = f; Changed(); }
        };
        string[] weights = { "Light", "Regular", "SemiBold", "Bold" };
        sp.Children.Add(Card(CardTitle("Text",
            Field("Font", combo),
            Field("Weight", Segmented(weights, Math.Max(0, Array.IndexOf(weights, s.FontWeight)), i => { s.FontWeight = weights[i]; Changed(); })),
            SliderField("Size", 12, 96, s.FontSize, v => $"{v:0} px", v => s.FontSize = Math.Round(v)),
            Field("Text color (before any time threshold is reached)", ColorField(s.FontColor, v => { s.FontColor = v; Changed(); }, presets: true)),
            Field("Show seconds (HH:MM:SS instead of HH:MM)", Toggle(s.ShowSeconds, v => { s.ShowSeconds = v; Changed(); })))));

        // background & shape
        sp.Children.Add(Card(CardTitle("Background & shape",
            Field("Background color", ColorField(s.BackgroundColor, v => { s.BackgroundColor = v; Changed(); })),
            SliderField("Background opacity  (0% = fully transparent  ...  100% = solid / opaque)", 0, 100, s.BackgroundOpacity * 100,
                v => $"{v:0}%", v => s.BackgroundOpacity = Math.Round(v) / 100.0),
            Field("Glass gradient (soft highlight on top)", Toggle(s.GlassEffect, v => { s.GlassEffect = v; Changed(); })),
            SliderField("Corner roundness  (0% = square  ...  100% = pill)", 0, 100, s.CornerPercent,
                v => $"{v:0}%", v => s.CornerPercent = Math.Round(v)),
            SliderField("Padding left / right", 0, 60, s.PaddingH, v => $"{v:0} px", v => s.PaddingH = Math.Round(v)),
            SliderField("Padding top / bottom", 0, 40, s.PaddingV, v => $"{v:0} px", v => s.PaddingV = Math.Round(v)),
            SliderField("Overall opacity (text + background)", 20, 100, s.Opacity * 100,
                v => $"{v:0}%", v => s.Opacity = Math.Round(v) / 100.0))));

        // border & shadow
        sp.Children.Add(Card(CardTitle("Border & shadow",
            Field("Border", Toggle(s.BorderEnabled, v => { s.BorderEnabled = v; Changed(); })),
            Field("Border color", ColorField(s.BorderColor, v => { s.BorderColor = v; Changed(); })),
            SliderField("Border width", 0.5, 6, s.BorderWidth, v => $"{v:0.0} px", v => s.BorderWidth = Math.Round(v * 2) / 2),
            Field("Shadow", Toggle(s.ShadowEnabled, v => { s.ShadowEnabled = v; Changed(); })),
            SliderField("Shadow strength", 0, 100, s.ShadowStrength, v => $"{v:0}%", v => s.ShadowStrength = Math.Round(v)))));

        // position
        sp.Children.Add(Card(CardTitle("Position",
            Hint("The timer can only be moved while position editing is on (button \"Edit position\" at the top). Otherwise clicks go straight through to a harmless no-op, so you can't move it by accident. Double-click the timer to hide it; bring it back via the tray icon."),
            Row(Btn("Edit position", ToggleEdit),
                Btn("Reset to top-left", () =>
                {
                    _o.Left = 100; _o.Top = 100;
                    s.OverlayLeft = 100; s.OverlayTop = 100;
                    QueueSave();
                })))));
        return sp;
    }

    // ------------------------------------------------------------ timer & idle
    static bool TryTime(string text, out string normalized)
    {
        normalized = "";
        var m = Regex.Match(text.Trim(), @"^(\d{1,2}):(\d{2})$");
        if (!m.Success) return false;
        int h = int.Parse(m.Groups[1].Value), mi = int.Parse(m.Groups[2].Value);
        if (h > 23 || mi > 59) return false;
        normalized = $"{h:00}:{mi:00}";
        return true;
    }

    UIElement BuildTimer()
    {
        var s = _t.Settings;
        var sp = NewPage("Timer & Idle");

        // idle
        int[] secs = { 0, 30, 60, 120, 300 };
        string[] labels = { "Never", "30 s", "60 s", "2 min", "5 min" };
        int sel = Math.Max(0, Array.IndexOf(secs, s.IdleTimeoutSeconds));
        sp.Children.Add(Card(CardTitle("Idle timeout",
            Hint("If you don't touch keyboard or mouse for this long, the timer stops counting until you're back. \"Never\" counts as long as the PC is on and awake (e.g. for watching videos)."),
            Segmented(labels, sel, i => { s.IdleTimeoutSeconds = secs[i]; Changed(); }))));

        // reset time
        var status = Lbl("✓ saved: resets at " + s.ResetTime, 12, dim: true);
        status.Margin = new Thickness(0, 6, 0, 0);
        TextBox? tb = null;
        bool Commit()
        {
            if (tb != null && TryTime(tb.Text, out var norm))
            {
                s.ResetTime = norm;
                _t.SaveSettings();       // saved immediately
                _t.Refresh();
                status.Text = "✓ saved: resets at " + norm;
                return true;
            }
            status.Text = "Please use the format HH:mm, e.g. 04:00";
            return false;
        }
        tb = Tb(s.ResetTime, 80, text => { if (TryTime(text, out _)) Commit(); else status.Text = "Please use the format HH:mm, e.g. 04:00"; });
        sp.Children.Add(Card(CardTitle("Daily reset time",
            Hint("At this time of day the timer starts again from 00:00:00. Until then it keeps counting across restarts and shutdowns. A night owl can use e.g. 04:00 so late-night sessions still count for the previous day."),
            Row(tb, Btn("Apply", () => Commit(), accent: true)),
            status)));

        // thresholds
        while (s.Thresholds.Count < 2) s.Thresholds.Add(new Threshold { Minutes = 120 * (s.Thresholds.Count + 1), Color = s.Thresholds.Count == 0 ? "#FFD740" : "#FF5252" });
        var th1 = s.Thresholds[0];
        var th2 = s.Thresholds[1];
        UIElement ThRow(string title, Threshold th) => Field(title, Row(
            Lbl("after", 13), new Border { Width = 8 },
            Tb(th.Minutes.ToString(), 60, v => { if (int.TryParse(v, out var m) && m >= 0 && m < 10000) { th.Minutes = m; Changed(); } }),
            Lbl("minutes → color", 13), new Border { Width = 8 },
            ColorField(th.Color, v => { th.Color = v; Changed(); })));
        sp.Children.Add(Card(CardTitle("Warning colors",
            Hint("The timer text changes color once today's screen time passes these limits (120 min = 2 h, 180 min = 3 h)."),
            ThRow("First warning (default yellow)", th1),
            ThRow("Second warning (default red)", th2))));
        return sp;
    }

    // ------------------------------------------------------------ general
    UIElement BuildGeneral()
    {
        var s = _t.Settings;
        var sp = NewPage("General");

        sp.Children.Add(Card(CardTitle("Startup",
            Field("Start with Windows", Toggle(StartupService.IsEnabled, v => StartupService.Set(v))),
            Hint("Note: this remembers the current location of the .exe. If you move or rename it, switch this off and on again."))));

        var apps = Tb(s.FocusApps ?? "", 520, v => { s.FocusApps = v; QueueSave(); });
        sp.Children.Add(Card(CardTitle("Focus rules",
            Hint("Apps (process names, comma separated) that count as \"focus\". The share of your active time in these apps decides the label in the statistics."),
            Field("Focus apps", apps),
            Field("\"Focused\" from (% of active time)", Tb(s.FocusHighPercent.ToString(), 60, v => { if (int.TryParse(v, out var n) && n is >= 0 and <= 100) { s.FocusHighPercent = n; QueueSave(); } })),
            Field("\"Balanced\" from (%) - below that: \"Leisure / distracted\"", Tb(s.FocusLowPercent.ToString(), 60, v => { if (int.TryParse(v, out var n) && n is >= 0 and <= 100) { s.FocusLowPercent = n; QueueSave(); } })))));

        var info = Lbl($"{_t.Days.Count(kv => kv.Value.ActiveSeconds > 0)} days with data · folder: {Storage.Dir}", 12, dim: true);
        info.Margin = new Thickness(0, 10, 0, 0);
        sp.Children.Add(Card(CardTitle("Your data",
            Hint("Everything stays on this PC - one small file per day, kept forever. No accounts, no cloud, no telemetry."),
            Row(Btn("Open data folder", () =>
                {
                    try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + Storage.Dir + "\"")); } catch { }
                }),
                Btn("Export history (CSV)", ExportCsv),
                Btn("Reset today", () =>
                {
                    if (MessageBox.Show("Reset today's timer to zero?", "Screen Time Tracker",
                            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        _t.ResetToday();
                })),
            info)));
        return sp;
    }

    void ExportCsv()
    {
        try
        {
            _t.Save();
            var sb = new StringBuilder("date,active_seconds,active_hh_mm_ss,idle_seconds,top_app\r\n");
            foreach (var kv in _t.Days.OrderBy(k => k.Key))
            {
                if (kv.Value.ActiveSeconds <= 0 && kv.Value.IdleSeconds <= 0) continue;
                string top = kv.Value.Apps.Count == 0 ? "" : kv.Value.Apps.OrderByDescending(a => a.Value).First().Key;
                sb.Append(kv.Key).Append(',')
                  .Append(kv.Value.ActiveSeconds.ToString("0", CultureInfo.InvariantCulture)).Append(',')
                  .Append(FmtClock(kv.Value.ActiveSeconds)).Append(',')
                  .Append(kv.Value.IdleSeconds.ToString("0", CultureInfo.InvariantCulture)).Append(',')
                  .Append('"').Append(top.Replace("\"", "\"\"")).Append('"').Append("\r\n");
            }
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                $"ScreenTime-export-{DateTime.Now:yyyyMMdd-HHmm}.csv");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            MessageBox.Show("Saved to your desktop:\n" + path, "Screen Time Tracker", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Export failed: " + ex.Message, "Screen Time Tracker", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
