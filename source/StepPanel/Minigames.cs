using System;

namespace StepPanel;

internal enum RoundResult
{
    InProgress,
    Win,
    Loss,
    Draw,
}

internal sealed class TicTacToe
{
    public const char Player = 'X';
    public const char Nix = 'O';
    private readonly Random random;
    private static readonly int[][] Lines =
    [
        [0, 1, 2], [3, 4, 5], [6, 7, 8],
        [0, 3, 6], [1, 4, 7], [2, 5, 8],
        [0, 4, 8], [2, 4, 6],
    ];

    private readonly char[] board = new char[9];

    public RoundResult Result { get; private set; }
    public bool NixStarted { get; private set; }

    public TicTacToe(Random random)
    {
        this.random = random;
        Reset();
    }

    public char this[int cell] => board[cell];

    public RoundResult Play(int cell)
    {
        if (Result != RoundResult.InProgress || board[cell] != '\0')
            return RoundResult.InProgress;

        board[cell] = Player;
        if (HasLine(Player))
            return Result = RoundResult.Win;

        Span<int> emptyCells = stackalloc int[9];
        var count = 0;
        for (var i = 0; i < board.Length; i++)
        {
            if (board[i] == '\0')
                emptyCells[count++] = i;
        }

        if (count == 0)
            return Result = RoundResult.Draw;

        var candidates = emptyCells[..count];
        var reply = FindFinishingMove(candidates, Nix) ??
                    FindFinishingMove(candidates, Player) ??
                    candidates[random.Next(count)];
        board[reply] = Nix;
        if (HasLine(Nix))
            Result = RoundResult.Loss;
        else if (count == 1)
            Result = RoundResult.Draw;
        return Result;
    }

    public void Reset()
    {
        Array.Clear(board);
        Result = RoundResult.InProgress;
        NixStarted = random.Next(2) == 0;
        if (NixStarted)
            board[random.Next(board.Length)] = Nix;
    }

    private int? FindFinishingMove(ReadOnlySpan<int> candidates, char mark)
    {
        foreach (var cell in candidates)
        {
            board[cell] = mark;
            var wins = HasLine(mark);
            board[cell] = '\0';
            if (wins)
                return cell;
        }

        return null;
    }

    private bool HasLine(char mark)
    {
        foreach (var line in Lines)
        {
            if (board[line[0]] == mark && board[line[1]] == mark && board[line[2]] == mark)
                return true;
        }

        return false;
    }
}

internal sealed class Aetherlight
{
    private const int AllLit = (1 << 9) - 1;
    private readonly Random random;
    private int lights;

    public Aetherlight(Random random)
    {
        this.random = random;
        Reset();
    }

    public int MovesLeft { get; private set; }
    public RoundResult Result { get; private set; }

    public bool IsLit(int cell) => (lights & (1 << cell)) != 0;

    public RoundResult Play(int cell)
    {
        if (Result != RoundResult.InProgress)
            return RoundResult.InProgress;

        lights ^= ToggleMask(cell);
        MovesLeft--;
        if (lights == AllLit)
            Result = RoundResult.Win;
        else if (MovesLeft == 0)
            Result = RoundResult.Loss;
        return Result;
    }

    public void Reset()
    {
        Span<int> cells = stackalloc int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
        do
        {
            lights = AllLit;
            random.Shuffle(cells);
            foreach (var cell in cells[..3])
                lights ^= ToggleMask(cell);
        } while (lights == AllLit);

        MovesLeft = 7;
        Result = RoundResult.InProgress;
    }

    private static int ToggleMask(int cell)
    {
        var mask = 1 << cell;
        if (cell >= 3)
            mask |= 1 << (cell - 3);
        if (cell < 6)
            mask |= 1 << (cell + 3);
        if (cell % 3 != 0)
            mask |= 1 << (cell - 1);
        if (cell % 3 != 2)
            mask |= 1 << (cell + 1);
        return mask;
    }
}
