using Gum.Forms.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nova3D.UI.Gum;

/// <summary>Projects a world position into the virtual Gum canvas.</summary>
public sealed class GumWorldMarker
{
    private readonly GumUiHost _host;
    private readonly FrameworkElement _element;

    public GumWorldMarker(GumUiHost host, FrameworkElement element)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _element = element ?? throw new ArgumentNullException(nameof(element));
    }

    public Vector2 Offset { get; set; }
    public float CanvasMargin { get; set; } = 8f;
    public bool ClampToCanvas { get; set; }
    public bool IsVisible { get; private set; }

    public bool Update(Vector3 worldPosition, Matrix view, Matrix projection, Viewport viewport)
    {
        Vector4 clip = Vector4.Transform(new Vector4(worldPosition, 1f), view * projection);
        if (clip.W <= 0f || viewport.Width <= 0 || viewport.Height <= 0)
            return SetVisibility(false);

        Vector3 projected = viewport.Project(worldPosition, projection, view, Matrix.Identity);
        float normalizedX = (projected.X - viewport.X) / viewport.Width;
        float normalizedY = (projected.Y - viewport.Y) / viewport.Height;
        bool insideDepth = projected.Z is >= 0f and <= 1f;
        bool insideCanvas = normalizedX is >= 0f and <= 1f && normalizedY is >= 0f and <= 1f;

        if (!insideDepth || (!insideCanvas && !ClampToCanvas))
            return SetVisibility(false);

        float canvasWidth = _host.Service.CanvasWidth;
        float canvasHeight = _host.Service.CanvasHeight;
        float x = normalizedX * canvasWidth + Offset.X;
        float y = normalizedY * canvasHeight + Offset.Y;
        if (ClampToCanvas)
        {
            x = MathHelper.Clamp(x, CanvasMargin, MathF.Max(CanvasMargin, canvasWidth - CanvasMargin));
            y = MathHelper.Clamp(y, CanvasMargin, MathF.Max(CanvasMargin, canvasHeight - CanvasMargin));
        }

        _element.X = x;
        _element.Y = y;
        return SetVisibility(true);
    }

    private bool SetVisibility(bool visible)
    {
        IsVisible = visible;
        _element.Visual.Visible = visible;
        return visible;
    }
}
