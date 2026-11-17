using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace StepPanel;

internal sealed class Arcade(Progress progress, NixArt artwork, bool isCourier)
{
    private const float TickSeconds = 1f / 120;
    private const float Width = AetherAscent.Width;
    private static readonly Vector4 Gold = new(.97f, .76f, .40f, 1);
    private static readonly Vector4 Jade = new(.39f, .87f, .76f, 1);

    private readonly AetherAscent ascent = new(Random.Shared);
    private readonly ChocoboCourier courier = new(Random.Shared);
    private bool active;
    private int? payout;
    private int lastFrame = -1;
    private float timeLeft;

    private int Score => isCourier ? courier.Score : ascent.Score;

    public void Draw(float unit)
    {
        ImGui.TextWrapped(isCourier
            ? "Left-click to jump over crates. Stay low under overhead gates. The trail keeps speeding up."
            : "Nix bounces on his own. Hold left-click on either half to steer that way. Release to brake. Avoid red crystals and stay ahead of the rising screen.");

        if (!active && ImGui.Button("Start (free)"))
            Start();

        DrawField(unit);
        ImGui.TextUnformatted(isCourier ? $"Obstacles cleared: {Score}" : $"Highest platform: {Score}");
        if (payout is { } reward)
            ImGui.TextColored(reward > 0 ? Jade : Gold, $"Run over. Payout: {reward} coins.");
        if (active)
        {
            if (ImGui.SmallButton("End run and collect earned payout"))
                Finish();
        }

        ImGui.TextWrapped("Payouts: 12 = 3, 20 = 5, 30 = 7, 45+ = 10 coins.");
    }

    private void Start()
    {
        if (isCourier)
            courier.Start();
        else
            ascent.Start();
        active = true;
        payout = null;
        timeLeft = 0;
        lastFrame = -1;
    }

    private void Finish()
    {
        active = false;
        var reward = ArcadePayout.At(Score);
        payout = reward;
        progress.FinishGame(isCourier ? DenGame.ChocoboCourier : DenGame.AetherAscent, reward);
    }

    private void DrawField(float unit)
    {
        var width = MathF.Max(1, MathF.Min(ImGui.GetContentRegionAvail().X, 28 * unit));
        var scale = width / Width;
        var height = isCourier ? 195 : AetherAscent.Height;
        var origin = ImGui.GetCursorScreenPos();
        var size = new Vector2(width, height * scale);
        ImGui.InvisibleButton("Arcade field", size);
        var overField = ImGui.IsItemHovered();
        if (active)
        {
            if (isCourier)
            {
                if (overField && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    courier.Jump();
            }
            else
            {
                var held = overField && ImGui.IsMouseDown(ImGuiMouseButton.Left);
                var right = ImGui.GetMousePos().X >= origin.X + width / 2;
                ascent.Steer(held ? right ? 1 : -1 : 0);
            }
        }

        var frame = ImGui.GetFrameCount();
        if (active && frame == lastFrame + 1)
        {
            timeLeft += Math.Clamp(ImGui.GetIO().DeltaTime, 0, .05f);
            while (active && timeLeft >= TickSeconds)
            {
                timeLeft -= TickSeconds;
                if (isCourier ? courier.Tick(TickSeconds) : ascent.Tick(TickSeconds))
                    Finish();
            }
        }
        lastFrame = frame;

        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(origin, origin + size, true);
        var canvas = new Canvas(draw, origin, scale);
        if (isCourier)
            DrawCourier(canvas);
        else
            DrawAscent(canvas);
        draw.PopClipRect();
    }

    private void DrawAscent(Canvas c)
    {
        c.Rect(0, 0, Width, AetherAscent.Height, 0xff272018);
        var camera = ascent.Camera;
        artwork.Background(c.Draw, "ascent", c.Origin, new Vector2(Width, AetherAscent.Height) * c.Scale, 0xffd0d0d0);
        foreach (var platform in ascent.Platforms)
        {
            var y = AetherAscent.Height + camera - platform.Y;
            if (y < -12 || y > AetherAscent.Height)
                continue;
            var half = platform.Width / 2;
            artwork.Stretch(c.Draw, "ledge.png", c.Point(platform.X - half, y), c.Point(platform.X + half, y + 15));
        }

        foreach (var hazard in ascent.Hazards)
        {
            var y = AetherAscent.Height + camera - hazard.Y;
            if (y < -20 || y > AetherAscent.Height + 20)
                continue;
            var point = c.Point(hazard.X(ascent.Time), y);
            c.Draw.AddCircleFilled(point, 14 * c.Scale, 0x685148ff);
            c.Draw.AddCircle(point, 13 * c.Scale, 0xb45771ff);
            artwork.Sprite(c.Draw, "crystal.png", point, new Vector2(20, 32) * c.Scale, tint: 0xff888dff);
        }

        var foot = AetherAscent.Height + camera - ascent.Y;
        var px = ascent.Platforms.Count == 0 ? Width / 2 : ascent.X;
        if (ascent.Platforms.Count == 0)
            foot -= 18;
        artwork.Sprite(c.Draw, "jump.png", c.Point(px, foot), new Vector2(36, 42) * c.Scale, bottom: true);
        c.Draw.AddLine(c.Point(Width / 2, AetherAscent.Height - 17), c.Point(Width / 2, AetherAscent.Height - 4), 0x66eeeeee);
        c.Draw.AddText(c.Point(7, AetherAscent.Height - 18), 0xbbeeeeee, "<");
        c.Draw.AddText(c.Point(Width - 16, AetherAscent.Height - 18), 0xbbeeeeee, ">");
    }

    private void DrawCourier(Canvas c)
    {
        c.Rect(0, 0, Width, 195, 0xff68517c);
        artwork.Background(c.Draw, "desert", c.Origin, new Vector2(Width, 195) * c.Scale);
        for (var i = 0; i < 10; i++)
        {
            var x = i * 36 - courier.Distance % 36;
            c.Rect(x, 172, x + 16, 173, 0x50806232);
        }
        foreach (var obstacle in courier.Obstacles)
        {
            if (obstacle.X > Width)
                continue;
            var bottom = ChocoboCourier.Ground - (obstacle.Overhead ? 60 : 0);
            var min = c.Point(obstacle.X, bottom - obstacle.Height);
            var max = c.Point(obstacle.X + obstacle.Width, bottom);
            artwork.Stretch(c.Draw, "crate.png", min, max, obstacle.Overhead ? 0xff84c5ff : uint.MaxValue);
            if (obstacle.Overhead)
            {
                c.Draw.AddLine(c.Point(obstacle.X + 3, 0), min + new Vector2(3 * c.Scale, 0), 0xff434756, 2);
                c.Draw.AddLine(c.Point(obstacle.X + obstacle.Width - 3, 0),
                    c.Point(obstacle.X + obstacle.Width - 3, bottom - obstacle.Height), 0xff434756, 2);
                c.Draw.AddLine(c.Point(obstacle.X, bottom), max, 0xff67cbff, 2);
            }
        }

        var px = ChocoboCourier.RiderX;
        var py = ChocoboCourier.Ground - courier.Lift;
        var stride = active && courier.Lift == 0 ? MathF.Sin(courier.Distance * .17f) * 2.2f : 0;
        artwork.Sprite(c.Draw, "chocobo.png", c.Point(px + 1, py - MathF.Abs(stride)),
            new Vector2(47, 45) * c.Scale, bottom: true);
        artwork.Sprite(c.Draw, "rider.png", c.Point(px - 5, py - 19 - MathF.Abs(stride)),
            new Vector2(32, 35) * c.Scale, bottom: true);
    }

    private readonly record struct Canvas(ImDrawListPtr Draw, Vector2 Origin, float Scale)
    {
        public void Rect(float x, float y, float right, float bottom, uint color, float radius = 0) =>
            Draw.AddRectFilled(Point(x, y), Point(right, bottom), color, radius * Scale);

        public Vector2 Point(float x, float y) => Origin + new Vector2(x, y) * Scale;
    }
}
