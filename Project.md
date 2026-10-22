# Screen Time Tracker – Projektdokumentation

| | |
|---|---|
| **Stand** | 22.10.2026 |
| **Version** | v2 (WPF, .NET 8) – Funktionsstand vom 18.10.2026 |
| **Plattform** | Windows 11 (Ziel), Windows 10 vermutlich lauffähig, nicht getestet |
| **Zweck** | Persönlicher, lokaler Bildschirmzeit-Tracker für den Windows-PC mit schwebendem Timer (Overlay), Statistik-Dashboard und Tray-Icon |
| **Lizenz / Nutzung** | Nur privat. Kein Code von Dritten außer .NET selbst. Das App-Icon stammt aus einem vom Nutzer bereitgestellten Bild; Rechte daran sind ungeklärt – nicht weitergeben oder veröffentlichen, bevor das geklärt ist. |

---

## Inhalt

1. [Kurzfassung](#1-kurzfassung)
2. [Schnellstart: EXE in 5 Schritten bauen](#2-schnellstart-exe-in-5-schritten-bauen)
3. [Voraussetzungen und Abhängigkeiten](#3-voraussetzungen-und-abhängigkeiten)
4. [EXE erstellen – alle Wege](#4-exe-erstellen--alle-wege)
5. [Fehlerbehebung](#5-fehlerbehebung)
6. [Funktionsumfang](#6-funktionsumfang)
7. [Abgleich mit der ursprünglichen Spezifikation](#7-abgleich-mit-der-ursprünglichen-spezifikation)
8. [Zählregeln und Bedingungen](#8-zählregeln-und-bedingungen)
9. [Daten und Einstellungen](#9-daten-und-einstellungen)
10. [Architektur und Dateien](#10-architektur-und-dateien)
11. [Regeln für Änderungen am Code](#11-regeln-für-änderungen-am-code)
12. [Bekannte Einschränkungen und Risiken](#12-bekannte-einschränkungen-und-risiken)
13. [Teststatus](#13-teststatus)
14. [Verlauf](#14-verlauf)
15. [Ausblick](#15-ausblick)

---

## 1. Kurzfassung

Eine kleine Windows-App, die misst, wie lange du **aktiv** am PC bist (Tastatur/Maus), und das als schwebenden Timer `HH:MM:SS` anzeigt. Dazu gibt es ein Einstellungs- und Statistikfenster und ein Tray-Icon.

- **Kein Internet, keine Konten, keine Telemetrie.** Alles liegt lokal in `%AppData%\ScreenTimeTracker`.
- **Keine NuGet-Pakete.** Nur .NET 8 (WPF + Windows Forms für Tray und Farbdialog).
- **Pro Tag eine Datei, nie gelöscht** → Verlauf über Jahre.
- **Erfasst:** aktive Zeit, Leerlaufzeit, Zeit pro Programm/Spiel (Prozessname), Zeit pro Stunde.
- **Nicht erfasst:** Tastatureingaben, Bildschirminhalte, Fenstertitel, besuchte Webseiten.

**Wichtigste Nachteile vorab:** Der Code wurde nie von mir kompiliert (nur Syntax geprüft). Die EXE ist nicht signiert (SmartScreen-Warnung). Keine automatisierten Tests. Details in [Abschnitt 12](#12-bekannte-einschränkungen-und-risiken).

---

## 2. Schnellstart: EXE in 5 Schritten bauen

1. **.NET SDK installieren** (Version 8 oder neuer; auf dem Entwicklungs-PC ist `10.0.401` installiert): <https://dotnet.microsoft.com/download> → „SDK" → Windows x64.
2. **ZIP entpacken** (`ScreenTimeTracker-v2.zip`), z. B. nach `C:\Users\<Name>\Desktop\ScreenTimeTracker-v2`. Nicht aus dem ZIP heraus starten.
3. **Doppelklick auf `BUILD (small, recommended).bat`.** Beim ersten Mal ist Internet nötig (Pakete werden geladen), dauert 1–3 Minuten.
4. Am Ende steht **„DONE!"** und der Ordner `output` öffnet sich. Steht dort **„BUILD FAILED"**, den roten Text komplett kopieren (siehe [Fehlerbehebung](#5-fehlerbehebung)).
5. **`output\ScreenTimeTracker.exe` doppelklicken.** Timer erscheint, Tray-Icon neben der Uhr (ggf. unter `^` versteckt). Doppelklick aufs Tray-Icon öffnet die Einstellungen.

Die EXE kann danach an einen festen Ort kopiert werden (z. B. `C:\Tools\ScreenTimeTracker\`). Das Projekt wird zum Benutzen nicht mehr gebraucht.

---

## 3. Voraussetzungen und Abhängigkeiten

### 3.1 Systembedingungen

| Bedingung | Anforderung | Anmerkung |
|---|---|---|
| Betriebssystem | **Windows 11 x64** (Ziel). Windows 10 ab Version 1607 wird von .NET 8 unterstützt | Windows 10 mit dieser App nicht getestet. Linux/macOS: **nicht möglich** (WPF ist Windows-only). |
| Prozessor | x64 (Standard). ARM64-Windows: `win-arm64` statt `win-x64` im Build-Befehl | ARM64 nicht getestet |
| Zum Bauen | .NET SDK **8.0 oder neuer** | Im Projekt steht `net8.0-windows`. Ein neueres SDK (z. B. 10.x) baut das ebenfalls, lädt dafür aber beim ersten Mal die .NET-8-Referenzpakete nach. |
| Zum Ausführen („small") | **.NET Desktop Runtime 8 oder neuer** | Beim Installieren des SDK mit dabei. Ohne Runtime erscheint beim Start ein Dialog „You must install .NET Desktop Runtime". |
| Zum Ausführen („standalone") | nichts | Alles ist in der EXE enthalten. |
| Festplatte | SDK ca. 1 GB (einmalig); „small"-EXE wenige MB; „standalone"-EXE nicht gemessen, wahrscheinlich zweistellige bis niedrige dreistellige MB | |
| Rechte | **Kein Administrator nötig** | Autostart schreibt nach `HKCU` (Benutzer), Daten nach `%AppData%`. |
| Anzeige | Der Dashboard-Hintergrund nutzt den Windows-11-Acrylic-Effekt (ab Windows 11 22H2); sonst Fallback auf einen dunklen, halbtransparenten Hintergrund | Unter Windows 10 sieht das Fenster evtl. anders aus |

### 3.2 Software-Abhängigkeiten

| Typ | Was | Wofür |
|---|---|---|
| NuGet-Pakete | **keine** | – |
| Framework | `Microsoft.NETCore.App` 8 (Basis) | Laufzeit |
| Framework | `Microsoft.WindowsDesktop.App` 8 (WPF + Windows Forms) | Fenster, Overlay, Diagramme; Tray-Icon (`NotifyIcon`) und Farbwahl-Dialog (`ColorDialog`) kommen aus Windows Forms |
| Windows-APIs | `user32.dll`: `GetLastInputInfo`, `GetForegroundWindow`, `GetWindowThreadProcessId`, `GetWindowLong`/`SetWindowLong` | Leerlauf-Erkennung, aktive App, Overlay aus Alt+Tab ausblenden |
| Windows-APIs | `dwmapi.dll`: `DwmExtendFrameIntoClientArea`, `DwmSetWindowAttribute` | Dunkle Titelleiste + Acrylic im Dashboard (Fehler werden ignoriert) |
| Registry | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, Wert `ScreenTimeTracker` | „Mit Windows starten" |
| Ereignisse | `Microsoft.Win32.SystemEvents.PowerModeChanged`, `Application.SessionEnding` | Speichern vor Standby/Abmelden, Lücke nach Standby nicht zählen |
| Ressourcen | `app.ico` (im Projektordner, als `ApplicationIcon` und `EmbeddedResource`) | EXE-Icon, Tray-Icon, Fenster-Icon |
| Schrift | „Segoe UI" (Windows-Standard) | Standardschrift. Weitere Schriften wählbar, sofern installiert |

### 3.3 Netzwerk

- **Beim Bauen:** Internet beim ersten Mal nötig (NuGet lädt Targeting-/Runtime-Packs). Danach ist der Build aus dem lokalen Cache auch offline möglich. Der „standalone"-Build braucht zusätzlich die Runtime-Packs (größerer Download).
- **Beim Benutzen:** Die App öffnet **keine** Netzwerkverbindung. Es gibt keinen Netzwerkcode im Projekt.

---

## 4. EXE erstellen – alle Wege

Projektordner (nach dem Entpacken):

```
ScreenTimeTracker-v2/
├─ BUILD (small, recommended).bat
├─ BUILD (standalone, bigger).bat
├─ README.txt
├─ ScreenTimeTracker.csproj
├─ app.ico
├─ Program.cs
├─ Models/Models.cs
├─ Services/  (AppIcon, NativeMethods, StartupService, Storage, TrackerService)
└─ Views/     (DashboardWindow, OverlayWindow, UiKit)
```

### 4.1 Weg A – „small" per Doppelklick (empfohlen)

`BUILD (small, recommended).bat` führt aus:

```bat
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o "%~dp0output"
```

Ergebnis: `output\ScreenTimeTracker.exe` (eine Datei, klein, braucht die installierte .NET Desktop Runtime).

### 4.2 Weg B – „standalone" per Doppelklick

`BUILD (standalone, bigger).bat` führt aus:

```bat
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o "%~dp0output-standalone"
```

Ergebnis: `output-standalone\ScreenTimeTracker.exe` (eine Datei, läuft auf jedem Windows-x64-PC ohne Installation).

### 4.3 Weg C – manuell in PowerShell

```powershell
cd "C:\Pfad\zu\ScreenTimeTracker-v2"      # Ordner, in dem ScreenTimeTracker.csproj liegt
dotnet --list-sdks                         # prüfen: mind. 8.x vorhanden
dotnet restore                             # Pakete laden
dotnet build -c Release                    # nur kompilieren (Fehler anzeigen)
dotnet run -c Release                      # direkt starten, ohne EXE zu erzeugen
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o output
```

Hinweis: Befehle in die **Konsole** eingeben, nicht C#-Code. Der Ordner muss die `.csproj` enthalten, sonst meldet `dotnet` „MSB1003".

### 4.4 Weg D – Visual Studio

1. Visual Studio 2022 oder neuer mit Workload **„.NET-Desktopentwicklung"** (bei .NET-10-SDK ggf. die neueste Version).
2. *Datei → Öffnen → Projekt/Projektmappe* → `ScreenTimeTracker.csproj`.
3. Oben **Release** wählen, `F5` startet, `Strg+Umschalt+B` baut.
4. Für eine einzelne EXE: Rechtsklick aufs Projekt → *Veröffentlichen* → *Ordner* → Zielruntime `win-x64`, Bereitstellungsmodus „Framework-abhängig", „Einzelne Datei" aktivieren. Einfacher ist Weg A.

### 4.5 Vergleich „small" und „standalone"

| | small | standalone |
|---|---|---|
| Dateigröße | klein (wenige MB) | deutlich größer (nicht gemessen, zweistellige bis niedrige dreistellige MB) |
| Braucht .NET Desktop Runtime | **ja** (8 oder neuer) | nein |
| Startzeit | schneller | langsamer beim ersten Start (entpackt Teile nach `%TEMP%`) |
| Updates der .NET-Runtime | über Windows/Installer | fest eingebaut, ändert sich nie |
| Empfehlung | Standard auf dem eigenen PC | zum Weitergeben an PCs ohne .NET |

### 4.6 Die Projektdatei erklärt

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>                 <!-- Fenster-App, kein Konsolenfenster -->
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>         <!-- nur für Tray-Icon + Farbdialog -->
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>ScreenTimeTracker</AssemblyName>
    <RootNamespace>ScreenTimeTracker</RootNamespace>
    <RollForward>Major</RollForward>                <!-- läuft auch mit neuerer Runtime (z. B. nur .NET 10 installiert) -->
    <SatelliteResourceLanguages>en</SatelliteResourceLanguages>  <!-- keine unnötigen Sprachordner -->
    <ApplicationIcon>app.ico</ApplicationIcon>
  </PropertyGroup>

  <!-- Verhindert die Namenskonflikte (Application, Color, Brush, FontFamily, MessageBox ...) zwischen WPF und WinForms -->
  <ItemGroup>
    <Using Remove="System.Windows.Forms" />
    <Using Remove="System.Drawing" />
  </ItemGroup>

  <ItemGroup>
    <EmbeddedResource Include="app.ico" LogicalName="app.ico" />
  </ItemGroup>
</Project>
```

### 4.7 Erster Start – Prüfliste

- [ ] Timer erscheint oben links (Position 100/100) und läuft hoch.
- [ ] Tray-Icon sichtbar (ggf. im `^`-Menü). Doppelklick öffnet die Einstellungen.
- [ ] *Edit position* (oben rechts im Fenster) → Timer lässt sich ziehen; *Lock position* beendet das.
- [ ] Doppelklick auf den Timer blendet ihn aus; Tray → *Show Overlay* holt ihn zurück.
- [ ] *Pause* stoppt den Zähler (Timer wird blasser).
- [ ] 60 s nichts anfassen → *Overview* zeigt „Idle", Leerlaufzeit steigt, Timer steht.
- [ ] App beenden (Tray → *Exit*) und neu starten → Zeit ist wieder da.
- [ ] *Timer & Idle* → Reset-Zeit ändern, App neu starten → Wert bleibt.
- [ ] *General* → *Start with Windows* an, abmelden/anmelden → App startet von selbst.
- [ ] *Appearance* → Regler für Ecken, Hintergrund-Deckkraft und Schatten verändern den Timer sofort.
- [ ] Standby testen: Rechner 2 Minuten schlafen lassen → die 2 Minuten dürfen **nicht** in der Zeit stehen.

### 4.8 Aktualisieren, verschieben, deinstallieren

- **Aktualisieren:** Zuerst Tray → *Exit*, dann neue EXE drüberkopieren. Einstellungen und Daten bleiben (liegen in `%AppData%`).
- **Verschieben:** Wenn *Start with Windows* aktiv ist, merkt sich Windows den **alten Pfad**. Nach dem Verschieben Autostart aus- und wieder einschalten.
- **Deinstallieren:** 1) *Start with Windows* ausschalten. 2) App beenden, EXE löschen. 3) Optional Daten löschen: Ordner `%AppData%\ScreenTimeTracker`. 4) Falls Autostart nicht sauber ausgeschaltet wurde: in `regedit` unter `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` den Wert `ScreenTimeTracker` löschen.

### 4.9 Installer

Es gibt **keinen** Installer. Die EXE ist portabel. Wer einen möchte: Inno Setup (kostenlos) oder MSIX wären Optionen; nicht umgesetzt, und ohne Code-Signatur zeigt Windows trotzdem eine Warnung.

---

## 5. Fehlerbehebung

### 5.1 Build-Fehler (tatsächlich aufgetreten)

| Meldung | Ursache | Lösung |
|---|---|---|
| `CS0103: The name 'Path' / 'File' / 'Directory' does not exist` | In WPF-Projekten sind `System.IO` & Co. nicht automatisch importiert | `using System.IO;` an den Dateianfang. In v2 bereits in `Storage.cs` und `AppIcon.cs` behoben. |
| `CS0246: The type or namespace name 'Stream' could not be found` | dasselbe | dasselbe (`AppIcon.cs`, behoben) |
| `CS0104: 'Application' / 'Color' / 'Brush' / 'FontFamily' / 'MessageBox' is an ambiguous reference` | `UseWindowsForms` + `ImplicitUsings` importieren WinForms/Drawing global, WPF hat dieselben Namen | In der `.csproj`: `<Using Remove="System.Windows.Forms" />` und `<Using Remove="System.Drawing" />` (steht in v2 drin). WinForms-Typen dann immer voll ausschreiben, z. B. `System.Windows.Forms.NotifyIcon`. |
| `MSB1003: Specify a project or solution file` | Konsole steht im falschen Ordner | `cd` in den Ordner mit der `.csproj` |
| `NETSDK1100` / „targeting pack not found" | Kein Internet beim ersten Restore | Internet an, `dotnet restore` wiederholen |
| `project.assets.json not found` | Restore lief nicht | `dotnet restore` |

### 5.2 Start-Probleme

| Symptom | Mögliche Ursache | Lösung |
|---|---|---|
| Doppelklick: nichts passiert | Läuft bereits (unsichtbar). Die App erlaubt nur **eine** Instanz und beendet eine zweite lautlos | Task-Manager (`Strg+Umschalt+Esc`) → `ScreenTimeTracker` beenden, neu starten |
| Dialog „You must install .NET Desktop Runtime" | „small"-Build ohne Runtime | .NET Desktop Runtime installieren oder „standalone" bauen |
| Windows: „Der Computer wurde geschützt" (SmartScreen) | EXE ist nicht signiert | *Weitere Informationen* → *Trotzdem ausführen* |
| Antivirus meldet die EXE | Unsignierte, selbst gebaute EXE (Fehlalarm möglich) | Ordner als Ausnahme eintragen. Die App liest keine Tastatureingaben (nur Zeit seit der letzten Eingabe). |
| Kein Timer sichtbar | a) per Doppelklick ausgeblendet, b) außerhalb des Bildschirms, c) Overlay aus | Tray → *Show Overlay*; Einstellungen → *Appearance* → *Reset to top-left* |
| Tray-Icon fehlt | Windows versteckt es im `^`-Bereich | Icon in die Taskleiste ziehen |
| Timer lässt sich nicht verschieben | Edit-Modus aus | Einstellungen → *Edit position* |
| EXE-Icon im Explorer alt/falsch | Windows-Icon-Cache | Neu anmelden oder Datei umbenennen |
| Einstellungen/Daten defekt | Datei beschädigt (z. B. Stromausfall beim Schreiben) | Die App legt `*.corrupt`-Kopien an und startet mit Standardwerten. Notfalls Ordner `%AppData%\ScreenTimeTracker` sichern und `settings.json` löschen. |

---

## 6. Funktionsumfang

### 6.1 Overlay (schwebender Timer)

- Rahmenloses, transparentes Fenster, immer im Vordergrund, nicht in Taskleiste/Alt+Tab, nimmt keinen Fokus.
- Zeigt nur `HH:MM:SS` (optional `HH:MM`).
- Standard-Look: schwarzes, abgerundetes Rechteck, weiße Schrift (Segoe UI, normal), weicher Schatten.
- **Verschieben nur im Edit-Modus** (Button im Dashboard). Position wird gespeichert und beim Start auf einen sichtbaren Bereich begrenzt.
- **Doppelklick blendet aus.** Zurückholen über Tray oder Dashboard.
- Textfarbe wechselt bei Schwellen (Standard: ab 120 min gelb, ab 180 min rot), weiche 0,5-s-Überblendung.
- Bei Pause wird der Text blasser.

### 6.2 Dashboard (5 Seiten)

| Seite | Inhalt |
|---|---|
| **Overview** | Aktive Zeit heute (groß), Status (Zählt / Pause / Idle + aktuelle App), Leerlaufzeit heute, gestern, Ø der letzten 7 Tage, Top-Apps heute, Focus-Label |
| **Statistics** | Zeitraum **Day / Week / Month / Year / All**, Diagramm **Bar / Line / Pie** (Pie = Zeit pro App), Summe, Ø pro aktivem Tag, bester Tag, Vergleich zum vorherigen Zeitraum, App-Liste mit Prozent, Focus, Tagesverlauf (letzte 60 Tage, Gesamtanzahl wird angezeigt) |
| **Appearance** | Presets (*Classic black, Liquid glass, Light, Minimal ghost*), Schriftart, Stärke, Größe, Textfarbe, Sekunden an/aus, Hintergrundfarbe, **Hintergrund-Deckkraft 0–100 %**, Glas-Verlauf an/aus, **Eckenradius 0–100 % (eckig → Pille)**, Innenabstände, Gesamt-Deckkraft, Rahmen (an/aus, Farbe, Breite), **Schatten (an/aus, Stärke)**, Position |
| **Timer & Idle** | Leerlauf-Grenze (*Never, 30 s, 60 s, 2 min, 5 min*), **tägliche Reset-Uhrzeit** (wird sofort gespeichert), zwei Warnfarben mit Minutenwerten |
| **General** | Start mit Windows, Focus-Regeln, Datenordner öffnen, **CSV-Export** (auf den Desktop), *Reset today* |

Obere Leiste (immer sichtbar): *Edit position / Lock position*, *Pause / Resume*, *Hide timer / Show timer*.

### 6.3 Tray-Menü

*Open Settings* · *Pause Timer / Resume Timer* · *Hide Overlay / Show Overlay* · *Start with Windows* (Haken) · *Reset Timer* (mit Rückfrage) · *Exit*. Doppelklick aufs Icon = Einstellungen.

---

## 7. Abgleich mit der ursprünglichen Spezifikation

Quelle: die Spezifikation vom 09.10.2026 („Windows Screen Time Overlay") und die späteren Wünsche aus dem Chat. ✅ erfüllt · ⚠️ teilweise/abweichend · ❌ nicht umgesetzt.

| Anforderung | Status | Anmerkung |
|---|---|---|
| Timer `HH:MM:SS`, keine Labels | ✅ | |
| Nur aktive Nutzung zählen, Pause bei Leerlauf, automatische Fortsetzung | ✅ | Siehe [Abschnitt 8](#8-zählregeln-und-bedingungen) |
| Immer im Vordergrund, transparent, Pille, schwarz-transparent | ✅ | Standard-Look nach Screenshot; Eckenradius per Regler |
| Konfigurierbares Design | ✅ | Siehe Appearance |
| Getrennte App für Details | ⚠️ | Zweites **Fenster** im selben Prozess, keine zweite EXE. Schließen des Fensters stoppt das Tracking nicht. |
| Verschieben nur nach Klick auf Edit | ✅ | |
| Position merken | ✅ | |
| Zeit über den Tag speichern, auch bei PC aus/an | ✅ | Speicherung als **Tagessumme**, nicht als einzelne Sitzungen mit Start/Ende (⚠️ abweichend vom Wortlaut „each session") |
| Idle-Zeit separat zählen | ✅ | |
| Schrift, Größe, Farbe, Deckkraft | ✅ | |
| „Liquid Glass" | ⚠️ | Verlauf + Lichtkante + Schatten. **Kein echter Hintergrund-Blur** beim Overlay (WPF kann das bei transparenten Fenstern nicht sauber). Dashboard nutzt Windows-Acrylic. |
| Farbschwellen (Standard 2 h gelb, 3 h rot) | ✅ | Oberfläche zeigt zwei Schwellen; das Datenmodell kann mehr |
| Tägliche Reset-Zeit, gespeichert | ✅ | |
| Leerlauf-Grenze konfigurierbar, Windows-API | ✅ | `GetLastInputInfo`; Auswahl 30 s/60 s/2 min/5 min/Never |
| Einstellungen automatisch merken | ✅ | `settings.json` |
| Tray mit Open Settings/Pause/Reset/Exit | ✅ | plus Hide/Show und Autostart |
| Start mit Windows | ✅ | |
| Offline, keine Telemetrie/Konto/Werbung | ✅ | |
| Native, sehr leicht, wenig RAM/CPU | ⚠️ | .NET/WPF, **nicht** nativ (C++). Verbrauch nicht gemessen. 1 Tick pro Sekunde, kaum CPU. |
| Durchschnitt, Prozente, Focus-Labels | ✅ | Focus = einfache, einstellbare Regel (siehe [8.5](#85-focus-label)) |
| Tages/Wochen/Monatsstatistik als Bar/Pie/Line | ✅ | zusätzlich Jahr und Gesamt |
| Zeit pro Spiel/App | ✅ | über Prozessnamen |
| Ein-/Ausblenden per Doppelklick | ✅ | |
| Animationen | ⚠️ | Dezent: Einblenden, Balken wachsen, Farbüberblendung. Keine aufwendigen Effekte. |
| „Opaque app design" | ⚠️ | Unklar formuliert; Dashboard ist dunkel/halbtransparent, Overlay-Hintergrund von 0 bis 100 % deckend einstellbar |
| Hintergrund des Overlays deckend einstellbar | ✅ | Regler „Background opacity" |
| Eckenradius-Regler (eckig bis Pille) | ✅ | Regler „Corner roundness" |
| Schatten an/aus | ✅ | plus Stärke |
| App-Icon (lila Stoppuhr) | ✅ | EXE, Tray, Fenster |
| Verlauf „bis zu Jahren/lebenslang" | ✅ | Eine Datei pro Tag, nie gelöscht |
| **SQLite** als Speicher | ❌ | Bewusst **JSON pro Tag** (keine Abhängigkeit). Für Jahre an Tagesdaten ausreichend, für sehr viele Detailabfragen wäre SQLite besser. |
| **MVVM** | ❌ | Oberfläche ist in C#-Code gebaut (kein XAML, keine ViewModels). Bewusst, um Build-Fehler gering zu halten. Weniger „sauber", dafür weniger Dateien. |
| „Nur Windows 11" | ⚠️ | Läuft vermutlich auch auf Windows 10, getestet ist keins von beiden |
| Verschiedene Themes im Dashboard | ❌ | Dashboard hat ein festes dunkles Design |

---

## 8. Zählregeln und Bedingungen

### 8.1 Takt und Lückenschutz

Ein Timer läuft **jede Sekunde** (`DispatcherTimer` im UI-Thread). Bei jedem Tick wird die **tatsächlich verstrichene Zeit** (`delta`) seit dem letzten Tick gemessen. Ist `delta ≤ 0` oder `delta > 3 s`, wird dieser Tick **nicht gezählt**. Das deckt ab:

- Standby/Ruhezustand (Rechner schlief)
- Hänger des Programms
- Zeitumstellung oder Uhr-Sprünge

Zusätzlich: bei *Suspend* wird gespeichert, bei *Resume* die Zeitbasis zurückgesetzt.

### 8.2 Aktiv oder Leerlauf

1. `idleSeconds` = Zeit seit der letzten Tastatur-/Mausaktion (Windows `GetLastInputInfo`).
2. **Leerlauf**, wenn Grenze > 0 **und** `idleSeconds ≥ Grenze`. Grenze „Never" = nie Leerlauf.
3. Ist **nicht pausiert**:
   - Leerlauf → `IdleSeconds += delta`
   - sonst → `ActiveSeconds += delta`, `Hours[aktuelle Stunde] += delta`, `Apps[Prozessname] += delta`
4. Ist **pausiert**: weder aktive noch Leerlaufzeit wird gezählt.

Folgen:
- Die ersten *Grenze* Sekunden einer Pause (z. B. 60 s) zählen noch als **aktiv**. Das ist ein bewusst in Kauf genommener Fehler von bis zu „Grenze" pro Pause.
- **Videos/Filme schauen ohne Eingabe** zählt als Leerlauf, außer die Grenze steht auf *Never*. Dann zählt aber auch alles andere (z. B. Rechner läuft, du bist weg).
- Wird der Bildschirm gesperrt oder per Remote-Desktop gearbeitet, ist das Verhalten **nicht getestet**.

### 8.3 Aktive App

- Nur wenn nicht im Leerlauf: Prozessname des **Vordergrundfensters** (`GetForegroundWindow` → Prozess-ID → Prozessname, z. B. `chrome`, `Code`, `eldenring`).
- Der Name wird pro Prozess-ID 60 s zwischengespeichert (spart CPU).
- Kein Fenstertitel, keine URL. Zwei Browser-Tabs sind für die App derselbe Eintrag.
- Geschützte oder beendete Prozesse können als `Unknown` erscheinen. Windows-Store-Apps erscheinen teils als `ApplicationFrameHost`.
- Hintergrundnutzung (z. B. Musik im Hintergrund) wird **nicht** erfasst.

### 8.4 Tagesgrenze und Reset-Zeit

- **Tracking-Tag** = `(jetzt − Reset-Zeit).Datum`, gespeichert als `yyyy-MM-dd`.
- Beispiel Reset `04:00`: Nutzung um 02:00 Uhr nachts gehört noch zum **Vortag**. Um 04:00 beginnt ein neuer Tag bei `00:00:00`.
- **Reset-Zeit steht in `settings.json`** und wird beim Tippen eines gültigen Wertes (`HH:mm`) sofort gespeichert.
- ⚠️ **Ändert man die Reset-Zeit mitten am Tag**, kann „heute" auf den Vortag umspringen (z. B. von `00:00` auf `04:00` um 02:00 Uhr). Der Timer zeigt dann den Stand des Vortags. Nichts geht verloren; es wird nur ein anderer Tagesdatensatz angezeigt. Daher: Reset-Zeit besser nicht kurz vor/nach der Umstellungszeit ändern.
- Die Stundenwerte (`Hours`) nutzen die **Uhrzeit**, nicht die Position im Tracking-Tag. Im Tages-Diagramm erscheinen Stunden vor der Reset-Zeit am Ende des Vortags.

### 8.5 Focus-Label

Aktive Zeit sagt nur „am PC", nicht „konzentriert". Das Label ist deshalb eine **offene, einstellbare Regel**:

- *Focus apps* (Prozessnamen, kommagetrennt, Groß/Klein egal).
- Anteil der aktiven Zeit in diesen Apps ≥ **60 %** → „Focused", ≥ **30 %** → „Balanced", darunter → „Leisure / distracted". Schwellen einstellbar.
- Mindestens 60 s Daten nötig, sonst „Not enough data yet".
- Standardliste (geraten, bitte anpassen): `Code, devenv, rider64, pycharm64, idea64, WINWORD, EXCEL, POWERPNT, notepad, obsidian, Notion`. Die Prozessnamen deiner Programme siehst du in der App-Liste der Statistik.

### 8.6 Speichern und Wiederherstellen

| Wann | Was |
|---|---|
| alle 10 Sekunden | `settings.json` + Tagesdatei(en) mit Änderungen |
| Einstellung geändert | `settings.json` nach ca. 0,5 s (Reset-Zeit sofort) |
| Standby, Abmelden, Herunterfahren, *Exit* | alles |
| Start | alle Tagesdateien laden; „heute" läuft mit dem gespeicherten Stand weiter |

Bei hartem Absturz/Stromausfall gehen **höchstens ca. 10 Sekunden** verloren. Dateien werden über eine temporäre Datei geschrieben (`.tmp` → ersetzen), damit sie nicht halb geschrieben zurückbleiben.

### 8.7 Weitere Bedingungen

- **Eine Instanz:** Ein zweiter Start beendet sich lautlos (Mutex `ScreenTimeTracker_SingleInstance`).
- **Dashboard schließen** ≠ App beenden. Beenden nur über Tray → *Exit*.
- *Reset Timer / Reset today* löscht den **ganzen** heutigen Datensatz (aktiv, Leerlauf, Apps, Stunden).
- Die App muss laufen, um zu zählen. Ist sie beendet, wird nichts nachgetragen.

---

## 9. Daten und Einstellungen

### 9.1 Speicherort

`%AppData%\ScreenTimeTracker` (also `C:\Users\<Name>\AppData\Roaming\ScreenTimeTracker`). Öffnen: *General → Open data folder*.

```
ScreenTimeTracker/
├─ settings.json            alle Einstellungen
└─ days/
   ├─ 2026-10-18.json       eine Datei pro Tracking-Tag
   ├─ 2026-10-19.json
   └─ ...
```

Ein älteres `usage.json` aus der ersten v2-Version wird beim ersten Start automatisch in Tagesdateien umgewandelt und danach in `usage.json.migrated` umbenannt.

### 9.2 Tagesdatei (Beispiel)

```json
{
  "ActiveSeconds": 7650.3,
  "IdleSeconds": 412.0,
  "Apps": { "chrome": 3100.2, "Code": 2800.0, "steam": 1750.1 },
  "Hours": [0,0,0,0,0,0,0,0,0,1200.5,3000,3449.8,0,0,0,0,0,0,0,0,0,0,0,0]
}
```

`Hours` hat immer 24 Werte (Sekunden pro Uhrzeit-Stunde).

### 9.3 Einstellungen (`settings.json`)

| Schlüssel | Standard | Bedeutung |
|---|---|---|
| `IdleTimeoutSeconds` | 60 | Leerlauf-Grenze in Sekunden; 0 = nie |
| `ResetTime` | "00:00" | Tägliche Reset-Uhrzeit `HH:mm` |
| `FontFamily` | "Segoe UI" | Schriftart |
| `FontSize` | 24 | Schriftgröße (12–96 im Regler) |
| `FontWeight` | "Regular" | Light / Regular / SemiBold / Bold |
| `FontColor` | "#FFFFFF" | Textfarbe unterhalb aller Schwellen |
| `ShowSeconds` | true | `HH:MM:SS` statt `HH:MM` |
| `BackgroundColor` | "#000000" | Hintergrundfarbe der Pille |
| `BackgroundOpacity` | 1.0 | 0 = durchsichtig … 1 = deckend |
| `GlassEffect` | false | Weicher Glas-Verlauf |
| `CornerPercent` | 65 | 0 = eckig … 100 = Pille |
| `PaddingH` / `PaddingV` | 18 / 8 | Innenabstand in Pixel |
| `Opacity` | 1.0 | Gesamt-Deckkraft (0,2–1) |
| `BorderEnabled` / `BorderColor` / `BorderWidth` | false / "#55FFFFFF" / 1.2 | Rahmen |
| `ShadowEnabled` / `ShadowStrength` | true / 50 | Schatten an/aus, Stärke 0–100 |
| `Thresholds` | `[{120,"#FFD740"},{180,"#FF5252"}]` | Warnfarben: ab *Minutes* gilt *Color* (höchste erreichte gewinnt) |
| `OverlayVisible` | true | Overlay sichtbar |
| `OverlayLeft` / `OverlayTop` | 100 / 100 | Position |
| `StatsRange` | "Week" | Day / Week / Month / Year / All |
| `StatsChart` | "Bar" | Bar / Line / Pie |
| `FocusApps` | siehe 8.5 | Focus-Programme |
| `FocusHighPercent` / `FocusLowPercent` | 60 / 30 | Focus-Schwellen |

Hinweis: Werte nur bearbeiten, wenn die App **beendet** ist, sonst überschreibt sie die Datei.

### 9.4 Sichern, übertragen, exportieren

- **Backup:** Ordner `%AppData%\ScreenTimeTracker` kopieren. Zurückspielen = Ordner an dieselbe Stelle legen (App vorher beenden).
- **Auf anderen PC:** Ordner kopieren, dort dieselbe EXE starten.
- **CSV:** *General → Export history (CSV)* legt `ScreenTime-export-<Datum>.csv` auf den Desktop (Spalten: Datum, aktive Sekunden, `hh:mm:ss`, Leerlauf-Sekunden, meistgenutzte App; UTF-8 mit BOM für Excel).
- **Kein Cloud-Sync.** Wer mehrere Rechner will, muss den Ordner selbst abgleichen.

---

## 10. Architektur und Dateien

```
Program.cs                 Start, Einzelinstanz, Tray, Stromsparmodus-Ereignisse, Beenden
Models/Models.cs           AppSettings, Threshold, DayRecord
Services/TrackerService    Tracking-Engine (1-s-Takt), Tagesgrenze, Speichern
Services/Storage           JSON lesen/schreiben, Tagesdateien, Migration, .corrupt-Sicherung
Services/NativeMethods     Windows-APIs: Leerlauf, Vordergrund-App, Fensterstil, Acrylic
Services/StartupService    Autostart (Registry HKCU\...\Run)
Services/AppIcon           Icon aus eingebetteter app.ico (Tray + Fenster)
Views/OverlayWindow        Schwebender Timer, Aussehen aus den Einstellungen
Views/DashboardWindow      5 Seiten, Diagramme (Balken/Linie/Torte) als WPF-Shapes
Views/UiKit                Hilfsfunktionen für Buttons, Regler, Farbfelder, Karten
```

Wichtige Entwurfsentscheidungen:

- **Die Engine ist unabhängig von der Oberfläche.** `TrackerService` läuft ständig; Overlay und Dashboard lesen nur und reagieren auf das Ereignis `Updated`.
- **Kein XAML.** Die Oberfläche wird in C# aufgebaut. Vorteil: weniger Dateien, weniger Build-Fehlerquellen. Nachteil: schwerer zu gestalten/zu lesen als XAML; kein Designer.
- **Diagramme ohne Bibliothek.** Gezeichnet mit `Canvas` + `Shapes`. Vorteil: keine Abhängigkeit. Nachteil: begrenzt (keine Achsentitel, keine Zoom-/Hover-Details außer Tooltips).
- **Speicherung:** pro Tag eine JSON-Datei; nur die geänderte (heutige) wird zurückgeschrieben.
- **Overlay:** `AllowsTransparency` + Schatten-Effekt; Eckenradius = Prozentsatz der halben Höhe.

---

## 11. Regeln für Änderungen am Code

Aus den bisherigen Build-Fehlern gelernt. Wer den Code erweitert (auch eine KI), sollte diese Punkte einhalten:

1. **Jede Datei mit `File`, `Path`, `Directory`, `Stream`, `StreamReader` braucht `using System.IO;`.** Bei WPF-Projekten ist das nicht automatisch importiert.
2. **WinForms und Drawing nur voll qualifiziert** verwenden (`System.Windows.Forms.NotifyIcon`, `System.Drawing.Icon`). Die globalen Usings sind in der `.csproj` bewusst entfernt.
3. **Nicht `using System.Windows.Shapes;`** schreiben (kollidiert mit `System.IO.Path`). Stattdessen Alias: `using Shapes = System.Windows.Shapes;`.
4. **Keine NuGet-Pakete** hinzufügen, ohne es zu besprechen (Ziel: null Abhängigkeiten).
5. **Datumsformat der Tagesdateien nicht ändern:** `yyyy-MM-dd`. Der Verlauf hängt daran.
6. **Neue Einstellungen** nur mit Standardwert in `AppSettings` ergänzen, damit alte `settings.json`-Dateien weiter funktionieren.
7. **Nach jeder Änderung auf Windows bauen** (`dotnet build -c Release`). Ohne Windows-Rechner lässt sich WPF nicht prüfen; reine Syntaxprüfung reicht nicht (siehe Abschnitt 13).
8. Bei Änderungen am Zählen: die Lückenregel (`delta > 3 s`) und das Speichern vor Standby beibehalten.

---

## 12. Bekannte Einschränkungen und Risiken

Ehrliche Liste, nach Wichtigkeit:

1. **Nie kompiliert von der Entwicklungsumgebung aus.** Zwei Build-Läufe hatten bisher je einen kleinen Fehler (fehlendes `using`). Weitere Fehler beim nächsten Build sind möglich. Ob der Build mit der letzten Korrektur (`AppIcon.cs`) erfolgreich war, ist **nicht bestätigt**.
2. **Funktionen sind teils ungetestet:** Autostart, Standby-Verhalten, Jahres-/Gesamt-Statistik, Pie-Chart mit vielen Apps, Verhalten bei mehreren Monitoren/anderer Skalierung, Windows 10.
3. **Unsigniert:** SmartScreen-/Antivirus-Warnung möglich. Ein Code-Signing-Zertifikat kostet Geld.
4. **Genauigkeit:** Aktiv/Leerlauf wird nur aus Tastatur/Maus abgeleitet. Lesen, Videos, Controller-Spiele (Gamepad-Eingaben setzen die Leerlauf-API in der Regel **nicht** zurück) können fälschlich als Leerlauf gelten. Pro Pause zählen bis zu *Grenze* Sekunden zu viel als aktiv.
5. **App-Erkennung nur über Prozessnamen:** keine Unterscheidung von Webseiten, Launcher-Spiele erscheinen manchmal unter dem Launcher-Namen, `Unknown`/`ApplicationFrameHost` möglich.
6. **„Liquid Glass" ist eine Annäherung:** kein echter Unschärfe-Effekt hinter dem Overlay.
7. **Reset-Zeit mitten am Tag ändern** kann „heute" kurz auf den Vortag umschalten (siehe 8.4).
8. **Bei hartem Absturz** bis zu ca. 10 s Verlust.
9. **JSON-Speicher:** Bei sehr vielen Jahren sind Statistik-Ladezeiten noch kein Problem (eine kleine Datei pro Tag), aber komplexe Abfragen wären mit SQLite bequemer.
10. **Aussehen der Standard-Steuerelemente:** Die Schriftart-Auswahlliste (ComboBox) und Textfelder nutzen teils Windows-Standardlook und passen optisch nicht perfekt zum dunklen Design.
11. **Kein Installer, keine Auto-Updates, keine Tests.**
12. **Das App-Icon** stammt aus einem vom Nutzer bereitgestellten Bild; Rechte unklar (nur privat nutzen).
13. **Quellcode nur im Chat-ZIP.** Der Code liegt in `ScreenTimeTracker-v2.zip` aus der Unterhaltung vom 18.10.2026, **nicht** im Claude-Projekt und nicht in einem Git-Repository. Geht das ZIP verloren, ist der Code weg (diese Datei beschreibt ihn nur).

---

## 13. Teststatus

| Prüfung | Ergebnis |
|---|---|
| Syntaxprüfung aller `.cs`-Dateien mit einem C#-Parser (tree-sitter) | ✅ keine Syntaxfehler (zuletzt vor dem `AppIcon`-Fix) |
| Kompilieren auf Windows | Erster v2-Build: 9 Fehler (`Storage.cs`), behoben. Danach zwischenzeitlich „Can't run the exe" (Ursache nicht geklärt), später vom Nutzer „It works" gemeldet. Neuer Build mit zusätzlichen Funktionen: 1 Fehler (`AppIcon.cs`), behoben. **Danach keine Rückmeldung** |
| Funktionstest durch Nutzer | Vorversion: lief. Funktionsumfang der aktuellen Version: nicht bestätigt |
| Automatisierte Tests | keine |
| Messung RAM/CPU/Startzeit | nicht durchgeführt |

Warum nicht von hier aus gebaut: Der WPF-Build braucht Windows (`net8.0-windows`) und das .NET SDK; in der Entwicklungsumgebung stand beides nicht zur Verfügung.

---

## 14. Verlauf

| Datum | Stand |
|---|---|
| 09.–10.10.2026 | Ursprüngliche Spezifikation. KI-generiertes Starterprojekt (WPF + WinForms) mit 17+ Build-Fehlern (fehlende `using System.IO`, Namenskonflikte Application/Color/Brush/FontFamily/MessageBox, Tippfehler `tray`). Ohne bestätigten Erfolg abgebrochen und durch eine Neuentwicklung ersetzt. |
| 18.10.2026 | **v1:** minimale WinForms-Version (Timer, Tray, Idle, 1 Datei). Danach **v2:** komplette Neuentwicklung in WPF mit Overlay, Dashboard (5 Seiten), Statistik, Pro-App-Zeit, Autostart. Build-Fix `Storage.cs`. |
| 18.10.2026 | **v2 erweitert:** schwarzer Pillen-Look nach Screenshot, Eckenradius-Regler, Hintergrund-Deckkraft, Schatten an/aus, Rahmen, Presets, mehr Schriftoptionen, Jahres-/Gesamt-Statistik, **eine Datei pro Tag (lebenslang)**, Migration von `usage.json`, CSV-Export, Autostart auch im Tray, App-Icon. Build-Fix `AppIcon.cs`. |

---

## 15. Ausblick

Mögliche nächste Schritte, nach Nutzen geordnet. Nichts davon ist umgesetzt.

1. **Code sichern:** ZIP in dieses Projekt hochladen oder in ein Git-Repository legen (kleinste Absicherung gegen Verlust).
2. **Build bestätigen und alle Funktionen einmal durchtesten** (Checkliste 4.7), Fehler melden → beheben.
3. **Geräteübergreifende Zusammenführung** (PC, Handy, Tablet, Switch 2 in einem Dashboard): Die Tagesdateien (`days\*.json`) sind ein einfaches Austauschformat. Ein Abgleich über einen Cloud-Ordner (z. B. Google Drive) wäre ohne Server möglich. Offene Fragen: Konflikte bei zwei Geräten am selben Tag, Format der Android-Daten (Awareen), Datenschutz. Das widerspricht der bisherigen Regel „kein Netzwerk" nur, wenn die App selbst hochlädt; ein Drive-Desktop-Ordner würde das umgehen.
4. **Code-Signatur** oder Installer (Inno Setup), falls die App weitergegeben werden soll.
5. **SQLite statt JSON** nur, wenn Abfragen komplexer werden (Sitzungslisten, Filter, Jahresvergleiche).
6. **Echte Sitzungen** (Start/Ende) statt nur Tagessummen, falls eine Zeitleiste gewünscht ist.
7. **Schönere Namen** für Apps (Mapping `chrome → Google Chrome`), Gamepad-Eingaben als Aktivität, globaler Hotkey zum Ein-/Ausblenden.
8. **Windows 10 testen** und Fallbacks für Acrylic prüfen.

---

## Anhang: Datenschutz in einem Satz

Die App liest nur (1) die Zeit seit der letzten Eingabe, (2) den Prozessnamen des Vordergrundfensters und (3) die Uhrzeit; sie zeichnet keine Tasten, keine Fenstertitel und keine Bildschirmbilder auf, hat keinen Netzwerkcode und schreibt nur in `%AppData%\ScreenTimeTracker` und in den Autostart-Eintrag des aktuellen Benutzers.
