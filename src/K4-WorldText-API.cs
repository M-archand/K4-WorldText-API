using System.Collections.Concurrent;
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

    internal void OnGameThread(string operation, Action action) => Server.NextWorldUpdate(() =>
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{Operation} failed on the game thread.", operation);
        }
    });

    public override string ModuleName => "K4-WorldText-API";
    public override string ModuleVersion => "2.0.0";
    public override string ModuleAuthor => "K4ryuu, Marchand";

    public static PluginCapability<IK4WorldTextSharedAPI> Capability_SharedAPI { get; } = new("k4-worldtext:sharedapi");

    public override void Load(bool hotReload)
    {
        Capabilities.RegisterPluginCapability(Capability_SharedAPI, () => new GameTextAPIHandler(this));

        RegisterListener<Listeners.OnMapEnd>(ClearData);

        RegisterEventHandler((EventRoundStart @event, GameEventInfo info) =>
        {
            foreach (var multilineWorldText in _multilineWorldTexts.Values)
            {
                if (multilineWorldText.HasValidEntities)
                    continue;

                try
                {
                    multilineWorldText.Update();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Round-start update of WorldText {Id} failed.", multilineWorldText.Id);
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

    public void ClearData()
    {
        foreach (var multilineWorldText in _multilineWorldTexts.Values)
            multilineWorldText.Dispose();
        _multilineWorldTexts.Clear();
    }

    public int SpawnMultipleLines(CCSPlayerController player, TextPlacement placement, List<TextLine> lines)
    {
        var multilineWorldText = new MultilineWorldText(this, lines);
        TrackWorldText(multilineWorldText);

        OnGameThread($"AddWorldTextAtPlayer({multilineWorldText.Id})", () =>
        {
            if (multilineWorldText.IsDisposed)
                return;

            var AbsOrigin = Vector.Zero;
            var AbsRotation = QAngle.Zero;
            var tempRotation = GetNormalizedAngles(player);
            switch (placement)
            {
                case TextPlacement.Wall:
                    AbsOrigin = GetPlayerPosition(player, lines);
                    AbsRotation = new QAngle(tempRotation.X, tempRotation.Y + 270, tempRotation.Z + 90);
                    break;
                case TextPlacement.Floor:
                    AbsOrigin = player.PlayerPawn.Value!.AbsOrigin!.With(z: player.PlayerPawn.Value!.AbsOrigin!.Z + 1);
                    AbsRotation = new QAngle(tempRotation.X, tempRotation.Y + 270, tempRotation.Z);
                    break;
            }

            var direction = EntityFaceToDirection(player.PlayerPawn.Value!.AbsRotation!.Y);
            var offset = GetDirectionOffset(direction, 15);

            multilineWorldText.Spawn(AbsOrigin + offset, AbsRotation, placement);
        });

        return multilineWorldText.Id;
    }

    public static QAngle GetNormalizedAngles(CCSPlayerController player)
    {
        var AbsRotation = player.PlayerPawn.Value!.AbsRotation!;
        return new QAngle(
            AbsRotation.X,
            (float)Math.Round(AbsRotation.Y / 10.0) * 10,
            AbsRotation.Z
        );
    }

    public static Vector GetPlayerPosition(CCSPlayerController player, List<TextLine> lines)
    {
        var feet = player.PlayerPawn.Value!.AbsOrigin!;

        static float StepFor(TextLine l) => MathF.Max(10f, TextLineLimits.FontSize(l.FontSize) / 3f);

        float totalAdvance = lines.Sum(StepFor);
        float usedHeight   = lines.Count > 0 ? totalAdvance - StepFor(lines[^1]) : 0f;

        float lastStep     = lines.Count > 0 ? StepFor(lines[^1]) : 0f;
        float bottomPad    = MathF.Max(8f, lastStep * 0.5f);

        return new Vector(feet.X, feet.Y, feet.Z + usedHeight + bottomPad);
    }

    public string EntityFaceToDirection(float yaw)
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

    public Vector GetDirectionOffset(string direction, float offsetValue)
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

    public class GameTextAPIHandler : IK4WorldTextSharedAPI
    {
        private readonly Plugin _plugin;

        public GameTextAPIHandler(Plugin plugin)
        {
            _plugin = plugin;
        }

        private MultilineWorldText RequireWorldText(int id) =>
            _plugin.FindWorldText(id) ?? throw new KeyNotFoundException($"WorldText with ID {id} not found.");

        private static List<TextLine> ValidateLines(List<TextLine>? textLines, string paramName)
        {
            ArgumentNullException.ThrowIfNull(textLines, paramName);
            if (textLines.Count == 0)
                throw new ArgumentException("At least one TextLine is required.", paramName);

            foreach (var line in textLines)
            {
                if (line is null)
                    throw new ArgumentException("TextLine entries must not be null.", paramName);
                if (line.Text is null)
                    throw new ArgumentException("TextLine.Text must not be null.", paramName);
            }

            return new List<TextLine>(textLines);
        }

        private static void ValidatePlacement(TextPlacement placement)
        {
            if (!Enum.IsDefined(placement))
                throw new ArgumentOutOfRangeException(nameof(placement), placement, "Unknown TextPlacement value.");
        }

        private static (Vector Position, QAngle Angle) CopyTransform(Vector? position, QAngle? angle)
        {
            ArgumentNullException.ThrowIfNull(position);
            ArgumentNullException.ThrowIfNull(angle);
            return (new Vector(position.X, position.Y, position.Z), new QAngle(angle.X, angle.Y, angle.Z));
        }

        public int AddWorldText(TextPlacement placement, TextLine textLine, Vector position, QAngle angle)
        {
            ArgumentNullException.ThrowIfNull(textLine);
            return AddWorldText(placement, new List<TextLine> { textLine }, position, angle);
        }

        public int AddWorldText(TextPlacement placement, List<TextLine> textLines, Vector position, QAngle angle)
        {
            ValidatePlacement(placement);
            var (spawnPosition, spawnAngle) = CopyTransform(position, angle);
            var lines = ValidateLines(textLines, nameof(textLines));

            var multilineWorldText = new MultilineWorldText(_plugin, lines);
            _plugin.TrackWorldText(multilineWorldText);

            _plugin.OnGameThread($"AddWorldText({multilineWorldText.Id})", () =>
            {
                if (multilineWorldText.IsDisposed)
                    return;

                multilineWorldText.Spawn(spawnPosition, spawnAngle, placement);
            });

            return multilineWorldText.Id;
        }

        public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, TextLine textLine)
        {
            ArgumentNullException.ThrowIfNull(textLine);
            return AddWorldTextAtPlayer(player, placement, new List<TextLine> { textLine });
        }

        public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, List<TextLine> textLines)
        {
            ArgumentNullException.ThrowIfNull(player);
            ValidatePlacement(placement);
            var lines = ValidateLines(textLines, nameof(textLines));

            return _plugin.SpawnMultipleLines(player, placement, lines);
        }

        public void UpdateWorldText(int id, TextLine? textLine = null)
        {
            UpdateWorldText(id, textLine is null ? null : new List<TextLine> { textLine });
        }

        public void UpdateWorldText(int id, List<TextLine>? textLines = null)
        {
            var lines = textLines is null ? null : ValidateLines(textLines, nameof(textLines));
            var target = RequireWorldText(id);

            _plugin.OnGameThread($"UpdateWorldText({id})", () =>
            {
                if (target.IsDisposed)
                    return;

                target.Update(lines);
            });
        }

        public void RemoveWorldText(int id)
        {
            var target = RequireWorldText(id);

            _plugin.UntrackWorldText(target);
            _plugin.OnGameThread($"RemoveWorldText({id})", target.Dispose);
        }

        public List<CPointWorldText>? GetWorldTextLineEntities(int id)
        {
            var target = _plugin.FindWorldText(id);
            if (target is null)
                return null;

            return target.Texts
                .Select(t => t.Entity)
                .Where(e => e?.IsValid == true)
                .Cast<CPointWorldText>()
                .ToList();
        }

        public void TeleportWorldText(int id, Vector position, QAngle angle)
        {
            var (newPosition, newAngle) = CopyTransform(position, angle);
            var target = RequireWorldText(id);

            _plugin.OnGameThread($"TeleportWorldText({id})", () =>
            {
                if (target.IsDisposed)
                    return;

                target.Teleport(newPosition, newAngle);
            });
        }

        public void RemoveAll()
        {
            var targets = _plugin.MultilineWorldTexts;
            foreach (var multilineWorldText in targets)
                _plugin.UntrackWorldText(multilineWorldText);

            _plugin.OnGameThread("RemoveAll", () =>
            {
                foreach (var multilineWorldText in targets)
                    multilineWorldText.Dispose();
            });
        }
    }
}