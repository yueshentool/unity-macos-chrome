// Copyright (C) 2026 Liquid Glass contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;
using UnityEditor;
using UnityEngine;

namespace LiquidGlass.Editor
{

    internal enum MenuBarBgMode
    {
        Auto = 0,
        Transparent = 1,
        Light = 2,
        Dark = 3,
        Custom = 4,
    }

    internal enum PopupThemeMode
    {
        Auto = 0,
        Light = 1,
        Dark = 2,
        Custom = 3,
    }

    [Serializable]
    internal class LiquidGlassSettings
    {
        const string k_Prefix = "LiquidGlass.";

        static LiquidGlassSettings s_Instance;

        public bool enabled;

        public bool fullGlass = true;
        public float panelAlpha = 0.5f;
        public float grain = 0.05f;
        public float sheen = 0.09f;

        public int fps = 15;
        public int downscale = 4;
        public int blurRadius = 10;
        public Color tint = new Color(0.055f, 0.065f, 0.095f, 1f);
        public float tintStrength = 0.38f;
        public float brightness = 0.9f;
        public float saturation = 1.25f;

        public bool captureExclude = false;

        public bool roundedCorners = true;
        public bool acrylicBackdrop = true;

        public string menuBarWhitelist = DefaultWhitelist;

        public const string DefaultWhitelist =
            "Code.exe,cursor.exe,rider64.exe,devenv.exe,idea64.exe,webstorm64.exe,clion64.exe," +
            "pycharm64.exe,goland64.exe,phpstorm64.exe,sublime_text.exe,notepad++.exe," +
            "WindowsTerminal.exe,wt.exe,Code - Insiders.exe,vscodium.exe,zed.exe";

        public int menuBarBgMode = (int)MenuBarBgMode.Auto;
        public Color menuBarCustomColor = new Color(0.11f, 0.11f, 0.12f, 1f);

        public int popupThemeMode = (int)PopupThemeMode.Auto;
        public Color popupCustomColor = new Color(0.16f, 0.16f, 0.17f, 1f);

        public static LiquidGlassSettings instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = new LiquidGlassSettings();
                    s_Instance.Load();
                }
                return s_Instance;
            }
        }

        public static void ResetToDefaults()
        {
            bool wasEnabled = instance.enabled;
            s_Instance = new LiquidGlassSettings { enabled = wasEnabled };
            s_Instance.Save();
        }

        public void Load()
        {
            enabled = EditorPrefs.GetBool(k_Prefix + "enabled", enabled);
            fullGlass = EditorPrefs.GetBool(k_Prefix + "fullGlass", fullGlass);
            panelAlpha = EditorPrefs.GetFloat(k_Prefix + "panelAlpha", panelAlpha);
            grain = EditorPrefs.GetFloat(k_Prefix + "grain", grain);
            sheen = EditorPrefs.GetFloat(k_Prefix + "sheen", sheen);
            fps = EditorPrefs.GetInt(k_Prefix + "fps", fps);
            downscale = EditorPrefs.GetInt(k_Prefix + "downscale", downscale);
            blurRadius = EditorPrefs.GetInt(k_Prefix + "blurRadius", blurRadius);
            tint = LoadColor(k_Prefix + "tint", tint);
            tintStrength = EditorPrefs.GetFloat(k_Prefix + "tintStrength", tintStrength);
            brightness = EditorPrefs.GetFloat(k_Prefix + "brightness", brightness);
            saturation = EditorPrefs.GetFloat(k_Prefix + "saturation", saturation);
            captureExclude = EditorPrefs.GetBool(k_Prefix + "captureExclude", captureExclude);
            roundedCorners = EditorPrefs.GetBool(k_Prefix + "roundedCorners", roundedCorners);
            acrylicBackdrop = EditorPrefs.GetBool(k_Prefix + "acrylicBackdrop", acrylicBackdrop);
            menuBarWhitelist = EditorPrefs.GetString(k_Prefix + "menuBarWhitelist", menuBarWhitelist);
            menuBarBgMode = EditorPrefs.GetInt(k_Prefix + "menuBarBgMode", menuBarBgMode);
            menuBarCustomColor = LoadColor(k_Prefix + "menuBarCustomColor", menuBarCustomColor);
            popupThemeMode = EditorPrefs.GetInt(k_Prefix + "popupThemeMode", popupThemeMode);
            popupCustomColor = LoadColor(k_Prefix + "popupCustomColor", popupCustomColor);
        }

        public void Save()
        {
            EditorPrefs.SetBool(k_Prefix + "enabled", enabled);
            EditorPrefs.SetBool(k_Prefix + "fullGlass", fullGlass);
            EditorPrefs.SetFloat(k_Prefix + "panelAlpha", panelAlpha);
            EditorPrefs.SetFloat(k_Prefix + "grain", grain);
            EditorPrefs.SetFloat(k_Prefix + "sheen", sheen);
            EditorPrefs.SetInt(k_Prefix + "fps", fps);
            EditorPrefs.SetInt(k_Prefix + "downscale", downscale);
            EditorPrefs.SetInt(k_Prefix + "blurRadius", blurRadius);
            SaveColor(k_Prefix + "tint", tint);
            EditorPrefs.SetFloat(k_Prefix + "tintStrength", tintStrength);
            EditorPrefs.SetFloat(k_Prefix + "brightness", brightness);
            EditorPrefs.SetFloat(k_Prefix + "saturation", saturation);
            EditorPrefs.SetBool(k_Prefix + "captureExclude", captureExclude);
            EditorPrefs.SetBool(k_Prefix + "roundedCorners", roundedCorners);
            EditorPrefs.SetBool(k_Prefix + "acrylicBackdrop", acrylicBackdrop);
            EditorPrefs.SetString(k_Prefix + "menuBarWhitelist", menuBarWhitelist);
            EditorPrefs.SetInt(k_Prefix + "menuBarBgMode", menuBarBgMode);
            SaveColor(k_Prefix + "menuBarCustomColor", menuBarCustomColor);
            EditorPrefs.SetInt(k_Prefix + "popupThemeMode", popupThemeMode);
            SaveColor(k_Prefix + "popupCustomColor", popupCustomColor);
        }

        static Color LoadColor(string key, Color fallback)
        {
            string raw = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(raw)) return fallback;

            var parts = raw.Split(',');
            if (parts.Length < 3) return fallback;

            if (!float.TryParse(parts[0], out float r)) return fallback;
            if (!float.TryParse(parts[1], out float g)) return fallback;
            if (!float.TryParse(parts[2], out float b)) return fallback;
            return new Color(r, g, b, 1f);
        }

        static void SaveColor(string key, Color c)
        {
            EditorPrefs.SetString(key, $"{c.r:0.####},{c.g:0.####},{c.b:0.####}");
        }
    }

    internal class LiquidGlassSettingsWindow : EditorWindow
    {
        Vector2 m_Scroll;

        [MenuItem("Tools/Liquid Glass/Settings...", false, 30)]
        static void Open()
        {
            OpenWindow();
        }

        public static void OpenWindow()
        {
            var w = GetWindow<LiquidGlassSettingsWindow>(false, "Liquid Glass", true);
            w.minSize = new Vector2(420, 760);
            w.Show();
        }

        void OnGUI()
        {
            var s = LiquidGlassSettings.instance;
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Liquid Glass for Unity Editor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "把桌面模糊后铺到每个面板底层，并把编辑器主题色改成半透明，" +
                "视觉上等价于「透过玻璃看桌面」。", MessageType.Info);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("全玻璃", EditorStyles.boldLabel);
            s.fullGlass = EditorGUILayout.Toggle(
                new GUIContent("整棵树半透明", "运行时把每个面板里所有不透明底色换成半透明（与 Unity 版本无关）"),
                s.fullGlass);
            using (new EditorGUI.DisabledScope(!s.fullGlass))
            {
                s.panelAlpha = EditorGUILayout.Slider("面板不透明度", s.panelAlpha, 0.1f, 0.95f);
                s.grain = EditorGUILayout.Slider("磨砂颗粒", s.grain, 0f, 0.18f);
                s.sheen = EditorGUILayout.Slider("斜向高光", s.sheen, 0f, 0.25f);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("背景", EditorStyles.boldLabel);
            s.fps = EditorGUILayout.IntSlider("刷新率 (FPS)", s.fps, 1, 60);
            s.downscale = EditorGUILayout.IntSlider("降采样倍数", s.downscale, 1, 16);
            s.blurRadius = EditorGUILayout.IntSlider("模糊半径", s.blurRadius, 0, 40);
            s.tint = EditorGUILayout.ColorField("玻璃染色", s.tint);
            s.tintStrength = EditorGUILayout.Slider("染色强度", s.tintStrength, 0f, 1f);
            s.brightness = EditorGUILayout.Slider("亮度", s.brightness, 0.3f, 1.5f);
            s.saturation = EditorGUILayout.Slider("饱和度", s.saturation, 0.5f, 2f);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle(
                    new GUIContent("捕获时隐藏自身（已停用）",
                        "会导致 PrtSc / 截图工具都拍不到编辑器，请保持关闭"),
                    false);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("窗口外框", EditorStyles.boldLabel);
            s.roundedCorners = EditorGUILayout.Toggle("圆角 (Win11)", s.roundedCorners);
            s.acrylicBackdrop = EditorGUILayout.Toggle("系统亚克力背板 (Win11)", s.acrylicBackdrop);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("顶部菜单栏", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "前台是 Unity 时永远显示。\n" +
                "另外这里列出的进程在前台时也继续显示 —— 方便「Unity 在后台、前面开代码编辑器」\n" +
                "边写代码边点 Unity 菜单。点击桌面空白处也不会隐藏（编辑器没被盖住）。\n" +
                "菜单栏最右边有一个「Liquid Glass」菜单，可以直接把 exe 加进白名单。",
                MessageType.None);
            EditorGUI.BeginChangeCheck();
            s.menuBarWhitelist = EditorGUILayout.TextArea(s.menuBarWhitelist, GUILayout.MinHeight(46));
            if (EditorGUI.EndChangeCheck()) s.Save();
            if (GUILayout.Button("恢复默认白名单", GUILayout.Height(20)))
            {
                s.menuBarWhitelist = LiquidGlassSettings.DefaultWhitelist;
                s.Save();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("菜单栏外观", EditorStyles.boldLabel);
            s.menuBarBgMode = (int)(MenuBarBgMode)EditorGUILayout.EnumPopup(
                new GUIContent("菜单栏底色",
                    "自动 = 编辑器窗口化时全透明、最大化/全屏时跟随系统浅色/深色"),
                (MenuBarBgMode)s.menuBarBgMode);
            using (new EditorGUI.DisabledScope(s.menuBarBgMode != (int)MenuBarBgMode.Custom))
            {
                s.menuBarCustomColor = EditorGUILayout.ColorField("菜单栏自定义色", s.menuBarCustomColor);
            }

            s.popupThemeMode = (int)(PopupThemeMode)EditorGUILayout.EnumPopup(
                new GUIContent("二级菜单配色", "跟随系统颜色模式，也可以固定浅色/深色或自定义"),
                (PopupThemeMode)s.popupThemeMode);
            using (new EditorGUI.DisabledScope(s.popupThemeMode != (int)PopupThemeMode.Custom))
            {
                s.popupCustomColor = EditorGUILayout.ColorField("二级菜单自定义色", s.popupCustomColor);
            }

            EditorGUILayout.Space(12);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(s.enabled ? "关闭" : "开启", GUILayout.Height(26)))
                {
                    if (s.enabled) LiquidGlassBootstrap.Disable();
                    else LiquidGlassBootstrap.Enable();
                }
                if (GUILayout.Button("立即刷新背景", GUILayout.Height(26)))
                {
                    DesktopBackdrop.RefreshNow();
                    GlassPanelInjector.Refresh();
                }
            }

            if (GUILayout.Button("恢复默认值", GUILayout.Height(22)))
            {
                LiquidGlassSettings.ResetToDefaults();
                LiquidGlassBootstrap.Enable();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "面板半透明的深浅在 Editor/Styles/GlassTheme.uss 里调整。\n" +
                "不同 Unity 版本的主题变量名可能不同，可用 Window > UI Toolkit > Debugger 查看真实变量名。",
                MessageType.None);

            if (GUI.changed) s.Save();

            EditorGUILayout.EndScrollView();
        }
    }
}
