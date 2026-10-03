namespace Nova3D.Production.Scenes.Assets;

/// <summary>An owned reference to one cached scene asset.</summary>
public sealed class SceneAssetLease<T> : IDisposable where T : class
{
    private SceneAssetCache<T>? _owner;
    private readonly T _value;

    internal SceneAssetLease(SceneAssetCache<T> owner, string path, T value)
    {
        _owner = owner;
        Path = path;
        _value = value;
    }

    public string Path { get; }
    public T Value
    {
        get
        {
            var owner = _owner;
            ObjectDisposedException.ThrowIf(owner is null, this);
            owner.VerifyLeaseAccess(Path);
            return _value;
        }
    }
    public bool IsDisposed => _owner is null || _owner.IsDisposed;

    public void Dispose()
    {
        var owner = _owner;
        if (owner is null)
            return;
        owner.Release(Path);
        _owner = null;
    }
}
