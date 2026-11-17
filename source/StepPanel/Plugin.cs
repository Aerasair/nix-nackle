using System;
using System.Numerics;
using System.Threading;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace StepPanel;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/steppanel";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IFramework framework;
    private readonly ICommandManager commands;
    private readonly IClientState clientState;
    private readonly IObjectTable objects;
    private readonly ICondition condition;
    private readonly Backroom backroom;
    private readonly Progress progress;
    private readonly StepCounter counter = new();
    private volatile bool isVisible = true;
    private int resetRequested;
    private int observedDay = -1;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        ICommandManager commands,
        IClientState clientState,
        IObjectTable objects,
        ICondition condition,
        ITextureProvider textures,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.framework = framework;
        this.commands = commands;
        this.clientState = clientState;
        this.objects = objects;
        this.condition = condition;
        progress = new Progress(pluginInterface, log);
        backroom = new Backroom(progress, textures);

        commands.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the step panel. Use /steppanel reset to clear session steps or /steppanel den to open the games.",
        });
        framework.Update += OnUpdate;
        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
    }

    public void Dispose()
    {
        pluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
        pluginInterface.UiBuilder.Draw -= Draw;
        framework.Update -= OnUpdate;
        commands.RemoveHandler(Command);
        progress.Save();
    }

    private void OnCommand(string command, string args)
    {
        var argument = args.Trim();
        if (argument.Length == 0)
            isVisible = !isVisible;
        else if (argument.Equals("reset", StringComparison.OrdinalIgnoreCase))
            Interlocked.Exchange(ref resetRequested, 1);
        else if (argument.Equals("den", StringComparison.OrdinalIgnoreCase))
            backroom.Open();
    }

    private void OpenMainUi()
    {
        isVisible = true;
        backroom.Open();
    }

    private void OnUpdate(IFramework currentFramework)
    {
        if (Interlocked.Exchange(ref resetRequested, 0) == 1)
            counter.Reset();

        var day = Progress.LocalDay;
        if (day != observedDay)
        {
            observedDay = day;
            counter.BreakDay();
        }

        var player = objects.LocalPlayer;
        var seconds = currentFramework.UpdateDelta.TotalSeconds;
        var territory = clientState.TerritoryType;
        long steps = 0;
        if (!clientState.IsLoggedIn || clientState.IsGPosing || IsMovementExcluded() || player is null)
        {
            counter.BreakPath();
        }
        else
        {
            var position = player.Position;
            var before = counter.Steps;
            counter.Observe(new Vector2(position.X, position.Z), territory, seconds);
            steps = counter.Steps - before;
        }

        progress.TrackSteps(day, steps);
    }

    private bool IsMovementExcluded() =>
        condition[ConditionFlag.BetweenAreas] ||
        condition[ConditionFlag.BetweenAreas51] ||
        condition[ConditionFlag.Mounted] ||
        condition[ConditionFlag.RidingPillion] ||
        condition[ConditionFlag.UsingChocoboTaxi] ||
        condition[ConditionFlag.InFlight] ||
        condition[ConditionFlag.Swimming] ||
        condition[ConditionFlag.Diving] ||
        condition[ConditionFlag.Jumping] ||
        condition[ConditionFlag.Jumping61] ||
        condition[ConditionFlag.BeingMoved] ||
        condition[ConditionFlag.OccupiedInCutSceneEvent] ||
        condition[ConditionFlag.WatchingCutscene] ||
        condition[ConditionFlag.WatchingCutscene78];

    private void Draw()
    {
        if (!clientState.IsLoggedIn)
            return;

        if (isVisible)
            DrawStepPanel();
        backroom.Draw();
    }

    private void DrawStepPanel()
    {
        const ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize |
                                       ImGuiWindowFlags.NoCollapse |
                                       ImGuiWindowFlags.NoDocking |
                                       ImGuiWindowFlags.NoFocusOnAppearing;

        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos + new Vector2(40, 160), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowBgAlpha(0.88f);
        var expanded = ImGui.Begin("Steps###StepPanel", flags);
        if (expanded)
        {
            var today = progress.Snapshot().Steps;
            ImGui.TextUnformatted($"{today:N0} today");
            ImGui.TextDisabled(today >= Progress.DailyGoal
                ? $"+{Progress.DailyReward} coins earned"
                : $"Goal {Progress.DailyGoal:N0}");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Session steps: {counter.Steps:N0}");
            if (ImGui.SmallButton("Nix's Den"))
                backroom.Open();
        }
        ImGui.End();
    }
}
