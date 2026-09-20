using Microsoft.Xna.Framework;

namespace Nova3D.World.Vegetation;

public sealed record VegetationInstances(Matrix[] Transforms, bool[] Enabled);

public static class VegetationScatter
{
    public static VegetationInstances CreateGrid(int count, float areaSize, int seed,
        Func<float, float, float>? heightProvider = null,
        Func<float, float, bool>? placementAllowed = null,
        float minimumScale = 0.72f, float maximumScale = 1.47f)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (areaSize <= 0f) throw new ArgumentOutOfRangeException(nameof(areaSize));
        if (minimumScale <= 0f || maximumScale < minimumScale)
            throw new ArgumentOutOfRangeException(nameof(minimumScale));

        var transforms = new Matrix[count];
        var enabled = new bool[count];
        var random = new Random(seed);
        var columns = (int)MathF.Ceiling(MathF.Sqrt(count));
        var rows = (int)MathF.Ceiling(count / (float)columns);
        var spacingX = areaSize / columns;
        var spacingZ = areaSize / rows;
        for (var i = 0; i < count; i++)
        {
            var gx = i % columns;
            var gz = i / columns;
            var x = (gx - (columns - 1) * 0.5f) * spacingX +
                    (float)(random.NextDouble() - 0.5) * spacingX * 0.7f;
            var z = (gz - (rows - 1) * 0.5f) * spacingZ +
                    (float)(random.NextDouble() - 0.5) * spacingZ * 0.7f;
            var scale = MathHelper.Lerp(minimumScale, maximumScale, (float)random.NextDouble());
            var yaw = (float)random.NextDouble() * MathF.Tau;
            var y = heightProvider?.Invoke(x, z) ?? 0f;
            transforms[i] = Matrix.CreateScale(scale) * Matrix.CreateRotationY(yaw) *
                            Matrix.CreateTranslation(x, y, z);
            enabled[i] = placementAllowed?.Invoke(x, z) ?? true;
        }
        return new VegetationInstances(transforms, enabled);
    }
}
