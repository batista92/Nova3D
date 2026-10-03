namespace Nova3D.Production.Scenes;

/// <summary>Reusable phase transitions around a game-owned SceneService.</summary>
public sealed class SceneFlowController
{
    private readonly SceneService _scenes;
    private readonly int _owningThreadId = Environment.CurrentManagedThreadId;
    private bool _changing;

    public SceneFlowController(SceneService scenes)
    {
        _scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
    }

    public SceneFlowState State { get; private set; } = SceneFlowState.Boot;
    public bool IsChanging => _changing;
    public SceneInstance? ActiveScene => _scenes.Active;

    /// <summary>Game-owned hook for UI, simulation and audio. It does not own those systems.</summary>
    public event Action<SceneFlowState, SceneFlowState>? StateChanged;

    public void Start(
        Func<CancellationToken, SceneLoadPlan> prepare,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prepare);
        Change(() => Load(() => _scenes.Load(prepare, cancellationToken: cancellationToken)));
    }

    public void Start(SceneLoadPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Change(() => Load(() => _scenes.Activate(plan, cancellationToken: cancellationToken)));
    }

    public void Restart(CancellationToken cancellationToken = default)
    {
        Change(() =>
        {
            RequireActivePhase();
            Load(() => _scenes.Reload(cancellationToken: cancellationToken));
        });
    }

    public void Pause() => Change(() =>
    {
        RequireState(SceneFlowState.Playing);
        SetState(SceneFlowState.Paused);
    });

    public void Resume() => Change(() =>
    {
        RequireState(SceneFlowState.Paused);
        SetState(SceneFlowState.Playing);
    });

    /// <summary>The game decides whether this means victory, defeat or another result.</summary>
    public void ShowResult() => Change(() =>
    {
        RequireState(SceneFlowState.Playing);
        SetState(SceneFlowState.Result);
    });

    public void ReturnToMenu() => Change(() =>
    {
        try { _scenes.Unload(); }
        finally
        {
            if (_scenes.Active is null)
                SetState(SceneFlowState.Menu);
        }
    });

    private void Load(Action activate)
    {
        var previousState = State;
        var previousScene = _scenes.Active;
        try { SetState(SceneFlowState.Loading); }
        catch
        {
            State = previousState;
            throw;
        }

        try { activate(); }
        catch
        {
            // A failed prepare/instantiate retains the old scene. A disposal
            // failure after commit may leave the replacement active instead.
            SetState(ReferenceEquals(_scenes.Active, previousScene)
                ? previousState
                : _scenes.Active is null ? SceneFlowState.Menu : SceneFlowState.Playing);
            throw;
        }
        SetState(SceneFlowState.Playing);
    }

    private void RequireActivePhase()
    {
        if (State is not (SceneFlowState.Playing or SceneFlowState.Paused or SceneFlowState.Result)
            || _scenes.Active is null)
            throw new InvalidOperationException("Restart requires an active playing, paused or result scene.");
    }

    private void RequireState(SceneFlowState expected)
    {
        if (State != expected)
            throw new InvalidOperationException($"Expected scene flow state {expected}, but found {State}.");
    }

    private void SetState(SceneFlowState next)
    {
        if (State == next)
            return;
        var previous = State;
        State = next;
        StateChanged?.Invoke(previous, next);
    }

    private void Change(Action action)
    {
        if (Environment.CurrentManagedThreadId != _owningThreadId)
            throw new InvalidOperationException("Scene flow transitions must run on the controller's owning thread.");
        if (_changing)
            throw new InvalidOperationException("A scene flow transition is already in progress.");
        _changing = true;
        try { action(); }
        finally { _changing = false; }
    }
}

public enum SceneFlowState { Boot, Menu, Loading, Playing, Paused, Result }
