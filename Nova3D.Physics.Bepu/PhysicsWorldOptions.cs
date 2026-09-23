using Microsoft.Xna.Framework;

namespace Nova3D.Physics.Bepu;

public sealed class PhysicsWorldOptions
{
    public Vector3 Gravity { get; init; } = new(0f, -9.81f, 0f);
    public float FixedTimeStep { get; init; } = 1f / 60f;
    public int MaximumStepsPerUpdate { get; init; } = 8;
    public int SolverIterationCount { get; init; } = 8;
    public int WorkerCount { get; init; } = Math.Clamp(Environment.ProcessorCount - 1, 1, 8);

    internal void Validate()
    {
        if (!float.IsFinite(FixedTimeStep) || FixedTimeStep <= 0f)
            throw new ArgumentOutOfRangeException(nameof(FixedTimeStep));
        if (MaximumStepsPerUpdate < 1)
            throw new ArgumentOutOfRangeException(nameof(MaximumStepsPerUpdate));
        if (SolverIterationCount < 1)
            throw new ArgumentOutOfRangeException(nameof(SolverIterationCount));
        if (WorkerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(WorkerCount));
    }
}
