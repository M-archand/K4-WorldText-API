using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using K4WorldTextSharedAPI;
using Microsoft.Extensions.Logging;

namespace K4ryuuCS2WorldTextAPI;

[MinimumApiVersion(369)]
public class Plugin : BasePlugin
{
    private readonly ConcurrentDictionary<int, MultilineWorldText> _multilineWorldTexts = new();
    public IReadOnlyCollection<MultilineWorldText> MultilineWorldTexts => _multilineWorldTexts.Values.ToList();

    internal void TrackWorldText(MultilineWorldText worldText) => _multilineWorldTexts[worldText.Id] = worldText;
    internal void UntrackWorldText(MultilineWorldText worldText) => _multilineWorldTexts.TryRemove(worldText.Id, out _);

    internal MultilineWorldText? FindWorldText(int id) =>
        _multilineWorldTexts.TryGetValue(id, out var worldText) ? worldText : null;

    internal List<MultilineWorldText> WorldTextsOwnedBy(string owner) =>
        _multilineWorldTexts.Values.Where(worldText => worldText.Owner == owner).ToList();

    private readonly WorldTextProvider _provider;

    public Plugin()
    {
        _provider = new WorldTextProvider(this);
    }

    private volatile int _gameThreadId;

    internal bool IsGameThread => _gameThreadId == Environment.CurrentManagedThreadId;

    internal void ThrowIfNotGameThread(string operation)
    {
        if (!IsGameThread)
        {
            Logger.LogWarning("{Operation} rejected: called from thread {ThreadId}, not the game thread.", operation, Environment.CurrentManagedThreadId);
            throw new InvalidOperationException(
                $"{operation} must be called from the game thread: a command handler, a game event handler, a listener, or a Server.NextWorldUpdate callback.");
        }
    }

    internal void OnGameThread(string operation, Action action)
    {
        if (IsGameThread)
            RunLogged(operation, action);
        else
            Server.NextWorldUpdate(() => RunLogged(operation, action));
    }

    private void RunLogged(string operation, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{Operation} failed on the game thread.", operation);
        }
    }

    public override string ModuleName => "K4-WorldText-API";
    public override string ModuleVersion => "2.0.0";
    public override string ModuleAuthor => "K4ryuu, Marchand";

    public static PluginCapability<IK4WorldTextProvider> Capability_SharedAPI { get; } = new("k4-worldtext:sharedapi");

    public override void Load(bool hotReload)
    {
        // BasePlugin.Load runs on the game thread. Capture the id synchronously so
        // IsGameThread is correct before the capability is registered and any
        // consumer can call the API. A deferred capture would leave a window where
        // every call, even from the game thread, was needlessly queued.
        _gameThreadId = Environment.CurrentManagedThreadId;

        Capabilities.RegisterPluginCapability(Capability_SharedAPI, () => _provider);

        RegisterListener<Listeners.OnMapEnd>(ClearData);

        RegisterEventHandler((EventRoundStart @event, GameEventInfo info) =>
        {
            foreach (var multilineWorldText in _multilineWorldTexts.Values)
            {
                try
                {
                    multilineWorldText.RespawnIfInvalid();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Round-start respawn of WorldText {Id} failed.", multilineWorldText.Id);
                }
            }
            return HookResult.Continue;
        });
    }

    public override void Unload(bool hotReload)
    {
        ClearData();
        MultilineWorldText.ResetIds();
    }

    internal void ClearData()
    {
        foreach (var multilineWorldText in _multilineWorldTexts.Values)
            multilineWorldText.Dispose();
        _multilineWorldTexts.Clear();
    }

    internal static bool TryGetPawnTransform(
        CCSPlayerController? player,
        [NotNullWhen(true)] out Vector? origin,
        [NotNullWhen(true)] out QAngle? rotation)
    {
        origin = null;
        rotation = null;

        if (player is null || !player.IsValid)
            return false;

        var pawn = player.PlayerPawn?.Value;
        if (pawn is null || !pawn.IsValid)
            return false;

        var pawnOrigin = pawn.AbsOrigin;
        var pawnRotation = pawn.AbsRotation;
        if (pawnOrigin is null || pawnRotation is null)
            return false;

        origin = new Vector(pawnOrigin.X, pawnOrigin.Y, pawnOrigin.Z);
        rotation = new QAngle(pawnRotation.X, pawnRotation.Y, pawnRotation.Z);
        return true;
    }

    internal int SpawnMultipleLines(string owner, CCSPlayerController player, TextPlacement placement, List<TextLine> lines)
    {
        var multilineWorldText = new MultilineWorldText(this, owner, lines);
        TrackWorldText(multilineWorldText);

        OnGameThread($"AddWorldTextAtPlayer({multilineWorldText.Id}) by {owner}", () =>
        {
            if (multilineWorldText.IsDisposed)
                return;

            if (!TryGetPawnTransform(player, out var pawnOrigin, out var pawnRotation))
            {
                Logger.LogWarning("AddWorldTextAtPlayer({Id}) by {Owner} discarded: the player controller became invalid before the spawn ran.", multilineWorldText.Id, owner);
                UntrackWorldText(multilineWorldText);
                multilineWorldText.Dispose();
                return;
            }

            var AbsOrigin = Vector.Zero;
            var AbsRotation = QAngle.Zero;
            var tempRotation = GetNormalizedAngles(pawnRotation);
            switch (placement)
            {
                case TextPlacement.Wall:
                    AbsOrigin = GetPlayerPosition(pawnOrigin, lines);
                    AbsRotation = new QAngle(tempRotation.X, tempRotation.Y + 270, tempRotation.Z + 90);
                    break;
                case TextPlacement.Floor:
                    AbsOrigin = pawnOrigin.With(z: pawnOrigin.Z + 1);
                    AbsRotation = new QAngle(tempRotation.X, tempRotation.Y + 270, tempRotation.Z);
                    break;
            }

            var direction = EntityFaceToDirection(pawnRotation.Y);
            var offset = GetDirectionOffset(direction, 15);

            multilineWorldText.Spawn(AbsOrigin + offset, AbsRotation, placement);
        });

        return multilineWorldText.Id;
    }

    internal static QAngle GetNormalizedAngles(QAngle absRotation)
    {
        return new QAngle(
            absRotation.X,
            (float)Math.Round(absRotation.Y / 10.0) * 10,
            absRotation.Z
        );
    }

    internal static Vector GetPlayerPosition(Vector feet, List<TextLine> lines)
    {
        static float StepFor(TextLine l) => MathF.Max(10f, TextLineLimits.FontSize(l.FontSize) / 3f);

        float totalAdvance = lines.Sum(StepFor);
        float usedHeight   = lines.Count > 0 ? totalAdvance - StepFor(lines[^1]) : 0f;

        float lastStep     = lines.Count > 0 ? StepFor(lines[^1]) : 0f;
        float bottomPad    = MathF.Max(8f, lastStep * 0.5f);

        return new Vector(feet.X, feet.Y, feet.Z + usedHeight + bottomPad);
    }

    internal string EntityFaceToDirection(float yaw)
    {
        yaw = ((yaw % 360f) + 540f) % 360f - 180f;

        if (yaw >= -22.5 && yaw < 22.5)
            return "X";
        if (yaw >= 22.5 && yaw < 67.5)
            return "XY";
        if (yaw >= 67.5 && yaw < 112.5)
            return "Y";
        if (yaw >= 112.5 && yaw < 157.5)
            return "-XY";
        if (yaw >= 157.5 || yaw < -157.5)
            return "-X";
        if (yaw >= -157.5 && yaw < -112.5)
            return "-X-Y";
        if (yaw >= -112.5 && yaw < -67.5)
            return "-Y";
        return "X-Y";
    }

    internal Vector GetDirectionOffset(string direction, float offsetValue)
    {
        return direction switch
        {
            "X" => new Vector(offsetValue, 0, 0),
            "-X" => new Vector(-offsetValue, 0, 0),
            "Y" => new Vector(0, offsetValue, 0),
            "-Y" => new Vector(0, -offsetValue, 0),
            "XY" => new Vector(offsetValue, offsetValue, 0),
            "-XY" => new Vector(-offsetValue, offsetValue, 0),
            "X-Y" => new Vector(offsetValue, -offsetValue, 0),
            "-X-Y" => new Vector(-offsetValue, -offsetValue, 0),
            _ => Vector.Zero
        };
    }

    public sealed class WorldTextProvider : IK4WorldTextProvider
    {
        private readonly Plugin _plugin;
        private readonly ConcurrentDictionary<string, GameTextAPIHandler> _scopes = new();

        public WorldTextProvider(Plugin plugin)
        {
            _plugin = plugin;
        }

        public IK4WorldTextSharedAPI ForPlugin(IPlugin owner)
        {
            if (owner is null)
            {
                _plugin.Logger.LogWarning("ForPlugin rejected: owner is null.");
                throw new ArgumentNullException(nameof(owner));
            }

            var name = owner.ModuleName;
            if (string.IsNullOrWhiteSpace(name))
            {
                _plugin.Logger.LogWarning("ForPlugin rejected: owner has an empty ModuleName.");
                throw new ArgumentException("The owning plugin must have a non-empty ModuleName.", nameof(owner));
            }

            if (_scopes.TryGetValue(name, out var existing))
                return existing;

            var scope = new GameTextAPIHandler(_plugin, name);
            var winner = _scopes.GetOrAdd(name, scope);
            if (ReferenceEquals(winner, scope))
                _plugin.Logger.LogInformation("Consumer {Owner} acquired its WorldText scope.", name);

            return winner;
        }
    }

    public class GameTextAPIHandler : IK4WorldTextSharedAPI
    {
        private readonly Plugin _plugin;

        public GameTextAPIHandler(Plugin plugin, string owner)
        {
            _plugin = plugin;
            Owner = owner;
        }

        public string Owner { get; }

        private TException Reject<TException>(string operation, TException exception, string? reason = null)
            where TException : Exception
        {
            _plugin.Logger.LogWarning("{Operation} by {Owner} rejected: {Reason}", operation, Owner, reason ?? exception.Message);
            return exception;
        }

        private MultilineWorldText RequireWorldText(string operation, int id)
        {
            var target = _plugin.FindWorldText(id);
            if (target is not null && target.Owner == Owner)
                return target;

            var reason = target is null ? null : $"WorldText {id} belongs to {target.Owner}.";
            throw Reject(operation, new KeyNotFoundException($"WorldText with ID {id} not found."), reason);
        }

        private List<TextLine> ValidateLines(string operation, List<TextLine>? textLines, string paramName)
        {
            if (textLines is null)
                throw Reject(operation, new ArgumentNullException(paramName));
            if (textLines.Count == 0)
                throw Reject(operation, new ArgumentException("At least one TextLine is required.", paramName));

            foreach (var line in textLines)
            {
                if (line is null)
                    throw Reject(operation, new ArgumentException("TextLine entries must not be null.", paramName));
                if (line.Text is null)
                    throw Reject(operation, new ArgumentException("TextLine.Text must not be null.", paramName));
            }

            return [.. textLines];
        }

        private void ValidatePlacement(string operation, TextPlacement placement)
        {
            if (!Enum.IsDefined(placement))
                throw Reject(operation, new ArgumentOutOfRangeException(nameof(placement), placement, "Unknown TextPlacement value."));
        }

        private (Vector Position, QAngle Angle) CopyTransform(string operation, Vector? position, QAngle? angle)
        {
            if (position is null)
                throw Reject(operation, new ArgumentNullException(nameof(position)));
            if (angle is null)
                throw Reject(operation, new ArgumentNullException(nameof(angle)));
            return (new Vector(position.X, position.Y, position.Z), new QAngle(angle.X, angle.Y, angle.Z));
        }

        public int AddWorldText(TextPlacement placement, TextLine textLine, Vector position, QAngle angle)
        {
            if (textLine is null)
                throw Reject(nameof(AddWorldText), new ArgumentNullException(nameof(textLine)));
            return AddWorldText(placement, new List<TextLine> { textLine }, position, angle);
        }

        public int AddWorldText(TextPlacement placement, List<TextLine> textLines, Vector position, QAngle angle)
        {
            ValidatePlacement(nameof(AddWorldText), placement);
            var (spawnPosition, spawnAngle) = CopyTransform(nameof(AddWorldText), position, angle);
            var lines = ValidateLines(nameof(AddWorldText), textLines, nameof(textLines));

            var multilineWorldText = new MultilineWorldText(_plugin, Owner, lines);
            _plugin.TrackWorldText(multilineWorldText);

            _plugin.OnGameThread($"AddWorldText({multilineWorldText.Id}) by {Owner}", () =>
            {
                if (multilineWorldText.IsDisposed)
                    return;

                multilineWorldText.Spawn(spawnPosition, spawnAngle, placement);
            });

            return multilineWorldText.Id;
        }

        public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, TextLine textLine)
        {
            if (textLine is null)
                throw Reject(nameof(AddWorldTextAtPlayer), new ArgumentNullException(nameof(textLine)));
            return AddWorldTextAtPlayer(player, placement, new List<TextLine> { textLine });
        }

        public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, List<TextLine> textLines)
        {
            if (player is null)
                throw Reject(nameof(AddWorldTextAtPlayer), new ArgumentNullException(nameof(player)));
            if (!TryGetPawnTransform(player, out _, out _))
                throw Reject(nameof(AddWorldTextAtPlayer), new ArgumentException("The player controller is invalid or has no valid pawn.", nameof(player)));
            ValidatePlacement(nameof(AddWorldTextAtPlayer), placement);
            var lines = ValidateLines(nameof(AddWorldTextAtPlayer), textLines, nameof(textLines));

            return _plugin.SpawnMultipleLines(Owner, player, placement, lines);
        }

        public void UpdateWorldText(int id, TextLine? textLine = null)
        {
            UpdateWorldText(id, textLine is null ? null : new List<TextLine> { textLine });
        }

        public void UpdateWorldText(int id, List<TextLine>? textLines = null)
        {
            var lines = textLines is null ? null : ValidateLines(nameof(UpdateWorldText), textLines, nameof(textLines));
            var target = RequireWorldText(nameof(UpdateWorldText), id);

            _plugin.OnGameThread($"UpdateWorldText({id}) by {Owner}", () =>
            {
                if (target.IsDisposed)
                    return;

                target.Update(lines);
            });
        }

        public void RemoveWorldText(int id)
        {
            var target = RequireWorldText(nameof(RemoveWorldText), id);

            _plugin.UntrackWorldText(target);
            _plugin.OnGameThread($"RemoveWorldText({id}) by {Owner}", target.Dispose);
        }

        public List<CPointWorldText>? GetWorldTextLineEntities(int id)
        {
            _plugin.ThrowIfNotGameThread($"{nameof(GetWorldTextLineEntities)} by {Owner}");

            var target = _plugin.FindWorldText(id);
            if (target is null)
                return null;

            if (target.Owner != Owner)
            {
                _plugin.Logger.LogWarning("{Operation} by {Owner} rejected: WorldText {Id} belongs to {ActualOwner}.",
                    nameof(GetWorldTextLineEntities), Owner, id, target.Owner);
                return null;
            }

            return target.Texts
                .Select(t => t.Entity)
                .Where(e => e?.IsValid == true)
                .Cast<CPointWorldText>()
                .ToList();
        }

        public void TeleportWorldText(int id, Vector position, QAngle angle)
        {
            var (newPosition, newAngle) = CopyTransform(nameof(TeleportWorldText), position, angle);
            var target = RequireWorldText(nameof(TeleportWorldText), id);

            _plugin.OnGameThread($"TeleportWorldText({id}) by {Owner}", () =>
            {
                if (target.IsDisposed)
                    return;

                target.Teleport(newPosition, newAngle);
            });
        }

        public void RemoveAll()
        {
            var targets = _plugin.WorldTextsOwnedBy(Owner);
            foreach (var multilineWorldText in targets)
                _plugin.UntrackWorldText(multilineWorldText);

            _plugin.OnGameThread($"RemoveAll by {Owner}", () =>
            {
                foreach (var multilineWorldText in targets)
                    multilineWorldText.Dispose();
            });
        }
    }
}