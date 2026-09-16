# K4 WorldText API for CSSharp
A shared-API-only CounterStrikeSharp plugin for spawning and managing in-world text entities (`point_worldtext`) in Counter-Strike 2. It handles single and multi-line texts, optional background panels, per-line styling, placement on walls or floors, live updates, and teleporting, so consuming plugins never touch entity plumbing.

The plugin has no commands, no configuration file, and does nothing on its own. Every text is created by another plugin through `IK4WorldTextSharedAPI`. A consumer gets the `IK4WorldTextProvider` from the `k4-worldtext:sharedapi` plugin capability (shared `K4WorldTextSharedAPI` assembly) and calls `ForPlugin(this)` on it to receive its own scope. Every text belongs to the plugin that created it.

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

private readonly PluginCapability<IK4WorldTextProvider> Capability = new("k4-worldtext:sharedapi");
private IK4WorldTextSharedAPI? WorldTextApi;
private int MessageId = -1;

public override void OnAllPluginsLoaded(bool hotReload)
{
    // The scope is keyed by this plugin's ModuleName, every text created through it belongs to this plugin.
    WorldTextApi = Capability.Get()?.ForPlugin(this);

    if (WorldTextApi == null)
    {
        Logger.LogError("[WorldText-Example] K4-WorldText-API not loaded");
        return;
    }
}

public override void Unload(bool hotReload)
{
    // Only this plugin's texts are removed.
    WorldTextApi?.RemoveAll();
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
`IK4WorldTextProvider`, served by the capability, has one method:
- `IK4WorldTextSharedAPI ForPlugin(IPlugin owner)` - returns the text API scoped to `owner`. Texts belong to the plugin that created them, and an id is only valid on the scope that issued it: `UpdateWorldText`, `RemoveWorldText`, and `TeleportWorldText` treat another plugin's id as unknown, `GetWorldTextLineEntities` returns `null` for it, and `RemoveAll()` removes only the caller's texts. Touching a foreign id is logged with the real owner's name. Scopes are keyed by `ModuleName`, so the same plugin always receives the same scope, also after it hot reloads; a reloaded consumer can call `RemoveAll()` to clear texts left by its previous instance. `ModuleName` must therefore be unique per plugin. Safe to call from any thread; call it as often as you like.

The `IK4WorldTextSharedAPI` scope exposes:
- `int AddWorldText(placement, textLine | textLines, position, angle)` - spawns a single- or multi-line text at a world position/angle. Returns the text id.
- `int AddWorldTextAtPlayer(player, placement, textLine | textLines)` - spawns text relative to the player: at their feet for `Floor`, at their position for `Wall`, offset 15 units in the direction they face. Returns the text id.
- `void UpdateWorldText(id, textLine | textLines = null)` - replaces the content/styling of an existing text. Entities are updated in place when the line count, font sizes, and background layout are unchanged; otherwise they are respawned. Passing no lines re-applies the current ones.
- `void RemoveWorldText(id)` - despawns a text.
- `List<CPointWorldText>? GetWorldTextLineEntities(id)` - returns the valid foreground entity for each line (background entities and lines whose entity is currently invalid are not included), or `null` if the id is not tracked. **Game thread only** (see below).
- `void TeleportWorldText(id, position, angle)` - moves an existing text in place. Entities are respawned only if one of them is no longer valid.
- `void RemoveAll()` - despawns every text created through this scope. Other plugins' texts are untouched.

`UpdateWorldText`, `RemoveWorldText`, and `TeleportWorldText` throw `KeyNotFoundException("WorldText with ID N not found.")` for an id that is not tracked, including every id issued before the last map change. Catch that type to handle the expected post-map-change case. `GetWorldTextLineEntities` returns `null` instead.

Arguments are validated synchronously on the calling thread before any work is queued, and a bad argument throws an `ArgumentException` (or a subclass): a null `owner` passed to `ForPlugin`, or one whose `ModuleName` is empty; a null `player`, `position`, `angle`, `textLine`, or `textLines`; a `player` whose controller is invalid or has no valid pawn (for example a disconnected player); an empty `textLines` list or one containing a null entry or a null `Text`; a `placement` outside the `TextPlacement` enum. The plugin copies `textLines` at the call, so the caller may reuse or clear its list afterwards.

Failures inside the entity work (for example `point_worldtext` creation failing because the server is out of entity slots) never reach the caller, whether the work ran immediately or was queued: the plugin rolls back any entities it created for that operation, logs the exception through its own logger, and keeps the id tracked so `RemoveWorldText` still works. The next round start retries the spawn. The one exception is `AddWorldTextAtPlayer` when the player is no longer valid by the time the queued spawn runs: there is no position to retry from, so the text is discarded, its id becomes unknown (`KeyNotFoundException` / `null` as described above), and a warning is logged.

`placement` is `TextPlacement.Wall` or `TextPlacement.Floor`. Each `TextLine` carries its own styling: color, font, size, scale, justification, reorient mode, and an optional background panel (`BackgroundEnabled` plus the `Background*` fields). `Text` longer than 512 UTF-8 bytes is truncated on a character boundary; that is the `point_worldtext` limit.

Numeric styling fields are clamped before they reach the entity, never rejected: `FontSize` to 1–500, `Scale` and `BackgroundScale` to 0.01–10, `BackgroundWorldToUV` to 0.001–10, `BackgroundBorderWidth`/`BackgroundBorderHeight` to 0–1000, the depth offsets to ±100, `BackgroundMaxCharsPerLine` to 1–256, `BackgroundPadChars` to 0–256, and `BackgroundWidthInflation` to 0.1–10. `NaN` becomes the in-range value closest to zero. `FontName` is truncated to 64 UTF-8 bytes, the size of the entity's font-name field.

### Lifecycle
- A text exists only because a consumer created it, and only until that consumer removes it (`RemoveWorldText`, or its own `RemoveAll()`) or the map ends. No other plugin can remove or modify it. Nothing is loaded from or saved to disk.
- On every round start the plugin respawns each tracked text whose entities the round transition destroyed. Texts whose entities survived are left untouched, so a `TextLine` mutated after the call is not picked up until `UpdateWorldText` is called.
- On map end every text is destroyed and its id becomes invalid. A consumer that wants a text on the next map re-creates it from its own map-start handling.
- Unloading or reloading this plugin destroys every text and resets the id counter, so ids restart from 1. Consumers that stay loaded across a reload must discard the ids they hold.

### Thread safety
`ForPlugin` and every scope method except `GetWorldTextLineEntities` are safe to call from any thread - a `Task.Run` body, an `HttpClient` continuation, a database callback. Where the entity work runs depends on the calling thread:

- From the game thread (a command handler, a game event handler, a listener, a `Server.NextWorldUpdate` callback) the entity work runs before the call returns. The text exists in the world when `AddWorldText` returns its id, and `GetWorldTextLineEntities` called right after already lists its entities.
- From any other thread the entity work is queued onto the game thread with `Server.NextWorldUpdate`. The call returns before the text exists in the world; the id is still valid right away. Queued work runs in call order, so `UpdateWorldText` / `RemoveWorldText` on a freshly returned id behave as written.
- `AddWorldTextAtPlayer` reads the player's position when the entity work runs. From the game thread that is the call itself, so the `ArgumentException` check is authoritative. From another thread the controller and pawn are re-validated at world-update time, and a player who disconnected in between produces no text (see above).
- `position` and `angle` are copied at the call. Passing an entity-backed `Vector`/`QAngle` such as `pawn.AbsOrigin` is safe; the plugin never reads the caller's instance again.

`GetWorldTextLineEntities` returns raw entity pointers, which cannot be marshalled, so it is game thread only: calling it from any other thread throws `InvalidOperationException`. A text whose spawn is still queued from another thread is listed with no entities until the next world update.

## Upgrading from 1.x
The 2.0 shared API is not source- or binary-compatible with 1.x. A consumer built against 1.x fails at `Capability.Get()` with `KeyNotFoundException` once 2.0 is installed, because nothing is registered under the old capability type any more.

- The capability now serves `IK4WorldTextProvider` instead of `IK4WorldTextSharedAPI`. Change the field to `PluginCapability<IK4WorldTextProvider>` and call `ForPlugin(this)` on the result of `Get()`; the returned `IK4WorldTextSharedAPI` has the same method names as before. The capability name `k4-worldtext:sharedapi` is unchanged.
- Texts are owned per plugin. Ids issued to another plugin are unknown to you, and `RemoveAll()` only removes your own texts.
- The persistence parameters are gone: `saveConfig` on the `AddWorldText*` overloads, `removeFromConfig` on `RemoveWorldText`, and `modifyConfig` on `TeleportWorldText`. `RemoveAllTemporary()` is replaced by `RemoveAll()`.
- Nothing is saved to disk. Re-create your texts from your own map-start handling.

## License

Distributed under the GPL-3.0 License. See `LICENSE.md` for more information.