// Copyright (C) 2026 Liquid Glass contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Diagnostics;
using System.Text;

namespace LiquidGlass.Editor
{

    internal static class UnityMainWindow
    {
        const string k_UnityClass = "UnityContainerWndClass";

        static IntPtr s_Cached = IntPtr.Zero;

        public static IntPtr Handle
        {
            get
            {
                if (s_Cached != IntPtr.Zero && NativeMethods.IsWindow(s_Cached))
                    return s_Cached;

                s_Cached = Find();
                return s_Cached;
            }
        }

        static IntPtr Find()
        {
            uint pid = (uint)Process.GetCurrentProcess().Id;
            IntPtr bestFree = IntPtr.Zero;
            IntPtr bestOwned = IntPtr.Zero;
            long freeArea = 0, ownedArea = 0;
            var sb = new StringBuilder(256);

            NativeMethods.EnumWindows((hWnd, _) =>
            {
                NativeMethods.GetWindowThreadProcessId(hWnd, out uint owner);
                if (owner != pid) return true;

                sb.Length = 0;
                NativeMethods.GetClassName(hWnd, sb, sb.Capacity);
                if (sb.ToString() != k_UnityClass) return true;

                if (!NativeMethods.GetWindowRect(hWnd, out var rect)) return true;
                long area = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);

                bool owned = NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != IntPtr.Zero;
                if (owned)
                {
                    if (area > ownedArea) { ownedArea = area; bestOwned = hWnd; }
                }
                else
                {
                    if (area > freeArea) { freeArea = area; bestFree = hWnd; }
                }
                return true;
            }, IntPtr.Zero);

            return bestFree != IntPtr.Zero ? bestFree : bestOwned;
        }

        public static bool TryGetClientRectOnScreen(out int x, out int y, out int width, out int height)
        {
            x = y = width = height = 0;
            var hwnd = Handle;
            if (hwnd == IntPtr.Zero) return false;
            if (!NativeMethods.GetClientRect(hwnd, out var rc)) return false;

            var origin = new NativeMethods.POINT { X = 0, Y = 0 };
            if (!NativeMethods.ClientToScreen(hwnd, ref origin)) return false;

            x = origin.X;
            y = origin.Y;
            width = rc.Right - rc.Left;
            height = rc.Bottom - rc.Top;
            return width > 0 && height > 0;
        }

        public static bool IsVisible
        {
            get
            {
                var hwnd = Handle;
                return hwnd != IntPtr.Zero
                       && NativeMethods.IsWindowVisible(hwnd)
                       && !NativeMethods.IsIconic(hwnd);
            }
        }
    }
}
