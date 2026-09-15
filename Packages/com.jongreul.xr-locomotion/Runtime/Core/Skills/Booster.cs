using System;
using System.Numerics;

namespace Jongreul.XrLocomotion.Skills
{
    [Serializable]
    public sealed class BoosterSettings
    {
        /// <summary>지면 추진 가속도(m/s²).</summary>
        public float Thrust = 20f;

        /// <summary>공중에서는 추진이 이 비율만큼만 먹는다.</summary>
        public float AirThrustScale = 0.5f;

        /// <summary>부스트 속도 상한(m/s). 걷기 속도에 더해진다.</summary>
        public float MaxSpeed = 8f;

        public float Duration = 0.7f;

        /// <summary>연료 1 = 가득. 초당 소모량.</summary>
        public float FuelPerSecond = 1f;

        public float MinFuelToStart = 0.25f;
        public float RegenPerSecond = 0.4f;

        /// <summary>부스트가 끝나고 이 시간이 지나야 연료가 찬다.</summary>
        public float RegenDelay = 0.8f;

        /// <summary>끝난 뒤 남은 부스트 속도가 반으로 줄어드는 시간(관성 감쇠).</summary>
        public float InertiaHalfLife = 0.3f;
    }

    /// <summary>
    /// 부스터. 켜져 있는 동안 시선의 수평 방향으로 가속하고(공중에서는 약하게), 연료가 떨어지거나 시간이 다 되면 꺼진다.
    /// 꺼진 뒤 남은 속도는 반감기로 줄어든다. 결과는 걷기 속도에 더해질 수평 속도(<see cref="Velocity"/>).
    /// </summary>
    public sealed class Booster
    {
        readonly BoosterSettings _settings;
        double _elapsed;
        double _sinceEnded = double.PositiveInfinity;

        public Booster(BoosterSettings settings = null)
        {
            _settings = settings ?? new BoosterSettings();
            Fuel = 1f;
        }

        public BoosterSettings Settings => _settings;

        /// <summary>0~1.</summary>
        public float Fuel { get; private set; }

        public bool IsBoosting { get; private set; }
        public Vector3 Velocity { get; private set; }

        /// <returns>연료가 모자라거나 이미 켜져 있으면 false.</returns>
        public bool Start()
        {
            if (IsBoosting || Fuel < _settings.MinFuelToStart)
                return false;

            IsBoosting = true;
            _elapsed = 0;
            return true;
        }

        public void Stop()
        {
            if (!IsBoosting)
                return;
            IsBoosting = false;
            _sinceEnded = 0;
        }

        /// <param name="forward">시선 방향. 수직 성분은 버린다.</param>
        /// <returns>이번 틱의 부스트 속도(수평).</returns>
        public Vector3 Step(double deltaTime, Vector3 forward, bool grounded)
        {
            if (!(deltaTime > 0))
                return Velocity;

            float dt = (float)deltaTime;
            if (IsBoosting)
            {
                float thrust = _settings.Thrust * (grounded ? 1f : _settings.AirThrustScale);
                Vector3 velocity = Velocity + Flat(forward) * (thrust * dt);
                float speed = velocity.Length();
                if (speed > _settings.MaxSpeed)
                    velocity *= _settings.MaxSpeed / speed;
                Velocity = velocity;

                Fuel = Math.Max(0f, Fuel - _settings.FuelPerSecond * dt);
                _elapsed += deltaTime;
                if (_elapsed >= _settings.Duration || Fuel <= 0f)
                    Stop();
            }
            else
            {
                Velocity *= (float)Math.Pow(0.5, deltaTime / _settings.InertiaHalfLife);
                if (Velocity.LengthSquared() < 1e-6f)
                    Velocity = Vector3.Zero;

                _sinceEnded += deltaTime;
                if (_sinceEnded >= _settings.RegenDelay)
                    Fuel = Math.Min(1f, Fuel + _settings.RegenPerSecond * dt);
            }

            return Velocity;
        }

        static Vector3 Flat(Vector3 forward)
        {
            var flat = new Vector3(forward.X, 0f, forward.Z);
            return flat.LengthSquared() > 1e-8f ? Vector3.Normalize(flat) : Vector3.Zero;
        }
    }
}
