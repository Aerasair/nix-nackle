using System;
using System.Numerics;
using System.Threading;

namespace StepPanel;

internal sealed class StepCounter
{
    private const double UnitsPerStep = 0.8;
    private const double MaxSampleSeconds = 0.35;
    private const double MaxUnitsPerSecond = 14;
    private const double PositionTolerance = 0.03;

    private Vector2? previousPosition;
    private uint previousTerritory;
    private double remainingDistance;
    private long steps;

    public long Steps => Interlocked.Read(ref steps);

    public void Observe(Vector2 position, uint territory, double seconds)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
        {
            BreakPath();
            return;
        }

        var previous = previousPosition;
        var sameTerritory = previousTerritory == territory;
        previousPosition = position;
        previousTerritory = territory;

        if (previous is not { } start || !sameTerritory || seconds is <= 0 or > MaxSampleSeconds)
            return;

        var distance = Vector2.Distance(start, position);
        if (distance <= 0 || distance > MaxUnitsPerSecond * seconds + PositionTolerance)
            return;

        remainingDistance += distance;
        if (remainingDistance < UnitsPerStep)
            return;

        var completedSteps = (long)(remainingDistance / UnitsPerStep);
        remainingDistance -= completedSteps * UnitsPerStep;
        Interlocked.Add(ref steps, completedSteps);
    }

    public void BreakPath() => previousPosition = null;

    public void BreakDay()
    {
        BreakPath();
        remainingDistance = 0;
    }

    public void Reset()
    {
        BreakDay();
        Interlocked.Exchange(ref steps, 0);
    }
}
