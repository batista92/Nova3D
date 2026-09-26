namespace Nova3D.UI.Gum;

/// <summary>Applies a value only when it changes, keeping an existing Gum control alive.</summary>
public sealed class GumValueBinding<T>
{
    private readonly Action<T> _apply;
    private readonly IEqualityComparer<T> _comparer;
    private T? _value;
    private bool _hasValue;

    public GumValueBinding(Action<T> apply, IEqualityComparer<T>? comparer = null)
    {
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _comparer = comparer ?? EqualityComparer<T>.Default;
    }

    public bool Set(T value)
    {
        if (_hasValue && _comparer.Equals(_value!, value)) return false;
        _value = value;
        _hasValue = true;
        _apply(value);
        return true;
    }

    public void Invalidate() => _hasValue = false;
}
