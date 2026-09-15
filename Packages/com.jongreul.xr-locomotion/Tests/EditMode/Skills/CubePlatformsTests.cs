using System.Collections.Generic;
using Jongreul.XrLocomotion.Skills;
using NUnit.Framework;

namespace Jongreul.XrLocomotion.Tests.Skills
{
    public class CubePlatformsTests
    {
        ManualClock _clock;
        CubePlatforms _cubes;
        List<(int Id, CubeRemoveReason Reason)> _removed;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock();
            _cubes = new CubePlatforms(_clock, new CubeSettings
            {
                MaxCount = 3, Lifetime = 6f, StandGrace = 2f, MaxLifetime = 12f,
            });
            _removed = new List<(int, CubeRemoveReason)>();
            _cubes.Removed += (id, reason) => _removed.Add((id, reason));
        }

        [Test]
        public void SpawnsUpToMax_ThenReplacesTheOldest()
        {
            for (int i = 0; i < 4; i++)
                _cubes.Spawn();

            Assert.That(_cubes.Count, Is.EqualTo(3));
            Assert.That(_cubes.Ids, Is.EqualTo(new[] { 2, 3, 4 }));
            Assert.That(_removed, Is.EqualTo(new[] { (1, CubeRemoveReason.Replaced) }));
        }

        [Test]
        public void ExpiresAfterItsLifetime()
        {
            int id = _cubes.Spawn();

            _clock.Advance(5.9);
            Assert.That(_cubes.Update(), Is.EqualTo(0));
            _clock.Advance(0.2);
            Assert.That(_cubes.Update(), Is.EqualTo(1));

            Assert.That(_cubes.Count, Is.EqualTo(0));
            Assert.That(_removed, Is.EqualTo(new[] { (id, CubeRemoveReason.Expired) }));
        }

        [Test]
        public void StandingOnIt_NearTheEnd_KeepsItForTheGrace()
        {
            int id = _cubes.Spawn();
            _clock.Advance(5.5);

            Assert.That(_cubes.Stand(id), Is.True);

            Assert.That(_cubes.Remaining(id), Is.EqualTo(2).Within(1e-9));
            _clock.Advance(1.9);
            Assert.That(_cubes.Update(), Is.EqualTo(0), "밟는 동안 발밑이 사라지지 않는다");
        }

        [Test]
        public void Extension_IsCappedAtMaxLifetime()
        {
            int id = _cubes.Spawn();
            for (int second = 1; second <= 11; second++)
            {
                _clock.Advance(1);
                _cubes.Stand(id);
                Assert.That(_cubes.Update(), Is.EqualTo(0), $"{second}초");
            }

            _clock.Advance(1); // 12초
            _cubes.Stand(id);
            Assert.That(_cubes.Update(), Is.EqualTo(1));
        }

        [Test]
        public void StandingOnAnUnknownCube_IsFalse()
        {
            Assert.That(_cubes.Stand(42), Is.False);
        }

        [Test]
        public void Clear_RemovesAll()
        {
            _cubes.Spawn();
            _cubes.Spawn();

            _cubes.Clear();

            Assert.That(_cubes.Count, Is.EqualTo(0));
            Assert.That(_removed.TrueForAll(r => r.Reason == CubeRemoveReason.Cleared), Is.True);
            Assert.That(_removed.Count, Is.EqualTo(2));
        }
    }
}
