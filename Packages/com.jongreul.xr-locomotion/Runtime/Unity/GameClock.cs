using UnityEngine;

namespace Jongreul.XrLocomotion
{
    /// <summary>게임 시간(Time.timeAsDouble). 일시정지·시간 배율을 따른다 — 스킬 쿨타임·발판 수명은 게임 시간으로 센다.</summary>
    public sealed class GameClock : IClock
    {
        public double Now => Time.timeAsDouble;
    }
}
