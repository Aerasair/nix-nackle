using System;
using System.Collections.Generic;

namespace StepPanel;

internal sealed class VaultMaze(Random random)
{
    public const int Width = 13;
    public const int Height = 11;
    public const int Reward = 5;
    public const int KeyCount = 2;
    private const int StartCell = Width + 1;
    private static readonly (int X, int Y)[] Directions = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private readonly bool[] floor = new bool[Width * Height];
    private readonly HashSet<int> keys = new();

    public bool Started { get; private set; }
    public bool Complete { get; private set; }
    public int Player { get; private set; }
    public int Exit { get; private set; }
    public int KeysFound => KeyCount - keys.Count;
    public bool IsFloor(int cell) => floor[cell];
    public bool HasKey(int cell) => keys.Contains(cell);

    public void Start()
    {
        Array.Clear(floor);
        keys.Clear();
        Complete = false;
        Started = true;
        Player = StartCell;
        var path = new Stack<int>();
        path.Push(StartCell);
        floor[StartCell] = true;
        Span<int> choices = stackalloc int[4];
        while (path.TryPeek(out var cell))
        {
            var count = 0;
            foreach (var (dx, dy) in Directions)
            {
                var x = cell % Width + 2 * dx;
                var y = cell / Width + 2 * dy;
                if (x > 0 && x < Width - 1 && y > 0 && y < Height - 1 && !floor[y * Width + x])
                    choices[count++] = y * Width + x;
            }
            if (count == 0)
            {
                path.Pop();
                continue;
            }
            var next = choices[random.Next(count)];
            floor[(cell + next) / 2] = floor[next] = true;
            path.Push(next);
        }

        var visited = new bool[floor.Length];
        var queue = new Queue<int>();
        var rooms = new List<int>();
        queue.Enqueue(StartCell);
        visited[StartCell] = true;
        while (queue.TryDequeue(out var cell))
        {
            Exit = cell;
            if (cell != StartCell && cell % Width % 2 == 1 && cell / Width % 2 == 1)
                rooms.Add(cell);
            foreach (var (dx, dy) in Directions)
            {
                var next = cell + dx + dy * Width;
                if (floor[next] && !visited[next])
                {
                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }
        }
        rooms.Remove(Exit);
        for (var n = 0; n < KeyCount; n++)
        {
            var index = random.Next(rooms.Count);
            keys.Add(rooms[index]);
            rooms.RemoveAt(index);
        }
    }

    public bool Move(int x, int y)
    {
        if (!Started || Complete || (uint)x >= Width || (uint)y >= Height)
            return false;

        var target = y * Width + x;
        if (!floor[target] || Math.Abs(x - Player % Width) + Math.Abs(y - Player / Width) != 1)
            return false;

        Player = target;
        keys.Remove(target);
        Complete = target == Exit && keys.Count == 0;
        return Complete;
    }
}
