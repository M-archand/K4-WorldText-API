using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using K4ryuuCS2WorldTextAPI;
using K4WorldTextSharedAPI;

public class WorldText : IDisposable
{
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

    private static string MaskLine(string text)
    {
        const char nbsp = '\u00A0';
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
            sb.Append(ch == '\n' ? '\n' : nbsp);
        return sb.ToString();
    }

    public void Spawn()
    {
        if (Data.BackgroundEnabled)
        {
            var bg = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext")
                    ?? throw new Exception("Failed to create background point_worldtext Entity.");

            bg.MessageText = Data.BackgroundHideText ? MaskLine(Data.Text) : Data.Text;

            bg.Enabled = true;
            bg.FontName = Data.FontName;
            bg.FontSize = Data.FontSize;
            bg.Fullbright = Data.BackgroundFullBright;
            bg.Color = Data.BackgroundColor;
            bg.WorldUnitsPerPx = Data.BackgroundScale;
            bg.DepthOffset = Data.BackgroundDepthOffset;
            bg.JustifyHorizontal = Data.JustifyHorizontal;
            bg.JustifyVertical = Data.JustifyVertical;
            bg.ReorientMode = Data.ReorientMode;

            bg.DrawBackground = true;
            bg.BackgroundBorderWidth  = Data.BackgroundBorderWidth;
            bg.BackgroundBorderHeight = Data.BackgroundBorderHeight;
            bg.BackgroundWorldToUV    = Data.BackgroundWorldToUV;

            bg.Teleport(AbsOrigin, AbsRotation);
            bg.DispatchSpawn();

            BackgroundEntity = bg;
        }

        var ent = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");
        if (ent is null)
            throw new Exception("Failed to create point_worldtext Entity.");

        ent.MessageText = Data.Text;
        ent.Enabled = true;
        ent.FontName = Data.FontName;
        ent.FontSize = Data.FontSize;
        ent.Color = Data.Color;
        ent.Fullbright = Data.FullBright;
        ent.WorldUnitsPerPx = Data.Scale;
        ent.DepthOffset = Data.ForegroundDepthOffset;
        ent.JustifyHorizontal = Data.JustifyHorizontal;
        ent.JustifyVertical = Data.JustifyVertical;
        ent.ReorientMode = Data.ReorientMode;

        ent.Teleport(AbsOrigin, AbsRotation);
        ent.DispatchSpawn();

        Entity = ent;
    }

    public void Update(TextLine? data = null)
    {
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