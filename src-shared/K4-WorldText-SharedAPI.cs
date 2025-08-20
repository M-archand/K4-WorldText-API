using System.Drawing;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace K4WorldTextSharedAPI;

public enum TextPlacement
{
    Wall,
    Floor
}

public class TextLine
{
    public Color Color = Color.White;
    public int FontSize = 20;
    public string FontName = "Arial Bold";
    public bool FullBright = true;

    public PointWorldTextJustifyHorizontal_t JustifyHorizontal =
        PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER;

    public PointWorldTextJustifyVertical_t JustifyVertical =
        PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;

    public PointWorldTextReorientMode_t ReorientMode = PointWorldTextReorientMode_t.POINT_WORLD_TEXT_REORIENT_NONE;
    public float Scale = 0.4f;
    public required string Text;

    /// <summary>Enable the extra background entity behind this line.</summary>
    public bool BackgroundEnabled = false;

    /// <summary>Color for the background entity.</summary>
    public Color BackgroundColor = Color.FromArgb(200, 127, 127, 127);

    /// <summary>Whether the background entity is fullbright.</summary>
    public bool BackgroundFullBright = true;

    /// <summary>Background scale (world units per pixel). Usually match foreground.</summary>
    public float BackgroundScale = 0.4f;

    /// <summary>Background rectangle UV scale (higher = denser texture; vanilla uses ~0.05).</summary>
    public float BackgroundWorldToUV = 0.05f;

    /// <summary>Width padding around the text background (world units).</summary>
    public float BackgroundBorderWidth = 0.1f;

    /// <summary>Height padding around the text background (world units).</summary>
    public float BackgroundBorderHeight = 0.1f;

    /// <summary>Depth offset for the foreground (0 keeps existing behavior).</summary>
    public float ForegroundDepthOffset = 0.0f;

    /// <summary>Depth offset for the background (negative puts it "behind").</summary>
    public float BackgroundDepthOffset = -0.0015f;

    /// <summary>Hide background text</summary>
    public bool BackgroundHideText = true;

    /// <summary>Background as single entity</summary>
    public bool BackgroundAsSingleBlock { get; set; } = false;

    /// <summary>Limit characters per line (point_worldtext has a limit of 512 total).</summary>
    public int BackgroundMaxCharsPerLine { get; set; } = 32;

    /// <summary>How much wider than the raw character count to make the mask (compensates for proportional fonts; 1.35–1.60 typical)</summary>
    public float BackgroundWidthInflation { get; set; } = 1.0f;

    /// <summary> Extra invisible "character" padding on each side of the mask</summary>
    public int BackgroundPadChars { get; set; } = 2;
}

public interface IK4WorldTextSharedAPI
{
    public int AddWorldText(TextPlacement placement, TextLine textLine, Vector position, QAngle angle,
        bool saveConfig = false);

    public int AddWorldText(TextPlacement placement, List<TextLine> textLines, Vector position, QAngle angle,
        bool saveConfig = false);

    public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, TextLine textLine,
        bool saveConfig = false);

    public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, List<TextLine> textLines,
        bool saveConfig = false);

    public void UpdateWorldText(int id, TextLine? textLine = null);
    public void UpdateWorldText(int id, List<TextLine>? textLines = null);
    public void RemoveWorldText(int id, bool removeFromConfig = true);
    public List<CPointWorldText>? GetWorldTextLineEntities(int id);
    public void TeleportWorldText(int id, Vector position, QAngle angle, bool modifyConfig = false);
    public void RemoveAllTemporary();
}