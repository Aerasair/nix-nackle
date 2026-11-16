using System;
using System.Collections.Generic;

namespace StepPanel;

internal static class ArcadePayout
{
    public const int FinishScore = 45;

    public static int At(int score) => score switch
    {
        >= FinishScore => 10,
        >= 30 => 7,
        >= 20 => 5,
        >= 12 => 3,
        _ => 0,
    };
}

internal sealed class AetherAscent(Random random)
{
    public const float Width = 300;
    public const float Height = 240;
    public const float PlatformWidth = 54;
    private const float FootRadius = 8;
    private const float JumpSpeed = 330;
    private const float Gravity = 660;

    internal readonly record struct Platform(float X, float Y, int Level);

    private readonly List<Platform> platforms = new();
    private float targetX;
    private float velocity;

    public IReadOnlyList<Platform> Platforms => platforms;
    public float X { get; private set; }
    public float Y { get; private set; }
    public float Camera { get; private set; }
    public int Score { get; private set; }

    public void Start()
    {
        platforms.Clear();
        platforms.Add(new Platform(Width / 2, 18, 0));
        X = targetX = Width / 2;
        Y = 18;
        Camera = 0;
        Score = 0;
        velocity = JumpSpeed;
        ExtendPlatforms();
    }

    public void Aim(float x) => targetX = Math.Clamp(x, FootRadius, Width - FootRadius);

    // Returns true when the run is finished.
    public bool Tick(float dt)
    {
        X += Math.Clamp(targetX - X, -225 * dt, 225 * dt);
        var previousY = Y;
        velocity -= Gravity * dt;
        Y += velocity * dt;
        if (velocity < 0)
        {
            for (var i = platforms.Count - 1; i >= 0; i--)
            {
                var platform = platforms[i];
                if (previousY < platform.Y || Y > platform.Y ||
                    MathF.Abs(X - platform.X) > PlatformWidth / 2 + FootRadius)
                    continue;

                Y = platform.Y;
                velocity = JumpSpeed;
                Score = Math.Max(Score, platform.Level);
                break;
            }
        }

        Camera = Math.Max(Camera, Y - Height * .62f);
        if (platforms[0].Y < Camera - 45)
            platforms.RemoveAt(0);
        ExtendPlatforms();
        return Y < Camera - 22 || Score >= ArcadePayout.FinishScore;
    }

    private void ExtendPlatforms()
    {
        while (platforms[^1].Y < Camera + Height + 90)
        {
            var last = platforms[^1];
            var direction = last.X < 105 ? 1 : last.X > 195 ? -1 : random.Next(2) * 2 - 1;
            var offset = random.Next(49, 91) * direction;
            platforms.Add(new Platform(last.X + offset, last.Y + random.Next(53, 68), last.Level + 1));
        }
    }
}

internal sealed class ChocoboCourier(Random random)
{
    public const float RiderX = 55;
    public const float Ground = 163;
    public const float RiderHalfWidth = 10;

    internal readonly record struct Crate(float X, float Width, float Height);

    private readonly List<Crate> crates = new();
    private float velocity;
    private float spawnDistance;

    public IReadOnlyList<Crate> Crates => crates;
    public float Lift { get; private set; }
    public float Distance { get; private set; }
    public int Score { get; private set; }

    public void Start()
    {
        crates.Clear();
        crates.Add(new Crate(340, 22, 22));
        Lift = velocity = Distance = 0;
        spawnDistance = 150;
        Score = 0;
    }

    public void Jump()
    {
        if (Lift == 0)
            velocity = 310;
    }

    public bool Tick(float dt)
    {
        var speed = 132 + Score * 3.4f;
        var travel = speed * dt;
        Distance += travel;
        spawnDistance -= travel;
        if (spawnDistance <= 0)
        {
            crates.Add(new Crate(340, random.Next(17, 34), random.Next(17, 39)));
            // Leave time to land before the next jump, even at top speed.
            spawnDistance += Math.Max(145, speed * .94f) + random.Next(0, 73);
        }

        velocity -= 830 * dt;
        Lift = Math.Max(0, Lift + velocity * dt);
        if (Lift == 0)
            velocity = 0;

        for (var i = crates.Count - 1; i >= 0; i--)
        {
            var crate = crates[i];
            var x = crate.X - travel;
            crates[i] = crate with { X = x };
            if (x < RiderX + RiderHalfWidth && x + crate.Width > RiderX - RiderHalfWidth && Lift < crate.Height)
                return true;

            if (x + crate.Width < RiderX - RiderHalfWidth &&
                crate.X + crate.Width >= RiderX - RiderHalfWidth)
                Score++;

            if (x + crate.Width < 0)
                crates.RemoveAt(i);
        }

        return Score >= ArcadePayout.FinishScore;
    }
}
