namespace Nova3D.Production.Input;

/// <summary>Two actions that read at least one of the same physical controls.</summary>
public readonly record struct InputBindingConflict(string FirstAction, int FirstBinding,
    string SecondAction, int SecondBinding);
