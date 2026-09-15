using System;
using Jongreul.XrLocomotion.Skills;
using NUnit.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Jongreul.XrLocomotion.Tests.Skills
{
    public class HookPullTests
    {
        const double Dt = 1.0 / 90;

        struct RunResult
        {
            public int Steps;
            public float MinDistance;
            public float MaxSpeed;
            public float LastSpeed;
            public float FirstSpeed;
            public float SecondSpeed;
        }

        static RunResult Run(HookPull hook, ref Vector3 position, bool move = true)
        {
            var result = new RunResult { MinDistance = float.MaxValue };
            while (hook.IsPulling && result.Steps < 5000)
            {
                Vector3 velocity = hook.Step(position, Dt);
                float speed = velocity.Length();
                if (speed > 0f)
                {
                    result.LastSpeed = speed;
                    result.MaxSpeed = Math.Max(result.MaxSpeed, speed);
                    if (result.Steps == 0)
                        result.FirstSpeed = speed;
                    else if (result.Steps == 1)
                        result.SecondSpeed = speed;
                }

                if (move)
                    position += velocity * (float)Dt;
                result.MinDistance = Math.Min(result.MinDistance, Vector3.Distance(position, hook.Anchor));
                result.Steps++;
            }

            return result;
        }

        [Test]
        public void OutOfRange_IsRefused()
        {
            var hook = new HookPull(new HookSettings { MaxRange = 15f });

            Assert.That(hook.Begin(Vector3.Zero, new Vector3(0, 0, 20)), Is.False);
            Assert.That(hook.State, Is.EqualTo(HookState.Idle));
        }

        [Test]
        public void PullsToTheAnchor_AndStopsAtArrivalDistance()
        {
            var hook = new HookPull();
            var anchor = new Vector3(0, 5, 10);
            Vector3 position = Vector3.Zero;
            Assert.That(hook.Begin(position, anchor), Is.True);

            Run(hook, ref position);

            float arrival = hook.Settings.ArrivalDistance;
            Assert.That(hook.State, Is.EqualTo(HookState.Arrived));
            Assert.That(Vector3.Distance(position, anchor), Is.InRange(arrival - 1e-3f, arrival + 0.05f));
        }

        [Test]
        public void NeverOvershootsTheArrivalPoint()
        {
            var hook = new HookPull();
            Vector3 position = Vector3.Zero;
            hook.Begin(position, new Vector3(3, 1, 12));

            RunResult result = Run(hook, ref position);

            Assert.That(result.MinDistance, Is.GreaterThanOrEqualTo(hook.Settings.ArrivalDistance - 1e-3f));
        }

        [Test]
        public void Speed_IsCappedAtMaxSpeed()
        {
            var hook = new HookPull();
            Vector3 position = Vector3.Zero;
            hook.Begin(position, new Vector3(0, 0, 14.5f));

            RunResult result = Run(hook, ref position);

            Assert.That(result.MaxSpeed, Is.LessThanOrEqualTo(hook.Settings.MaxSpeed + 1e-4f));
            Assert.That(result.MaxSpeed, Is.GreaterThan(hook.Settings.MaxSpeed - 0.5f), "먼 거리면 상한까지 붙는다");
        }

        [Test]
        public void StartsFromRest_AndArrivesSlowly()
        {
            var hook = new HookPull();
            Vector3 position = Vector3.Zero;
            hook.Begin(position, new Vector3(0, 0, 10));

            RunResult result = Run(hook, ref position);

            Assert.That(result.FirstSpeed, Is.EqualTo(hook.Settings.Acceleration * (float)Dt).Within(1e-4f));
            Assert.That(result.SecondSpeed, Is.GreaterThan(result.FirstSpeed));
            Assert.That(result.LastSpeed, Is.LessThan(2.5f), $"도착 직전 속도 {result.LastSpeed}");
        }

        [Test]
        public void Cancel_StopsImmediately()
        {
            var hook = new HookPull();
            Vector3 position = Vector3.Zero;
            hook.Begin(position, new Vector3(0, 0, 10));
            hook.Step(position, Dt);

            hook.Cancel();

            Assert.That(hook.State, Is.EqualTo(HookState.Cancelled));
            Assert.That(hook.Step(position, Dt), Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void BlockedByAWall_TimesOut()
        {
            var hook = new HookPull();
            Vector3 position = Vector3.Zero;
            hook.Begin(position, new Vector3(0, 0, 10));

            Run(hook, ref position, move: false); // 몸이 벽에 막혀 제자리

            Assert.That(hook.State, Is.EqualTo(HookState.TimedOut));
            Assert.That(hook.Elapsed, Is.GreaterThanOrEqualTo(hook.Settings.MaxSeconds));
        }
    }
}
