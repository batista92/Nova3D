namespace Nova3D.Benchmarks.Validation.Performance;

internal readonly record struct CityPerformanceSnapshot(
    double ShadowCpuMilliseconds,
    double WorldCpuMilliseconds,
    double PostCpuMilliseconds,
    int DrawCalls,
    long Triangles,
    long ShadowTriangles,
    int ShadowDrawCalls,
    int Instances,
    int VisibleChunks,
    int Candidates);
