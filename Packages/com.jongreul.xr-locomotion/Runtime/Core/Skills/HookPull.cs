using System;
using System.Numerics;

namespace Jongreul.XrLocomotion.Skills
{
    [Serializable]
    public sealed class HookSettings
    {
        /// <summary>훅이 닿는 거리(m).</summary>
        public float MaxRange = 15f;

        public float MaxSpeed = 12f;
        public float Acceleration = 30f;

        /// <summary>도착 전 감속도(m/s²). 남은 거리로 허용 속도 곡선을 만든다.</summary>
        public float BrakeDeceleration = 20f;

        /// <summary>훅 지점에서 이만큼 떨어지면 도착으로 본다(몸이 벽에 박히지 않게).</summary>
        public float ArrivalDistance = 0.8f;

        public float ArrivalSpeed = 1.5f;

        /// <summary>벽에 막혀 가까워지지 못해도 이 시간이 지나면 풀린다.</summary>
        public float MaxSeconds = 3f;
    }

    public enum HookState
    {
        Idle,
        Pulling,
        Arrived,
        Cancelled,
        TimedOut,
    }

    /// <summary>
    /// 훅이 박힌 지점으로 몸을 당긴다. 0에서 가속해 최고 속도까지, 도착 앞에서는
    /// v² = 도착속도² + 2·감속도·남은거리 곡선 아래로 줄이며, 한 틱에 도착 지점을 넘어가지 않는다.
    /// 매 틱 이 속도로 몸을 옮기는 것은 호출자(모터)의 몫이다.
    /// </summary>
    public sealed class HookPull
    {
        readonly HookSettings _settings;

        public HookPull(HookSettings settings = null)
        {
            _settings = settings ?? new HookSettings();
        }

        public HookSettings Settings => _settings;
        public HookState State { get; private set; }
        public Vector3 Anchor { get; private set; }
        public float Speed { get; private set; }
        public double Elapsed { get; private set; }
        public bool IsPulling => State == HookState.Pulling;

        public bool InRange(Vector3 from, Vector3 anchor) => Vector3.Distance(from, anchor) <= _settings.MaxRange;

        /// <returns>사거리 밖이면 false.</returns>
        public bool Begin(Vector3 position, Vector3 anchor)
        {
            if (!InRange(position, anchor))
                return false;

            Anchor = anchor;
            Speed = 0f;
            Elapsed = 0;
            State = HookState.Pulling;
            return true;
        }

        /// <summary>이번 틱에 몸에 줄 속도. 끝났으면 0.</summary>
        public Vector3 Step(Vector3 position, double deltaTime)
        {
            if (State != HookState.Pulling || !(deltaTime > 0))
                return Vector3.Zero;

            Elapsed += deltaTime;
            Vector3 toAnchor = Anchor - position;
            float distance = toAnchor.Length();
            float remaining = distance - _settings.ArrivalDistance;
            if (remaining <= 1e-4f)
                return End(HookState.Arrived);
            if (Elapsed >= _settings.MaxSeconds)
                return End(HookState.TimedOut);

            float dt = (float)deltaTime;
            float arrival = _settings.ArrivalSpeed;
            float brake = MathF.Sqrt(arrival * arrival + 2f * _settings.BrakeDeceleration * remaining);
            float speed = Math.Min(Math.Min(Speed + _settings.Acceleration * dt, _settings.MaxSpeed), brake);
            Speed = Math.Min(speed, remaining / dt); // 한 틱에 도착 지점을 넘지 않는다
            return toAnchor / distance * Speed;
        }

        public void Cancel()
        {
            if (State == HookState.Pulling)
                End(HookState.Cancelled);
        }

        Vector3 End(HookState state)
        {
            State = state;
            Speed = 0f;
            return Vector3.Zero;
        }
    }
}
