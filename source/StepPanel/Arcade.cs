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
            ? "Left-click the field to jump over cargo crates."
            : "Nix bounces on his own. Left-click the field to steer him toward that spot.");

        if (!active && ImGui.Button("Start (free)"))
            Start();

        DrawField(unit);
        ImGui.TextUnformatted(isCourier ? $"Crates cleared: {Score}" : $"Highest platform: {Score}");
        if (payout is { } reward)
            ImGui.TextColored(reward > 0 ? Jade : Gold, $"Run over. Payout: {reward} coins.");
        if (active)
        {
            if (ImGui.SmallButton("End run and collect earned payout"))
                Finish(completed: false);
        }

        ImGui.TextWrapped("Payouts: 12 = 3, 20 = 5, 30 = 7, 45 = 10 coins. The run ends at 45.");
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

    private void Finish(bool completed)
    {
        active = false;
        var reward = ArcadePayout.At(Score);
        payout = reward;
        if (completed)
            progress.FinishGame(isCourier ? DenGame.ChocoboCourier : DenGame.AetherAscent, reward);
        else
            progress.ChangeCoins(reward);
    }

    private void DrawField(float unit)
    {
        var width = MathF.Max(1, MathF.Min(ImGui.GetContentRegionAvail().X, 28 * unit));
        var scale = width / Width;
        var height = isCourier ? 195 : AetherAscent.Height;
        var origin = ImGui.GetCursorScreenPos();
        var size = new Vector2(width, height * scale);
        ImGui.InvisibleButton("Arcade field", size);
        if (active && ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (isCourier)
                courier.Jump();
            else
                ascent.Aim((ImGui.GetMousePos().X - origin.X) / scale);
        }

        var frame = ImGui.GetFrameCount();
        if (active && frame == lastFrame + 1)
        {
            timeLeft += Math.Clamp(ImGui.GetIO().DeltaTime, 0, .05f);
            while (active && timeLeft >= TickSeconds)
            {
                timeLeft -= TickSeconds;
                if (isCourier ? courier.Tick(TickSeconds) : ascent.Tick(TickSeconds))
                    Finish(completed: true);
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
            var half = AetherAscent.PlatformWidth / 2;
            artwork.Stretch(c.Draw, "ledge.png", c.Point(platform.X - half, y), c.Point(platform.X + half, y + 15));
        }

        var foot = AetherAscent.Height + camera - ascent.Y;
        var px = ascent.Platforms.Count == 0 ? Width / 2 : ascent.X;
        if (ascent.Platforms.Count == 0)
            foot -= 18;
        artwork.Sprite(c.Draw, "jump.png", c.Point(px, foot), new Vector2(36, 42) * c.Scale, bottom: true);
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
        foreach (var crate in courier.Crates)
        {
            if (crate.X > Width)
                continue;
            var min = c.Point(crate.X, ChocoboCourier.Ground - crate.Height);
            var max = c.Point(crate.X + crate.Width, ChocoboCourier.Ground);
            artwork.Stretch(c.Draw, "crate.png", min, max);
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
