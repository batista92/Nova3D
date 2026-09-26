using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nova3D.UI.Gum;

namespace Nova3D.Samples.RollingBall;

internal enum RunState { Menu, Playing, Paused, Victory, Defeat }

internal sealed class RollingBallGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private GumUiHost? _uiHost;
    private RollingBallUi? _ui;
    private RollingBallWorld? _world;
    private RunState _state = RunState.Menu;
    private TimeSpan _elapsed;
    private bool _checkpointReached;

    public RollingBallGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            GraphicsProfile = GraphicsProfile.HiDef
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "Nova3D Sample - RollingBall";
    }

    protected override void Initialize()
    {
        base.Initialize();
        _uiHost = new GumUiHost(this);
        _ui = new RollingBallUi(_uiHost);
        _ui.ShowMenu(StartRun, Exit);
    }

    protected override void LoadContent() => _world = new RollingBallWorld(GraphicsDevice);

    protected override void Update(GameTime gameTime)
    {
        _uiHost!.Update(gameTime);
        if (_uiHost.Navigation.BackPressed)
        {
            if (_state == RunState.Playing) Pause();
            else if (_state == RunState.Paused) Resume();
            else if (_state is RunState.Victory or RunState.Defeat) ShowMenu();
            else Exit();
        }

        if (_state == RunState.Playing)
        {
            KeyboardState keyboard = Keyboard.GetState();
            var input = new Vector2(
                Axis(keyboard, Keys.A, Keys.Left, Keys.D, Keys.Right),
                Axis(keyboard, Keys.W, Keys.Up, Keys.S, Keys.Down));
            _world!.Update(gameTime, input);
            _elapsed += gameTime.ElapsedGameTime;

            if (!_checkpointReached && Contains(_world.CheckpointBounds, _world.MarblePosition))
            {
                _checkpointReached = true;
                _world.ActivateCheckpoint();
            }
            if (_checkpointReached && Contains(_world.FinishBounds, _world.MarblePosition))
                Finish(victory: true);
            else if (_world.MarblePosition.Y < -8f)
                Finish(victory: false);
        }

        _ui!.Update(gameTime, (int)_elapsed.TotalSeconds, _checkpointReached);
        base.Update(gameTime);
    }

    private static float Axis(KeyboardState state, Keys negativeA, Keys negativeB,
        Keys positiveA, Keys positiveB) =>
        (state.IsKeyDown(positiveA) || state.IsKeyDown(positiveB) ? 1f : 0f) -
        (state.IsKeyDown(negativeA) || state.IsKeyDown(negativeB) ? 1f : 0f);

    private static bool Contains(BoundingBox bounds, Vector3 point) =>
        bounds.Contains(point) != ContainmentType.Disjoint;

    private void StartRun()
    {
        _elapsed = TimeSpan.Zero;
        _checkpointReached = false;
        _world?.Respawn(resetCheckpoint: true);
        _state = RunState.Playing;
        _ui!.ShowHud();
    }

    private void Pause()
    {
        _state = RunState.Paused;
        _ui!.ShowPause(Resume, StartRun, ShowMenu);
    }

    private void Resume()
    {
        _ui!.HidePause();
        _state = RunState.Playing;
    }

    private void Finish(bool victory)
    {
        _state = victory ? RunState.Victory : RunState.Defeat;
        _ui!.ShowEnd(victory, (int)_elapsed.TotalSeconds, StartRun, ShowMenu);
    }

    private void ShowMenu()
    {
        _state = RunState.Menu;
        _ui!.ShowMenu(StartRun, Exit);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(22, 27, 36));
        _world?.Draw(_checkpointReached);
        _uiHost?.Draw();
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _ui?.Dispose();
        _uiHost?.Dispose();
        _world?.Dispose();
        base.UnloadContent();
    }
}
