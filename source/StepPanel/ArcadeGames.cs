using System;
using System.Collections.Generic;

namespace StepPanel;

internal static class ArcadePayout
{
    public static int At(int score) => score switch
    {
        >= 45 => 10,
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
    private const float FootRadius = 7;
    private const float JumpSpeed = 345;
    private const float Gravity = 660;
    private const float MaxSideSpeed = 200;
    public const float HazardRadius = 10;

    internal readonly record struct Platform(float X, float Y, float Width, int Level);
    internal readonly record struct Hazard(float Center, float Y, float Phase)
    {
        public float X(float time) => Center + MathF.Sin(time * 2.5f + Phase) * 12;
    }

    private readonly List<Platform> platforms = new();
    private readonly List<Hazard> hazards = new();
    private float horizontalVelocity;
    private float verticalVelocity;
    private int direction;

    public IReadOnlyList<Platform> Platforms => platforms;
    public IReadOnlyList<Hazard> Hazards => hazards;
    public float X { get; private set; }
    public float Y { get; private set; }
    public float Camera { get; private set; }
    public float Time { get; private set; }
    public float HorizontalVelocity => horizontalVelocity;
    public int Score { get; private set; }

    public void Start()
    {
        platforms.Clear();
        hazards.Clear();
        platforms.Add(new Platform(Width / 2, 18, 64, 0));
        X = Width / 2;
        Y = 18;
        Camera = Time = horizontalVelocity = 0;
        Score = direction = 0;
        verticalVelocity = JumpSpeed;
        ExtendPlatforms();
    }

    public void Steer(int input) => direction = Math.Clamp(input, -1, 1);

    public bool Tick(float dt)
    {
        Time += dt;
        var target = direction * MaxSideSpeed;
        var acceleration = (direction == 0 ? 540 : 720) * dt;
        horizontalVelocity += Math.Clamp(target - horizontalVelocity, -acceleration, acceleration);
        X = Math.Clamp(X + horizontalVelocity * dt, FootRadius, Width - FootRadius);
        if (X == FootRadius || X == Width - FootRadius)
            horizontalVelocity = 0;

        var previousY = Y;
        verticalVelocity -= Gravity * dt;
        Y += verticalVelocity * dt;
        foreach (var hazard in hazards)
            if (MathF.Abs(hazard.X(Time) - X) < HazardRadius + FootRadius &&
                MathF.Abs(hazard.Y - (Y + 17)) < HazardRadius + 13)
                return true;

        if (verticalVelocity < 0)
        {
            for (var i = platforms.Count - 1; i >= 0; i--)
            {
                var platform = platforms[i];
                if (previousY < platform.Y || Y > platform.Y ||
                    MathF.Abs(X - platform.X) > platform.Width / 2 + FootRadius)
                    continue;

                Y = platform.Y;
                verticalVelocity = JumpSpeed;
                Score = Math.Max(Score, platform.Level);
                break;
            }
        }

        var scroll = Score >= 5 ? MathF.Min(52, 6 + Score * .95f) * dt : 0;
        Camera = Math.Max(Camera + scroll, Y - Height * .62f);
        if (Y < Camera - 22)
            return true;

        ExtendPlatforms();
        while (platforms[0].Y < Camera - 45)
            platforms.RemoveAt(0);
        hazards.RemoveAll(hazard => hazard.Y < Camera - 40);
        return false;
    }

    private void ExtendPlatforms()
    {
        while (platforms[^1].Y < Camera + Height + 90)
        {
            var last = platforms[^1];
            var level = last.Level + 1;
            var side = last.X < 102 ? 1 : last.X > 198 ? -1 : random.Next(2) * 2 - 1;
            var x = last.X + random.Next(46, 79) * side;
            var y = last.Y + random.Next(53, 65) + Math.Min(level / 8, 10);
            var width = Math.Max(32, 56 - level * .48f);
            platforms.Add(new Platform(x, y, width, level));

            if (level >= 4 && level % (level < 18 ? 3 : 2) == 0)
            {
                // Trap the outside lane. The route between the two ledges stays open.
                var left = MathF.Min(last.X, x);
                var right = MathF.Max(last.X, x);
                var hazardX = left > Width - right ? left - 53 : right + 53;
                hazards.Add(new Hazard(hazardX, y + 16, random.NextSingle() * MathF.Tau));
            }
        }
    }
}

internal sealed class ChocoboCourier(Random random)
{
    public const float RiderX = 55;
    public const float Ground = 163;
    public const float RiderHalfWidth = 10;
    public const float GateClearance = 12;
    private const float JumpSpeed = 310;
    private const float Gravity = 830;
    private const float SpawnX = 340;

    internal readonly record struct Obstacle(float X, float Width, float Height, bool Overhead = false);

    private readonly List<Obstacle> obstacles = new();
    private float velocity;
    private float spawnDistance;
    private int patterns;

    public IReadOnlyList<Obstacle> Obstacles => obstacles;
    public float Lift { get; private set; }
    public float Distance { get; private set; }
    public float Speed { get; private set; }
    public int Score { get; private set; }

    public void Start()
    {
        obstacles.Clear();
        obstacles.Add(new Obstacle(SpawnX, 22, 22));
        Lift = velocity = Distance = 0;
        Speed = 132;
        spawnDistance = 155;
        Score = patterns = 0;
    }

    public void Jump()
    {
        if (Lift == 0)
            velocity = JumpSpeed;
    }

    public bool Tick(float dt)
    {
        Speed += 5.4f * dt;
        var travel = Speed * dt;
        Distance += travel;
        var oldLift = Lift;
        velocity -= Gravity * dt;
        Lift = Math.Max(0, Lift + velocity * dt);
        if (Lift == 0)
            velocity = 0;

        for (var i = obstacles.Count - 1; i >= 0; i--)
        {
            var obstacle = obstacles[i];
            var x = obstacle.X - travel;
            obstacles[i] = obstacle with { X = x };

            if (x < RiderX + RiderHalfWidth && obstacle.X + obstacle.Width > RiderX - RiderHalfWidth)
            {
                // Check the full sweep, including obstacles that cross both edges in one tick.
                var enter = Math.Clamp((obstacle.X - (RiderX + RiderHalfWidth)) / travel, 0, 1);
                var exit = Math.Clamp((obstacle.X + obstacle.Width - (RiderX - RiderHalfWidth)) / travel, 0, 1);
                var firstLift = float.Lerp(oldLift, Lift, enter);
                var lastLift = float.Lerp(oldLift, Lift, exit);
                if (obstacle.Overhead
                    ? MathF.Max(firstLift, lastLift) > GateClearance
                    : MathF.Min(firstLift, lastLift) < obstacle.Height)
                    return true;
            }

            if (x + obstacle.Width < RiderX - RiderHalfWidth &&
                obstacle.X + obstacle.Width >= RiderX - RiderHalfWidth)
                Score++;
            if (x + obstacle.Width < 0)
                obstacles.RemoveAt(i);
        }

        spawnDistance -= travel;
        while (spawnDistance <= 0)
        {
            var patternWidth = SpawnPattern(SpawnX + spawnDistance);
            spawnDistance += patternWidth + Math.Max(120, Speed * .92f) + random.Next(0, 45);
        }
        return false;
    }

    private float SpawnPattern(float x)
    {
        patterns++;
        if (patterns >= 17 && patterns % 5 == 2)
        {
            // The fixed gap eventually becomes too short to land after the tall crate.
            obstacles.Add(new Obstacle(x, 29, 38));
            obstacles.Add(new Obstacle(x + 114, 34, 30, Overhead: true));
            return 148;
        }

        switch (random.Next(patterns < 4 ? 2 : 4))
        {
            case 0:
                var width = random.Next(18, 30);
                obstacles.Add(new Obstacle(x, width, random.Next(17, 28)));
                return width;
            case 1:
                obstacles.Add(new Obstacle(x, 27, 36));
                return 27;
            case 2:
                obstacles.Add(new Obstacle(x, 18, 20));
                obstacles.Add(new Obstacle(x + 30, 19, 24));
                return 49;
            default:
                obstacles.Add(new Obstacle(x, 34, 30, Overhead: true));
                return 34;
        }
    }
}
