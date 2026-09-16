using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using K4WorldTextSharedAPI;

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

    internal static void OnGameThread(Action action) => Server.NextWorldUpdate(action);

    public override string ModuleName => "K4-WorldText-API";
    public override string ModuleVersion => "1.3.0";
    public override string ModuleAuthor => "K4ryuu, Marchand";

    public static PluginCapability<IK4WorldTextSharedAPI> Capability_SharedAPI { get; } = new("k4-worldtext:sharedapi");

    public override void Load(bool hotReload)
    {
        Capabilities.RegisterPluginCapability(Capability_SharedAPI, () => new GameTextAPIHandler(this));

        RegisterListener<Listeners.OnMapEnd>(ClearData);

        RegisterEventHandler((EventRoundStart @event, GameEventInfo info) =>
        {
            foreach (var multilineWorldText in _multilineWorldTexts.Values)
                multilineWorldText.Update();
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

    public int SpawnMultipleLines(CCSPlayerController player, TextPlacement placement, List<TextLine> lines, bool saveConfig = false)
    {
        var multilineWorldText = new MultilineWorldText(this, lines, saveConfig);
        TrackWorldText(multilineWorldText);

        OnGameThread(() =>
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

        static float StepFor(TextLine l) => MathF.Max(10f, l.FontSize / 3f);

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

        public int AddWorldText(TextPlacement placement, TextLine textLine, Vector position, QAngle angle,
            bool saveConfig = false)
        {
            return AddWorldText(placement, new List<TextLine> { textLine }, position, angle);
        }

        public int AddWorldText(TextPlacement placement, List<TextLine> textLines, Vector position, QAngle angle,
            bool saveConfig = false)
        {
            var multilineWorldText = new MultilineWorldText(_plugin, textLines, saveConfig);
            _plugin.TrackWorldText(multilineWorldText);

            OnGameThread(() =>
            {
                if (multilineWorldText.IsDisposed)
                    return;

                multilineWorldText.Spawn(position, angle, placement);
            });

            return multilineWorldText.Id;
        }

        public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, TextLine textLine,
            bool saveConfig = false)
        {
            return AddWorldTextAtPlayer(player, placement, new List<TextLine> { textLine });
        }

        public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, List<TextLine> textLines,
            bool saveConfig = false)
        {
            return _plugin.SpawnMultipleLines(player, placement, textLines, saveConfig);
        }

        public void UpdateWorldText(int id, TextLine? textLine = null)
        {
            UpdateWorldText(id, textLine is null ? null : new List<TextLine> { textLine });
        }

        public void UpdateWorldText(int id, List<TextLine>? textLines = null)
        {
            var target = _plugin.FindWorldText(id);
            if (target is null)
                throw new Exception($"WorldText with ID {id} not found.");

            OnGameThread(() =>
            {
                if (target.IsDisposed)
                    return;

                target.Update(textLines);
            });
        }

        public void RemoveWorldText(int id, bool removeFromConfig = true)
        {
            var target = _plugin.FindWorldText(id);
            if (target is null)
                throw new Exception($"WorldText with ID {id} not found.");

            _plugin.UntrackWorldText(target);
            OnGameThread(target.Dispose);
        }

        public List<CPointWorldText>? GetWorldTextLineEntities(int id)
        {
            var target = _plugin.FindWorldText(id);
            if (target is null)
                throw new Exception($"WorldText with ID {id} not found.");

            return target.Texts.Where(t => t.Entity != null).Select(t => t.Entity).Cast<CPointWorldText>().ToList();
        }

        public void TeleportWorldText(int id, Vector position, QAngle angle, bool modifyConfig = false)
        {
            var target = _plugin.FindWorldText(id);
            if (target is null)
                throw new Exception($"WorldText with ID {id} not found.");

            OnGameThread(() =>
            {
                if (target.IsDisposed)
                    return;

                target.Teleport(position, angle, modifyConfig);
            });
        }

        public void RemoveAllTemporary()
        {
            var targets = _plugin.MultilineWorldTexts.Where(wt => !wt.SaveToConfig).ToList();
            foreach (var multilineWorldText in targets)
                _plugin.UntrackWorldText(multilineWorldText);

            OnGameThread(() =>
            {
                foreach (var multilineWorldText in targets)
                    multilineWorldText.Dispose();
            });
        }
    }
}