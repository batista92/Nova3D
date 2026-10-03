namespace Nova3D.Production.Scenes;

/// <summary>Owns the active scene and swaps it only after a replacement is fully instantiated.</summary>
public sealed class SceneService : IDisposable
{
    private readonly SceneInstantiator _instantiator;
    private readonly int _owningThreadId = Environment.CurrentManagedThreadId;
    private bool _transitioning;
    private bool _disposed;

    public SceneService(SceneInstantiator instantiator)
    {
        _instantiator = instantiator ?? throw new ArgumentNullException(nameof(instantiator));
        if (_instantiator.OwningThreadId != _owningThreadId)
            throw new InvalidOperationException("SceneService and SceneInstantiator must be created on the same thread.");
    }

    public SceneInstance? Active { get; private set; }
    public SceneLoadPlan? ActivePlan => Active?.Plan;
    public bool IsTransitioning => _transitioning;

    /// <summary>Prepares and activates a scene. Preparation is CPU-only, but this method runs on the owning thread.</summary>
    public SceneInstance Load(
        Func<CancellationToken, SceneLoadPlan> prepare,
        IProgress<SceneTransitionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prepare);
        return Transition(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new SceneTransitionProgress(SceneTransitionStage.Preparing));
            var plan = prepare(cancellationToken) ?? throw new InvalidOperationException("Scene preparation returned null.");
            return ActivateCore(plan, progress, cancellationToken);
        });
    }

    /// <summary>Activates a previously prepared CPU plan, retaining the old scene on failure.</summary>
    public SceneInstance Activate(
        SceneLoadPlan plan,
        IProgress<SceneTransitionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return Transition(() => ActivateCore(plan, progress, cancellationToken));
    }

    /// <summary>Re-instantiates the active plan. Throws if no scene is active.</summary>
    public SceneInstance Reload(
        IProgress<SceneTransitionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Transition(() =>
        {
            var plan = Active?.Plan ?? throw new InvalidOperationException("There is no active scene to reload.");
            return ActivateCore(plan, progress, cancellationToken);
        });
    }

    public void Unload(IProgress<SceneTransitionProgress>? progress = null)
    {
        Transition(() =>
        {
            progress?.Report(new SceneTransitionProgress(SceneTransitionStage.Unloading));
            var previous = Active;
            Active = null;
            previous?.Dispose();
            progress?.Report(new SceneTransitionProgress(SceneTransitionStage.Complete));
            return 0;
        });
    }

    private SceneInstance ActivateCore(
        SceneLoadPlan plan,
        IProgress<SceneTransitionProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new SceneTransitionProgress(SceneTransitionStage.Instantiating));
        var next = _instantiator.Instantiate(plan, cancellationToken);
        // A cancellation requested during the last factory must not replace the old scene.
        if (cancellationToken.IsCancellationRequested)
        {
            next.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }

        var previous = Active;
        try
        {
            progress?.Report(new SceneTransitionProgress(SceneTransitionStage.Unloading));
        }
        catch
        {
            next.Dispose();
            throw;
        }
        Active = next;
        previous?.Dispose();
        progress?.Report(new SceneTransitionProgress(SceneTransitionStage.Complete));
        return next;
    }

    private T Transition<T>(Func<T> operation)
    {
        VerifyThread();
        if (_disposed)
            throw new ObjectDisposedException(nameof(SceneService));
        if (_transitioning)
            throw new InvalidOperationException("A scene transition is already in progress.");
        _transitioning = true;
        try { return operation(); }
        finally { _transitioning = false; }
    }

    private void VerifyThread()
    {
        if (Environment.CurrentManagedThreadId != _owningThreadId)
            throw new InvalidOperationException("Scene transitions must run on the SceneService owning thread.");
    }

    public void Dispose()
    {
        VerifyThread();
        if (_disposed)
            return;
        if (_transitioning)
            throw new InvalidOperationException("Cannot dispose a SceneService during a transition.");
        _disposed = true;
        var previous = Active;
        Active = null;
        previous?.Dispose();
    }
}

public enum SceneTransitionStage { Preparing, Instantiating, Unloading, Complete }

/// <summary>A UI-neutral transition milestone; no timing or percentage is implied.</summary>
public readonly record struct SceneTransitionProgress(SceneTransitionStage Stage);
