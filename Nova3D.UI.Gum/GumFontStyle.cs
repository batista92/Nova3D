using Gum.GueDeriving;

namespace Nova3D.UI.Gum;

/// <summary>Font settings for Gum V3 text visuals; font loading remains owned by Gum.</summary>
public sealed class GumFontStyle
{
    public GumFontStyle(int size, string? family = null, bool isBold = false, bool isItalic = false)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        Size = size;
        Family = family;
        IsBold = isBold;
        IsItalic = isItalic;
    }

    public int Size { get; }
    public string? Family { get; }
    public bool IsBold { get; }
    public bool IsItalic { get; }

    public void Apply(TextRuntime text, float scale = 1f)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!float.IsFinite(scale) || scale <= 0f) throw new ArgumentOutOfRangeException(nameof(scale));
        text.FontSize = Math.Max(1, (int)MathF.Round(Size * scale));
        text.IsBold = IsBold;
        text.IsItalic = IsItalic;
        if (!string.IsNullOrWhiteSpace(Family)) text.FontFamily = Family;
    }
}
