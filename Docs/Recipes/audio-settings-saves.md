# Audio, settings and save data

## Use when

Adding music/SFX, user options and persistent progress. Nova3D does not wrap
these MonoGame/game responsibilities.

## Files

```text
Content/Content.mgcb
Game/AudioController.cs
Game/GameSettings.cs
Game/SaveData.cs
```

## Implementation

Load compiled audio through `ContentManager`:

```csharp
_checkpointSound = Content.Load<SoundEffect>("Audio/checkpoint");
_music = Content.Load<Song>("Audio/music");
MediaPlayer.IsRepeating = true;
MediaPlayer.Volume = settings.MusicVolume;
MediaPlayer.Play(_music);

_checkpointSound.Play(settings.SfxVolume, 0f, 0f);
```

Do not dispose `Content.Load` results. Stop global playback during unload:

```csharp
MediaPlayer.Stop();
```

Keep serializable game data free of GPU/physics/UI objects:

```csharp
public sealed record GameSettings(float MusicVolume = 0.8f,
    float SfxVolume = 1f, bool Fullscreen = false);
public sealed record SaveData(int HighestUnlockedLevel = 1, double BestSeconds = 0);
```

Store JSON in a user-writable directory:

```csharp
string root = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "MyStudio", "MyGame");
Directory.CreateDirectory(root);
string json = JsonSerializer.Serialize(settings);
File.WriteAllText(Path.Combine(root, "settings.json"), json);
```

Load defensively: missing or invalid files return defaults and log the failure.
Write a temporary file first, then replace the last valid save.

## Ownership

`ContentManager` owns compiled audio. Game code owns settings/save records and
file operations. UI edits a working copy; Apply commits it to runtime systems.

## Validate

- separate music and SFX sliders persist after restart;
- mute/volume changes affect active playback;
- missing/corrupt JSON falls back without crashing;
- save path is outside installation/publish directories;
- checkpoint sound does not allocate/load content on every activation.

## Common failures

- loading audio in `Update`: stutter and repeated allocations;
- disposing `Content.Load` assets manually: later consumers fail;
- saving beside executable: fails in protected installations;
- serializing physics handles or Gum controls: unstable/nonportable saves.

