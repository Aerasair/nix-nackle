using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace StepPanel;

internal sealed class ArtCollection(Progress progress, ITextureProvider textures)
{
    private const double RevealSeconds = 1.4;
    private SealResult? lastPull;
    private double revealAt;
    private bool albumOpen;
    private bool focusAlbum;
    private int? selected;

    private bool Revealing => ImGui.GetTime() < revealAt;

    public void OpenAlbum()
    {
        albumOpen = true;
        focusAlbum = true;
    }

    public void DrawGacha(float unit)
    {
        var state = progress.Snapshot();
        var full = (state.Art & Progress.CompleteAlbum) == Progress.CompleteAlbum;
        ImGui.TextUnformatted("Nix's Sealed Prints");
        ImGui.TextWrapped($"A seal costs {Progress.PullPrice} Nackle Coins. 50% chance of a new print, 50% chance of an empty seal.");
        ImGui.BeginDisabled(Revealing || full || state.SaveFailed || state.Coins < Progress.PullPrice);
        if (ImGui.Button($"Break a seal ({Progress.PullPrice} coins)") && progress.Pull() is { } result)
        {
            lastPull = result;
            revealAt = ImGui.GetTime() + RevealSeconds;
        }
        ImGui.EndDisabled();

        if (Revealing)
        {
            var elapsed = (float)(1 - (revealAt - ImGui.GetTime()) / RevealSeconds);
            DrawSeal(unit, elapsed);
            ImGui.ProgressBar(elapsed, new Vector2(14 * unit, 0), "Breaking the seal...");
            return;
        }

        if (full)
            ImGui.TextUnformatted("Album complete.");
        else if (state.Coins < Progress.PullPrice)
            ImGui.TextWrapped("Earn coins from the daily step goal or games in the Den.");

        if (lastPull is { Art: { } revealed })
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("New print unlocked!");
            ImGui.TextWrapped(AlbumCatalog.Title(revealed));
            DrawArt(revealed, MathF.Min(ImGui.GetContentRegionAvail().X, 16 * unit));
            if (ImGui.Button("View in album"))
            {
                selected = revealed;
                OpenAlbum();
            }
        }
        else if (lastPull.HasValue)
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("The seal was empty.");
        }
    }

    public void DrawAlbum()
    {
        if (!albumOpen)
            return;

        if (focusAlbum)
        {
            ImGui.SetNextWindowFocus();
            focusAlbum = false;
        }

        var unit = ImGui.GetFontSize();
        ImGui.SetNextWindowSize(new Vector2(33, 39) * unit, ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Nix's Album###StepPanelAlbum", ref albumOpen, ImGuiWindowFlags.NoDocking))
            DrawAlbumContents(unit);
        ImGui.End();
    }

    private void DrawAlbumContents(float unit)
    {
        var art = progress.Snapshot().Art & Progress.CompleteAlbum;
        if (Revealing && lastPull is { Art: { } hidden })
            art &= ~(1UL << hidden);

        ImGui.TextUnformatted($"{BitOperations.PopCount(art)} / {Progress.ArtCount} collected");
        if (art == 0)
        {
            ImGui.TextWrapped("Open a seal in the Gacha tab to add your first print.");
            return;
        }

        if (selected is { } selection && (art & (1UL << selection)) != 0)
        {
            if (ImGui.Button("Back to album"))
                selected = null;
            else
            {
                ImGui.TextWrapped(AlbumCatalog.Title(selection));
                DrawArt(selection, MathF.Min(ImGui.GetContentRegionAvail().X, 32 * unit));
            }
            return;
        }

        var tile = 6.5f * unit;
        var columns = Math.Max(1, (int)((ImGui.GetContentRegionAvail().X + ImGui.GetStyle().ItemSpacing.X) /
                                       (tile + ImGui.GetStyle().ItemSpacing.X + 2 * ImGui.GetStyle().FramePadding.X)));
        var shown = 0;
        for (var i = 0; i < Progress.ArtCount; i++)
        {
            if ((art & (1UL << i)) == 0)
                continue;

            if (shown++ % columns != 0)
                ImGui.SameLine();

            var texture = textures.GetFromManifestResource(typeof(ArtCollection).Assembly, Resource(i));
            ImGui.PushID(i);
            var clicked = texture.TryGetWrap(out var wrap, out _)
                ? ImGui.ImageButton(wrap.Handle, new Vector2(tile))
                : ImGui.Button(AlbumCatalog.Title(i), new Vector2(tile));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(AlbumCatalog.Title(i));
            ImGui.PopID();
            if (clicked)
                selected = i;
        }
    }

    private void DrawArt(int index, float side)
    {
        if (side <= 0)
            return;

        var texture = textures.GetFromManifestResource(typeof(ArtCollection).Assembly, Resource(index));
        if (texture.TryGetWrap(out var wrap, out var error))
            ImGui.Image(wrap.Handle, new Vector2(side));
        else
            ImGui.TextDisabled(error is null ? "Loading print..." : "Could not load this print.");
    }

    private static void DrawSeal(float unit, float elapsed)
    {
        var center = ImGui.GetCursorScreenPos() + new Vector2(7 * unit, 3.5f * unit);
        var radius = (1.5f + elapsed * .6f) * unit;
        var width = (.22f + .78f * MathF.Abs(MathF.Cos(elapsed * 3 * MathF.PI))) * radius;
        var gold = ImGui.GetColorU32(new Vector4(.97f, .77f, .43f, 1));
        var ink = ImGui.GetColorU32(new Vector4(.25f, .14f, .10f, 1));
        var draw = ImGui.GetWindowDrawList();
        var top = center - new Vector2(0, radius);
        var right = center + new Vector2(width, 0);
        var bottom = center + new Vector2(0, radius);
        var left = center - new Vector2(width, 0);
        draw.AddQuadFilled(top, right, bottom, left, ink);
        draw.AddQuad(top, right, bottom, left, gold, 2);
        draw.AddCircleFilled(center, unit * (.15f + elapsed * .25f), gold);
        ImGui.Dummy(new Vector2(14 * unit, 7 * unit));
    }

    private static string Resource(int index) => $"StepPanel.Prints.{index:D2}.jpg";
}
