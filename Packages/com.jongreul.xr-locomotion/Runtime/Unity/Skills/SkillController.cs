using Jongreul.XrLocomotion.Framework;
using Jongreul.XrLocomotion.Radial;
using UnityEngine;

namespace Jongreul.XrLocomotion
{
    /// <summary>
    /// 스킬 입력. 오른손 스틱을 밀면 라디얼 메뉴가 열리고, 놓으면 가리키던 슬롯이 선택된다.
    /// 오른손 트리거를 누르면 조준, 떼면 실행. 매 프레임 로드아웃을 한 틱 돌린다(모터보다 먼저).
    /// </summary>
    [DefaultExecutionOrder(-75)]
    public sealed class SkillController : MonoBehaviour
    {
        public const int SlotCount = 4;

        [SerializeField] PlayerRig rig;
        [SerializeField] HandSide skillHand = HandSide.Right;
        [SerializeField, Range(0f, 1f)] float triggerPress = 0.6f;
        [SerializeField, Range(0f, 1f)] float triggerRelease = 0.35f;

        SkillLoadout _loadout;
        RadialSelector _radial;
        bool _triggerDown;

        public SkillLoadout Loadout => _loadout;
        public RadialSelector Radial => _radial;
        public HandSide SkillHand => skillHand;
        public bool TriggerDown => _triggerDown;

        void Awake()
        {
            _loadout = new SkillLoadout(SlotCount, new GameClock());
            _radial = new RadialSelector(SlotCount);
            _radial.Confirmed += slot => _loadout.Select(slot);
        }

        public void Configure(PlayerRig playerRig) => rig = playerRig;

        void Update()
        {
            if (rig != null && rig.ActiveSource != null && rig.ActiveSource.TryGetHand(skillHand, out HandInputFrame frame))
            {
                _radial.Update(frame.Stick.x, frame.Stick.y);

                if (!_triggerDown && frame.Trigger >= triggerPress)
                {
                    _triggerDown = true;
                    _loadout.Press();
                }
                else if (_triggerDown && frame.Trigger <= triggerRelease)
                {
                    _triggerDown = false;
                    _loadout.Release();
                }
            }

            _loadout.Tick(Time.deltaTime);
        }

        void OnDisable() => _loadout?.CancelAll();
    }
}
