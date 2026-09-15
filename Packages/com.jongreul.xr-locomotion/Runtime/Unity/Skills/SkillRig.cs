using UnityEngine;

namespace Jongreul.XrLocomotion
{
    /// <summary>
    /// 이동 리그 + 스킬 네 개 + 라디얼 메뉴 + 크기 보정을 한 번에 조립한다(데모와 테스트가 같은 조립을 쓴다).
    /// 슬롯은 스틱 방향과 맞췄다: 위 = 훅샷 · 오른쪽 = 부스터 · 아래 = 백스텝 · 왼쪽 = 큐브 발판.
    /// </summary>
    public sealed class SkillRig
    {
        public const int HookSlot = 0;
        public const int BoostSlot = 1;
        public const int BackSlot = 2;
        public const int CubeSlot = 3;
        public const int CubeCharges = 6;

        static readonly string[] Names = { "HOOK", "BOOST", "BACK", "CUBE" };

        public LocomotionRig Body { get; private set; }
        public SkillController Skills { get; private set; }
        public HookShotSkill Hook { get; private set; }
        public BoosterSkill Boost { get; private set; }
        public BackStepSkill Back { get; private set; }
        public MakeCubeSkill Cube { get; private set; }
        public ScaleController Scale { get; private set; }
        public RadialMenu Menu { get; private set; }

        public static Vector2 StickFor(int slot)
        {
            float angle = slot * Mathf.PI * 2f / SkillController.SlotCount;
            return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
        }

        public static SkillRig Build(Transform parent, bool withCamera = true, bool useXRDevice = true)
        {
            LocomotionRig body = LocomotionRig.Create(parent, withCamera: withCamera, useXRDevice: useXRDevice);
            GameObject root = body.Root;

            var skills = root.AddComponent<SkillController>();
            skills.Configure(body.Rig);

            var hook = root.AddComponent<HookShotSkill>();
            hook.Configure(body.Rig, body.Motor, body.RightHand);
            var boost = root.AddComponent<BoosterSkill>();
            boost.Configure(body.Rig, body.Motor);
            var back = root.AddComponent<BackStepSkill>();
            back.Configure(body.Rig, body.Motor);
            var cube = root.AddComponent<MakeCubeSkill>();
            cube.Configure(body.Rig, body.Motor, body.RightHand, parent);

            skills.Loadout.Equip(HookSlot, hook);
            skills.Loadout.Equip(BoostSlot, boost);
            skills.Loadout.Equip(BackSlot, back);
            skills.Loadout.Equip(CubeSlot, cube, CubeCharges);

            var menu = root.AddComponent<RadialMenu>();
            menu.Configure(skills, body.Rig, body.RightHand, Names);
            menu.Extra = slot => slot == BoostSlot ? $"fuel {boost.Booster.Fuel * 100f:0}%" : null;

            var scale = root.AddComponent<ScaleController>();
            scale.Configure(body, menu);

            return new SkillRig
            {
                Body = body,
                Skills = skills,
                Hook = hook,
                Boost = boost,
                Back = back,
                Cube = cube,
                Scale = scale,
                Menu = menu,
            };
        }
    }
}
