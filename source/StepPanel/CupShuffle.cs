using System;

namespace StepPanel;

internal sealed class CupShuffle(Random random)
{
    internal readonly record struct Swap(int From, int To);
    public const double PreviewSeconds = 1.65;
    private int initialSlot;
    private bool picked;

    public int Count { get; private set; }
    public int HiddenSlot { get; private set; }
    public Swap[] Swaps { get; private set; } = [];
    public double SwapSeconds => Count switch { 3 => .62, 5 => .43, _ => .30 };
    public int InitialSlot => initialSlot;

    public void Start(int count)
    {
        Count = count;
        initialSlot = HiddenSlot = random.Next(count);
        picked = false;
        Swaps = new Swap[count switch { 3 => 5, 5 => 10, _ => 16 }];

        for (var i = 0; i < Swaps.Length; i++)
        {
            // Move the hidden cube regularly, with decoy swaps between moves.
            var from = i % 2 == 0 ? HiddenSlot : random.Next(count);
            var to = random.Next(count - 1);
            if (to >= from)
                to++;
            Swaps[i] = new Swap(from, to);
            if (HiddenSlot == from)
                HiddenSlot = to;
            else if (HiddenSlot == to)
                HiddenSlot = from;
        }
    }

    public int? Pick(int slot)
    {
        if (Count == 0 || picked || (uint)slot >= Count)
            return null;

        picked = true;
        return slot == HiddenSlot ? Count : 0;
    }
}
