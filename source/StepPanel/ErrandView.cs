using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace StepPanel;

internal sealed class ErrandView(Progress progress)
{
    private ErrandKind lastAlert;
    private bool alerted;
    private double alertAt;

    public void Draw()
    {
        var state = progress.ErrandSnapshot();
        var enabled = state.Enabled;
        if (ImGui.Checkbox("Enable Nix's Errands", ref enabled))
            progress.SetErrandsEnabled(enabled);

        ImGui.Spacing();
        if (!enabled)
            ImGui.TextWrapped("Errands paused. Your progress and streak are saved.");
        else if (state.Active == ErrandKind.None)
        {
            var remaining = TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0, Errands.Interval - state.Wait)));
            ImGui.TextUnformatted("Next errand: " + remaining.ToString(@"mm\:ss"));
            ImGui.TextDisabled("Time counts while you are outside duties.");
        }
        else
        {
            ImGui.TextUnformatted(Errands.Name(state.Active));
            ImGui.TextWrapped(Errands.Objective(state.Active));
            if (state.Active == ErrandKind.FriendlyTell)
            {
                ImGui.TextDisabled("Ideas for your /tell");
                ImGui.Bullet();
                ImGui.TextWrapped("Good luck with your next duty!");
                ImGui.Bullet();
                ImGui.TextWrapped("Nix says good luck. Somehow I'm the one getting billed for it.");
            }
            var goal = Errands.StepGoal(state.Active);
            if (goal > 0)
                ImGui.TextUnformatted($"Steps: {Math.Min(state.Steps, goal)} / {goal}");
            if (Errands.UsesEmote(state.Active))
                ImGui.TextDisabled(state.Emoted ? "Emote done" : "Emote pending");
            ImGui.TextUnformatted($"Reward: {state.Streak + 1:N0} Nackle Coins");
            if (ImGui.SmallButton("Abandon errand"))
                ImGui.OpenPopup("Abandon errand?");
            if (ImGui.BeginPopup("Abandon errand?"))
            {
                ImGui.TextUnformatted("Abandon this errand and reset your streak?");
                if (ImGui.Button("Abandon"))
                {
                    progress.AbandonErrand();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Keep errand"))
                    ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted($"Completed: {state.Completed:N0}    Coins earned: {state.Earned:N0}");
        ImGui.TextUnformatted($"Current streak: {state.Streak:N0}    Best: {state.BestStreak:N0}");
        ImGui.TextDisabled($"Abandoned: {state.Abandoned:N0}");
    }

    public bool DrawAlert(Vector2 anchor, Vector2 panelSize, bool outsideDuty)
    {
        var state = progress.ErrandSnapshot();
        if (lastAlert != state.Active)
        {
            lastAlert = state.Active;
            alerted = false;
            alertAt = ImGui.GetTime();
        }

        if (!outsideDuty || !state.Enabled || state.Active == ErrandKind.None || alerted)
            return false;

        var phase = ImGui.GetTime() - alertAt;
        var pulse = phase < 12 ? (float)(.5 + .5 * Math.Cos(phase * 7)) : 0;
        var width = ImGui.CalcTextSize("New quest").X + 42;
        var viewport = ImGui.GetMainViewport();
        var x = MathF.Min(anchor.X + panelSize.X + 7, viewport.WorkPos.X + viewport.WorkSize.X - width);
        if (x < anchor.X + panelSize.X)
            x = MathF.Max(viewport.WorkPos.X, anchor.X - width);
        ImGui.SetNextWindowPos(new Vector2(x, anchor.Y + ImGui.GetFrameHeight()), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(.94f);
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove |
                                       ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings |
                                       ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoDocking;
        if (ImGui.Begin("###StepPanelQuestAlert", flags))
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.30f + .34f * pulse, .21f + .23f * pulse, .07f, 1));
            alerted = ImGui.SmallButton("New quest");
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Errands.Name(state.Active));
        }
        ImGui.End();
        return alerted;
    }
}
