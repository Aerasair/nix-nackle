using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace StepPanel;

internal sealed class AetherSortView(Progress progress, NixArt art)
{
    private const double FlightTime = .34;
    private static readonly string[] Symbols = ["+", "=", "^", "x", "o", "|", "-", "*"];
    private static readonly uint[] Colors =
        [0xff5353ef, 0xff49aaff, 0xff5ed9ff, 0xff6bc76c, 0xffe9a353, 0xff8dd750, 0xffdb69ab, 0xffb69cf5];
    private readonly AetherSort round = new(Random.Shared);
    private int difficulty;
    private int? selected;
    private Flight? flight;

    private readonly record struct Flight(int From, int To, int ToSlot, int Color, double Start);

    public void Draw(float unit)
    {
        ImGui.TextUnformatted("Nix's Aether Sort");
        ImGui.TextWrapped("Pick a tube, then move its top crystal to any tube with room. Fill one tube with each color.");
        for (var i = 0; i < SortLevel.Levels.Length; i++)
        {
            if (i > 0)
                ImGui.SameLine();
            ImGui.RadioButton(SortLevel.Levels[i].Name, ref difficulty, i);
        }

        var next = SortLevel.Levels[difficulty];
        if (ImGui.Button($"New {next.Name} puzzle###NewAetherSort"))
        {
            round.Start(next);
            selected = null;
            flight = null;
        }
        if (round.TubeCount == 0)
            return;

        var now = ImGui.GetTime();
        if (flight is { } move && now - move.Start >= FlightTime)
            flight = null;

        ImGui.SameLine();
        ImGui.BeginDisabled(!round.CanUndo || flight.HasValue);
        if (ImGui.Button("Undo") && round.Undo())
            selected = null;
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(round.Won);
        if (ImGui.Button("Restart"))
        {
            round.Restart();
            selected = null;
            flight = null;
        }
        ImGui.EndDisabled();

        DrawRack(unit, now);
        if (round.Won && !flight.HasValue)
            ImGui.TextUnformatted($"All sorted! +{round.Level.Reward} Nackle Coins");
    }

    private void DrawRack(float unit, double now)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1, MathF.Min(ImGui.GetContentRegionAvail().X, 38 * unit));
        var foot = (5f + round.Level.Capacity * 1.35f) * unit;
        var size = new Vector2(width, foot + .9f * unit);
        var slotWidth = (width - .8f * unit) / round.TubeCount;
        var radius = MathF.Max(1, MathF.Min(.60f * unit, slotWidth * .33f));
        var top = foot - (round.Level.Capacity * 1.35f + .9f) * unit;
        var tubeHalf = MathF.Min(.86f * unit, slotWidth * .43f);
        ImGui.InvisibleButton("Aether Sort rack", size);
        var mouse = ImGui.GetMousePos() - origin;
        var hovered = (int)MathF.Floor((mouse.X - .4f * unit) / slotWidth);
        if (!ImGui.IsItemHovered() || mouse.Y < top - .5f * unit || mouse.Y > foot ||
            (uint)hovered >= round.TubeCount)
            hovered = -1;
        if (hovered >= 0 && !round.Won && !flight.HasValue && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            ClickTube(hovered, now);

        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(origin, origin + size, true);
        draw.AddRectFilled(origin, origin + size, 0xff2d2820, .3f * unit);
        art.Background(draw, "den", origin, size, 0xffadabab);
        art.Sprite(draw, "face.png", origin + new Vector2(width - 2.15f * unit, 1.64f * unit), new Vector2(2.8f * unit));
        draw.AddRectFilled(origin + new Vector2(.16f * unit, foot), origin + new Vector2(width - .16f * unit, foot + .30f * unit),
            0xff74a3cc, .10f * unit);

        for (var tube = 0; tube < round.TubeCount; tube++)
        {
            var contents = round.Tube(tube);
            var center = CrystalPosition(tube, 0);
            var left = new Vector2(center.X - tubeHalf, origin.Y + top);
            var right = new Vector2(center.X + tubeHalf, origin.Y + foot);
            var chrome = selected == tube ? 0xff9edcf9 : round.IsCompleteTube(tube) ? 0xff94d8a0 :
                hovered == tube ? 0xffbdd9dc : 0xff82988f;
            draw.AddRectFilled(left, right, 0xe62c2823, tubeHalf * .65f);
            draw.AddRect(left, right, chrome, tubeHalf * .65f, ImDrawFlags.None, 1.5f);
            draw.AddRectFilled(left - new Vector2(.075f * unit, .10f * unit),
                new Vector2(right.X + .075f * unit, left.Y + .12f * unit), chrome, .08f * unit);

            for (var j = 0; j < contents.Count; j++)
            {
                if (flight is { } pending && pending.To == tube && pending.ToSlot == j)
                    continue;
                var centerOfCrystal = CrystalPosition(tube, j);
                if (selected == tube && j == contents.Count - 1)
                    centerOfCrystal.Y = left.Y - .85f * unit;
                DrawCrystal(draw, centerOfCrystal, radius, contents[j]);
            }
        }

        if (flight is { } flying)
        {
            var t = Math.Clamp((float)((now - flying.Start) / FlightTime), 0, 1);
            var from = CrystalPosition(flying.From, 0);
            from.Y = origin.Y + top - .85f * unit;
            var to = CrystalPosition(flying.To, flying.ToSlot);
            var center = Vector2.Lerp(from, to, t);
            center.Y -= MathF.Sin(t * MathF.PI) * 2.05f * unit;
            DrawCrystal(draw, center, radius, flying.Color);
        }
        draw.PopClipRect();

        Vector2 CrystalPosition(int tube, int depth) => origin + new Vector2(
            .4f * unit + (tube + .5f) * slotWidth, foot - (.82f + depth * 1.35f) * unit);
    }

    private void ClickTube(int tube, double now)
    {
        if (selected is not { } from)
        {
            if (round.Tube(tube).Count > 0)
                selected = tube;
            return;
        }
        if (from == tube)
        {
            selected = null;
            return;
        }

        var source = round.Tube(from);
        var toSlot = round.Tube(tube).Count;
        var color = source[^1];
        if (!round.TryMove(from, tube))
            return;

        selected = null;
        flight = new Flight(from, tube, toSlot, color, now);
        if (round.Won)
            progress.FinishGame(DenGame.AetherSort, round.Level.Reward);
    }

    private static void DrawCrystal(ImDrawListPtr draw, Vector2 center, float radius, int color)
    {
        var top = center - new Vector2(0, radius);
        var right = center + new Vector2(radius * .85f, 0);
        var bottom = center + new Vector2(0, radius);
        var left = center - new Vector2(radius * .85f, 0);
        draw.AddQuadFilled(top, right, bottom, left, Colors[color]);
        draw.AddTriangleFilled(top, left, center, 0x65ffffff);
        draw.AddTriangleFilled(right, bottom, center, 0x600c1124);
        draw.AddQuad(top, right, bottom, left, 0x939ddbe9);
        var symbol = Symbols[color];
        var size = radius * 1.2f;
        var measure = ImGui.CalcTextSize(symbol) * (size / ImGui.GetFontSize());
        draw.AddText(ImGui.GetFont(), size, center - measure / 2, 0xff171622, symbol);
    }
}
