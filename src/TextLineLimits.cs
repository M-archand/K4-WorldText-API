namespace K4ryuuCS2WorldTextAPI;

// Bounds applied to consumer-supplied TextLine fields before they reach native entity memory.
// Out-of-range values are clamped rather than rejected, mirroring the 512-byte m_messageText truncation,
internal static class TextLineLimits
{
    public const int MinFontSize = 1;
    public const int MaxFontSize = 500;

    public const float MinScale = 0.01f;
    public const float MaxScale = 10f;

    public const float MinWorldToUV = 0.001f;
    public const float MaxWorldToUV = 10f;

    public const float MaxBorder = 1000f;

    public const float MaxDepthOffset = 100f;

    // One NBSP mask character costs 2 UTF-8 bytes, so this is the widest single-line mask that fits the budget.
    public const int MaxCharsPerLine = WorldText.MessageTextByteBudget / 2;
    public const int MaxPadChars = 256;

    public const float MinWidthInflation = 0.1f;
    public const float MaxWidthInflation = 10f;

    // CPointWorldText::m_FontName is a fixed 64-byte char array.
    public const int FontNameByteBudget = 64;

    public static int FontSize(int value) => Math.Clamp(value, MinFontSize, MaxFontSize);
    public static float Scale(float value) => Clamp(value, MinScale, MaxScale);
    public static float WorldToUV(float value) => Clamp(value, MinWorldToUV, MaxWorldToUV);
    public static float Border(float value) => Clamp(value, 0f, MaxBorder);
    public static float DepthOffset(float value) => Clamp(value, -MaxDepthOffset, MaxDepthOffset);
    public static int CharsPerLine(int value) => Math.Clamp(value, 1, MaxCharsPerLine);
    public static int PadChars(int value) => Math.Clamp(value, 0, MaxPadChars);
    public static float WidthInflation(float value) => Clamp(value, MinWidthInflation, MaxWidthInflation);
    public static string FontName(string? value) => WorldText.ClampToByteBudget(value, FontNameByteBudget);

    // NaN becomes the in-range value closest to zero.
    private static float Clamp(float value, float min, float max) =>
        float.IsNaN(value) ? Math.Clamp(0f, min, max) : Math.Clamp(value, min, max);
}
