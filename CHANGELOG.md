# Changelog

## 0.2.0 - 2026-11-14

- **Crash hardening for GPU device loss.** A new `GlassStabilityGuard` holds back window restructuring and screen capture while the editor is compiling, importing, switching play mode, or resuming from an idle session, plus for a short window after a domain reload - the exact moment a D3D12 device rebuild used to collide with the title-bar hook.
- Window chrome now installs ~0.15 s after a cold start and ~0.8 s after a domain reload (previously a flat 3 s), and the freeze curtain is also raised when only the persisted style says the frame is ours, so the native title bar no longer flashes back during reloads.
- Backdrop capture backs off 2-10 s after a slow or failing capture, and `WDA_EXCLUDEFROMCAPTURE` is cleared once instead of on every frame.
- **License change**: MIT -> GNU GPL v3.0 (`GPL-3.0-only`); `LICENSE` now holds the full GPLv3 text, and `package.json` and the README were updated to match.
- Removed comments from source and config files (C# / USS / packaging script / SVG / `.gitignore`); behavior is unchanged.
- Added copyright and SPDX license headers to source files (`SPDX-License-Identifier: GPL-3.0-only`).
- Bilingual documentation, English first: English `README.md` as the landing page with a Chinese `README.zh-CN.md`, English `docs/macos-chrome.md` with a Chinese `docs/macos-chrome.zh-CN.md`, English `CHANGELOG.md` with a Chinese `CHANGELOG.zh-CN.md`, and English diagram labels in `architecture.svg` with Chinese labels in `architecture.zh-CN.svg`.
- Rebuilt the three packages under `dist/` so their sources and license match the repository.

## 0.1.0 - 2026-11-13

First public release. Baseline: **Unity 6000.3.8f1 / Windows 11**.

- **macOS-style title bar**: traffic lights (close / minimize / maximize), centered project name, rounded corners; colors follow window focus. Native dragging, double-click maximize, Aero Snap and eight-way resizing are preserved.
- **Screen-top menu bar**: a separate always-on-top layered window with fully custom-drawn dropdowns, cascading submenus, checkmarks, separators and hover highlighting; menu commands are forwarded to Unity, with no menu logic reimplemented.
- **Appearance options**: menu bar background (auto / transparent / light / dark / custom) and dropdown palette (follow system / light / dark / custom).
- **Focus whitelist**: does not steal focus when a code editor is in the foreground, so clicking the menu does not kick you out of your IDE.
- **Lifecycle**: the on/off state is persisted in `EditorPrefs` and restored after domain reloads or editor restarts.
- **Repair and diagnostics**: `Restore Native Frame`, `Run Self Test`, window screenshots, and `Library/mactitlebar.log`.
- **Three packages**: `dist/unity-macos-chrome-0.1.0.unitypackage` (one-click import), `.zip` (unzip and drop in) and `com.liquidglass.macos-chrome-0.1.0.tgz` (UPM tarball), all produced by `tools/make-unitypackage.py`.
