using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework.Input;

namespace Nova3D.Production.Input;

/// <summary>Versioned JSON persistence for one action map. Call only outside the frame hot path.</summary>
public static class InputBindingJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static string Serialize(InputActionMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var actions = map.Actions.Select(action => new ActionData
        {
            Name = action.Name,
            Kind = action.Kind,
            Bindings = action.Bindings.Select(BindingData.FromBinding).ToArray()
        }).ToArray();
        return JsonSerializer.Serialize(new ProfileData { Version = 1, Actions = actions }, Options);
    }

    /// <summary>Validates the entire profile before changing any action. Existing action objects remain valid.</summary>
    public static void Apply(InputActionMap map, string json)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var profile = JsonSerializer.Deserialize<ProfileData>(json, Options)
            ?? throw new JsonException("Input profile must be an object.");
        if (profile.Version != 1) throw new JsonException($"Unsupported input profile version {profile.Version}.");
        if (profile.Actions is null || profile.Actions.Length != map.Actions.Count)
            throw new JsonException("Input profile must contain exactly the registered actions.");
        var replacements = new Dictionary<string, InputBinding[]>(StringComparer.Ordinal);
        foreach (var item in profile.Actions)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Name) ||
                !map.TryGet(item.Name, out var action) || item.Kind != action.Kind ||
                item.Bindings is null || !replacements.TryAdd(item.Name,
                    item.Bindings.Select(binding => binding?.ToBinding() ??
                        throw new JsonException("Null input binding.")).ToArray()))
                throw new JsonException("Unknown, duplicated or mismatched input action.");
        }
        foreach (var action in map.Actions)
            map.ReplaceBindings(action.Name, replacements[action.Name]);
    }

    private sealed class ProfileData
    {
        public required int Version { get; init; }
        public required ActionData[] Actions { get; init; }
    }

    private sealed class ActionData
    {
        public required string Name { get; init; }
        public required InputActionKind Kind { get; init; }
        public required BindingData[] Bindings { get; init; }
    }

    private sealed class BindingData
    {
        public required string Type { get; init; }
        public Keys? Key { get; init; }
        public Keys? Negative { get; init; }
        public Keys? Positive { get; init; }
        public Keys? Down { get; init; }
        public Keys? Up { get; init; }
        public InputMouseButton? MouseButton { get; init; }
        public Buttons? GamePadButton { get; init; }
        public InputGamePadAxis? GamePadAxis { get; init; }
        public InputGamePadStick? GamePadStick { get; init; }
        public InputMouseAxis? MouseAxis { get; init; }
        public float? Deadzone { get; init; }
        public float? Scale { get; init; }
        public bool? Invert { get; init; }

        public static BindingData FromBinding(InputBinding binding) => new()
        {
            Type = binding.Source.ToString(),
            Key = binding.Source == InputBindingKind.Key ? binding.Positive : null,
            Negative = binding.Source is InputBindingKind.KeyboardAxis or InputBindingKind.KeyboardVector
                ? binding.Negative : null,
            Positive = binding.Source is InputBindingKind.KeyboardAxis or InputBindingKind.KeyboardVector
                ? binding.Positive : null,
            Down = binding.Source == InputBindingKind.KeyboardVector ? binding.Down : null,
            Up = binding.Source == InputBindingKind.KeyboardVector ? binding.Up : null,
            MouseButton = binding.Source == InputBindingKind.MouseButton ? binding.MouseButton : null,
            GamePadButton = binding.Source == InputBindingKind.GamePadButton ? binding.GamePadButton : null,
            GamePadAxis = binding.Source == InputBindingKind.GamePadAxis ? binding.GamePadAxis : null,
            GamePadStick = binding.Source == InputBindingKind.GamePadStick ? binding.GamePadStick : null,
            MouseAxis = binding.Source == InputBindingKind.MouseAxis ? binding.MouseAxis : null,
            Deadzone = binding.Source is InputBindingKind.GamePadAxis or InputBindingKind.GamePadStick
                ? binding.Deadzone : null,
            Scale = binding.ActionKind != InputActionKind.Digital ? binding.Scale : null,
            Invert = binding.ActionKind != InputActionKind.Digital ? binding.Invert : null
        };

        public InputBinding ToBinding()
        {
            if (!Enum.TryParse<InputBindingKind>(Type, ignoreCase: false, out var type) ||
                !Enum.IsDefined(type))
                throw new JsonException($"Unknown input binding type '{Type}'.");
            try
            {
                return type switch
                {
                    InputBindingKind.Key => InputBinding.Key(RequiredKey(Key)),
                    InputBindingKind.MouseButton => InputBinding.MouseButtonInput(Required(MouseButton)),
                    InputBindingKind.GamePadButton => InputBinding.GamePadButtonInput(Required(GamePadButton)),
                    InputBindingKind.KeyboardAxis => InputBinding.KeyboardAxis(
                        RequiredKey(Negative), RequiredKey(Positive), Scale ?? 1f, Invert ?? false),
                    InputBindingKind.GamePadAxis => InputBinding.GamePadAxisInput(
                        Required(GamePadAxis), Deadzone ?? 0.15f, Scale ?? 1f, Invert ?? false),
                    InputBindingKind.MouseAxis => InputBinding.MouseAxisInput(
                        Required(MouseAxis), Scale ?? 1f, Invert ?? false),
                    InputBindingKind.KeyboardVector => InputBinding.KeyboardVector(
                        RequiredKey(Negative), RequiredKey(Positive), RequiredKey(Down), RequiredKey(Up),
                        Scale ?? 1f, Invert ?? false),
                    InputBindingKind.GamePadStick => InputBinding.GamePadStickInput(
                        Required(GamePadStick), Deadzone ?? 0.15f, Scale ?? 1f, Invert ?? false),
                    InputBindingKind.MouseDelta => InputBinding.MouseDelta(Scale ?? 1f, Invert ?? false),
                    _ => throw new JsonException("Unsupported input binding type.")
                };
            }
            catch (ArgumentException exception)
            {
                throw new JsonException("Invalid input binding value.", exception);
            }
        }

        private static T Required<T>(T? value) where T : struct, Enum =>
            value is { } result && Enum.IsDefined(result)
                ? result : throw new JsonException("Missing or invalid input control.");

        private static Keys RequiredKey(Keys? value) =>
            value is { } key && key != Keys.None && Enum.IsDefined(key)
                ? key : throw new JsonException("Missing or invalid input key.");
    }
}
