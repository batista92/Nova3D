using Gum.Forms.Controls;
using Microsoft.Xna.Framework;

namespace Nova3D.UI.Gum;

/// <summary>A fixed-capacity notification pool that never recreates controls during update.</summary>
public sealed class GumNotificationQueue
{
    private readonly Label[] _labels;
    private readonly float[] _remainingSeconds;
    private int _next;

    public GumNotificationQueue(StackPanel container, int capacity, Func<int, Label>? createLabel = null)
    {
        ArgumentNullException.ThrowIfNull(container);
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

        _labels = new Label[capacity];
        _remainingSeconds = new float[capacity];
        for (int i = 0; i < capacity; i++)
        {
            Label label = createLabel?.Invoke(i) ?? new Label();
            label.Visual.Visible = false;
            _labels[i] = label;
            container.AddChild(label);
        }
    }

    public int Capacity => _labels.Length;
    public int ActiveCount { get; private set; }

    public void Enqueue(string text, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));

        int index = FindAvailableIndex();
        bool wasInactive = _remainingSeconds[index] <= 0f;
        _labels[index].Text = text;
        _labels[index].Visual.Visible = true;
        _remainingSeconds[index] = (float)duration.TotalSeconds;
        if (wasInactive) ActiveCount++;
        _next = (index + 1) % _labels.Length;
    }

    public void Update(GameTime gameTime)
    {
        float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
        for (int i = 0; i < _labels.Length; i++)
        {
            if (_remainingSeconds[i] <= 0f) continue;
            _remainingSeconds[i] -= elapsed;
            if (_remainingSeconds[i] > 0f) continue;
            _remainingSeconds[i] = 0f;
            _labels[i].Visual.Visible = false;
            ActiveCount--;
        }
    }

    public void Clear()
    {
        for (int i = 0; i < _labels.Length; i++)
        {
            _remainingSeconds[i] = 0f;
            _labels[i].Visual.Visible = false;
        }
        ActiveCount = 0;
    }

    private int FindAvailableIndex()
    {
        for (int offset = 0; offset < _labels.Length; offset++)
        {
            int index = (_next + offset) % _labels.Length;
            if (_remainingSeconds[index] <= 0f) return index;
        }
        return _next;
    }
}
