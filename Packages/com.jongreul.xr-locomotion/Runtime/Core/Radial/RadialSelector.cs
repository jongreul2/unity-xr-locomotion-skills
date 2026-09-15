using System;

namespace Jongreul.XrLocomotion.Radial
{
    /// <summary>
    /// 스틱 방향 → 슬롯. 0번이 위(12시), 시계 방향으로 번호가 는다. 데드존 안이면 선택 없음(-1).
    /// 경계에서 흔들리지 않도록 이미 고른 칸은 칸 중심에서 (반 칸 + 여유 각도)를 넘어야 바뀐다.
    /// 스틱을 놓아 데드존으로 돌아오는 순간 마지막 칸을 확정한다(<see cref="Confirmed"/>, 한 번).
    /// </summary>
    public sealed class RadialSelector
    {
        public RadialSelector(int sectors, float deadzone = 0.35f, float hysteresisDegrees = 8f)
        {
            if (sectors < 2)
                throw new ArgumentOutOfRangeException(nameof(sectors));
            if (!(deadzone >= 0f && deadzone < 1f))
                throw new ArgumentOutOfRangeException(nameof(deadzone));
            if (!(hysteresisDegrees >= 0f && hysteresisDegrees < 180f / sectors))
                throw new ArgumentOutOfRangeException(nameof(hysteresisDegrees));

            Sectors = sectors;
            Deadzone = deadzone;
            HysteresisDegrees = hysteresisDegrees;
        }

        public int Sectors { get; }
        public float Deadzone { get; }
        public float HysteresisDegrees { get; }
        public float SectorWidth => 360f / Sectors;

        /// <summary>스틱이 데드존 밖에 있어 메뉴가 열려 있다.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>지금 가리키는 칸(-1 = 없음).</summary>
        public int Current { get; private set; } = -1;

        public event Action<int> Confirmed;

        /// <summary>위 = 0°, 시계 방향 [0, 360).</summary>
        public static float AngleOf(float x, float y) => Wrap((float)(Math.Atan2(x, y) * 180.0 / Math.PI));

        public int SectorOf(float angleDegrees) => (int)(Wrap(angleDegrees + SectorWidth * 0.5f) / SectorWidth) % Sectors;

        public float CenterOf(int sector) => sector * SectorWidth;

        /// <summary>스틱 값(각 축 -1~1)을 넣는다.</summary>
        /// <returns>지금 가리키는 칸(-1 = 데드존).</returns>
        public int Update(float x, float y)
        {
            if (MathF.Sqrt(x * x + y * y) < Deadzone)
            {
                if (IsOpen)
                {
                    IsOpen = false;
                    int picked = Current;
                    Current = -1;
                    if (picked >= 0)
                        Confirmed?.Invoke(picked);
                }

                return -1;
            }

            IsOpen = true;
            float angle = AngleOf(x, y);
            int raw = SectorOf(angle);
            if (Current < 0 || raw == Current ||
                AngularDistance(angle, CenterOf(Current)) > SectorWidth * 0.5f + HysteresisDegrees)
                Current = raw;
            return Current;
        }

        static float AngularDistance(float a, float b)
        {
            float d = Wrap(a - b);
            return d > 180f ? 360f - d : d;
        }

        static float Wrap(float degrees)
        {
            degrees %= 360f;
            return degrees < 0f ? degrees + 360f : degrees;
        }
    }
}
