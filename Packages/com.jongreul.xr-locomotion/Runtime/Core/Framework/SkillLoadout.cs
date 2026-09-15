using System;

namespace Jongreul.XrLocomotion.Framework
{
    public enum SkillDenyReason
    {
        None,
        EmptySlot,
        CoolingDown,
        NoCharges,
        /// <summary>같은 스킬이 이미 실행 중이거나, 다른 스킬이 잠금을 걸고 실행 중이다.</summary>
        Busy,
        /// <summary>조준 대상이 없는 등 스킬 스스로 실행을 거절했다. 수량·쿨타임은 쓰지 않는다.</summary>
        NotReady,
        /// <summary>조준 중인 스킬 없이 떼었다.</summary>
        NotPreparing,
    }

    /// <summary>
    /// 스킬 하나. 누르고 있는 동안 <see cref="Prepare"/>(조준), 떼면 <see cref="Execute"/>, 실행 중에는 매 틱
    /// <see cref="Tick"/>이 불리고 true를 돌려주면 끝난다. 실행하지 않고 끝내려면 <see cref="Cancel"/>.
    /// </summary>
    public interface ISkill
    {
        string Id { get; }

        /// <summary>끝난 뒤 다시 쓸 수 있을 때까지(초).</summary>
        double Cooldown { get; }

        /// <summary>실행 중 다른 스킬을 막는가. 몸을 옮기는 스킬끼리는 겹치면 안 된다.</summary>
        bool LocksOthers { get; }

        void Prepare();

        /// <returns>false면 발동하지 않는다(조준 대상 없음 등).</returns>
        bool Execute();

        /// <returns>true면 끝.</returns>
        bool Tick(double deltaTime);

        void Cancel();
    }

    /// <summary>
    /// 스킬 슬롯 묶음. 선택한 슬롯을 누르면 조준, 떼면 실행한다. 거절 규칙: 빈 슬롯 · 쿨타임 · 수량 0 ·
    /// 잠금 스킬 실행 중(또는 같은 스킬 실행 중). 수량은 실제로 발동했을 때만 줄고, 쿨타임은 스킬이 끝난 순간부터 센다.
    /// 시작·끝 이벤트는 이펙트·사운드·네트워크 동기화를 거는 자리다.
    /// </summary>
    public sealed class SkillLoadout
    {
        public const int Unlimited = -1;

        sealed class Slot
        {
            public ISkill Skill;
            public int Charges;
            public double CooldownEndsAt = double.NegativeInfinity;
            public bool Running;
        }

        readonly Slot[] _slots;
        readonly IClock _clock;
        int _preparing = -1;

        public SkillLoadout(int slotCount, IClock clock)
        {
            if (slotCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(slotCount));
            _slots = new Slot[slotCount];
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public int SlotCount => _slots.Length;
        public int Selected { get; private set; }

        /// <summary>조준 중인 슬롯. 없으면 -1.</summary>
        public int PreparingSlot => _preparing;

        /// <summary>조준 시작.</summary>
        public event Action<int> Prepared;

        /// <summary>실행 시작.</summary>
        public event Action<int> Started;

        /// <summary>실행 끝(두 번째 인자: 취소로 끝났는가).</summary>
        public event Action<int, bool> Ended;

        public event Action<int, SkillDenyReason> Denied;

        public ISkill Get(int slot) => Valid(slot) ? _slots[slot]?.Skill : null;

        public bool IsRunning(int slot) => Valid(slot) && _slots[slot] != null && _slots[slot].Running;

        /// <summary>남은 수량. 무제한이면 <see cref="Unlimited"/>.</summary>
        public int Charges(int slot) => Valid(slot) && _slots[slot] != null ? _slots[slot].Charges : 0;

        public bool AnyLockingRunning
        {
            get
            {
                foreach (Slot slot in _slots)
                {
                    if (slot != null && slot.Running && slot.Skill.LocksOthers)
                        return true;
                }

                return false;
            }
        }

        public void Equip(int slot, ISkill skill, int charges = Unlimited)
        {
            Validate(slot);
            if (skill == null)
                throw new ArgumentNullException(nameof(skill));
            if (charges < Unlimited)
                throw new ArgumentOutOfRangeException(nameof(charges));

            Unequip(slot);
            _slots[slot] = new Slot { Skill = skill, Charges = charges };
        }

        public void Unequip(int slot)
        {
            Validate(slot);
            if (_slots[slot] == null)
                return;
            if (_preparing == slot)
                CancelPreparing();
            if (_slots[slot].Running)
                Finish(slot, cancelled: true);
            _slots[slot] = null;
        }

        /// <summary>슬롯을 고른다. 다른 슬롯을 조준하던 중이면 그 조준은 취소된다.</summary>
        public void Select(int slot)
        {
            Validate(slot);
            if (_preparing >= 0 && _preparing != slot)
                CancelPreparing();
            Selected = slot;
        }

        /// <summary>지금 이 슬롯을 쓸 수 있는가(쓸 수 없으면 이유).</summary>
        public SkillDenyReason CanUse(int slot)
        {
            Validate(slot);
            Slot s = _slots[slot];
            if (s == null)
                return SkillDenyReason.EmptySlot;
            if (s.Running || AnyLockingRunning)
                return SkillDenyReason.Busy;
            if (CooldownRemaining(slot) > 0)
                return SkillDenyReason.CoolingDown;
            if (s.Charges == 0)
                return SkillDenyReason.NoCharges;
            return SkillDenyReason.None;
        }

        /// <summary>선택한 슬롯 버튼을 눌렀다 → 조준 시작.</summary>
        public SkillDenyReason Press()
        {
            if (_preparing >= 0)
                return SkillDenyReason.None; // 이미 조준 중

            int slot = Selected;
            SkillDenyReason reason = CanUse(slot);
            if (reason != SkillDenyReason.None)
            {
                Denied?.Invoke(slot, reason);
                return reason;
            }

            _preparing = slot;
            _slots[slot].Skill.Prepare();
            Prepared?.Invoke(slot);
            return SkillDenyReason.None;
        }

        /// <summary>버튼을 뗐다 → 조준하던 스킬 실행.</summary>
        public SkillDenyReason Release()
        {
            if (_preparing < 0)
                return SkillDenyReason.NotPreparing;

            int slot = _preparing;
            _preparing = -1;
            Slot s = _slots[slot];

            // 조준하는 동안 다른 잠금 스킬이 시작됐을 수 있다.
            SkillDenyReason reason = CanUse(slot);
            if (reason != SkillDenyReason.None)
            {
                s.Skill.Cancel();
                Denied?.Invoke(slot, reason);
                return reason;
            }

            if (!s.Skill.Execute())
            {
                Denied?.Invoke(slot, SkillDenyReason.NotReady);
                return SkillDenyReason.NotReady;
            }

            if (s.Charges > 0)
                s.Charges--;
            s.Running = true;
            Started?.Invoke(slot);
            return SkillDenyReason.None;
        }

        /// <summary>조준 중인 것과 실행 중인 것을 모두 취소한다(실행 중이던 것은 쿨타임이 시작된다).</summary>
        public void CancelAll()
        {
            if (_preparing >= 0)
                CancelPreparing();
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && _slots[i].Running)
                    Finish(i, cancelled: true);
            }
        }

        public void Tick(double deltaTime)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot s = _slots[i];
                if (s != null && s.Running && s.Skill.Tick(deltaTime))
                    Finish(i, cancelled: false);
            }
        }

        public double CooldownRemaining(int slot)
        {
            Validate(slot);
            Slot s = _slots[slot];
            return s == null ? 0 : Math.Max(0, s.CooldownEndsAt - _clock.Now);
        }

        /// <summary>쿨타임 고리 표시용 0(방금 끝남)~1(사용 가능).</summary>
        public double CooldownProgress(int slot)
        {
            Slot s = Valid(slot) ? _slots[slot] : null;
            if (s == null || s.Skill.Cooldown <= 0)
                return 1;
            return 1 - Math.Min(1, CooldownRemaining(slot) / s.Skill.Cooldown);
        }

        /// <summary>수량을 채운다(무제한 슬롯은 그대로).</summary>
        public void AddCharges(int slot, int amount)
        {
            Validate(slot);
            Slot s = _slots[slot];
            if (s == null || s.Charges == Unlimited || amount <= 0)
                return;
            s.Charges += amount;
        }

        void Finish(int slot, bool cancelled)
        {
            Slot s = _slots[slot];
            if (cancelled)
                s.Skill.Cancel();
            s.Running = false;
            s.CooldownEndsAt = _clock.Now + Math.Max(0, s.Skill.Cooldown);
            Ended?.Invoke(slot, cancelled);
        }

        void CancelPreparing()
        {
            int slot = _preparing;
            _preparing = -1;
            _slots[slot]?.Skill.Cancel();
        }

        bool Valid(int slot) => slot >= 0 && slot < _slots.Length;

        void Validate(int slot)
        {
            if (!Valid(slot))
                throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }
}
