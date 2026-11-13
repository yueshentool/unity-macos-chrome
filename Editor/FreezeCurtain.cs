// Copyright (C) 2026 Liquid Glass contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LiquidGlass.Editor
{

    internal static class FreezeCurtain
    {
        const string kClass = "LiquidGlassFreezeCurtain";
        const string kTitle = "LiquidGlassFreezeCurtain";

        const uint WS_POPUP = 0x80000000u;
        const uint WS_EX_LAYERED = 0x00080000u;
        const uint WS_EX_TOOLWINDOW = 0x00000080u;
        const uint WS_EX_TOPMOST = 0x00000008u;
        const uint WS_EX_NOACTIVATE = 0x08000000u;

        const uint ULW_ALPHA = 0x00000002u;
        const int SW_SHOWNOACTIVATE = 4;
        const uint DIB_RGB_COLORS = 0u;

        static IntPtr s_Hwnd;

        static IntPtr s_NativeDefProc;
        static bool s_ClassReady;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WNDCLASSEX
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
        struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SIZE
        {
            public int cx;
            public int cy;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr GetModuleHandleW(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentProcessId();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern ushort RegisterClassExW(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref NativeMethods.POINT pptDst, ref SIZE psize, IntPtr hdcSrc,
            ref NativeMethods.POINT pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);

        internal static bool Exists => FindOwnCurtain() != IntPtr.Zero;

        internal static void Show(IntPtr mainHwnd)
        {
            try
            {
                if (mainHwnd == IntPtr.Zero) return;
                if (!NativeMethods.IsWindow(mainHwnd)) return;
                if (NativeMethods.IsIconic(mainHwnd)) return;
                if (!NativeMethods.GetWindowRect(mainHwnd, out var wr)) return;

                var origin = new NativeMethods.POINT { X = 0, Y = 0 };
                if (!NativeMethods.ClientToScreen(mainHwnd, ref origin)) return;

                int w = wr.Right - wr.Left;
                int h = origin.Y - wr.Top;
                if (w <= 0 || h <= 0 || h > 200)
                {
                    MacTitleBar.Log($"FreezeCurtain 跳过：几何异常 w={w} h={h}");
                    return;
                }

                Hide();

                if (!EnsureClass()) return;

                s_Hwnd = CreateWindowExW(
                    WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
                    kClass, kTitle, WS_POPUP,
                    wr.Left, wr.Top, w, h,
                    IntPtr.Zero, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);

                if (s_Hwnd == IntPtr.Zero)
                {
                    MacTitleBar.Log("FreezeCurtain 创建失败 err=" + Marshal.GetLastWin32Error());
                    return;
                }

                if (!Paint(s_Hwnd, wr.Left, wr.Top, w, h))
                {
                    MacTitleBar.Log("FreezeCurtain 快照失败");
                    Hide();
                    return;
                }

                NativeMethods.ShowWindow(s_Hwnd, SW_SHOWNOACTIVATE);
                MacTitleBar.Log($"FreezeCurtain 已铺 ({wr.Left},{wr.Top}) {w}x{h}");
            }
            catch (Exception e)
            {
                MacTitleBar.Log("FreezeCurtain 异常：" + e.GetType().Name + " " + e.Message);
                try { Hide(); } catch { }
            }
        }

        internal static void Hide()
        {
            try
            {
                IntPtr h = s_Hwnd;
                if (h != IntPtr.Zero && NativeMethods.IsWindow(h)) DestroyWindow(h);
                s_Hwnd = IntPtr.Zero;

                IntPtr leftover = FindOwnCurtain();
                if (leftover != IntPtr.Zero)
                {
                    DestroyWindow(leftover);
                    MacTitleBar.Log("FreezeCurtain 撤掉残留幕布");
                }
            }
            catch { }
        }

        static bool EnsureClass()
        {
            if (s_ClassReady) return true;

            IntPtr def = NativeDefProc;
            if (def == IntPtr.Zero)
            {
                MacTitleBar.Log("FreezeCurtain 跳过：取不到 DefWindowProcW");
                return false;
            }

            IntPtr inst = GetModuleHandleW(null);
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                style = 0,
                lpfnWndProc = def,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = inst,
                hIcon = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                lpszClassName = kClass,
                hIconSm = IntPtr.Zero,
            };

            ushort atom = RegisterClassExW(ref wc);
            if (atom == 0)
            {
                int err = Marshal.GetLastWin32Error();

                if (err != 1410)
                {
                    MacTitleBar.Log("FreezeCurtain 注册类失败 err=" + err);
                    return false;
                }
            }

            s_ClassReady = true;
            return true;
        }

        static IntPtr NativeDefProc
        {
            get
            {
                if (s_NativeDefProc != IntPtr.Zero) return s_NativeDefProc;
                try
                {

                    IntPtr u32 = GetModuleHandleW("user32.dll");
                    if (u32 != IntPtr.Zero)
                        s_NativeDefProc = GetProcAddress(u32, "DefWindowProcW");
                }
                catch { }
                return s_NativeDefProc;
            }
        }

        static bool Paint(IntPtr hwnd, int x, int y, int w, int h)
        {
            IntPtr screen = NativeMethods.GetDC(IntPtr.Zero);
            if (screen == IntPtr.Zero) return false;

            IntPtr memDC = IntPtr.Zero;
            IntPtr bmp = IntPtr.Zero;
            IntPtr oldBmp = IntPtr.Zero;

            try
            {
                var bmi = new NativeMethods.BITMAPINFO();
                bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(NativeMethods.BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = w;
                bmi.bmiHeader.biHeight = -h;
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = 0;

                memDC = NativeMethods.CreateCompatibleDC(screen);
                if (memDC == IntPtr.Zero) return false;

                bmp = NativeMethods.CreateDIBSection(screen, ref bmi, DIB_RGB_COLORS,
                    out IntPtr bits, IntPtr.Zero, 0);
                if (bmp == IntPtr.Zero || bits == IntPtr.Zero) return false;

                oldBmp = NativeMethods.SelectObject(memDC, bmp);

                if (!NativeMethods.BitBlt(memDC, 0, 0, w, h, screen, x, y, NativeMethods.SRCCOPY))
                    return false;

                int len = w * h * 4;
                byte[] px = new byte[len];
                Marshal.Copy(bits, px, 0, len);
                for (int i = 3; i < len; i += 4) px[i] = 255;
                Marshal.Copy(px, 0, bits, len);

                var dst = new NativeMethods.POINT { X = x, Y = y };
                var size = new SIZE { cx = w, cy = h };
                var src = new NativeMethods.POINT { X = 0, Y = 0 };
                var blend = new BLENDFUNCTION
                {
                    BlendOp = 0,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = 1,
                };

                return UpdateLayeredWindow(hwnd, IntPtr.Zero, ref dst, ref size, memDC,
                    ref src, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                if (memDC != IntPtr.Zero && oldBmp != IntPtr.Zero)
                    NativeMethods.SelectObject(memDC, oldBmp);
                if (bmp != IntPtr.Zero) NativeMethods.DeleteObject(bmp);
                if (memDC != IntPtr.Zero) NativeMethods.DeleteDC(memDC);
                NativeMethods.ReleaseDC(IntPtr.Zero, screen);
            }
        }

        static IntPtr FindOwnCurtain()
        {
            IntPtr found = IntPtr.Zero;
            uint self = GetCurrentProcessId();
            var sb = new StringBuilder(64);

            try
            {
                NativeMethods.EnumWindows((h, _) =>
                {
                    NativeMethods.GetWindowThreadProcessId(h, out uint pid);
                    if (pid != self) return true;

                    sb.Length = 0;
                    if (NativeMethods.GetClassName(h, sb, sb.Capacity) <= 0) return true;
                    if (sb.ToString() != kClass) return true;

                    found = h;
                    return false;
                }, IntPtr.Zero);
            }
            catch { }

            return found;
        }
    }
}
