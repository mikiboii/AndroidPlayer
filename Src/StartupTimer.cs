using System;

namespace Androidplayer;

using System.Diagnostics;

public static class StartupTimer
{
    public static readonly Stopwatch Sw = Stopwatch.StartNew();
    public static void Mark(string label)
        => Console.WriteLine($"[startup +{Sw.ElapsedMilliseconds,5} ms] {label}");
}