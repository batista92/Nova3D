using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nova3D.Rendering;

namespace Nova3DGame.Game;

public sealed class World : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly Mesh _cube;
    private readonly BasicEffect _effect;
    private readonly Camera3D _camera = new()
    {
        Position = new Vector3(0f, 2.2f, 6f),
        Direction = Vector3.Normalize(new Vector3(0f, -0.25f, -1f)),
        NearPlane = 0.1f,
        FarPlane = 100f
    };
    private float _rotation;

    public World(GraphicsDevice device)
    {
        _device = device;
        _cube = CreateCube(device);
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            LightingEnabled = false
        };
    }

    public void Update(GameTime gameTime, float aspectRatio)
    {
        _rotation += (float)gameTime.ElapsedGameTime.TotalSeconds * 0.65f;
        _camera.SetAspectRatio(aspectRatio);
    }

    public void Draw()
    {
        _device.DepthStencilState = DepthStencilState.Default;
        _device.BlendState = BlendState.Opaque;
        _device.RasterizerState = RasterizerState.CullCounterClockwise;
        _effect.World = Matrix.CreateRotationY(_rotation) * Matrix.CreateRotationX(_rotation * 0.35f);
        _effect.View = _camera.View;
        _effect.Projection = _camera.Projection;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _cube.Draw(_device);
        }
    }

    private static Mesh CreateCube(GraphicsDevice device)
    {
        var vertices = new[]
        {
            new VertexPositionColor(new Vector3(-1,-1,-1), Color.CornflowerBlue),
            new VertexPositionColor(new Vector3( 1,-1,-1), Color.Orange),
            new VertexPositionColor(new Vector3( 1, 1,-1), Color.LimeGreen),
            new VertexPositionColor(new Vector3(-1, 1,-1), Color.HotPink),
            new VertexPositionColor(new Vector3(-1,-1, 1), Color.Gold),
            new VertexPositionColor(new Vector3( 1,-1, 1), Color.Cyan),
            new VertexPositionColor(new Vector3( 1, 1, 1), Color.White),
            new VertexPositionColor(new Vector3(-1, 1, 1), Color.MediumPurple)
        };
        ushort[] indices =
        {
            0,1,2, 0,2,3, 1,5,6, 1,6,2, 5,4,7, 5,7,6,
            4,0,3, 4,3,7, 3,2,6, 3,6,7, 4,5,1, 4,1,0
        };
        return Mesh.Create(device, vertices, indices);
    }

    public void Dispose()
    {
        _cube.Dispose();
        _effect.Dispose();
    }
}
