using System;

namespace StepPanel;

public enum ErrandKind
{
    None,
    Walk,
    Greeting,
    LookBusy,
    Delivery,
    LongWalk,
    FriendlyTell,
}

public sealed class ErrandState
{
    public bool Enabled { get; set; } = true;
    public ErrandKind Active { get; set; }
    public ErrandKind Previous { get; set; }
    public double Wait { get; set; }
    public int Steps { get; set; }
    public bool Emoted { get; set; }
    public long Completed { get; set; }
    public long Earned { get; set; }
    public long Streak { get; set; }
    public long BestStreak { get; set; }
    public long Abandoned { get; set; }

    internal ErrandState Copy() => (ErrandState)MemberwiseClone();

    internal bool Abandon()
    {
        if (!Enabled || Active == ErrandKind.None)
            return false;
        Abandoned++;
        Streak = 0;
        Clear();
        return true;
    }

    internal (bool Changed, bool SaveNow, long Reward) Advance(
        double seconds, long steps, bool emote, bool landmarkAvailable, bool landmarkReached, Random random,
        bool sentTell = false)
    {
        if (!Enabled)
            return default;

        if (Active == ErrandKind.None)
        {
            if (seconds <= 0)
                return default;
            Wait = Math.Min(Errands.Interval, Wait + seconds);
            if (Wait < Errands.Interval)
                return (true, false, 0);

            Span<ErrandKind> options = stackalloc ErrandKind[(int)ErrandKind.FriendlyTell];
            var count = 0;
            for (var kind = ErrandKind.Walk; kind <= ErrandKind.FriendlyTell; kind++)
                if (kind != Previous && (kind != ErrandKind.Delivery || landmarkAvailable))
                    options[count++] = kind;
            Active = options[random.Next(count)];
            return (true, true, 0);
        }

        var target = Errands.StepGoal(Active);
        var previousSteps = Steps;
        var previousEmoted = Emoted;
        Steps = (int)Math.Min(target, Steps + steps);
        if (Errands.UsesEmote(Active))
            Emoted |= emote;
        var complete = Active switch
        {
            ErrandKind.Greeting => Emoted,
            ErrandKind.LookBusy => Emoted && Steps >= target,
            ErrandKind.Delivery => landmarkReached,
            ErrandKind.FriendlyTell => sentTell,
            ErrandKind.Walk or ErrandKind.LongWalk => Steps >= target,
            _ => false,
        };
        if (!complete)
            return (previousSteps != Steps || previousEmoted != Emoted, false, 0);

        Completed++;
        Streak++;
        BestStreak = Math.Max(BestStreak, Streak);
        Earned = Streak > long.MaxValue - Earned ? long.MaxValue : Earned + Streak;
        Clear();
        return (true, true, Streak);
    }

    private void Clear()
    {
        Previous = Active;
        Active = ErrandKind.None;
        Wait = 0;
        Steps = 0;
        Emoted = false;
    }
}

internal static class Errands
{
    public const double Interval = 600;

    public static bool UsesEmote(ErrandKind kind) => kind is ErrandKind.Greeting or ErrandKind.LookBusy;

    public static int StepGoal(ErrandKind kind) => kind switch
    {
        ErrandKind.Walk => 150,
        ErrandKind.LookBusy => 100,
        ErrandKind.LongWalk => 250,
        _ => 0,
    };

    public static string Name(ErrandKind kind) => kind switch
    {
        ErrandKind.Walk => "Inspect the Premises",
        ErrandKind.Greeting => "A Very Formal Greeting",
        ErrandKind.LookBusy => "Look Busy",
        ErrandKind.Delivery => "A Quiet Delivery",
        ErrandKind.LongWalk => "Check the Perimeter",
        ErrandKind.FriendlyTell => "Goodwill, Allegedly",
        _ => "",
    };

    public static string Objective(ErrandKind kind) => kind switch
    {
        ErrandKind.Greeting => "Use an emote, such as /bow or /wave.",
        ErrandKind.LookBusy => "Walk 100 steps and use an emote.",
        ErrandKind.Delivery => "Approach an aetheryte or market board.",
        ErrandKind.FriendlyTell => "Pick another adventurer and send a friendly /tell. Wish them luck or share a quick joke. One message is enough.",
        _ => $"Walk {StepGoal(kind)} steps in Eorzea.",
    };
}
