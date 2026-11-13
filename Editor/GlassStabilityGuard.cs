// Copyright (C) 2026 Liquid Glass contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Runtime.InteropServices;
using UnityEditor;

namespace LiquidGlass.Editor
{

    internal static class GlassStabilityGuard
    {
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        static extern uint GetTickCount();

        const double k_PostReloadQuiet = 0.8;

        const double k_ColdStartQuiet = 0.15;

        const string k_SessionKey = "LiquidGlass.DomainLoaded";

        const double k_ResumeQuiet = 5.0;
        const double k_PlayModeQuiet = 2.0;
        const double k_IdleSeconds = 60.0;
        const double k_PollInterval = 0.25;

        public static bool ColdStart { get; private set; }

        static double s_DomainAt;

        public static double SinceDomainLoad => s_DomainAt <= 0 ? 0 : Now - s_DomainAt;

        static double s_ReadyAt;
        static double s_ResumeUntil;
        static double s_PlayUntil;
        static double s_NextPoll;
        static bool s_SawIdle;
        static bool s_PlayingKnown;
        static bool s_LastPlaying;
        static bool s_Risky;
        static bool s_StructureLocked;
        static bool s_Inited;
        static string s_Reason = "ready";

        static double Now => EditorApplication.timeSinceStartup;

        public static double IdleSeconds { get; private set; }

        public static string Reason => s_Reason;

        public static void Reset(string why)
        {
            s_Inited = true;
            s_DomainAt = Now;

            ColdStart = !SessionState.GetBool(k_SessionKey, false);
            SessionState.SetBool(k_SessionKey, true);

            double quiet = ColdStart ? k_ColdStartQuiet : k_PostReloadQuiet;

            s_ReadyAt = Now + quiet;
            s_ResumeUntil = 0;
            s_SawIdle = false;
            s_NextPoll = 0;
            s_Risky = true;
            s_StructureLocked = true;
            s_Reason = why;
            MacTitleBar.Log("[Guard] 已加载，静默 " + quiet.ToString("0.00") + "s（" +
                            why + (ColdStart ? " / 冷启动" : " / 重载") + "）");
        }

        public static void Quiet(double seconds, string why)
        {
            s_Inited = true;
            double until = Now + Math.Max(0.1, seconds);
            if (until > s_ResumeUntil) s_ResumeUntil = until;
            MacTitleBar.Log("[Guard] 追加静默 " + seconds.ToString("0.0") + "s（" + why + "）");
        }

        public static bool IsRisky
        {
            get
            {
                Poll();
                return s_Risky;
            }
        }

        public static bool StructureLocked
        {
            get
            {
                Poll();
                return s_StructureLocked;
            }
        }

        static void Poll()
        {
            if (!s_Inited)
            {
                s_Inited = true;
                s_DomainAt = Now;
                s_ReadyAt = Now + k_PostReloadQuiet;
                s_Risky = true;
                s_StructureLocked = true;
                s_Reason = "init";
            }

            double now = Now;
            if (now < s_NextPoll) return;
            s_NextPoll = now + k_PollInterval;

            double idle = ReadIdleSeconds();
            IdleSeconds = idle;

            if (idle >= k_IdleSeconds)
            {
                s_SawIdle = true;
            }
            else if (s_SawIdle && idle < 1.0)
            {
                s_SawIdle = false;
                s_ResumeUntil = now + k_ResumeQuiet;
                MacTitleBar.Log("[Guard] 检测到从空闲返回，静默 " + k_ResumeQuiet.ToString("0.0") + "s");
            }

            bool playing = EditorApplication.isPlaying;
            if (!s_PlayingKnown)
            {
                s_PlayingKnown = true;
                s_LastPlaying = playing;
            }
            else if (playing != s_LastPlaying)
            {
                s_LastPlaying = playing;
                s_PlayUntil = now + k_PlayModeQuiet;
                MacTitleBar.Log("[Guard] 播放模式切换，静默 " + k_PlayModeQuiet.ToString("0.0") + "s");
            }

            string reason = null;
            bool structure = now < s_ReadyAt || EditorApplication.isCompiling || now < s_PlayUntil;
            if (now < s_ReadyAt) reason = "post-reload";
            else if (EditorApplication.isCompiling) reason = "compiling";
            else if (EditorApplication.isUpdating) reason = "importing";
            else if (now < s_PlayUntil) reason = "playmode";
            else if (now < s_ResumeUntil) reason = "resume";

            s_StructureLocked = structure;

            bool risky = reason != null;
            if (risky != s_Risky || (risky && !string.Equals(reason, s_Reason)))
            {
                s_Risky = risky;
                s_Reason = risky ? reason : "ready";
                MacTitleBar.Log(risky ? "[Guard] 暂停：" + reason : "[Guard] 恢复");
            }
        }

        static double ReadIdleSeconds()
        {
            try
            {
                var info = new LASTINPUTINFO();
                info.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
                if (!GetLastInputInfo(ref info)) return 0;
                uint elapsed = unchecked(GetTickCount() - info.dwTime);
                return elapsed / 1000.0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
