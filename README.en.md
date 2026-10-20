# Auto Clicker / Автоклікер

[Українська](README.md) · [English](README.en.md)

A Windows desktop auto clicker with **English and Ukrainian interfaces**, multiple independently scheduled click points, draggable on-screen circles and automatic keyboard shortcuts.

## Download and run

1. **[Download the ready-to-run Windows ZIP](https://github.com/Klon4ik/Avtokliker/raw/refs/heads/main/downloads/Avtokliker-Windows.zip)**.
2. Extract the **entire folder** to a writable location, such as Documents.
3. Open **Start.cmd** or **app/AvtoClicker.exe**. Do not run it from inside the ZIP archive.
4. Run **Create-Shortcut.cmd** to create an **Auto Clicker** shortcut on your desktop.

Requires **Windows 10/11 and .NET Framework 4.x**. No Python, .NET SDK, package installation or internet connection is needed to use the app. The executable is also available in [app](app). You can download the whole repository using **Code → Download ZIP** and follow the same steps after extracting it.

## Choose your language

Select **English** or **Українська / Ukrainian** in the top-right corner. The interface changes immediately without restarting. Your action names stay exactly as you entered them. The language preference is saved with the profile and restored on the next launch. Stop automation with **F9** before changing the language.

## Add mouse clicks

1. Click **+ Click point**.
2. Enter a name and select **Left button**, **Right button** or **Middle button**.
3. Click **Pick on screen · 3 s**. Move the pointer to the desired position within three seconds. The editor temporarily hides, captures the pointer coordinates, then returns. You can also enter X and Y manually.
4. Set the **Interval, s**, **Initial delay, s** and **Repeats**. A repeat count of **0** means unlimited. The minimum interval is **0.01 seconds**.
5. Click **Save**. Add more points with different intervals, or select an existing point and click **Duplicate**.

Use the **On** checkbox to enable or disable each action. Select a row and click **Edit** to change its settings, or **Remove** to remove it.

## Drag circles on screen

Enable **Show click points on screen**. Every mouse point appears as a numbered circle above other windows. Its number matches the **№** in the table. The **crosshair at the center** is the exact click position.

- **Drag a circle with the left mouse button** to move its click point. The table updates as you drag; the new coordinates are saved when you release the button.
- **Arrange points** enables circles and minimizes the main window so you can position them over your target application.
- Add multiple circles using **+ Click point** or **Duplicate**. Each keeps its own button, interval, delay and repeat count.
- **Double-click a circle** to edit that point.
- When automation starts, circles become **transparent to mouse input** so clicks reach the application underneath. They briefly highlight when clicks are sent.
- Circles cannot be moved during the countdown or while automation is running. Press **F9** to stop and move them again.
- Disabled actions have gray dashed circles. Turn off **Show click points on screen** to hide all circles.

Circles temporarily hide while settings and file dialogs are open, and close with the app. A point outside the connected displays is hidden; edit its coordinates or capture a new position with **F6**.

## Add keyboard actions

Click **+ Key**, choose the key and optionally enable **Ctrl**, **Alt** and **Shift**. Set its own interval, initial delay and repeat count, then save it. Keys are sent as presses and releases to the **active window**. Mouse clicks may change which window is active. Action keys represent keyboard keys; typing letters also depends on your active keyboard layout.

**F6, F8 and F9 are reserved** for controlling the app and cannot be selected as automatic key actions.

## Start and stop

Keep the **Start countdown** at **3 seconds** and enable **Minimize on start**. Click **Start** or press **F8**, then switch to your target window before the countdown ends.

| Hotkey | Action |
| --- | --- |
| **F6** | Capture the current pointer coordinates for the selected click point while automation is stopped. Works while another app is active. |
| **F8** | Start or stop all enabled actions. A new run restarts the countdown, delays and counters. |
| **F9** | Stop immediately or cancel the countdown. |

Closing the app stops all actions. If another app has reserved F6, F8 or F9, Auto Clicker explains the conflict and blocks automation until you restart it with those hotkeys available.

Each action has an independent schedule. Its first execution occurs after the shared countdown plus its own initial delay; subsequent executions follow its interval. Windows has one system pointer, so mouse movements and clicks are dispatched in order. Missed intervals, such as while the computer sleeps, are skipped rather than replayed in a burst. Timing accuracy depends on Windows load, especially at 10–20 ms intervals.

Automatic input is skipped, without increasing its counter, while a physical mouse button or Ctrl/Alt/Shift/Windows modifier is held. Key actions are also skipped while their chosen key is already held. They resume after you release the input.

## Profiles and shortcuts

- Settings are automatically saved beside the app in **app/settings.json**; **app/settings.json.bak** stores the previous version.
- **Save profile** and **Open profile** let you keep several configurations as JSON files, including their language and marker visibility preferences.
- After moving the application folder, run **Create-Shortcut.cmd** again to update the desktop shortcut.
- Multiple monitors and negative X/Y coordinates are supported. Review your points after changing monitor layout or display scaling.
- If a target app is running as administrator, Auto Clicker may need the same privileges to interact with it. Some apps and games do not accept synthetic input.

You can launch directly with a particular profile:

```text
app\AvtoClicker.exe --profile "C:\path\profile.json"
```

Changes are automatically saved to that file.

## Build and test

Close the running app before building so Windows can replace the EXE. Run this in the repository folder:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

The script uses the .NET Framework compiler included with Windows, builds **app/AvtoClicker.exe** and creates a local shortcut. No dependency downloads are needed.

**14 automated checks** cover independent intervals, delays and repeat limits, cancellation, input failures, profile serialization and backups, native input layouts, multiple-monitor coordinates, circle dragging and live language switching while preserving actions. The self-tests do not send real mouse clicks or keystrokes.

To build the portable ZIP:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Package.ps1
```

The output is **downloads/Avtokliker-Windows.zip**. It includes the app, icon, both guides and launcher/shortcut scripts. Personal profiles, backups and shortcuts containing local paths are excluded from both the package and the repository.

Source code is in **src/**. Translations are maintained in **src/L10n.cs**. A manual click-receiving test window is available in **tests/ClickReceiver.cs**; it does not generate input.

Input uses Windows [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput); multi-monitor coordinates use [MOUSEINPUT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-mouseinput).
