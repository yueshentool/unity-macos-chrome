// Copyright (C) 2026 Liquid Glass contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace LiquidGlass.Editor
{

    [InitializeOnLoad]
    internal static class LiquidGlassBootstrap
    {
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        const int VK_LBUTTON = 0x01;

        static LiquidGlassBootstrap()
        {

            GlassStabilityGuard.Reset("domain reload");

            EditorApplication.update += OnUpdate;
            EditorApplication.delayCall += AutoRestore;
        }

        static void AutoRestore()
        {
            if (!LiquidGlassSettings.instance.enabled) return;

            if (!GlassStabilityGuard.IsRisky)
            {
                Enable();
                return;
            }

            EditorApplication.update -= AutoRestoreWhenReady;
            EditorApplication.update += AutoRestoreWhenReady;
        }

        static void AutoRestoreWhenReady()
        {
            if (!LiquidGlassSettings.instance.enabled)
            {
                EditorApplication.update -= AutoRestoreWhenReady;
                return;
            }
            if (GlassStabilityGuard.IsRisky) return;

            EditorApplication.update -= AutoRestoreWhenReady;
            Enable();
        }

        static bool UserIsDragging()
        {
            return (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
        }

        static void OnUpdate()
        {
            PerfProbe.Frame();

            if (!LiquidGlassSettings.instance.enabled) return;

            if (GlassStabilityGuard.IsRisky) return;

            if (!DesktopBackdrop.IsRunning) DesktopBackdrop.Start();

            if (UserIsDragging()) return;

            PerfProbe.Begin(); DesktopBackdrop.Tick(); PerfProbe.End("backdrop");
            PerfProbe.Begin(); GlassPanelInjector.Tick(); PerfProbe.End("panel");
            PerfProbe.Begin(); FullGlassPatcher.Tick(); PerfProbe.End("fullglass");
        }

        [MenuItem("Tools/Liquid Glass/Enable", false, 1)]
        public static void Enable()
        {
            var settings = LiquidGlassSettings.instance;
            settings.enabled = true;
            settings.Save();

            GlassWindowChrome.Apply();
            DesktopBackdrop.Start();
            DesktopBackdrop.RefreshNow();
            GlassPanelInjector.Refresh();
            FullGlassPatcher.Tick();

            Debug.Log("[Liquid Glass] 已开启。");
        }

        [MenuItem("Tools/Liquid Glass/Enable", true)]
        static bool EnableValidate() => !LiquidGlassSettings.instance.enabled;

        [MenuItem("Tools/Liquid Glass/Disable", false, 2)]
        public static void Disable()
        {
            var settings = LiquidGlassSettings.instance;
            settings.enabled = false;
            settings.Save();

            DesktopBackdrop.Release();
            FullGlassPatcher.Restore();
            GlassPanelInjector.Remove();
            GlassOverlay.Release();
            GlassWindowChrome.Restore();

            Debug.Log("[Liquid Glass] 已关闭。");
        }

        [MenuItem("Tools/Liquid Glass/Disable", true)]
        static bool DisableValidate() => LiquidGlassSettings.instance.enabled;

        [MenuItem("Tools/Liquid Glass/Refresh Backdrop %#l", false, 20)]
        static void RefreshBackdrop()
        {
            DesktopBackdrop.RefreshNow();
            GlassPanelInjector.Refresh();
        }
    }
}
