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

    internal bool RecordRound()
    {
        if (Total >= GameLevels.MaxExperience)
            return false;

        Total = Math.Min(Total + GameLevels.ExperiencePerRound, GameLevels.MaxExperience);
        return true;
    }
}

internal static class GameLevels
{
    public const int MaxLevel = 110;
    public const int RoundsToMax = 500;
    public const int ExperiencePerRound = 100;
    public const int MaxExperience = RoundsToMax * ExperiencePerRound;

    public static int Threshold(int level)
    {
        var levels = Math.Clamp(level, 1, MaxLevel) - 1;
        var steps = MaxLevel - 1;
        const int firstLevelCost = 350;
        var baseXp = firstLevelCost * levels;
        var growth = MaxExperience - firstLevelCost * steps;
        return baseXp + growth * levels * (levels - 1) / (steps * (steps - 1));
    }

    public static int Level(int experience)
    {
        var level = 1;
        while (level < MaxLevel && experience >= Threshold(level + 1))
            level++;
        return level;
    }
}
