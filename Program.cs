using System.Windows;
using Microsoft.Win32;
using ScreenTimeTracker.Services;
using ScreenTimeTracker.Views;

namespace ScreenTimeTracker;

static class Program
{
    static TrackerService _tracker = null!;
    static OverlayWindow _overlay = null!;
    static DashboardWindow? _dash;
    static System.Windows.Forms.NotifyIcon? _tray;

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "ScreenTimeTracker_SingleInstance", out bool created);
        if (!created) return;   // already running

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += (_, _) => Init();
        app.SessionEnding += (_, _) => _tracker?.Save();
        app.Exit += (_, _) => Cleanup();
        app.Run();
    }

    static void Init()
    {
        _tracker = new TrackerService();
        _overlay = new OverlayWindow(_tracker);
        if (_tracker.Settings.OverlayVisible) _overlay.ShowOverlay();
        _tracker.Start();
        BuildTray();
        SystemEvents.PowerModeChanged += OnPower;
    }

    static void OnPower(object? sender, PowerModeChangedEventArgs e)
    {
        // SystemEvents fire on another thread -> hop back to the UI thread
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (e.Mode == PowerModes.Suspend) _tracker.Save();
            else if (e.Mode == PowerModes.Resume) _tracker.SkipGap();
        });
    }

    static void BuildTray()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();

        System.Windows.Forms.ToolStripMenuItem Item(string text, Action action)
        {
            var it = new System.Windows.Forms.ToolStripMenuItem(text);
            it.Click += (_, _) => action();
            return it;
        }

        var pause = Item("Pause Timer", () => { _tracker.Paused = !_tracker.Paused; _tracker.Refresh(); });
        var vis = Item("Hide Overlay", () => { if (_overlay.IsVisible) _overlay.HideOverlay(); else _overlay.ShowOverlay(); });

        var autostart = new System.Windows.Forms.ToolStripMenuItem("Start with Windows") { CheckOnClick = false };
        autostart.Click += (_, _) => StartupService.Set(!StartupService.IsEnabled);

        menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[]
        {
            Item("Open Settings", OpenDashboard),
            pause,
            vis,
            autostart,
            Item("Reset Timer", () =>
            {
                if (MessageBox.Show("Reset today's timer to zero?", "Screen Time Tracker",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    _tracker.ResetToday();
            }),
            new System.Windows.Forms.ToolStripSeparator(),
            Item("Exit", () => { _dash?.Close(); Application.Current.Shutdown(); })
        });
        menu.Opening += (_, _) =>
        {
            pause.Text = _tracker.Paused ? "Resume Timer" : "Pause Timer";
            vis.Text = _overlay.IsVisible ? "Hide Overlay" : "Show Overlay";
            autostart.Checked = StartupService.IsEnabled;
        };

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = AppIcon.TrayIcon() ?? System.Drawing.SystemIcons.Application,
            Text = "Screen Time Tracker",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => OpenDashboard();

        _tracker.Updated += () =>
        {
            if (DateTime.Now.Second % 20 == 0 && _tray != null)
                _tray.Text = "Screen time today: " + UiKit.Fmt(_tracker.Today.ActiveSeconds);
        };
    }

    static void OpenDashboard()
    {
        if (_dash == null)
        {
            _dash = new DashboardWindow(_tracker, _overlay);
            _dash.Closed += (_, _) => _dash = null;
            _dash.Show();
        }
        else
        {
            if (_dash.WindowState == WindowState.Minimized) _dash.WindowState = WindowState.Normal;
            _dash.Activate();
        }
    }

    static void Cleanup()
    {
        SystemEvents.PowerModeChanged -= OnPower;
        _tracker?.Dispose();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
    }
}
