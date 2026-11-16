using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace StepPanel;

internal sealed class MemoryCards(Progress progress, NixArt artwork)
{
    private static readonly Vector4 FaceColor = new(.31f, .45f, .49f, 1);
    private static readonly Vector4 MatchColor = new(.22f, .49f, .35f, 1);
    private readonly MemoryRound round = new(Random.Shared);
    private int difficulty;

    public void Draw(float unit)
    {
        ImGui.TextUnformatted("Nix's Memory Cards");
        ImGui.TextWrapped("Flip two cards at a time. Only mismatched pairs count as mistakes.");
        for (var i = 0; i < MemoryLevel.Levels.Length; i++)
        {
            if (i > 0)
                ImGui.SameLine();
            ImGui.RadioButton(MemoryLevel.Levels[i].Name, ref difficulty, i);
        }
        var nextLevel = MemoryLevel.Levels[difficulty];
        ImGui.TextWrapped($"{nextLevel.Pairs} pairs · {nextLevel.MistakeLimit} mistakes allowed · {nextLevel.Reward} coins (+1 with {nextLevel.BonusLimit} mistakes or fewer)");
        if (ImGui.Button(round.Started ? $"New {nextLevel.Name} deck###DealMemory" : "Deal cards###DealMemory"))
            round.Deal(nextLevel);
        if (!round.Started)
            return;

        var now = ImGui.GetTime();
        round.Update(now);

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var columns = round.Level.Columns;
        var width = MathF.Min(4.8f * unit, (ImGui.GetContentRegionAvail().X - (columns - 1) * spacing) / columns);
        for (var i = 0; i < round.Count; i++)
        {
            if (i % columns != 0)
                ImGui.SameLine();

            var revealed = round.IsRevealed(i);
            ImGui.PushID(i);
            if (revealed)
                ImGui.PushStyleColor(ImGuiCol.Button, round.IsMatched(i) ? MatchColor : FaceColor);
            var clicked = ImGui.Button("###Card",
                new Vector2(MathF.Max(1, width), 2.8f * unit));
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            DrawFace(ImGui.GetWindowDrawList(), (min + max) / 2,
                MathF.Min(max.X - min.X, max.Y - min.Y) * .27f, revealed ? round.Face(i) : -1);
            if (revealed)
                ImGui.PopStyleColor();
            ImGui.PopID();
            if (clicked && !round.Won && !round.Lost)
            {
                var reward = round.Flip(i, now);
                if (round.Won || round.Lost)
                    progress.FinishGame(DenGame.Memory, reward);
            }
        }

        ImGui.TextWrapped($"{round.Level.Name} · Pairs: {round.PairCount} / {round.Level.Pairs} · Mistakes: {round.Mistakes} / {round.Level.MistakeLimit}");
        if (round.Won)
            ImGui.TextUnformatted($"Deck cleared! +{round.Reward} coins");
        else if (round.Lost)
            ImGui.TextUnformatted("Mistake limit exceeded. Deal a new deck to try again.");
    }

    private static readonly string[] Illustrations =
        ["sun", "moon", "star", "shell", "leaf", "crystal", "chest", "chocobo", "cup", "lantern", "rug-brush", "crate"];

    private void DrawFace(ImDrawListPtr draw, Vector2 center, float r, int face)
    {
        artwork.Sprite(draw, face < 0 ? "face.png" : Illustrations[face] + ".png", center, new Vector2(2.7f * r));
    }
}
