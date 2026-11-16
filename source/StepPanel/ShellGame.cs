using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace StepPanel;

internal sealed class ShellGame(Progress progress, NixArt artwork)
{
    private readonly CupShuffle game = new(Random.Shared);
    private int count = 3;
    private int? reward;
    private int? chosenSlot;
    private double elapsed;
    private int lastFrame = -1;

    public void Draw(float unit)
    {
        ImGui.TextUnformatted("Nix's Shell Game");
        ImGui.TextWrapped("Watch where Nix hides the cube. Follow the cup, then click it. A correct pick earns one coin per cup.");

        var playing = game.Count > 0 && reward is null;
        ImGui.BeginDisabled(playing);
        ImGui.RadioButton("3 Easy", ref count, 3);
        ImGui.SameLine();
        ImGui.RadioButton("5 Medium", ref count, 5);
        ImGui.SameLine();
        ImGui.RadioButton("7 Hard", ref count, 7);
        if (ImGui.Button("Hide the cube"))
        {
            game.Start(count);
            elapsed = 0;
            reward = chosenSlot = null;
        }
        ImGui.EndDisabled();

        if (game.Count == 0)
            return;

        var frame = ImGui.GetFrameCount();
        if (playing && frame == lastFrame + 1)
            elapsed += Math.Clamp(ImGui.GetIO().DeltaTime, 0, .05f);
        lastFrame = frame;
        var swapTime = elapsed - CupShuffle.PreviewSeconds;
        var shuffling = swapTime >= 0 && swapTime < game.Swaps.Length * game.SwapSeconds;
        var canPick = swapTime >= 0 && !shuffling && reward is null;
        DrawTable(unit, swapTime, shuffling, canPick);
        ImGui.TextUnformatted(reward switch
        {
            > 0 => $"Found it! +{reward} coins",
            0 => "Empty cup. The cube is under the raised cup.",
            _ when swapTime < 0 => "Watch the cube.",
            _ when shuffling => "Watch the cups...",
            _ => "Pick a cup.",
        });
    }

    private void DrawTable(float unit, double swapTime, bool shuffling, bool canPick)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1, MathF.Min(ImGui.GetContentRegionAvail().X, 28 * unit));
        var size = new Vector2(width, 8 * unit);
        var slotWidth = width / game.Count;
        var baseY = origin.Y + 5.75f * unit;
        ImGui.InvisibleButton("Cup table", size);
        var hover = ImGui.IsItemHovered();
        var hoveredSlot = hover ? Math.Clamp((int)((ImGui.GetMousePos().X - origin.X) / slotWidth), 0, game.Count - 1) : -1;
        if (canPick && hover && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            reward = game.Pick(hoveredSlot);
            chosenSlot = hoveredSlot;
            if (reward is { } coins)
                progress.FinishGame(DenGame.ShellGame, coins);
        }

        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(origin, origin + size, true);
        draw.AddRectFilled(origin, origin + size, 0xff24221b, .35f * unit);
        artwork.Background(draw, "den", origin, size, 0xffa9aaaa);
        artwork.Sprite(draw, "face.png", origin + new Vector2(width / 2, 1.45f * unit), new Vector2(2.6f * unit));
        var preview = swapTime < 0;
        var cubeSlot = preview ? game.InitialSlot : game.HiddenSlot;
        if (preview || reward.HasValue)
        {
            var center = new Vector2(origin.X + slotWidth * (cubeSlot + .5f), baseY - .52f * unit);
            var half = new Vector2(MathF.Min(slotWidth * .25f, .46f * unit));
            draw.AddRectFilled(center - half, center + half, 0xffa0dcf9, .12f * unit);
            draw.AddCircleFilled(center, .10f * unit, 0xff37302a);
        }

        var current = shuffling ? game.Swaps[(int)(swapTime / game.SwapSeconds)] : default;
        var phase = shuffling ? (float)(swapTime / game.SwapSeconds % 1) : 0;
        var blend = phase * phase * (3 - 2 * phase);
        for (var i = 0; i < game.Count; i++)
        {
            if (shuffling && (i == current.From || i == current.To))
                continue;

            var lift = preview && i == cubeSlot
                ? 1.75f * unit * Math.Clamp((float)((-swapTime - .06) / .30), 0, 1)
                : reward.HasValue && i == cubeSlot ? 1.75f * unit : 0;
            var color = chosenSlot == i || (canPick && hoveredSlot == i) ? 0xff88afa5u : 0xff72836au;
            DrawCup(i, -lift, color);
        }
        if (shuffling)
        {
            var arc = MathF.Sin(MathF.PI * phase) * 1.4f * unit;
            DrawCup(current.From + (current.To - current.From) * blend, -arc, 0xff72836a);
            DrawCup(current.To + (current.From - current.To) * blend, arc, 0xff72836a);
        }
        draw.PopClipRect();

        void DrawCup(float slot, float offset, uint color)
        {
            var x = origin.X + slotWidth * (slot + .5f);
            var y = baseY + offset;
            var half = MathF.Min(slotWidth * .38f, 1.75f * unit);
            var top = y - 2.65f * unit;
            artwork.Sprite(draw, "cup.png", new Vector2(x, y), new Vector2(2 * half, y - top), bottom: true,
                tint: color == 0xff88afa5u ? uint.MaxValue : 0xffdadada);
        }
    }
}
