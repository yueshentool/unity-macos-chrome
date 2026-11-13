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

    public static class MacMenuBar
    {
        const string kClassName = "LiquidGlassMacMenuBar";
        const string kBarTitle = "LiquidGlassMenuBar";

        const int WM_PAINT = 0x000F;
        const int WM_ERASEBKGND = 0x0014;
        const int WM_MOUSEMOVE = 0x0200;
        const int WM_LBUTTONUP = 0x0202;
        const int WM_MOUSELEAVE = 0x02A3;
        const int WM_NULL = 0x0000;
        const int WM_SETCURSOR = 0x0020;

        const uint WS_POPUP = 0x80000000;
        const uint WS_VISIBLE = 0x10000000;

        const uint WS_EX_TOPMOST = 0x00000008;
        const uint WS_EX_TOOLWINDOW = 0x00000080;
        const uint WS_EX_LAYERED = 0x00080000;
        const uint WS_EX_NOACTIVATE = 0x08000000;

        const int SW_HIDE = 0;
        const int SW_SHOWNOACTIVATE = 4;

        const uint TME_LEAVE = 0x00000002;

        const uint TPM_LEFTALIGN = 0x0000;
        const uint TPM_TOPALIGN = 0x0000;
        const uint TPM_RETURNCMD = 0x0100;

        const uint WM_COMMAND = 0x0111;

        const uint MF_BYPOSITION = 0x0400;
        const uint MONITOR_DEFAULTTONEAREST = 2;

        const int IDC_ARROW = 32512;

        const float kBarHeight = 24f;

        const string kOwnMenuTitle = "Liquid Glass";

        const int kCmdOpenSettings = 0x4C00;
        const int kCmdWhitelistAdd = 0x4C01;
        const int kCmdRemoveBase = 0x4C20;
        const int kCmdRemoveMax = 0x4C40;
        const int kCmdPopupAuto = 0x4C50;
        const int kCmdPopupCustom = 0x4C53;
        const int kCmdBarAuto = 0x4C60;
        const int kCmdBarCustom = 0x4C64;
        const int kCmdOwnMin = 0x4C00;
        const int kCmdOwnMax = 0x4CFF;

        const float kFontSize = 12f;

        const uint kAcrylicTint = 0x5A141414;

        struct UiPalette
        {
            public bool light;
            public float barAlpha;
            public uint barBg;
            public uint barText;
            public uint barHover;

            public uint barHoverFill;
            public uint barLine;

            public uint popupBg;
            public uint popupText;
            public uint popupTextHover;
            public uint popupHover;

            public uint popupHoverFill;
            public uint popupSep;
            public uint popupBorder;
        }

        static bool s_SysLight;
        static double s_SysLightAt = -1;

        static bool SystemUsesLightTheme()
        {
            double now = EditorApplication.timeSinceStartup;
            if (s_SysLightAt > 0 && now - s_SysLightAt < 1.0) return s_SysLight;

            s_SysLightAt = now;
            try
            {
                object v = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 0);
                s_SysLight = Convert.ToInt32(v) != 0;
            }
            catch
            {
                s_SysLight = false;
            }
            return s_SysLight;
        }

        static uint ToColorRef(Color c)
        {
            uint r = (uint)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
            uint g = (uint)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
            uint b = (uint)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
            return (b << 16) | (g << 8) | r;
        }

        static float Luminance(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        static UiPalette CurrentPalette(bool barFullscreen)
        {
            return CurrentPalette(barFullscreen, MenuOpen);
        }

        static UiPalette CurrentPalette(bool barFullscreen, bool menuOpen)
        {
            var s = LiquidGlassSettings.instance;
            bool sysLight = SystemUsesLightTheme();
            var p = new UiPalette();

            int barMode = s.menuBarBgMode;
            if (barMode == (int)MenuBarBgMode.Auto)
                barMode = barFullscreen
                    ? (sysLight ? (int)MenuBarBgMode.Light : (int)MenuBarBgMode.Dark)
                    : (int)MenuBarBgMode.Transparent;

            switch (barMode)
            {
                case (int)MenuBarBgMode.Light:
                    p.light = true;
                    p.barAlpha = 1f;
                    p.barBg = 0x00F3F3F3u;
                    p.barText = 0x00202020u;
                    p.barHover = Pre(0x00000000u, 0.09f);
                    p.barHoverFill = 0x00E0E0E0u;
                    p.barLine = Pre(0x00000000u, 0.14f);
                    break;

                case (int)MenuBarBgMode.Dark:
                    p.light = false;
                    p.barAlpha = 1f;

                    p.barBg = 0x002C2C2Cu;
                    p.barText = 0x00EDEDEDu;
                    p.barHover = Pre(0x00FFFFFFu, 0.14f);
                    p.barHoverFill = 0x003E3E3Eu;
                    p.barLine = Pre(0x00FFFFFFu, 0.10f);
                    break;

                case (int)MenuBarBgMode.Custom:
                {
                    var c = s.menuBarCustomColor;
                    bool lightBg = Luminance(c) > 0.55f;
                    p.light = lightBg;
                    p.barAlpha = 1f;
                    p.barBg = ToColorRef(c);
                    p.barText = lightBg ? 0x00202020u : 0x00EDEDEDu;
                    p.barHover = lightBg ? Pre(0x00000000u, 0.09f) : Pre(0x00FFFFFFu, 0.14f);
                    p.barHoverFill = lightBg ? 0x00E0E0E0u : 0x003E3E3Eu;
                    p.barLine = lightBg ? Pre(0x00000000u, 0.14f) : Pre(0x00FFFFFFu, 0.10f);
                    break;
                }

                default:
                    p.light = false;
                    p.barAlpha = 0f;
                    p.barBg = 0;
                    p.barText = 0x00FFFFFFu;
                    p.barHover = Pre(0x00FFFFFFu, 0.20f);

                    p.barHoverFill = 0x003A3A3Au;
                    p.barLine = Pre(0x00FFFFFFu, 0.10f);
                    break;
            }

            int popMode = s.popupThemeMode;
            if (popMode == (int)PopupThemeMode.Auto)
                popMode = sysLight ? (int)PopupThemeMode.Light : (int)PopupThemeMode.Dark;

            switch (popMode)
            {
                case (int)PopupThemeMode.Light:
                    p.popupBg = 0x00FAFAFAu;
                    p.popupText = 0x00202020u;
                    p.popupTextHover = 0x00000000u;
                    p.popupHover = 0x00E4E4E4u;

                    p.popupHoverFill = 0x001E88E5u;
                    p.popupSep = 0x00DCDCDCu;
                    p.popupBorder = 0x00C9C9C9u;
                    break;

                case (int)PopupThemeMode.Custom:
                {
                    var c = s.popupCustomColor;
                    bool lightBg = Luminance(c) > 0.55f;
                    p.popupBg = ToColorRef(c);
                    p.popupText = lightBg ? 0x00202020u : 0x00E8E8E8u;
                    p.popupTextHover = 0x00FFFFFFu;
                    p.popupHover = lightBg ? 0x00D8D8D8u : Pre(0x00FFFFFFu, 0.18f);

                    p.popupHoverFill = 0x001E88E5u;
                    p.popupSep = lightBg ? 0x00CFCFCFu : Pre(0x00FFFFFFu, 0.16f);
                    p.popupBorder = lightBg ? 0x00BFBFBFu : Pre(0x00FFFFFFu, 0.22f);
                    break;
                }

                default:

                    p.popupBg = 0x002C2C2Cu;
                    p.popupText = 0x00FFFFFFu;
                    p.popupTextHover = 0x00FFFFFFu;
                    p.popupHover = Pre(0x00FFFFFFu, 0.12f);

                    p.popupHoverFill = 0x002C5D87u;
                    p.popupSep = 0x003E3E3Eu;
                    p.popupBorder = 0x003E3E3Eu;
                    break;
            }

            return p;
        }

        static uint GdiToRgb(uint c)
        {
            return ((c & 0xFFu) << 16) | (c & 0x00FF00u) | ((c >> 16) & 0xFFu);
        }

        static bool EditorFullscreen()
        {
            if (s_UnityHwnd == IntPtr.Zero) return false;
            if (NativeMethods.IsZoomed(s_UnityHwnd)) return true;

            NativeMethods.RECT wr;
            if (!NativeMethods.GetWindowRect(s_UnityHwnd, out wr)) return false;

            var mi = new NativeMethods.MONITORINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.MONITORINFO))
            };
            IntPtr mon = NativeMethods.MonitorFromWindow(s_UnityHwnd, MONITOR_DEFAULTTONEAREST);
            if (mon == IntPtr.Zero || !NativeMethods.GetMonitorInfo(mon, ref mi)) return false;

            int bar = s_BarH > 0 ? s_BarH : Mathf.RoundToInt(kBarHeight * (s_Scale > 0 ? s_Scale : 1f));
            int workW = mi.rcWork.Right - mi.rcWork.Left;
            int workH = mi.rcWork.Bottom - mi.rcWork.Top;
            int w = wr.Right - wr.Left;
            int h = wr.Bottom - wr.Top;

            const int kTol = 2;
            return Mathf.Abs(w - workW) <= kTol
                && Mathf.Abs(wr.Top - (mi.rcWork.Top + bar)) <= kTol
                && Mathf.Abs(h - (workH - bar)) <= kTol;
        }

        public static void RefreshAppearance()
        {
            if (s_Hwnd == IntPtr.Zero) return;
            var st = LiquidGlassSettings.instance;
            MacTitleBar.Log($"RefreshAppearance barMode={st.menuBarBgMode} popupMode={st.popupThemeMode} zoom={EditorFullscreen()}");
            RenderBar(s_Hwnd);
            NativeMethods.InvalidateRect(s_Hwnd, IntPtr.Zero, false);
        }

        delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        static WndProcDelegate s_Delegate;
        static IntPtr s_Hwnd;
        static IntPtr s_UnityHwnd;
        static IntPtr s_HMenu;
        static bool s_Visible;
        static int s_Hover = -1;

        static bool s_WantPending;
        static double s_WantSince = -1;
        static float s_Scale = 1f;
        static int s_BarH = 24;

        const int kBarOverlap = 1;

        static int s_FreezeExtra;
        static bool s_Acrylic;
        static double s_NextRefresh;

        static bool s_LastZoom;
        static float s_LastBarAlpha = -1f;

        static string[] s_Titles = new string[0];

        static int s_RealCount;

        static IntPtr s_OwnMenu;
        static int[] s_ItemLeft = new int[0];
        static int[] s_ItemRight = new int[0];
        static int[] s_ItemTextX = new int[0];
        static int s_AppW;
        static int s_PadX;
        static int s_ItemPad;
        static int s_GapAfterApp;

        public static bool Alive => s_Hwnd != IntPtr.Zero;

        static IntPtr s_NativeDefProc;

        static IntPtr FindOwnBarWindow()
        {
            IntPtr found = IntPtr.Zero;
            uint self = NativeMethods.GetCurrentProcessId();
            var sb = new StringBuilder(64);

            try
            {
                NativeMethods.EnumWindows((h, _) =>
                {
                    NativeMethods.GetWindowThreadProcessId(h, out uint pid);
                    if (pid != self) return true;

                    sb.Length = 0;
                    if (NativeMethods.GetClassName(h, sb, sb.Capacity) <= 0) return true;
                    if (sb.ToString() != kClassName) return true;

                    found = h;
                    return false;
                }, IntPtr.Zero);
            }
            catch { }

            return found;
        }

        static bool s_ClassProcNative;

        static IntPtr NativeDefProc
        {
            get
            {
                if (s_NativeDefProc == IntPtr.Zero)
                {
                    try
                    {

                        IntPtr u32 = NativeMethods.GetModuleHandleW("user32.dll");
                        if (u32 != IntPtr.Zero)
                            s_NativeDefProc = NativeMethods.GetProcAddress(u32, "DefWindowProcW");
                    }
                    catch { }

                    if (s_NativeDefProc == IntPtr.Zero)
                        MacTitleBar.Log("警告：取不到 DefWindowProcW，菜单栏无法跨域重载存活（退回旧行为）");
                }
                return s_NativeDefProc;
            }
        }

        public static int BarHeight => s_BarH > 0 ? s_BarH : Mathf.RoundToInt(kBarHeight * (s_Scale > 0 ? s_Scale : 1f));

        public static bool CaptureExcluded = false;

        public static void ApplyCaptureAffinity()
        {
            if (s_Hwnd == IntPtr.Zero) return;
            try
            {

                NativeMethods.SetWindowDisplayAffinity(s_Hwnd, CaptureExcluded ? 0x11u : 0x0u);
            }
            catch { }
        }

        public static void DebugExcludeFromCapture(bool on)
        {
            CaptureExcluded = on;
            ApplyCaptureAffinity();
        }

        public static bool AcrylicActive => s_Acrylic;

        public static bool ApplyAcrylic(uint tint)
        {
            s_Acrylic = false;
            return false;
        }

        public static bool ForceShow;

        public static bool Create(IntPtr unityHwnd, IntPtr hMenu, float scale)
        {

            IntPtr frozen = FindOwnBarWindow();

            if (frozen != IntPtr.Zero)
            {
                s_Hwnd = frozen;
            }
            else
            {
                Destroy();
            }

            s_UnityHwnd = unityHwnd;
            s_HMenu = hMenu;
            s_Scale = scale > 0 ? scale : 1f;

            int wantH = MacTitleBar.BarTopHeight;
            int baseH = Mathf.RoundToInt(kBarHeight * s_Scale);
            s_BarH = wantH > baseH ? wantH : baseH;

            IntPtr hInst = NativeMethods.GetModuleHandle(null);

            s_Delegate = WndProc;

            IntPtr classProc = NativeDefProc;
            s_ClassProcNative = classProc != IntPtr.Zero;
            if (!s_ClassProcNative)
                classProc = Marshal.GetFunctionPointerForDelegate(s_Delegate);

            var wc = new NativeMethods.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX)),
                style = 0,

                lpfnWndProc = classProc,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = hInst,
                hIcon = IntPtr.Zero,
                hCursor = NativeMethods.LoadCursor(IntPtr.Zero, IDC_ARROW),
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                lpszClassName = kClassName,
                hIconSm = IntPtr.Zero,
            };

            if (s_Hwnd == IntPtr.Zero && NativeMethods.RegisterClassEx(ref wc) == 0)
            {

                if (NativeMethods.UnregisterClass(kClassName, hInst) &&
                    NativeMethods.RegisterClassEx(ref wc) == 0)
                {
                    Debug.LogWarning("[Editor Chrome] 菜单栏窗口类注册失败。");
                    return false;
                }
            }

            var mon = GetMonitorRect(unityHwnd);

            if (s_Hwnd == IntPtr.Zero)
            {

                s_Hwnd = NativeMethods.CreateWindowEx(
                    WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED,
                    kClassName, kBarTitle,
                    WS_POPUP,
                    mon.Left, mon.Top, mon.Right - mon.Left, s_BarH + kBarOverlap,
                    IntPtr.Zero, IntPtr.Zero, hInst, IntPtr.Zero);

                if (s_Hwnd == IntPtr.Zero)
                {
                    Debug.LogWarning("[Editor Chrome] 菜单栏窗口创建失败。");
                    NativeMethods.UnregisterClass(kClassName, hInst);
                    s_Delegate = null;
                    return false;
                }
            }
            else
            {

                try
                {
                    if (NativeDefProc != IntPtr.Zero)
                        NativeMethods.SetClassLongPtr(s_Hwnd, NativeMethods.GCLP_WNDPROC, NativeDefProc);
                }
                catch { }
                MacTitleBar.Log("Create 接管冻结的菜单栏窗口");
            }

            NativeMethods.SetWindowLongPtr(s_Hwnd, NativeMethods.GWLP_WNDPROC,
                Marshal.GetFunctionPointerForDelegate(s_Delegate));

            if (s_FreezeExtra > 0) MacTitleBar.RepaintCaptionNow();
            s_FreezeExtra = 0;

            RefreshTitles();
            s_LastRect = new NativeMethods.RECT();
            NativeMethods.ShowWindow(s_Hwnd, SW_SHOWNOACTIVATE);
            s_Visible = true;

            RenderBar(s_Hwnd);
            ApplyCaptureAffinity();

            s_NextRefresh = EditorApplication.timeSinceStartup + 1.0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;

            return true;
        }

        public static void Destroy()
        {
            EditorApplication.update -= Tick;

            try
            {
                IntPtr leftover = FindOwnBarWindow();
                if (leftover != IntPtr.Zero) NativeMethods.DestroyWindow(leftover);
            }
            catch { }

            if (s_Hwnd != IntPtr.Zero)
            {
                NativeMethods.DestroyWindow(s_Hwnd);
                s_Hwnd = IntPtr.Zero;
            }

            IntPtr hInst = NativeMethods.GetModuleHandle(null);
            NativeMethods.UnregisterClass(kClassName, hInst);

            s_Delegate = null;
            s_Visible = false;
            s_Hover = -1;
            s_Acrylic = false;
            s_Titles = new string[0];
            s_ItemLeft = new int[0];
            s_ItemRight = new int[0];
            s_FgHwnd = IntPtr.Zero;
            s_FgPid = 0;
            s_FgExe = string.Empty;
            s_LastSwitchIndex = -1;
        }

        public static void FreezeForReload()
        {
            EditorApplication.update -= Tick;

            try { CloseActivePopup(); } catch { }

            if (s_Hwnd != IntPtr.Zero && NativeMethods.IsWindow(s_Hwnd))
            {

                if (!s_ClassProcNative)
                {
                    try { NativeMethods.DestroyWindow(s_Hwnd); } catch { }
                }
                else
                {
                    try
                    {

                        if (EditorFullscreen())
                        {
                            int extra = MacTitleBar.BarTopHeight;
                            if (extra > 0)
                            {
                                s_FreezeExtra = extra;
                                RenderBar(s_Hwnd);
                            }
                        }

                        IntPtr old = NativeMethods.SetWindowLongPtr(s_Hwnd, NativeMethods.GWLP_WNDPROC, NativeDefProc);
                        if (old == IntPtr.Zero) NativeMethods.DestroyWindow(s_Hwnd);
                    }
                    catch
                    {
                        try { NativeMethods.DestroyWindow(s_Hwnd); } catch { }
                    }
                }
            }

            s_Hwnd = IntPtr.Zero;
            s_Delegate = null;
            s_Visible = false;
            s_Hover = -1;
            s_Acrylic = false;
            s_Titles = new string[0];
            s_ItemLeft = new int[0];
            s_ItemRight = new int[0];
            s_FgHwnd = IntPtr.Zero;
            s_FgPid = 0;
            s_FgExe = string.Empty;
            s_LastSwitchIndex = -1;
            MacTitleBar.Log("FreezeForReload 完成 freezeExtra=" + s_FreezeExtra);
        }

        public static void Tick()
        {
            if (s_Hwnd == IntPtr.Zero) return;

            if (GlassStabilityGuard.StructureLocked) return;

            PerfProbe.Begin();
            TickCore();
            PerfProbe.End("menubar");
        }

        static void TickCore()
        {
            double now = EditorApplication.timeSinceStartup;
            bool want = ShouldShowBar();

            if (s_ActivePopup != null)
            {
                double since = EditorApplication.timeSinceStartup - s_PopupOpenedAt;
                if (!want && since > 0.35)
                {
                    MacTitleBar.Log("Tick: 前台切走，关闭菜单弹层");
                    CloseActivePopup();
                }

                if (s_ActivePopup != null)
                {
                    NativeMethods.POINT cp;
                    if (NativeMethods.GetCursorPos(out cp))
                    {
                        bool inside = s_ActivePopup.ChainContainsPoint(cp);
                        bool onBar = BarHitTest(cp, out _);

                        if (inside || onBar)
                        {
                            s_LeaveSince = -1;

                            s_ActivePopup.EnsureCapture();
                        }
                        else if (since > 0.25)
                        {

                            if (s_LeaveSince < 0) s_LeaveSince = now;
                            else if (now - s_LeaveSince >= 0.12)
                            {
                                MacTitleBar.Log("Tick: 光标离开菜单链 -> 关闭弹层");
                                CloseActivePopup();
                            }
                        }
                    }
                }
                else s_LeaveSince = -1;

                if (s_ActivePopup != null) want = true;
            }

            if (want != s_Visible)
            {

                if (want != s_WantPending || s_WantSince < 0)
                {
                    s_WantPending = want;
                    s_WantSince = now;
                }

                if (now - s_WantSince >= 0.12)
                {

                    if (want) RenderBar(s_Hwnd);
                    NativeMethods.ShowWindow(s_Hwnd, want ? SW_SHOWNOACTIVATE : SW_HIDE);
                    s_Visible = want;
                    s_WantSince = -1;
                }
            }
            else s_WantSince = -1;

            bool zoom = EditorFullscreen();
            if (zoom != s_LastZoom)
            {
                s_LastZoom = zoom;
                RenderBar(s_Hwnd);
            }

            if (s_UnityHwnd != IntPtr.Zero)
            {
                IntPtr cur = NativeMethods.GetMenu(s_UnityHwnd);
                if (cur != IntPtr.Zero && cur != s_HMenu) s_HMenu = cur;
            }

            if (now < s_NextRefresh) return;
            s_NextRefresh = now + 1.0;

            Reposition();
            RefreshTitles();
        }

        public static string Describe()
        {
            var sb = new StringBuilder();
            sb.Append("alive=").Append(Alive);
            sb.Append(" visible=").Append(s_Visible);
            sb.Append(" hwnd=0x").Append(s_Hwnd.ToInt64().ToString("X"));
            sb.Append(" hMenu=0x").Append(s_HMenu.ToInt64().ToString("X"));
            sb.Append(" items=").Append(s_Titles.Length);
            sb.Append(" barH=").Append(s_BarH);
            sb.Append(" hover=").Append(s_Hover);
            var st = LiquidGlassSettings.instance;
            sb.Append(" barMode=").Append(st.menuBarBgMode);
            sb.Append(" popupMode=").Append(st.popupThemeMode);
            sb.Append(" menuOpen=").Append(MenuOpen);
            sb.Append(" zoom=").Append(EditorFullscreen());
            sb.Append(" captured=").Append(!CaptureExcluded);
            return sb.ToString();
        }

        public static void DebugClick(int x)
        {
            if (s_Hwnd == IntPtr.Zero) return;
            long lp = ((long)(s_BarH / 2) << 16) | (uint)(x & 0xFFFF);
            NativeMethods.PostMessage(s_Hwnd, WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lp);
        }

        public static void DebugOpen(int index)
        {
            OpenMenu(index);
        }

        public static void SendEscape()
        {
            CloseActivePopup();
        }

        public static string DumpSub(int index)
        {
            if (s_HMenu == IntPtr.Zero) return "no menu";
            IntPtr sub = NativeMethods.GetSubMenu(s_HMenu, index);
            if (sub == IntPtr.Zero) return "no submenu at " + index;

            int count = NativeMethods.GetMenuItemCount(sub);
            var sb = new StringBuilder();
            sb.Append("sub[").Append(index).Append("] count=").Append(count).Append(" | ");
            var buf = new StringBuilder(128);
            for (int i = 0; i < Math.Min(count, 5); i++)
            {
                buf.Length = 0;
                NativeMethods.GetMenuString(sub, (uint)i, buf, 128, MF_BYPOSITION);
                int id = NativeMethods.GetMenuItemID(sub, i);
                sb.Append(buf.ToString().Replace("&", "")).Append("(id=").Append(id).Append(") ");
            }
            return sb.ToString();
        }

        static bool IsUnityForeground()
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            NativeMethods.GetWindowThreadProcessId(fg, out uint pid);
            return pid == NativeMethods.GetCurrentProcessId();
        }

        static IntPtr s_FgHwnd;
        static uint s_FgPid;
        static string s_FgExe = string.Empty;

        static bool ShouldShowBar()
        {
            if (ForceShow) return true;

            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == IntPtr.Zero) return s_Visible;

            NativeMethods.GetWindowThreadProcessId(fg, out uint pid);
            if (pid == NativeMethods.GetCurrentProcessId()) return true;

            if (IsDesktopWindow(fg))
                return s_UnityHwnd == IntPtr.Zero || !NativeMethods.IsIconic(s_UnityHwnd);

            string exe = ExeNameOf(fg, pid);
            if (string.IsNullOrEmpty(exe)) return false;
            if (!IsWhitelisted(exe)) return false;

            if (s_UnityHwnd != IntPtr.Zero && NativeMethods.IsIconic(s_UnityHwnd)) return false;

            return true;
        }

        static bool IsDesktopWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            try
            {
                var sb = new StringBuilder(64);
                NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
                string cls = sb.ToString();

                return cls == "Progman" || cls == "WorkerW";
            }
            catch
            {
                return false;
            }
        }

        static string ExeNameOf(IntPtr hwnd, uint pid)
        {
            if (hwnd == s_FgHwnd && pid == s_FgPid) return s_FgExe;

            s_FgHwnd = hwnd;
            s_FgPid = pid;
            s_FgExe = QueryExeName(pid);
            return s_FgExe;
        }

        static string QueryExeName(uint pid)
        {

            IntPtr h = NativeMethods.OpenProcess(0x1000, false, pid);
            if (h == IntPtr.Zero) return string.Empty;

            try
            {
                var sb = new StringBuilder(1024);
                uint size = (uint)sb.Capacity;
                if (!NativeMethods.QueryFullProcessImageName(h, 0, sb, ref size)) return string.Empty;

                string full = sb.ToString(0, (int)Mathf.Min(size, (uint)sb.Length));
                int slash = full.LastIndexOf('\\');
                return slash >= 0 ? full.Substring(slash + 1) : full;
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                NativeMethods.CloseHandle(h);
            }
        }

        static string s_WlRaw;
        static string[] s_WlEntries;

        static bool IsWhitelisted(string exe)
        {
            string raw = null;
            try { raw = LiquidGlassSettings.instance.menuBarWhitelist; } catch { }
            if (string.IsNullOrEmpty(raw)) return false;

            if (raw != s_WlRaw)
            {
                s_WlRaw = raw;
                string[] parts = raw.Split(
                    new[] { ',', ';', '\n', '\r', '\t', ' ' },
                    StringSplitOptions.RemoveEmptyEntries);

                var list = new List<string>(parts.Length);
                foreach (string part in parts)
                {
                    string e = part.Trim();
                    if (e.Length == 0) continue;
                    if (!e.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) e += ".exe";
                    list.Add(e);
                }
                s_WlEntries = list.ToArray();
            }

            if (s_WlEntries == null) return false;
            foreach (string e in s_WlEntries)
                if (string.Equals(e, exe, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string DescribeForeground()
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            NativeMethods.GetWindowThreadProcessId(fg, out uint pid);
            bool self = pid == NativeMethods.GetCurrentProcessId();
            string exe = self ? "(本进程)" : ExeNameOf(fg, pid);
            return "fg=0x" + fg.ToInt64().ToString("X") + " pid=" + pid +
                   " exe=" + exe + " show=" + ShouldShowBar();
        }

        public static string DebugSetWhitelist(string list)
        {
            LiquidGlassSettings.instance.menuBarWhitelist = list ?? string.Empty;
            LiquidGlassSettings.instance.Save();
            return DescribeForeground();
        }

        static NativeMethods.RECT GetMonitorRect(IntPtr hwnd)
        {
            var info = new NativeMethods.MONITORINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.MONITORINFO))
            };

            IntPtr mon = NativeMethods.MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (mon != IntPtr.Zero && NativeMethods.GetMonitorInfo(mon, ref info))
                return info.rcMonitor;

            int w = NativeMethods.GetSystemMetrics(0);
            return new NativeMethods.RECT { Left = 0, Top = 0, Right = w, Bottom = 0 };
        }

        static NativeMethods.RECT s_LastRect;

        static void Reposition()
        {
            if (s_UnityHwnd == IntPtr.Zero || s_Hwnd == IntPtr.Zero) return;

            var mon = GetMonitorRect(s_UnityHwnd);
            int w = mon.Right - mon.Left;

            int h = s_BarH + kBarOverlap + s_FreezeExtra;

            if (s_LastRect.Left == mon.Left && s_LastRect.Top == mon.Top &&
                s_LastRect.Right == w && s_LastRect.Bottom == h)
                return;

            s_LastRect = new NativeMethods.RECT
            {
                Left = mon.Left, Top = mon.Top, Right = w, Bottom = h
            };

            NativeMethods.SetWindowPos(s_Hwnd, NativeMethods.HWND_TOPMOST,
                mon.Left, mon.Top, w, h,
                NativeMethods.SWP_NOACTIVATE);
        }

        static void RefreshTitles()
        {
            var titles = new string[0];
            if (s_HMenu != IntPtr.Zero)
            {
                int count = NativeMethods.GetMenuItemCount(s_HMenu);
                if (count > 0)
                {
                    titles = new string[count];
                    var buf = new StringBuilder(128);
                    for (int i = 0; i < count; i++)
                    {
                        buf.Length = 0;
                        NativeMethods.GetMenuString(s_HMenu, (uint)i, buf, 128, MF_BYPOSITION);
                        titles[i] = buf.ToString().Replace("&", "");
                    }
                }
            }

            s_RealCount = titles.Length;

            var withOwn = new string[s_RealCount + 1];
            if (s_RealCount > 0) Array.Copy(titles, withOwn, s_RealCount);
            withOwn[s_RealCount] = kOwnMenuTitle;
            titles = withOwn;

            bool changed = titles.Length != s_Titles.Length;
            if (!changed)
            {
                for (int i = 0; i < titles.Length; i++)
                {
                    if (titles[i] != s_Titles[i]) { changed = true; break; }
                }
            }

            if (!changed) return;

            s_Titles = titles;
            RefreshLayout();

            if (s_Hwnd != IntPtr.Zero)
                NativeMethods.InvalidateRect(s_Hwnd, IntPtr.Zero, false);
        }

        static void RefreshLayout()
        {
            if (s_Titles.Length == 0)
            {
                s_ItemLeft = new int[0];
                s_ItemRight = new int[0];
                s_ItemTextX = new int[0];
                s_AppW = 0;
                return;
            }

            IntPtr dc = NativeMethods.GetDC(IntPtr.Zero);
            if (dc == IntPtr.Zero) return;

            IntPtr bold = IntPtr.Zero;
            IntPtr norm = IntPtr.Zero;

            try
            {
                int fh = -Mathf.Max(9, Mathf.RoundToInt(kFontSize * s_Scale));
                bold = NativeMethods.CreateFont(fh, 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 0, 0, "Segoe UI");
                norm = NativeMethods.CreateFont(fh, 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 0, 0, "Segoe UI");

                s_PadX = Mathf.RoundToInt(12f * s_Scale);
                s_ItemPad = Mathf.RoundToInt(8f * s_Scale);
                s_GapAfterApp = Mathf.RoundToInt(12f * s_Scale);

                IntPtr old = NativeMethods.SelectObject(dc, bold);
                s_AppW = MeasureText(dc, "Unity");
                NativeMethods.SelectObject(dc, old);

                old = NativeMethods.SelectObject(dc, norm);

                int x = s_PadX + s_AppW + s_GapAfterApp;
                int n = s_Titles.Length;
                s_ItemLeft = new int[n];
                s_ItemRight = new int[n];
                s_ItemTextX = new int[n];

                for (int i = 0; i < n; i++)
                {
                    int w = MeasureText(dc, s_Titles[i]);
                    s_ItemLeft[i] = x - s_ItemPad;
                    s_ItemTextX[i] = x;
                    s_ItemRight[i] = x + w + s_ItemPad;
                    x += w + s_ItemPad * 2;
                }

                NativeMethods.SelectObject(dc, old);
            }
            finally
            {
                if (bold != IntPtr.Zero) NativeMethods.DeleteObject(bold);
                if (norm != IntPtr.Zero) NativeMethods.DeleteObject(norm);
                NativeMethods.ReleaseDC(IntPtr.Zero, dc);
            }
        }

        static void Paint(IntPtr hWnd)
        {

            NativeMethods.PAINTSTRUCT ps;
            if (NativeMethods.BeginPaint(hWnd, out ps) != IntPtr.Zero)
                NativeMethods.EndPaint(hWnd, ref ps);

            RenderBar(hWnd);
        }

        static IntPtr CreateUiFont(int height, int weight)
        {

            return NativeMethods.CreateFont(height, 0, 0, 0, weight, 0, 0, 0, 0, 0, 0,
                4, 0, "Segoe UI");
        }

        static void RenderBar(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            var mon = GetMonitorRect(s_UnityHwnd);
            int width = mon.Right - mon.Left;
            int barH = s_BarH;

            int height = barH + kBarOverlap + s_FreezeExtra;
            if (width <= 0 || height <= 0) return;

            var cv = LayeredCanvas.Create(width, height);
            if (cv == null) return;

            var pal = CurrentPalette(EditorFullscreen());
            if (pal.barAlpha != s_LastBarAlpha)
            {
                s_LastBarAlpha = pal.barAlpha;
                MacTitleBar.Log($"RenderBar barAlpha={pal.barAlpha} menuOpen={MenuOpen} zoom={EditorFullscreen()} mode={LiquidGlassSettings.instance.menuBarBgMode}");
            }

            IntPtr boldFont = IntPtr.Zero;
            IntPtr normFont = IntPtr.Zero;

            try
            {
                IntPtr dc = cv.Dc;

                cv.FillColor(pal.barBg, pal.barAlpha);

                if (pal.barAlpha <= 0f)
                {

                    int lineY = (s_FreezeExtra > 0 ? barH : barH + kBarOverlap) - 1;
                    IntPtr line = NativeMethods.CreateSolidBrush(pal.barLine);
                    var bottom = new NativeMethods.RECT { Left = 0, Top = lineY, Right = width, Bottom = lineY + 1 };
                    NativeMethods.FillRect(dc, ref bottom, line);
                    NativeMethods.DeleteObject(line);
                }

                NativeMethods.SetBkMode(dc, 1);
                NativeMethods.SetTextColor(dc, pal.barText);

                int fh = -Mathf.Max(9, Mathf.RoundToInt(kFontSize * s_Scale));
                boldFont = CreateUiFont(fh, 700);
                normFont = CreateUiFont(fh, 400);

                IntPtr old = NativeMethods.SelectObject(dc, boldFont);
                DrawTextV(dc, "Unity", s_PadX, barH, width);
                NativeMethods.SelectObject(dc, old);

                NativeMethods.SelectObject(dc, normFont);

                for (int i = 0; i < s_Titles.Length && i < s_ItemLeft.Length; i++)
                {

                    NativeMethods.SetTextColor(dc, pal.barText);
                    DrawTextV(dc, s_Titles[i], s_ItemTextX[i], barH, width);
                }

                if (s_FreezeExtra > 0)
                {
                    IntPtr screen = NativeMethods.GetDC(IntPtr.Zero);
                    if (screen != IntPtr.Zero)
                    {
                        try
                        {

                            NativeMethods.BitBlt(dc, 0, barH, width, kBarOverlap + s_FreezeExtra, screen,
                                mon.Left, mon.Top + barH, NativeMethods.SRCCOPY);
                        }
                        finally { NativeMethods.ReleaseDC(IntPtr.Zero, screen); }

                        cv.ForceOpaque(0, barH, width, height);
                    }
                }
            }
            catch
            {
                MacTitleBar.Log("RenderBar 绘制异常");
            }
            finally
            {
                if (boldFont != IntPtr.Zero) NativeMethods.DeleteObject(boldFont);
                if (normFont != IntPtr.Zero) NativeMethods.DeleteObject(normFont);

                if (s_ItemLeft.Length > 0 && s_ItemRight.Length == s_ItemLeft.Length)
                {
                    int hitL = s_ItemLeft[0];
                    int hitR = s_ItemRight[s_ItemRight.Length - 1];
                    cv.EnsureHitAlpha(hitL, 0, hitR, barH);
                }

                if (!cv.Commit(hWnd, mon.Left, mon.Top))
                    MacTitleBar.Log("RenderBar Commit 失败");
                cv.Destroy();
            }
        }

        static int MeasureText(IntPtr dc, string text)
        {
            var size = new NativeMethods.SIZE();
            if (NativeMethods.GetTextExtentPoint32(dc, text, text.Length, ref size))
                return size.cx;
            return text.Length * 8;
        }

        static void DrawTextV(IntPtr dc, string text, int x, int height, int width)
        {
            var r = new NativeMethods.RECT
            {
                Left = x,
                Top = 0,
                Right = Math.Min(width, x + 600),
                Bottom = height
            };
            NativeMethods.DrawText(dc, text, text.Length, ref r,
                0x00000004   | 0x00000020   | 0x00000800  );
        }

        static int HitTest(int x)
        {
            for (int i = 0; i < s_ItemLeft.Length && i < s_ItemRight.Length; i++)
            {
                if (x >= s_ItemLeft[i] && x < s_ItemRight[i]) return i;
            }
            return -1;
        }

        internal static bool BarHitTest(NativeMethods.POINT screen, out int clientX)
        {
            clientX = -1;
            if (s_Hwnd == IntPtr.Zero) return false;

            NativeMethods.RECT r;
            if (!NativeMethods.GetWindowRect(s_Hwnd, out r)) return false;
            if (screen.X < r.Left || screen.X >= r.Right) return false;
            if (screen.Y < r.Top || screen.Y >= r.Bottom) return false;

            clientX = screen.X - r.Left;
            return true;
        }

        internal static int BarIndexAt(NativeMethods.POINT screen)
        {
            int cx;
            if (!BarHitTest(screen, out cx)) return -1;
            return HitTest(cx);
        }

        static void OnMouseMove(IntPtr lParam)
        {
            int x = (short)(lParam.ToInt64() & 0xFFFF);

            var tme = new NativeMethods.TRACKMOUSEEVENT
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.TRACKMOUSEEVENT)),
                dwFlags = TME_LEAVE,
                hwndTrack = s_Hwnd,
                dwHoverTime = 0
            };
            NativeMethods.TrackMouseEvent(ref tme);

            int hit = HitTest(x);
            if (hit != s_Hover)
            {
                s_Hover = hit;

            }
        }

        static void OnClick(IntPtr lParam)
        {
            int x = (short)(lParam.ToInt64() & 0xFFFF);
            int index = HitTest(x);
            MacTitleBar.Log($"OnClick x={x} hit={index} items={s_ItemLeft.Length}");
            if (index >= 0) OpenMenu(index);
        }

        public static bool MenuOpen => s_ActivePopup != null;

        static MenuPopup s_ActivePopup;

        static IntPtr BuildOwnMenu()
        {
            if (s_OwnMenu != IntPtr.Zero)
            {
                NativeMethods.DestroyMenu(s_OwnMenu);
                s_OwnMenu = IntPtr.Zero;
            }

            var s = LiquidGlassSettings.instance;
            IntPtr menu = NativeMethods.CreatePopupMenu();
            if (menu == IntPtr.Zero) return IntPtr.Zero;

            NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, (IntPtr)kCmdWhitelistAdd,
                "添加白名单程序…");

            var entries = ParseWhitelist(s.menuBarWhitelist);
            IntPtr removeSub = NativeMethods.CreatePopupMenu();
            if (entries.Length == 0)
            {
                NativeMethods.AppendMenu(removeSub,
                    NativeMethods.MF_STRING | NativeMethods.MF_GRAYED,
                    IntPtr.Zero, "（白名单为空）");
            }
            else
            {
                for (int i = 0; i < entries.Length && i < (kCmdRemoveMax - kCmdRemoveBase); i++)
                {
                    NativeMethods.AppendMenu(removeSub, NativeMethods.MF_STRING,
                        (IntPtr)(kCmdRemoveBase + i), entries[i]);
                }
            }
            NativeMethods.AppendMenu(menu, NativeMethods.MF_POPUP, removeSub, "移除白名单程序");

            NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, IntPtr.Zero, null);

            int popMode = s.popupThemeMode;
            AppendRadio(menu, kCmdPopupAuto, "二级菜单配色：跟随系统", popMode == (int)PopupThemeMode.Auto);
            AppendRadio(menu, kCmdPopupAuto + 1, "二级菜单配色：浅色", popMode == (int)PopupThemeMode.Light);
            AppendRadio(menu, kCmdPopupAuto + 2, "二级菜单配色：深色", popMode == (int)PopupThemeMode.Dark);
            AppendRadio(menu, kCmdPopupCustom, "二级菜单配色：自定义…", popMode == (int)PopupThemeMode.Custom);

            NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, IntPtr.Zero, null);

            int barMode = s.menuBarBgMode;
            AppendRadio(menu, kCmdBarAuto, "菜单栏底色：自动（窗口透明 / 全屏跟随系统）",
                barMode == (int)MenuBarBgMode.Auto);
            AppendRadio(menu, kCmdBarAuto + 1, "菜单栏底色：透明", barMode == (int)MenuBarBgMode.Transparent);
            AppendRadio(menu, kCmdBarAuto + 2, "菜单栏底色：浅色", barMode == (int)MenuBarBgMode.Light);
            AppendRadio(menu, kCmdBarAuto + 3, "菜单栏底色：深色", barMode == (int)MenuBarBgMode.Dark);
            AppendRadio(menu, kCmdBarCustom, "菜单栏底色：自定义…", barMode == (int)MenuBarBgMode.Custom);

            NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, IntPtr.Zero, null);
            NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, (IntPtr)kCmdOpenSettings,
                "打开设置…");

            s_OwnMenu = menu;
            return menu;
        }

        static void AppendRadio(IntPtr menu, int id, string text, bool on)
        {
            uint flags = NativeMethods.MF_STRING;
            if (on) flags |= NativeMethods.MF_CHECKED;
            NativeMethods.AppendMenu(menu, flags, (IntPtr)id, text);
        }

        static string[] ParseWhitelist(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return new string[0];
            var list = new List<string>();
            foreach (string part in raw.Split(',', ';', '\n', '\r', '\t'))
            {
                string e = part.Trim();
                if (e.Length > 0) list.Add(e);
            }
            return list.ToArray();
        }

        static void HandleOwnCommand(int id)
        {
            var s = LiquidGlassSettings.instance;
            MacTitleBar.Log($"own cmd id=0x{id:X4}");

            if (id == kCmdOpenSettings) { LiquidGlassSettingsWindow.OpenWindow(); return; }

            if (id == kCmdWhitelistAdd)
            {
                AddWhitelistViaDialog();
                return;
            }

            if (id >= kCmdRemoveBase && id < kCmdRemoveMax)
            {
                var entries = ParseWhitelist(s.menuBarWhitelist);
                int idx = id - kCmdRemoveBase;
                if (idx >= 0 && idx < entries.Length)
                {
                    var keep = new List<string>(entries);
                    keep.RemoveAt(idx);
                    s.menuBarWhitelist = string.Join(",", keep.ToArray());
                    s.Save();
                    MacTitleBar.Log($"自建菜单：移除白名单 {entries[idx]}");
                }
                return;
            }

            if (id >= kCmdPopupAuto && id <= kCmdPopupAuto + 2)
            {
                s.popupThemeMode = (int)PopupThemeMode.Auto + (id - kCmdPopupAuto);
                s.Save();
                return;
            }
            if (id == kCmdPopupCustom)
            {
                s.popupThemeMode = (int)PopupThemeMode.Custom;
                s.Save();
                LiquidGlassSettingsWindow.OpenWindow();
                return;
            }

            if (id >= kCmdBarAuto && id <= kCmdBarAuto + 3)
            {
                s.menuBarBgMode = (int)MenuBarBgMode.Auto + (id - kCmdBarAuto);
                s.Save();
                RefreshAppearance();
                return;
            }
            if (id == kCmdBarCustom)
            {
                s.menuBarBgMode = (int)MenuBarBgMode.Custom;
                s.Save();
                RefreshAppearance();
                LiquidGlassSettingsWindow.OpenWindow();
                return;
            }
        }

        static void AddWhitelistViaDialog()
        {
            string path = EditorUtility.OpenFilePanelWithFilters(
                "选择要加入菜单栏白名单的程序", "", new[] { "程序", "exe" });
            if (string.IsNullOrEmpty(path)) return;

            string exe = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(exe)) return;

            var s = LiquidGlassSettings.instance;
            var entries = ParseWhitelist(s.menuBarWhitelist);
            foreach (string e in entries)
            {
                if (string.Equals(e, exe, StringComparison.OrdinalIgnoreCase))
                {
                    MacTitleBar.Log($"自建菜单：{exe} 已在白名单里");
                    return;
                }
            }

            var list = new List<string>(entries) { exe };
            s.menuBarWhitelist = string.Join(",", list.ToArray());
            s.Save();
            MacTitleBar.Log($"自建菜单：白名单新增 {exe} -> {s.menuBarWhitelist}");
        }

        static int s_LastSwitchIndex = -1;
        static double s_LastSwitchTime;

        static double s_PopupOpenedAt;

        static double s_LeaveSince = -1;

        static void OpenMenu(int index)
        {
            if (index < 0 || index >= s_ItemLeft.Length) return;

            CloseActivePopup();

            bool ownMenu = index >= s_RealCount;
            IntPtr sub;
            if (ownMenu)
            {

                sub = BuildOwnMenu();
            }
            else
            {
                if (s_HMenu == IntPtr.Zero) return;
                sub = NativeMethods.GetSubMenu(s_HMenu, index);
            }

            if (sub == IntPtr.Zero)
            {
                MacTitleBar.Log("OpenMenu 无子菜单 index=" + index);
                return;
            }

            var pt = new NativeMethods.POINT { X = s_ItemLeft[index], Y = s_BarH + kBarOverlap };
            NativeMethods.ClientToScreen(s_Hwnd, ref pt);
            int x = pt.X;
            int y = pt.Y;

            bool alreadyOurs = IsUnityForeground();

            bool keepOtherFocus = ownMenu || (!alreadyOurs && ShouldShowBar());
            bool fg = alreadyOurs || keepOtherFocus || EnsureForeground(s_UnityHwnd);

            MacTitleBar.Log($"OpenMenu index={index} own={ownMenu} x={x} y={y} fg={fg} keepOtherFocus={keepOtherFocus}");

            s_ActivePopup = new MenuPopup(sub, x, y, s_UnityHwnd, s_Scale, null);
            s_ActivePopup.openedIndex = index;
            s_ActivePopup.keepOtherFocus = keepOtherFocus;
            if (ownMenu) s_ActivePopup.commandTarget = s_Hwnd;
            s_PopupOpenedAt = EditorApplication.timeSinceStartup;
            s_LeaveSince = -1;
            s_ActivePopup.Show();

            RenderBar(s_Hwnd);
        }

        public static void CloseActivePopup()
        {
            bool had = s_ActivePopup != null;
            s_LeaveSince = -1;
            if (s_ActivePopup != null)
            {
                s_ActivePopup.CloseChain();
                s_ActivePopup = null;
            }

            if (had) RenderBar(s_Hwnd);
        }

        static bool EnsureForeground(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;

            IntPtr fg0 = NativeMethods.GetForegroundWindow();
            if (fg0 == hwnd) return true;

            if (fg0 != IntPtr.Zero)
            {
                NativeMethods.GetWindowThreadProcessId(fg0, out uint fgPid);
                if (fgPid == NativeMethods.GetCurrentProcessId()) return true;
            }

            IntPtr fg = fg0;
            uint fgThread = fg != IntPtr.Zero
                ? NativeMethods.GetWindowThreadProcessId(fg, out _)
                : 0u;
            uint myThread = NativeMethods.GetCurrentThreadId();

            bool attached = false;
            try
            {
                if (fgThread != 0 && fgThread != myThread)
                    attached = NativeMethods.AttachThreadInput(myThread, fgThread, true);

                NativeMethods.SetForegroundWindow(hwnd);
                NativeMethods.BringWindowToTop(hwnd);
            }
            catch { }
            finally
            {
                if (attached)
                {
                    try { NativeMethods.AttachThreadInput(myThread, fgThread, false); } catch { }
                }
            }

            return NativeMethods.GetForegroundWindow() == hwnd;
        }

        static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                switch (msg)
                {
                    case WM_PAINT:
                        Paint(hWnd);
                        return IntPtr.Zero;

                    case WM_ERASEBKGND:
                        return (IntPtr)1;

                    case WM_SETCURSOR:
                        break;

                    case WM_MOUSEMOVE:
                        OnMouseMove(lParam);
                        return IntPtr.Zero;

                    case WM_MOUSELEAVE:
                        if (s_Hover != -1)
                        {
                            s_Hover = -1;
                            NativeMethods.InvalidateRect(hWnd, IntPtr.Zero, false);
                        }
                        return IntPtr.Zero;

                    case WM_LBUTTONUP:
                        OnClick(lParam);
                        return IntPtr.Zero;

                    case WM_COMMAND:
                    {

                        int id = (int)(wParam.ToInt64() & 0xFFFF);
                        if (id >= kCmdOwnMin && id <= kCmdOwnMax)
                        {
                            HandleOwnCommand(id);
                            return IntPtr.Zero;
                        }
                        break;
                    }
                }

                return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        internal static class NativeMethods
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct RECT { public int Left, Top, Right, Bottom; }

            [StructLayout(LayoutKind.Sequential)]
            public struct SIZE { public int cx, cy; }

            [StructLayout(LayoutKind.Sequential)]
            public struct ACCENTPOLICY
            {
                public int AccentState;
                public int AccentFlags;
                public uint GradientColor;
                public int AnimationId;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct WINCOMPATTRDATA
            {
                public int Attribute;
                public IntPtr Data;
                public UIntPtr SizeOfData;
            }

            [DllImport("user32.dll")]
            public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WINCOMPATTRDATA data);

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT { public int X, Y; }

            [StructLayout(LayoutKind.Sequential)]
            public struct PAINTSTRUCT
            {
                public IntPtr hdc;
                public int fErase;
                public RECT rcPaint;
                public int fRestore;
                public int fIncUpdate;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
                public byte[] rgbReserved;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct TRACKMOUSEEVENT
            {
                public uint cbSize;
                public uint dwFlags;
                public IntPtr hwndTrack;
                public uint dwHoverTime;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MONITORINFO
            {
                public uint cbSize;
                public RECT rcMonitor;
                public RECT rcWork;
                public uint dwFlags;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct WNDCLASSEX
            {
                public uint cbSize;
                public uint style;
                public IntPtr lpfnWndProc;
                public int cbClsExtra;
                public int cbWndExtra;
                public IntPtr hInstance;
                public IntPtr hIcon;
                public IntPtr hCursor;
                public IntPtr hbrBackground;
                [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
                [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
                public IntPtr hIconSm;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct BITMAPINFOHEADER
            {
                public uint biSize;
                public int biWidth;
                public int biHeight;
                public ushort biPlanes;
                public ushort biBitCount;
                public uint biCompression;
                public uint biSizeImage;
                public int biXPelsPerMeter;
                public int biYPelsPerMeter;
                public uint biClrUsed;
                public uint biClrImportant;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct BITMAPINFO
            {
                public BITMAPINFOHEADER bmiHeader;
                public uint bmiColors;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct BLENDFUNCTION
            {
                public byte BlendOp;
                public byte BlendFlags;
                public byte SourceConstantAlpha;
                public byte AlphaFormat;
            }

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

            [DllImport("gdi32.dll")]
            public static extern bool DeleteDC(IntPtr hdc);

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi,
                uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

            [DllImport("user32.dll")]
            public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
                ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc,
                uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);

            [DllImport("kernel32.dll")]
            public static extern IntPtr GetModuleHandle([MarshalAs(UnmanagedType.LPWStr)] string name);

            [DllImport("kernel32.dll")]
            public static extern uint GetCurrentProcessId();

            [DllImport("user32.dll")]
            public static extern IntPtr CreatePopupMenu();

            [DllImport("user32.dll")]
            public static extern bool DestroyMenu(IntPtr hMenu);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern bool AppendMenu(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, string lpNewItem);

            public const uint MF_STRING = 0x00000000;

            [DllImport("user32.dll")]
            public static extern bool IsIconic(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool IsZoomed(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CloseHandle(IntPtr hObject);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags,
                StringBuilder lpExeName, ref uint lpdwSize);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool UnregisterClass([MarshalAs(UnmanagedType.LPWStr)] string className, IntPtr hInstance);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr CreateWindowEx(
                uint dwExStyle, [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
                [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
                uint dwStyle, int x, int y, int nWidth, int nHeight,
                IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

            [DllImport("user32.dll")]
            public static extern bool DestroyWindow(IntPtr hWnd);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                int x, int y, int cx, int cy, uint flags);

            [DllImport("user32.dll")]
            public static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

            [DllImport("user32.dll")]
            public static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

            [DllImport("user32.dll")]
            public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

            [DllImport("user32.dll")]
            public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

            [DllImport("user32.dll")]
            public static extern IntPtr SetCursor(IntPtr hCursor);

            [DllImport("user32.dll")]
            public static extern int GetSystemMetrics(int index);

            [DllImport("user32.dll")]
            public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

            [DllImport("user32.dll")]
            public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

            [DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            [DllImport("user32.dll")]
            public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);

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
            public static extern int GetMenuItemID(IntPtr hMenu, int nPos);

            [DllImport("user32.dll")]
            public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr FindWindow([MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
                [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName);

            [DllImport("user32.dll")]
            public static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

            [DllImport("user32.dll")]
            public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

            [DllImport("kernel32.dll")]
            public static extern uint GetCurrentThreadId();

            [DllImport("user32.dll")]
            public static extern bool BringWindowToTop(IntPtr hWnd);

            public const uint SWP_NOSIZE = 0x0001;
            public const uint SWP_NOMOVE = 0x0002;
            public const uint SWP_NOZORDER = 0x0004;
            public const uint SWP_NOACTIVATE = 0x0010;

            public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
            public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateSolidBrush(uint color);

            [DllImport("user32.dll")]
            public static extern IntPtr GetDC(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

            [DllImport("gdi32.dll")]
            public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

            [DllImport("gdi32.dll")]
            public static extern bool DeleteObject(IntPtr obj);

            [DllImport("gdi32.dll")]
            public static extern int SetBkMode(IntPtr hdc, int mode);

            [DllImport("gdi32.dll")]
            public static extern uint SetTextColor(IntPtr hdc, uint color);

            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
            public static extern bool GetTextExtentPoint32(IntPtr hdc, string lpString, int c, ref SIZE psizl);

            [DllImport("user32.dll")]
            public static extern int FillRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);

            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr CreateFont(int height, int width, int escapement, int orientation,
                int weight, uint italic, uint underline, uint strikeOut, uint charSet,
                uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string faceName);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int DrawText(IntPtr hdc, string text, int length, ref RECT rect, uint flags);

            [DllImport("user32.dll")]
            public static extern uint GetMenuState(IntPtr hMenu, uint uId, uint uFlags);

            [DllImport("user32.dll")]
            public static extern IntPtr SetCapture(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool ReleaseCapture();

            [DllImport("user32.dll")]
            public static extern IntPtr GetCapture();

            [DllImport("user32.dll")]
            public static extern IntPtr SetFocus(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool GetCursorPos(out POINT lpPoint);

            [DllImport("user32.dll")]
            public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

            [DllImport("user32.dll")]
            public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int GetClassName(IntPtr hWnd, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder lpClassName, int nMaxCount);

            [DllImport("user32.dll")]
            public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

            [DllImport("user32.dll")]
            public static extern bool IsWindow(IntPtr hWnd);

            public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

            public const int GWLP_USERDATA = -21;
            public const int GWLP_WNDPROC = -4;

            public const int GCLP_WNDPROC = -24;
            public const uint SRCCOPY = 0x00CC0020;

            [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")]
            public static extern IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

            [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr GetModuleHandleW(string lpModuleName);

            [DllImport("gdi32.dll")]
            public static extern bool BitBlt(IntPtr hdcDest, int x, int y, int cx, int cy,
                IntPtr hdcSrc, int x1, int y1, uint rop);

            public const uint MF_POPUP = 0x00000010;
            public const uint MF_SEPARATOR = 0x00000800;
            public const uint MF_DISABLED = 0x00000002;
            public const uint MF_GRAYED = 0x00000001;
            public const uint MF_CHECKED = 0x00000008;
            public const uint MF_OWNERDRAW = 0x00000100;

            [DllImport("user32.dll")]
            public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);

            [DllImport("gdi32.dll")]
            public static extern int CombineRgn(IntPtr hrgnDst, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int fnCombineMode);

            [DllImport("user32.dll")]
            public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
        }

        sealed class LayeredCanvas
        {
            public IntPtr Dc;
            public IntPtr Bitmap;
            public IntPtr OldBitmap;
            public IntPtr Bits;
            public int Width;
            public int Height;

            IntPtr _screenDc;
            byte[] _px;

            public static LayeredCanvas Create(int w, int h)
            {
                if (w <= 0 || h <= 0) return null;

                var c = new LayeredCanvas();
                c._screenDc = NativeMethods.GetDC(IntPtr.Zero);
                c.Dc = NativeMethods.CreateCompatibleDC(c._screenDc);

                var bi = new NativeMethods.BITMAPINFO();
                bi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(NativeMethods.BITMAPINFOHEADER));
                bi.bmiHeader.biWidth = w;
                bi.bmiHeader.biHeight = -h;
                bi.bmiHeader.biPlanes = 1;
                bi.bmiHeader.biBitCount = 32;
                bi.bmiHeader.biCompression = 0;

                c.Bitmap = NativeMethods.CreateDIBSection(c.Dc, ref bi, 0, out c.Bits, IntPtr.Zero, 0);
                if (c.Bitmap == IntPtr.Zero)
                {
                    c.Destroy();
                    return null;
                }

                c.OldBitmap = NativeMethods.SelectObject(c.Dc, c.Bitmap);
                c.Width = w;
                c.Height = h;
                c._px = new byte[w * h * 4];

                Array.Clear(c._px, 0, c._px.Length);
                Marshal.Copy(c._px, 0, c.Bits, c._px.Length);
                return c;
            }

            public bool Commit(IntPtr hwnd, int screenX, int screenY)
            {
                if (hwnd == IntPtr.Zero || Bits == IntPtr.Zero) return false;

                Marshal.Copy(Bits, _px, 0, _px.Length);
                for (int i = 0; i < _px.Length; i += 4)
                {
                    byte b = _px[i], g = _px[i + 1], r = _px[i + 2];
                    byte fromColor = r > g ? (r > b ? r : b) : (g > b ? g : b);

                    byte preset = _px[i + 3];
                    _px[i + 3] = fromColor > preset ? fromColor : preset;
                }
                Marshal.Copy(_px, 0, Bits, _px.Length);

                var dst = new NativeMethods.POINT { X = screenX, Y = screenY };
                var size = new NativeMethods.SIZE { cx = Width, cy = Height };
                var src = new NativeMethods.POINT { X = 0, Y = 0 };
                var blend = new NativeMethods.BLENDFUNCTION
                {
                    BlendOp = 0,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = 1
                };

                return NativeMethods.UpdateLayeredWindow(hwnd, IntPtr.Zero, ref dst, ref size,
                    Dc, ref src, 0, ref blend, 0x02  );
            }

            public void FillPremultiplied(byte a, byte r, byte g, byte b)
            {
                if (a == 0) return;
                for (int i = 0; i < _px.Length; i += 4)
                {
                    _px[i] = b; _px[i + 1] = g; _px[i + 2] = r; _px[i + 3] = a;
                }
                Marshal.Copy(_px, 0, Bits, _px.Length);
            }

            public void ForceOpaque(int l, int t, int r, int b)
            {
                l = Mathf.Clamp(l, 0, Width); r = Mathf.Clamp(r, 0, Width);
                t = Mathf.Clamp(t, 0, Height); b = Mathf.Clamp(b, 0, Height);
                if (r <= l || b <= t) return;

                Marshal.Copy(Bits, _px, 0, _px.Length);
                for (int y = t; y < b; y++)
                {
                    int row = y * Width * 4;
                    for (int x = l; x < r; x++) _px[row + x * 4 + 3] = 255;
                }
                Marshal.Copy(_px, 0, Bits, _px.Length);
            }

            public void RoundCorners(int radius)
            {
                int r = Mathf.Clamp(radius, 0, Mathf.Min(Width, Height) / 2);
                if (r <= 0) return;

                Marshal.Copy(Bits, _px, 0, _px.Length);

                for (int corner = 0; corner < 4; corner++)
                {
                    int baseX = (corner & 1) == 0 ? 0 : Width - r;
                    int baseY = (corner & 2) == 0 ? 0 : Height - r;
                    float ccx = (corner & 1) == 0 ? r : Width - r;
                    float ccy = (corner & 2) == 0 ? r : Height - r;

                    for (int y = 0; y < r; y++)
                    {
                        for (int x = 0; x < r; x++)
                        {
                            int px = baseX + x, py = baseY + y;
                            float dx = px + 0.5f - ccx;
                            float dy = py + 0.5f - ccy;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            float cov = Mathf.Clamp01(r - d + 0.5f);
                            if (cov >= 0.999f) continue;

                            int i = (py * Width + px) * 4;
                            _px[i] = (byte)(_px[i] * cov);
                            _px[i + 1] = (byte)(_px[i + 1] * cov);
                            _px[i + 2] = (byte)(_px[i + 2] * cov);
                            _px[i + 3] = (byte)(_px[i + 3] * cov);
                        }
                    }
                }

                Marshal.Copy(_px, 0, Bits, _px.Length);
            }

            public void EnsureHitAlpha(int l, int t, int r, int b, byte minAlpha = 1)
            {
                if (Bits == IntPtr.Zero || minAlpha == 0) return;

                l = Mathf.Clamp(l, 0, Width); r = Mathf.Clamp(r, 0, Width);
                t = Mathf.Clamp(t, 0, Height); b = Mathf.Clamp(b, 0, Height);
                if (r <= l || b <= t) return;

                Marshal.Copy(Bits, _px, 0, _px.Length);
                bool touched = false;
                for (int y = t; y < b; y++)
                {
                    int row = y * Width * 4;
                    for (int x = l; x < r; x++)
                    {
                        int i = row + x * 4 + 3;
                        if (_px[i] < minAlpha) { _px[i] = minAlpha; touched = true; }
                    }
                }
                if (touched) Marshal.Copy(_px, 0, Bits, _px.Length);
            }

            public void FillColor(uint rgb, float alpha)
            {
                int a = Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
                if (a == 0) return;
                uint pm = Pre(rgb, a / 255f);
                FillPremultiplied((byte)a, (byte)(pm & 0xFF), (byte)((pm >> 8) & 0xFF), (byte)((pm >> 16) & 0xFF));
            }

            public void FillRectColor(int l, int t, int r, int b, uint rgb, float alpha)
            {
                int a = Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
                if (a == 0) return;

                l = Mathf.Clamp(l, 0, Width); r = Mathf.Clamp(r, 0, Width);
                t = Mathf.Clamp(t, 0, Height); b = Mathf.Clamp(b, 0, Height);
                if (r <= l || b <= t) return;

                uint pm = Pre(rgb, a / 255f);
                byte pb = (byte)(pm & 0xFF);
                byte pg = (byte)((pm >> 8) & 0xFF);
                byte pr = (byte)((pm >> 16) & 0xFF);

                Marshal.Copy(Bits, _px, 0, _px.Length);
                for (int y = t; y < b; y++)
                {
                    int row = y * Width * 4;
                    for (int x = l; x < r; x++)
                    {
                        int i = row + x * 4;
                        _px[i] = pb;
                        _px[i + 1] = pg;
                        _px[i + 2] = pr;
                        _px[i + 3] = (byte)a;
                    }
                }
                Marshal.Copy(_px, 0, Bits, _px.Length);
            }

            public void StrokeRoundRect(int l, int t, int r, int b, int radius, uint rgb)
            {
                l = Mathf.Clamp(l, 0, Width); r = Mathf.Clamp(r, 0, Width);
                t = Mathf.Clamp(t, 0, Height); b = Mathf.Clamp(b, 0, Height);
                if (r - l <= 2 || b - t <= 2) return;

                int rad = Mathf.Clamp(radius, 0, Mathf.Min((r - l) / 2, (b - t) / 2));
                byte sb = (byte)(rgb & 0xFF);
                byte sg = (byte)((rgb >> 8) & 0xFF);
                byte sr = (byte)((rgb >> 16) & 0xFF);

                Marshal.Copy(Bits, _px, 0, _px.Length);

                for (int x = l + rad; x < r - rad; x++)
                {
                    BlendStroke(x, t, sr, sg, sb, 1f);
                    BlendStroke(x, b - 1, sr, sg, sb, 1f);
                }
                for (int y = t + rad; y < b - rad; y++)
                {
                    BlendStroke(l, y, sr, sg, sb, 1f);
                    BlendStroke(r - 1, y, sr, sg, sb, 1f);
                }

                if (rad > 0)
                {
                    for (int corner = 0; corner < 4; corner++)
                    {
                        bool right = (corner & 1) != 0;
                        bool bottom = (corner & 2) != 0;
                        int x0 = right ? r - rad : l;
                        int y0 = bottom ? b - rad : t;
                        float ccx = (right ? r - rad : l + rad);
                        float ccy = (bottom ? b - rad : t + rad);

                        for (int yy = 0; yy < rad; yy++)
                        {
                            for (int xx = 0; xx < rad; xx++)
                            {
                                int px = x0 + xx, py = y0 + yy;
                                float dx = px + 0.5f - ccx;
                                float dy = py + 0.5f - ccy;
                                float d = Mathf.Sqrt(dx * dx + dy * dy);
                                float cov = Mathf.Clamp01(1f - Mathf.Abs(d - (rad - 0.75f)));
                                if (cov <= 0.004f) continue;
                                BlendStroke(px, py, sr, sg, sb, cov);
                            }
                        }
                    }
                }

                Marshal.Copy(_px, 0, Bits, _px.Length);
            }

            void BlendStroke(int x, int y, byte sr, byte sg, byte sb, float cov)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) return;
                if (cov <= 0f) return;
                if (cov > 1f) cov = 1f;

                int i = (y * Width + x) * 4;
                float inv = 1f - cov;
                _px[i] = (byte)(_px[i] * inv + sb * cov);
                _px[i + 1] = (byte)(_px[i + 1] * inv + sg * cov);
                _px[i + 2] = (byte)(_px[i + 2] * inv + sr * cov);
                _px[i + 3] = 255;
            }

            public void Destroy()
            {
                if (Dc != IntPtr.Zero)
                {
                    if (OldBitmap != IntPtr.Zero) NativeMethods.SelectObject(Dc, OldBitmap);
                    if (Bitmap != IntPtr.Zero) NativeMethods.DeleteObject(Bitmap);
                    NativeMethods.DeleteDC(Dc);
                }
                if (_screenDc != IntPtr.Zero) NativeMethods.ReleaseDC(IntPtr.Zero, _screenDc);

                Dc = Bitmap = OldBitmap = Bits = _screenDc = IntPtr.Zero;
                _px = null;
            }
        }

        static uint Pre(uint rgb, float a)
        {
            uint b = (rgb >> 16) & 0xFF;
            uint g = (rgb >> 8) & 0xFF;
            uint r = rgb & 0xFF;
            b = (uint)(b * a);
            g = (uint)(g * a);
            r = (uint)(r * a);
            return (b << 16) | (g << 8) | r;
        }

        class MenuPopup
        {
            ~MenuPopup()
            {

                if (cachedCv != null)
                {
                    try { cachedCv.Destroy(); } catch { }
                    cachedCv = null;
                }
            }

            const int WM_PAINT = 0x000F;
            const int WM_ERASEBKGND = 0x0014;
            const int WM_MOUSEMOVE = 0x0200;
            const int WM_LBUTTONDOWN = 0x0201;
            const int WM_LBUTTONUP = 0x0202;
            const int WM_RBUTTONDOWN = 0x0204;
            const int WM_KEYDOWN = 0x0100;
            const int WM_KILLFOCUS = 0x0008;

            const uint WS_POPUP = 0x80000000;
            const uint WS_EX_TOPMOST = 0x00000008;
            const uint WS_EX_TOOLWINDOW = 0x00000080;

            const int SW_SHOWNOACTIVATE = 4;
            const uint WM_COMMAND = 0x0111;

            const uint DT_VCENTER = 0x00000004;
            const uint DT_SINGLELINE = 0x00000020;
            const uint DT_NOPREFIX = 0x00000800;

            delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

            static readonly WndProcDelegate s_StaticProc = StaticWndProc;
            static bool s_ClassReady;
            const string kClassName = "LiquidGlassMenuPopup";

            IntPtr hMenuSub;
            IntPtr hwnd;
            IntPtr unityHwnd;
            MenuPopup parent;
            MenuPopup child;
            GCHandle handle;
            float scale;
            int itemH;
            int width;
            int height;
            int screenX;
            int screenY;
            public int openedIndex;
            public bool keepOtherFocus;

            NativeMethods.POINT openedCursor;

            int triggerIndex = -1;

            public IntPtr commandTarget;
            int hover = -1;

            int lastHover = -1;

            UiPalette lastPal;

            LayeredCanvas cachedCv;
            int selectedId;
            bool alive = true;
            bool shiftDown;

            struct Item
            {
                public string text;
                public int id;
                public bool sep;
                public bool enabled;
                public bool check;
                public bool hasSub;
                public IntPtr sub;
            }
            List<Item> items = new List<Item>();

            int[] tops = new int[0];
            int[] heights = new int[0];

            int Gutter => Mathf.RoundToInt(36f * scale);

            int SepH => Mathf.Max(6, Mathf.RoundToInt(9f * scale));

            int CornerRadius
            {
                get
                {
                    int cap = Mathf.Max(2, Mathf.Min(width, height) / 4);
                    return Mathf.Clamp(Mathf.RoundToInt(12f * scale), 0, cap);
                }
            }

            public MenuPopup(IntPtr sub, int x, int y, IntPtr unity, float sc, MenuPopup parent)
            {
                hMenuSub = sub;
                unityHwnd = unity;
                commandTarget = unity;
                scale = sc;
                this.parent = parent;
                screenX = x;
                screenY = y;
                itemH = Mathf.RoundToInt(22f * scale);
                BuildItems();
                ComputeSize();
            }

            void BuildItems()
            {
                items.Clear();
                int count = NativeMethods.GetMenuItemCount(hMenuSub);
                if (count <= 0) return;

                for (int i = 0; i < count; i++)
                {
                    uint state = NativeMethods.GetMenuState(hMenuSub, (uint)i, MacMenuBar.MF_BYPOSITION);
                    Item it = new Item();

                    if ((state & NativeMethods.MF_SEPARATOR) != 0)
                    {
                        it.sep = true;
                    }
                    else
                    {
                        it.enabled = ((state & (NativeMethods.MF_DISABLED | NativeMethods.MF_GRAYED)) == 0);
                        it.check = ((state & NativeMethods.MF_CHECKED) != 0);
                        it.hasSub = ((state & NativeMethods.MF_POPUP) != 0);
                        if (it.hasSub) it.sub = NativeMethods.GetSubMenu(hMenuSub, i);

                        var buf = new StringBuilder(256);
                        NativeMethods.GetMenuString(hMenuSub, (uint)i, buf, 256, MacMenuBar.MF_BYPOSITION);
                        it.text = buf.ToString().Replace("&", "");
                        it.id = NativeMethods.GetMenuItemID(hMenuSub, i);
                    }
                    items.Add(it);
                }
            }

            void ComputeSize()
            {
                IntPtr dc = NativeMethods.GetDC(IntPtr.Zero);
                int maxText = 0;
                bool anySub = false;
                int fh = -Mathf.Max(9, Mathf.RoundToInt(12f * scale));
                IntPtr font = NativeMethods.CreateFont(fh, 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 0, 0, "Segoe UI");
                IntPtr old = IntPtr.Zero;
                if (dc != IntPtr.Zero) old = NativeMethods.SelectObject(dc, font);

                foreach (var it in items)
                {
                    if (it.sep) continue;
                    int w = it.text.Length == 0 ? 40 : MeasureText(dc, it.text);
                    if (w > maxText) maxText = w;
                    if (it.hasSub) anySub = true;
                }

                if (dc != IntPtr.Zero) { NativeMethods.SelectObject(dc, old); NativeMethods.ReleaseDC(IntPtr.Zero, dc); }
                if (font != IntPtr.Zero) NativeMethods.DeleteObject(font);

                int pad = Mathf.RoundToInt(4f * scale);
                int sepH = SepH;

                tops = new int[items.Count];
                heights = new int[items.Count];
                int y = pad;
                for (int i = 0; i < items.Count; i++)
                {
                    int h = items[i].sep ? sepH : itemH;
                    tops[i] = y;
                    heights[i] = h;
                    y += h;
                }
                height = y + pad;

                int gutter = Gutter;
                int arrow = anySub ? Mathf.RoundToInt(20f * scale) : Mathf.RoundToInt(10f * scale);
                int padR = Mathf.RoundToInt(8f * scale);
                width = gutter + maxText + arrow + padR;
                int minW = Mathf.RoundToInt(120f * scale);
                if (width < minW) width = minW;
            }

            int MeasureText(IntPtr dc, string text)
            {
                if (dc == IntPtr.Zero || string.IsNullOrEmpty(text)) return text.Length * 8;
                var size = new NativeMethods.SIZE();
                if (NativeMethods.GetTextExtentPoint32(dc, text, text.Length, ref size))
                    return size.cx;
                return text.Length * 8;
            }

            public void Show()
            {
                EnsureClass();

                IntPtr hInst = NativeMethods.GetModuleHandle(null);

                hwnd = NativeMethods.CreateWindowEx(
                    WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_LAYERED,
                    kClassName, "Popup",
                    WS_POPUP,
                    screenX, screenY, width, height,
                    unityHwnd, IntPtr.Zero, hInst, IntPtr.Zero);

                if (hwnd == IntPtr.Zero) { alive = false; return; }

                handle = GCHandle.Alloc(this);
                NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWLP_USERDATA, GCHandle.ToIntPtr(handle));

                Render();

                NativeMethods.GetCursorPos(out openedCursor);

                NativeMethods.ShowWindow(hwnd, SW_SHOWNOACTIVATE);
                NativeMethods.SetCapture(hwnd);
                NativeMethods.SetFocus(hwnd);
                MacTitleBar.Log($"MenuPopup.Show rect=({screenX},{screenY}) {width}x{height} items={items.Count} hwnd=0x{hwnd.ToInt64():X}");
            }

            public void CloseChain()
            {
                if (!alive) return;
                alive = false;
                if (parent == null)
                    MacTitleBar.Log($"MenuPopup.CloseChain 根弹层关闭 items={items.Count}");

                if (child != null) { child.CloseChain(); child = null; }
                if (parent != null && parent.child == this) parent.child = null;

                if (hwnd != IntPtr.Zero)
                {

                    if (NativeMethods.GetCapture() == hwnd)
                    {
                        NativeMethods.ReleaseCapture();

                        MenuPopup up = parent;
                        while (up != null && (!up.alive || up.hwnd == IntPtr.Zero)) up = up.parent;
                        if (up != null) NativeMethods.SetCapture(up.hwnd);
                    }

                    NativeMethods.DestroyWindow(hwnd);
                    if (handle.IsAllocated) handle.Free();
                    hwnd = IntPtr.Zero;
                }

                if (cachedCv != null)
                {
                    try { cachedCv.Destroy(); } catch { }
                    cachedCv = null;
                }

                if (parent != null)
                {
                    try { if (parent.hwnd != IntPtr.Zero) NativeMethods.SetFocus(parent.hwnd); } catch { }
                }
                else
                {

                    if (!keepOtherFocus)
                    {
                        try { NativeMethods.SetForegroundWindow(unityHwnd); } catch { }
                    }
                    if (s_ActivePopup == this) s_ActivePopup = null;
                }
            }

            static void EnsureClass()
            {
                if (s_ClassReady) return;
                s_ClassReady = true;

                var wc = new NativeMethods.WNDCLASSEX
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX)),
                    style = 0,
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_StaticProc),
                    cbClsExtra = 0,
                    cbWndExtra = 0,
                    hInstance = NativeMethods.GetModuleHandle(null),
                    hIcon = IntPtr.Zero,
                    hCursor = NativeMethods.LoadCursor(IntPtr.Zero, 32512),
                    hbrBackground = IntPtr.Zero,
                    lpszMenuName = null,
                    lpszClassName = kClassName,
                    hIconSm = IntPtr.Zero,
                };
                if (NativeMethods.RegisterClassEx(ref wc) == 0)
                {

                    if (NativeMethods.UnregisterClass(kClassName, NativeMethods.GetModuleHandle(null)))
                    {
                        if (NativeMethods.RegisterClassEx(ref wc) == 0)
                            MacTitleBar.Log("MenuPopup 窗口类重新注册失败");
                    }
                    else
                    {
                        MacTitleBar.Log("MenuPopup 窗口类注销失败（有窗口未销毁？）");
                    }
                }
            }

            static IntPtr StaticWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
            {
                MenuPopup self = null;
                try
                {
                    IntPtr ud = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA);
                    if (ud != IntPtr.Zero) self = (MenuPopup)GCHandle.FromIntPtr(ud).Target;
                }
                catch { }
                if (self != null && self.alive) return self.Proc(msg, wParam, lParam);
                return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
            }

            IntPtr Proc(uint msg, IntPtr wParam, IntPtr lParam)
            {
                try
                {
                    switch (msg)
                    {
                        case WM_PAINT: Paint(); return IntPtr.Zero;
                        case WM_ERASEBKGND: return (IntPtr)1;
                        case WM_MOUSEMOVE: OnMove(lParam); return IntPtr.Zero;
                        case WM_LBUTTONDOWN: OnDown(lParam); return IntPtr.Zero;
                        case WM_LBUTTONUP: OnUp(lParam); return IntPtr.Zero;
                        case WM_RBUTTONDOWN: CloseChain(); return IntPtr.Zero;
                        case WM_KEYDOWN: OnKey(wParam); return IntPtr.Zero;

                        case 0x0020:
                            break;
                        case WM_KILLFOCUS:

                            return IntPtr.Zero;
                    }
                    return NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);
                }
                catch { return IntPtr.Zero; }
            }

            void OnMove(IntPtr lParam)
            {

                {
                    NativeMethods.POINT cp0;
                    bool moved = NativeMethods.GetCursorPos(out cp0)
                                 && (cp0.X != openedCursor.X || cp0.Y != openedCursor.Y);
                    if (moved && !OverMenuBar(cp0))
                    {
                        if (parent == null)
                        {
                            if (HandleCursorLeft(cp0)) return;
                        }
                        else if (!ChainContains(this, cp0))
                        {

                            bool inParent = parent.hwnd != IntPtr.Zero && PointInWindow(parent.hwnd, cp0);
                            if (!inParent)
                            {
                                if (HandleCursorLeft(cp0)) return;
                            }
                            else
                            {
                                NativeMethods.POINT rel = cp0;
                                NativeMethods.ScreenToClient(parent.hwnd, ref rel);
                                bool onTrigger = parent.HitTest(rel.X, rel.Y) == triggerIndex;
                                if (!onTrigger) { CloseChain(); return; }
                            }
                        }
                    }
                }

                int cx = (short)(lParam.ToInt64() & 0xFFFF);
                int cy = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                int idx = HitTest(cx, cy);
                if (idx != hover)
                {
                    if (parent == null)
                        MacTitleBar.Log($"MenuPopup.OnMove 根弹层 cx={cx} cy={cy} idx={idx} (原 {hover})");
                    hover = idx;

                    Render();
                }

                if (idx >= 0 && items[idx].hasSub && items[idx].enabled)
                {
                    if (child == null || child.hMenuSub != items[idx].sub)
                        OpenChild(idx);
                }
                else if (child != null)
                {

                    NativeMethods.POINT p; NativeMethods.GetCursorPos(out p);
                    if (!PointInWindow(child.hwnd, p)) child.CloseChain();
                }

                if (parent == null)
                {
                    NativeMethods.POINT cp; NativeMethods.GetCursorPos(out cp);

                    int hitIndex = MacMenuBar.BarIndexAt(cp);
                    if (hitIndex >= 0 && hitIndex != openedIndex)
                    {

                        double now = EditorApplication.timeSinceStartup;
                        if (hitIndex == s_LastSwitchIndex && now - s_LastSwitchTime < 0.1)
                            return;

                        s_LastSwitchIndex = hitIndex;
                        s_LastSwitchTime = now;

                        CloseChain();
                        OpenMenu(hitIndex);
                        return;
                    }
                }
            }

            void OnDown(IntPtr lParam)
            {
                int cx = (short)(lParam.ToInt64() & 0xFFFF);
                int cy = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                int idx = HitTest(cx, cy);

                NativeMethods.POINT scr;
                if (NativeMethods.GetCursorPos(out scr))
                {
                    int barIdx = MacMenuBar.BarIndexAt(scr);
                    if (barIdx >= 0)
                    {
                        MenuPopup root = Root();
                        if (barIdx == root.openedIndex)
                        {

                            MacMenuBar.CloseActivePopup();
                        }
                        else
                        {

                            MacMenuBar.CloseActivePopup();
                            MacMenuBar.OpenMenu(barIdx);
                        }
                        return;
                    }
                }

                if (idx < 0)
                {

                    NativeMethods.POINT p; NativeMethods.GetCursorPos(out p);
                    if (!OverMenuBar(p)) CloseChain();
                }

            }

            void OnUp(IntPtr lParam)
            {

                NativeMethods.POINT scr;
                if (NativeMethods.GetCursorPos(out scr) && MacMenuBar.BarIndexAt(scr) >= 0) return;

                int cx = (short)(lParam.ToInt64() & 0xFFFF);
                int cy = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                int idx = HitTest(cx, cy);

                if (idx >= 0 && !items[idx].sep && items[idx].enabled)
                {
                    if (items[idx].hasSub)
                    {
                        OpenChild(idx);
                    }
                    else
                    {
                        selectedId = items[idx].id;
                        CloseChain();
                        if (selectedId > 0)
                            NativeMethods.PostMessage(commandTarget, WM_COMMAND, (IntPtr)selectedId, IntPtr.Zero);
                    }
                }
            }

            void OnKey(IntPtr wParam)
            {
                int vk = (int)(wParam.ToInt64() & 0xFFFF);
                if (vk == 0x1B) { CloseChain(); return; }
                if (vk == 0x0D || vk == 0x20)
                {
                    if (hover >= 0 && !items[hover].sep && items[hover].enabled)
                    {
                        if (items[hover].hasSub) OpenChild(hover);
                        else
                        {
                            selectedId = items[hover].id;
                            CloseChain();
                            if (selectedId > 0)
                                NativeMethods.PostMessage(commandTarget, WM_COMMAND, (IntPtr)selectedId, IntPtr.Zero);
                        }
                    }
                    return;
                }
                if (vk == 0x28) MoveHover(1);
                else if (vk == 0x26) MoveHover(-1);
                else if (vk == 0x27 && hover >= 0 && items[hover].hasSub) OpenChild(hover);
                else if (vk == 0x25 && parent != null) CloseChain();
            }

            void MoveHover(int dir)
            {
                int n = items.Count;
                if (n == 0) return;
                int i = hover < 0 ? (dir > 0 ? -1 : 0) : hover;
                for (int k = 0; k < n; k++)
                {
                    i += dir;
                    if (i < 0) i = n - 1;
                    if (i >= n) i = 0;
                    if (!items[i].sep && items[i].enabled) { hover = i; Invalidate(); return; }
                }
            }

            void OpenChild(int idx)
            {
                if (child != null) child.CloseChain();
                int subX = screenX + width;
                int subY = screenY + (idx < tops.Length ? tops[idx] : Mathf.RoundToInt(4f * scale));
                child = new MenuPopup(items[idx].sub, subX, subY, unityHwnd, scale, this);
                child.triggerIndex = idx;
                child.Show();
                NativeMethods.SetFocus(child.hwnd);
                MacTitleBar.Log($"MenuPopup.OpenChild idx={idx} sub=0x{items[idx].sub.ToInt64():X} childHwnd=0x{(child.hwnd != IntPtr.Zero ? child.hwnd.ToInt64() : 0):X}");
            }

            MenuPopup Root()
            {
                MenuPopup p = this;
                while (p.parent != null) p = p.parent;
                return p;
            }

            static bool ChainContains(MenuPopup popup, NativeMethods.POINT p)
            {
                for (MenuPopup q = popup; q != null; q = q.child)
                {
                    if (q.hwnd != IntPtr.Zero && PointInWindow(q.hwnd, p)) return true;
                }
                return false;
            }

            bool HandleCursorLeft(NativeMethods.POINT cp)
            {

                if (cp.X == openedCursor.X && cp.Y == openedCursor.Y) return false;

                MenuPopup root = Root();
                if (ChainContains(root, cp)) return false;
                if (OverMenuBar(cp)) return false;

                MacTitleBar.Log($"光标离开菜单链 -> 关闭 cp=({cp.X},{cp.Y}) open=({openedCursor.X},{openedCursor.Y})");
                MacMenuBar.CloseActivePopup();
                return true;
            }

            int HitTest(int cx, int cy)
            {
                if (cx < 0 || cx >= width) return -1;
                for (int i = 0; i < items.Count && i < tops.Length; i++)
                {
                    int t = tops[i];
                    int h = heights[i];
                    if (h <= 0) continue;
                    if (cy >= t && cy < t + h) return items[i].sep ? -1 : i;
                }
                return -1;
            }

            void Invalidate()
            {

                if (hwnd != IntPtr.Zero) NativeMethods.InvalidateRect(hwnd, IntPtr.Zero, false);
            }

            static bool PointInWindow(IntPtr h, NativeMethods.POINT p)
            {
                NativeMethods.RECT r;
                if (!NativeMethods.GetWindowRect(h, out r)) return false;
                return p.X >= r.Left && p.X <= r.Right && p.Y >= r.Top && p.Y <= r.Bottom;
            }

            static bool OverMenuBar(NativeMethods.POINT p)
            {
                return MacMenuBar.BarHitTest(p, out _);
            }

            public bool ChainContainsPoint(NativeMethods.POINT p)
            {
                return ChainContains(this, p);
            }

            public void EnsureCapture()
            {
                if (!alive) return;

                NativeMethods.POINT cp;
                if (!NativeMethods.GetCursorPos(out cp)) return;

                MenuPopup target = this;
                for (MenuPopup q = this; q != null; q = q.child)
                {
                    if (q.hwnd != IntPtr.Zero && PointInWindow(q.hwnd, cp)) target = q;
                }

                if (target.hwnd == IntPtr.Zero) return;
                if (NativeMethods.GetCapture() == target.hwnd) return;

                NativeMethods.SetCapture(target.hwnd);
            }

            void Paint()
            {

                NativeMethods.PAINTSTRUCT ps;
                if (NativeMethods.BeginPaint(hwnd, out ps) != IntPtr.Zero)
                    NativeMethods.EndPaint(hwnd, ref ps);

                Render();
            }

            void Render()
            {
                if (hwnd == IntPtr.Zero || width <= 0 || height <= 0) return;

                LayeredCanvas cv = cachedCv;
                bool fresh = (cv == null || cv.Width != width || cv.Height != height);
                if (fresh)
                {
                    if (cv != null) cv.Destroy();
                    cv = LayeredCanvas.Create(width, height);
                    cachedCv = cv;
                }
                if (cv == null) return;

                IntPtr font = IntPtr.Zero;
                try
                {
                    IntPtr dc = cv.Dc;
                    int w = width, h = height;

                    {

                        var pal = CurrentPalette(false);
                        lastPal = pal;

                        cv.FillColor(pal.popupBg, 1f);

                        uint border = GdiToRgb(pal.popupBorder);
                        cv.StrokeRoundRect(0, 0, w, h, CornerRadius, border);

                        NativeMethods.SetBkMode(dc, 1);

                        int fh = -Mathf.Max(9, Mathf.RoundToInt(12f * scale));
                        font = MacMenuBar.CreateUiFont(fh, 400);
                        IntPtr old = NativeMethods.SelectObject(dc, font);

                        int gutter = Gutter;

                        for (int i = 0; i < items.Count; i++)
                        {
                            int iy = i < tops.Length ? tops[i] : 0;
                            int ih = i < heights.Length ? heights[i] : itemH;
                            var it = items[i];

                            if (it.sep)
                            {

                                int ly = iy + ih / 2;
                                cv.FillRectColor(1, ly, w - 1, ly + 1, GdiToRgb(pal.popupSep), 1f);
                                continue;
                            }

                            NativeMethods.SetTextColor(dc, !it.enabled
                                ? Pre(pal.popupText, 0.45f)
                                : pal.popupText);
                            var tr = new NativeMethods.RECT
                            {
                                Left = gutter,
                                Top = iy,
                                Right = w - Mathf.RoundToInt(20f * scale),
                                Bottom = iy + ih
                            };
                            NativeMethods.DrawText(dc, it.text, it.text.Length, ref tr, DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);

                            if (it.check)
                            {
                                NativeMethods.SetTextColor(dc, !it.enabled
                                    ? Pre(pal.popupText, 0.45f)
                                    : pal.popupText);
                                var cr = new NativeMethods.RECT
                                {

                                    Left = Mathf.RoundToInt(6f * scale),
                                    Top = iy,
                                    Right = gutter - Mathf.RoundToInt(8f * scale),
                                    Bottom = iy + ih
                                };
                                NativeMethods.DrawText(dc, "✓", 1, ref cr, DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                            }

                            if (it.hasSub)
                            {
                                NativeMethods.SetTextColor(dc, !it.enabled
                                    ? Pre(pal.popupText, 0.35f)
                                    : Pre(pal.popupText, 0.75f));
                                var ar = new NativeMethods.RECT
                                {
                                    Left = w - Mathf.RoundToInt(20f * scale),
                                    Top = iy,
                                    Right = w - 4,
                                    Bottom = iy + ih
                                };
                                NativeMethods.DrawText(dc, "›", 1, ref ar, DT_VCENTER | DT_SINGLELINE);
                            }
                        }

                        NativeMethods.SelectObject(dc, old);
                    }

                    PaintHover(cv);

                    cv.RoundCorners(CornerRadius);

                    cv.Commit(hwnd, screenX, screenY);
                }
                catch { }
                finally
                {
                    if (font != IntPtr.Zero) NativeMethods.DeleteObject(font);

                }
            }

            void PaintHover(LayeredCanvas cv)
            {
                if (cv == null) return;
                int w = width, h = height;
                var pal = lastPal;

                int gutter = Gutter;
                int arrowW = Mathf.RoundToInt(20f * scale);

                int hl = 1;
                int hr = w - 1;

                System.Action<int> redrawOne = (int idx) =>
                {
                    if (idx < 0 || idx >= items.Count) return;
                    var it = items[idx];
                    if (it.sep) return;
                    int iy = idx < tops.Length ? tops[idx] : 0;
                    int ih = idx < heights.Length ? heights[idx] : itemH;

                    cv.FillRectColor(hl, iy, hr, iy + ih, pal.popupBg, 1f);

                    IntPtr dc = cv.Dc;
                    IntPtr font = IntPtr.Zero;
                    IntPtr oldFont = IntPtr.Zero;
                    try
                    {
                        int fh = -Mathf.Max(9, Mathf.RoundToInt(12f * scale));
                        font = MacMenuBar.CreateUiFont(fh, 400);
                        oldFont = NativeMethods.SelectObject(dc, font);

                        NativeMethods.SetBkMode(dc, 1);
                        NativeMethods.SetTextColor(dc, !it.enabled
                            ? Pre(pal.popupText, 0.45f)
                            : pal.popupText);

                        var tr = new NativeMethods.RECT
                        {
                            Left = gutter,
                            Top = iy,
                            Right = w - arrowW,
                            Bottom = iy + ih
                        };
                        NativeMethods.DrawText(dc, it.text, it.text.Length, ref tr,
                            DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);

                        if (it.check)
                        {
                            NativeMethods.SetTextColor(dc, !it.enabled
                                ? Pre(pal.popupText, 0.45f)
                                : pal.popupText);
                            var cr = new NativeMethods.RECT
                            {
                                Left = Mathf.RoundToInt(6f * scale),
                                Top = iy,
                                Right = gutter - Mathf.RoundToInt(8f * scale),
                                Bottom = iy + ih
                            };
                            NativeMethods.DrawText(dc, "✓", 1, ref cr, DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                        }

                        if (it.hasSub)
                        {
                            NativeMethods.SetTextColor(dc, it.enabled
                                ? Pre(pal.popupText, 0.75f)
                                : Pre(pal.popupText, 0.35f));
                            var ar = new NativeMethods.RECT
                            {
                                Left = w - arrowW,
                                Top = iy,
                                Right = w - 4,
                                Bottom = iy + ih
                            };
                            NativeMethods.DrawText(dc, "›", 1, ref ar, DT_VCENTER | DT_SINGLELINE);
                        }
                    }
                    finally
                    {
                        if (oldFont != IntPtr.Zero) NativeMethods.SelectObject(dc, oldFont);
                        if (font != IntPtr.Zero) NativeMethods.DeleteObject(font);
                    }
                };

                if (lastHover >= 0 && lastHover != hover)
                    redrawOne(lastHover);

                if (hover >= 0 && hover < items.Count && !items[hover].sep && items[hover].enabled)
                {
                    int iy = hover < tops.Length ? tops[hover] : 0;
                    int ih = hover < heights.Length ? heights[hover] : itemH;

                    cv.FillRectColor(hl, iy, hr, iy + ih, pal.popupHoverFill, 1f);

                    var it = items[hover];
                    IntPtr dc = cv.Dc;
                    IntPtr font = IntPtr.Zero;
                    IntPtr oldFont = IntPtr.Zero;
                    try
                    {
                        int fh = -Mathf.Max(9, Mathf.RoundToInt(12f * scale));
                        font = MacMenuBar.CreateUiFont(fh, 400);
                        oldFont = NativeMethods.SelectObject(dc, font);

                        NativeMethods.SetBkMode(dc, 1);
                        NativeMethods.SetTextColor(dc, !it.enabled
                            ? Pre(pal.popupTextHover, 0.45f)
                            : pal.popupTextHover);

                        var tr = new NativeMethods.RECT
                        {
                            Left = gutter,
                            Top = iy,
                            Right = w - arrowW,
                            Bottom = iy + ih
                        };
                        NativeMethods.DrawText(dc, it.text, it.text.Length, ref tr,
                            DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);

                        if (it.check)
                        {
                            NativeMethods.SetTextColor(dc, !it.enabled
                                ? Pre(pal.popupTextHover, 0.45f)
                                : pal.popupTextHover);
                            var cr = new NativeMethods.RECT
                            {
                                Left = Mathf.RoundToInt(6f * scale),
                                Top = iy,
                                Right = gutter - Mathf.RoundToInt(8f * scale),
                                Bottom = iy + ih
                            };
                            NativeMethods.DrawText(dc, "✓", 1, ref cr, DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
                        }

                        if (it.hasSub)
                        {
                            NativeMethods.SetTextColor(dc, it.enabled
                                ? Pre(pal.popupTextHover, 0.75f)
                                : Pre(pal.popupTextHover, 0.35f));
                            var ar = new NativeMethods.RECT
                            {
                                Left = w - arrowW,
                                Top = iy,
                                Right = w - 4,
                                Bottom = iy + ih
                            };
                            NativeMethods.DrawText(dc, "›", 1, ref ar, DT_VCENTER | DT_SINGLELINE);
                        }
                    }
                    finally
                    {
                        if (oldFont != IntPtr.Zero) NativeMethods.SelectObject(dc, oldFont);
                        if (font != IntPtr.Zero) NativeMethods.DeleteObject(font);
                    }
                }

                lastHover = hover;
            }

        }
    }
}
