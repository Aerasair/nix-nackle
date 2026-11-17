using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace StepPanel;

internal static class ProfileView
{
    private readonly record struct Entry(DenGame Game, string Name, string Icon);
    private sealed record Section(string Name, uint Accent, uint IconBackground, Entry[] Games);

    private static readonly Section TableGames = new("Table Games", 0xffc49c68, 0xff473428,
    [
        new(DenGame.TicTacToe, "Tic-Tac-Toe", "face.png"),
        new(DenGame.ShellGame, "Shell Game", "cup.png"),
        new(DenGame.Memory, "Memory", "star.png"),
    ]);

    private static readonly Section Puzzles = new("Puzzles", 0xffd7b195, 0xff493647,
    [
        new(DenGame.Aetherlight, "Aetherlight", "crystal.png"),
        new(DenGame.Vault, "Vault", "chest.png"),
        new(DenGame.AetherSort, "Aether Sort", "lantern.png"),
    ]);

    private static readonly Section Arcade = new("Arcade", 0xff89a9df, 0xff453347,
    [
        new(DenGame.AetherAscent, "Aether Ascent", "jump.png"),
        new(DenGame.ChocoboCourier, "Chocobo Courier", "chocobo.png"),
    ]);

    private static readonly Section Pastimes = new("Pastimes", 0xffa6c992, 0xff354735,
    [
        new(DenGame.Fishing, "Fishing", "shell.png"),
        new(DenGame.RugSpa, "Rug Spa", "rug-brush.png"),
    ]);

    private const float Pad = .84f;
    private const float Gap = 1.45f;
    private const float Heading = 1.65f;
    private const float Row = 2.45f;
    private const float GroupGap = .9f;

    public static void Draw(Progress progress, NixArt artwork)
    {
        var unit = ImGui.GetFontSize();
        var available = MathF.Max(1, ImGui.GetContentRegionAvail().X);
        var width = MathF.Min(available, 49 * unit);
        var twoColumns = width >= 33.5f * unit;
        var height = unit * (2 * Pad + (twoColumns
            ? Height(TableGames) + Height(Puzzles) + GroupGap
            : Height(TableGames) + Height(Puzzles) + Height(Arcade) + Height(Pastimes) + 3 * GroupGap));

        var origin = ImGui.GetCursorScreenPos();
        var size = new Vector2(width, height);
        ImGui.Dummy(size);
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(origin, origin + size, true);
        draw.AddRectFilled(origin, origin + size, 0xff373b3e, .28f * unit);
        draw.AddRect(origin, origin + size, 0xff575b5d, .28f * unit);

        var start = origin + new Vector2(Pad * unit);
        var innerWidth = width - 2 * Pad * unit;
        var columnWidth = twoColumns ? (innerWidth - Gap * unit) / 2 : innerWidth;
        var second = DrawSection(draw, progress, artwork, TableGames, start, columnWidth, unit);
        var third = DrawSection(draw, progress, artwork, Puzzles, second, columnWidth, unit);
        var arcadeOrigin = twoColumns ? start + new Vector2(columnWidth + Gap * unit, 0) : third;
        var pastimesOrigin = DrawSection(draw, progress, artwork, Arcade, arcadeOrigin, columnWidth, unit);
        DrawSection(draw, progress, artwork, Pastimes, pastimesOrigin, columnWidth, unit);
        draw.PopClipRect();
    }

    private static float Height(Section section) => Heading + section.Games.Length * Row;

    private static Vector2 DrawSection(ImDrawListPtr draw, Progress progress, NixArt artwork, Section section,
        Vector2 origin, float width, float unit)
    {
        const uint headingText = 0xffc2c6c9;
        draw.AddCircleFilled(origin + new Vector2(.35f, .48f) * unit, .21f * unit, section.Accent);
        draw.AddText(origin + new Vector2(.78f, 0) * unit, headingText, section.Name);
        draw.AddLine(origin + new Vector2(0, 1.18f * unit), origin + new Vector2(width, 1.18f * unit), 0xff777b7f);
        for (var i = 0; i < section.Games.Length; i++)
        {
            var top = origin + new Vector2(0, (Heading + i * Row) * unit);
            DrawGame(draw, progress, artwork, section, section.Games[i], top, width, unit);
        }

        return origin + new Vector2(0, (Height(section) + GroupGap) * unit);
    }

    private static void DrawGame(ImDrawListPtr draw, Progress progress, NixArt artwork, Section section, Entry entry,
        Vector2 top, float width, float unit)
    {
        var experience = progress.GetGameExperience(entry.Game);
        var level = GameLevels.Level(entry.Game, experience);
        var atMax = level == GameLevels.MaxLevel;
        var start = GameLevels.Threshold(entry.Game, level);
        var next = GameLevels.Threshold(entry.Game, level + 1);
        var fill = atMax ? 1 : (experience - start) / (float)(next - start);

        var iconSize = new Vector2(1.97f * unit);
        draw.AddRectFilled(top + Vector2.One, top + iconSize + Vector2.One, 0xff222426, .24f * unit);
        draw.AddRectFilled(top, top + iconSize, section.IconBackground, .24f * unit);
        draw.AddRect(top, top + iconSize, 0xff77a8c9, .24f * unit);
        artwork.Sprite(draw, entry.Icon, top + iconSize / 2, iconSize * .81f);

        var numberRight = top.X + 4.55f * unit;
        var number = level.ToString();
        const float numberScale = 1.22f;
        var numberWidth = ImGui.CalcTextSize(number).X * numberScale;
        draw.AddText(ImGui.GetFont(), unit * numberScale, new Vector2(numberRight - numberWidth, top.Y + .29f * unit),
            atMax ? 0xff5b9af3 : 0xffe3e6e6, number);

        var nameX = top.X + 4.87f * unit;
        var available = MathF.Max(1, top.X + width - nameX);
        var nameScale = MathF.Min(1, available / MathF.Max(1, ImGui.CalcTextSize(entry.Name).X));
        draw.AddText(ImGui.GetFont(), unit * nameScale, new Vector2(nameX, top.Y + .16f * unit), 0xffdde0e1, entry.Name);

        var track = new Vector2(nameX, top.Y + 1.43f * unit);
        var bar = new Vector2(available, .28f * unit);
        draw.AddRectFilled(track, track + bar, 0xff606568);
        if (fill > 0)
            draw.AddRectFilled(track, track + new Vector2(bar.X * fill, bar.Y), atMax ? 0xff6a91c8 : 0xffc0c2c3);

        if (ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(top, top + new Vector2(width, Row * unit)))
            ImGui.SetTooltip(atMax ? "Maximum level" : $"{experience - start:N0} / {next - start:N0} XP");
    }
}
