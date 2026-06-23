using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using K4WorldTextSharedAPI;

namespace K4ryuuCS2WorldTextAPI;

[MinimumApiVersion(369)]
public class Plugin : BasePlugin
{
    public List<MultilineWorldText> multilineWorldTexts = new();
    public override string ModuleName => "CS2 WorldText API";
    public override string ModuleVersion => "1.2.6";
    public override string ModuleAuthor => "K4ryuu (updated by Marchand)";

    public static PluginCapability<IK4WorldTextSharedAPI> Capability_SharedAPI { get; } = new("k4-worldtext:sharedapi");

    public override void Load(bool hotReload)
    {
        Capabilities.RegisterPluginCapability(Capability_SharedAPI, () => new GameTextAPIHandler(this));

        RegisterEventHandler((EventRoundStart @event, GameEventInfo info) =>
        {
            multilineWorldTexts.ForEach(multilineWorldText => multilineWorldText.Update());
            return HookResult.Continue;
        });
    }

    public override void Unload(bool hotReload)
    {
        ClearData();
    }

    public void ClearData()
    {
        multilineWorldTexts.ForEach(multilineWorldText => multilineWorldText.Dispose());
        multilineWorldTexts.Clear();
    }

    public int SpawnMultipleLines(CCSPlayerController player, TextPlacement placement, List<TextLine> lines, bool saveConfig = false)
    {
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

        var multilineWorldText = new MultilineWorldText(this, lines, saveConfig);
        multilineWorldText.Spawn(AbsOrigin + offset, AbsRotation, placement);

        multilineWorldTexts.Add(multilineWorldText);
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
            multilineWorldText.Spawn(position, angle, placement);

            _plugin.multilineWorldTexts.Add(multilineWorldText);
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
            var target = _plugin.multilineWorldTexts.Find(wt => wt.Id == id);
            if (target is null)
                throw new Exception($"WorldText with ID {id} not found.");

            target.Update(textLines);
        }

        public void RemoveWorldText(int id, bool removeFromConfig = true)
        {
            var target = _plugin.multilineWorldTexts.Find(wt => wt.Id == id);
            if (target is null)
                throw new Exception($"WorldText with ID {id} not found.");

            target.Dispose();
            _plugin.multilineWorldTexts.Remove(target);
        }

        public List<CPointWorldText>? GetWorldTextLineEntities(int id)
        {
            var target = _plugin.multilineWorldTexts.Find(wt => wt.Id == id);
            if (target is null)
                throw new Exception($"WorldText with ID {id} not found.");

            return target.Texts.Where(t => t.Entity != null).Select(t => t.Entity).Cast<CPointWorldText>().ToList();
        }

        public void TeleportWorldText(int id, Vector position, QAngle angle, bool modifyConfig = false)
        {
            var target = _plugin.multilineWorldTexts.Find(wt => wt.Id == id);
            if (target is null)
                throw new Exception($"WorldText with ID {id} not found.");

            target.Teleport(position, angle, modifyConfig);
        }

        public void RemoveAllTemporary()
        {
            _plugin.multilineWorldTexts.Where(wt => !wt.SaveToConfig).ToList()
                .ForEach(multilineWorldText => multilineWorldText.Dispose());
        }
    }
}