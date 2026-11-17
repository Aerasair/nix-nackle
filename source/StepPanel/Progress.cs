using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace StepPanel;

[Serializable]
public sealed class CoinLedger : IPluginConfiguration
{
    public int Version { get; set; } = 2;
    public long Coins { get; set; }
    public Dictionary<int, long> DailySteps { get; set; } = new();
    public Dictionary<int, int> DailyFishingCoins { get; set; } = new();
    public ulong UnlockedArt { get; set; }
    public ErrandState Errands { get; set; } = new();
    public Dictionary<DenGame, GameExperience> GameLevels { get; set; } = new();
    public int GameLevelsRevision { get; set; }

    internal bool ApplyGameLevelReset()
    {
        if (GameLevelsRevision >= 2)
            return false;

        GameLevels.Clear();
        GameLevelsRevision = 2;
        return true;
    }

    internal bool RecordGameCoins(DenGame game, int coins)
    {
        if (coins <= 0)
            return false;
        if (!GameLevels.TryGetValue(game, out var experience))
            GameLevels[game] = experience = new GameExperience();
        return experience.RecordCoins(game, coins);
    }

    internal bool AddSteps(int day, long steps)
    {
        var before = DailySteps.GetValueOrDefault(day);
        var after = before + steps;
        DailySteps[day] = after;
        if (before >= Progress.DailyGoal || after < Progress.DailyGoal)
            return false;

        ChangeCoins(Progress.DailyReward);
        return true;
    }

    internal void ChangeCoins(long delta)
    {
        Coins = delta > 0 && Coins > long.MaxValue - delta ? long.MaxValue : Coins + delta;
    }

    internal int RewardFishing(int day, int coins)
    {
        var earned = DailyFishingCoins.GetValueOrDefault(day);
        var reward = Math.Clamp(Progress.FishingDailyLimit - earned, 0, coins);
        if (reward > 0)
        {
            DailyFishingCoins[day] = earned + reward;
            ChangeCoins(reward);
            RecordGameCoins(DenGame.Fishing, reward);
        }
        return reward;
    }

    internal SealResult? OpenSeal(Random random)
    {
        if (Coins < Progress.PullPrice || (UnlockedArt & Progress.CompleteAlbum) == Progress.CompleteAlbum)
            return null;

        ChangeCoins(-Progress.PullPrice);
        if (random.Next(2) == 0)
            return new SealResult(null);

        var index = Progress.PickLockedArt(UnlockedArt, random)!.Value;
        UnlockedArt |= 1UL << index;
        return new SealResult(index);
    }
}

internal readonly record struct SealResult(int? Art);

internal sealed class Progress
{
    public const int DailyGoal = 10_000;
    public const int DailyReward = 100;
    public const int ArtCount = AlbumCatalog.Count;
    public const int PullPrice = 100;
    public const int FishingDailyLimit = 20;
    public const ulong CompleteAlbum = (1UL << ArtCount) - 1;
    private const long SaveIntervalMs = 10_000;

    private readonly object gate = new();
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly CoinLedger ledger;
    private bool dirty;
    private bool saveFailed;
    private long nextSave = Environment.TickCount64 + SaveIntervalMs;

    public Progress(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.log = log;
        ledger = pluginInterface.GetPluginConfig() as CoinLedger ?? new();
        if (ledger.ApplyGameLevelReset())
        {
            dirty = true;
            SaveLocked();
        }
    }

    public static int LocalDay => DateOnly.FromDateTime(DateTime.Today).DayNumber;

    public (long Coins, long Steps, ulong Art, bool SaveFailed) Snapshot()
    {
        lock (gate)
            return (ledger.Coins, ledger.DailySteps.GetValueOrDefault(LocalDay), ledger.UnlockedArt, saveFailed);
    }

    public void TrackSteps(int day, long steps)
    {
        lock (gate)
        {
            if (steps > 0)
            {
                dirty = true;
                if (ledger.AddSteps(day, steps))
                    SaveLocked();
            }

            if (dirty && Environment.TickCount64 >= nextSave)
                SaveLocked();
        }
    }

    public bool ChangeCoins(long delta)
    {
        lock (gate)
        {
            if (delta < 0 && (saveFailed || delta < -ledger.Coins))
                return false;

            if (delta != 0)
            {
                ledger.ChangeCoins(delta);
                dirty = true;
                SaveLocked();
            }
            return true;
        }
    }

    public ErrandState ErrandSnapshot()
    {
        lock (gate)
            return ledger.Errands.Copy();
    }

    public int GetGameExperience(DenGame game)
    {
        lock (gate)
            return ledger.GameLevels.TryGetValue(game, out var experience) ? experience.Total : 0;
    }

    public void FinishGame(DenGame game, int coins = 0, int spentCoins = 0)
    {
        lock (gate)
        {
            var gainedExperience = ledger.RecordGameCoins(game, coins + spentCoins);
            if (coins == 0 && !gainedExperience)
                return;
            ledger.ChangeCoins(coins);
            dirty = true;
            SaveLocked();
        }
    }

    public void SetErrandsEnabled(bool enabled)
    {
        lock (gate)
        {
            if (ledger.Errands.Enabled == enabled)
                return;
            ledger.Errands.Enabled = enabled;
            dirty = true;
            SaveLocked();
        }
    }

    public void AbandonErrand()
    {
        lock (gate)
        {
            if (!ledger.Errands.Abandon())
                return;
            dirty = true;
            SaveLocked();
        }
    }

    public void TrackErrands(double seconds, long steps, bool emote, bool landmarkAvailable, bool landmarkReached,
        bool sentTell = false)
    {
        lock (gate)
        {
            var result = ledger.Errands.Advance(seconds, steps, emote, landmarkAvailable, landmarkReached, Random.Shared, sentTell);
            if (!result.Changed)
                return;

            if (result.Reward > 0)
                ledger.ChangeCoins(result.Reward);
            dirty = true;
            if (result.SaveNow)
                SaveLocked();
        }
    }

    public int FishingEarnedToday()
    {
        lock (gate)
            return ledger.DailyFishingCoins.GetValueOrDefault(LocalDay);
    }

    public int RewardFishing(int coins)
    {
        lock (gate)
        {
            var day = LocalDay;
            var reward = ledger.RewardFishing(day, coins);
            if (reward > 0)
            {
                dirty = true;
                SaveLocked();
            }
            return reward;
        }
    }

    public SealResult? Pull()
    {
        lock (gate)
        {
            if (saveFailed || ledger.OpenSeal(Random.Shared) is not { } result)
                return null;

            dirty = true;
            SaveLocked();
            return result;
        }
    }

    internal static int? PickLockedArt(ulong unlocked, Random random)
    {
        Span<int> locked = stackalloc int[ArtCount];
        var count = 0;
        for (var i = 0; i < ArtCount; i++)
        {
            if ((unlocked & (1UL << i)) == 0)
                locked[count++] = i;
        }
        return count == 0 ? null : locked[random.Next(count)];
    }

    public void Save()
    {
        lock (gate)
        {
            if (dirty)
                SaveLocked();
        }
    }

    private void SaveLocked()
    {
        nextSave = Environment.TickCount64 + SaveIntervalMs;
        try
        {
            pluginInterface.SavePluginConfig(ledger);
            dirty = false;
            saveFailed = false;
        }
        catch (Exception error)
        {
            saveFailed = true;
            log.Error(error, "Could not save Step Panel progress.");
        }
    }
}
