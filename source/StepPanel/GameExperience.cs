using System;

namespace StepPanel;

public enum DenGame
{
    TicTacToe,
    Aetherlight,
    ShellGame,
    Vault,
    AetherAscent,
    ChocoboCourier,
    Fishing,
    Memory,
    RugSpa,
}

public sealed class GameExperience
{
    public int Total { get; set; }

    internal bool RecordCoins(DenGame game, int coins)
    {
        var maximum = GameLevels.MaxExperience(game);
        if (coins <= 0 || Total >= maximum)
            return false;

        Total = (int)Math.Min(Total + (long)coins * GameLevels.ExperiencePerCoin, maximum);
        return true;
    }
}

internal static class GameLevels
{
    public const int MaxLevel = 110;
    public const int ExperiencePerCoin = 100;

    public static int CoinGoal(DenGame game) => game switch
    {
        DenGame.TicTacToe => 900,
        DenGame.Memory => 1_200,
        DenGame.Fishing or DenGame.RugSpa => 750,
        _ => 1_500,
    };

    public static int MaxExperience(DenGame game) => CoinGoal(game) * ExperiencePerCoin;

    public static int Threshold(DenGame game, int level)
    {
        var levels = Math.Clamp(level, 1, MaxLevel) - 1;
        var steps = MaxLevel - 1;
        var linear = levels * (steps - 1);
        var curved = levels * (levels - 1);
        // Use the same gently increasing curve for each game's XP target.
        return (int)((long)MaxExperience(game) * (3 * linear + curved) / (4 * steps * (steps - 1)));
    }

    public static int Level(DenGame game, int experience)
    {
        var level = 1;
        while (level < MaxLevel && experience >= Threshold(game, level + 1))
            level++;
        return level;
    }
}
