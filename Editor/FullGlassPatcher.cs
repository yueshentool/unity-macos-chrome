// Copyright (C) 2026 Liquid Glass contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiquidGlass.Editor
{

    internal static class FullGlassPatcher
    {
        static readonly Dictionary<VisualElement, StyleColor> s_Original =
            new Dictionary<VisualElement, StyleColor>();

        static readonly Dictionary<VisualElement, Color> s_Applied =
            new Dictionary<VisualElement, Color>();

        const int k_MaxNodesPerWindow = 8000;
        const int k_PruneThreshold = 20000;

        const double k_Interval = 2.0;

        static double s_NextPass;
        static float s_LastAlpha = -1f;

        public static bool IsActive => LiquidGlassSettings.instance.fullGlass;

        public static int PatchedCount
        {
            get { return s_Original.Count; }
        }

        public static void Tick()
        {
            var settings = LiquidGlassSettings.instance;

            if (!settings.fullGlass)
            {
                if (s_LastAlpha >= 0f) Restore();
                return;
            }

            if (EditorApplication.timeSinceStartup < s_NextPass) return;
            s_NextPass = EditorApplication.timeSinceStartup + k_Interval;

            if (s_Original.Count > k_PruneThreshold) Prune();

            float alpha = Mathf.Clamp(settings.panelAlpha, 0.05f, 1f);

            if (!Mathf.Approximately(alpha, s_LastAlpha))
            {
                Restore();
                s_LastAlpha = alpha;
            }

            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();

            if (!s_Dumped)
            {
                s_Dumped = true;
                DumpToolbarTree(windows);
            }

            foreach (var window in windows)
            {
                if (window == null) continue;
                var root = window.rootVisualElement;
                if (root == null || root.panel == null) continue;

                try
                {

                    bool isToolbar = IsMainToolbarWindow(window);
                    Patch(root, alpha, isToolbar);
                    if (isToolbar) ApplyToolbarChrome(root);
                }
                catch
                {

                }
            }
        }

        static void Patch(VisualElement root, float alpha, bool noGlass = false)
        {
            var stack = new Stack<VisualElement>();
            var skip = new Stack<bool>();
            stack.Push(root);
            skip.Push(noGlass);
            int guard = 0;

            while (stack.Count > 0 && guard++ < k_MaxNodesPerWindow)
            {
                var element = stack.Pop();
                bool glassOff = skip.Pop();
                if (element == null) continue;

                if (!glassOff && IsToolbar(element)) glassOff = true;

                if (glassOff)
                {
                    RestoreOne(element);
                }
                else
                {
                    Color bg = element.resolvedStyle.backgroundColor;
                    if (bg.a > 0.92f)
                    {
                        if (!s_Original.ContainsKey(element))
                        {
                            s_Original[element] = element.style.backgroundColor;
                        }

                        float a = alpha;
                        if (IsControl(element)) a = Mathf.Min(1f, alpha + 0.28f);
                        if (IsAccent(bg)) a = Mathf.Max(a, 0.62f);

                        var tinted = bg;
                        tinted.a = a;

                        if (!s_Applied.TryGetValue(element, out Color applied) || applied != tinted)
                        {
                            element.style.backgroundColor = tinted;
                            s_Applied[element] = tinted;
                        }
                    }
                }

                var hierarchy = element.hierarchy;
                for (int i = 0; i < hierarchy.childCount; i++)
                {
                    stack.Push(hierarchy[i]);
                    skip.Push(glassOff);
                }
            }
        }

        static void RestoreOne(VisualElement element)
        {
            if (!s_Applied.ContainsKey(element) && !s_Original.ContainsKey(element)) return;

            if (s_Original.TryGetValue(element, out StyleColor original))
            {
                element.style.backgroundColor = original;
            }

            s_Original.Remove(element);
            s_Applied.Remove(element);
        }

        static readonly Color kToolbarChrome = new Color32(0x37, 0x37, 0x37, 0xFF);

        static VisualElement s_ToolbarRoot;
        static Color s_ToolbarApplied;

        static void ApplyToolbarChrome(VisualElement root)
        {
            if (s_ToolbarRoot == root && s_ToolbarApplied == kToolbarChrome) return;
            root.style.backgroundColor = kToolbarChrome;
            s_ToolbarRoot = root;
            s_ToolbarApplied = kToolbarChrome;
        }

        static bool IsMainToolbarWindow(EditorWindow window)
        {
            for (var t = window.GetType(); t != null; t = t.BaseType)
            {
                if (t.Name == "MainToolbarWindow") return true;
            }
            return false;
        }

        static bool s_Dumped;

        static void DumpToolbarTree(EditorWindow[] windows)
        {
            int found = 0;
            foreach (var w in windows)
            {
                if (w == null || !IsMainToolbarWindow(w)) continue;
                found++;

                var root = w.rootVisualElement;
                MacTitleBar.Log("[Glass] toolbar window '" + w.name + "' root=" + (root != null));
                if (root == null) continue;

                var stack = new Stack<(VisualElement el, int depth)>();
                stack.Push((root, 0));
                int guard = 0;
                while (stack.Count > 0 && guard++ < 300)
                {
                    var (el, depth) = stack.Pop();
                    if (el == null) continue;

                    var classes = string.Join(".", el.GetClasses());
                    MacTitleBar.Log("[Glass] " + new string(' ', depth * 2) +
                                    el.name + " | " + classes +
                                    " | bg=" + el.resolvedStyle.backgroundColor);

                    if (depth >= 5) continue;
                    var h = el.hierarchy;
                    for (int i = 0; i < h.childCount; i++) stack.Push((h[i], depth + 1));
                }
            }

            MacTitleBar.Log("[Glass] toolbar windows = " + found);
        }

        static bool IsToolbar(VisualElement element)
        {
            if (element.ClassListContains("unity-editor-toolbar")) return true;
            if (element.ClassListContains("unity-editor-toolbar-container")) return true;

            var name = element.name;
            if (!string.IsNullOrEmpty(name) &&
                name.IndexOf("toolbar", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        static bool IsControl(VisualElement element)
        {
            return element.ClassListContains("unity-text-input")
                   || element.ClassListContains("unity-button")
                   || element.ClassListContains("unity-toggle")
                   || element.ClassListContains("unity-radio-button")
                   || element.ClassListContains("unity-toolbar-button")
                   || element.ClassListContains("unity-tab")
                   || element.ClassListContains("unity-base-field__input")
                   || element.ClassListContains("unity-object-field__input");
        }

        static bool IsAccent(Color bg)
        {
            float max = Mathf.Max(bg.r, Mathf.Max(bg.g, bg.b));
            float min = Mathf.Min(bg.r, Mathf.Min(bg.g, bg.b));
            return (max - min) > 0.22f;
        }

        static void Prune()
        {
            var alive = new List<VisualElement>(s_Original.Count);
            foreach (var kv in s_Original)
            {
                if (kv.Key != null && kv.Key.panel != null) alive.Add(kv.Key);
            }

            var snapshot = new Dictionary<VisualElement, StyleColor>(alive.Count);
            foreach (var key in alive)
            {
                snapshot[key] = s_Original[key];
            }

            s_Original.Clear();
            foreach (var kv in snapshot) s_Original[kv.Key] = kv.Value;

            List<VisualElement> deadApplied = null;
            foreach (var kv in s_Applied)
            {
                if (kv.Key == null || kv.Key.panel == null)
                    (deadApplied ??= new List<VisualElement>()).Add(kv.Key);
            }
            if (deadApplied != null)
                foreach (var k in deadApplied) s_Applied.Remove(k);
        }

        public static void Restore()
        {
            foreach (var kv in s_Original)
            {
                if (kv.Key != null) kv.Key.style.backgroundColor = kv.Value;
            }
            s_Original.Clear();
            s_Applied.Clear();
            s_LastAlpha = -1f;
        }
    }
}
