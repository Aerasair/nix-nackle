using System;
using System.Collections.Generic;

namespace StepPanel;

internal sealed record SortLevel(string Name, int Colors, int Capacity, int Reward)
{
    public static readonly SortLevel[] Levels =
    [
        new("Easy", 4, 4, 2),
        new("Medium", 6, 4, 4),
        new("Hard", 8, 5, 6),
    ];
}

internal sealed class AetherSort(Random random)
{
    private readonly Stack<(int From, int To)> undo = new();
    private List<int>[] tubes = [];
    private int[][] initial = [];

    public SortLevel Level { get; private set; } = SortLevel.Levels[0];
    public int TubeCount => tubes.Length;
    public bool Won { get; private set; }
    public bool CanUndo => !Won && undo.Count > 0;
    public IReadOnlyList<int> Tube(int index) => tubes[index];

    public void Start(SortLevel level)
    {
        Level = level;
        tubes = new List<int>[level.Colors + 2];
        for (var i = 0; i < tubes.Length; i++)
        {
            tubes[i] = new List<int>(level.Capacity);
            if (i < level.Colors)
                for (var j = 0; j < level.Capacity; j++)
                    tubes[i].Add(i);
        }

        // Every cross-tube swap can be reversed through the two buffer tubes.
        for (var i = 0; i < level.Colors * level.Capacity * 3; i++)
        {
            var from = random.Next(level.Colors);
            var to = random.Next(level.Colors - 1);
            if (to >= from)
                to++;
            SwapContents(from, random.Next(level.Capacity), to, random.Next(level.Capacity));
        }
        if (IsSolved())
            SwapContents(0, 0, 1, 0);

        initial = Array.ConvertAll(tubes, tube => tube.ToArray());
        undo.Clear();
        Won = false;
    }

    public void Restart()
    {
        tubes = Array.ConvertAll(initial, contents => new List<int>(contents));
        undo.Clear();
        Won = false;
    }

    public bool TryMove(int from, int to)
    {
        if (Won || from == to || (uint)from >= tubes.Length || (uint)to >= tubes.Length ||
            tubes[from].Count == 0 || tubes[to].Count == Level.Capacity)
            return false;

        Transfer(from, to);
        undo.Push((from, to));
        Won = IsSolved();
        return true;
    }

    public bool Undo()
    {
        if (!CanUndo || !undo.TryPop(out var move))
            return false;
        Transfer(move.To, move.From);
        return true;
    }

    public bool IsCompleteTube(int index)
    {
        var contents = tubes[index];
        if (contents.Count != Level.Capacity)
            return false;
        for (var i = 1; i < contents.Count; i++)
            if (contents[i] != contents[0])
                return false;
        return true;
    }

    private bool IsSolved()
    {
        for (var i = 0; i < tubes.Length; i++)
            if (tubes[i].Count > 0 && !IsCompleteTube(i))
                return false;
        return true;
    }

    private void SwapContents(int a, int aDepth, int b, int bDepth)
    {
        var bufferA = Level.Colors;
        var bufferB = Level.Colors + 1;
        for (var i = 0; i < aDepth; i++)
            Transfer(a, bufferA);
        for (var i = 0; i < bDepth; i++)
            Transfer(b, bufferB);
        Transfer(a, bufferA);
        Transfer(b, a);
        Transfer(bufferA, b);
        for (var i = 0; i < aDepth; i++)
            Transfer(bufferA, a);
        for (var i = 0; i < bDepth; i++)
            Transfer(bufferB, b);
    }

    private void Transfer(int from, int to)
    {
        var source = tubes[from];
        tubes[to].Add(source[^1]);
        source.RemoveAt(source.Count - 1);
    }
}
