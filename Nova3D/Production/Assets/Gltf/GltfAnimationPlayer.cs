using Microsoft.Xna.Framework;

namespace Nova3D.Production.Assets.Gltf;

/// <summary>Allocation-free TRS clip playback for one <see cref="GltfSkeletonPose"/>.</summary>
public sealed class GltfAnimationPlayer
{
    private readonly IReadOnlyList<GltfNode> _nodes;
    private readonly IReadOnlyList<GltfAnimationClip> _clips;
    private readonly GltfSkeletonPose _pose;
    private readonly Vector3[] _sourceTranslations;
    private readonly Quaternion[] _sourceRotations;
    private readonly Vector3[] _sourceScales;
    private readonly Vector3[] _targetTranslations;
    private readonly Quaternion[] _targetRotations;
    private readonly Vector3[] _targetScales;
    private int _currentClipIndex = -1;
    private int _sourceClipIndex = -1;
    private float _currentTime;
    private float _sourceTime;
    private float _blendDuration;
    private float _blendElapsed;
    private float _speed = 1f;
    private float _sourceSpeed = 1f;
    private bool _loop = true;
    private bool _sourceLoop = true;

    public GltfAnimationPlayer(GltfModel model, GltfSkeletonPose pose)
        : this(model?.Nodes ?? throw new ArgumentNullException(nameof(model)), model.Animations, pose)
    {
    }

    internal GltfAnimationPlayer(IReadOnlyList<GltfNode> nodes,
        IReadOnlyList<GltfAnimationClip> clips, GltfSkeletonPose pose)
    {
        _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        _clips = clips ?? throw new ArgumentNullException(nameof(clips));
        _pose = pose ?? throw new ArgumentNullException(nameof(pose));
        if (pose.NodeCount != nodes.Count)
            throw new ArgumentException("Pose and animation model node counts differ.", nameof(pose));
        _sourceTranslations = new Vector3[nodes.Count];
        _sourceRotations = new Quaternion[nodes.Count];
        _sourceScales = new Vector3[nodes.Count];
        _targetTranslations = new Vector3[nodes.Count];
        _targetRotations = new Quaternion[nodes.Count];
        _targetScales = new Vector3[nodes.Count];
    }

    public GltfSkeletonPose Pose => _pose;
    public bool IsPlaying { get; private set; }
    public bool IsBlending => _sourceClipIndex >= 0;
    public bool Loop => _loop;
    public float Time => _currentTime;
    public float BlendProgress => _sourceClipIndex < 0 || _blendDuration <= 0f
        ? 1f : Math.Clamp(_blendElapsed / _blendDuration, 0f, 1f);
    public GltfAnimationClip? CurrentClip => _currentClipIndex >= 0
        ? _clips[_currentClipIndex] : null;

    public float Speed
    {
        get => _speed;
        set
        {
            if (!float.IsFinite(value) || value < 0f)
                throw new ArgumentOutOfRangeException(nameof(value), "Playback speed must be finite and non-negative.");
            _speed = value;
        }
    }

    public void Play(string clipName, bool loop = true, float speed = 1f, float blendDuration = 0f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clipName);
        for (var i = 0; i < _clips.Count; i++)
        {
            if (string.Equals(_clips[i].Name, clipName, StringComparison.Ordinal))
            {
                Play(i, loop, speed, blendDuration);
                return;
            }
        }
        throw new ArgumentException($"Animation clip '{clipName}' was not found.", nameof(clipName));
    }

    public void Play(int clipIndex, bool loop = true, float speed = 1f, float blendDuration = 0f)
    {
        if ((uint)clipIndex >= (uint)_clips.Count) throw new ArgumentOutOfRangeException(nameof(clipIndex));
        if (!float.IsFinite(speed) || speed < 0f) throw new ArgumentOutOfRangeException(nameof(speed));
        if (!float.IsFinite(blendDuration) || blendDuration < 0f)
            throw new ArgumentOutOfRangeException(nameof(blendDuration));

        if (IsPlaying && blendDuration > 0f)
        {
            _sourceClipIndex = _currentClipIndex;
            _sourceTime = _currentTime;
            _sourceLoop = _loop;
            _sourceSpeed = _speed;
            _blendDuration = blendDuration;
            _blendElapsed = 0f;
        }
        else
        {
            _sourceClipIndex = -1;
            _blendDuration = 0f;
            _blendElapsed = 0f;
        }

        _currentClipIndex = clipIndex;
        _currentTime = 0f;
        _loop = loop;
        Speed = speed;
        IsPlaying = true;
        EvaluateCurrentPose();
    }

    public void Stop(bool resetToBindPose = true)
    {
        IsPlaying = false;
        _currentClipIndex = -1;
        _sourceClipIndex = -1;
        _currentTime = 0f;
        _blendDuration = 0f;
        _blendElapsed = 0f;
        if (resetToBindPose) _pose.ResetToBindPose();
    }

    public void Update(TimeSpan elapsed) => Update((float)elapsed.TotalSeconds);

    public void Update(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (!IsPlaying || _currentClipIndex < 0) return;

        var targetFinished = Advance(ref _currentTime, _clips[_currentClipIndex].Duration,
            elapsedSeconds, _speed, _loop);
        if (_sourceClipIndex >= 0)
        {
            Advance(ref _sourceTime, _clips[_sourceClipIndex].Duration,
                elapsedSeconds, _sourceSpeed, _sourceLoop);
            _blendElapsed = Math.Min(_blendElapsed + elapsedSeconds, _blendDuration);
        }

        EvaluateCurrentPose();
        if (targetFinished && _sourceClipIndex < 0) IsPlaying = false;
    }

    private void EvaluateCurrentPose()
    {
        CopyBindPose(_targetTranslations, _targetRotations, _targetScales);
        EvaluateClip(_clips[_currentClipIndex], _currentTime,
            _targetTranslations, _targetRotations, _targetScales);

        if (_sourceClipIndex < 0)
        {
            _pose.ApplyLocalPose(_targetTranslations, _targetRotations, _targetScales);
            return;
        }

        CopyBindPose(_sourceTranslations, _sourceRotations, _sourceScales);
        EvaluateClip(_clips[_sourceClipIndex], _sourceTime,
            _sourceTranslations, _sourceRotations, _sourceScales);
        var amount = BlendProgress;
        _pose.ApplyBlendedLocalPose(_sourceTranslations, _sourceRotations, _sourceScales,
            _targetTranslations, _targetRotations, _targetScales, amount);
        if (amount >= 1f)
        {
            _sourceClipIndex = -1;
            _blendDuration = 0f;
            _blendElapsed = 0f;
        }
    }

    private void CopyBindPose(Vector3[] translations, Quaternion[] rotations, Vector3[] scales)
    {
        for (var i = 0; i < _nodes.Count; i++)
        {
            translations[i] = _nodes[i].Translation;
            rotations[i] = _nodes[i].Rotation;
            scales[i] = _nodes[i].Scale;
        }
    }

    private static void EvaluateClip(GltfAnimationClip clip, float time,
        Vector3[] translations, Quaternion[] rotations, Vector3[] scales)
    {
        for (var i = 0; i < clip.Channels.Count; i++)
        {
            var channel = clip.Channels[i];
            var value = Sample(clip.Samplers[channel.SamplerIndex], time,
                channel.TargetPath == GltfAnimationTargetPath.Rotation);
            switch (channel.TargetPath)
            {
                case GltfAnimationTargetPath.Translation:
                    translations[channel.NodeIndex] = new Vector3(value.X, value.Y, value.Z);
                    break;
                case GltfAnimationTargetPath.Rotation:
                    rotations[channel.NodeIndex] = new Quaternion(value.X, value.Y, value.Z, value.W);
                    break;
                case GltfAnimationTargetPath.Scale:
                    scales[channel.NodeIndex] = new Vector3(value.X, value.Y, value.Z);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private static Vector4 Sample(GltfAnimationSampler sampler, float time, bool rotation)
    {
        var count = sampler.Times.Count;
        if (count == 1 || time <= sampler.Times[0]) return sampler.Values[0];
        if (time >= sampler.Times[count - 1]) return sampler.Values[count - 1];

        var low = 0;
        var high = count - 1;
        while (high - low > 1)
        {
            var middle = (low + high) >> 1;
            if (time < sampler.Times[middle]) high = middle;
            else low = middle;
        }
        if (sampler.Interpolation == GltfAnimationInterpolation.Step)
            return sampler.Values[low];

        var amount = (time - sampler.Times[low]) /
                     (sampler.Times[high] - sampler.Times[low]);
        if (!rotation) return Vector4.Lerp(sampler.Values[low], sampler.Values[high], amount);
        var from = sampler.Values[low];
        var to = sampler.Values[high];
        var result = Quaternion.Normalize(Quaternion.Slerp(
            new Quaternion(from.X, from.Y, from.Z, from.W),
            new Quaternion(to.X, to.Y, to.Z, to.W), amount));
        return new Vector4(result.X, result.Y, result.Z, result.W);
    }

    private static bool Advance(ref float time, float duration, float elapsed,
        float speed, bool loop)
    {
        if (duration <= 0f)
        {
            time = 0f;
            return !loop;
        }
        time += elapsed * speed;
        if (loop)
        {
            if (time >= duration) time %= duration;
            return false;
        }
        if (time < duration) return false;
        time = duration;
        return true;
    }
}
