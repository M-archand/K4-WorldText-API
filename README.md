# K4 WorldText API for CSSharp
A shared-API-only CounterStrikeSharp plugin for spawning and managing in-world text entities (`point_worldtext`) in Counter-Strike 2. It handles single and multi-line texts, optional background panels, per-line styling, placement on walls or floors, live updates, and teleporting, so consuming plugins never touch entity plumbing.

The plugin has no commands, no configuration file, and does nothing on its own. Every text is created by another plugin through `IK4WorldTextSharedAPI`, obtained via the `k4-worldtext:sharedapi` plugin capability from the shared `K4WorldTextSharedAPI` assembly.

## Requirements
- CounterStrikeSharp with API version **369** or newer (`MinimumApiVersion(369)`).
- .NET 10 runtime (the plugin targets `net10.0`).

## Installation
The release zip mirrors the CounterStrikeSharp layout. Extract it into your server's `game/csgo/` directory:
- `addons/counterstrikesharp/plugins/K4-WorldText-API/` - the plugin.
- `addons/counterstrikesharp/shared/K4-WorldText-SharedAPI/` - the shared API assembly.

Consumer plugins reference `K4-WorldText-SharedAPI.dll` at compile time and must not ship their own copy of it; CounterStrikeSharp loads the one in `shared/`.

## Example
```c#
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using K4WorldTextSharedAPI;
using Microsoft.Extensions.Logging;
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

    // Spawn on the floor in front of the calling player.
    MessageId = WorldTextApi.AddWorldTextAtPlayer(caller, TextPlacement.Floor, line);
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

    WorldTextApi.RemoveWorldText(MessageId);
    MessageId = -1;
}
```

## API
The `IK4WorldTextSharedAPI` interface exposes:
- `int AddWorldText(placement, textLine | textLines, position, angle)` - spawns a single- or multi-line text at a world position/angle. Returns the text id.
- `int AddWorldTextAtPlayer(player, placement, textLine | textLines)` - spawns text relative to the player: at their feet for `Floor`, at their position for `Wall`, offset 15 units in the direction they face. Returns the text id.
- `void UpdateWorldText(id, textLine | textLines = null)` - replaces the content/styling of an existing text. Entities are updated in place when the line count, font sizes, and background layout are unchanged; otherwise they are respawned. Passing no lines re-applies the current ones.
- `void RemoveWorldText(id)` - despawns a text.
- `List<CPointWorldText>? GetWorldTextLineEntities(id)` - returns the valid foreground entity for each line (background entities and lines whose entity is currently invalid are not included), or `null` if the id is not tracked. **Game thread only** (see below).
- `void TeleportWorldText(id, position, angle)` - moves an existing text in place. Entities are respawned only if one of them is no longer valid.
- `void RemoveAll()` - despawns every tracked text, including texts created by other consumers.

`UpdateWorldText`, `RemoveWorldText`, and `TeleportWorldText` throw `KeyNotFoundException("WorldText with ID N not found.")` for an id that is not tracked, including every id issued before the last map change. Catch that type to handle the expected post-map-change case. `GetWorldTextLineEntities` returns `null` instead.

Arguments are validated synchronously on the calling thread before any work is queued, and a bad argument throws an `ArgumentException` (or a subclass): a null `player`, `position`, `angle`, `textLine`, or `textLines`; an empty `textLines` list or one containing a null entry or a null `Text`; a `placement` outside the `TextPlacement` enum. The plugin copies `textLines` at the call, so the caller may reuse or clear its list afterwards.

Failures inside the queued entity work (for example `point_worldtext` creation failing because the server is out of entity slots) never reach the caller: the plugin rolls back any entities it created for that operation, logs the exception through its own logger, and keeps the id tracked so `RemoveWorldText` still works. The next round start retries the spawn.

`placement` is `TextPlacement.Wall` or `TextPlacement.Floor`. Each `TextLine` carries its own styling: color, font, size, scale, justification, reorient mode, and an optional background panel (`BackgroundEnabled` plus the `Background*` fields). `Text` longer than 512 UTF-8 bytes is truncated on a character boundary; that is the `point_worldtext` limit.

Numeric styling fields are clamped before they reach the entity, never rejected: `FontSize` to 1–500, `Scale` and `BackgroundScale` to 0.01–10, `BackgroundWorldToUV` to 0.001–10, `BackgroundBorderWidth`/`BackgroundBorderHeight` to 0–1000, the depth offsets to ±100, `BackgroundMaxCharsPerLine` to 1–256, `BackgroundPadChars` to 0–256, and `BackgroundWidthInflation` to 0.1–10. `NaN` becomes the in-range value closest to zero. `FontName` is truncated to 64 UTF-8 bytes, the size of the entity's font-name field.

### Lifecycle
- A text exists only because a consumer created it, and only until that consumer removes it, someone calls `RemoveAll()`, or the map ends. Nothing is loaded from or saved to disk.
- On every round start the plugin respawns each tracked text whose entities the round transition destroyed. Texts whose entities survived are left untouched, so a `TextLine` mutated after the call is not picked up until `UpdateWorldText` is called.
- On map end every text is destroyed and its id becomes invalid. A consumer that wants a text on the next map re-creates it from its own map-start handling.
- Unloading or reloading this plugin destroys every text and resets the id counter, so ids restart from 1. Consumers that stay loaded across a reload must discard the ids they hold.

### Thread safety
Every method except `GetWorldTextLineEntities` is safe to call from any thread - a `Task.Run` body, an `HttpClient` continuation, a database callback. The entity work is queued onto the game thread with `Server.NextWorldUpdate`, so:

- The call returns before the text exists in the world. `AddWorldText` still returns its id immediately, and that id is valid right away.
- Queued work runs in call order, so `UpdateWorldText` / `RemoveWorldText` on a freshly returned id behave as written.
- `AddWorldTextAtPlayer` reads the player's position at world-update time, not at call time.
- `position` and `angle` are copied at the call. Passing an entity-backed `Vector`/`QAngle` such as `pawn.AbsOrigin` is safe; the plugin never reads the caller's instance again.

`GetWorldTextLineEntities` returns raw entity pointers, which cannot be marshalled. Call it from a command handler, a game event handler, a listener, or inside your own `Server.NextWorldUpdate` callback.

## License

Distributed under the GPL-3.0 License. See `LICENSE.md` for more information.