// Copyright (C) 2026 Liquid Glass contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;
using UnityEditor;
using UnityEngine;

namespace LiquidGlass.Editor
{

    internal static class DesktopBackdrop
    {
        static Texture2D s_Texture;
        static byte[] s_Raw;
        static byte[] s_Small;
        static byte[] s_Temp;
        static Color32[] s_Pixels;
        static int s_SmallW;
        static int s_SmallH;
        static double s_NextTime;
        static bool s_Started;

        static double s_LastCost;

        static double s_Penalty;

        static bool s_AffinityCleared;

        public static Texture2D Texture => s_Texture;

        public static bool IsRunning => s_Started;

        public static void Start()
        {
            s_Started = true;
            s_NextTime = 0;
        }

        public static void Stop()
        {
            s_Started = false;
        }

        public static void Release()
        {
            Stop();
            if (s_Texture != null)
            {
                UnityEngine.Object.DestroyImmediate(s_Texture);
                s_Texture = null;
            }
            s_Raw = null;
            s_Small = null;
            s_Temp = null;
            s_Pixels = null;
            s_Penalty = 0;
            s_AffinityCleared = false;
        }

        public static void Tick()
        {
            if (!s_Started) return;

            if (GlassStabilityGuard.IsRisky) return;

            var settings = LiquidGlassSettings.instance;
            double wanted = 1.0 / Mathf.Clamp(settings.fps, 1, 60);

            const double k_Budget = 0.03;
            double interval = Math.Max(wanted, s_LastCost / k_Budget);

            if (s_Penalty > 0) interval = Math.Max(interval, s_Penalty);

            if (EditorApplication.timeSinceStartup < s_NextTime) return;
            s_NextTime = EditorApplication.timeSinceStartup + interval;

            double t0 = EditorApplication.timeSinceStartup;
            bool failed = false;
            try
            {
                CaptureOnce(settings);
            }
            catch (Exception e)
            {
                failed = true;
                s_Penalty = Math.Min(10.0, Math.Max(2.0, s_Penalty * 2.0));
                MacTitleBar.Log("Backdrop 抓屏异常：" + e.GetType().Name + " " + e.Message +
                                "（退避 " + s_Penalty.ToString("0.0") + "s）");
            }
            s_LastCost = EditorApplication.timeSinceStartup - t0;

            if (failed) return;

            if (s_LastCost > 0.2)
            {
                s_Penalty = Math.Min(10.0, Math.Max(2.0, s_LastCost * 10.0));
                MacTitleBar.Log("Backdrop 单次耗时 " + (s_LastCost * 1000).ToString("0") +
                                "ms，退避 " + s_Penalty.ToString("0.0") + "s");
            }
            else if (s_LastCost < 0.05 && s_Penalty > 0)
            {
                s_Penalty = 0;
            }
        }

        public static void RefreshNow()
        {
            s_NextTime = 0;
            CaptureOnce(LiquidGlassSettings.instance);
        }

        static void CaptureOnce(LiquidGlassSettings settings)
        {
            if (!UnityMainWindow.IsVisible) return;
            if (!UnityMainWindow.TryGetClientRectOnScreen(out int x, out int y, out int w, out int h))
                return;

            if (!s_AffinityCleared && UnityMainWindow.Handle != IntPtr.Zero)
            {
                NativeMethods.SetWindowDisplayAffinity(UnityMainWindow.Handle, NativeMethods.WDA_NONE);
                s_AffinityCleared = true;
            }

            if (!GdiScreenCapture.TryCapture(x, y, w, h, out s_Raw)) return;

            int scale = Mathf.Clamp(settings.downscale, 1, 16);
            int sw = Mathf.Max(1, w / scale);
            int sh = Mathf.Max(1, h / scale);

            int bytes = sw * sh * 4;
            if (s_Small == null || s_Small.Length != bytes)
            {
                s_Small = new byte[bytes];
                s_Temp = new byte[bytes];
                s_Pixels = new Color32[sw * sh];
                s_SmallW = sw;
                s_SmallH = sh;
            }
            else if (s_SmallW != sw || s_SmallH != sh)
            {
                s_Small = new byte[bytes];
                s_Temp = new byte[bytes];
                s_Pixels = new Color32[sw * sh];
                s_SmallW = sw;
                s_SmallH = sh;
            }

            BoxDownsample(s_Raw, w, h, s_Small, sw, sh, scale);

            int radius = Mathf.Clamp(settings.blurRadius, 0, 64);
            if (radius > 0)
                BoxBlur(s_Small, s_Temp, sw, sh, radius, 3);

            ApplyGrade(s_Small, settings.tint, settings.tintStrength,
                settings.brightness, settings.saturation);

            Upload(s_Small, sw, sh);
        }

        static void BoxDownsample(byte[] src, int sw, int sh, byte[] dst, int dw, int dh, int scale)
        {
            for (int y = 0; y < dh; y++)
            {
                int sy0 = y * scale;
                int sy1 = Mathf.Min(sh, sy0 + scale);
                int dstRow = y * dw * 4;

                for (int x = 0; x < dw; x++)
                {
                    int sx0 = x * scale;
                    int sx1 = Mathf.Min(sw, sx0 + scale);

                    int r = 0, g = 0, b = 0, n = 0;
                    for (int sy = sy0; sy < sy1; sy++)
                    {
                        int row = sy * sw * 4;
                        for (int sx = sx0; sx < sx1; sx++)
                        {
                            int i = row + sx * 4;
                            b += src[i];
                            g += src[i + 1];
                            r += src[i + 2];
                            n++;
                        }
                    }

                    if (n == 0) n = 1;
                    int o = dstRow + x * 4;
                    dst[o] = (byte)(b / n);
                    dst[o + 1] = (byte)(g / n);
                    dst[o + 2] = (byte)(r / n);
                    dst[o + 3] = 255;
                }
            }
        }

        static void BoxBlur(byte[] src, byte[] tmp, int w, int h, int radius, int passes)
        {
            for (int p = 0; p < passes; p++)
            {
                BlurHorizontal(src, tmp, w, h, radius);
                BlurVertical(tmp, src, w, h, radius);
            }
        }

        static void BlurHorizontal(byte[] src, byte[] dst, int w, int h, int radius)
        {
            int window = radius * 2 + 1;

            for (int y = 0; y < h; y++)
            {
                int row = y * w * 4;
                int r = 0, g = 0, b = 0;

                for (int k = -radius; k <= radius; k++)
                {
                    int i = row + Mathf.Clamp(k, 0, w - 1) * 4;
                    b += src[i];
                    g += src[i + 1];
                    r += src[i + 2];
                }

                for (int x = 0; x < w; x++)
                {
                    int o = row + x * 4;
                    dst[o] = (byte)(b / window);
                    dst[o + 1] = (byte)(g / window);
                    dst[o + 2] = (byte)(r / window);
                    dst[o + 3] = 255;

                    int add = row + Mathf.Clamp(x + radius + 1, 0, w - 1) * 4;
                    int sub = row + Mathf.Clamp(x - radius, 0, w - 1) * 4;
                    b += src[add] - src[sub];
                    g += src[add + 1] - src[sub + 1];
                    r += src[add + 2] - src[sub + 2];
                }
            }
        }

        static void BlurVertical(byte[] src, byte[] dst, int w, int h, int radius)
        {
            int window = radius * 2 + 1;
            int stride = w * 4;

            for (int x = 0; x < w; x++)
            {
                int col = x * 4;
                int r = 0, g = 0, b = 0;

                for (int k = -radius; k <= radius; k++)
                {
                    int i = col + Mathf.Clamp(k, 0, h - 1) * stride;
                    b += src[i];
                    g += src[i + 1];
                    r += src[i + 2];
                }

                for (int y = 0; y < h; y++)
                {
                    int o = col + y * stride;
                    dst[o] = (byte)(b / window);
                    dst[o + 1] = (byte)(g / window);
                    dst[o + 2] = (byte)(r / window);
                    dst[o + 3] = 255;

                    int add = col + Mathf.Clamp(y + radius + 1, 0, h - 1) * stride;
                    int sub = col + Mathf.Clamp(y - radius, 0, h - 1) * stride;
                    b += src[add] - src[sub];
                    g += src[add + 1] - src[sub + 1];
                    r += src[add + 2] - src[sub + 2];
                }
            }
        }

        static void ApplyGrade(byte[] px, Color tint, float strength, float brightness, float saturation)
        {
            float tr = tint.r * 255f;
            float tg = tint.g * 255f;
            float tb = tint.b * 255f;
            strength = Mathf.Clamp01(strength);
            brightness = Mathf.Clamp(brightness, 0.1f, 2f);
            saturation = Mathf.Clamp(saturation, 0.1f, 3f);

            for (int i = 0; i < px.Length; i += 4)
            {
                float b = px[i] * brightness;
                float g = px[i + 1] * brightness;
                float r = px[i + 2] * brightness;

                float luma = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                r = luma + (r - luma) * saturation;
                g = luma + (g - luma) * saturation;
                b = luma + (b - luma) * saturation;

                r = Mathf.Lerp(r, tr, strength);
                g = Mathf.Lerp(g, tg, strength);
                b = Mathf.Lerp(b, tb, strength);

                px[i] = (byte)Mathf.Clamp(b, 0f, 255f);
                px[i + 1] = (byte)Mathf.Clamp(g, 0f, 255f);
                px[i + 2] = (byte)Mathf.Clamp(r, 0f, 255f);
            }
        }

        static void Upload(byte[] bgra, int w, int h)
        {
            if (s_Texture == null || s_Texture.width != w || s_Texture.height != h)
            {
                if (s_Texture != null) UnityEngine.Object.DestroyImmediate(s_Texture);
                s_Texture = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "LiquidGlassBackdrop"
                };
                s_Pixels = new Color32[w * h];
            }

            var pixels = s_Pixels;
            for (int y = 0; y < h; y++)
            {
                int srcRow = y * w * 4;
                int dstRow = (h - 1 - y) * w;
                for (int x = 0; x < w; x++)
                {
                    int i = srcRow + x * 4;
                    pixels[dstRow + x] = new Color32(bgra[i + 2], bgra[i + 1], bgra[i], 255);
                }
            }

            s_Texture.SetPixels32(pixels);
            s_Texture.Apply(false, false);
        }
    }
}
