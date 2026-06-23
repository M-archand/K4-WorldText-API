using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using K4ryuuCS2WorldTextAPI;
using K4WorldTextSharedAPI;

public class MultilineWorldText : IDisposable
{
    private static int nextId = 1;

    private bool disposed;
    public TextPlacement placement;
    private readonly Plugin Plugin;

    public Vector? SpawnOrigin;
    public QAngle? SpawnRotation;
    private CPointWorldText? _blockBackground;

    public MultilineWorldText(Plugin plugin, List<TextLine> lines, bool save = false)
    {
        Plugin = plugin;

        Id = nextId++;
        Lines = lines;
        SaveToConfig = save;
    }

    public int Id { get; }
    public List<TextLine> Lines { get; private set; } = new();
    public List<WorldText> Texts { get; } = new();
    public bool SaveToConfig { get; set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private static TextLine CloneWithoutBackground(TextLine s) => new TextLine
    {
        Text = s.Text,
        Color = s.Color,
        FontSize = s.FontSize,
        FontName = s.FontName,
        FullBright = s.FullBright,
        JustifyHorizontal = s.JustifyHorizontal,
        JustifyVertical = s.JustifyVertical,
        ReorientMode = s.ReorientMode,
        Scale = s.Scale,
        ForegroundDepthOffset = s.ForegroundDepthOffset,
        BackgroundEnabled = false
    };

    public void Teleport(Vector absOrigin, QAngle absRotation, bool modifyConfig = false)
    {
        Remove();
        Spawn(absOrigin, absRotation, placement);
    }

    private static string MakeBlockMask(
        IReadOnlyList<string> lines,
        int maxCharsPerLineCap,
        float inflation,
        int padChars,
        out int usedWidthChars,
        out int desiredWidthChars)
    {
        const char nbsp = '\u00A0';
        const int bytesPerChar = 2;

        int lineCount = Math.Max(1, lines.Count);

        int longest = lines.Count == 0 ? 1 : Math.Max(1, lines.Max(s => s.Length));

        desiredWidthChars = Math.Max(1,
            Math.Min(maxCharsPerLineCap, (int)Math.Ceiling(longest * inflation) + (padChars * 2)));

        int newlineBytes     = lineCount - 1;
        int totalByteBudget  = 512;
        int budgetForNbsp    = Math.Max(1, totalByteBudget - newlineBytes);

        int safePerLine = Math.Max(1, budgetForNbsp / (lineCount * bytesPerChar));

        usedWidthChars = Math.Min(desiredWidthChars, safePerLine);

        string row = new string(nbsp, usedWidthChars);
        return string.Join('\n', Enumerable.Repeat(row, lineCount));
    }

    public void Spawn(Vector absOrigin, QAngle absRotation, TextPlacement placement)
    {
        this.placement = placement;

        static float StepFor(TextLine l) => MathF.Max(10f, l.FontSize / 3f);

        float totalAdvance = Lines.Sum(StepFor);
        float usedHeight = Lines.Count > 0 ? totalAdvance - StepFor(Lines[^1]) : 0f;
        float halfHeight = usedHeight * 0.5f;

        bool blockBg = Lines.Any(l => l.BackgroundEnabled && l.BackgroundAsSingleBlock);

        if (blockBg)
        {
            var cfg = Lines.First(l => l.BackgroundEnabled);
            var refLine = Lines.FirstOrDefault(l => !l.BackgroundEnabled) ?? Lines[0];
            int backgroundFontSize = refLine.FontSize + 3;
            float blockBorderWidth = cfg.BackgroundBorderWidth;

            var bg = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext")
                    ?? throw new Exception("Failed to create block background.");

            var foregroundTexts = Lines.Select(l => l.Text).ToList();
            int usedWidthChars, desiredWidthChars;
            bg.MessageText = MakeBlockMask(
                foregroundTexts,
                cfg.BackgroundMaxCharsPerLine,
                cfg.BackgroundWidthInflation,
                cfg.BackgroundPadChars,
                out usedWidthChars,
                out desiredWidthChars
            );

            if (desiredWidthChars > usedWidthChars && usedWidthChars > 0)
            {
                float ratio = (float)desiredWidthChars / usedWidthChars;
                blockBorderWidth *= ratio;
            }

            bg.Enabled = true;
            bg.FontName = refLine.FontName;
            bg.FontSize = backgroundFontSize;
            bg.Fullbright = true;
            bg.Color = cfg.BackgroundColor;
            float requestedBgScale = refLine.Scale;
            if (cfg.BackgroundScale > 0f && cfg.BackgroundScale < requestedBgScale)
                requestedBgScale = cfg.BackgroundScale;

            float maxBgScaleForStep = StepFor(refLine) / MathF.Max(1f, backgroundFontSize);
            float effectiveBgScale = MathF.Min(requestedBgScale, maxBgScaleForStep);

            bg.WorldUnitsPerPx = effectiveBgScale;
            bg.JustifyHorizontal = refLine.JustifyHorizontal;
            bg.JustifyVertical   = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;
            bg.ReorientMode = refLine.ReorientMode;
            bg.DrawBackground = true;
            bg.BackgroundBorderWidth  = blockBorderWidth;
            bg.BackgroundBorderHeight = cfg.BackgroundBorderHeight;
            bg.BackgroundWorldToUV    = cfg.BackgroundWorldToUV;
            bg.DepthOffset = cfg.BackgroundDepthOffset;

            // Center the background
            Vector bgOrigin;
            switch (placement)
            {
                case TextPlacement.Wall:
                    bgOrigin = absOrigin.With(z: absOrigin.Z - halfHeight);
                    break;
                case TextPlacement.Floor:
                    var dir = Plugin.EntityFaceToDirection(absRotation.Y - 270);
                    var off = Plugin.GetDirectionOffset(dir, halfHeight);
                    bgOrigin = absOrigin - off;
                    break;
                default:
                    bgOrigin = absOrigin;
                    break;
            }

            bg.Teleport(bgOrigin, absRotation, null);
            bg.DispatchSpawn();
            _blockBackground = bg;
        }

        WorldText? lastSpanedText = null;
        float currentHeight = 0f;

        foreach (var line in Lines)
        {
            TextLine ln = line;
            if (blockBg && line.BackgroundEnabled)
            {
                ln = new TextLine
                {
                    Text = line.Text,
                    Color = line.Color,
                    FontSize = line.FontSize,
                    FontName = line.FontName,
                    FullBright = line.FullBright,
                    Scale = line.Scale,
                    JustifyHorizontal = line.JustifyHorizontal,
                    JustifyVertical = line.JustifyVertical,
                    ReorientMode = line.ReorientMode,
                    ForegroundDepthOffset = line.ForegroundDepthOffset,
                    BackgroundEnabled = false
                };
            }

            switch (placement)
            {
                case TextPlacement.Wall:
                    Texts.Add(new WorldText(
                        Plugin,
                        absOrigin.With(z: absOrigin.Z - currentHeight),
                        absRotation,
                        ln));
                    break;

                case TextPlacement.Floor:
                    if (lastSpanedText?.Entity != null)
                    {
                        var dir = Plugin.EntityFaceToDirection(lastSpanedText.Entity.AbsRotation!.Y - 270);
                        var off = Plugin.GetDirectionOffset(dir, currentHeight);
                        lastSpanedText = new WorldText(Plugin, absOrigin - off, absRotation, ln);
                    }
                    else
                    {
                        lastSpanedText = new WorldText(Plugin, absOrigin, absRotation, ln);
                    }
                    Texts.Add(lastSpanedText);
                    break;
            }

            currentHeight += StepFor(line);
        }

        SpawnOrigin = Texts[0].AbsOrigin;
        SpawnRotation = Texts[0].AbsRotation;
    }

    public void Update(List<TextLine>? lines = null)
    {
        Remove();

        if (lines != null)
            Lines = lines;

        if (SpawnOrigin != null && SpawnRotation != null)
            Spawn(SpawnOrigin, SpawnRotation, placement);
    }

    public void Remove()
    {
        if (Texts.Count > 0)
            Texts.ForEach(text => text.Remove());
        if (_blockBackground?.IsValid == true)
            _blockBackground.Remove();
        _blockBackground = null;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
                Remove();

            disposed = true;
        }
    }
}