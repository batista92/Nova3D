# Audio

Nova3D keeps MonoGame audio assets and playback types visible. The reusable
layer adds category mixing, fades, bounded voice reuse, 3D playback and focus
lifecycle; it does not replace `SoundEffect`, `SoundEffectInstance`, `Song`,
`AudioListener`, `AudioEmitter` or `MediaPlayer`.

## Setup and ownership

```csharp
using Nova3D.Production.Audio;

private readonly AudioSystem _audio = new();
private SoundEffectPool _checkpointPool = null!;

protected override void LoadContent()
{
    // ContentManager retains ownership of the asset.
    SoundEffect checkpoint = Content.Load<SoundEffect>("Audio/checkpoint");
    Song music = Content.Load<Song>("Audio/music");

    // AudioSystem owns and disposes every instance created by its pools.
    _checkpointPool = _audio.CreateSoundPool(checkpoint, capacity: 4);
    _audio.Music.Play(music, loop: true, fadeIn: TimeSpan.FromSeconds(1));
}

protected override void Update(GameTime gameTime)
{
    _audio.Update(gameTime.ElapsedGameTime);
    base.Update(gameTime);
}

protected override void UnloadContent()
{
    _audio.Dispose();
    base.UnloadContent();
}
```

One `AudioSystem` must be the exclusive music controller because MonoGame's
`MediaPlayer` is process-global. Do not dispose assets returned by
`Content.Load`; do dispose `AudioSystem`, which owns the
`SoundEffectInstance`s it creates.

## Buses, mute and fades

`Master`, `Music` and `Sfx` always exist. A child bus multiplies its gain by all
parents, so a master mute affects active music and effects after `Update`.

```csharp
_audio.Mixer.Master.Volume = settings.MasterVolume;
_audio.Mixer.Music.IsMuted = settings.MusicMuted;

AudioBus ui = _audio.Mixer.CreateBus("UI", _audio.Mixer.Sfx);
ui.FadeTo(0.4f, TimeSpan.FromMilliseconds(250));
```

Volumes are finite values from zero through one. Bus fades are linear and the
mixer update is allocation-free.

## Sound pools

```csharp
SoundEffectInstance? voice = _checkpointPool.Play(
    new SoundPlaybackOptions(Volume: 0.8f, Pitch: 0f, Pan: 0f));
```

Pool capacity is fixed at construction. `Reject` returns `null` when every
voice is busy. `StealOldest` immediately reuses the oldest voice. A loop is a
native `SoundEffectInstance.IsLooped` setting; stop the returned instance or
call `StopAll` when its gameplay lifetime ends.

For spatial playback, keep MonoGame listener/emitter objects updated and pass
them directly:

```csharp
_enginePool.Play3D(listener, emitter,
    new SoundPlaybackOptions(Volume: 0.7f, IsLooped: true));
```

The pool reapplies 3D values during `Update`, so moving emitters follow the
listener without allocating new instances.

## Focus lifecycle

Forward MonoGame activation events. Nova3D resumes only voices that it paused;
it does not resume a sound that gameplay had already paused.

```csharp
Activated += (_, _) => _audio.SetActive(true);
Deactivated += (_, _) => _audio.SetActive(false);
```

New pool playback is rejected while inactive. Music and active SFX pause on
focus loss and resume on activation.

## Limits and validation

- `MediaPlayer` is global; multiple `AudioSystem` music owners are unsupported.
- Cross-fading two songs is not supported by `MediaPlayer`; fade out, change
  song, then fade in.
- Pool size is fixed; choose it from measured simultaneous voices.
- Content loading never belongs in `Update`.
- The CPU contract test covers bus hierarchy, mute, fades and allocations.
  Playback, device loss and audible balance still require an interactive test
  on each target platform.

Run the focused contract:

```powershell
dotnet run --project Benchmarks/AudioContractTests/AudioContractTests.csproj -c Release
```
