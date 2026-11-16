using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using StepPanel;

var dll = Path.GetFullPath(args[0]);
if (args.Length > 1)
{
    var dalamudPath = Path.GetFullPath(args[1]);
    AssemblyLoadContext.Default.Resolving += (context, name) =>
    {
        var path = Path.Combine(dalamudPath, $"{name.Name}.dll");
        return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
    };
}
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(dll, ".json")));
var metadata = manifest.RootElement;
if (metadata.GetProperty("InternalName").GetString() != assembly.GetName().Name ||
    metadata.GetProperty("AssemblyVersion").GetString() != assembly.GetName().Version?.ToString() ||
    metadata.GetProperty("DalamudApiLevel").GetInt32() != 15)
    throw new Exception("Manifest mismatch");

using var portrait = assembly.GetManifestResourceStream("StepPanel.NixNackle.png")
    ?? throw new Exception("Missing portrait");
Span<byte> pngHeader = stackalloc byte[8];
portrait.ReadExactly(pngHeader);
if (!pngHeader.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
    throw new Exception("Invalid portrait PNG");

var type = assembly.GetType("StepPanel.StepCounter", throwOnError: true)!;
var counter = Activator.CreateInstance(type)!;
var observe = type.GetMethod("Observe")!;
var reset = type.GetMethod("Reset")!;
var breakPath = type.GetMethod("BreakPath")!;
var steps = type.GetProperty("Steps")!;

void Sample(float x, float z = 0, uint territory = 1, double seconds = .1)
    => observe.Invoke(counter, [new Vector2(x, z), territory, seconds]);

void Check(long expected, string scenario)
{
    var actual = (long)steps.GetValue(counter)!;
    if (actual != expected)
        throw new Exception($"{scenario}: expected {expected}, got {actual}");
    Console.WriteLine($"PASS {scenario}");
}

Sample(0);
Sample(0);
Check(0, "stationary");
Sample(.5f);
Check(0, "partial step");
Sample(1);
Check(1, "walk");
Sample(1, 1);
Check(2, "movement along Z");
Sample(100, 100);
Check(2, "same-zone position jump");
Sample(101, 100);
Check(3, "walk after position jump");
Sample(102, 100, territory: 2);
Check(3, "zone change");
breakPath.Invoke(counter, null);
Sample(103, 100, territory: 2);
Check(3, "first sample after excluded movement");
Sample(104, 100, territory: 2, seconds: .6);
Check(3, "stalled update");
Sample(float.NaN);
Sample(1);
Check(3, "invalid coordinates break path");
reset.Invoke(counter, null);
Sample(50);
Sample(50.5f);
Check(0, "reset clears steps, path and remainder");

var outcomes = new HashSet<RoundResult>();
var openers = new HashSet<bool>();
for (var seed = 0; seed < 1000; seed++)
{
    var opponent = new TicTacToe(new Random(seed));
    openers.Add(opponent.NixStarted);
    var playerRandom = new Random(seed + 1000);
    while (opponent.Result == RoundResult.InProgress)
    {
        var empty = Enumerable.Range(0, 9).Where(cell => opponent[cell] == '\0').ToArray();
        var cell = empty[playerRandom.Next(empty.Length)];
        opponent.Play(cell);
        if (opponent[cell] != 'X')
            throw new Exception("Opponent overwrote the player's move");
    }

    outcomes.Add(opponent.Result);
    var board = Enumerable.Range(0, 9).Select(cell => opponent[cell]).ToArray();
    for (var cell = 0; cell < 9; cell++)
    {
        if (opponent.Play(cell) != RoundResult.InProgress || opponent[cell] != board[cell])
            throw new Exception("Tic-Tac-Toe repeated a finished result or changed the board");
    }
    opponent.Reset();
    if (opponent.Result != RoundResult.InProgress ||
        Enumerable.Range(0, 9).Count(cell => opponent[cell] == TicTacToe.Nix) != (opponent.NixStarted ? 1 : 0) ||
        Enumerable.Range(0, 9).Any(cell => opponent[cell] == TicTacToe.Player))
        throw new Exception("Tic-Tac-Toe failed to reset");
}
if (openers.Count != 2)
    throw new Exception("Both Tic-Tac-Toe sides must be able to start");
if (!outcomes.SetEquals([RoundResult.Win, RoundResult.Loss, RoundResult.Draw]))
    throw new Exception("Missing reachable Tic-Tac-Toe result");
Console.WriteLine("PASS Tic-Tac-Toe win/loss/draw, reset, and one-shot result");

for (var seed = 0; seed < 200; seed++)
{
    var foundSolution = false;
    for (var first = 0; first < 7 && !foundSolution; first++)
    for (var second = first + 1; second < 8 && !foundSolution; second++)
    for (var third = second + 1; third < 9 && !foundSolution; third++)
    {
        var puzzle = new Aetherlight(new Random(seed));
        if (Enumerable.Range(0, 9).All(puzzle.IsLit))
            throw new Exception("Aetherlight generated a completed board");
        puzzle.Play(first);
        puzzle.Play(second);
        foundSolution = puzzle.Play(third) == RoundResult.Win;
        if (foundSolution && (puzzle.MovesLeft != 4 || puzzle.Play(first) != RoundResult.InProgress))
            throw new Exception("Aetherlight failed to end the round");
    }
    if (!foundSolution)
        throw new Exception($"Unsolvable Aetherlight board for seed {seed}");
}
var losingPuzzle = new Aetherlight(new Random(42));
for (var i = 0; i < 7; i++)
    losingPuzzle.Play(0);
if (losingPuzzle.Result != RoundResult.Loss || losingPuzzle.MovesLeft != 0)
    throw new Exception("Aetherlight failed to end after seven moves");
Console.WriteLine("PASS Aetherlight 200 solvable boards, move limit, and one-shot result");

foreach (var count in new[] { 3, 5, 7 })
{
    var cups = new CupShuffle(new Random(count));
    for (var round = 0; round < 100; round++)
    {
        cups.Start(count);
        var positions = Enumerable.Range(0, count).ToArray();
        foreach (var swap in cups.Swaps)
            (positions[swap.From], positions[swap.To]) = (positions[swap.To], positions[swap.From]);
        var tracked = Array.IndexOf(positions, cups.InitialSlot);
        if (tracked != cups.HiddenSlot || cups.Pick(tracked) != count || cups.Pick(tracked) is not null)
            throw new Exception("Shell Game lost the cube or awarded twice");
        cups.Start(count);
        if (cups.Pick((cups.HiddenSlot + 1) % count) != 0 || cups.Pick(cups.HiddenSlot) is not null)
            throw new Exception("Wrong cup awarded coins");
    }
}
Console.WriteLine("PASS 3/5/7 cup shuffle tracking and one-shot payouts");

for (var seed = 0; seed < 100; seed++)
{
    var maze = new VaultMaze(new Random(seed));
    maze.Start();
    var cells = Enumerable.Range(0, VaultMaze.Width * VaultMaze.Height).ToArray();
    var seals = cells.Where(maze.HasKey).ToArray();
    if (seals.Length != VaultMaze.KeyCount || seals.Contains(maze.Exit) ||
        maze.Move(-1, -1) || maze.Move(maze.Player % VaultMaze.Width + 2, maze.Player / VaultMaze.Width))
        throw new Exception("Bad maze start or invalid movement");

    List<int> Route(int target)
    {
        var parents = new Dictionary<int, int> { [maze.Player] = -1 };
        var frontier = new Queue<int>();
        frontier.Enqueue(maze.Player);
        while (frontier.TryDequeue(out var cell) && !parents.ContainsKey(target))
        {
            foreach (var next in new[] { cell - 1, cell + 1, cell - VaultMaze.Width, cell + VaultMaze.Width })
                if (maze.IsFloor(next) && parents.TryAdd(next, cell))
                    frontier.Enqueue(next);
        }
        var path = new List<int>();
        for (var cell = target; parents[cell] != -1; cell = parents[cell])
            path.Add(cell);
        path.Reverse();
        return path;
    }

    var rewards = 0;
    foreach (var destination in seals.Append(maze.Exit))
    {
        foreach (var cell in Route(destination))
            if (maze.Move(cell % VaultMaze.Width, cell / VaultMaze.Width))
                rewards++;
        if (maze.Complete)
            break;
    }
    if (rewards != 1 || maze.KeysFound != VaultMaze.KeyCount ||
        maze.Move(maze.Exit % VaultMaze.Width, maze.Exit / VaultMaze.Width))
        throw new Exception("Vault labyrinth was not completable with one reward");
}
Console.WriteLine("PASS connected randomized labyrinths, seals and one-shot exit reward");

var ledgerType = assembly.GetType("StepPanel.CoinLedger", throwOnError: true)!;
var progressType = assembly.GetType("StepPanel.Progress", throwOnError: true)!;
var ledger = JsonSerializer.Deserialize("{\"Version\":1,\"Coins\":31,\"UnlockedArt\":524289}", ledgerType)!;
if ((ulong)ledgerType.GetProperty("UnlockedArt")!.GetValue(ledger)! != 524289UL)
    throw new Exception("Older album bits did not migrate");
var coins = ledgerType.GetProperty("Coins")!;
var addSteps = ledgerType.GetMethod("AddSteps", BindingFlags.Instance | BindingFlags.NonPublic)!;
bool AddSteps(int day, long count) => (bool)addSteps.Invoke(ledger, [day, count])!;
if (AddSteps(10, 9999) || !AddSteps(10, 2) || AddSteps(10, 10000) ||
    (long)coins.GetValue(ledger)! != 131 ||
    !AddSteps(11, 10000) || AddSteps(10, 10000) || (long)coins.GetValue(ledger)! != 231)
    throw new Exception("Incorrect daily threshold, migration, or date rollover");
ledger = JsonSerializer.Deserialize(JsonSerializer.Serialize(ledger, ledgerType), ledgerType)!;
if (AddSteps(11, 10000) || (long)coins.GetValue(ledger)! != 231)
    throw new Exception("Reload granted the daily reward twice");
Console.WriteLine("PASS legacy balance, daily threshold, local date rollover and save/reload");

var pickArt = progressType.GetMethod("PickLockedArt", BindingFlags.Static | BindingFlags.NonPublic)!;
var titles = Enumerable.Range(0, AlbumCatalog.Count).Select(AlbumCatalog.Title).ToArray();
if (titles.Distinct().Count() != AlbumCatalog.Count || titles.Any(title => title.StartsWith("Print ")))
    throw new Exception("Missing title or duplicate album title");
ulong unlocked = 0;
var rng = new Random(42);
for (var i = 0; i < AlbumCatalog.Count; i++)
{
    var index = (int)pickArt.Invoke(null, [unlocked, rng])!;
    if ((unlocked & (1UL << index)) != 0)
        throw new Exception("Duplicate art pull");
    unlocked |= 1UL << index;
    using var print = assembly.GetManifestResourceStream($"StepPanel.Prints.{index:D2}.jpg")
        ?? throw new Exception($"Missing print {index}");
    if (print.ReadByte() != 255 || print.ReadByte() != 216)
        throw new Exception($"Invalid JPEG for print {index}");
}
if (pickArt.Invoke(null, [unlocked, rng]) is not null)
    throw new Exception("A full album still allows pulls");
Console.WriteLine("PASS 57 embedded prints, duplicate protection, and completed album");

var openSeal = ledgerType.GetMethod("OpenSeal", BindingFlags.Instance | BindingFlags.NonPublic)!;
var unlockedArt = ledgerType.GetProperty("UnlockedArt")!;
unlockedArt.SetValue(ledger, 1UL << 56);
ledger = JsonSerializer.Deserialize(JsonSerializer.Serialize(ledger, ledgerType), ledgerType)!;
if ((ulong)unlockedArt.GetValue(ledger)! != 1UL << 56)
    throw new Exception("Upper album bits were lost after reload");
unlockedArt.SetValue(ledger, 0UL);
var sealRandom = new DoorRandom(0, 1, 0);
coins.SetValue(ledger, 110L);
var emptySeal = openSeal.Invoke(ledger, [sealRandom])!;
var artProperty = emptySeal.GetType().GetProperty("Art")!;
if (artProperty.GetValue(emptySeal) is not null ||
    (long)coins.GetValue(ledger)! != 60 || (ulong)unlockedArt.GetValue(ledger)! != 0)
    throw new Exception("Empty seal did not cost full price");
var artSeal = openSeal.Invoke(ledger, [sealRandom])!;
if ((int)artProperty.GetValue(artSeal)! != 0 ||
    (long)coins.GetValue(ledger)! != 10 || (ulong)unlockedArt.GetValue(ledger)! != 1)
    throw new Exception("Incorrect artwork seal result");
if (openSeal.Invoke(ledger, [sealRandom]) is not null)
    throw new Exception("Opened a seal without enough coins");
coins.SetValue(ledger, 100L);
unlockedArt.SetValue(ledger, (1UL << AlbumCatalog.Count) - 1);
if (openSeal.Invoke(ledger, [sealRandom]) is not null || (long)coins.GetValue(ledger)! != 100)
    throw new Exception("A completed album allowed a paid pull");
Console.WriteLine("PASS empty seal, art, full-price pulls and complete album");

if (!new[] { 0, 11, 12, 19, 20, 29, 30, 44, 45 }.Select(ArcadePayout.At)
        .SequenceEqual(new[] { 0, 0, 3, 3, 5, 5, 7, 7, 10 }))
    throw new Exception("Incorrect arcade payout thresholds");

const float ArcadeTick = 1f / 120;
for (var seed = 0; seed < 100; seed++)
{
    var ascent = new AetherAscent(new Random(seed));
    ascent.Start();
    for (var frame = 0; frame < 12_000; frame++)
    {
        var next = ascent.Platforms.First(platform => platform.Level == ascent.Score + 1);
        ascent.Aim(next.X);
        if (ascent.Tick(ArcadeTick))
            break;
    }
    if (ascent.Score != ArcadePayout.FinishScore)
        throw new Exception($"Ascent target unreachable for seed {seed} at {ascent.Score}");

    var courier = new ChocoboCourier(new Random(seed));
    courier.Start();
    for (var frame = 0; frame < 12_000; frame++)
    {
        var next = courier.Crates.FirstOrDefault(crate => crate.X + crate.Width >= ChocoboCourier.RiderX - ChocoboCourier.RiderHalfWidth);
        var speed = 132 + courier.Score * 3.4f;
        var jumpAt = ChocoboCourier.RiderX - next.Width / 2 + speed * .37f;
        if (next.X >= ChocoboCourier.RiderX + ChocoboCourier.RiderHalfWidth && next.X <= jumpAt)
            courier.Jump();
        if (courier.Tick(ArcadeTick))
            break;
    }
    if (courier.Score != ArcadePayout.FinishScore)
        throw new Exception($"Courier target unreachable for seed {seed} at {courier.Score}");
}
var missedJump = new ChocoboCourier(new Random(42));
missedJump.Start();
var crashed = false;
for (var frame = 0; frame < 500 && !crashed; frame++)
    crashed = missedJump.Tick(ArcadeTick);
if (!crashed || missedJump.Score != 0)
    throw new Exception("Courier did not collide with the first crate");
var missedLanding = new AetherAscent(new Random(42));
missedLanding.Start();
missedLanding.Aim(0);
var fell = false;
for (var frame = 0; frame < 500 && !fell; frame++)
    fell = missedLanding.Tick(ArcadeTick);
if (!fell || missedLanding.Score != 0)
    throw new Exception("Ascent did not end after missing all platforms");
Console.WriteLine("PASS arcade payout tiers, failure states, and score 45 in both games on 100 seeds");

var fishing = new FishingCast(new Random(1));
if (fishing.TryCatch())
    throw new Exception("Fishing paid outside the target");
fishing.Advance(.625f);
if (!fishing.TryCatch() || fishing.TryCatch())
    throw new Exception("Fishing did not award one catch inside the target");
fishing.Advance(1.25f);
if (MathF.Abs(fishing.Marker - .5f) > .001f)
    throw new Exception("Fishing marker failed to reverse at the edge");
var fishReward = ledgerType.GetMethod("RewardFishing", BindingFlags.Instance | BindingFlags.NonPublic)!;
int RewardFish(int day, int amount) => (int)fishReward.Invoke(ledger, [day, amount])!;
var beforeFish = (long)coins.GetValue(ledger)!;
for (var i = 0; i < 9; i++)
    if (RewardFish(20, 2) != 2)
        throw new Exception("Unexpected fishing limit");
if (RewardFish(20, 1) != 1 || RewardFish(20, 2) != 1 || RewardFish(20, 2) != 0)
    throw new Exception("Fishing reward exceeded the daily limit");
ledger = JsonSerializer.Deserialize(JsonSerializer.Serialize(ledger, ledgerType), ledgerType)!;
if (RewardFish(20, 1) != 0 || RewardFish(21, 2) != 2 || (long)coins.GetValue(ledger)! != beforeFish + 22)
    throw new Exception("Fishing cap did not persist or roll over");
Console.WriteLine("PASS fishing timing, one payout per cast, daily cap, reload and rollover");

var spa = new RugCycle();
if (RugCycle.WashSeconds != 15)
    throw new Exception("Rug cleaning must last 15 seconds");
var balanceLeft = RugCycle.RugCount + 1;
var spent = 0;
bool Pay(int price)
{
    if (balanceLeft < price)
        return false;
    balanceLeft -= price;
    spent += price;
    return true;
}
if (spa.Start(0, _ => false) || spa.Busy || spa.Running || spa.Rug != 0)
    throw new Exception("Rug Spa started without payment");
if (!spa.Start(0, Pay))
    throw new Exception("Rug Spa refused the first paid rug");
for (var i = 0; i <= RugCycle.RugCount; i++)
{
    var start = i * 16d;
    if (spa.Start(start, Pay) || spa.Rug != i % RugCycle.RugCount ||
        spa.Advance(start + 1) != 1 || spa.Advance(start + 14.9) >= RugCycle.WashSeconds ||
        spa.Advance(start + 15.5) != RugCycle.WashSeconds ||
        spa.Advance(start + 16) != 0 || spa.Busy || spa.Rug != (i + 1) % RugCycle.RugCount)
        throw new Exception("Incorrect Rug Spa cycle");
    spa.Continue(start + 16, false, Pay);
    if (spa.Busy || spent != i + 1)
        throw new Exception("Rug Spa charged for an unloaded rug");
    spa.Continue(start + 16, true, Pay);
    spa.Continue(start + 16, true, Pay);
}
if (spent != RugCycle.RugCount + 1 || spa.Busy || spa.Running || balanceLeft != 0)
    throw new Exception("Rug Spa did not stop when funds ran out");
balanceLeft = 1;
spa.Continue(65, true, Pay);
if (balanceLeft != 1 || spa.Busy)
    throw new Exception("Rug Spa restarted without a new click");
Console.WriteLine("PASS Rug Spa automatic payment, wraparound and stop at zero");

if (!spa.Start(70, Pay))
    throw new Exception("Could not restart Rug Spa after topping up");
spa.Stop(72);
spa.Continue(100, true, Pay);
if (spa.Advance(100) != 2 || spa.Running || !spa.Busy)
    throw new Exception("Stop did not freeze the paid rug");
spa.Resume(100);
if (spa.Advance(101) != 3 || balanceLeft != 0 || spent != RugCycle.RugCount + 2)
    throw new Exception("Resume charged again or did not preserve elapsed time");
Console.WriteLine("PASS Rug Spa stop and free resume");

var errands = new ErrandState();
var errandRandom = new DoorRandom(0, 0, 0);
errands.Advance(599, 0, false, false, false, errandRandom);
if (errands.Active != ErrandKind.None || errands.Wait != 599)
    throw new Exception("Errand appeared early");
errands.Advance(1, 0, false, false, false, errandRandom);
if (errands.Active != ErrandKind.Walk)
    throw new Exception("No errand at ten minutes");
errands.Advance(900, 149, false, false, false, errandRandom);
if (errands.Active != ErrandKind.Walk || errands.Steps != 149)
    throw new Exception("An active errand was replaced");
errands.Enabled = false;
if (errands.Advance(1, 1, true, true, true, errandRandom).Changed || errands.Abandon())
    throw new Exception("Disabled errands changed progress");
errands.Enabled = true;
if (errands.Advance(1, 1, false, false, false, errandRandom).Reward != 1)
    throw new Exception("First errand must pay one coin");
errands.Advance(600, 0, false, false, false, errandRandom);
if (errands.Active != ErrandKind.Greeting ||
    errands.Advance(1, 0, true, false, false, errandRandom).Reward != 2)
    throw new Exception("Emote errand did not pay the streak");
errands.Advance(600, 0, false, false, false, errandRandom);
if (!errands.Abandon() || errands.Streak != 0 || errands.BestStreak != 2 ||
    errands.Completed != 2 || errands.Earned != 3 || errands.Abandoned != 1 || errands.Wait != 0)
    throw new Exception("Errand statistics or abandon cooldown incorrect");
var delivery = new ErrandState { Active = ErrandKind.Delivery };
if (delivery.Advance(1, 0, false, true, false, errandRandom).Reward != 0 ||
    delivery.Advance(1, 0, false, true, true, errandRandom).Reward != 1)
    throw new Exception("Delivery did not require reaching a landmark");
var combo = new ErrandState { Active = ErrandKind.LookBusy };
combo.Advance(1, 50, true, false, false, errandRandom);
combo = JsonSerializer.Deserialize<ErrandState>(JsonSerializer.Serialize(combo))!;
if (!combo.Emoted || combo.Steps != 50 ||
    combo.Advance(1, 50, false, false, false, errandRandom).Reward != 1)
    throw new Exception("Partial quest progress failed to survive reload");
foreach (var kind in new[] { ErrandKind.Greeting, ErrandKind.LookBusy, ErrandKind.Delivery, ErrandKind.FriendlyTell })
{
    var dedicated = new ErrandState { Active = kind };
    if (dedicated.Advance(5, 1000, false, false, false, errandRandom).Reward != 0 || dedicated.Active != kind)
        throw new Exception($"Walking bypassed the objective of {kind}");
}
var tell = new ErrandState { Active = ErrandKind.FriendlyTell, Streak = 4 };
if (tell.Advance(0, 0, true, true, true, errandRandom).Reward != 0 ||
    tell.Advance(0, 0, false, false, false, errandRandom, sentTell: true).Reward != 5 ||
    tell.Advance(0, 0, false, false, false, errandRandom, sentTell: true).Reward != 0)
    throw new Exception("The tell quest awarded incorrectly");
Console.WriteLine("PASS quest timing, single active quest, alternate objectives, streak and reload");

if (!MemoryLevel.Levels.Select(level => level.MistakeLimit).SequenceEqual([8, 9, 12]))
    throw new Exception("Unexpected Memory difficulty limits");
foreach (var level in MemoryLevel.Levels)
{
    var memory = new MemoryRound(new Random(level.Pairs));
    memory.Deal(level);
    var groups = Enumerable.Range(0, memory.Count).GroupBy(memory.Face).Select(g => g.ToArray()).ToArray();
    if (groups.Length != level.Pairs || groups.Any(group => group.Length != 2))
        throw new Exception("Memory deck must contain exactly two of every image");

    void Miss(double time)
    {
        if (memory.Flip(groups[0][0], time) != 0 ||
            memory.Flip(groups[0][0], time) != 0 ||
            memory.Flip(groups[1][0], time) != 0 ||
            !memory.IsRevealed(groups[1][0]))
            throw new Exception("Invalid Memory mismatch handling");
        memory.Update(time + 1);
    }

    for (var i = 0; i < level.MistakeLimit; i++)
        Miss(i * 2);
    if (memory.Lost || memory.Mistakes != level.MistakeLimit)
        throw new Exception("Memory ended before all allowed mistakes were used");

    var reward = 0;
    foreach (var group in groups)
    {
        reward += memory.Flip(group[0], 100);
        reward += memory.Flip(group[1], 100);
    }
    if (!memory.Won || reward != level.Reward || memory.Flip(groups[0][0], 100) != 0)
        throw new Exception("Memory did not pay exactly once after the last allowed mistake");

    memory.Deal(level);
    groups = Enumerable.Range(0, memory.Count).GroupBy(memory.Face).Select(g => g.ToArray()).ToArray();
    for (var i = 0; i <= level.MistakeLimit; i++)
        Miss(i * 2);
    memory.Flip(groups[0][0], 100);
    memory.Flip(groups[0][1], 100);
    if (!memory.Lost || memory.PairCount > 0 || memory.Reward != 0)
        throw new Exception("Memory still rewarded play after going over the limit");

    memory.Deal(level);
    reward = 0;
    foreach (var group in Enumerable.Range(0, memory.Count).GroupBy(memory.Face))
        foreach (var index in group)
            reward += memory.Flip(index, 100);
    if (!memory.Won || reward != level.Reward + 1)
        throw new Exception("Memory did not pay the accuracy bonus");
}
Console.WriteLine("PASS Memory deck sizes, inclusive mistake caps, loss lockout and single payouts");

var experience = new GameExperience();
if (GameLevels.Level(0) != 1 || GameLevels.Threshold(110) != 50_000)
    throw new Exception("Wrong progression endpoints");
for (var level = 2; level <= GameLevels.MaxLevel; level++)
{
    var threshold = GameLevels.Threshold(level);
    if (GameLevels.Level(threshold) != level || GameLevels.Level(threshold - 1) != level - 1)
        throw new Exception("Game XP level threshold failed");
}
for (var round = 1; round <= 500; round++)
{
    if (!experience.RecordRound() ||
        (GameLevels.Level(experience.Total) == GameLevels.MaxLevel) != (round == 500))
        throw new Exception("Unlimited rounds or level 110 threshold failed");
}
if (experience.RecordRound())
    throw new Exception("A maxed minigame still received experience");

var levelsType = ledgerType.GetProperty("GameLevels")!;
var recordRound = ledgerType.GetMethod("RecordRound", BindingFlags.Instance | BindingFlags.NonPublic)!;
var gameType = assembly.GetType("StepPanel.DenGame", throwOnError: true)!;
var fishingGame = Enum.Parse(gameType, "Fishing");
var vaultGame = Enum.Parse(gameType, "Vault");
recordRound.Invoke(ledger, [fishingGame]);
recordRound.Invoke(ledger, [fishingGame]);
recordRound.Invoke(ledger, [vaultGame]);
ledger = JsonSerializer.Deserialize(JsonSerializer.Serialize(ledger, ledgerType), ledgerType)!;
var levels = (System.Collections.IDictionary)levelsType.GetValue(ledger)!;
int Total(object game) => (int)levels[game]!.GetType().GetProperty("Total")!.GetValue(levels[game])!;
if (Total(fishingGame) != 200 || Total(vaultGame) != 100)
    throw new Exception("Independent minigame experience did not survive reload");
var resetLevels = ledgerType.GetMethod("ApplyGameLevelReset", BindingFlags.Instance | BindingFlags.NonPublic)!;
var balanceBeforeReset = (long)coins.GetValue(ledger)!;
var artBeforeReset = (ulong)unlockedArt.GetValue(ledger)!;
if (!(bool)resetLevels.Invoke(ledger, null)! || levels.Count != 0 ||
    (long)coins.GetValue(ledger)! != balanceBeforeReset || (ulong)unlockedArt.GetValue(ledger)! != artBeforeReset)
    throw new Exception("XP reset failed or modified unrelated progress");
recordRound.Invoke(ledger, [fishingGame]);
ledger = JsonSerializer.Deserialize(JsonSerializer.Serialize(ledger, ledgerType), ledgerType)!;
levels = (System.Collections.IDictionary)levelsType.GetValue(ledger)!;
if ((bool)resetLevels.Invoke(ledger, null)! || Total(fishingGame) != 100)
    throw new Exception("XP reset ran again after reload");
Console.WriteLine("PASS 500-round progression, independent profiles and one-time XP reset");

file sealed class DoorRandom(params int[] outcomes) : Random
{
    private int next;
    public override int Next(int maxValue) => outcomes[next++];
}
