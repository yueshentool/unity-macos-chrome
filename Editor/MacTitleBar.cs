// Copyright (C) 2026 Liquid Glass contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LiquidGlass.Editor
{

    public static class MacTitleBar
    {

        const int GWLP_WNDPROC = -4;
        const int GWL_STYLE = -16;

        const long WS_CAPTION = 0x00C00000L;
        const long WS_THICKFRAME = 0x00040000L;
        const long WS_SYSMENU = 0x00080000L;
        const long WS_MINIMIZEBOX = 0x00020000L;
        const long WS_MAXIMIZEBOX = 0x00010000L;
        const long WS_MAXIMIZE = 0x01000000L;

        const uint kEditorBackdrop = 0x002A2A2Au;

        const int WM_SIZE = 0x0005;
        const int WM_ACTIVATE = 0x0006;
        const int WM_MOVE = 0x0003;
        const int WM_SETTEXT = 0x000C;
        const int WM_COMMAND = 0x0111;
        const int WM_GETMINMAXINFO = 0x0024;
        const int WM_NCCALCSIZE = 0x0083;
        const int WM_PAINT = 0x000F;
        const int WM_ERASEBKGND = 0x0014;
        const int WM_NCPAINT = 0x0085;
        const int WM_NCACTIVATE = 0x0086;
        const int WM_NCHITTEST = 0x0084;
        const int WM_NCLBUTTONDOWN = 0x00A1;
        const int WM_SETCURSOR = 0x0020;
        const int WM_SYSCOMMAND = 0x0112;
        const int WM_ENTERSIZEMOVE = 0x0231;
        const int WM_EXITSIZEMOVE = 0x0232;

        const int SM_CXSIZEFRAME = 32;
        const int SM_CYSIZEFRAME = 33;
        const int SM_CXPADDEDBORDER = 92;
        const int SM_CYMENU = 15;

        const int HTCLIENT = 1;
        const int HTTOP = 12;
        const int HTCAPTION = 2;
        const int HTLEFT = 10;
        const int HTRIGHT = 11;
        const int HTBOTTOM = 15;
        const int HTTOPLEFT = 13;
        const int HTTOPRIGHT = 14;
        const int HTBOTTOMLEFT = 16;
        const int HTBOTTOMRIGHT = 17;

        const int SC_MINIMIZE = 0xF020;
        const int SC_MAXIMIZE = 0xF030;
        const int SC_CLOSE = 0xF060;
        const int SC_RESTORE = 0xF120;

        const uint DT_CENTER = 0x00000001;
        const uint DT_VCENTER = 0x00000004;
        const uint DT_SINGLELINE = 0x00000020;
        const uint DT_NOPREFIX = 0x00000800;
        const uint DT_END_ELLIPSIS = 0x00008000;

        const int TRANSPARENT = 1;
        const uint SRCCOPY = 0x00CC0020;

        const string k_PrefKey = "LiquidGlass.MacTitleBar.Enabled";

        const float kBarHeight = 28f;

        const float kCornerRadius = 14f;

        delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        static WndProcDelegate s_Delegate;
        static IntPtr s_OldWndProc;
        static IntPtr s_Hwnd;
        static long s_OrigStyle;
        static IntPtr s_HMenu;
        static int s_BarHeight = 28;
        static int s_FrameY = 8;
        static float s_DpiScale = 1f;
        static bool s_Painting;
        static bool s_LoggedNcPaint;

        static bool s_ActiveStable = true;

        static bool s_InSizeMove;

        sealed class CaptionCache
        {
            public IntPtr MemDC = IntPtr.Zero;
            public IntPtr Bmp = IntPtr.Zero;
            public IntPtr OldBmp = IntPtr.Zero;
            public int W, H;

            public bool HasContent;
            public int CW, CBarBottom, CTitleLen;
            public bool CActive, CDebug;

            public double LastBlitAt = -1;
        }

        static readonly Dictionary<IntPtr, CaptionCache> s_Captions =
            new Dictionary<IntPtr, CaptionCache>();

        static IntPtr s_TitleFont;
        static int s_TitleFontHeight = int.MinValue;
        static IntPtr s_BrushActive, s_BrushInactive, s_BrushDebug, s_BrushErase;
        static int s_OurPid = -1;

        static int OurPid()
        {
            if (s_OurPid < 0) s_OurPid = (int)NativeMethods.GetCurrentProcessId();
            return s_OurPid;
        }

        static IntPtr GetTitleFont(int height)
        {
            if (s_TitleFont != IntPtr.Zero && s_TitleFontHeight == height) return s_TitleFont;
            if (s_TitleFont != IntPtr.Zero) NativeMethods.DeleteObject(s_TitleFont);
            s_TitleFont = NativeMethods.CreateFont(height, 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 0, 0, "Segoe UI");
            s_TitleFontHeight = height;
            return s_TitleFont;
        }

        static IntPtr GetBgBrush(uint color)
        {
            if (color == 0x2C2C2Cu) return s_BrushActive != IntPtr.Zero ? s_BrushActive : (s_BrushActive = NativeMethods.CreateSolidBrush(color));
            if (color == 0x242424u) return s_BrushInactive != IntPtr.Zero ? s_BrushInactive : (s_BrushInactive = NativeMethods.CreateSolidBrush(color));
            return s_BrushDebug != IntPtr.Zero ? s_BrushDebug : (s_BrushDebug = NativeMethods.CreateSolidBrush(color));
        }

        static void FixClassStyle(IntPtr hWnd)
        {
            try
            {
                long cur = NativeMethods.GetClassLongPtr(hWnd, NativeMethods.GCL_STYLE).ToInt64();
                long want = NativeMethods.CS_DBLCLKS | NativeMethods.CS_HREDRAW | NativeMethods.CS_VREDRAW;
                if (cur == want) return;

                NativeMethods.SetClassLongPtr(hWnd, NativeMethods.GCL_STYLE, (IntPtr)want);

                long now = NativeMethods.GetClassLongPtr(hWnd, NativeMethods.GCL_STYLE).ToInt64();
                Log($"类样式修复 hwnd=0x{hWnd.ToInt64():X} 0x{cur:X} -> 0x{now:X}");
            }
            catch { }
        }

        static void ReleaseCaptionCache(IntPtr hWnd)        {
            if (!s_Captions.TryGetValue(hWnd, out var cc)) return;
            if (cc.MemDC != IntPtr.Zero)
            {
                if (cc.OldBmp != IntPtr.Zero) NativeMethods.SelectObject(cc.MemDC, cc.OldBmp);
                NativeMethods.DeleteDC(cc.MemDC);
            }
            if (cc.Bmp != IntPtr.Zero) NativeMethods.DeleteObject(cc.Bmp);
            s_Captions.Remove(hWnd);
        }

        static void ReleaseAllCaptionCaches()
        {
            foreach (var h in new List<IntPtr>(s_Captions.Keys)) ReleaseCaptionCache(h);
            if (s_TitleFont != IntPtr.Zero) { NativeMethods.DeleteObject(s_TitleFont); s_TitleFont = IntPtr.Zero; }
            s_TitleFontHeight = int.MinValue;
            foreach (var b in new[] { s_BrushActive, s_BrushInactive, s_BrushDebug, s_BrushErase })
                if (b != IntPtr.Zero) NativeMethods.DeleteObject(b);
            s_BrushActive = s_BrushInactive = s_BrushDebug = s_BrushErase = IntPtr.Zero;
        }

        static bool ResolveActive(bool rawActive)
        {
            s_ActiveStable = rawActive;
            return rawActive;
        }

        public static int BarTopHeight => s_FrameY + s_BarHeight;

        static readonly uint[] s_DotColors = { 0x575FFF, 0x2EBCFE, 0x40C828 };
        static readonly uint[] s_DotColorsInactive = { 0x4A4A4A, 0x4A4A4A, 0x4A4A4A };

        public static bool Installed { get; private set; }

        class WinRec
        {
            public IntPtr OldProc;
            public long OrigStyle;
            public bool IsMain;
        }

        static readonly Dictionary<IntPtr, WinRec> s_Wins = new Dictionary<IntPtr, WinRec>();
        static double s_NextWindowScan;

        static bool IsHooked(IntPtr hWnd) => s_Wins.ContainsKey(hWnd);

        static bool IsMainWindow(IntPtr hWnd) => s_Hwnd != IntPtr.Zero && hWnd == s_Hwnd;

        public static void ClearCaptureExclusion(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            try
            {
                NativeMethods.SetWindowDisplayAffinity(hWnd, 0);
            }
            catch { }
        }

        public static string ClearCaptureExclusionNow()
        {
            ClearCaptureExclusion(s_Hwnd);
            return "cleared 0x" + s_Hwnd.ToInt64().ToString("X");
        }

        public static bool DebugPaint;

        public static bool ForceActive;

        public static int PaintCount { get; private set; }

        static readonly string s_LogPath = System.IO.Path.Combine("Library", "mactitlebar.log");

        internal static void Log(string msg)
        {
            try
            {
                System.IO.File.AppendAllText(s_LogPath,
                    DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + Environment.NewLine);
            }
            catch { }
        }

        [InitializeOnLoadMethod]
        static void OnEditorLoad()
        {

            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            EditorApplication.quitting -= OnQuitting;
            EditorApplication.quitting += OnQuitting;

            bool wanted = EditorPrefs.GetBool(k_PrefKey, false);
            Log("OnEditorLoad pref=" + wanted);

            if (!wanted)
            {

                try { MacMenuBar.Destroy(); } catch { }
                try { FreezeCurtain.Hide(); } catch { }
            }

            if (wanted)
            {

                s_PendingInstall = true;
                s_PendingDeadline = EditorApplication.timeSinceStartup + 6.0;
                EditorApplication.delayCall -= DeferredInstall;
                EditorApplication.delayCall += DeferredInstall;

                s_NextInstallTry = EditorApplication.timeSinceStartup + 0.15;
                EditorApplication.update -= OnUpdateInstall;
                EditorApplication.update += OnUpdateInstall;
            }
        }

        static bool s_PendingInstall;
        static double s_NextInstallTry;
        static double s_PendingDeadline;

        static void DeferredInstall()
        {
            EditorApplication.delayCall -= DeferredInstall;
            if (!s_PendingInstall || Installed) return;

            try { Install(); }
            catch (Exception e) { Log("DeferredInstall 异常：" + e.GetType().Name + " " + e.Message); }

            if (Installed)
            {
                s_PendingInstall = false;
                return;
            }

            s_NextInstallTry = EditorApplication.timeSinceStartup + 0.15;
            EditorApplication.update -= OnUpdateInstall;
            EditorApplication.update += OnUpdateInstall;
        }

        static void OnUpdateInstall()
        {
            if (!s_PendingInstall) return;

            double now = EditorApplication.timeSinceStartup;
            if (now < s_NextInstallTry) return;
            s_NextInstallTry = now + 0.15;

            if (!Installed)
            {
                try { Install(); }
                catch (Exception e) { Log("OnUpdateInstall 异常：" + e.GetType().Name + " " + e.Message); }
            }

            if (Installed || now > s_PendingDeadline)
            {
                bool ok = Installed;
                s_PendingInstall = false;
                EditorApplication.update -= OnUpdateInstall;
                if (!ok)
                {
                    Log("Install 重试超时，放弃本轮（下次域重载会再试）");
                    RecoverNativeFrame();
                }
            }
        }

        static void RecoverNativeFrame()
        {
            try
            {
                long orig = ReadPersistedOrigStyle();
                IntPtr hwnd = s_Hwnd != IntPtr.Zero ? s_Hwnd : UnityMainWindow.Handle;

                if (orig != 0 && hwnd != IntPtr.Zero && NativeMethods.IsWindow(hwnd))
                {
                    long cur = NativeMethods.GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
                    if ((cur & WS_CAPTION) == 0)
                    {
                        NativeMethods.SetWindowLongPtr(hwnd, GWL_STYLE, (IntPtr)orig);
                        ForceRepaint(hwnd);
                        Log("RecoverNativeFrame：已还原原生标题栏兜底");
                    }
                }

                try { MacMenuBar.Destroy(); } catch { }
                try { FreezeCurtain.Hide(); } catch { }
                ClearPersistedOrigStyle();
            }
            catch { }
        }

        static void OnQuitting()
        {
            s_Quitting = true;
            try { OnBeforeReload(); } catch { }
        }

        static bool s_Quitting;

        static void OnBeforeReload()
        {
            Log("OnBeforeReload installed=" + Installed);

            try { MacMenuBar.CloseActivePopup(); } catch { }

            try { EditorApplication.delayCall -= DeferredInstall; } catch { }
            try { EditorApplication.update -= OnUpdateInstall; } catch { }
            s_PendingInstall = false;

            PersistOrigStyle();
            DetachForReload();
        }

        [MenuItem("Tools/Editor Chrome/macOS Title Bar/Enable", false, 1)]
        public static void Install()
        {
            if (Installed) return;

            s_Hwnd = UnityMainWindow.Handle;
            if (s_Hwnd == IntPtr.Zero)
            {
                Log("Install 中止：找不到主窗口");
                Debug.LogWarning("[Editor Chrome] 找不到 Unity 主窗口。");
                return;
            }

            if (!NativeMethods.GetWindowRect(s_Hwnd, out var wr))
            {
                Log("Install 中止：读不到窗口矩形");
                return;
            }

            var origin = new NativeMethods.POINT { X = 0, Y = 0 };
            if (!NativeMethods.ClientToScreen(s_Hwnd, ref origin))
            {
                Log("Install 中止：读不到客户区原点");
                return;
            }

            int captionHeight = origin.Y - wr.Top;
            if (captionHeight <= 0)
            {
                Log("Install 中止：没有原生标题栏 captionHeight=" + captionHeight);
                Debug.LogWarning("[Editor Chrome] 这个窗口没有原生标题栏，无法仿 macOS。");
                return;
            }

            s_DpiScale = GetDpiScale(s_Hwnd);

            s_FrameY = NativeMethods.GetSystemMetrics(SM_CYSIZEFRAME)
                     + NativeMethods.GetSystemMetrics(SM_CXPADDEDBORDER);

            int menuH = NativeMethods.GetSystemMetrics(SM_CYMENU);
            s_BarHeight = Mathf.Max(
                Mathf.RoundToInt(kBarHeight * s_DpiScale) - s_FrameY,
                menuH);

            try
            {

                s_Delegate = WndProc;
                IntPtr newProc = Marshal.GetFunctionPointerForDelegate(s_Delegate);
                s_OldWndProc = NativeMethods.SetWindowLongPtr(s_Hwnd, GWLP_WNDPROC, newProc);
                Log("Install oldProc=0x" + s_OldWndProc.ToInt64().ToString("X"));
                if (s_OldWndProc == IntPtr.Zero)
                {
                    s_Delegate = null;
                    Log("Install 失败：SetWindowLongPtr 返回 0");
                    return;
                }

                long liveStyle = NativeMethods.GetWindowLongPtr(s_Hwnd, GWL_STYLE).ToInt64();
                long persistedStyle = ReadPersistedOrigStyle();
                s_OrigStyle = (persistedStyle != 0 && (liveStyle & WS_CAPTION) == 0)
                    ? persistedStyle
                    : liveStyle;

                long style = liveStyle;
                style &= ~WS_CAPTION;
                style |= WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
                NativeMethods.SetWindowLongPtr(s_Hwnd, GWL_STYLE, (IntPtr)style);
                PersistOrigStyle();

                s_HMenu = NativeMethods.GetMenu(s_Hwnd);

                int none = 1;
                NativeMethods.DwmSetWindowAttribute(s_Hwnd, 38, ref none, sizeof(int));
                int dark = 1;
                NativeMethods.DwmSetWindowAttribute(s_Hwnd, 20, ref dark, sizeof(int));
                int round = 2;
                NativeMethods.DwmSetWindowAttribute(s_Hwnd, 33, ref round, sizeof(int));
                int noBorder = unchecked((int)0xFFFFFFFE);
                NativeMethods.DwmSetWindowAttribute(s_Hwnd, 34, ref noBorder, sizeof(int));

                int captionColor = 0x002C2C2C;
                NativeMethods.DwmSetWindowAttribute(s_Hwnd, 35, ref captionColor, sizeof(int));
                int textColor = 0x00F0F0F0;
                NativeMethods.DwmSetWindowAttribute(s_Hwnd, 36, ref textColor, sizeof(int));

                EnableDarkMenus();

                ClearCaptureExclusion(s_Hwnd);

                Installed = true;
                EditorPrefs.SetBool(k_PrefKey, true);

                s_Wins[s_Hwnd] = new WinRec
                {
                    OldProc = s_OldWndProc,
                    OrigStyle = s_OrigStyle,
                    IsMain = true,
                };

                MacMenuBar.Create(s_Hwnd, s_HMenu, s_DpiScale);

                ForceRepaint(s_Hwnd);

                FreezeCurtain.Hide();

                RepairStaleZoom(s_Hwnd);

                NormalizeWindowRect(s_Hwnd);

                IntPtr darkBrush = NativeMethods.CreateSolidBrush(kEditorBackdrop);
                NativeMethods.SetClassLongPtr(s_Hwnd, NativeMethods.GCLP_HBRBACKGROUND, darkBrush);

                FixClassStyle(s_Hwnd);

                NativeMethods.SetClassLongPtr(s_Hwnd, NativeMethods.GCLP_HCURSOR, IntPtr.Zero);

                IntPtr view = NativeMethods.FindWindowEx(s_Hwnd, IntPtr.Zero, "UnityGUIViewWndClass", null);
                if (view == IntPtr.Zero)
                {
                    IntPtr container = NativeMethods.FindWindowEx(s_Hwnd, IntPtr.Zero, "UnityContainerWndClass", null);
                    if (container != IntPtr.Zero)
                        view = NativeMethods.FindWindowEx(container, IntPtr.Zero, "UnityGUIViewWndClass", null);
                }
                if (view != IntPtr.Zero)
                {
                    NativeMethods.SetClassLongPtr(view, NativeMethods.GCLP_HBRBACKGROUND,
                        NativeMethods.CreateSolidBrush(kEditorBackdrop));

                    FixClassStyle(view);
                }

                Log($"Install 成功 barH={s_BarHeight} dpi={s_DpiScale:0.##} frameY={s_FrameY} " +
                    $"classBrush={(view != IntPtr.Zero ? "main+view" : "main-only")} " +
                    $"menu=0x{s_HMenu.ToInt64().ToString("X")} origStyle=0x{s_OrigStyle.ToString("X")}");
                Debug.Log($"[Editor Chrome] macOS 标题栏已开启（高 {s_FrameY + s_BarHeight}px，DPI {s_DpiScale * 100:0}%）。");

                EditorApplication.update -= SyncTick;
                EditorApplication.update += SyncTick;
                ScanEditorWindows(true);
            }
            catch (Exception e)
            {
                Log("Install 异常：" + e);
                Debug.LogError($"[Editor Chrome] 开启失败：{e.Message}");
            }
        }

        static bool s_DarkMenusApplied;

        const string kUnityWindowClass = "UnityContainerWndClass";

        static string GetTitle(IntPtr hWnd)
        {
            try
            {
                var sb = new StringBuilder(256);
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                return sb.ToString();
            }
            catch { return string.Empty; }
        }

        static void HookExtra(IntPtr hWnd)
        {
            try
            {
                if (hWnd == IntPtr.Zero || s_Hwnd == IntPtr.Zero) return;
                if (hWnd == s_Hwnd || IsHooked(hWnd)) return;
                if (s_Delegate == null) return;
                if (!NativeMethods.IsWindow(hWnd)) return;

                if (!NativeMethods.GetWindowRect(hWnd, out var wr)) return;
                var origin = new NativeMethods.POINT { X = 0, Y = 0 };
                if (!NativeMethods.ClientToScreen(hWnd, ref origin)) return;

                if (origin.Y - wr.Top <= 0) return;

                IntPtr oldProc = NativeMethods.SetWindowLongPtr(hWnd, GWLP_WNDPROC,
                    Marshal.GetFunctionPointerForDelegate(s_Delegate));
                if (oldProc == IntPtr.Zero) return;

                long origStyle = NativeMethods.GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64();
                long style = origStyle & ~WS_CAPTION;
                style |= WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
                NativeMethods.SetWindowLongPtr(hWnd, GWL_STYLE, (IntPtr)style);

                int none = 1, dark = 1, round = 2;
                int noBorder = unchecked((int)0xFFFFFFFE);
                NativeMethods.DwmSetWindowAttribute(hWnd, 38, ref none, sizeof(int));
                NativeMethods.DwmSetWindowAttribute(hWnd, 20, ref dark, sizeof(int));
                NativeMethods.DwmSetWindowAttribute(hWnd, 33, ref round, sizeof(int));
                NativeMethods.DwmSetWindowAttribute(hWnd, 34, ref noBorder, sizeof(int));

                s_Wins[hWnd] = new WinRec { OldProc = oldProc, OrigStyle = origStyle, IsMain = false };

                ClearCaptureExclusion(hWnd);

                NormalizeWindowRect(hWnd);
                ForceRepaint(hWnd);

                Log($"HookExtra 接管 0x{hWnd.ToInt64():X} title=\"{GetTitle(hWnd)}\"");
            }
            catch (Exception e)
            {
                Log("HookExtra 异常：" + e.Message);
            }
        }

        static void ScanEditorWindows(bool force)
        {
            if (!Installed) return;

            double now = EditorApplication.timeSinceStartup;
            if (!force && now < s_NextWindowScan) return;
            s_NextWindowScan = now + 0.5;

            if (s_Wins.Count > 0)
            {
                List<IntPtr> dead = null;
                foreach (var kv in s_Wins)
                {
                    if (!NativeMethods.IsWindow(kv.Key))
                    {
                        dead ??= new List<IntPtr>();
                        dead.Add(kv.Key);
                    }
                }
                if (dead != null)
                    foreach (IntPtr h in dead) s_Wins.Remove(h);
            }

            uint pid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            var found = new List<IntPtr>();
            var sb = new StringBuilder(128);

            NativeMethods.EnumWindows((h, _) =>
            {
                NativeMethods.GetWindowThreadProcessId(h, out uint owner);
                if (owner != pid) return true;
                sb.Length = 0;
                NativeMethods.GetClassName(h, sb, sb.Capacity);
                if (sb.ToString() == kUnityWindowClass) found.Add(h);
                return true;
            }, IntPtr.Zero);

            int added = 0;
            foreach (IntPtr h in found)
            {
                if (h == s_Hwnd || IsHooked(h)) continue;
                HookExtra(h);
                if (IsHooked(h)) added++;
            }

            if (added > 0) Log($"ScanEditorWindows 新增 {added} 个，共 {s_Wins.Count} 个窗口");
        }

        static void SyncTick()
        {

            if (s_InSizeMove) return;

            PerfProbe.Begin();
            ScanEditorWindows(false);

            if (Installed && s_Hwnd != IntPtr.Zero) PaintCaption(s_Hwnd);
            PerfProbe.End("title");
        }

        delegate int SetPreferredAppModeFn(int mode);
        delegate void FlushMenuThemesFn();

        static void EnableDarkMenus()
        {
            if (s_DarkMenusApplied) return;

            IntPtr ux = NativeMethods.LoadLibrary("uxtheme.dll");
            if (ux == IntPtr.Zero) return;

            try
            {
                IntPtr pSetMode = NativeMethods.GetProcAddress(ux, "#135");
                if (pSetMode != IntPtr.Zero)
                {
                    var setMode = (SetPreferredAppModeFn)Marshal.GetDelegateForFunctionPointer(
                        pSetMode, typeof(SetPreferredAppModeFn));
                    setMode(2);
                }

                IntPtr pFlush = NativeMethods.GetProcAddress(ux, "#136");
                if (pFlush != IntPtr.Zero)
                {
                    var flush = (FlushMenuThemesFn)Marshal.GetDelegateForFunctionPointer(
                        pFlush, typeof(FlushMenuThemesFn));
                    flush();
                }

                s_DarkMenusApplied = true;
                Log("dark menus applied");
            }
            catch (Exception e)
            {
                Log("EnableDarkMenus 失败：" + e.Message);
            }
        }

        static float GetDpiScale(IntPtr hwnd)
        {
            try
            {
                uint dpi = NativeMethods.GetDpiForWindow(hwnd);
                if (dpi > 0) return dpi / 96f;
            }
            catch { }

            try
            {
                IntPtr dc = NativeMethods.GetDC(IntPtr.Zero);
                if (dc != IntPtr.Zero)
                {
                    int dpi = NativeMethods.GetDeviceCaps(dc, 88);
                    NativeMethods.ReleaseDC(IntPtr.Zero, dc);
                    if (dpi > 0) return dpi / 96f;
                }
            }
            catch { }

            return 1f;
        }

        [MenuItem("Tools/Editor Chrome/macOS Title Bar/Enable", true)]
        static bool InstallValidate() { return !Installed; }

        [MenuItem("Tools/Editor Chrome/macOS Title Bar/Disable", false, 2)]
        public static void Uninstall()
        {
            EditorPrefs.SetBool(k_PrefKey, false);
            Unhook(silent: false);
            try { FreezeCurtain.Hide(); } catch { }
            ClearPersistedOrigStyle();
        }

        [MenuItem("Tools/Editor Chrome/macOS Title Bar/Disable", true)]
        static bool UninstallValidate() { return Installed; }

        static void Unhook(bool silent)
        {
            if (!Installed || s_Hwnd == IntPtr.Zero) return;

            try
            {

                MacMenuBar.Destroy();

                EditorApplication.update -= SyncTick;
                foreach (var kv in s_Wins)
                {
                    IntPtr h = kv.Key;
                    if (h == s_Hwnd) continue;
                    try
                    {
                        if (!NativeMethods.IsWindow(h)) continue;
                        RestoreStyleBits(h, kv.Value.OrigStyle);
                        if (kv.Value.OldProc != IntPtr.Zero)
                            NativeMethods.SetWindowLongPtr(h, GWLP_WNDPROC, kv.Value.OldProc);
                        NativeMethods.SetWindowRgn(h, IntPtr.Zero, true);

                        if (!silent) ForceRepaint(h);
                    }
                    catch { }
                }
                s_Wins.Clear();

                if (s_HMenu != IntPtr.Zero && NativeMethods.GetMenu(s_Hwnd) == IntPtr.Zero)
                    NativeMethods.SetMenu(s_Hwnd, s_HMenu);
                s_HMenu = IntPtr.Zero;

                RestoreStyleBits(s_Hwnd, s_OrigStyle);

                if (s_OldWndProc != IntPtr.Zero)
                    NativeMethods.SetWindowLongPtr(s_Hwnd, GWLP_WNDPROC, s_OldWndProc);
            }
            catch
            {

            }

            Installed = false;
            s_OldWndProc = IntPtr.Zero;
            s_Delegate = null;

            try { NativeMethods.SetWindowRgn(s_Hwnd, IntPtr.Zero, true); } catch { }

            try { ReleaseAllCaptionCaches(); } catch { }
            s_RgnHwnd = IntPtr.Zero;
            s_RgnZoomed = false;

            if (!silent) ForceRepaint(s_Hwnd);
            Log("Unhook 完成 silent=" + silent);
            if (!silent)
                Debug.Log("[Editor Chrome] macOS 标题栏已关闭，原生标题栏已恢复。");
        }

        public static void RepaintCaptionNow()
        {
            if (s_Hwnd == IntPtr.Zero) return;
            try { PaintCaptionForced(s_Hwnd, null); } catch { }
        }

        static void DetachForReload()
        {

            try { MacMenuBar.FreezeForReload(); } catch { }

            try
            {

                IntPtr curtainTarget = s_Hwnd != IntPtr.Zero ? s_Hwnd : UnityMainWindow.Handle;
                Log($"幕布检查 installed={Installed} quitting={s_Quitting} target=0x{curtainTarget.ToInt64():X}");
                if (!s_Quitting && Installed) FreezeCurtain.Show(curtainTarget);
            }
            catch { }

            if (!Installed || s_Hwnd == IntPtr.Zero) return;

            try
            {
                EditorApplication.update -= SyncTick;

                foreach (var kv in s_Wins)
                {
                    IntPtr h = kv.Key;
                    if (h == s_Hwnd) continue;
                    try
                    {
                        if (!NativeMethods.IsWindow(h)) continue;
                        if (kv.Value.OldProc != IntPtr.Zero)
                            NativeMethods.SetWindowLongPtr(h, GWLP_WNDPROC, kv.Value.OldProc);
                        NativeMethods.SetWindowRgn(h, IntPtr.Zero, true);
                    }
                    catch { }
                }
                s_Wins.Clear();

                if (s_OldWndProc != IntPtr.Zero)
                    NativeMethods.SetWindowLongPtr(s_Hwnd, GWLP_WNDPROC, s_OldWndProc);
            }
            catch { }

            Installed = false;
            s_OldWndProc = IntPtr.Zero;
            s_Delegate = null;

            try { ReleaseAllCaptionCaches(); } catch { }
            s_RgnHwnd = IntPtr.Zero;
            s_RgnZoomed = false;

            Log("DetachForReload 完成（保持无边框，未还原原生标题栏）");
        }

        const string k_PrefOrigStyle = "LiquidGlass.MacTitleBar.OrigStyle";

        static void PersistOrigStyle()
        {
            try
            {
                if (s_OrigStyle != 0)
                    EditorPrefs.SetString(k_PrefOrigStyle, s_OrigStyle.ToString("X"));
            }
            catch { }
        }

        static long ReadPersistedOrigStyle()
        {
            try
            {
                string s = EditorPrefs.GetString(k_PrefOrigStyle, "");
                long v;
                if (!string.IsNullOrEmpty(s) &&
                    long.TryParse(s, System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out v))
                    return v;
            }
            catch { }
            return 0;
        }

        static void ClearPersistedOrigStyle()
        {
            try { EditorPrefs.DeleteKey(k_PrefOrigStyle); } catch { }
        }

        static void RestoreStyleBits(IntPtr hWnd, long origStyle)
        {
            if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd)) return;

            long cur = NativeMethods.GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64();
            long added = WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;

            long want = cur | (origStyle & WS_CAPTION);
            want &= ~(added & ~origStyle);

            if (want == cur) return;

            NativeMethods.SetWindowLongPtr(hWnd, GWL_STYLE, (IntPtr)want);
        }

        static void RepairStaleZoom(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd)) return;
            if (!NativeMethods.IsZoomed(hWnd)) return;

            var mi = new NativeMethods.MONITORINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.MONITORINFO))
            };
            IntPtr mon = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (mon == IntPtr.Zero || !NativeMethods.GetMonitorInfo(mon, ref mi)) return;

            int workW = mi.rcWork.Right - mi.rcWork.Left;
            int workH = mi.rcWork.Bottom - mi.rcWork.Top;
            if (workW <= 0 || workH <= 0) return;

            if (!NativeMethods.GetWindowRect(hWnd, out var r)) return;
            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;
            if (w <= 0 || h <= 0) return;

            if (w > workW - 40 && h > workH - 120) return;

            try
            {
                long st = NativeMethods.GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64();
                NativeMethods.SetWindowLongPtr(hWnd, GWL_STYLE, (IntPtr)(st & ~WS_MAXIMIZE));
                Log($"清理脏最大化状态：{w}x{h} @({r.Left},{r.Top}) 样式 0x{st:X} -> 0x{(st & ~WS_MAXIMIZE):X}");
            }
            catch { }
        }

        static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                switch (msg)
                {

                    case WM_GETMINMAXINFO:
                    {
                        IntPtr res = NativeMethods.CallWindowProc(s_OldWndProc, hWnd, msg, wParam, lParam);

                        var mi = new NativeMethods.MONITORINFO
                        {
                            cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.MONITORINFO))
                        };
                        IntPtr mon = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
                        if (mon != IntPtr.Zero && NativeMethods.GetMonitorInfo(mon, ref mi))
                        {
                            int workW = mi.rcWork.Right - mi.rcWork.Left;
                            int workH = mi.rcWork.Bottom - mi.rcWork.Top;
                            int offX = mi.rcWork.Left - mi.rcMonitor.Left;
                            int offY = mi.rcWork.Top - mi.rcMonitor.Top;

                            int bar = s_FrameY + s_BarHeight;

                            if (MacMenuBar.Alive) bar = MacMenuBar.BarHeight;
                            if (workH > bar + 100)
                            {
                                var mmi = (NativeMethods.MINMAXINFO)Marshal.PtrToStructure(
                                    lParam, typeof(NativeMethods.MINMAXINFO));

                                mmi.ptMaxPosition.X = offX;
                                mmi.ptMaxPosition.Y = offY + bar;
                                mmi.ptMaxSize.X = workW;
                                mmi.ptMaxSize.Y = workH - bar;

                                Marshal.StructureToPtr(mmi, lParam, false);
                            }
                        }
                        return res;
                    }

                    case WM_NCCALCSIZE:
                        if (wParam != IntPtr.Zero)
                        {

                            var before = (NativeMethods.NCCALCSIZE_PARAMS)Marshal.PtrToStructure(
                                lParam, typeof(NativeMethods.NCCALCSIZE_PARAMS));
                            int windowLeft = before.rgrc0.Left;
                            int windowTop = before.rgrc0.Top;
                            int windowRight = before.rgrc0.Right;
                            int windowBottom = before.rgrc0.Bottom;

                            IntPtr res = NativeMethods.CallWindowProc(s_OldWndProc, hWnd, msg, wParam, lParam);

                            var p = (NativeMethods.NCCALCSIZE_PARAMS)Marshal.PtrToStructure(
                                lParam, typeof(NativeMethods.NCCALCSIZE_PARAMS));

                            p.rgrc0.Left = windowLeft;
                            p.rgrc0.Right = windowRight;
                            p.rgrc0.Bottom = windowBottom;

                            p.rgrc0.Top = windowTop + s_FrameY + s_BarHeight;

                            Marshal.StructureToPtr(p, lParam, false);

                            return (IntPtr)0x0300;
                        }
                        break;

                    case WM_NCPAINT:
                        if (!s_LoggedNcPaint)
                        {
                            s_LoggedNcPaint = true;
                            Log("WM_NCPAINT 已拦截（不再交给 DefWindowProc 画浅色边框）");
                        }
                        PaintCaptionForced(hWnd, null);
                        return IntPtr.Zero;

                    case WM_NCACTIVATE:
                        PaintCaptionForced(hWnd, wParam != IntPtr.Zero);
                        return (IntPtr)1;

                    case WM_ENTERSIZEMOVE:
                        s_InSizeMove = true;
                        return IntPtr.Zero;

                    case WM_EXITSIZEMOVE:
                        s_InSizeMove = false;
                        PaintCaptionForced(hWnd, null);
                        return IntPtr.Zero;

                    case WM_NCLBUTTONDOWN:
                        if (TryHandleDotClick(hWnd, lParam)) return IntPtr.Zero;
                        break;

                    case WM_SETCURSOR:

                        break;

                    case WM_ERASEBKGND:
                    {

                        IntPtr dc = wParam;
                        if (dc != IntPtr.Zero &&
                            NativeMethods.GetClientRect(hWnd, out var cr))
                        {
                            if (s_BrushErase == IntPtr.Zero)
                                s_BrushErase = NativeMethods.CreateSolidBrush(kEditorBackdrop);
                            if (s_BrushErase != IntPtr.Zero)
                                NativeMethods.FillRect(dc, ref cr, s_BrushErase);
                        }
                        return (IntPtr)1;
                    }

                    case WM_COMMAND:
                        Log($"WM_COMMAND id={(int)(wParam.ToInt64() & 0xFFFF)}");
                        break;

                    case WM_NCHITTEST:
                    {
                        long packed = lParam.ToInt64();
                        int sx = (short)(packed & 0xFFFF);
                        int sy = (short)((packed >> 16) & 0xFFFF);
                        if (!NativeMethods.GetWindowRect(hWnd, out var wr)) break;

                        int x = sx - wr.Left;
                        int y = sy - wr.Top;
                        int w = wr.Right - wr.Left;
                        int h = wr.Bottom - wr.Top;

                        if (NativeMethods.IsZoomed(hWnd))
                        {
                            if (y >= 0 && y < s_FrameY + s_BarHeight) return (IntPtr)HTCAPTION;
                            return (IntPtr)HTCLIENT;
                        }

                        int b = Mathf.Max(4, Mathf.RoundToInt(5f * s_DpiScale));
                        bool left = x < b, right = x >= w - b;
                        bool top = y < b, bottom = y >= h - b;

                        if (top && left) return (IntPtr)HTTOPLEFT;
                        if (top && right) return (IntPtr)HTTOPRIGHT;
                        if (bottom && left) return (IntPtr)HTBOTTOMLEFT;
                        if (bottom && right) return (IntPtr)HTBOTTOMRIGHT;
                        if (left) return (IntPtr)HTLEFT;
                        if (right) return (IntPtr)HTRIGHT;
                        if (top) return (IntPtr)HTTOP;
                        if (bottom) return (IntPtr)HTBOTTOM;

                        if (y >= 0 && y < s_FrameY + s_BarHeight) return (IntPtr)HTCAPTION;

                        return (IntPtr)HTCLIENT;
                    }
                }

                IntPtr result = NativeMethods.CallWindowProc(s_OldWndProc, hWnd, msg, wParam, lParam);

                if (msg == WM_PAINT) PaintCaption(hWnd);

                if (msg == WM_SETTEXT || msg == WM_SIZE || msg == WM_SYSCOMMAND ||
                    msg == WM_ACTIVATE)
                {
                    if (msg == WM_SIZE) ApplyRoundRegion(hWnd);
                    if (msg == WM_ACTIVATE)
                        PaintCaptionForced(hWnd, (wParam.ToInt64() & 0xFFFF) != 0);
                    else
                        PaintCaptionForced(hWnd, null);
                }

                if (msg == WM_SIZE)
                {
                    RepaintEdges(hWnd, sync: !NativeMethods.IsZoomed(hWnd));
                }

                return result;
            }
            catch
            {

                try { return NativeMethods.CallWindowProc(s_OldWndProc, hWnd, msg, wParam, lParam); }
                catch { return IntPtr.Zero; }
            }
        }

        static bool TryHandleDotClick(IntPtr hWnd, IntPtr lParam)
        {
            long packed = lParam.ToInt64();
            int screenX = (short)(packed & 0xFFFF);
            int screenY = (short)((packed >> 16) & 0xFFFF);

            if (!NativeMethods.GetWindowRect(hWnd, out var wr)) return false;

            int x = screenX - wr.Left;
            int y = screenY - wr.Top;

            int dot = HitTestDot(hWnd, x, y);
            if (dot < 0) return false;

            int command = dot == 0 ? SC_CLOSE
                        : dot == 1 ? SC_MINIMIZE
                        : (NativeMethods.IsZoomed(hWnd) ? SC_RESTORE : SC_MAXIMIZE);

            NativeMethods.PostMessage(hWnd, WM_SYSCOMMAND, (IntPtr)command, IntPtr.Zero);
            return true;
        }

        static void RepaintEdges(IntPtr hWnd, bool sync)
        {
            try
            {
                if (!NativeMethods.GetWindowRect(hWnd, out var sz)) return;

                int cw = sz.Right - sz.Left;
                int ch = sz.Bottom - sz.Top;
                if (cw <= 0 || ch <= 0) return;

                int ew = Mathf.Max(4, Mathf.RoundToInt(8f * s_DpiScale));
                int top = s_FrameY + s_BarHeight;
                if (ch - top < ew) return;

                uint flags = NativeMethods.RDW_INVALIDATE |
                             NativeMethods.RDW_ALLCHILDREN;

                if (sync) flags |= NativeMethods.RDW_UPDATENOW;

                var rcL = new NativeMethods.RECT { Left = 0, Top = top, Right = ew, Bottom = ch };
                NativeMethods.RedrawWindowRect(hWnd, ref rcL, IntPtr.Zero, flags);
                var rcR = new NativeMethods.RECT { Left = cw - ew, Top = top, Right = cw, Bottom = ch };
                NativeMethods.RedrawWindowRect(hWnd, ref rcR, IntPtr.Zero, flags);
                var rcB = new NativeMethods.RECT { Left = 0, Top = ch - ew, Right = cw, Bottom = ch };
                NativeMethods.RedrawWindowRect(hWnd, ref rcB, IntPtr.Zero, flags);
            }
            catch { }
        }

        static void PaintCaption(IntPtr hWnd)
        {
            PaintCaption(hWnd, null, false);
        }

        static void PaintCaptionForced(IntPtr hWnd, bool? activeOverride)
        {
            PaintCaption(hWnd, activeOverride, true);
        }

        static bool IsOurProcessForeground()
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            NativeMethods.GetWindowThreadProcessId(fg, out uint pid);
            return pid == OurPid();
        }

        static void PaintCaption(IntPtr hWnd, bool? activeOverride, bool force)
        {
            if (s_Painting || !Installed) return;

            if (s_InSizeMove && !force) return;

            bool rawActive = IsOurProcessForeground() || MacMenuBar.MenuOpen || ForceActive;
            if (activeOverride.HasValue && activeOverride.Value) rawActive = true;
            bool active = ResolveActive(rawActive);

            double now = EditorApplication.timeSinceStartup;

            if (!NativeMethods.GetWindowRect(hWnd, out var wr)) return;
            int width = wr.Right - wr.Left;
            int height = wr.Bottom - wr.Top;
            if (width <= 0 || height <= 0) return;

            var origin = new NativeMethods.POINT { X = 0, Y = 0 };
            if (!NativeMethods.ClientToScreen(hWnd, ref origin)) return;

            int barBottom = origin.Y - wr.Top;
            int barTop = s_FrameY;
            int barH = barBottom - barTop;
            if (barH <= 0 || barBottom > height) return;

            int titleLen = NativeMethods.GetWindowTextLength(hWnd);

            if (!s_Captions.TryGetValue(hWnd, out var cc))
            {
                cc = new CaptionCache();
                s_Captions[hWnd] = cc;
            }

            bool same = cc.HasContent &&
                        cc.CW == width && cc.CBarBottom == barBottom &&
                        cc.CTitleLen == titleLen && cc.CActive == active &&
                        cc.CDebug == DebugPaint;

            if (same && !force && now - cc.LastBlitAt < 1.0) return;

            s_Painting = true;
            try
            {
                if (!EnsureSurface(cc, width, barBottom)) return;

                if (!same)
                {
                    PaintIntoCache(cc, hWnd, width, barBottom, barTop, barH, active);
                    cc.HasContent = true;
                    cc.CW = width;
                    cc.CBarBottom = barBottom;
                    cc.CTitleLen = titleLen;
                    cc.CActive = active;
                    cc.CDebug = DebugPaint;
                }

                IntPtr dc = NativeMethods.GetWindowDC(hWnd);
                if (dc == IntPtr.Zero) return;
                try
                {

                    NativeMethods.BitBlt(dc, 0, 0, width, barBottom, cc.MemDC, 0, 0, SRCCOPY);
                    cc.LastBlitAt = now;
                    PaintCount++;
                }
                finally
                {
                    NativeMethods.ReleaseDC(hWnd, dc);
                }
            }
            catch
            {

            }
            finally
            {
                s_Painting = false;
            }
        }

        static bool EnsureSurface(CaptionCache cc, int w, int h)
        {
            if (cc.MemDC != IntPtr.Zero && cc.W == w && cc.H == h) return true;

            if (cc.MemDC != IntPtr.Zero)
            {
                if (cc.OldBmp != IntPtr.Zero) NativeMethods.SelectObject(cc.MemDC, cc.OldBmp);
                NativeMethods.DeleteDC(cc.MemDC);
                cc.MemDC = IntPtr.Zero;
            }
            if (cc.Bmp != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(cc.Bmp);
                cc.Bmp = IntPtr.Zero;
            }

            IntPtr screen = NativeMethods.GetDC(IntPtr.Zero);
            if (screen == IntPtr.Zero) return false;
            try
            {
                cc.MemDC = NativeMethods.CreateCompatibleDC(screen);
                cc.Bmp = NativeMethods.CreateCompatibleBitmap(screen, w, h);
            }
            finally
            {
                NativeMethods.ReleaseDC(IntPtr.Zero, screen);
            }

            if (cc.MemDC == IntPtr.Zero || cc.Bmp == IntPtr.Zero) return false;
            cc.OldBmp = NativeMethods.SelectObject(cc.MemDC, cc.Bmp);
            cc.W = w;
            cc.H = h;
            cc.HasContent = false;
            return true;
        }

        static void PaintIntoCache(CaptionCache cc, IntPtr hWnd, int width, int barBottom,
                                   int barTop, int barH, bool active)
        {
            uint bg = DebugPaint ? 0xFF00FFu : (active ? 0x2C2C2Cu : 0x242424u);
            IntPtr brush = GetBgBrush(bg);
            var rect = new NativeMethods.RECT { Left = 0, Top = 0, Right = width, Bottom = barBottom };
            NativeMethods.FillRect(cc.MemDC, ref rect, brush);

            float scale = s_DpiScale;
            DrawDots(cc.MemDC, barTop, barH, active, scale, bg);
            DrawTitle(cc.MemDC, hWnd, width, barTop, barH, active, scale);
        }

        static void DrawDots(IntPtr dc, int barTop, int barH, bool active, float scale, uint bgColor)
        {
            int radius = Mathf.Max(4, Mathf.RoundToInt(6f * scale));
            int spacing = Mathf.Max(12, Mathf.RoundToInt(20f * scale));
            int firstX = Mathf.RoundToInt(14f * scale);

            int cy = barTop + barH / 2;
            var colors = (active || DebugPaint) ? s_DotColors : s_DotColorsInactive;

            for (int i = 0; i < 3; i++)
            {
                int cx = firstX + i * spacing;
                FillCircleAA(dc, cx, cy, radius, bgColor, colors[i]);
            }
        }

        static void FillCircleAA(IntPtr dc, int cx, int cy, int radius, uint bgColor, uint fgColor)
        {
            const int SS = 4;
            const float SS2 = SS * SS;
            int r2 = radius * radius;

            for (int y = cy - radius; y <= cy + radius; y++)
            {
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < SS; sy++)
                    {
                        float py = y + (sy + 0.5f) / SS - cy;
                        float py2 = py * py;
                        if (py2 > r2) continue;

                        for (int sx = 0; sx < SS; sx++)
                        {
                            float px = x + (sx + 0.5f) / SS - cx;
                            if (px * px + py2 <= r2) hits++;
                        }
                    }

                    if (hits == 0) continue;

                    if (hits == SS2) NativeMethods.SetPixelV(dc, x, y, fgColor);
                    else NativeMethods.SetPixelV(dc, x, y, BlendColor(bgColor, fgColor, hits / SS2));
                }
            }
        }

        static uint BlendColor(uint bg, uint fg, float t)
        {
            int br = (int)(bg & 0xFF), bgG = (int)((bg >> 8) & 0xFF), bb = (int)((bg >> 16) & 0xFF);
            int fr = (int)(fg & 0xFF), fgG = (int)((fg >> 8) & 0xFF), fb = (int)((fg >> 16) & 0xFF);

            int r = (int)(br + (fr - br) * t + 0.5f);
            int g = (int)(bgG + (fgG - bgG) * t + 0.5f);
            int b = (int)(bb + (fb - bb) * t + 0.5f);

            if (r < 0) r = 0; else if (r > 255) r = 255;
            if (g < 0) g = 0; else if (g > 255) g = 255;
            if (b < 0) b = 0; else if (b > 255) b = 255;

            return (uint)(r | (g << 8) | (b << 16));
        }

        static void DrawTitle(IntPtr dc, IntPtr hWnd, int width, int barTop, int barH, bool active, float scale)
        {
            int length = NativeMethods.GetWindowTextLength(hWnd);
            if (length <= 0) return;

            var buffer = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(hWnd, buffer, buffer.Capacity);

            string title = ShortenTitle(buffer.ToString());

            int fontHeight = -Mathf.Max(9, Mathf.RoundToInt(12f * scale));

            IntPtr font = GetTitleFont(fontHeight);
            IntPtr oldFont = font != IntPtr.Zero ? NativeMethods.SelectObject(dc, font) : IntPtr.Zero;

            NativeMethods.SetBkMode(dc, TRANSPARENT);
            NativeMethods.SetTextColor(dc, active ? 0xDCDCDCu : 0x9A9A9Au);

            int sideMargin = Mathf.RoundToInt(90f * scale);
            var rect = new NativeMethods.RECT
            {
                Left = sideMargin,
                Top = barTop,
                Right = width - sideMargin,
                Bottom = barTop + barH
            };

            NativeMethods.DrawText(dc, title, title.Length, ref rect,
                DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX);

            NativeMethods.SelectObject(dc, oldFont);
        }

        static string ShortenTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "Unity";

            int first = title.IndexOf(" - ", StringComparison.Ordinal);
            if (first < 0) return title;

            int second = title.IndexOf(" - ", first + 3, StringComparison.Ordinal);
            return second < 0 ? title : title.Substring(0, second);
        }

        static int HitTestDot(IntPtr hWnd, int x, int y)
        {
            if (!NativeMethods.GetWindowRect(hWnd, out var wr)) return -1;

            var origin = new NativeMethods.POINT { X = 0, Y = 0 };
            if (!NativeMethods.ClientToScreen(hWnd, ref origin)) return -1;

            int barBottom = origin.Y - wr.Top;
            if (barBottom <= s_FrameY) return -1;
            if (y < 0 || y > barBottom) return -1;

            float scale = s_DpiScale;
            int radius = Mathf.RoundToInt(9f * scale);
            int spacing = Mathf.Max(12, Mathf.RoundToInt(20f * scale));
            int firstX = Mathf.RoundToInt(14f * scale);
            int cy = s_FrameY + (barBottom - s_FrameY) / 2;

            for (int i = 0; i < 3; i++)
            {
                int cx = firstX + i * spacing;
                int dx = x - cx;
                int dy = y - cy;
                if (dx * dx + dy * dy <= radius * radius) return i;
            }
            return -1;
        }

        static void ForceRepaint(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            ApplyRoundRegion(hWnd);

            NativeMethods.RedrawWindow(hWnd, IntPtr.Zero, IntPtr.Zero,
                NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_FRAME |
                NativeMethods.RDW_ALLCHILDREN);

            NativeMethods.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_FRAMECHANGED);
        }

        static IntPtr s_RgnHwnd;
        static int s_RgnW, s_RgnH;
        static bool s_RgnZoomed;

        static void ApplyRoundRegion(IntPtr hWnd)
        {
            if (!Installed || hWnd == IntPtr.Zero) return;

            bool zoomed = NativeMethods.IsZoomed(hWnd);

            if (zoomed)
            {
                if (s_RgnHwnd == hWnd && s_RgnZoomed) return;
                NativeMethods.SetWindowRgn(hWnd, IntPtr.Zero, false);
                s_RgnHwnd = hWnd;
                s_RgnZoomed = true;
                return;
            }

            if (!NativeMethods.GetWindowRect(hWnd, out var wr)) return;

            int w = wr.Right - wr.Left;
            int h = wr.Bottom - wr.Top;
            if (w <= 0 || h <= 0) return;

            if (s_RgnHwnd == hWnd && !s_RgnZoomed && s_RgnW == w && s_RgnH == h) return;

            int r = Mathf.Max(2, Mathf.RoundToInt(kCornerRadius * s_DpiScale)) * 2;

            IntPtr rgn = NativeMethods.CreateRoundRectRgn(0, 0, w + 1, h + 1, r, r);
            if (rgn == IntPtr.Zero) return;

            int n = Mathf.RoundToInt(6f * s_DpiScale);
            ExtendRegion(rgn, w, h, n);

            if (NativeMethods.SetWindowRgn(hWnd, rgn, true) == 0)
                NativeMethods.DeleteObject(rgn);

            s_RgnHwnd = hWnd;
            s_RgnZoomed = false;
            s_RgnW = w;
            s_RgnH = h;
        }

        static void ExtendRegion(IntPtr rgn, int w, int h, int n)
        {
            if (n <= 0 || rgn == IntPtr.Zero) return;

            NativeMethods.CombineRgn(rgn, rgn, NativeMethods.CreateRectRgn(-n, 0, 0, h + 1), 2);
            NativeMethods.CombineRgn(rgn, rgn, NativeMethods.CreateRectRgn(w + 1, 0, w + 1 + n, h + 1), 2);
            NativeMethods.CombineRgn(rgn, rgn, NativeMethods.CreateRectRgn(0, -n, w + 1, 0), 2);
            NativeMethods.CombineRgn(rgn, rgn, NativeMethods.CreateRectRgn(0, h + 1, w + 1, h + 1 + n), 2);
        }

        static void NormalizeWindowRect(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            try
            {
                if (!NativeMethods.GetWindowRect(hWnd, out var r)) return;

                NativeMethods.SetWindowPos(hWnd, IntPtr.Zero,
                    r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                    NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
            }
            catch (Exception e)
            {
                Log("NormalizeWindowRect 失败：" + e.Message);
            }
        }

        public static void DebugZoom(bool maximize)
        {
            IntPtr h = UnityMainWindow.Handle;
            if (h == IntPtr.Zero) return;
            NativeMethods.ShowWindow(h, maximize ? 3   : 9  );
        }

        public static void ForcePaint()
        {
            ForceRepaint(UnityMainWindow.Handle);
            PaintCaption(UnityMainWindow.Handle);
        }

        public static string BringToFront()
        {
            var hwnd = UnityMainWindow.Handle;
            if (hwnd == IntPtr.Zero) return "FAIL";

            NativeMethods.ShowWindow(hwnd, 9);

            IntPtr fg = NativeMethods.GetForegroundWindow();
            uint fgThread = NativeMethods.GetWindowThreadProcessId(fg, out _);
            uint myThread = NativeMethods.GetCurrentThreadId();

            bool attached = false;
            try
            {
                if (fgThread != 0 && fgThread != myThread)
                    attached = NativeMethods.AttachThreadInput(myThread, fgThread, true);

                NativeMethods.SetForegroundWindow(hwnd);
                NativeMethods.BringWindowToTop(hwnd);
            }
            finally
            {
                if (attached)
                    NativeMethods.AttachThreadInput(myThread, fgThread, false);
            }

            System.Threading.Thread.Sleep(400);
            ForceRepaint(hwnd);
            System.Threading.Thread.Sleep(200);
            return Describe() + " | fg=" + (NativeMethods.GetForegroundWindow() == hwnd);
        }

        public static string DumpMenu()
        {
            var hwnd = UnityMainWindow.Handle;
            if (hwnd == IntPtr.Zero) return "FAIL: 无窗口";

            IntPtr hMenu = NativeMethods.GetMenu(hwnd);
            if (hMenu == IntPtr.Zero)
                return "GetMenu = NULL —— Unity 没有原生菜单栏（是自绘的）";

            int count = NativeMethods.GetMenuItemCount(hMenu);
            var sb = new StringBuilder();
            sb.Append("hMenu=0x").Append(hMenu.ToInt64().ToString("X")).Append(" count=").Append(count).Append(" | ");

            for (int i = 0; i < count; i++)
            {
                var buf = new StringBuilder(128);
                NativeMethods.GetMenuString(hMenu, (uint)i, buf, 128, 0x0400);
                IntPtr sub = NativeMethods.GetSubMenu(hMenu, i);
                sb.Append('[').Append(i).Append(']').Append(buf.ToString().Replace("&", ""))
                  .Append("(sub=").Append(sub != IntPtr.Zero ? "y" : "n").Append(") ");
            }
            return sb.ToString();
        }

        public static string Describe()
        {
            var hwnd = s_Hwnd != IntPtr.Zero ? s_Hwnd : UnityMainWindow.Handle;
            var sb = new StringBuilder();
            sb.Append("installed=").Append(Installed);
            sb.Append(" hwnd=0x").Append(hwnd.ToInt64().ToString("X"));
            sb.Append(" paintCount=").Append(PaintCount);
            sb.Append(" barH=").Append(s_BarHeight);
            sb.Append(" dpi=").Append(s_DpiScale.ToString("0.##"));
            sb.Append(" menu=0x").Append(s_HMenu.ToInt64().ToString("X"));

            if (hwnd != IntPtr.Zero && NativeMethods.GetWindowRect(hwnd, out var wr))
            {
                var origin = new NativeMethods.POINT { X = 0, Y = 0 };
                NativeMethods.ClientToScreen(hwnd, ref origin);
                sb.Append(" realCaptionH=").Append(origin.Y - wr.Top);
                sb.Append(" rect=").Append(wr.Left).Append(',').Append(wr.Top)
                  .Append(' ').Append(wr.Right - wr.Left).Append('x').Append(wr.Bottom - wr.Top);
                sb.Append(" style=0x").Append(
                    NativeMethods.GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64().ToString("X"));
                sb.Append(" zoomed=").Append(NativeMethods.IsZoomed(hwnd));
            }
            return sb.ToString();
        }

        [MenuItem("Tools/Editor Chrome/Restore Native Frame (fix broken window)", false, 20)]
        static void RestoreNativeFrameMenu()
        {
            Debug.Log("[Editor Chrome] " + RestoreNativeFrame());
        }

        public static string RestoreNativeFrame()
        {
            EditorPrefs.SetBool(k_PrefKey, false);

            long orig = s_OrigStyle != 0 ? s_OrigStyle : ReadPersistedOrigStyle();
            IntPtr hwnd = s_Hwnd != IntPtr.Zero ? s_Hwnd : UnityMainWindow.Handle;

            if (hwnd != IntPtr.Zero && orig != 0)
            {
                NativeMethods.SetWindowLongPtr(hwnd, GWL_STYLE, (IntPtr)orig);
                ForceRepaint(hwnd);
            }
            Unhook(silent: false);
            try { FreezeCurtain.Hide(); } catch { }
            ClearPersistedOrigStyle();
            return Describe();
        }

        static class NativeMethods
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct RECT { public int Left, Top, Right, Bottom; }

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT { public int X, Y; }

            [StructLayout(LayoutKind.Sequential)]
            public struct NCCALCSIZE_PARAMS
            {
                public RECT rgrc0;
                public RECT rgrc1;
                public RECT rgrc2;
                public IntPtr lppos;
            }

            [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
            public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

            [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
            public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll")]
            public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern IntPtr GetWindowDC(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

            [DllImport("gdi32.dll")]
            public static extern bool BitBlt(IntPtr hdcDest, int x, int y, int w, int h,
                IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

            [DllImport("gdi32.dll")]
            public static extern bool DeleteDC(IntPtr hdc);

            [DllImport("user32.dll")]
            public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

            [DllImport("user32.dll")]
            public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

            [DllImport("user32.dll")]
            public static extern bool IsZoomed(IntPtr hWnd);

            [StructLayout(LayoutKind.Sequential)]
            public struct MINMAXINFO
            {
                public POINT ptReserved;
                public POINT ptMaxSize;
                public POINT ptMaxPosition;
                public POINT ptMinTrackSize;
                public POINT ptMaxTrackSize;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MONITORINFO
            {
                public uint cbSize;
                public RECT rcMonitor;
                public RECT rcWork;
                public uint dwFlags;
            }

            [DllImport("user32.dll")]
            public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

            [DllImport("user32.dll")]
            public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

            public const uint MONITOR_DEFAULTTONEAREST = 2;

            [DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

            [DllImport("user32.dll")]
            public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

            [DllImport("user32.dll")]
            public static extern bool BringWindowToTop(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            [DllImport("kernel32.dll")]
            public static extern uint GetCurrentThreadId();

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr LoadLibrary(string lpFileName);

            [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

            [DllImport("user32.dll")]
            public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int GetWindowTextLength(IntPtr hWnd);

            public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool IsWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

            [DllImport("kernel32.dll")]
            public static extern uint GetCurrentProcessId();

            [DllImport("user32.dll")]
            public static extern IntPtr SetCursor(IntPtr hCursor);

            [DllImport("user32.dll")]
            public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

            [DllImport("user32.dll")]
            public static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

            [DllImport("user32.dll", EntryPoint = "RedrawWindow")]
            public static extern bool RedrawWindowRect(IntPtr hWnd, ref RECT rcUpdate, IntPtr hrgnUpdate, uint flags);

            [DllImport("user32.dll")]
            public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

            [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")]
            public static extern IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

            [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
            public static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

            public const int GCLP_HBRBACKGROUND = -10;
            public const int GCLP_HCURSOR = -12;
            public const int GCL_STYLE = -26;

            public const int CS_VREDRAW = 0x0001;
            public const int CS_HREDRAW = 0x0002;
            public const int CS_DBLCLKS = 0x0008;

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

            [DllImport("user32.dll")]
            public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                int x, int y, int cx, int cy, uint flags);

            [DllImport("user32.dll")]
            public static extern int GetSystemMetrics(int index);

            public const uint RDW_INVALIDATE = 0x0001;
            public const uint RDW_ERASE = 0x0004;
            public const uint RDW_UPDATENOW = 0x0100;
            public const uint RDW_ALLCHILDREN = 0x0080;
            public const uint RDW_FRAME = 0x0400;

            public const uint SWP_NOSIZE = 0x0001;
            public const uint SWP_NOMOVE = 0x0002;
            public const uint SWP_NOZORDER = 0x0004;
            public const uint SWP_NOACTIVATE = 0x0010;
            public const uint SWP_FRAMECHANGED = 0x0020;

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateSolidBrush(uint color);

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreatePen(int style, int width, uint color);

            [DllImport("gdi32.dll")]
            public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

            [DllImport("gdi32.dll")]
            public static extern bool DeleteObject(IntPtr obj);

            [DllImport("gdi32.dll")]
            public static extern bool Ellipse(IntPtr hdc, int left, int top, int right, int bottom);

            [DllImport("gdi32.dll")]
            public static extern uint GetPixel(IntPtr hdc, int x, int y);

            [DllImport("gdi32.dll")]
            public static extern int SetPixelV(IntPtr hdc, int x, int y, uint crColor);

            [DllImport("user32.dll")]
            public static extern int FillRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);

            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr CreateFont(int height, int width, int escapement, int orientation,
                int weight, uint italic, uint underline, uint strikeOut, uint charSet,
                uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string faceName);

            [DllImport("gdi32.dll")]
            public static extern uint SetTextColor(IntPtr hdc, uint color);

            [DllImport("gdi32.dll")]
            public static extern int SetBkMode(IntPtr hdc, int mode);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int DrawText(IntPtr hdc, string text, int length, ref RECT rect, uint flags);

            [DllImport("dwmapi.dll")]
            public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

            [DllImport("user32.dll")]
            public static extern IntPtr GetMenu(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool SetMenu(IntPtr hWnd, IntPtr hMenu);

            [DllImport("user32.dll")]
            public static extern int GetMenuItemCount(IntPtr hMenu);

            [DllImport("user32.dll")]
            public static extern IntPtr GetSubMenu(IntPtr hMenu, int nPos);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int GetMenuString(IntPtr hMenu, uint uIDItem, StringBuilder lpString, int nMaxCount, uint uFlag);

            [DllImport("user32.dll")]
            public static extern uint GetDpiForWindow(IntPtr hWnd);

            [DllImport("gdi32.dll")]
            public static extern int GetDeviceCaps(IntPtr hdc, int index);

            [DllImport("user32.dll")]
            public static extern IntPtr GetDC(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int w, int h);

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

            [DllImport("gdi32.dll")]
            public static extern int CombineRgn(IntPtr hrgnDst, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int fnMode);
        }
    }
}
