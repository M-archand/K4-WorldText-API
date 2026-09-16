using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using K4ryuuCS2WorldTextAPI;
using K4WorldTextSharedAPI;

public class WorldText : IDisposable
{
    private const string SchemaClass = "CPointWorldText";

    private bool disposed;
    public Plugin Plugin;

    public WorldText(Plugin plugin, Vector absOrigin, QAngle absRotation, TextLine data)
    {
        Plugin = plugin;
        AbsOrigin = absOrigin;
        AbsRotation = absRotation;
        Data = data;

        Spawn();
    }

    public CPointWorldText? Entity { get; private set; }

    public CPointWorldText? BackgroundEntity { get; private set; }

    public TextLine Data { get; set; }
    public Vector AbsOrigin { get; set; }
    public QAngle AbsRotation { get; set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    internal const int MessageTextByteBudget = 512;

    internal static string ClampToMessageTextBudget(string? text) =>
        ClampToByteBudget(text, MessageTextByteBudget);

    internal static string ClampToByteBudget(string? text, int byteBudget)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (Encoding.UTF8.GetByteCount(text) <= byteBudget)
            return text;

        Span<byte> buffer = stackalloc byte[byteBudget];
        Encoding.UTF8.GetEncoder().Convert(text, buffer, flush: true, out int charsUsed, out _, out _);
        return text[..charsUsed];
    }

    private static string MaskLine(string text)
    {
        const char nbsp = '\u00A0';
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
            sb.Append(ch == '\n' ? '\n' : nbsp);
        return sb.ToString();
    }

    private static string BackgroundText(TextLine data) =>
        data.BackgroundHideText ? MaskLine(data.Text) : data.Text;

    private static void ApplyForeground(CPointWorldText ent, TextLine data)
    {
        ent.MessageText = ClampToMessageTextBudget(data.Text);
        ent.Enabled = true;
        ent.FontName = TextLineLimits.FontName(data.FontName);
        ent.FontSize = TextLineLimits.FontSize(data.FontSize);
        ent.Color = data.Color;
        ent.Fullbright = data.FullBright;
        ent.WorldUnitsPerPx = TextLineLimits.Scale(data.Scale);
        ent.DepthOffset = TextLineLimits.DepthOffset(data.ForegroundDepthOffset);
        ent.JustifyHorizontal = data.JustifyHorizontal;
        ent.JustifyVertical = data.JustifyVertical;
        ent.ReorientMode = data.ReorientMode;
    }

    private static void ApplyBackground(CPointWorldText bg, TextLine data)
    {
        bg.MessageText = ClampToMessageTextBudget(BackgroundText(data));
        bg.Enabled = true;
        bg.FontName = TextLineLimits.FontName(data.FontName);
        bg.FontSize = TextLineLimits.FontSize(data.FontSize);
        bg.Fullbright = data.BackgroundFullBright;
        bg.Color = data.BackgroundColor;
        bg.WorldUnitsPerPx = TextLineLimits.Scale(data.BackgroundScale);
        bg.DepthOffset = TextLineLimits.DepthOffset(data.BackgroundDepthOffset);
        bg.JustifyHorizontal = data.JustifyHorizontal;
        bg.JustifyVertical = data.JustifyVertical;
        bg.ReorientMode = data.ReorientMode;

        bg.DrawBackground = true;
        bg.BackgroundBorderWidth  = TextLineLimits.Border(data.BackgroundBorderWidth);
        bg.BackgroundBorderHeight = TextLineLimits.Border(data.BackgroundBorderHeight);
        bg.BackgroundWorldToUV    = TextLineLimits.WorldToUV(data.BackgroundWorldToUV);
    }

    internal static void NotifyChanged(CPointWorldText ent, bool background)
    {
        Utilities.SetStateChanged(ent, SchemaClass, "m_messageText");
        Utilities.SetStateChanged(ent, SchemaClass, "m_bEnabled");
        Utilities.SetStateChanged(ent, SchemaClass, "m_FontName");
        Utilities.SetStateChanged(ent, SchemaClass, "m_flFontSize");
        Utilities.SetStateChanged(ent, SchemaClass, "m_Color");
        Utilities.SetStateChanged(ent, SchemaClass, "m_bFullbright");
        Utilities.SetStateChanged(ent, SchemaClass, "m_flWorldUnitsPerPx");
        Utilities.SetStateChanged(ent, SchemaClass, "m_flDepthOffset");
        Utilities.SetStateChanged(ent, SchemaClass, "m_nJustifyHorizontal");
        Utilities.SetStateChanged(ent, SchemaClass, "m_nJustifyVertical");
        Utilities.SetStateChanged(ent, SchemaClass, "m_nReorientMode");

        if (!background)
            return;

        Utilities.SetStateChanged(ent, SchemaClass, "m_bDrawBackground");
        Utilities.SetStateChanged(ent, SchemaClass, "m_flBackgroundBorderWidth");
        Utilities.SetStateChanged(ent, SchemaClass, "m_flBackgroundBorderHeight");
        Utilities.SetStateChanged(ent, SchemaClass, "m_flBackgroundWorldToUV");
    }

    public void Spawn()
    {
        if (Data.BackgroundEnabled)
        {
            var bg = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext")
                    ?? throw new InvalidOperationException("Failed to create background point_worldtext Entity.");

            ApplyBackground(bg, Data);

            bg.Teleport(AbsOrigin, AbsRotation);
            bg.DispatchSpawn();

            BackgroundEntity = bg;
        }

        var ent = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");
        if (ent is null)
        {
            Remove();
            throw new InvalidOperationException("Failed to create point_worldtext Entity.");
        }

        ApplyForeground(ent, Data);

        ent.Teleport(AbsOrigin, AbsRotation);
        ent.DispatchSpawn();

        Entity = ent;
    }

    public bool CanUpdateInPlace(TextLine? data = null)
    {
        var next = data ?? Data;

        if (Entity?.IsValid != true)
            return false;

        bool hasBackground = BackgroundEntity?.IsValid == true;
        return next.BackgroundEnabled == hasBackground;
    }

    public bool TryUpdateInPlace(TextLine? data = null)
    {
        if (!CanUpdateInPlace(data))
            return false;

        if (data != null)
            Data = data;

        ApplyForeground(Entity!, Data);
        NotifyChanged(Entity!, background: false);

        if (BackgroundEntity is not null)
        {
            ApplyBackground(BackgroundEntity, Data);
            NotifyChanged(BackgroundEntity, background: true);
        }

        return true;
    }

    public void Update(TextLine? data = null)
    {
        if (TryUpdateInPlace(data))
            return;

        Remove();
        if (data != null) Data = data;
        Spawn();
    }

    public void Remove()
    {
        if (Entity?.IsValid == true)
            Entity.Remove();
        if (BackgroundEntity?.IsValid == true)
            BackgroundEntity.Remove();
        Entity = null;
        BackgroundEntity = null;
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