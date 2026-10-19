using System.Drawing;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace K4WorldTextSharedAPI;

/// <summary>Where a text is oriented in the world.</summary>
public enum TextPlacement
{
    /// <summary>Text stands upright on a vertical surface.</summary>
    Wall,

    /// <summary>Text lies flat on a horizontal surface.</summary>
    Floor
}

/// <summary>
/// One line of world text with its own styling and optional background panel. Numeric fields are clamped to the
/// documented ranges before they reach the entity, never rejected; <c>NaN</c> becomes the in-range value closest to zero.
/// The plugin reads a <see cref="TextLine"/> only when it is passed to an API call, so mutating one afterwards has no
/// effect until <see cref="IK4WorldTextSharedAPI.UpdateWorldText(int, TextLine?)"/> is called.
/// </summary>
public class TextLine
{
    /// <summary>Text color.</summary>
    public Color Color = Color.White;

    /// <summary>Font size in points. Clamped to 1–500.</summary>
    public int FontSize = 20;

    /// <summary>
    /// Font family name. Truncated to 64 UTF-8 bytes, the size of the entity's font-name field. Rendering happens on
    /// each client, and the client resolves the name through the game's own fontconfig setup, so an unknown name falls
    /// back to a default sans-serif face instead of failing.
    /// <para>
    /// Faces shipped with CS2 (<c>game/csgo/panorama/fonts/</c>). Stratum2 faces are named exactly as in the game's
    /// panorama <c>fonts.css</c>:
    /// <list type="bullet">
    /// <item><description><c>Stratum2</c>, <c>Stratum2 Thin</c>, <c>Stratum2 Light</c>, <c>Stratum2 Medium</c>, <c>Stratum2 Black</c></description></item>
    /// <item><description><c>Stratum2 Thin Italic</c>, <c>Stratum2 Light Italic</c>, <c>Stratum2 Medium Italic</c>, <c>Stratum2 Black Italic</c></description></item>
    /// <item><description><c>Stratum2 Condensed</c>, <c>Stratum2 Thin Condensed</c>, <c>Stratum2 Light Condensed</c>, <c>Stratum2 Medium Condensed</c>, <c>Stratum2 Bold Condensed</c>, <c>Stratum2 Black Condensed</c></description></item>
    /// <item><description><c>Stratum2 TF</c>, <c>Stratum2 Thin TF</c>, <c>Stratum2 Light TF</c>, <c>Stratum2 Medium TF</c>, <c>Stratum2 Bold TF</c>, <c>Stratum2 Black TF</c> (tabular figures)</description></item>
    /// <item><description><c>Stratum2 Mono</c>, <c>Stratum2 Mono Light</c>, <c>Stratum2 Regular Monodigit</c>, <c>Stratum2 Bold Monodigit</c></description></item>
    /// <item><description><c>Noto Sans</c>, <c>Noto Sans Bold</c>, <c>Noto Sans Italic</c>, <c>Noto Sans Bold Italic</c></description></item>
    /// <item><description><c>Noto Serif</c>, <c>Noto Serif Bold</c>, <c>Noto Serif Italic</c>, <c>Noto Serif Bold Italic</c></description></item>
    /// <item><description><c>Noto Mono</c>, <c>Noto Sans Symbols</c></description></item>
    /// <item><description><c>Noto Sans JP</c>, <c>Noto Sans KR</c>, <c>Noto Sans SC</c>, <c>Noto Sans TC</c>, <c>Noto Sans Thai</c>, each also as <c>... Light</c> and <c>... Bold</c></description></item>
    /// </list>
    /// The game's own CSS selects regular Stratum2 bold and italic through font weight and style, which
    /// <c>point_worldtext</c> has no field for, so use one of the named faces above for those.
    /// </para>
    /// <para>
    /// <c>Arial</c>, <c>Arial Bold</c>, <c>Tahoma</c>, <c>Verdana</c>, and <c>Courier New</c> are not shipped: the game
    /// loads them from the client's OS font directory, so they render only on clients that have them installed
    /// (Windows by default; Linux and macOS clients get a fontconfig substitute).
    /// </para>
    /// </summary>
    public string FontName = "Arial Bold";

    /// <summary>Render the text unlit, ignoring map lighting.</summary>
    public bool FullBright = true;

    /// <summary>Horizontal alignment of the text relative to its origin.</summary>
    public PointWorldTextJustifyHorizontal_t JustifyHorizontal =
        PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER;

    /// <summary>Vertical alignment of the text relative to its origin.</summary>
    public PointWorldTextJustifyVertical_t JustifyVertical =
        PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;

    /// <summary>Whether the text turns to face the viewer.</summary>
    public PointWorldTextReorientMode_t ReorientMode = PointWorldTextReorientMode_t.POINT_WORLD_TEXT_REORIENT_NONE;

    /// <summary>World units per pixel. Clamped to 0.01–10.</summary>
    public float Scale = 0.4f;

    /// <summary>Line text. Text longer than 512 UTF-8 bytes is truncated on a character boundary before it reaches the entity.</summary>
    public required string Text;

    /// <summary>Enable the extra background entity behind this line.</summary>
    public bool BackgroundEnabled = false;

    /// <summary>Color for the background entity.</summary>
    public Color BackgroundColor = Color.FromArgb(200, 127, 127, 127);

    /// <summary>Whether the background entity is fullbright.</summary>
    public bool BackgroundFullBright = true;

    /// <summary>Background scale (world units per pixel). Usually match foreground. Clamped to 0.01–10.</summary>
    public float BackgroundScale = 0.4f;

    /// <summary>Background rectangle UV scale (higher = denser texture; vanilla uses ~0.05). Clamped to 0.001–10.</summary>
    public float BackgroundWorldToUV = 0.05f;

    /// <summary>Width padding around the text background (world units). Clamped to 0–1000.</summary>
    public float BackgroundBorderWidth = 0.1f;

    /// <summary>Height padding around the text background (world units). Clamped to 0–1000.</summary>
    public float BackgroundBorderHeight = 0.1f;

    /// <summary>Depth offset for the foreground (0 keeps existing behavior). Clamped to ±100.</summary>
    public float ForegroundDepthOffset = 0.0f;

    /// <summary>Depth offset for the background (negative puts it "behind"). Clamped to ±100.</summary>
    public float BackgroundDepthOffset = -0.0015f;

    /// <summary>Hide background text</summary>
    public bool BackgroundHideText = true;

    /// <summary>Background as single entity</summary>
    public bool BackgroundAsSingleBlock { get; set; } = false;

    /// <summary>Limit characters per line (point_worldtext has a limit of 512 total). Clamped to 1–256.</summary>
    public int BackgroundMaxCharsPerLine { get; set; } = 32;

    /// <summary>How much wider than the raw character count to make the mask (compensates for proportional fonts; 1.35–1.60 typical). Clamped to 0.1–10.</summary>
    public float BackgroundWidthInflation { get; set; } = 1.0f;

    /// <summary>Extra invisible "character" padding on each side of the mask. Clamped to 0–256.</summary>
    public int BackgroundPadChars { get; set; } = 2;

    /// <summary>
    /// Spawn a <c>prop_dynamic_override</c> behind the text as a model background. Independent of the
    /// <see cref="BackgroundEnabled"/> panel: either, both, or neither may be on. One prop is spawned per text, centered
    /// on the stack of lines, for a multi-line text the first line with this enabled and a non-empty
    /// <see cref="ModelBackgroundPath"/> supplies the settings. Ignored while <see cref="ModelBackgroundPath"/> is empty.
    /// The prop spawns with collision disabled, so it never blocks players or bullets.
    /// </summary>
    public bool ModelBackgroundEnabled = false;

    /// <summary>
    /// Path of the <c>.vmdl</c> model to show behind the text, for example <c>models/my_plugin/panel.vmdl</c>.
    /// The consuming plugin must precache it from its own <c>Listeners.OnServerPrecacheResources</c> listener
    /// (<c>manifest.AddResource(path)</c>), this API does not precache models, and an unprecached model renders nothing.
    /// Changing it, <see cref="ModelBackgroundMaterialGroup"/>, <see cref="ModelBackgroundDepth"/>, or
    /// <see cref="ModelBackgroundSurfaceScale"/> through
    /// <see cref="IK4WorldTextSharedAPI.UpdateWorldText(int, TextLine?)"/> respawns the text.
    /// </summary>
    public string ModelBackgroundPath = "";

    /// <summary>
    /// Name of the material group (skin) the prop uses, as declared in the model's <c>MaterialGroupList</c>, for
    /// example <c>burgundy</c>. Empty, the default, keeps the model's <c>DefaultMaterialGroup</c>. A name the model
    /// does not declare also falls back to the default.
    /// </summary>
    public string ModelBackgroundMaterialGroup = "";

    /// <summary>
    /// Distance in world units the prop is pushed back from the text plane, away from the viewer, so it does not overlap
    /// the text. Clamped to ±1000.
    /// </summary>
    public int ModelBackgroundDepth = 2;

    /// <summary>
    /// Uniform scale applied to the prop, Source 2 props only scale uniformly. 1 keeps the model's own size, 2 doubles it,
    /// 0.5 halves it. Clamped to 0.01–100.
    /// </summary>
    public float ModelBackgroundSurfaceScale = 1.0f;
}

/// <summary>
/// Entry point served by the <c>k4-worldtext:sharedapi</c> plugin capability.
/// Obtain it with <c>new PluginCapability&lt;IK4WorldTextProvider&gt;("k4-worldtext:sharedapi").Get()</c>.
/// </summary>
public interface IK4WorldTextProvider
{
    /// <summary>
    /// Returns the text API scoped to <paramref name="owner"/>. Every text created through the returned instance
    /// belongs to that plugin: its id is unknown to every other scope, and <see cref="IK4WorldTextSharedAPI.RemoveAll"/>
    /// on it removes only those texts. Scopes are keyed by <see cref="IPlugin.ModuleName"/>, so repeated calls from the
    /// same plugin return the same scope, also after that plugin hot reloads. Safe to call from any thread.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="owner"/> has an empty <see cref="IPlugin.ModuleName"/>.</exception>
    public IK4WorldTextSharedAPI ForPlugin(IPlugin owner);
}

/// <summary>
/// Text API bound to one owning plugin; obtain it from <see cref="IK4WorldTextProvider.ForPlugin"/>.
/// An id is only valid on the scope that issued it. Every id-taking method treats an id issued to another plugin
/// exactly like an unknown id.
/// <para>
/// Every method except <see cref="GetWorldTextLineEntities"/> is safe to call from any thread. Arguments are validated
/// synchronously on the calling thread. From the game thread the entity work runs before the call returns; from any
/// other thread it is queued with <c>Server.NextWorldUpdate</c> and runs in call order, and the returned id is valid
/// right away. Failures inside the entity work never reach the caller: the plugin rolls back, logs, keeps the id
/// tracked, and retries the spawn on the next round start.
/// </para>
/// <para>
/// Every id becomes invalid on map end and when this plugin unloads or reloads. Re-create texts from your own
/// map-start handling.
/// </para>
/// </summary>
public interface IK4WorldTextSharedAPI
{
    /// <summary>Spawns a single-line text at a world position and angle.</summary>
    /// <param name="placement">Wall or floor orientation.</param>
    /// <param name="textLine">Content and styling of the line. Copied at the call.</param>
    /// <param name="position">World position. Copied at the call, so an entity-backed <see cref="Vector"/> is safe.</param>
    /// <param name="angle">World angle. Copied at the call, so an entity-backed <see cref="QAngle"/> is safe.</param>
    /// <returns>The id of the new text, valid on this scope only.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="textLine"/>, <paramref name="position"/>, or <paramref name="angle"/> is null,
    /// <paramref name="textLine"/> has a null <see cref="TextLine.Text"/>, or <paramref name="placement"/> is outside
    /// the <see cref="TextPlacement"/> enum.
    /// </exception>
    public int AddWorldText(TextPlacement placement, TextLine textLine, Vector position, QAngle angle);

    /// <summary>Spawns a multi-line text at a world position and angle. Lines are stacked top to bottom in list order.</summary>
    /// <param name="placement">Wall or floor orientation.</param>
    /// <param name="textLines">Content and styling of each line. The list is copied at the call, so the caller may reuse or clear it afterwards.</param>
    /// <param name="position">World position. Copied at the call, so an entity-backed <see cref="Vector"/> is safe.</param>
    /// <param name="angle">World angle. Copied at the call, so an entity-backed <see cref="QAngle"/> is safe.</param>
    /// <returns>The id of the new text, valid on this scope only.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="textLines"/>, <paramref name="position"/>, or <paramref name="angle"/> is null,
    /// <paramref name="textLines"/> is empty or contains a null entry or a null <see cref="TextLine.Text"/>, or
    /// <paramref name="placement"/> is outside the <see cref="TextPlacement"/> enum.
    /// </exception>
    public int AddWorldText(TextPlacement placement, List<TextLine> textLines, Vector position, QAngle angle);

    /// <summary>
    /// Spawns a single-line text relative to a player: at their feet for <see cref="TextPlacement.Floor"/>, at their
    /// position for <see cref="TextPlacement.Wall"/>, offset 15 units in the direction they face. The player's
    /// position is read when the entity work runs. From a non-game thread the player is re-validated at world-update
    /// time; if they disconnected in between, no text is created, the id becomes unknown, and a warning is logged.
    /// </summary>
    /// <param name="player">Player to place the text at.</param>
    /// <param name="placement">Wall or floor orientation.</param>
    /// <param name="textLine">Content and styling of the line. Copied at the call.</param>
    /// <returns>The id of the new text, valid on this scope only.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="player"/> or <paramref name="textLine"/> is null, <paramref name="player"/> is invalid or has no
    /// valid pawn, <paramref name="textLine"/> has a null <see cref="TextLine.Text"/>, or <paramref name="placement"/>
    /// is outside the <see cref="TextPlacement"/> enum.
    /// </exception>
    public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, TextLine textLine);

    /// <summary>
    /// Spawns a multi-line text relative to a player: at their feet for <see cref="TextPlacement.Floor"/>, at their
    /// position for <see cref="TextPlacement.Wall"/>, offset 15 units in the direction they face. The player's
    /// position is read when the entity work runs. From a non-game thread the player is re-validated at world-update
    /// time; if they disconnected in between, no text is created, the id becomes unknown, and a warning is logged.
    /// </summary>
    /// <param name="player">Player to place the text at.</param>
    /// <param name="placement">Wall or floor orientation.</param>
    /// <param name="textLines">Content and styling of each line. The list is copied at the call, so the caller may reuse or clear it afterwards.</param>
    /// <returns>The id of the new text, valid on this scope only.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="player"/> or <paramref name="textLines"/> is null, <paramref name="player"/> is invalid or has
    /// no valid pawn, <paramref name="textLines"/> is empty or contains a null entry or a null
    /// <see cref="TextLine.Text"/>, or <paramref name="placement"/> is outside the <see cref="TextPlacement"/> enum.
    /// </exception>
    public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, List<TextLine> textLines);

    /// <summary>
    /// Replaces the content and styling of an existing text with a single line. The entity is updated in place when
    /// the line count, font sizes, background layout, and model background settings are unchanged; otherwise the text
    /// is respawned.
    /// </summary>
    /// <param name="id">Id issued by this scope.</param>
    /// <param name="textLine">New content and styling, or <c>null</c> to re-apply the current lines.</param>
    /// <exception cref="KeyNotFoundException"><paramref name="id"/> is not tracked by this scope, including every id issued before the last map change.</exception>
    /// <exception cref="ArgumentException"><paramref name="textLine"/> has a null <see cref="TextLine.Text"/>.</exception>
    public void UpdateWorldText(int id, TextLine? textLine = null);

    /// <summary>
    /// Replaces the content and styling of an existing text with a list of lines. Entities are updated in place when
    /// the line count, font sizes, background layout, and model background settings are unchanged; otherwise the text
    /// is respawned.
    /// </summary>
    /// <param name="id">Id issued by this scope.</param>
    /// <param name="textLines">New content and styling for each line, or <c>null</c> to re-apply the current lines. The list is copied at the call.</param>
    /// <exception cref="KeyNotFoundException"><paramref name="id"/> is not tracked by this scope, including every id issued before the last map change.</exception>
    /// <exception cref="ArgumentException"><paramref name="textLines"/> is empty or contains a null entry or a null <see cref="TextLine.Text"/>.</exception>
    public void UpdateWorldText(int id, List<TextLine>? textLines = null);

    /// <summary>Despawns a text and forgets its id.</summary>
    /// <param name="id">Id issued by this scope.</param>
    /// <exception cref="KeyNotFoundException"><paramref name="id"/> is not tracked by this scope, including every id issued before the last map change.</exception>
    public void RemoveWorldText(int id);

    /// <summary>
    /// Returns the valid foreground entity for each line of a text. Background entities and lines whose entity is
    /// currently invalid are not included. A text whose spawn is still queued from another thread is listed with no
    /// entities until the next world update. <b>Game thread only:</b> the entities cannot be marshalled.
    /// </summary>
    /// <param name="id">Id issued by this scope.</param>
    /// <returns>The line entities, or <c>null</c> if <paramref name="id"/> is not tracked by this scope.</returns>
    /// <exception cref="InvalidOperationException">Called from a thread other than the game thread.</exception>
    public List<CPointWorldText>? GetWorldTextLineEntities(int id);

    /// <summary>Moves an existing text in place. Entities are respawned only if one of them is no longer valid.</summary>
    /// <param name="id">Id issued by this scope.</param>
    /// <param name="position">New world position. Copied at the call.</param>
    /// <param name="angle">New world angle. Copied at the call.</param>
    /// <exception cref="KeyNotFoundException"><paramref name="id"/> is not tracked by this scope, including every id issued before the last map change.</exception>
    /// <exception cref="ArgumentException"><paramref name="position"/> or <paramref name="angle"/> is null.</exception>
    public void TeleportWorldText(int id, Vector position, QAngle angle);

    /// <summary>Despawns every text created through this scope. Texts owned by other plugins are untouched.</summary>
    public void RemoveAll();
}