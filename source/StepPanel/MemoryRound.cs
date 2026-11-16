using System;

namespace StepPanel;

internal sealed record MemoryLevel(string Name, int Pairs, int Columns, int ExtraMistakes, int Reward, int BonusLimit)
{
    public static readonly MemoryLevel[] Levels =
    [
        new("Easy", 6, 4, 3, 2, 3),
        new("Medium", 8, 4, 2, 3, 4),
        new("Hard", 12, 6, 1, 5, 6),
    ];

    public int MistakeLimit => Pairs - 1 + ExtraMistakes;
}

internal sealed class MemoryRound(Random random)
{
    private readonly int[] cards = new int[24];
    private readonly bool[] matched = new bool[24];
    private int first = -1;
    private int second = -1;
    private double hideAt;

    public MemoryLevel Level { get; private set; } = MemoryLevel.Levels[0];
    public bool Started { get; private set; }
    public int PairCount { get; private set; }
    public int Mistakes { get; private set; }
    public int Count => Level.Pairs * 2;
    public bool Won => PairCount == Level.Pairs;
    public bool Lost => Mistakes > Level.MistakeLimit;
    public int Reward => Won ? Level.Reward + (Mistakes <= Level.BonusLimit ? 1 : 0) : 0;
    public int Face(int index) => cards[index];
    public bool IsMatched(int index) => matched[index];
    public bool IsRevealed(int index) => matched[index] || index == first || index == second;

    public void Deal(MemoryLevel level)
    {
        Level = level;
        for (var i = 0; i < Count; i++)
            cards[i] = i / 2;
        random.Shuffle(cards.AsSpan(0, Count));
        Array.Clear(matched);
        first = second = -1;
        PairCount = Mistakes = 0;
        Started = true;
    }

    public void Update(double now)
    {
        if (second >= 0 && now >= hideAt)
            first = second = -1;
    }

    public int Flip(int index, double now)
    {
        if (!Started || Won || Lost || second >= 0 || IsRevealed(index))
            return 0;
        if (first < 0)
        {
            first = index;
            return 0;
        }
        if (cards[first] != cards[index])
        {
            Mistakes++;
            second = index;
            hideAt = now + .8;
            return 0;
        }

        matched[first] = matched[index] = true;
        first = -1;
        PairCount++;
        return Reward;
    }
}
