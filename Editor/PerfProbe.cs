// Copyright (C) 2026 Liquid Glass contributors
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEditor;

namespace LiquidGlass.Editor
{

    internal static class PerfProbe
    {
        sealed class Slot
        {
            public double Sum;
            public double Max;
            public int N;
        }

        static readonly Stopwatch s_Sw = new Stopwatch();
        static readonly Dictionary<string, Slot> s_Slots = new Dictionary<string, Slot>();
        static double s_LastLog = -1;
        static int s_Frames;

        public static void Begin()
        {
            s_Sw.Restart();
        }

        public static void End(string key)
        {
            s_Sw.Stop();
            double ms = s_Sw.Elapsed.TotalMilliseconds;

            if (!s_Slots.TryGetValue(key, out var s))
            {
                s = new Slot();
                s_Slots[key] = s;
            }
            s.Sum += ms;
            s.N++;
            if (ms > s.Max) s.Max = ms;
        }

        public static void Frame()
        {
            s_Frames++;

            double now = EditorApplication.timeSinceStartup;
            if (s_LastLog < 0)
            {
                s_LastLog = now;
                return;
            }
            if (now - s_LastLog < 5.0) return;

            var sb = new StringBuilder("Perf/5s frames=");
            sb.Append(s_Frames);
            foreach (var kv in s_Slots)
            {
                sb.Append(' ').Append(kv.Key).Append('=')
                  .Append((kv.Value.Sum / Math.Max(1, kv.Value.N)).ToString("0.000"))
                  .Append("/max").Append(kv.Value.Max.ToString("0.00"));
            }
            MacTitleBar.Log(sb.ToString());

            s_Slots.Clear();
            s_Frames = 0;
            s_LastLog = now;
        }
    }
}
