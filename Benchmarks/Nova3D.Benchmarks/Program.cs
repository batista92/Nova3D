using Nova3D.Benchmarks;

string? captureDirectory = null;
string? performanceReport = null;
VisualBenchmarkScene captureScene = VisualBenchmarkScene.City;
for (int index = 0; index < args.Length; index++)
{
    if (args[index] == "--capture" && index + 1 < args.Length && captureDirectory is null)
    {
        captureDirectory = args[++index];
        continue;
    }

    if (args[index] == "--scene" && index + 1 < args.Length)
    {
        if (!Enum.TryParse(args[++index], ignoreCase: true, out captureScene))
        {
            Console.Error.WriteLine("Unknown scene. Use City, Pbr or Gltf.");
            return 2;
        }
        continue;
    }

    if (args[index] == "--performance-report" && index + 1 < args.Length && performanceReport is null)
    {
        performanceReport = args[++index];
        continue;
    }

    Console.Error.WriteLine("Usage: Nova3D.Benchmarks [--capture OUTPUT_DIRECTORY] [--scene City|Pbr|Gltf] [--performance-report OUTPUT.json]");
    return 2;
}

if (captureDirectory is not null && performanceReport is not null)
{
    Console.Error.WriteLine("--capture and --performance-report are mutually exclusive.");
    return 2;
}
if (captureDirectory is null && captureScene != VisualBenchmarkScene.City)
{
    Console.Error.WriteLine("--scene is supported only with --capture.");
    return 2;
}

using var game = new Nova3DBenchmarkGame(captureDirectory, captureScene, performanceReport);
game.Run();
return 0;
