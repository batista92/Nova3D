namespace Nova3D.Production.Assets;

public sealed class AssetHandle<T> where T : class
{
    internal AssetHandle(string name, string sourcePath, T value)
    {
        Name = name;
        SourcePath = sourcePath;
        Value = value;
    }

    public string Name { get; }
    public string SourcePath { get; }
    public T Value { get; private set; }
    public int Version { get; private set; } = 1;
    public event Action<AssetHandle<T>>? Reloaded;

    internal T Replace(T value)
    {
        var previous = Value;
        Value = value;
        Version++;
        Reloaded?.Invoke(this);
        return previous;
    }
}
