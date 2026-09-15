using System;
using System.Numerics;

namespace Jongreul.XrLocomotion.Skills
{
    [Serializable]
    public sealed class BackStepSettings
    {
        public float Distance = 2.5f;
        public float Duration = 0.18f;

        /// <summary>뒤쪽 장애물에서 이만큼 떨어져 멈춘다(벽 속으로 들어가지 않게).</summary>
        public float WallMargin = 0.35f;

        /// <summary>이보다 짧게밖에 못 가면 발동하지 않는다.</summary>
        public float MinDistance = 0.3f;

        /// <summary>시작부터 이 시간 동안 무적.</summary>
        public float InvincibleSeconds = 0.35f;
    }

    /// <summary>
    /// 백스텝. 시선의 수평 반대 방향으로 짧게 대시한다(처음이 빠르고 끝이 느린 감속 곡선).
    /// 뒤에 벽이 있으면 벽 앞(여유 거리)까지만 가고, 너무 가까우면 발동하지 않는다. 시작부터 잠깐 무적이다.
    /// 장애물 거리는 호출자가 레이캐스트로 재서 넣는다.
    /// </summary>
    public sealed class BackStep
    {
        readonly BackStepSettings _settings;
        double _elapsed;
        float _travelled;
        bool _invincible;

        public BackStep(BackStepSettings settings = null)
        {
            _settings = settings ?? new BackStepSettings();
        }

        public BackStepSettings Settings => _settings;
        public bool IsDashing { get; private set; }
        public bool IsInvincible => _invincible;
        public Vector3 Direction { get; private set; }
        public float PlannedDistance { get; private set; }

        public event Action InvincibilityStarted;
        public event Action InvincibilityEnded;

        /// <summary>시선의 수평 반대 방향. 거의 수직으로 보고 있으면 fallbackForward(몸 방향)의 반대.</summary>
        public static Vector3 DirectionFrom(Vector3 lookForward, Vector3 fallbackForward)
        {
            var flat = new Vector3(lookForward.X, 0f, lookForward.Z);
            if (flat.LengthSquared() < 1e-4f)
                flat = new Vector3(fallbackForward.X, 0f, fallbackForward.Z);
            return flat.LengthSquared() > 1e-8f ? -Vector3.Normalize(flat) : Vector3.Zero;
        }

        /// <param name="obstacleDistance">그 방향 첫 장애물까지 거리. 없으면 +∞.</param>
        public float Plan(float obstacleDistance) =>
            Math.Min(_settings.Distance, Math.Max(0f, obstacleDistance - _settings.WallMargin));

        /// <returns>이미 대시 중이거나 갈 수 있는 거리가 너무 짧으면 false.</returns>
        public bool Begin(Vector3 lookForward, Vector3 fallbackForward, float obstacleDistance)
        {
            if (IsDashing)
                return false;

            Vector3 direction = DirectionFrom(lookForward, fallbackForward);
            float distance = Plan(obstacleDistance);
            if (direction == Vector3.Zero || distance < _settings.MinDistance)
                return false;

            Direction = direction;
            PlannedDistance = distance;
            _elapsed = 0;
            _travelled = 0f;
            IsDashing = true;
            if (!_invincible)
            {
                _invincible = true;
                InvincibilityStarted?.Invoke();
            }

            return true;
        }

        /// <summary>대시를 멈추고 무적도 끝낸다(스킬 취소·순간이동).</summary>
        public void Cancel()
        {
            IsDashing = false;
            if (_invincible)
            {
                _invincible = false;
                InvincibilityEnded?.Invoke();
            }
        }

        /// <summary>이번 틱에 옮길 변위.</summary>
        public Vector3 Step(double deltaTime)
        {
            if (!(deltaTime > 0) || (!IsDashing && !_invincible))
                return Vector3.Zero;

            _elapsed += deltaTime;
            Vector3 delta = Vector3.Zero;
            if (IsDashing)
            {
                float t = (float)Math.Min(1.0, _elapsed / _settings.Duration);
                float target = PlannedDistance * (1f - (1f - t) * (1f - t));
                delta = Direction * (target - _travelled);
                _travelled = target;
                if (t >= 1f)
                    IsDashing = false;
            }

            if (_invincible && _elapsed >= _settings.InvincibleSeconds)
            {
                _invincible = false;
                InvincibilityEnded?.Invoke();
            }

            return delta;
        }
    }
}
