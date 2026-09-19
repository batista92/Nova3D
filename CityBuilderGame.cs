using CityBuilder.Tests.LargeWorld;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CityBuilder;

public sealed class CityBuilderGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private LargeWorldScene? _scene;

    public CityBuilderGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = false,
            PreferMultiSampling = true,
            GraphicsProfile = GraphicsProfile.HiDef
        };

        _graphics.PreparingDeviceSettings += (_, args) =>
            args.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 4;

        Content.RootDirectory = "Content";
        IsFixedTimeStep = false;
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "CityBuilder - Teste 01: PBR";
    }

    protected override void LoadContent()
    {
        _scene = new LargeWorldScene(
            GraphicsDevice,
            Content.Load<Effect>("Shaders/LargeWorld"),
            Content.Load<Effect>("Shaders/Vegetation"),
            Content.Load<Effect>("Shaders/Skybox"),
            Content.Load<Effect>("Shaders/ShadowDepth"),
            Content.Load<Effect>("Shaders/InstancedShadow"),
            Content.Load<Effect>("Shaders/PostProcess"));
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        _scene?.Update(gameTime, Window);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(18, 22, 30));
        _scene?.Draw(GraphicsDevice.Viewport.AspectRatio);
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _scene?.Dispose();
        _scene = null;
        base.UnloadContent();
    }
}
