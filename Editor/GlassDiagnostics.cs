// Copyright (C) 2026 Liquid Glass contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LiquidGlass.Editor
{

    public static class GlassDiagnostics
    {
        [MenuItem("Tools/Liquid Glass/Run Self Test", false, 40)]
        static void SelfTestMenu()
        {
            Debug.Log(RunSelfTest());
        }

        [MenuItem("Tools/Liquid Glass/Capture Window (PNG)", false, 41)]
        static void CaptureMenu()
        {
            string path = Path.Combine(Path.GetTempPath(), "liquidglass-capture.png");
            Debug.Log("[Liquid Glass] " + Capture(path));
        }

        public static string PrepareShowcase(string path)
        {

            foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (w == null) continue;
                string title = w.titleContent != null ? w.titleContent.text : "";
                if (title.IndexOf("MCP for Unity", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    w.maximized = false;
                }
            }

            string backdropImage = Path.Combine(Path.GetDirectoryName(path), "showcase-backdrop.png");
            WriteColorfulImage(backdropImage, 1920, 1080);
            EditorUtility.OpenWithDefaultApp(backdropImage);
            System.Threading.Thread.Sleep(3000);

            var hwnd = UnityMainWindow.Handle;
            if (hwnd == IntPtr.Zero) return "FAIL: 找不到 Unity 主窗口";
            NativeMethods.GetWindowRect(hwnd, out var original);

            try
            {
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 300, 110, 1280, 820,
                    NativeMethods.SWP_SHOWWINDOW);
                System.Threading.Thread.Sleep(1600);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                System.Threading.Thread.Sleep(900);

                DesktopBackdrop.RefreshNow();
                System.Threading.Thread.Sleep(400);

                return CaptureLive(path);
            }
            finally
            {
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_NOTOPMOST,
                    original.Left, original.Top,
                    original.Right - original.Left, original.Bottom - original.Top, 0);
            }
        }

        static void WriteColorfulImage(string path, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)width;
                    float v = y / (float)height;

                    Color c = Color.HSVToRGB(Mathf.Repeat(u * 0.85f + v * 0.15f, 1f), 0.72f, 0.92f);
                    float glow1 = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.24f, 0.72f)) * 2.4f);
                    float glow2 = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.78f, 0.28f)) * 2.0f);
                    c += Color.white * (glow1 * 0.35f + glow2 * 0.28f);
                    c.a = 1f;

                    pixels[y * width + x] = c;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        public static string CaptureScreen(string path)
        {            int w = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            int h = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
            if (w <= 0 || h <= 0) return "FAIL: 拿不到屏幕尺寸";

            byte[] bgra;
            if (!GdiScreenCapture.TryCapture(0, 0, w, h, out bgra)) return "FAIL: 截屏失败";
            return WritePng(bgra, w, h, path, string.Format("OK: 全屏 {0}x{1} → {2}", w, h, path));
        }

        public static string CaptureTitleBar(string path)
        {
            var hwnd = UnityMainWindow.Handle;
            if (hwnd == IntPtr.Zero) return "FAIL: 找不到 Unity 主窗口";

            NativeMethods.GetWindowRect(hwnd, out var original);
            int w = original.Right - original.Left;
            int h = original.Bottom - original.Top;

            var origin = new NativeMethods.POINT { X = 0, Y = 0 };
            NativeMethods.ClientToScreen(hwnd, ref origin);
            int captionHeight = origin.Y - original.Top;

            int stripH = Mathf.Clamp(captionHeight + 110, 80, h);

            int screenW = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            int screenH = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);

            int capX = Mathf.Clamp(original.Left, 0, Mathf.Max(0, screenW - 1));
            int capY = Mathf.Clamp(original.Top, 0, Mathf.Max(0, screenH - 1));
            int capW = Mathf.Min(w - (capX - original.Left), screenW - capX);
            int capH = Mathf.Min(stripH - (capY - original.Top), screenH - capY);
            if (capW <= 0 || capH <= 0) return "FAIL: 窗口不在屏幕内";

            try
            {
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
                    original.Left, original.Top, w, h,
                    NativeMethods.SWP_SHOWWINDOW);
                System.Threading.Thread.Sleep(600);

                NativeMethods.RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero,
                    NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_FRAME |
                    NativeMethods.RDW_ALLCHILDREN | NativeMethods.RDW_UPDATENOW);
                System.Threading.Thread.Sleep(500);

                byte[] bgra;
                if (!GdiScreenCapture.TryCapture(capX, capY, capW, capH, out bgra))
                    return "FAIL: 截屏失败";

                return WritePng(bgra, capW, capH, path,
                    string.Format("OK: 标题栏 {0}px, 截取 {1}x{2} @ {3},{4} → {5}",
                        captionHeight, capW, capH, capX, capY, path));
            }
            finally
            {
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_NOTOPMOST,
                    original.Left, original.Top, w, h, 0);
            }
        }

        public static string DescribeAllWindows()
        {
            var sb = new StringBuilder();
            uint pid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            var cls = new StringBuilder(256);
            var txt = new StringBuilder(256);

            NativeMethods.EnumWindows((h, _) =>
            {
                NativeMethods.GetWindowThreadProcessId(h, out uint owner);
                if (owner != pid) return true;
                if (!NativeMethods.IsWindowVisible(h)) return true;

                cls.Length = 0;
                txt.Length = 0;
                NativeMethods.GetClassName(h, cls, 256);
                NativeMethods.GetWindowText(h, txt, 256);
                NativeMethods.GetWindowRect(h, out var r);

                sb.AppendLine(string.Format("{0} | {1} | {2},{3} {4}x{5}",
                    cls, txt, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top));
                return true;
            }, IntPtr.Zero);

            return sb.ToString();
        }

        public static string DescribeWindows()
        {
            var sb = new StringBuilder();
            var hwnd = UnityMainWindow.Handle;
            sb.Append("Unity HWND=" + (hwnd == IntPtr.Zero ? "null" : "0x" + hwnd.ToInt64().ToString("X")));

            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.GetWindowRect(hwnd, out var r);
                sb.Append(" rect=" + r.Left + "," + r.Top + " " + (r.Right - r.Left) + "x" + (r.Bottom - r.Top));
                sb.Append(" visible=" + NativeMethods.IsWindowVisible(hwnd));
                sb.Append(" iconic=" + NativeMethods.IsIconic(hwnd));
            }

            var fg = GetForegroundWindowSafe();
            var name = new StringBuilder(256);
            NativeMethods.GetClassName(fg, name, 256);
            sb.Append(" | foreground=" + name);
            return sb.ToString();
        }

        static IntPtr GetForegroundWindowSafe()
        {
            return NativeMethods.GetForegroundWindow();
        }

        public static string ShowcaseCapture(string path)
        {
            return ShowcaseCaptureSized(path, 1400, 880);
        }

        public static string ShowcaseCaptureSized(string path, int width, int height)
        {
            var hwnd = UnityMainWindow.Handle;
            if (hwnd == IntPtr.Zero) return "FAIL: 找不到 Unity 主窗口";

            NativeMethods.GetWindowRect(hwnd, out var original);

            try
            {

                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 30, 30, width, height,
                    NativeMethods.SWP_SHOWWINDOW);
                System.Threading.Thread.Sleep(1400);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                System.Threading.Thread.Sleep(900);

                DesktopBackdrop.RefreshNow();
                System.Threading.Thread.Sleep(350);

                return CaptureLive(path);
            }
            finally
            {
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_NOTOPMOST,
                    original.Left, original.Top,
                    original.Right - original.Left, original.Bottom - original.Top,
                    0);
            }
        }

        public static string CaptureLive(string path)
        {
            var hwnd = UnityMainWindow.Handle;
            if (hwnd == IntPtr.Zero) return "FAIL: 找不到 Unity 主窗口";

            if (!UnityMainWindow.TryGetClientRectOnScreen(out int x, out int y, out int w, out int h))
                return "FAIL: 拿不到客户区";

            var settings = LiquidGlassSettings.instance;
            bool wasExcluded = settings.captureExclude;

            try
            {
                DesktopBackdrop.Stop();
                NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_NONE);
                NativeMethods.RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero,
                    NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_ALLCHILDREN | NativeMethods.RDW_UPDATENOW);
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                System.Threading.Thread.Sleep(700);

                byte[] bgra;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                if (!GdiScreenCapture.TryCapture(x, y, w, h, out bgra))
                    return "FAIL: 截屏失败";
                watch.Stop();

                return WritePng(bgra, w, h, path,
                    string.Format("OK: {0}x{1}, 截屏 {2:0.0}ms → {3}", w, h, watch.Elapsed.TotalMilliseconds, path));
            }
            finally
            {
                NativeMethods.SetWindowDisplayAffinity(hwnd,
                    wasExcluded ? NativeMethods.WDA_EXCLUDEFROMCAPTURE : NativeMethods.WDA_NONE);
                DesktopBackdrop.Start();
            }
        }

        static string WritePng(byte[] bgra, int width, int height, string path, string message)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int srcRow = y * width * 4;
                int dstRow = (height - 1 - y) * width;
                for (int x = 0; x < width; x++)
                {
                    int i = srcRow + x * 4;
                    pixels[dstRow + x] = new Color32(bgra[i + 2], bgra[i + 1], bgra[i], 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, texture.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(texture);
            return message;
        }

        public static string Capture(string path)
        {
            var hwnd = UnityMainWindow.Handle;
            if (hwnd == IntPtr.Zero) return "FAIL: 找不到 Unity 主窗口";

            byte[] bgra;
            int width, height;

            var watch = System.Diagnostics.Stopwatch.StartNew();
            if (!GdiScreenCapture.TryCaptureWindow(hwnd, out bgra, out width, out height))
            {
                return "FAIL: PrintWindow 失败（窗口最小化？）";
            }
            watch.Stop();

            return WritePng(bgra, width, height, path,
                string.Format("OK: {0}x{1}, PrintWindow {2:0.0}ms → {3}",
                    width, height, watch.Elapsed.TotalMilliseconds, path));
        }

        public static string RunSelfTest()
        {
            var sb = new StringBuilder();
            var settings = LiquidGlassSettings.instance;

            sb.AppendLine("=== Liquid Glass 自检 ===");
            sb.AppendLine("OS:        " + Environment.OSVersion.VersionString);
            sb.AppendLine("Unity:     " + Application.unityVersion);

            var hwnd = UnityMainWindow.Handle;
            sb.AppendLine("HWND:      " + (hwnd == IntPtr.Zero ? "未找到" : "0x" + hwnd.ToInt64().ToString("X")));

            int x, y, w, h;
            sb.AppendLine("客户区:    " + (UnityMainWindow.TryGetClientRectOnScreen(out x, out y, out w, out h)
                ? w + "x" + h + " @" + x + "," + y
                : "未知"));

            bool wda = NativeMethods.SetWindowDisplayAffinity(hwnd,
                settings.captureExclude ? NativeMethods.WDA_EXCLUDEFROMCAPTURE : NativeMethods.WDA_NONE);
            sb.AppendLine("捕获隐身:  " + (wda ? "支持（抓到的是桌面）" : "不支持（会抓到自身）"));

            var texture = DesktopBackdrop.Texture;
            sb.AppendLine("背景贴图:  " + (texture == null ? "无" : texture.width + "x" + texture.height));

            sb.AppendLine("注入窗口:  " + GlassPanelInjector.InjectedWindows);
            sb.AppendLine("半透明元素:" + FullGlassPatcher.PatchedCount);
            sb.AppendLine("主题 USS:  " + (GlassPanelInjector.ThemeSheetFound ? "已加载" : "未找到"));
            sb.AppendLine("面板 alpha:" + settings.panelAlpha);

            return sb.ToString();
        }
    }
}
