# Nix Nackle

Nix Nackle is a small **Final Fantasy XIV** plugin for Dalamud. It adds a movable step tracker and opens **Nix’s Den**, a backroom run by a suspicious Lalafell merchant. Dalamud lists the plugin as **Step Panel**.

- **Daily steps.** Earn 100 Nackle Coins for reaching 10,000 steps in a local calendar day.
- **Games for the duty queue.** Play Tic-Tac-Toe, Aetherlight, Shell Game, the Sil’dihn Labyrinth, Aether Ascent, Chocobo Courier, Fishing, or Memory.
- **Spend coins.** Open a seal for a chance at an illustration, or set Nix’s brush to work in Rug Spa. The album contains 57 illustrations. Use the arrows above an open print to move between unlocked illustrations.
- **Profile.** Each minigame has a separate level up to 110. Earn 100 XP per coin won in that game. Rug Spa grants 100 XP per paid rug after cleaning finishes.

[addon/](addon/) contains the ready-to-use plugin files. [source/](source/) contains the C# projects, artwork, and regression checks.

## Install in Dalamud

Version **0.11.2** targets **Dalamud API 15**.

1. Download this repository via **Code → Download ZIP**, then extract it to a folder you plan to keep. To share the plugin, send the whole `addon` folder.
2. In FFXIV, run `/xlsettings` and open **Experimental → Dev Plugin Locations**.
3. Add the full path to `addon\StepPanel.dll` and save. Keep `StepPanel.json` and `StepPanel.deps.json` beside the DLL.
4. Open `/xlplugins`, then look under **Dev Tools → Installed Dev Plugins**. Enable **Step Panel** if needed.
5. Click **Nix’s Den** below the step count or run `/steppanel den`.

Use `/steppanel` to toggle the step panel. Drag the panel by its title bar to move it. See the [Dalamud guide to loading local development plugins](https://dalamud.dev/faq/getting-started/#how-do-i-hot-reload-my-plugin).

To update an existing installation, disable **Step Panel**, replace all three files in your existing addon folder, and enable it again. Close FFXIV first if Windows reports that the DLL is in use.

Updating from a version older than 0.11.0 resets minigame XP once for the new leveling system. Your coin balance, unlocked illustrations, steps, and saved quest history carry over. Quests are currently paused.

## Build and review

Use the .NET 10 SDK and a local installation of the **Dalamud API 15** assemblies. The project defaults to `%APPDATA%\XIVLauncher\addon\Hooks\dev`. To select a different install, set `DALAMUD_HOME` to the directory containing `Dalamud.dll` and `Dalamud.Bindings.ImGui.dll`.

From the repository root in PowerShell:

```powershell
$dalamud = if ($env:DALAMUD_HOME) { $env:DALAMUD_HOME } else { Join-Path $env:APPDATA 'XIVLauncher\addon\Hooks\dev' }
dotnet build .\source\StepPanel\StepPanel.csproj -c Release
dotnet run --project .\source\StepPanelChecks\StepPanelChecks.csproj -c Release -- .\source\StepPanel\bin\Release\net10.0-windows\StepPanel.dll $dalamud
```

The DLL, manifest, and dependency manifest are written to `source\StepPanel\bin\Release\net10.0-windows`. You can point Dalamud at this DLL to run your own build.

Start with [Plugin.cs](source/StepPanel/Plugin.cs) for the game-facing hooks and [Progress.cs](source/StepPanel/Progress.cs) for saved data. The plugin uses local character position and game state to estimate steps. Dalamud stores coins, levels, and album unlocks locally. The illustrations are embedded in the DLL.

**Nix Nackle** is a community fan project for FFXIV. The source code is available under the [MIT License](LICENSE). FFXIV and its setting belong to Square Enix.
