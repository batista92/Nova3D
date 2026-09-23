using Microsoft.Xna.Framework;
using Nova3D.Physics.Bepu;

internal static class PhysicsGateValidation
{
    public static PhysicsGateResult Run()
    {
        ulong first = RunDeterministicScenario();
        ulong second = RunDeterministicScenario();
        if (first != second)
            throw new InvalidOperationException(
                $"Single-thread physics diverged: {first:X16} != {second:X16}.");

        long allocatedBytesPerStep = MeasureSteadyStateAllocations();
        if (allocatedBytesPerStep > 64)
            throw new InvalidOperationException(
                $"Physics steady-state allocated {allocatedBytesPerStep} bytes/step.");

        return new PhysicsGateResult(first, allocatedBytesPerStep);
    }

    private static ulong RunDeterministicScenario()
    {
        using var world = new BepuPhysicsWorld(new PhysicsWorldOptions
        {
            WorkerCount = 1,
            FixedTimeStep = 1f / 60f
        });
        world.CreateStaticBox(new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f));

        var bodies = new BepuBody[64];
        for (int i = 0; i < bodies.Length; i++)
        {
            int x = i % 8;
            int z = i / 8;
            bodies[i] = world.CreateDynamicBox(
                new Vector3(x * 1.05f - 3.7f, 1f + (i % 4) * 1.1f, z * 1.05f - 3.7f),
                Vector3.One);
            bodies[i].LinearVelocity = new Vector3(
                ((i * 17) % 7 - 3) * 0.1f,
                0f,
                ((i * 29) % 7 - 3) * 0.1f);
        }

        for (int step = 0; step < 360; step++)
            world.Update(world.FixedTimeStep);

        ulong hash = 14695981039346656037UL;
        foreach (BepuBody body in bodies)
        {
            hash = Hash(hash, body.Position.X);
            hash = Hash(hash, body.Position.Y);
            hash = Hash(hash, body.Position.Z);
            hash = Hash(hash, body.Rotation.X);
            hash = Hash(hash, body.Rotation.Y);
            hash = Hash(hash, body.Rotation.Z);
            hash = Hash(hash, body.Rotation.W);
            hash = Hash(hash, body.LinearVelocity.X);
            hash = Hash(hash, body.LinearVelocity.Y);
            hash = Hash(hash, body.LinearVelocity.Z);
        }
        return hash;
    }

    private static long MeasureSteadyStateAllocations()
    {
        using var world = new BepuPhysicsWorld(new PhysicsWorldOptions
        {
            Gravity = Vector3.Zero,
            WorkerCount = 1
        });
        for (int i = 0; i < 1_000; i++)
        {
            int x = i % 40;
            int z = i / 40;
            BepuBody body = world.CreateDynamicSphere(
                new Vector3(x * 2f, (i % 5) * 2f, z * 2f), 0.25f);
            body.LinearVelocity = new Vector3(0.01f, 0f, 0.01f);
        }

        for (int step = 0; step < 30; step++)
            world.Update(world.FixedTimeStep);

        const int measuredSteps = 240;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int step = 0; step < measuredSteps; step++)
            world.Update(world.FixedTimeStep);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        return allocated / measuredSteps;
    }

    private static ulong Hash(ulong hash, float value)
    {
        hash ^= unchecked((uint)BitConverter.SingleToInt32Bits(value));
        return hash * 1099511628211UL;
    }
}

internal readonly record struct PhysicsGateResult(ulong DeterminismHash, long AllocatedBytesPerStep);
