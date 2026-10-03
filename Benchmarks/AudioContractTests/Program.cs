using Nova3D.Production.Audio;

var mixer = new AudioMixer();
Require(mixer.Master.Parent is null, "master is the root bus");
Require(ReferenceEquals(mixer.Music.Parent, mixer.Master), "music belongs to master");
Require(ReferenceEquals(mixer.Sfx.Parent, mixer.Master), "SFX belongs to master");
Require(mixer.TryGetBus("music", out var music) && ReferenceEquals(music, mixer.Music),
    "bus lookup is case insensitive");

var ui = mixer.CreateBus("UI", mixer.Sfx);
mixer.Master.Volume = 0.5f;
mixer.Sfx.Volume = 0.8f;
ui.Volume = 0.25f;
Require(Near(ui.EffectiveVolume, 0.1f), "nested effective volume");
mixer.Sfx.IsMuted = true;
Require(ui.EffectiveVolume == 0f, "parent mute propagates");
mixer.Sfx.IsMuted = false;

ui.FadeTo(0.75f, TimeSpan.FromSeconds(2));
mixer.Update(1f);
Require(ui.IsFading && Near(ui.Volume, 0.5f), "linear fade midpoint");
mixer.Update(1f);
Require(!ui.IsFading && Near(ui.Volume, 0.75f), "linear fade completes exactly");
ui.FadeTo(0.2f, TimeSpan.Zero);
Require(!ui.IsFading && Near(ui.Volume, 0.2f), "zero duration fade is immediate");

Expect<InvalidOperationException>(() => mixer.CreateBus("ui"), "duplicate bus name");
Expect<ArgumentException>(() => mixer.CreateBus(" "), "empty bus name");
Expect<ArgumentException>(() => mixer.CreateBus("foreign-child", new AudioMixer().Master),
    "foreign parent bus");
Expect<ArgumentOutOfRangeException>(() => ui.Volume = float.NaN, "non-finite volume");
Expect<ArgumentOutOfRangeException>(() => ui.Volume = -0.1f, "negative volume");
Expect<ArgumentOutOfRangeException>(
    () => ui.FadeTo(1f, TimeSpan.FromSeconds(-1)), "negative fade duration");

for (int index = 0; index < 1000; index++) mixer.Update(1f / 60f);
long before = GC.GetAllocatedBytesForCurrentThread();
for (int index = 0; index < 10000; index++) mixer.Update(1f / 60f);
long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
Require(allocated == 0, $"mixer update allocated {allocated} bytes in 10k updates");

using (var system = new AudioSystem())
{
    system.Mixer.Master.FadeTo(0.5f, TimeSpan.FromSeconds(1));
    system.Update(TimeSpan.FromSeconds(0.5));
    Require(Near(system.Mixer.Master.Volume, 0.75f), "system advances mixer fades");
    system.SetActive(false);
    Require(!system.IsActive, "system records focus loss without initialized music");
    system.SetActive(true);
}

Console.WriteLine($"Audio contracts PASS | buses {mixer.Buses.Count} | allocations/update {allocated}");

static bool Near(float left, float right) => MathF.Abs(left - right) < 0.00001f;

static void Require(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException($"FAIL: {description}");
}

static void Expect<TException>(Action action, string description)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"FAIL: {description} did not throw {typeof(TException).Name}");
}
