using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace StepPanel;

internal sealed class VaultMazeView(Progress progress, NixArt art)
{
    private readonly VaultMaze maze = new(Random.Shared);

    public void Draw(float unit)
    {
        ImGui.TextUnformatted("The Sil'dihn Labyrinth");
        ImGui.TextWrapped("Click or hold the left mouse button over an adjacent tile to move Nix. Collect both seals, then reach the chest.");
        if (!maze.Started || maze.Complete)
        {
            if (ImGui.Button(maze.Started ? "New maze" : "Enter the maze (free)"))
                maze.Start();
        }
        if (!maze.Started)
            return;

        var tile = MathF.Max(1, MathF.Min(ImGui.GetContentRegionAvail().X / VaultMaze.Width, 2.05f * unit));
        var origin = ImGui.GetCursorScreenPos();
        var size = new Vector2(VaultMaze.Width, VaultMaze.Height) * tile;
        ImGui.InvisibleButton("Labyrinth", size);
        var hover = ImGui.IsItemHovered();
        var mouse = (ImGui.GetMousePos() - origin) / tile;
        if (hover && ImGui.IsMouseDown(ImGuiMouseButton.Left) && maze.Move((int)mouse.X, (int)mouse.Y))
            progress.FinishGame(DenGame.Vault, VaultMaze.Reward);

        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(origin, origin + size, true);
        draw.AddRectFilled(origin, origin + size, 0xff1d1919);
        for (var y = 0; y < VaultMaze.Height; y++)
        for (var x = 0; x < VaultMaze.Width; x++)
        {
            var cell = y * VaultMaze.Width + x;
            var min = origin + new Vector2(x, y) * tile;
            var center = min + new Vector2(tile * .5f);
            if (!maze.IsFloor(cell))
            {
                draw.AddRectFilled(min + Vector2.One, min + new Vector2(tile), 0xff504034);
                continue;
            }

            var highlighted = hover && (int)mouse.X == x && (int)mouse.Y == y;
            draw.AddRectFilled(min, min + new Vector2(tile), highlighted ? 0xff695c3a : 0xff3b302b);
            if (maze.HasKey(cell))
                art.Sprite(draw, "crystal.png", center, new Vector2(.79f * tile));
            if (cell == maze.Exit)
                art.Sprite(draw, "chest.png", center, new Vector2(.86f * tile),
                    tint: maze.KeysFound < VaultMaze.KeyCount ? 0xff8b8b8b : uint.MaxValue);
            if (cell == maze.Player && !maze.Complete)
                art.Sprite(draw, "face.png", center, new Vector2(.93f * tile));
        }
        draw.PopClipRect();
        ImGui.TextUnformatted(maze.Complete
            ? $"Vault cleared! +{VaultMaze.Reward} Nackle Coins"
            : $"Seals: {maze.KeysFound} / {VaultMaze.KeyCount}");
    }
}
