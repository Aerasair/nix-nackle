using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace StepPanel;

internal sealed class FishingSpot(Progress progress, NixArt artwork)
{
    private readonly FishingCast cast = new(Random.Shared);
    private string? feedback;
    private double feedbackUntil;

    public void Draw(float unit)
    {
        ImGui.TextUnformatted("Nix's Fishing Spot");
        ImGui.TextWrapped("Click anywhere on the pier scene when the marker reaches the green zone.");
        var earned = progress.FishingEarnedToday();
        ImGui.TextDisabled($"{earned} / {Progress.FishingDailyLimit} coins earned today");

        var now = ImGui.GetTime();
        var canFish = !progress.Snapshot().SaveFailed;
        if (canFish)
            cast.Advance(Math.Clamp(ImGui.GetIO().DeltaTime, 0, .05f));

        if (DrawPier(unit, (float)now, canFish))
        {
            feedback = cast.TryCatch()
                ? (progress.RewardFishing(1) == 1 ? "Fish caught! +1 coin" : "Fish caught!")
                : "Missed! Try the next pass.";
            feedbackUntil = now + 1.3;
        }
        if (feedback is not null && now < feedbackUntil)
            ImGui.TextUnformatted(feedback);
    }

    private bool DrawPier(float unit, float time, bool enabled)
    {
        var p = ImGui.GetCursorScreenPos();
        var w = MathF.Max(1, MathF.Min(ImGui.GetContentRegionAvail().X, 27 * unit));
        var h = 9 * unit;
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(p, p + new Vector2(w, h), true);
        draw.AddRectFilled(p, p + new Vector2(w, h), 0xff685244, .35f * unit);
        artwork.Stretch(draw, "harbor.jpg", p, p + new Vector2(w, h));
        var spriteWidth = MathF.Min(w * .35f, 5.6f * unit);
        var fisher = p + new Vector2(w * .22f, .72f * h);
        var fisherSize = artwork.Sprite(draw, "fisher.png", fisher, new Vector2(spriteWidth, .58f * h), bottom: true);
        var rod = fisher + new Vector2(fisherSize.X * .47f, -fisherSize.Y * .97f);
        var bobber = p + new Vector2(w * (.40f + .45f * cast.Marker), h * .66f + unit * MathF.Sin(time * 5) * .12f);
        draw.AddLine(rod, bobber, 0x99e6e7ef);
        draw.AddCircleFilled(bobber, .29f * unit, 0xff6889f9);

        var track = p + new Vector2(w * .09f, h * .83f);
        var span = w * .82f;
        draw.AddRectFilled(track, track + new Vector2(span, .57f * unit), 0xff241d17, .12f * unit);
        draw.AddRectFilled(track + new Vector2(span * (cast.Target - FishingCast.TargetHalfWidth), 0),
            track + new Vector2(span * (cast.Target + FishingCast.TargetHalfWidth), .57f * unit), 0xff79d76e);
        var marker = track + new Vector2(span * cast.Marker, 0);
        draw.AddLine(marker - new Vector2(0, .18f * unit), marker + new Vector2(0, .75f * unit), 0xffbdeaff, 3);
        draw.PopClipRect();
        ImGui.InvisibleButton("Fishing scene", new Vector2(w, h));
        return enabled && ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
