using System;

namespace StepPanel;

internal sealed class RugCycle
{
    public const int Passes = 6;
    public const int RugCount = 3;
    public const int Price = 1;
    public const double WashSeconds = 15;
    public const double PassSeconds = WashSeconds / Passes;
    private const double DisplaySeconds = .75;
    private double? start;
    private double? pausedAt;

    public int Rug { get; private set; }
    public bool Busy => start.HasValue;
    public bool Running { get; private set; }

    public bool Start(double now, Func<int, bool> pay)
    {
        if (Busy || !pay(Price))
            return false;

        start = now;
        Running = true;
        return true;
    }

    public void Continue(double now, bool ready, Func<int, bool> pay)
    {
        if (Running && !Busy && ready && !Start(now, pay))
            Running = false;
    }

    public void Stop(double now)
    {
        if (!Running)
            return;

        Running = false;
        if (Busy)
            pausedAt = now;
    }

    public void Resume(double now)
    {
        if (start is { } began && pausedAt is { } paused)
            start = began + Math.Max(0, now - paused);
        pausedAt = null;
        Running = true;
    }

    public double Advance(double now)
    {
        if (start is not { } began)
            return 0;

        var elapsed = Math.Max(0, (pausedAt ?? now) - began);
        if (elapsed < WashSeconds + DisplaySeconds)
            return Math.Min(elapsed, WashSeconds);

        Rug = (Rug + 1) % RugCount;
        start = null;
        return 0;
    }
}
