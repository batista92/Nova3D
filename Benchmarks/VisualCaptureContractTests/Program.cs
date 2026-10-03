using Nova3D.Production.VisualTesting;

TimeSpan step = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);
VisualCapturePlan plan = new("city-benchmark", 1280, 720, 9127, step, 120);
Require(plan.SceneName == "city-benchmark", "scene name");
Require(plan.Width == 1280 && plan.Height == 720, "resolution");
Require(plan.Seed == 9127, "seed");
Require(plan.FixedTimeStep == step, "fixed timestep");
Require(plan.WarmupFrames == 120 && plan.CaptureFrame == 121, "warm-up capture frame");

Expect<ArgumentException>(() => new VisualCapturePlan("../escape", 1, 1, 0, step, 0));
Expect<ArgumentOutOfRangeException>(() => new VisualCapturePlan("scene", 0, 1, 0, step, 0));
Expect<ArgumentOutOfRangeException>(() => new VisualCapturePlan("scene", 1, 0, 0, step, 0));
Expect<ArgumentOutOfRangeException>(() => new VisualCapturePlan("scene", 1, 1, 0, TimeSpan.Zero, 0));
Expect<ArgumentOutOfRangeException>(() => new VisualCapturePlan("scene", 1, 1, 0, step, -1));

Console.WriteLine(
    "Visual capture G8.1 CPU PASS | safe scene | resolution | seed | timestep | warm-up frame");

static void Expect<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static void Require(bool condition, string evidence)
{
    if (!condition)
        throw new InvalidOperationException($"Visual capture contract failed: {evidence}.");
}
