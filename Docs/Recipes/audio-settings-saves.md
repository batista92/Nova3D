# Audio, settings and save data

## Use when

Adding music/SFX, user options and persistent progress. Nova3D coordinates
reusable playback behavior while game code owns event meaning and saved values.

## Files

```text
Content/Content.mgcb
Nova3D.Production.Audio.AudioSystem
Game/GameSettings.cs
Game/SaveData.cs
```

## Implementation

Load compiled audio through `ContentManager`:

```csharp
SoundEffect checkpoint = Content.Load<SoundEffect>("Audio/checkpoint");
Song music = Content.Load<Song>("Audio/music");

_audio = new AudioSystem();
_checkpointPool = _audio.CreateSoundPool(checkpoint, capacity: 4);
_audio.Mixer.Music.Volume = settings.MusicVolume;
_audio.Music.Play(music, loop: true);

_checkpointPool.Play(new SoundPlaybackOptions(Volume: settings.SfxVolume));
```

Do not dispose `Content.Load` results. Stop global playback during unload:

```csharp
_audio.Dispose();
```

Keep serializable game data free of GPU/physics/UI objects:

```csharp
public sealed record GameSettings(float MusicVolume = 0.8f,
    float SfxVolume = 1f, bool Fullscreen = false);
public sealed record SaveData(int HighestUnlockedLevel = 1, double BestSeconds = 0);
```

Store JSON in a user-writable directory with a versioned atomic store:

```csharp
var paths = new GameDataPaths("MyStudio", "MyGame");
var settingsStore = new VersionedJsonStore<GameSettings>(
    paths.SettingsFile, "mygame.settings", 1,
    () => new GameSettings());

GameSettings settings = settingsStore.Load().Value;
settingsStore.Save(settings);
```

Missing or invalid files try `.bak`, then return defaults with an explicit
`PersistenceLoadSource`. See [persistence.md](../persistence.md) for validation,
migrations, bindings and save slots.

## Ownership

`ContentManager` owns compiled audio. `AudioSystem` owns created voices and its
music session. Game code owns settings/save records and file operations. UI
edits a working copy; Apply commits it to runtime systems.

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

