using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Nova3DGame.Game;

public sealed class MyGame : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private World? _world;
#if (ui)
    private Nova3D.UI.Gum.GumUiHost? _uiHost;
    private UiOverlay? _ui;
#endif

    public MyGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = true,
            GraphicsProfile = GraphicsProfile.HiDef
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "Nova3DGame";
    }

    protected override void Initialize()
    {
        base.Initialize();
#if (ui)
        _uiHost = new Nova3D.UI.Gum.GumUiHost(this, new Nova3D.UI.Gum.GumUiHostOptions
        {
            InputMode = Nova3D.UI.Gum.GumUiInputMode.Overlay,
            Accessibility = new Nova3D.UI.Gum.GumUiAccessibilitySettings
            {
                MinimumHitTarget = 44f,
                MinimumContrastRatio = 4.5f
            }
        });
        _ui = new UiOverlay(_uiHost, Exit);
#endif
    }

    protected override void LoadContent() => _world = new World(GraphicsDevice);

    protected override void Update(GameTime gameTime)
    {
#if (ui)
        _uiHost?.Update(gameTime);
        if (_uiHost?.Navigation.BackPressed == true) Exit();
        _ui?.Update(gameTime);
#else
        if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();
#endif
        _world?.Update(gameTime, GraphicsDevice.Viewport.AspectRatio);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(18, 22, 30));
        _world?.Draw();
#if (ui)
        _uiHost?.Draw();
#endif
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
#if (ui)
        _ui?.Dispose();
        _ui = null;
        _uiHost?.Dispose();
        _uiHost = null;
#endif
        _world?.Dispose();
        _world = null;
        base.UnloadContent();
    }
}
