using Microsoft.Xna.Framework;

namespace Nova3D.World.Spatial;

/// <summary>XZ uniform grid for immutable world positions.</summary>
public sealed class StaticSpatialGrid
{
    private readonly Dictionary<long, int[]> _cells;

    public StaticSpatialGrid(IReadOnlyList<Vector3> positions, float cellSize)
    {
        ArgumentNullException.ThrowIfNull(positions);
        if (!float.IsFinite(cellSize) || cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
        CellSize = cellSize;
        var building = new Dictionary<long, List<int>>();
        for (var index = 0; index < positions.Count; index++)
        {
            var cellX = ToCell(positions[index].X);
            var cellZ = ToCell(positions[index].Z);
            var key = Key(cellX, cellZ);
            if (!building.TryGetValue(key, out var indices))
                building.Add(key, indices = new List<int>());
            indices.Add(index);
        }
        _cells = building.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    }

    public float CellSize { get; }
    public int OccupiedCellCount => _cells.Count;

    public void Query(Vector3 center, float radius, List<int> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (!float.IsFinite(radius) || radius < 0f) throw new ArgumentOutOfRangeException(nameof(radius));
        results.Clear();
        var minX = ToCell(center.X - radius);
        var maxX = ToCell(center.X + radius);
        var minZ = ToCell(center.Z - radius);
        var maxZ = ToCell(center.Z + radius);
        for (var z = minZ; z <= maxZ; z++)
        for (var x = minX; x <= maxX; x++)
            if (_cells.TryGetValue(Key(x, z), out var indices))
                results.AddRange(indices);
    }

    private int ToCell(float coordinate) => (int)MathF.Floor(coordinate / CellSize);
    private static long Key(int x, int z) => ((long)x << 32) | (uint)z;
}
