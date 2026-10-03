namespace Nova3D.Production.Input;

/// <summary>Explicitly activated group of actions at one input-routing priority.</summary>
public sealed class InputContext
{
    internal InputContext(string name, int priority, bool blocksLowerContexts, bool active, int order)
    {
        Name = name;
        Priority = priority;
        BlocksLowerContexts = blocksLowerContexts;
        IsActive = active;
        Order = order;
    }

    public string Name { get; }
    public int Priority { get; }
    public bool BlocksLowerContexts { get; }
    public bool IsActive { get; private set; }
    public bool IsReceivingInput { get; internal set; }
    public InputActionMap Actions { get; } = new();
    internal int Order { get; }

    public void SetActive(bool active) => IsActive = active;
}
