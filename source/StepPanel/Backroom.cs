using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace StepPanel;

internal sealed class Backroom
{
    private const string Portrait = "StepPanel.NixNackle.png";
    private const int TicTacToeReward = 3;
    private const int AetherlightReward = 5;

    private static readonly Vector4 Gold = new(0.97f, 0.77f, 0.43f, 1);
    private static readonly Vector4 Jade = new(0.44f, 0.91f, 0.79f, 1);
    private static readonly Vector4 Ink = new(0.08f, 0.13f, 0.15f, 0.97f);
    private static readonly Vector4 Tile = new(0.15f, 0.23f, 0.24f, 1);
    private static readonly Vector4 Hover = new(0.20f, 0.35f, 0.34f, 1);
    private static readonly Vector4 LitTile = new(0.17f, 0.39f, 0.33f, 1);

    private readonly Progress progress;
    private readonly ITextureProvider textures;
    private readonly NixArt artwork;
    private readonly ArtCollection collection;
    private readonly Arcade ascent;
    private readonly Arcade courier;
    private readonly FishingSpot fishing;
    private readonly TicTacToe ticTacToe = new(Random.Shared);
    private readonly Aetherlight aetherlight = new(Random.Shared);
    private readonly VaultMazeView vault;
    private readonly ShellGame shellGame;
    private readonly MemoryCards memory;
    private readonly RugSpa rugSpa;
    private volatile bool openRequested;
    private bool isOpen;
    private int selectedTab;
    private static readonly string[] Tabs =
        ["Profile", "Gacha", "Tic-Tac-Toe", "Aetherlight", "Shell Game", "Vault", "Aether Ascent", "Chocobo Courier", "Fishing", "Memory", "Rug Spa"];

    public Backroom(Progress progress, ITextureProvider textures)
    {
        this.progress = progress;
        this.textures = textures;
        artwork = new NixArt(textures);
        vault = new VaultMazeView(progress, artwork);
        collection = new ArtCollection(progress, textures);
        ascent = new Arcade(progress, artwork, isCourier: false);
        courier = new Arcade(progress, artwork, isCourier: true);
        fishing = new FishingSpot(progress, artwork);
        memory = new MemoryCards(progress, artwork);
        rugSpa = new RugSpa(progress, artwork);
        shellGame = new ShellGame(progress, artwork);
    }

    public void Open() => openRequested = true;

    public void Draw()
    {
        DrawDen();
        collection.DrawAlbum();
    }

    private void DrawDen()
    {
        if (openRequested)
        {
            openRequested = false;
            isOpen = true;
            ImGui.SetNextWindowFocus();
        }

        if (!isOpen)
            return;

        var unit = ImGui.GetFontSize();
        ImGui.SetNextWindowSize(new Vector2(46, 32) * unit, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(22, 14) * unit, new Vector2(float.MaxValue));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Ink);
        ImGui.PushStyleColor(ImGuiCol.Button, Tile);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Hover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, LitTile);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(.95f, .8f) * unit);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, .25f * unit);
        var expanded = ImGui.Begin("Nix's Den###StepPanelDen", ref isOpen,
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking);
        if (expanded)
        {
            DrawHost(unit);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawTabs(unit);
        }
        ImGui.End();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(4);
    }

    private void DrawTabs(float unit)
    {
        var available = ImGui.GetContentRegionAvail().X;
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var padding = ImGui.GetStyle().FramePadding.X * 2;
        var rowWidth = 0f;
        for (var tab = 0; tab < Tabs.Length; tab++)
        {
            var width = MathF.Min(available, MathF.Ceiling(ImGui.CalcTextSize(Tabs[tab]).X + padding));
            if (tab == 8)
                rowWidth = 0;
            if (rowWidth > 0 && rowWidth + gap + width <= available)
            {
                ImGui.SameLine();
                rowWidth += gap;
            }
            else
            {
                rowWidth = 0;
            }

            if (selectedTab == tab)
                ImGui.PushStyleColor(ImGuiCol.Button, LitTile);
            var clicked = ImGui.Button(Tabs[tab], new Vector2(width, 0));
            if (selectedTab == tab)
                ImGui.PopStyleColor();
            if (clicked)
                selectedTab = tab;
            rowWidth += width;
        }

        ImGui.Separator();
        ImGui.Spacing();
        switch (selectedTab)
        {
            case 0: ProfileView.Draw(progress, artwork); break;
            case 1: collection.DrawGacha(unit); break;
            case 2: DrawTicTacToe(unit); break;
            case 3: DrawAetherlight(unit); break;
            case 4: shellGame.Draw(unit); break;
            case 5: vault.Draw(unit); break;
            case 6: ascent.Draw(unit); break;
            case 7: courier.Draw(unit); break;
            case 8: fishing.Draw(unit); break;
            case 9: memory.Draw(unit); break;
            case 10: rugSpa.Draw(unit); break;
        }
    }

    private void DrawHost(float unit)
    {
        var size = new Vector2(6.5f * unit);
        var portrait = textures.GetFromManifestResource(typeof(Backroom).Assembly, Portrait);
        if (portrait.TryGetWrap(out var wrap, out _))
            ImGui.Image(wrap.Handle, size);
        else
            ImGui.Dummy(size);

        ImGui.SameLine(0, .85f * unit);
        ImGui.BeginGroup();
        ImGui.TextColored(Gold, "Nix Nackle");
        ImGui.TextDisabled("Ul'dahn back-alley dealer");
        ImGui.TextWrapped("\"The duty bell can wait. One quick game?\"");
        ImGui.Spacing();
        var balance = progress.Snapshot();
        ImGui.TextColored(Gold, $"{balance.Coins:N0} Nackle Coins");
        if (ImGui.SmallButton("Open album"))
            collection.OpenAlbum();
        if (balance.SaveFailed)
        {
            ImGui.TextWrapped("Coin save failed. Retry to save your progress.");
            if (ImGui.SmallButton("Retry save"))
                progress.Save();
        }
        ImGui.EndGroup();
    }

    private void DrawTicTacToe(float unit)
    {
        var opener = ticTacToe.NixStarted ? "Nix went first." : "You went first.";
        ImGui.TextWrapped($"You play the light crystal. Nix uses his portrait. {opener} Win to earn {TicTacToeReward} Nackle Coins.");
        ImGui.Spacing();

        for (var cell = 0; cell < 9; cell++)
        {
            if (cell % 3 != 0)
                ImGui.SameLine();

            var mark = ticTacToe[cell];
            var available = mark == '\0' && ticTacToe.Result == RoundResult.InProgress;
            ImGui.PushID(cell);
            var clicked = ImGui.Button("###Square", new Vector2(3.4f * unit));
            var center = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
            if (mark == TicTacToe.Nix)
                artwork.Sprite(ImGui.GetWindowDrawList(), "face.png", center, new Vector2(2.75f * unit));
            else if (mark == TicTacToe.Player)
                artwork.Sprite(ImGui.GetWindowDrawList(), "crystal.png", center, new Vector2(2.55f * unit));
            ImGui.PopID();
            if (clicked && available && ticTacToe.Play(cell) is not RoundResult.InProgress and var result)
                progress.FinishGame(DenGame.TicTacToe, result == RoundResult.Win ? TicTacToeReward : 0);
        }

        DrawResult(ticTacToe.Result, "Your move.", "Nix wins this round.", TicTacToeReward);
        if (ImGui.Button("New match"))
            ticTacToe.Reset();
    }

    private void DrawAetherlight(float unit)
    {
        var art = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(4.3f, 4.1f) * unit);
        artwork.Sprite(ImGui.GetWindowDrawList(), "lantern.png", art + new Vector2(2.15f, 2.05f) * unit, new Vector2(4.1f * unit));
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextWrapped("Light all nine crystals. Each tap flips that crystal and its neighbors up, down, left and right.");
        ImGui.TextDisabled($"Reward for a clear: {AetherlightReward} Nackle Coins");
        ImGui.EndGroup();
        ImGui.Spacing();

        for (var cell = 0; cell < 9; cell++)
        {
            if (cell % 3 != 0)
                ImGui.SameLine();

            var lit = aetherlight.IsLit(cell);
            ImGui.PushID(cell);
            ImGui.PushStyleColor(ImGuiCol.Button, lit ? LitTile : Tile);
            var clicked = ImGui.Button("###Crystal", new Vector2(3.4f * unit));
            artwork.Sprite(ImGui.GetWindowDrawList(), "crystal.png", (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2,
                new Vector2(2.55f * unit), tint: lit ? uint.MaxValue : 0xff777777);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(lit ? "Lit" : "Dark");
            ImGui.PopStyleColor();
            ImGui.PopID();
            if (clicked && aetherlight.Play(cell) is not RoundResult.InProgress and var result)
                progress.FinishGame(DenGame.Aetherlight, result == RoundResult.Win ? AetherlightReward : 0);
        }

        DrawResult(aetherlight.Result, $"Moves left: {aetherlight.MovesLeft}", "Out of moves.", AetherlightReward);
        if (ImGui.Button("New puzzle"))
            aetherlight.Reset();
    }

    private static void DrawResult(RoundResult result, string playing, string loss, int reward)
    {
        ImGui.Spacing();
        if (result == RoundResult.Win)
        {
            ImGui.TextColored(Gold, $"You win! +{reward} Nackle Coins");
            return;
        }

        ImGui.TextUnformatted(result switch
        {
            RoundResult.Loss => loss,
            RoundResult.Draw => "A draw.",
            _ => playing,
        });
    }

}
