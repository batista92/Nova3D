namespace Nova3D.World.Terrain;

public interface IHeightProvider
{
    float SampleHeight(float worldX, float worldZ);
}

public sealed class DelegateHeightProvider : IHeightProvider
{
    private readonly Func<float, float, float> _sample;

    public DelegateHeightProvider(Func<float, float, float> sample)
    {
        _sample = sample ?? throw new ArgumentNullException(nameof(sample));
    }

    public float SampleHeight(float worldX, float worldZ) => _sample(worldX, worldZ);
}
