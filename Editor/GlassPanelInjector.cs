// Copyright (C) 2026 Liquid Glass contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiquidGlass.Editor
{

    internal static class GlassPanelInjector
    {

        const float k_ScanInterval = 3f;

        static readonly Dictionary<VisualElement, StyleColor> s_OriginalBg =
            new Dictionary<VisualElement, StyleColor>();

        static readonly Dictionary<VisualElement, StyleBackground> s_OriginalBgImage =
            new Dictionary<VisualElement, StyleBackground>();

        static StyleSheet s_Sheet;
        static bool s_SheetWarningShown;
        static double s_NextScan;
        static int s_InjectedWindows;

        public static int InjectedWindows
        {
            get { return s_InjectedWindows; }
        }

        public static bool ThemeSheetFound
        {
            get { return s_Sheet != null; }
        }

        public static void Tick()
        {
            if (EditorApplication.timeSinceStartup < s_NextScan) return;
            s_NextScan = EditorApplication.timeSinceStartup + k_ScanInterval;
            Refresh();
        }

        public static void Refresh()
        {

            if (s_Sheet == null)
            {
                s_Sheet = FindThemeSheet();
            }

            var settings = LiquidGlassSettings.instance;
            var texture = DesktopBackdrop.Texture;
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            int injected = 0;

            foreach (var window in windows)
            {
                if (window == null) continue;
                if (window.position.width < 120f || window.position.height < 80f) continue;

                try
                {
                    if (Inject(window, settings, texture)) injected++;
                }
                catch
                {

                }
            }

            s_InjectedWindows = injected;
        }

        static bool Inject(EditorWindow window, LiquidGlassSettings settings, Texture2D texture)
        {
            var root = window.rootVisualElement;
            if (root == null) return false;

            var panel = root.panel;
            if (panel == null) return false;

            if (s_Sheet != null && !panel.visualTree.styleSheets.Contains(s_Sheet))
            {
                panel.visualTree.styleSheets.Add(s_Sheet);
            }

            var panelRoot = panel.visualTree;

            if (!s_OriginalBgImage.ContainsKey(panelRoot))
            {
                s_OriginalBgImage[panelRoot] = panelRoot.style.backgroundImage;
                s_OriginalBg[panelRoot] = panelRoot.style.backgroundColor;
            }

            if (texture != null)
            {
                panelRoot.style.backgroundImage = new StyleBackground(texture);
                panelRoot.style.backgroundSize =
                    new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Cover));
                panelRoot.style.backgroundRepeat =
                    new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat));
            }

            if (!s_OriginalBg.ContainsKey(root))
            {
                s_OriginalBg[root] = root.style.backgroundColor;
            }
            root.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));

            ClearDockBackgrounds(panel.visualTree);

            GlassOverlay.Sync(root, settings);

            return texture != null;
        }

        static void ClearDockBackgrounds(VisualElement panelRoot)
        {
            foreach (var element in panelRoot.Query<VisualElement>().ToList())
            {
                string name = element.name;
                if (string.IsNullOrEmpty(name)) continue;

                bool isDock = name.IndexOf("dock", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isDock) continue;

                if (s_OriginalBg.ContainsKey(element)) continue;

                s_OriginalBg[element] = element.style.backgroundColor;
                element.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            }
        }

        static StyleSheet FindThemeSheet()
        {
            var guids = AssetDatabase.FindAssets("GlassTheme t:StyleSheet");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet != null) return sheet;
            }

            if (!s_SheetWarningShown)
            {
                s_SheetWarningShown = true;
                Debug.LogWarning("[Liquid Glass] 没找到 GlassTheme.uss，面板内部仍是实心底色。");
            }
            return null;
        }

        public static void Remove()
        {
            foreach (var kv in s_OriginalBg)
            {
                if (kv.Key != null) kv.Key.style.backgroundColor = kv.Value;
            }
            s_OriginalBg.Clear();

            foreach (var kv in s_OriginalBgImage)
            {
                if (kv.Key != null) kv.Key.style.backgroundImage = kv.Value;
            }
            s_OriginalBgImage.Clear();

            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var window in windows)
            {
                if (window == null) continue;
                var root = window.rootVisualElement;
                if (root == null) continue;

                GlassOverlay.RemoveAll(root);

                var panel = root.panel;
                if (panel != null && s_Sheet != null && panel.visualTree.styleSheets.Contains(s_Sheet))
                {
                    panel.visualTree.styleSheets.Remove(s_Sheet);
                }
            }

            s_InjectedWindows = 0;
        }
    }
}
