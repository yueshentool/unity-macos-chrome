// Copyright (C) 2026 Liquid Glass contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;

namespace LiquidGlass.Editor
{

    internal static class GlassWindowChrome
    {
        public static void Apply()
        {
            var hwnd = UnityMainWindow.Handle;
            if (hwnd == IntPtr.Zero) return;

            var settings = LiquidGlassSettings.instance;

            int on = 1;
            TrySet(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, on);

            if (settings.roundedCorners)
            {
                TrySet(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, NativeMethods.DWMWCP_ROUND);
            }

            if (settings.acrylicBackdrop && !MacTitleBar.Installed)
            {
                TrySet(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, NativeMethods.DWMSBT_TRANSIENTWINDOW);
            }

            TrySet(hwnd, NativeMethods.DWMWA_BORDER_COLOR, unchecked((int)NativeMethods.DWMWA_COLOR_NONE));

            NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_NONE);
        }

        public static void Restore()
        {
            var hwnd = UnityMainWindow.Handle;
            if (hwnd == IntPtr.Zero) return;

            NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_NONE);
            TrySet(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, NativeMethods.DWMWCP_DEFAULT);
            TrySet(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, NativeMethods.DWMSBT_AUTO);
        }

        static void TrySet(IntPtr hwnd, int attribute, int value)
        {
            try
            {
                int v = value;
                NativeMethods.DwmSetWindowAttribute(hwnd, attribute, ref v, sizeof(int));
            }
            catch
            {

            }
        }
    }
}
