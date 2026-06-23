# K4 WorldText API for CSSharp
This plugin exposes a shared developer API for spawning and managing in-world text entities (`point_worldtext`) in Counter-Strike 2. It handles single and multi-line texts, optional background panels, per-line styling, placement on walls or floors, live updates, teleporting, and optional per-map persistence - so consuming plugins never touch entity plumbing.

Texts spawned with `saveConfig: true` are persisted per map and respawned automatically; temporary texts live until the round/map ends or are cleared explicitly. Other plugins consume the API through the shared `K4WorldTextSharedAPI` assembly and the `k4-worldtext:sharedapi` plugin capability.

## Requirements
- CounterStrikeSharp with API version **369** or newer (`MinimumApiVersion(369)`).
- .NET 10 runtime (the plugin targets `net10.0`).

## Example
```c#
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using K4WorldTextSharedAPI;
using System.Drawing;

private readonly PluginCapability<IK4WorldTextSharedAPI> Capability = new("k4-worldtext:sharedapi");
private IK4WorldTextSharedAPI? WorldTextApi;
private int MessageId = -1;

public override void OnAllPluginsLoaded(bool hotReload)
{
    WorldTextApi = Capability.Get();

    if (WorldTextApi == null)
    {
        Logger.LogError("[WorldText-Example] K4-WorldText-API not loaded");
        return;
    }
}

[ConsoleCommand("css_worldtext_example", "Spawns an example world text where the player is looking")]
public void OnExampleCommand(CCSPlayerController? caller, CommandInfo _)
{
    if (caller == null || !caller.IsValid || WorldTextApi == null)
        return;

    var line = new TextLine
    {
        Text = "Hello from K4-WorldText-API!",
        Color = Color.Cyan,
        FontSize = 28,
        FontName = "Arial Bold",
        Scale = 0.45f,
        BackgroundEnabled = true,
        BackgroundColor = Color.FromArgb(200, 0, 0, 0)
    };

    // Spawn on the floor in front of the calling player, not persisted to config.
    MessageId = WorldTextApi.AddWorldTextAtPlayer(caller, TextPlacement.Floor, line, saveConfig: false);
}

[ConsoleCommand("css_worldtext_update", "Updates the example world text")]
public void OnUpdateCommand(CCSPlayerController? caller, CommandInfo _)
{
    if (WorldTextApi == null || MessageId == -1)
        return;

    WorldTextApi.UpdateWorldText(MessageId, new TextLine
    {
        Text = "Updated text!",
        Color = Color.Lime
    });
}

[ConsoleCommand("css_worldtext_remove", "Removes the example world text")]
public void OnRemoveCommand(CCSPlayerController? caller, CommandInfo _)
{
    if (WorldTextApi == null || MessageId == -1)
        return;

    WorldTextApi.RemoveWorldText(MessageId, removeFromConfig: true);
    MessageId = -1;
}
```

## API
The `IK4WorldTextSharedAPI` interface exposes:
- `int AddWorldText(placement, textLine | textLines, position, angle, saveConfig = false)` - spawns a single- or multi-line text at a world position/angle. Returns the message id.
- `int AddWorldTextAtPlayer(player, placement, textLine | textLines, saveConfig = false)` - spawns text relative to the player (e.g. floor in front of them). Returns the message id.
- `void UpdateWorldText(id, textLine | textLines = null)` - replaces the content/styling of an existing text.
- `void RemoveWorldText(id, removeFromConfig = true)` - despawns a text and, by default, drops it from the saved config.
- `List<CPointWorldText>? GetWorldTextLineEntities(id)` - returns the underlying entities for a text, or `null`.
- `void TeleportWorldText(id, position, angle, modifyConfig = false)` - moves an existing text; optionally updates its saved position.
- `void RemoveAllTemporary()` - despawns every non-persisted text.

`placement` is `TextPlacement.Wall` or `TextPlacement.Floor`. Each `TextLine` carries its own styling — color, font, size, scale, justification, reorient mode, and an optional background panel (`BackgroundEnabled` plus the `Background*` fields).

Pass `saveConfig: true` to persist a text for the current map so it respawns automatically; leave it `false` for temporary texts. Persisted texts are keyed per map and cleared on map end.

## License

Distributed under the GPL-3.0 License. See `LICENSE.md` for more information.