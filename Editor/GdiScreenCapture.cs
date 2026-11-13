// Copyright (C) 2026 Liquid Glass contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Runtime.InteropServices;

namespace LiquidGlass.Editor
{

    internal static class GdiScreenCapture
    {

        public static bool TryCaptureWindow(IntPtr hwnd, out byte[] bgra, out int width, out int height)
        {
            bgra = null;
            width = height = 0;

            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd)) return false;
            if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return false;

            width = rect.Right - rect.Left;
            height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0) return false;

            IntPtr screenDc = IntPtr.Zero;
            IntPtr memDc = IntPtr.Zero;
            IntPtr dib = IntPtr.Zero;
            IntPtr oldObj = IntPtr.Zero;

            try
            {
                screenDc = NativeMethods.GetDC(IntPtr.Zero);
                if (screenDc == IntPtr.Zero) return false;

                memDc = NativeMethods.CreateCompatibleDC(screenDc);
                if (memDc == IntPtr.Zero) return false;

                var bmi = new NativeMethods.BITMAPINFO();
                bmi.bmiHeader.biSize = Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>();
                bmi.bmiHeader.biWidth = width;
                bmi.bmiHeader.biHeight = -height;
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = 0;

                IntPtr bits;
                dib = NativeMethods.CreateDIBSection(memDc, ref bmi, 0, out bits, IntPtr.Zero, 0);
                if (dib == IntPtr.Zero || bits == IntPtr.Zero) return false;

                oldObj = NativeMethods.SelectObject(memDc, dib);

                if (!NativeMethods.PrintWindow(hwnd, memDc, NativeMethods.PW_RENDERFULLCONTENT))
                    return false;

                NativeMethods.GdiFlush();

                bgra = new byte[width * height * 4];
                Marshal.Copy(bits, bgra, 0, bgra.Length);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (memDc != IntPtr.Zero && oldObj != IntPtr.Zero)
                    NativeMethods.SelectObject(memDc, oldObj);
                if (dib != IntPtr.Zero) NativeMethods.DeleteObject(dib);
                if (memDc != IntPtr.Zero) NativeMethods.DeleteDC(memDc);
                if (screenDc != IntPtr.Zero) NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        public static bool TryCapture(int x, int y, int width, int height, out byte[] bgra)
        {
            bgra = null;
            if (width <= 0 || height <= 0) return false;

            IntPtr screenDc = IntPtr.Zero;
            IntPtr memDc = IntPtr.Zero;
            IntPtr dib = IntPtr.Zero;
            IntPtr oldObj = IntPtr.Zero;

            try
            {
                screenDc = NativeMethods.GetDC(IntPtr.Zero);
                if (screenDc == IntPtr.Zero) return false;

                memDc = NativeMethods.CreateCompatibleDC(screenDc);
                if (memDc == IntPtr.Zero) return false;

                var bmi = new NativeMethods.BITMAPINFO();
                bmi.bmiHeader.biSize = Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>();
                bmi.bmiHeader.biWidth = width;
                bmi.bmiHeader.biHeight = -height;
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = 0;

                IntPtr bits;
                dib = NativeMethods.CreateDIBSection(memDc, ref bmi, 0, out bits, IntPtr.Zero, 0);
                if (dib == IntPtr.Zero || bits == IntPtr.Zero) return false;

                oldObj = NativeMethods.SelectObject(memDc, dib);

                bool ok = NativeMethods.BitBlt(memDc, 0, 0, width, height, screenDc, x, y,
                    NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);
                if (!ok) return false;

                NativeMethods.GdiFlush();

                bgra = new byte[width * height * 4];
                Marshal.Copy(bits, bgra, 0, bgra.Length);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (memDc != IntPtr.Zero && oldObj != IntPtr.Zero)
                    NativeMethods.SelectObject(memDc, oldObj);
                if (dib != IntPtr.Zero) NativeMethods.DeleteObject(dib);
                if (memDc != IntPtr.Zero) NativeMethods.DeleteDC(memDc);
                if (screenDc != IntPtr.Zero) NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }
}
