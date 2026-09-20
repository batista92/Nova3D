using Microsoft.Xna.Framework;

namespace Nova3D.World.Streaming;

public readonly record struct WorldCell(int X, int Z)
{
    public Vector2 Center(float cellSize, Vector2 origin) =>
        origin + new Vector2((X + 0.5f) * cellSize, (Z + 0.5f) * cellSize);
}
