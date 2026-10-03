namespace Nova3D.Production.Scenes;

/// <summary>Instantiates a load plan on the thread where this object was created.</summary>
public sealed class SceneInstantiator
{
    private readonly int _owningThreadId = Environment.CurrentManagedThreadId;

    public int OwningThreadId => _owningThreadId;

    public SceneInstance Instantiate(
        SceneLoadPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        VerifyOwningThread();

        var nodes = new List<SceneNodeInstance>(plan.Nodes.Count);
        var created = new List<SceneComponentInstance>();
        try
        {
            foreach (var plannedNode in plan.Nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var components = new List<SceneComponentInstance>();
                foreach (var plannedComponent in plannedNode.Components)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (plannedComponent.Descriptor is not ISceneRuntimeComponentDescriptor runtimeDescriptor)
                        continue;

                    object value;
                    try
                    {
                        value = runtimeDescriptor.Create(new SceneComponentInstantiationContext(
                            plan, plannedNode, plannedComponent.Document, cancellationToken));
                        if (value is null)
                            throw new InvalidOperationException("A scene runtime component factory returned null.");
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        throw new SceneInstantiationException(
                            plan.DocumentPath,
                            plannedNode.Id,
                            plannedComponent.Document.Id,
                            plannedComponent.Document.Type,
                            exception);
                    }

                    var instance = new SceneComponentInstance(
                        plannedComponent.Document, value, runtimeDescriptor);
                    components.Add(instance);
                    created.Add(instance);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                nodes.Add(new SceneNodeInstance(plannedNode, components));
            }

            return new SceneInstance(plan, nodes, _owningThreadId);
        }
        catch (Exception creationFailure)
        {
            var rollbackFailures = DestroyReverse(created);
            if (rollbackFailures.Count > 0)
            {
                rollbackFailures.Insert(0, creationFailure);
                throw new AggregateException(
                    "Scene instantiation failed and rollback also reported errors.", rollbackFailures);
            }
            throw;
        }
    }

    private void VerifyOwningThread()
    {
        if (Environment.CurrentManagedThreadId != _owningThreadId)
        {
            throw new InvalidOperationException(
                $"Scene runtime work must execute on owning thread {_owningThreadId}; " +
                $"current thread is {Environment.CurrentManagedThreadId}.");
        }
    }

    private static List<Exception> DestroyReverse(IReadOnlyList<SceneComponentInstance> created)
    {
        var failures = new List<Exception>();
        for (var index = created.Count - 1; index >= 0; index--)
        {
            try
            {
                created[index].Descriptor.Destroy(created[index].Value);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }
        return failures;
    }
}
