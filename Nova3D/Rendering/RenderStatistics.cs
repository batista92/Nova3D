namespace Nova3D.Rendering;

/// <summary>Renderer counters for the last completed frame.</summary>
public sealed class RenderStatistics
{
    public int DrawCalls { get; private set; }
    public long Triangles { get; private set; }
    public long ShadowTriangles { get; private set; }
    public int Instances { get; private set; }
    public int VisibleChunks { get; private set; }
    public int ShadowDrawCalls { get; private set; }

    public void Reset()
    {
        DrawCalls = 0;
        Triangles = 0;
        ShadowTriangles = 0;
        Instances = 0;
        VisibleChunks = 0;
        ShadowDrawCalls = 0;
    }

    public void RecordDraw(long triangles, int instances = 1, bool shadow = false)
    {
        DrawCalls++;
        if (shadow) ShadowTriangles += triangles;
        else Triangles += triangles;
        Instances += instances;
        if (shadow) ShadowDrawCalls++;
    }

    public void RecordDraws(int drawCalls, long triangles, int instances = 0, bool shadow = false)
    {
        if (drawCalls < 0) throw new ArgumentOutOfRangeException(nameof(drawCalls));
        if (triangles < 0) throw new ArgumentOutOfRangeException(nameof(triangles));
        if (instances < 0) throw new ArgumentOutOfRangeException(nameof(instances));
        DrawCalls += drawCalls;
        if (shadow) ShadowTriangles += triangles;
        else Triangles += triangles;
        Instances += instances;
        if (shadow) ShadowDrawCalls += drawCalls;
    }

    public void RecordVisibleChunks(int count) => VisibleChunks += count;

    internal void CopyFrom(RenderStatistics source)
    {
        DrawCalls = source.DrawCalls;
        Triangles = source.Triangles;
        ShadowTriangles = source.ShadowTriangles;
        Instances = source.Instances;
        VisibleChunks = source.VisibleChunks;
        ShadowDrawCalls = source.ShadowDrawCalls;
    }
}
