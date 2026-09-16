using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using K4ryuuCS2WorldTextAPI;
using K4WorldTextSharedAPI;

public class MultilineWorldText : IDisposable
{
    private static int nextId = 0;

    internal static void ResetIds() => Interlocked.Exchange(ref nextId, 0);

    private bool disposed;
    public bool IsDisposed => disposed;

    public TextPlacement placement;
    private readonly Plugin Plugin;

    public Vector? SpawnOrigin;
    public QAngle? SpawnRotation;
    private CPointWorldText? _blockBackground;

    public MultilineWorldText(Plugin plugin, string owner, List<TextLine> lines)
    {
        Plugin = plugin;
        Owner = owner;

        Id = Interlocked.Increment(ref nextId);
        Lines = lines;
    }

    public int Id { get; }
    public string Owner { get; }
    public List<TextLine> Lines { get; private set; } = new();
    public List<WorldText> Texts { get; } = new();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
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

    private static bool UsesBlockBackground(List<TextLine> lines) =>
        lines.Any(l => l.BackgroundEnabled && l.BackgroundAsSingleBlock);

    private static TextLine ForegroundLine(TextLine line, bool blockBg) =>
        blockBg && line.BackgroundEnabled ? CloneWithoutBackground(line) : line;

    private static float StepFor(TextLine l) => MathF.Max(10f, TextLineLimits.FontSize(l.FontSize) / 3f);

    public void Teleport(Vector absOrigin, QAngle absRotation)
    {
        ThrowIfDisposed();

        if (TryTeleportInPlace(absOrigin, absRotation))
            return;

        Remove();
        Spawn(absOrigin, absRotation, placement);
    }

    private bool TryTeleportInPlace(Vector absOrigin, QAngle absRotation)
    {
        if (Texts.Count == 0 || !HasValidEntities)
            return false;

        var floorDir = Plugin.EntityFaceToDirection(absRotation.Y - 270);

        if (_blockBackground?.IsValid == true)
            _blockBackground.Teleport(LineOrigin(absOrigin, floorDir, UsedHeight() * 0.5f), absRotation, null);

        float currentHeight = 0f;
        for (int i = 0; i < Texts.Count; i++)
        {
            Texts[i].Teleport(LineOrigin(absOrigin, floorDir, currentHeight), absRotation);
            currentHeight += StepFor(Lines[i]);
        }

        SpawnOrigin = Texts[0].AbsOrigin;
        SpawnRotation = Texts[0].AbsRotation;
        return true;
    }

    // Vertical extent covered by the lines, from the first baseline to the last
    private float UsedHeight() =>
        Lines.Count > 0 ? Lines.Sum(StepFor) - StepFor(Lines[^1]) : 0f;

    // Origin of a line `distance` units down the stack: along -Z on a wall, along the facing direction on the floor.
    private Vector LineOrigin(Vector absOrigin, string floorDir, float distance) => placement switch
    {
        TextPlacement.Wall => absOrigin.With(z: absOrigin.Z - distance),
        TextPlacement.Floor => absOrigin - Plugin.GetDirectionOffset(floorDir, distance),
        _ => absOrigin
    };

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
        int totalByteBudget  = WorldText.MessageTextByteBudget;
        int budgetForNbsp    = Math.Max(1, totalByteBudget - newlineBytes);

        int safePerLine = Math.Max(1, budgetForNbsp / (lineCount * bytesPerChar));

        usedWidthChars = Math.Min(desiredWidthChars, safePerLine);

        string row = new string(nbsp, usedWidthChars);
        return WorldText.ClampToMessageTextBudget(string.Join('\n', Enumerable.Repeat(row, lineCount)));
    }

    private static void ApplyBlockBackground(CPointWorldText bg, List<TextLine> lines)
    {
        var cfg = lines.First(l => l.BackgroundEnabled);
        var refLine = lines.FirstOrDefault(l => !l.BackgroundEnabled) ?? lines[0];
        int backgroundFontSize = TextLineLimits.FontSize(refLine.FontSize) + 3;
        float blockBorderWidth = TextLineLimits.Border(cfg.BackgroundBorderWidth);

        var foregroundTexts = lines.Select(l => l.Text).ToList();
        int usedWidthChars, desiredWidthChars;
        bg.MessageText = MakeBlockMask(
            foregroundTexts,
            TextLineLimits.CharsPerLine(cfg.BackgroundMaxCharsPerLine),
            TextLineLimits.WidthInflation(cfg.BackgroundWidthInflation),
            TextLineLimits.PadChars(cfg.BackgroundPadChars),
            out usedWidthChars,
            out desiredWidthChars
        );

        if (desiredWidthChars > usedWidthChars && usedWidthChars > 0)
        {
            float ratio = (float)desiredWidthChars / usedWidthChars;
            blockBorderWidth = TextLineLimits.Border(blockBorderWidth * ratio);
        }

        bg.Enabled = true;
        bg.FontName = TextLineLimits.FontName(refLine.FontName);
        bg.FontSize = backgroundFontSize;
        bg.Fullbright = true;
        bg.Color = cfg.BackgroundColor;
        float requestedBgScale = TextLineLimits.Scale(refLine.Scale);
        if (cfg.BackgroundScale > 0f)
            requestedBgScale = MathF.Min(requestedBgScale, TextLineLimits.Scale(cfg.BackgroundScale));

        float maxBgScaleForStep = StepFor(refLine) / MathF.Max(1f, backgroundFontSize);
        float effectiveBgScale = MathF.Min(requestedBgScale, maxBgScaleForStep);

        bg.WorldUnitsPerPx = effectiveBgScale;
        bg.JustifyHorizontal = refLine.JustifyHorizontal;
        bg.JustifyVertical   = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;
        bg.ReorientMode = refLine.ReorientMode;
        bg.DrawBackground = true;
        bg.BackgroundBorderWidth  = blockBorderWidth;
        bg.BackgroundBorderHeight = TextLineLimits.Border(cfg.BackgroundBorderHeight);
        bg.BackgroundWorldToUV    = TextLineLimits.WorldToUV(cfg.BackgroundWorldToUV);
        bg.DepthOffset            = TextLineLimits.DepthOffset(cfg.BackgroundDepthOffset);
    }

    public void Spawn(Vector absOrigin, QAngle absRotation, TextPlacement placement)
    {
        ThrowIfDisposed();

        try
        {
            SpawnEntities(absOrigin, absRotation, placement);
        }
        catch
        {
            Remove();
            throw;
        }
    }

    private void SpawnEntities(Vector absOrigin, QAngle absRotation, TextPlacement placement)
    {
        this.placement = placement;

        var floorDir = Plugin.EntityFaceToDirection(absRotation.Y - 270);
        bool blockBg = UsesBlockBackground(Lines);

        if (blockBg)
        {
            var bg = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext")
                    ?? throw new InvalidOperationException("Failed to create block background.");

            ApplyBlockBackground(bg, Lines);

            // Center the background on the stack of lines
            bg.Teleport(LineOrigin(absOrigin, floorDir, UsedHeight() * 0.5f), absRotation, null);
            bg.DispatchSpawn();
            _blockBackground = bg;
        }

        float currentHeight = 0f;

        foreach (var line in Lines)
        {
            Texts.Add(new WorldText(
                Plugin,
                LineOrigin(absOrigin, floorDir, currentHeight),
                absRotation,
                ForegroundLine(line, blockBg)));

            currentHeight += StepFor(line);
        }

        SpawnOrigin = Texts[0].AbsOrigin;
        SpawnRotation = Texts[0].AbsRotation;
    }

    public bool HasValidEntities
    {
        get
        {
            if (Texts.Count != Lines.Count)
                return false;

            if (UsesBlockBackground(Lines) && _blockBackground?.IsValid != true)
                return false;

            foreach (var text in Texts)
            {
                if (!text.HasValidEntities)
                    return false;
            }

            return true;
        }
    }

    private bool CanUpdateInPlace(List<TextLine>? lines)
    {
        var next = lines ?? Lines;

        if (next.Count == 0 || next.Count != Lines.Count || Texts.Count != Lines.Count)
            return false;

        bool blockBg = UsesBlockBackground(Lines);
        if (blockBg != UsesBlockBackground(next))
            return false;

        if (blockBg && _blockBackground?.IsValid != true)
            return false;

        for (int i = 0; i < next.Count; i++)
        {
            var prev = Lines[i];
            var cur = next[i];

            if (prev.FontSize != cur.FontSize)
                return false;
            if (prev.BackgroundEnabled != cur.BackgroundEnabled)
                return false;
            if (prev.BackgroundAsSingleBlock != cur.BackgroundAsSingleBlock)
                return false;

            if (!Texts[i].CanUpdateInPlace(ForegroundLine(cur, blockBg)))
                return false;
        }

        return true;
    }

    private bool TryUpdateInPlace(List<TextLine>? lines)
    {
        if (!CanUpdateInPlace(lines))
            return false;

        var next = lines ?? Lines;
        bool blockBg = UsesBlockBackground(next);

        for (int i = 0; i < next.Count; i++)
        {
            if (!Texts[i].TryUpdateInPlace(ForegroundLine(next[i], blockBg)))
                return false;
        }

        if (blockBg)
        {
            ApplyBlockBackground(_blockBackground!, next);
            WorldText.NotifyChanged(_blockBackground!, background: true);
        }

        Lines = next;
        return true;
    }

    public void Update(List<TextLine>? lines = null)
    {
        ThrowIfDisposed();

        if (TryUpdateInPlace(lines))
            return;

        if (lines != null)
            Lines = lines;

        Respawn();
    }

    public void RespawnIfInvalid()
    {
        ThrowIfDisposed();

        if (HasValidEntities)
            return;

        Respawn();
    }

    private void Respawn()
    {
        Remove();

        if (SpawnOrigin != null && SpawnRotation != null)
            Spawn(SpawnOrigin, SpawnRotation, placement);
    }

    public void Remove()
    {
        Texts.ForEach(text => text.Dispose());
        Texts.Clear();
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