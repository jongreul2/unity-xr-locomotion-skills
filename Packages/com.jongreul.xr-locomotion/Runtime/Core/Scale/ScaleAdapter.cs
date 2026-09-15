using System;

namespace Jongreul.XrLocomotion.Scale
{
    /// <summary>1배일 때의 값. 플레이어 크기에 비례해야 하는 것들.</summary>
    [Serializable]
    public sealed class ScaleBaseline
    {
        public float MoveSpeed = 2.5f;
        public float GrabRadius = 0.08f;
        public float UiDistance = 0.5f;
        public float AudioMinDistance = 1f;
        public float AudioMaxDistance = 20f;
        public float NearClip = 0.02f;
        public float StepHeight = 0.35f;
    }

    public readonly struct ScaledValues
    {
        public readonly float Scale;
        public readonly float MoveSpeed;
        public readonly float GrabRadius;
        public readonly float UiDistance;
        public readonly float AudioMinDistance;
        public readonly float AudioMaxDistance;
        public readonly float NearClip;
        public readonly float StepHeight;

        public ScaledValues(float scale, ScaleBaseline b)
        {
            Scale = scale;
            MoveSpeed = b.MoveSpeed * scale;
            GrabRadius = b.GrabRadius * scale;
            UiDistance = b.UiDistance * scale;
            AudioMinDistance = b.AudioMinDistance * scale;
            AudioMaxDistance = b.AudioMaxDistance * scale;
            NearClip = b.NearClip * scale;
            StepHeight = b.StepHeight * scale;
        }
    }

    /// <summary>
    /// 플레이어 크기 변경(거대화·축소) 보정. 크기가 바뀌면 이동 속도·손 잡는 반경·UI 거리·소리 감쇠 거리·카메라 근접 클리핑·
    /// 계단 높이를 함께 비례로 맞춘다. 값은 언제나 1배 기준값에서 새로 계산하므로 여러 번 커졌다 작아져도
    /// 1배로 돌아오면 정확히 원래 값이다(곱셈을 쌓지 않는다). 전환은 로그 공간에서 부드럽게 — 0.5→2배와 2→0.5배가 같은 속도로 느껴진다.
    /// </summary>
    public sealed class ScaleAdapter
    {
        readonly ScaleBaseline _baseline;
        float _from = 1f;
        double _elapsed;

        public ScaleAdapter(ScaleBaseline baseline = null, float minScale = 0.25f, float maxScale = 4f,
            float transitionSeconds = 0.6f)
        {
            if (!(minScale > 0f && maxScale >= minScale))
                throw new ArgumentOutOfRangeException(nameof(minScale));
            if (!(transitionSeconds >= 0f))
                throw new ArgumentOutOfRangeException(nameof(transitionSeconds));

            _baseline = baseline ?? new ScaleBaseline();
            MinScale = minScale;
            MaxScale = maxScale;
            TransitionSeconds = transitionSeconds;
        }

        public float MinScale { get; }
        public float MaxScale { get; }
        public float TransitionSeconds { get; }
        public ScaleBaseline Baseline => _baseline;
        public float Current { get; private set; } = 1f;
        public float Target { get; private set; } = 1f;
        public bool IsTransitioning => Current != Target;
        public ScaledValues Values => new ScaledValues(Current, _baseline);

        public event Action<ScaledValues> Changed;

        /// <summary>목표 크기(범위 밖은 붙인다). 전환 중에 바꾸면 지금 크기에서 이어서 간다.</summary>
        public void SetTarget(float scale)
        {
            float target = Math.Clamp(scale, MinScale, MaxScale);
            if (target == Target)
                return;

            _from = Current;
            Target = target;
            _elapsed = 0;
            if (TransitionSeconds <= 0f)
                Apply(Target);
        }

        public void SetImmediate(float scale)
        {
            Target = Math.Clamp(scale, MinScale, MaxScale);
            _from = Target;
            Apply(Target);
        }

        /// <returns>이번 틱에 크기가 바뀌었으면 true.</returns>
        public bool Step(double deltaTime)
        {
            if (Current == Target || !(deltaTime > 0))
                return false;

            _elapsed += deltaTime;
            float k = (float)Math.Min(1.0, _elapsed / TransitionSeconds);
            if (k >= 1f)
            {
                Apply(Target);
                return true;
            }

            float eased = k * k * (3f - 2f * k);
            float log = MathF.Log(_from) + (MathF.Log(Target) - MathF.Log(_from)) * eased;
            Apply(MathF.Exp(log));
            return true;
        }

        void Apply(float scale)
        {
            Current = scale;
            Changed?.Invoke(Values);
        }
    }
}
