using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Nova3D.Production.Scenes.BuiltIns;

internal static class SceneBuiltInProperties
{
    public static void ValidateKnown(
        SceneComponentValidationContext context,
        params string[] names)
    {
        var known = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (var property in context.Component.Properties.EnumerateObject())
        {
            if (!known.Contains(property.Name))
            {
                context.Report(
                    SceneBuiltInValidationCodes.UnknownProperty,
                    $"Property '{property.Name}' is not supported by component '{context.Component.Type}'.",
                    property.Name);
            }
        }
    }

    public static bool RequireString(
        SceneComponentValidationContext context,
        string name,
        out string value)
    {
        value = string.Empty;
        if (!context.Component.Properties.TryGetProperty(name, out var property))
        {
            context.Report(SceneBuiltInValidationCodes.MissingProperty,
                $"Property '{name}' is required.", name);
            return false;
        }
        if (property.ValueKind != JsonValueKind.String)
        {
            context.Report(SceneBuiltInValidationCodes.InvalidPropertyType,
                $"Property '{name}' must be a string.", name);
            return false;
        }

        value = property.GetString()!;
        if (string.IsNullOrWhiteSpace(value))
        {
            context.Report(SceneBuiltInValidationCodes.InvalidPropertyValue,
                $"Property '{name}' cannot be empty.", name);
            return false;
        }
        return true;
    }

    public static bool OptionalBoolean(
        SceneComponentValidationContext context,
        string name,
        bool defaultValue)
    {
        if (!context.Component.Properties.TryGetProperty(name, out var property))
            return defaultValue;
        if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return property.GetBoolean();

        context.Report(SceneBuiltInValidationCodes.InvalidPropertyType,
            $"Property '{name}' must be a boolean.", name);
        return defaultValue;
    }

    public static float OptionalNumber(
        SceneComponentValidationContext context,
        string name,
        float defaultValue,
        Func<float, bool> isValid,
        string requirement)
    {
        if (!context.Component.Properties.TryGetProperty(name, out var property))
            return defaultValue;
        if (property.ValueKind != JsonValueKind.Number ||
            !property.TryGetSingle(out var value) ||
            !float.IsFinite(value))
        {
            context.Report(SceneBuiltInValidationCodes.InvalidPropertyType,
                $"Property '{name}' must be a finite number.", name);
            return defaultValue;
        }
        if (!isValid(value))
        {
            context.Report(SceneBuiltInValidationCodes.InvalidPropertyValue,
                $"Property '{name}' {requirement}.", name);
            return defaultValue;
        }
        return value;
    }

    public static Vector3 OptionalColor(
        SceneComponentValidationContext context,
        string name,
        Vector3 defaultValue)
    {
        if (!context.Component.Properties.TryGetProperty(name, out var property))
            return defaultValue;
        if (property.ValueKind != JsonValueKind.Array || property.GetArrayLength() != 3)
        {
            context.Report(SceneBuiltInValidationCodes.InvalidPropertyType,
                $"Property '{name}' must be an array of three finite numbers.", name);
            return defaultValue;
        }

        var values = new float[3];
        var index = 0;
        foreach (var element in property.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Number ||
                !element.TryGetSingle(out values[index]) ||
                !float.IsFinite(values[index]))
            {
                context.Report(SceneBuiltInValidationCodes.InvalidPropertyType,
                    $"Property '{name}' must be an array of three finite numbers.", name);
                return defaultValue;
            }
            index++;
        }

        var value = new Vector3(values[0], values[1], values[2]);
        if (value.X < 0f || value.Y < 0f || value.Z < 0f)
        {
            context.Report(SceneBuiltInValidationCodes.InvalidPropertyValue,
                $"Property '{name}' components must be greater than or equal to zero.", name);
            return defaultValue;
        }
        return value;
    }

    public static bool ReadBoolean(SceneComponentDocument component, string name, bool defaultValue) =>
        component.Properties.TryGetProperty(name, out var property) ? property.GetBoolean() : defaultValue;

    public static float ReadNumber(SceneComponentDocument component, string name, float defaultValue) =>
        component.Properties.TryGetProperty(name, out var property) ? property.GetSingle() : defaultValue;

    public static string ReadString(SceneComponentDocument component, string name) =>
        component.Properties.GetProperty(name).GetString()!;

    public static Vector3 ReadColor(SceneComponentDocument component, string name, Vector3 defaultValue)
    {
        if (!component.Properties.TryGetProperty(name, out var property))
            return defaultValue;
        var values = property.EnumerateArray().Select(value => value.GetSingle()).ToArray();
        return new Vector3(values[0], values[1], values[2]);
    }

    public static Vector3 Forward(Matrix transform) =>
        Vector3.Normalize(Vector3.TransformNormal(Vector3.Forward, transform));

    public static Vector3 CameraUp(Matrix transform, Vector3 direction)
    {
        var candidate = Vector3.Normalize(Vector3.TransformNormal(Vector3.Up, transform));
        var right = Vector3.Cross(direction, candidate);
        if (right.LengthSquared() < 0.000001f)
            right = Vector3.Cross(direction, Math.Abs(direction.Y) < 0.99f ? Vector3.Up : Vector3.Right);
        right.Normalize();
        return Vector3.Normalize(Vector3.Cross(right, direction));
    }
}
