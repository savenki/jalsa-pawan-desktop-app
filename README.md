# Jalsa Pawan desktop companion

[Download the latest portable Windows build](dist/JalsaPawan-Windows.zip). Extract it and run `JalsaPawan.exe`.

The original ZIP at repository root is retained as requested; the newest build is in `dist/`.

Run **JalsaPawan.exe**. No installer or extra runtime download is needed on this Windows computer. Keep this folder in place so the Start menu shortcuts keep working.

## Start and stop

Search the Windows Start menu for **Jalsa Pawan - Start or Stop**. The same shortcut starts him when closed and stops him when running. **Jalsa Pawan - Stop** always stops him. Click him and use **Stop pet** in the control panel, or right-click the pet or tray icon and choose **Quit**.

## What he does

- Starts in **Pomodoro Focus mode**: sits under a tree above the Start button, reading. Focus lasts **25 minutes**, followed by a **5-minute break**. Bamboo and the hourly nap routine stay paused in Focus mode.
- The tree is visible only in the reading animation. It hides for cursor sword play, water breaks, waving, breaks, sleep, jumping and bamboo routines.
- Open **Focus timer** in the control panel or pet menu to pause, resume, restart, change focus/break durations, or leave Focus mode.
- Move the cursor near him to aim the katana. Quick swipes trigger an eight-direction sword sweep; after 2.5 quiet seconds, he returns to reading. Single-click still opens the control panel. Toggle cursor sword control in the right-click menu.
- Runs between three bamboo stations above the taskbar, slices them and starts another round after they regrow.
- Reminds you to blink every **10 minutes**, and drink water every **30 minutes**. Water reminders use the new drinking-glass animation. Both reminders can speak with an installed Windows voice.
- Every hour, sleeps for **2 minutes**. A bamboo stalk then grows to the usable screen height. Hover over the sleeping pet after it finishes growing to trigger one jump and one sword strike. The top falls away, he lands, and the next hour starts.
- Click him to open his control panel, drag him to reposition, or use **Move to screen** from his menu. The panel includes animation previews and timing controls.

Right-click > **Settings** changes the reminder intervals, voice, size and routines. **Preview hourly nap + giant bamboo (fast)** tests the hourly sequence with a4-second nap and4-second growth rather than waiting an hour. Normal timing remains60 minutes +2 minutes.

Reminders and the hourly timer run only while the companion is open. Closing it stops them; reopening starts fresh timers. This version does not automatically start at login. It uses Windows speech, not Pawan Kalyan's recorded or cloned voice. All artwork is embedded; it makes no network requests.

## Source and checks

Source is in `source/JalsaDesktop.cs`. `Build.ps1` rebuilds the executable with the installed .NET Framework compiler. `Install-StartMenu.ps1` recreates your per-user Start menu shortcuts.

`self-test.json` records scheduling, persistence and artwork checks. Diagnostic commands: `--status` writes runtime-status.json; `--water`, `--blink`, `--bamboo`, `--hourly-demo`, `--settings`, `--quit`, `--toggle` control the running instance.

`--shutdown-test` checks closing with the control panel, focus tree, reminder bubble and speech active. Stop pet now queues an orderly main-window shutdown rather than recursively exiting all forms during a button event.
