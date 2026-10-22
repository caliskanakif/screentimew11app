SCREEN TIME TRACKER  (WPF, .NET 8, Windows 11, fully offline)

BUILD
  1. Unzip this folder anywhere (e.g. Desktop).
  2. Double-click  "BUILD (small, recommended).bat"
     -> creates the "output" folder with ScreenTimeTracker.exe
  3. Double-click ScreenTimeTracker.exe. The timer appears + a tray icon near the clock.
  If the build shows red errors: copy ALL of the text and send it to Claude.

  "BUILD (standalone, bigger).bat" makes one big exe that needs nothing installed.

USE
  - Tray icon: double-click = settings. Right-click = Open Settings / Pause / Hide / Start with Windows / Reset / Exit.
  - Settings > "Edit position" (top right): drag the timer, position is remembered. "Lock position" ends it.
  - Double-click the timer = hide it (tray icon brings it back).
  - Appearance: presets, font/weight/size/color, show seconds, background color,
    background opacity (transparent <-> solid), glass gradient, corner roundness 0-100 %
    (square -> pill), padding, overall opacity, border, shadow on/off + strength.
  - Timer & Idle: idle timeout, daily reset time (saved immediately), warning colors.
  - Statistics: Day / Week / Month / Year / All, bar / line / pie, per app, averages,
    comparison with the previous period, daily history, focus label.
  - General: start with Windows, focus rules, open data folder, export history as CSV (Desktop).

DATA (all local, %AppData%\ScreenTimeTracker)
  settings.json         all settings incl. daily reset time
  days\yyyy-MM-dd.json  one small file per day - kept forever (years are no problem)
  An old usage.json from an earlier version is converted automatically.
  Today's total is saved every 10 s, on sleep, logoff, shutdown and exit, so after a restart it
  continues until the daily reset time. Time while the PC sleeps or is off is never counted.
