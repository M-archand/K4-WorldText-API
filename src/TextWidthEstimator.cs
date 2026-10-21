using CounterStrikeSharp.API.Core;
using K4WorldTextSharedAPI;

namespace K4ryuuCS2WorldTextAPI;

// Approximate rendered width of point_worldtext lines
internal static class TextWidthEstimator
{
    private const float RenderedEmScale = 0.9f;
    private const float DefaultAdvance = 0.556f;

    // ASCII 32 (space) through 126 (~)
    private static readonly float[] AsciiAdvance =
    {
        0.278f, 0.278f, 0.355f, 0.556f, 0.556f, 0.889f, 0.667f, 0.191f, 0.333f, 0.333f, 0.389f, 0.584f, 0.278f, 0.333f, 0.278f, 0.278f,
        0.556f, 0.556f, 0.556f, 0.556f, 0.556f, 0.556f, 0.556f, 0.556f, 0.556f, 0.556f, 0.278f, 0.278f, 0.584f, 0.584f, 0.584f, 0.556f,
        1.015f, 0.667f, 0.667f, 0.722f, 0.722f, 0.667f, 0.611f, 0.778f, 0.722f, 0.278f, 0.500f, 0.667f, 0.556f, 0.833f, 0.722f, 0.778f,
        0.667f, 0.778f, 0.722f, 0.667f, 0.611f, 0.722f, 0.667f, 0.944f, 0.667f, 0.667f, 0.611f, 0.278f, 0.278f, 0.278f, 0.469f, 0.556f,
        0.333f, 0.556f, 0.556f, 0.500f, 0.556f, 0.556f, 0.278f, 0.556f, 0.556f, 0.222f, 0.222f, 0.500f, 0.222f, 0.833f, 0.556f, 0.556f,
        0.556f, 0.556f, 0.333f, 0.500f, 0.278f, 0.556f, 0.500f, 0.722f, 0.500f, 0.500f, 0.500f, 0.334f, 0.260f, 0.334f, 0.584f,
    };

    // Midpoint of the lines' horizontal extent relative to the text origin, in world units, positive toward the
    // viewer's right: 0 for centered lines, half the widest line for left-justified ones, the negative of that for
    // right-justified ones. Mixed justifications combine their extents.
    public static float CenterOffset(IReadOnlyList<TextLine> lines)
    {
        float left = 0f, right = 0f;

        foreach (var line in lines)
        {
            float width = Width(line);
            var (lineLeft, lineRight) = line.JustifyHorizontal switch
            {
                PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_LEFT  => (0f, width),
                PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_RIGHT => (-width, 0f),
                _ => (-width * 0.5f, width * 0.5f),
            };

            left  = MathF.Min(left, lineLeft);
            right = MathF.Max(right, lineRight);
        }

        return (left + right) * 0.5f;
    }

    public static float Width(TextLine line)
    {
        float widestEm = 0f;

        foreach (var row in (line.Text ?? string.Empty).Split('\n'))
        {
            float em = 0f;
            foreach (char c in row)
                em += Advance(c);
            widestEm = MathF.Max(widestEm, em);
        }

        return widestEm * RenderedEmScale * TextLineLimits.FontSize(line.FontSize) * TextLineLimits.Scale(line.Scale);
    }

    private static float Advance(char c) => c switch
    {
        >= ' ' and <= '~' => AsciiAdvance[c - ' '],
        '\u00A0' or '\t' => AsciiAdvance[0],
        < ' ' => 0f,
        _ => DefaultAdvance,
    };
}
