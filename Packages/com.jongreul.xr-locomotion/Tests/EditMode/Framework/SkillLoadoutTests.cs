using System.Collections.Generic;
using Jongreul.XrLocomotion.Framework;
using NUnit.Framework;

namespace Jongreul.XrLocomotion.Tests.Framework
{
    sealed class FakeSkill : ISkill
    {
        readonly int _durationTicks;
        int _ticks;

        public FakeSkill(string id, double cooldown = 1, bool locks = true, int durationTicks = 3)
        {
            Id = id;
            Cooldown = cooldown;
            LocksOthers = locks;
            _durationTicks = durationTicks;
        }

        public string Id { get; }
        public double Cooldown { get; }
        public bool LocksOthers { get; }
        public bool Ready = true;
        public int Prepares;
        public int Executes;
        public int Cancels;

        public void Prepare() => Prepares++;

        public bool Execute()
        {
            if (!Ready)
                return false;
            Executes++;
            _ticks = 0;
            return true;
        }

        public bool Tick(double deltaTime) => ++_ticks >= _durationTicks;

        public void Cancel() => Cancels++;
    }

    public class SkillLoadoutTests
    {
        ManualClock _clock;
        SkillLoadout _loadout;
        FakeSkill _hook;
        FakeSkill _cube;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock();
            _loadout = new SkillLoadout(3, _clock);
            _hook = new FakeSkill("hook", cooldown: 2, locks: true, durationTicks: 3);
            _cube = new FakeSkill("cube", cooldown: 1, locks: false, durationTicks: 1);
            _loadout.Equip(0, _hook, charges: 2);
            _loadout.Equip(1, _cube);
        }

        void Use(int slot)
        {
            _loadout.Select(slot);
            _loadout.Press();
            _loadout.Release();
        }

        void RunToEnd()
        {
            for (int i = 0; i < 10; i++)
                _loadout.Tick(0.1);
        }

        [Test]
        public void PressThenRelease_ExecutesSelected_AndUsesACharge()
        {
            _loadout.Select(0);

            Assert.That(_loadout.Press(), Is.EqualTo(SkillDenyReason.None));
            Assert.That(_hook.Prepares, Is.EqualTo(1));
            Assert.That(_loadout.Release(), Is.EqualTo(SkillDenyReason.None));
            Assert.That(_hook.Executes, Is.EqualTo(1));
            Assert.That(_loadout.IsRunning(0), Is.True);
            Assert.That(_loadout.Charges(0), Is.EqualTo(1));
        }

        [Test]
        public void EmptySlot_IsDenied()
        {
            _loadout.Select(2);

            Assert.That(_loadout.Press(), Is.EqualTo(SkillDenyReason.EmptySlot));
        }

        [Test]
        public void Cooldown_StartsWhenTheSkillEnds_NotAtExecute()
        {
            Use(0);
            _clock.Advance(5);
            Assert.That(_loadout.CooldownRemaining(0), Is.EqualTo(0), "실행 중에는 쿨타임이 돌지 않는다");

            RunToEnd();

            Assert.That(_loadout.CooldownRemaining(0), Is.EqualTo(2).Within(1e-9));
            Assert.That(_loadout.CanUse(0), Is.EqualTo(SkillDenyReason.CoolingDown));
            _clock.Advance(2);
            Assert.That(_loadout.CanUse(0), Is.EqualTo(SkillDenyReason.None));
        }

        [Test]
        public void RunningOutOfCharges_IsDenied_UntilRefilled()
        {
            for (int i = 0; i < 2; i++)
            {
                Use(0);
                RunToEnd();
                _clock.Advance(2);
            }

            Assert.That(_loadout.Charges(0), Is.EqualTo(0));
            _loadout.Select(0);
            Assert.That(_loadout.Press(), Is.EqualTo(SkillDenyReason.NoCharges));

            _loadout.AddCharges(0, 1);
            Assert.That(_loadout.Press(), Is.EqualTo(SkillDenyReason.None));
        }

        [Test]
        public void UnlimitedSlot_NeverRunsOut()
        {
            for (int i = 0; i < 5; i++)
            {
                Use(1);
                RunToEnd();
                _clock.Advance(1);
            }

            Assert.That(_cube.Executes, Is.EqualTo(5));
            Assert.That(_loadout.Charges(1), Is.EqualTo(SkillLoadout.Unlimited));
        }

        [Test]
        public void LockingSkillRunning_BlocksOtherSlots()
        {
            Use(0);
            _loadout.Select(1);

            Assert.That(_loadout.Press(), Is.EqualTo(SkillDenyReason.Busy));
            Assert.That(_cube.Prepares, Is.EqualTo(0));

            RunToEnd();
            Assert.That(_loadout.Press(), Is.EqualTo(SkillDenyReason.None), "끝나면 풀린다");
        }

        [Test]
        public void NonLockingSkillRunning_AllowsAnother()
        {
            var slow = new FakeSkill("slow", cooldown: 0, locks: false, durationTicks: 5);
            _loadout.Equip(2, slow);
            Use(2);

            _loadout.Select(0);
            Assert.That(_loadout.Press(), Is.EqualTo(SkillDenyReason.None));
            Assert.That(_loadout.Release(), Is.EqualTo(SkillDenyReason.None));
            Assert.That(_loadout.IsRunning(0) && _loadout.IsRunning(2), Is.True);
        }

        [Test]
        public void SameSkillWhileRunning_IsBusy()
        {
            var slow = new FakeSkill("slow", cooldown: 0, locks: false, durationTicks: 5);
            _loadout.Equip(2, slow);
            Use(2);

            _loadout.Select(2);
            Assert.That(_loadout.Press(), Is.EqualTo(SkillDenyReason.Busy));
        }

        [Test]
        public void CancelWhilePreparing_CostsNothing()
        {
            _loadout.Select(0);
            _loadout.Press();

            _loadout.CancelAll();

            Assert.That(_hook.Cancels, Is.EqualTo(1));
            Assert.That(_loadout.PreparingSlot, Is.EqualTo(-1));
            Assert.That(_loadout.Charges(0), Is.EqualTo(2));
            Assert.That(_loadout.CooldownRemaining(0), Is.EqualTo(0));
        }

        [Test]
        public void CancelWhileRunning_EndsAsCancelled_AndStartsCooldown()
        {
            bool? cancelled = null;
            _loadout.Ended += (_, c) => cancelled = c;
            Use(0);

            _loadout.CancelAll();

            Assert.That(cancelled, Is.True);
            Assert.That(_loadout.IsRunning(0), Is.False);
            Assert.That(_hook.Cancels, Is.EqualTo(1));
            Assert.That(_loadout.CooldownRemaining(0), Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void SkillRefusingToExecute_IsNotReady_AndCostsNothing()
        {
            _hook.Ready = false;
            _loadout.Select(0);
            _loadout.Press();

            Assert.That(_loadout.Release(), Is.EqualTo(SkillDenyReason.NotReady));
            Assert.That(_loadout.IsRunning(0), Is.False);
            Assert.That(_loadout.Charges(0), Is.EqualTo(2));
            Assert.That(_loadout.CooldownRemaining(0), Is.EqualTo(0));
        }

        [Test]
        public void SwitchingSlotWhilePreparing_CancelsThePreparation()
        {
            _loadout.Select(0);
            _loadout.Press();

            _loadout.Select(1);

            Assert.That(_hook.Cancels, Is.EqualTo(1));
            Assert.That(_loadout.PreparingSlot, Is.EqualTo(-1));
            Assert.That(_loadout.Release(), Is.EqualTo(SkillDenyReason.NotPreparing));
        }

        [Test]
        public void Events_FireInOrder()
        {
            var log = new List<string>();
            _loadout.Prepared += s => log.Add($"prepared {s}");
            _loadout.Started += s => log.Add($"started {s}");
            _loadout.Ended += (s, _) => log.Add($"ended {s}");

            Use(0);
            RunToEnd();

            Assert.That(log, Is.EqualTo(new[] { "prepared 0", "started 0", "ended 0" }));
        }

        [Test]
        public void Denied_ReportsTheReason()
        {
            SkillDenyReason? reason = null;
            _loadout.Denied += (_, r) => reason = r;
            Use(0);
            RunToEnd();

            _loadout.Select(0);
            _loadout.Press();

            Assert.That(reason, Is.EqualTo(SkillDenyReason.CoolingDown));
        }

        [Test]
        public void CooldownProgress_GoesFromZeroToOne()
        {
            Use(0);
            RunToEnd();

            Assert.That(_loadout.CooldownProgress(0), Is.EqualTo(0).Within(1e-9));
            _clock.Advance(1);
            Assert.That(_loadout.CooldownProgress(0), Is.EqualTo(0.5).Within(1e-9));
            _clock.Advance(1);
            Assert.That(_loadout.CooldownProgress(0), Is.EqualTo(1));
        }

        [Test]
        public void Unequip_WhileRunning_CancelsIt()
        {
            Use(0);

            _loadout.Unequip(0);

            Assert.That(_hook.Cancels, Is.EqualTo(1));
            Assert.That(_loadout.Get(0), Is.Null);
            Assert.That(_loadout.CanUse(0), Is.EqualTo(SkillDenyReason.EmptySlot));
        }
    }
}
