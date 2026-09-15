using System;
using Jongreul.XrLocomotion.Skills;
using NUnit.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Jongreul.XrLocomotion.Tests.Skills
{
    public class BoosterTests
    {
        const double Dt = 1.0 / 90;
        static readonly Vector3 LookDownForward = new Vector3(0, -0.5f, 1);

        static void Run(Booster booster, double seconds, bool grounded = true)
        {
            for (double t = 0; t < seconds - 1e-9; t += Dt)
                booster.Step(Dt, LookDownForward, grounded);
        }

        [Test]
        public void Accelerates_AlongTheFlatLookDirection_UpToMaxSpeed()
        {
            var booster = new Booster();
            Assert.That(booster.Start(), Is.True);

            Run(booster, 0.6);

            Assert.That(booster.Velocity.Y, Is.EqualTo(0f), "시선의 수직 성분은 버린다");
            Assert.That(booster.Velocity.X, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(booster.Velocity.Length(), Is.EqualTo(booster.Settings.MaxSpeed).Within(1e-3f));
        }

        [Test]
        public void InTheAir_ThrustIsWeaker()
        {
            var ground = new Booster();
            var air = new Booster();
            ground.Start();
            air.Start();

            Run(ground, 0.1, grounded: true);
            Run(air, 0.1, grounded: false);

            Assert.That(air.Velocity.Length(), Is.EqualTo(ground.Velocity.Length() * air.Settings.AirThrustScale).Within(0.05f));
        }

        [Test]
        public void StopsAfterDuration_AndUsesFuel()
        {
            var booster = new Booster();
            booster.Start();

            Run(booster, 1.0);

            Assert.That(booster.IsBoosting, Is.False);
            Assert.That(booster.Fuel, Is.EqualTo(1f - booster.Settings.Duration).Within(0.03f));
        }

        [Test]
        public void EmptyTank_CannotStart()
        {
            var booster = new Booster(new BoosterSettings { Duration = 5f });
            booster.Start();
            Run(booster, 1.2);
            Assert.That(booster.Fuel, Is.EqualTo(0f), "연료가 떨어져 꺼짐");

            Assert.That(booster.Start(), Is.False);
        }

        [Test]
        public void AfterStopping_LeftoverSpeedHalvesEachHalfLife()
        {
            var booster = new Booster();
            booster.Start();
            Run(booster, 0.5);
            booster.Stop();
            float before = booster.Velocity.Length();

            // 반감기만큼의 틱 수(float 0.3을 double로 넘기면 0.30000001이 되어 한 틱 더 도는 것을 피한다)
            int steps = (int)Math.Round(booster.Settings.InertiaHalfLife / Dt);
            for (int i = 0; i < steps; i++)
                booster.Step(Dt, LookDownForward, grounded: true);

            Assert.That(booster.Velocity.Length(), Is.EqualTo(before * 0.5f).Within(0.05f));
        }

        [Test]
        public void Fuel_RefillsOnlyAfterTheDelay()
        {
            var booster = new Booster();
            booster.Start();
            Run(booster, 1.0); // 0.7초에 꺼짐, 0.3초 지남
            float fuel = booster.Fuel;

            Run(booster, 0.4); // 꺼진 뒤 0.7초 < 0.8초
            Assert.That(booster.Fuel, Is.EqualTo(fuel));

            Run(booster, 1.0);
            Assert.That(booster.Fuel, Is.GreaterThan(fuel + 0.2f));
        }
    }

    public class BackStepTests
    {
        const double Dt = 1.0 / 90;
        static readonly Vector3 Forward = new Vector3(0, 0, 1);

        static Vector3 Run(BackStep step, double seconds)
        {
            Vector3 total = Vector3.Zero;
            for (double t = 0; t < seconds - 1e-9; t += Dt)
                total += step.Step(Dt);
            return total;
        }

        [Test]
        public void Direction_IsTheFlatOppositeOfTheLook()
        {
            Vector3 direction = BackStep.DirectionFrom(new Vector3(0, -0.6f, 0.8f), Forward);

            Assert.That(Vector3.Distance(direction, new Vector3(0, 0, -1)), Is.LessThan(1e-5f));
        }

        [Test]
        public void LookingStraightDown_UsesTheBodyDirection()
        {
            Vector3 direction = BackStep.DirectionFrom(new Vector3(0, -1, 0), new Vector3(1, 0, 0));

            Assert.That(Vector3.Distance(direction, new Vector3(-1, 0, 0)), Is.LessThan(1e-5f));
        }

        [Test]
        public void OpenSpace_DashesTheFullDistance()
        {
            var step = new BackStep();
            Assert.That(step.Begin(Forward, Forward, float.PositiveInfinity), Is.True);

            Vector3 moved = Run(step, 0.4);

            Assert.That(Vector3.Distance(moved, new Vector3(0, 0, -step.Settings.Distance)), Is.LessThan(1e-4f));
            Assert.That(step.IsDashing, Is.False);
        }

        [Test]
        public void WallBehind_StopsShortByTheMargin()
        {
            var step = new BackStep();
            step.Begin(Forward, Forward, obstacleDistance: 1.5f);

            Vector3 moved = Run(step, 0.4);

            Assert.That(moved.Length(), Is.EqualTo(1.5f - step.Settings.WallMargin).Within(1e-4f));
        }

        [Test]
        public void WallTooClose_Refuses()
        {
            var step = new BackStep();
            int started = 0;
            step.InvincibilityStarted += () => started++;

            Assert.That(step.Begin(Forward, Forward, obstacleDistance: 0.5f), Is.False);
            Assert.That(step.IsDashing, Is.False);
            Assert.That(started, Is.EqualTo(0));
        }

        [Test]
        public void Invincible_ForTheConfiguredWindow()
        {
            var step = new BackStep();
            int started = 0;
            int ended = 0;
            step.InvincibilityStarted += () => started++;
            step.InvincibilityEnded += () => ended++;

            step.Begin(Forward, Forward, float.PositiveInfinity);
            Run(step, 0.3);
            Assert.That(step.IsInvincible, Is.True);
            Run(step, 0.1);

            Assert.That(step.IsInvincible, Is.False);
            Assert.That((started, ended), Is.EqualTo((1, 1)));
        }

        [Test]
        public void EasesOut_TheFirstHalfCoversMore()
        {
            var step = new BackStep();
            step.Begin(Forward, Forward, float.PositiveInfinity);
            double half = step.Settings.Duration / 2;

            float first = Run(step, half).Length();
            float second = Run(step, half + 0.05).Length();

            Assert.That(first, Is.GreaterThan(second * 2f));
        }

        [Test]
        public void WhileDashing_ASecondBeginIsRefused()
        {
            var step = new BackStep();
            step.Begin(Forward, Forward, float.PositiveInfinity);

            Assert.That(step.Begin(Forward, Forward, float.PositiveInfinity), Is.False);
        }
    }
}
