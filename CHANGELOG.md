# Changelog

## [2.1.0](https://github.com/M-archand/K4-WorldText-API/compare/v2.0.0...v2.1.0) (2026-10-19)


### Features

* **api:** add a model background behind text. Set `ModelBackgroundEnabled` and `ModelBackgroundPath` on a `TextLine` to spawn one `prop_dynamic_override` behind the whole text, with an optional material group, depth, and uniform scale. It is independent of the `point_worldtext` background panel, so a line may use either, both, or neither. The consuming plugin precaches the model ([9daf8e2](https://github.com/M-archand/K4-WorldText-API/commit/9daf8e2e1db63889d5000af3b721f91469f8504e))


### API Changes (2.0.0 -> 2.1.0)

**New `TextLine` fields**

* `bool ModelBackgroundEnabled` (default `false`) - spawn a model background behind the text. Ignored while `ModelBackgroundPath` is empty
* `string ModelBackgroundPath` (default `""`) - the `.vmdl` to show, for example `models/my_plugin/panel.vmdl`. The API does not precache it: add it from your own `Listeners.OnServerPrecacheResources` listener with `manifest.AddResource(path)`, or the prop renders nothing
* `string ModelBackgroundMaterialGroup` (default `""`) - the material group (skin) name declared in the model's `MaterialGroupList`. Empty or an unknown name keeps the model's `DefaultMaterialGroup`
* `int ModelBackgroundDepth` (default `2`) - world units the prop sits behind the text plane, away from the viewer. Clamped to ±1000
* `float ModelBackgroundSurfaceScale` (default `1.0`) - uniform scale of the prop. Clamped to 0.01-100

**Behaviour**

* One prop per text. For a multi-line text, the first line with `ModelBackgroundEnabled` and a non-empty `ModelBackgroundPath` supplies the settings, and the prop is centered on the stack of lines
* The prop spawns with collision disabled, so it never blocks players or bullets
* `UpdateWorldText` updates in place only when the model background settings are also unchanged. A change to any `ModelBackground*` field respawns the text
* `TeleportWorldText` moves the prop in place along with the lines
* `GetWorldTextLineEntities` does not include the prop
* Texts that do not enable a model background behave exactly as in 2.0.0


## [2.0.0](https://github.com/M-archand/K4-WorldText-API/compare/v1.3.0...v2.0.0) (2026-09-16)


### ⚠ BREAKING CHANGES

* the `k4-worldtext:sharedapi` capability now serves `IK4WorldTextProvider` instead of `IK4WorldTextSharedAPI`. A consumer built against 1.x fails at `Capability.Get()` with `KeyNotFoundException` once 2.0 is installed. Change the field to `PluginCapability<IK4WorldTextProvider>` and call `ForPlugin(this)` on the result.
* config persistence is gone. The `saveConfig`, `removeFromConfig`, and `modifyConfig` parameters were removed, `RemoveAllTemporary()` was replaced by `RemoveAll()`, and nothing is written to or read from disk. Re-create texts from your own map-start handling.


### Features

* **api:** scope the shared API per consumer plugin. `IK4WorldTextProvider.ForPlugin(IPlugin owner)` returns a scope keyed by `ModuleName`; every text belongs to the plugin that created it, an id issued to another plugin is treated as unknown, foreign-id access is logged with the real owner's name, and `RemoveAll()` removes only the caller's texts. The same plugin gets the same scope after a hot reload, so it can clear texts left by its previous instance ([0402eac](https://github.com/M-archand/K4-WorldText-API/commit/0402eac311b2dbf6769cade3666b384db4e89caa))
* **api:** remove the config persistence parameters from the shared API and rewrite the README around the shared-API-only model ([aa197d3](https://github.com/M-archand/K4-WorldText-API/commit/aa197d366366c7c50d621ae69cce5be4e07f5027))
* **api:** document every public member of the shared API and ship `K4-WorldText-SharedAPI.xml` next to the DLL, so the argument, threading, and clamping rules show up in IntelliSense ([60cf26f](https://github.com/M-archand/K4-WorldText-API/commit/60cf26fbbe22a0691683d63f0a396f1be2021ceb))


### Bug Fixes

* **threading:** marshal API entity work onto the game thread and make text tracking concurrent, so calls from a `Task.Run` body, an `HttpClient` continuation, or a database callback no longer touch entity memory off-thread ([e51b65c](https://github.com/M-archand/K4-WorldText-API/commit/e51b65c8b8f13db2bfc81c0f76c8be9eaa12b9f9))
* **threading:** run the entity work inline when the caller is already on the game thread, so a text exists in the world by the time `AddWorldText` returns, and capture the game thread id synchronously in `Load` instead of racing the first world update ([001d67c](https://github.com/M-archand/K4-WorldText-API/commit/001d67c7e602e48068a08eaa3524009699091f73)) ([e7cf60b](https://github.com/M-archand/K4-WorldText-API/commit/e7cf60b40a6fb74755b88ca7d9f020a730b6229f))
* **api:** validate arguments synchronously on the calling thread and throw `ArgumentException` before any work is queued, instead of failing later on the game thread with no way to report it ([daaa8ad](https://github.com/M-archand/K4-WorldText-API/commit/daaa8ada14eb58f4f487de906f3119ec7a040f71))
* **api:** validate `CCSPlayerController` and its pawn at the boundary and again inside the deferred spawn, so a player who disconnects between the call and the world update produces a logged warning instead of a crash ([f12a002](https://github.com/M-archand/K4-WorldText-API/commit/f12a0023417762e05e1186be415b990b8da4da93))
* **api:** copy the caller's `Vector`/`QAngle` at the boundary so a respawn never reads entity-backed memory, making `pawn.AbsOrigin` safe to pass ([8757296](https://github.com/M-archand/K4-WorldText-API/commit/87572964775f02e3e76d269fef9f620cafe37f1e))
* **api:** contain entity-work exceptions and throw `KeyNotFoundException` for unknown text ids. A failed spawn rolls back, logs, keeps the id tracked, and retries on the next round start, rather than surfacing at the caller ([d180290](https://github.com/M-archand/K4-WorldText-API/commit/d1802902c628cc3ad9aa5a0adccefcc9f9a9d8cd))
* **api:** return `null` from `GetWorldTextLineEntities` for unknown ids and skip entities that are no longer valid, instead of handing back dangling pointers ([5494db2](https://github.com/M-archand/K4-WorldText-API/commit/5494db2b7831f477aaf424d9468ccdb146e6fe4a))
* **api:** log rejected shared-API calls before throwing, so a misbehaving consumer is identifiable from the server log ([a690c50](https://github.com/M-archand/K4-WorldText-API/commit/a690c5041f7258cbaf66918ad1eceff6ee8aee83))
* **entities:** clamp `point_worldtext` `MessageText` to its 512-byte budget, truncating on a character boundary ([8a1a998](https://github.com/M-archand/K4-WorldText-API/commit/8a1a9989cae473e4d756292e5c154aeb5128bb81))
* **entities:** clamp the numeric `TextLine` styling fields and truncate `FontName` to the entity's 64-byte field before writing to native memory; `NaN` becomes the in-range value closest to zero ([ebc651c](https://github.com/M-archand/K4-WorldText-API/commit/ebc651cbe632eace5216fc854f6ac46f239829df))
* **entities:** dispose `WorldText` children on removal and guard the mutators after dispose, and untrack disposed texts so a disposed `MultilineWorldText` is never re-used ([af6014f](https://github.com/M-archand/K4-WorldText-API/commit/af6014fabca390d170f1e94fe820e649da116dc5)) ([f5abea6](https://github.com/M-archand/K4-WorldText-API/commit/f5abea6203611ff54e495d34370511853f43301f))
* **geometry:** normalize yaw to `[-180,180)` in `EntityFaceToDirection` ([ec2c2be](https://github.com/M-archand/K4-WorldText-API/commit/ec2c2be02d88fae15d3a80886355b423c5e19815))
* **geometry:** derive the floor line stacking direction from `absRotation` instead of the previous entity, so multi-line floor texts stack correctly ([07a6f3f](https://github.com/M-archand/K4-WorldText-API/commit/07a6f3fa22c625e567895bd43e32a162cba434c6))
* **perf:** update world text entities in place instead of respawning them when the line count, font sizes, and background layout are unchanged ([38f215a](https://github.com/M-archand/K4-WorldText-API/commit/38f215a531cab35ccbb3b0a854f780802204890f))
* **perf:** teleport world text entities in place, respawning only when one of them is no longer valid ([55afc76](https://github.com/M-archand/K4-WorldText-API/commit/55afc76c312c7cdf7dea700bba319412ce3cfc85))
* **perf:** skip the round-start reapply for texts whose entities survived the round transition, and cache entity validity in that check ([2e5b79d](https://github.com/M-archand/K4-WorldText-API/commit/2e5b79d6f8ab9b32e1e167a07b3389187db50d2a)) ([2a86740](https://github.com/M-archand/K4-WorldText-API/commit/2a86740155263f29b6870bf8f51d22b06e254082))


### Code Refactoring

* mark the geometry and spawn helpers `internal` so they are no longer part of the public surface ([5392a19](https://github.com/M-archand/K4-WorldText-API/commit/5392a19b492e05388c384b1765261952b3dc3c58))


### API Changes (1.3.0 -> 2.0.0)

**Capability**

* `k4-worldtext:sharedapi` (name unchanged) now serves `IK4WorldTextProvider` instead of `IK4WorldTextSharedAPI`

**New interface**

* `IK4WorldTextProvider`
  * `IK4WorldTextSharedAPI ForPlugin(IPlugin owner)` - the scope for one consumer plugin, keyed by `ModuleName`. Throws `ArgumentNullException` for a null owner and `ArgumentException` for an empty `ModuleName`. Safe to call from any thread

**Removed parameters**

* `saveConfig` from both `AddWorldText` overloads and both `AddWorldTextAtPlayer` overloads
* `removeFromConfig` from `RemoveWorldText`
* `modifyConfig` from `TeleportWorldText`

**Renamed methods**

* `RemoveAllTemporary()` -> `RemoveAll()`, now scoped to the calling plugin

**Changed behaviour**

* Ids are per scope. `UpdateWorldText`, `RemoveWorldText`, and `TeleportWorldText` throw `KeyNotFoundException` for an id that is not tracked by the calling scope, including every id issued before the last map change; `GetWorldTextLineEntities` returns `null` instead
* Bad arguments throw `ArgumentException` synchronously on the calling thread before any entity work is queued
* `GetWorldTextLineEntities` is game thread only and throws `InvalidOperationException` elsewhere, because raw entity pointers cannot be marshalled
* `textLines` is copied at the call, so the caller may reuse or clear its list afterwards
* `TextLine` numeric fields are clamped, never rejected: `FontSize` 1-500, `Scale` and `BackgroundScale` 0.01-10, `BackgroundWorldToUV` 0.001-10, `BackgroundBorderWidth`/`BackgroundBorderHeight` 0-1000, the depth offsets ±100, `BackgroundMaxCharsPerLine` 1-256, `BackgroundPadChars` 0-256, `BackgroundWidthInflation` 0.1-10. `FontName` is truncated to 64 UTF-8 bytes and `Text` to 512
* Nothing is persisted to disk. Every text lives until its owner removes it or the map ends. Unloading or reloading the plugin destroys every text and resets the id counter, so ids restart from 1


### Miscellaneous

* **deps:** remove the unused `Newtonsoft.Json` package reference ([c824519](https://github.com/M-archand/K4-WorldText-API/commit/c824519a167142ca7fb394df3b246065b8c7b971))
