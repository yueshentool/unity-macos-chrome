# macOS Chrome for Unity Editor

![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6)
![Unity](https://img.shields.io/badge/Unity-2021.3%2B-222222)
![License](https://img.shields.io/badge/License-GPL--3.0-blue)

**English** | [简体中文](README.zh-CN.md)

[About](#about) • [Screenshots](#screenshots) • [Install](#install) • [How it works](#how-it-works)

**A Unity Editor extension that gives the Windows editor a macOS look.**

Traffic-light window controls, a centered project name, rounded corners, and a menu bar moved to the very top of the screen.

- [Screenshots](#screenshots)
- [About](#about)
- [Install](#install)
- [Usage](#usage)
- [How it works](#how-it-works)
- [Requirements](#requirements)
- [Project layout](#project-layout)
- [License](#license)

---

## Screenshots

<p align="center">
  <img src="docs/img/editor-screenshot.png" alt="The Unity Editor with the extension enabled" width="960">
</p>

<p align="center">
  In action: the macOS-style menu bar drawn at the top of the screen, with the Unity Editor window below using the custom title bar.
</p>

---

## About

Unity on Windows ships with the standard Win32 window chrome. This project replaces it with a macOS look for people who are used to macOS — **without giving up any native window behavior**.

| Feature | Details |
|---|---|
| macOS-style title bar | Traffic lights on the left (close / minimize / maximize), centered project name, rounded corners; the lights follow window focus and turn gray when inactive |
| Screen-top menu bar | Unity's top-level menus moved to the very top of the screen (a separate always-on-top window). Dropdowns are **fully custom-drawn**, with cascading submenus, checkmarks, separators and hover highlighting |
| Adjustable appearance | Menu bar background (auto / transparent / light / dark / custom) and dropdown palette (follow system / light / dark / custom) |
| Native window behavior | Dragging, double-click maximize, Aero Snap, eight-way resize, `Win + arrow keys` and `Alt + Space` all still work |
| No install, easy removal | The on/off state lives in `EditorPrefs` and is restored after domain reloads or editor restarts; nothing outside the package is modified — delete the folder to uninstall |

All dimensions scale with `GetDpiForWindow`, so high-DPI displays need no extra configuration.

---

## Install

**Windows only** (pure Win32 non-client-area work). Windows 10 2004+ / Windows 11 recommended.

| Method | File | Notes |
|---|---|---|
| **One-click import (recommended)** | [`unity-macos-chrome-0.1.0.unitypackage`](dist/unity-macos-chrome-0.1.0.unitypackage) | Double-click it, or use `Assets > Import Package > Custom Package...`, select all → Import. Installs to `Assets/LiquidGlass/`; an existing folder is updated in place |
| Unzip and drop in | [`unity-macos-chrome-0.1.0.zip`](dist/unity-macos-chrome-0.1.0.zip) | Unzip to get `LiquidGlass/`, then drag the whole folder into your project's `Assets/` |
| UPM tarball | [`com.liquidglass.macos-chrome-0.1.0.tgz`](dist/com.liquidglass.macos-chrome-0.1.0.tgz) | `Window > Package Manager > + > Add package from tarball...` |
| Local package | `package.json` | `Add package from disk...`; or add `"com.liquidglass.macos-chrome": "file:../../unity-macos-chrome"` to `Packages/manifest.json` |
| Git URL | Repository URL | `Package Manager > + > Add package from git URL...` |

> **Note** — The import package has no asmdef, so the scripts compile into `Assembly-CSharp-Editor`; installing via UPM uses the dedicated `LiquidGlass.Editor` assembly. **Do not install both at once** — you will get duplicate type definitions (`CS0101`). When switching from Assets to UPM, delete the old `Assets/LiquidGlass` first.

Once Unity finishes compiling, `Tools > Editor Chrome` appears in the menu bar. Enable it as described below.

---

## Usage

### Menu

| Menu | Action |
|---|---|
| `Tools > Editor Chrome > macOS Title Bar > Enable` | Enable the macOS title bar and the screen-top menu bar |
| `Tools > Editor Chrome > macOS Title Bar > Disable` | Disable and fully restore the native chrome |
| `Tools > Editor Chrome > Restore Native Frame (fix broken window)` | **Emergency repair**: force-restore the window frame / client area if it gets messed up |
| `Tools > Liquid Glass > Settings...` | Settings panel: whitelist, menu bar background, dropdown palette, etc. |
| `Tools > Liquid Glass > Run Self Test` | Self-check; prints window / menu / capture state to the Console |
| `Tools > Liquid Glass > Capture Window (PNG)` | Capture a screenshot of the editor window |

The on/off state is stored in `EditorPrefs` (key prefix `LiquidGlass.`), so **it is restored automatically after a domain reload or editor restart** — no need to click it every time.

### Configuration

Use the `Liquid Glass` menu at the far right of the screen-top menu bar, or the `Settings...` panel, to adjust:

- **Menu bar background**: auto / transparent / light / dark / custom
- **Dropdown palette**: follow system / light / dark / custom
- **Whitelisted apps**: add / remove, or edit the text field directly in the settings panel

### Focus whitelist

The menu bar window uses `WS_EX_NOACTIVATE`, so clicking it does not activate Unity. However, if the foreground window is a **code editor** (VS Code, Rider, Cursor, …), the extension will not steal focus by default — otherwise clicking the menu would kick you out of your IDE.

The trade-off: while another app has focus, keyboard navigation inside dropdowns (Esc / arrow keys) does not work; the mouse still does.

The default whitelist already covers common IDEs (`Code.exe`, `cursor.exe`, `rider64.exe`, `devenv.exe`, `sublime_text.exe`, `WindowsTerminal.exe`, …). Entries may be written as `Code.exe` or `code`, case-insensitive.

---

## How it works

In one sentence: **remove the native title bar, make the non-client area "ownerless", then draw it yourself.**

1. `SetWindowLongPtr(GWL_STYLE)` removes `WS_CAPTION` while keeping `WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX`;
2. `WM_NCCALCSIZE` pushes the top of the client area down, leaving a 28 px title bar strip;
3. The strip is custom-painted with traffic lights, the title and rounded corners, and `WM_NCHITTEST` reports it as `HTCAPTION` — dragging, double-click maximize and Aero Snap are still handled natively by the system;
4. The menu bar is a separate `WS_POPUP` topmost layered window, shown only while Unity is the foreground process; dropdowns are fully custom-drawn and the selected item is sent back to Unity via `PostMessage(WM_COMMAND, id)` — **no menu logic is reimplemented**;
5. `WM_GETMINMAXINFO` shifts the maximize area down so a maximized window stops below the screen menu bar.

About 7,600 lines of C# — see [How it works](docs/macos-chrome.md) for details.

---

## Requirements

| Item | Notes |
|---|---|
| OS | **Windows only**. Windows 10 2004+ is mostly fine; Windows 11 gives the full experience (rounded corners, acrylic, dark title bar) |
| Unity | Tested on **6000.3.8f1**; `package.json` declares `2021.3` and above |
| Render pipeline | Irrelevant — only Win32 windows are touched, nothing in rendering |
| Platform modules | None required; editor-only scripts, never included in builds |
| Multi-monitor | The menu bar covers only the display that contains the main Unity window |

---

## Project layout

```
unity-macos-chrome/
├─ Editor/
│  ├─ MacTitleBar.cs            ★ macOS-style title bar
│  ├─ MacMenuBar.cs             ★ Screen-top menu bar + custom dropdowns
│  ├─ FreezeCurtain.cs          Snapshot curtain covering the NC area during domain reloads
│  ├─ NativeMethods.cs          Win32 / DWM / GDI declarations
│  ├─ UnityMainWindow.cs        Locates the main Unity window handle
│  ├─ LiquidGlassSettings.cs    Settings persistence + settings panel
│  ├─ LiquidGlassBootstrap.cs   Lifecycle and menu entry points
│  ├─ GlassDiagnostics.cs       Self-test / screenshot tools
│  ├─ LiquidGlass.Editor.asmdef
│  └─ Styles/GlassTheme.uss     Theme styles (optional)
├─ docs/
│  ├─ macos-chrome.md           How it works
│  ├─ macos-chrome.zh-CN.md     How it works (Chinese)
│  └─ img/                      Screenshots and diagrams
├─ dist/                        Three distribution packages
├─ tools/make-unitypackage.py   Packaging script
├─ README.zh-CN.md              Chinese README
└─ package.json                 UPM package manifest
```

The two files marked ★ are the core.

---

## License

The approach borrows from the custom title bars in Chrome / VS Code / Electron, and from macOS window and menu bar conventions.

This project is licensed under the [GNU General Public License v3.0](LICENSE) (SPDX: `GPL-3.0-only`), © 2026 Liquid Glass contributors.

- You may use, modify and redistribute it freely, including commercially;
- If you distribute this plugin or any derivative work that includes or builds on it, **the whole must be licensed under GPL-3.0 with complete source code**;
- You must keep the copyright and license notices, include the full `LICENSE` text, and state any changes;
- Provided "as is", without any warranty.

If you need to integrate this plugin into closed-source or proprietary software, please contact the copyright holder for a separate license.

> The `.unitypackage` and `.zip` under `dist/` contain only `Assets/` resources and no license file; when distributing those two packages separately, include the `LICENSE` from the repository root (`.tgz` already bundles it).

> Before you start: this plugin **subclasses the Unity main window's window procedure** and modifies window styles. Domain-reload protection and an emergency repair menu are in place, but it is still wise to try it in a clean working copy under version control.
