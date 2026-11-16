using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace StepPanel;

internal sealed class RugSpa(Progress progress, NixArt art)
{
    private const string Workshop = "rug-workshop.jpg";
    private const string Brush = "rug-brush.png";
    private readonly RugCycle cycle = new();

    private bool Pay(int coins) => progress.ChangeCoins(-coins);

    public void Draw(float unit)
    {
        var now = ImGui.GetTime();
        var wasBusy = cycle.Busy;
        var elapsed = cycle.Advance(now);
        if (wasBusy && !cycle.Busy)
            progress.FinishGame(DenGame.RugSpa);
        var clean = $"rug-{cycle.Rug + 1}.png";
        var dirty = $"rug-{cycle.Rug + 1}-dirty.png";
        var ready = art.IsReady(Workshop) & art.IsReady(Brush) & art.IsReady(clean) & art.IsReady(dirty);
        DrawScene(unit, elapsed, clean, dirty);

        var balance = progress.Snapshot();
        ImGui.TextDisabled("Washes the next rug automatically for 1 coin.");
        if (cycle.Running)
        {
            if (ImGui.Button("Stop"))
                cycle.Stop(now);
        }
        else if (cycle.Busy)
        {
            if (ImGui.Button("Resume"))
                cycle.Resume(now);
        }
        else
        {
            ImGui.BeginDisabled(!ready || balance.SaveFailed || balance.Coins < RugCycle.Price);
            if (ImGui.Button("Start washing - 1 coin per rug"))
                cycle.Start(now, Pay);
            ImGui.EndDisabled();
        }

        if (cycle.Busy)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(!cycle.Running ? "Paused." : elapsed < RugCycle.WashSeconds ? "Brushing..." : "Clean.");
        }
        else if (balance.Coins < RugCycle.Price)
        {
            ImGui.TextDisabled("You need 1 Nackle Coin.");
        }
        cycle.Continue(now, ready, Pay);
    }

    private void DrawScene(float unit, double elapsed, string clean, string dirty)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1, MathF.Min(ImGui.GetContentRegionAvail().X, 44 * unit));
        var size = new Vector2(width, width * 2 / 3);
        ImGui.Dummy(size);

        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(origin, origin + size, true);
        draw.AddRectFilled(origin, origin + size, 0xff29211b);
        art.Stretch(draw, Workshop, origin, origin + size);

        var min = origin + size * new Vector2(.17f, .08f);
        var max = origin + size * new Vector2(.64f, .92f);
        art.Stretch(draw, dirty, min, max);

        if (cycle.Busy)
            DrawPass(draw, clean, min, max, elapsed);

        draw.PopClipRect();
    }

    private void DrawPass(ImDrawListPtr draw, string clean, Vector2 min, Vector2 max, double elapsed)
    {
        var pass = Math.Min((int)(elapsed / RugCycle.PassSeconds), RugCycle.Passes);
        if (pass == RugCycle.Passes)
        {
            art.Stretch(draw, clean, min, max);
            return;
        }

        var height = (max.Y - min.Y) / RugCycle.Passes;
        var y = min.Y + height * pass;
        if (pass > 0)
            Reveal(draw, clean, min, max, min, new Vector2(max.X, y));

        var phase = Math.Clamp((float)(elapsed / RugCycle.PassSeconds - pass), 0, 1);
        var turning = pass < RugCycle.Passes - 1;
        var sweep = turning ? MathF.Min(1, phase / .86f) : phase;
        var stepDown = turning ? Math.Clamp((phase - .86f) / .14f, 0, 1) : 0;
        var forward = (pass & 1) == 0;
        var x = float.Lerp(min.X, max.X, forward ? sweep : 1 - sweep);
        var band = new Vector2(forward ? min.X : x, y);
        var end = new Vector2(forward ? x : max.X, y + height);
        if (end.X > band.X)
            Reveal(draw, clean, min, max, band, end);

        var brush = new Vector2(x, y + height * (.5f + stepDown + MathF.Sin(sweep * 8 * MathF.PI) * .06f));
        DrawDust(draw, brush, height, phase, forward ? -1 : 1);
        art.Sprite(draw, Brush, brush, new Vector2(1.6f, 1.16f) * height);
    }

    private void Reveal(ImDrawListPtr draw, string clean, Vector2 min, Vector2 max, Vector2 clipMin, Vector2 clipMax)
    {
        draw.PushClipRect(clipMin, clipMax, true);
        art.Stretch(draw, clean, min, max);
        draw.PopClipRect();
    }

    private static void DrawDust(ImDrawListPtr draw, Vector2 brush, float size, float phase, int direction)
    {
        for (var i = 0; i < 7; i++)
        {
            var age = (phase * 4 + i / 7f) % 1;
            var position = brush + new Vector2(direction * (.32f + age * .94f),
                (i % 3 - 1) * .25f - age * .24f) * size;
            var alpha = (uint)((1 - age) * (i == 0 ? 205 : 105));
            var color = (alpha << 24) | (i == 0 ? 0x0075caffu : 0x0085a0b9u);
            draw.AddCircleFilled(position, size * (.018f + (i % 3) * .013f), color);
        }
    }
}
