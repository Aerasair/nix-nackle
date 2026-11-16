using System;

namespace StepPanel;

internal sealed class FishingCast(Random random)
{
    public const float TargetHalfWidth = .085f;
    public float Target { get; private set; } = .5f;
    public float Marker { get; private set; }
    private float phase;

    public void Advance(float dt)
    {
        phase = (phase + dt / 1.25f) % 2;
        Marker = 1 - MathF.Abs(1 - phase);
    }

    public bool TryCatch()
    {
        if (MathF.Abs(Marker - Target) > TargetHalfWidth)
            return false;

        Target = Marker < .5f ? .65f + random.NextSingle() * .24f : .11f + random.NextSingle() * .24f;
        return true;
    }
}
