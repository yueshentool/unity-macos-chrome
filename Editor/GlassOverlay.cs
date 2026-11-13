// Copyright (C) 2026 Liquid Glass contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiquidGlass.Editor
{

    internal static class GlassOverlay
    {
        const string k_Grain = "liquidglass-grain";
        const string k_Sheen = "liquidglass-sheen";

        static Texture2D s_GrainTexture;
        static Texture2D s_SheenTexture;

        public static void Sync(VisualElement root, LiquidGlassSettings settings)
        {
            SyncLayer(root, k_Grain, settings.grain, GrainTexture, element =>
            {
                element.style.backgroundRepeat =
                    new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat));
                element.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(
                    new Length(140f, LengthUnit.Pixel),
                    new Length(140f, LengthUnit.Pixel)));
            });

            SyncLayer(root, k_Sheen, settings.sheen, SheenTexture, element =>
            {
                element.style.backgroundSize =
                    new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Cover));
            });
        }

        public static void RemoveAll(VisualElement root)
        {
            RemoveLayer(root, k_Grain);
            RemoveLayer(root, k_Sheen);
        }

        static void RemoveLayer(VisualElement root, string name)
        {
            var layer = root.Q<VisualElement>(name);
            if (layer != null) layer.RemoveFromHierarchy();
        }

        static void SyncLayer(VisualElement root, string name, float opacity,
            Texture2D texture, Action<VisualElement> configure)
        {
            var layer = root.Q<VisualElement>(name);

            if (opacity <= 0.002f)
            {
                if (layer != null) layer.RemoveFromHierarchy();
                return;
            }

            if (layer == null)
            {
                layer = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
                layer.style.position = Position.Absolute;
                layer.style.left = 0;
                layer.style.top = 0;
                layer.style.right = 0;
                layer.style.bottom = 0;
                configure(layer);
                root.Add(layer);
            }

            if (texture != null)
            {
                layer.style.backgroundImage = new StyleBackground(texture);
            }
            layer.style.opacity = opacity;
        }

        static Texture2D GrainTexture
        {
            get
            {
                if (s_GrainTexture != null) return s_GrainTexture;

                const int size = 128;
                var pixels = new Color32[size * size];
                var random = new System.Random(20261017);

                for (int i = 0; i < pixels.Length; i++)
                {
                    byte v = (byte)random.Next(0, 256);
                    pixels[i] = new Color32(v, v, v, 255);
                }

                s_GrainTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
                {
                    name = "LiquidGlassGrain",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat
                };
                s_GrainTexture.SetPixels32(pixels);
                s_GrainTexture.Apply(false, false);

                return s_GrainTexture;
            }
        }

        static Texture2D SheenTexture
        {
            get
            {
                if (s_SheenTexture != null) return s_SheenTexture;

                const int size = 256;
                var pixels = new Color32[size * size];

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {

                        float t = (x + (size - 1 - y)) / (float)(2 * size - 2);
                        float a = Mathf.Clamp01(1f - t * 1.55f);
                        a *= a;
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                }

                s_SheenTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
                {
                    name = "LiquidGlassSheen",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                s_SheenTexture.SetPixels32(pixels);
                s_SheenTexture.Apply(false, false);

                return s_SheenTexture;
            }
        }

        public static void Release()
        {
            if (s_GrainTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(s_GrainTexture);
                s_GrainTexture = null;
            }
            if (s_SheenTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(s_SheenTexture);
                s_SheenTexture = null;
            }
        }
    }
}
