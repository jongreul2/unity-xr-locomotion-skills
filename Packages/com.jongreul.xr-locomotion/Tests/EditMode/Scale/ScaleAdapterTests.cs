using System;
using Jongreul.XrLocomotion.Scale;
using NUnit.Framework;

namespace Jongreul.XrLocomotion.Tests.Scale
{
    public class ScaleAdapterTests
    {
        const double Dt = 1.0 / 90;

        static void Settle(ScaleAdapter adapter)
        {
            for (int i = 0; i < 1000 && adapter.IsTransitioning; i++)
                adapter.Step(Dt);
        }

        [Test]
        public void Values_AreProportionalToScale()
        {
            var baseline = new ScaleBaseline();

            var big = new ScaledValues(2f, baseline);
            var small = new ScaledValues(0.5f, baseline);

            Assert.That(big.MoveSpeed, Is.EqualTo(baseline.MoveSpeed * 2f));
            Assert.That(big.GrabRadius, Is.EqualTo(baseline.GrabRadius * 2f));
            Assert.That(big.AudioMaxDistance, Is.EqualTo(baseline.AudioMaxDistance * 2f));
            Assert.That(small.UiDistance, Is.EqualTo(baseline.UiDistance * 0.5f));
            Assert.That(small.NearClip, Is.EqualTo(baseline.NearClip * 0.5f));
            Assert.That(small.StepHeight, Is.EqualTo(baseline.StepHeight * 0.5f));
        }

        [Test]
        public void Target_IsClampedToRange()
        {
            var adapter = new ScaleAdapter(minScale: 0.25f, maxScale: 4f);

            adapter.SetImmediate(10f);
            Assert.That(adapter.Current, Is.EqualTo(4f));
            adapter.SetImmediate(0.01f);
            Assert.That(adapter.Current, Is.EqualTo(0.25f));
        }

        [Test]
        public void Transition_ReachesTheTargetExactly_AtItsDuration()
        {
            var adapter = new ScaleAdapter(transitionSeconds: 0.6f);
            adapter.SetTarget(2f);

            for (int i = 0; i < 53; i++) // 0.589초
                adapter.Step(Dt);
            Assert.That(adapter.Current, Is.LessThan(2f));

            adapter.Step(Dt);
            adapter.Step(Dt);
            Assert.That(adapter.Current, Is.EqualTo(2f));
            Assert.That(adapter.IsTransitioning, Is.False);
        }

        [Test]
        public void Transition_IsMonotonic()
        {
            var adapter = new ScaleAdapter();
            adapter.SetTarget(3f);
            float previous = adapter.Current;

            while (adapter.IsTransitioning)
            {
                adapter.Step(Dt);
                Assert.That(adapter.Current, Is.GreaterThanOrEqualTo(previous));
                previous = adapter.Current;
            }
        }

        [Test]
        public void HalfwayFromHalfToDouble_IsOne_InLogSpace()
        {
            var adapter = new ScaleAdapter(transitionSeconds: 0.6f);
            adapter.SetImmediate(0.5f);
            adapter.SetTarget(2f);

            adapter.Step(0.3);

            Assert.That(adapter.Current, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void ManyRoundTrips_ComeBackExactlyToBaseline()
        {
            var baseline = new ScaleBaseline();
            var adapter = new ScaleAdapter(baseline);
            var random = new Random(7);

            for (int i = 0; i < 20; i++)
            {
                adapter.SetTarget(0.3f + (float)random.NextDouble() * 3.4f);
                Settle(adapter);
            }

            adapter.SetTarget(1f);
            Settle(adapter);

            ScaledValues values = adapter.Values;
            Assert.That(values.Scale, Is.EqualTo(1f));
            Assert.That(values.MoveSpeed, Is.EqualTo(baseline.MoveSpeed), "누적 오차 없음");
            Assert.That(values.GrabRadius, Is.EqualTo(baseline.GrabRadius));
            Assert.That(values.NearClip, Is.EqualTo(baseline.NearClip));
            Assert.That(values.AudioMaxDistance, Is.EqualTo(baseline.AudioMaxDistance));
        }

        [Test]
        public void Retargeting_MidTransition_ContinuesFromWhereItIs()
        {
            var adapter = new ScaleAdapter();
            adapter.SetTarget(2f);
            for (int i = 0; i < 18; i++)
                adapter.Step(Dt);
            float current = adapter.Current;

            adapter.SetTarget(0.5f);
            adapter.Step(Dt);

            Assert.That(Math.Abs(adapter.Current - current), Is.LessThan(0.01f), "튀지 않는다");
        }

        [Test]
        public void Changed_FiresOnlyWhenScaleMoves()
        {
            var adapter = new ScaleAdapter();
            int changes = 0;
            adapter.Changed += _ => changes++;

            Assert.That(adapter.Step(Dt), Is.False);
            adapter.SetTarget(1.5f);
            adapter.Step(Dt);

            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void ZeroTransitionTime_AppliesImmediately()
        {
            var adapter = new ScaleAdapter(transitionSeconds: 0f);

            adapter.SetTarget(2f);

            Assert.That(adapter.Current, Is.EqualTo(2f));
        }
    }
}
