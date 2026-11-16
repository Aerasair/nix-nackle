using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace StepPanel;

internal static class ProfileView
{
    public static void Draw(Progress progress)
    {
        ImGui.TextUnformatted("Minigame Levels");
        ImGui.Spacing();

        DrawGame(progress, DenGame.TicTacToe, "Tic-Tac-Toe");
        DrawGame(progress, DenGame.Aetherlight, "Aetherlight");
        DrawGame(progress, DenGame.ShellGame, "Shell Game");
        DrawGame(progress, DenGame.Vault, "Vault");
        DrawGame(progress, DenGame.AetherAscent, "Aether Ascent");
        DrawGame(progress, DenGame.ChocoboCourier, "Chocobo Courier");
        DrawGame(progress, DenGame.Fishing, "Fishing");
        DrawGame(progress, DenGame.Memory, "Memory");
        DrawGame(progress, DenGame.RugSpa, "Rug Spa");
    }

    private static void DrawGame(Progress progress, DenGame game, string name)
    {
        var experience = progress.GetGameExperience(game);
        var level = GameLevels.Level(experience);
        ImGui.TextUnformatted($"{name}  ·  Lv. {level}");
        var start = GameLevels.Threshold(level);
        var next = GameLevels.Threshold(level + 1);
        var atMax = level == GameLevels.MaxLevel;
        ImGui.ProgressBar(atMax ? 1f : (experience - start) / (float)(next - start), new Vector2(-1, 0),
            atMax ? "MAX" : $"{experience - start} / {next - start} XP");
        ImGui.Spacing();
    }
}
