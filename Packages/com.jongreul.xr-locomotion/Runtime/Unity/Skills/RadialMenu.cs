using System;
using Jongreul.XrLocomotion.Framework;
using Jongreul.XrLocomotion.Radial;
using UnityEngine;
using UnityEngine.UI;

namespace Jongreul.XrLocomotion
{
    /// <summary>
    /// 스킬 손 위의 라디얼 메뉴. 스틱을 밀면 네 칸이 펼쳐지고 가리키는 칸이 커진다(칸마다 쿨타임 점·남은 수량).
    /// 닫혀 있을 때는 선택한 스킬과 상태 한 줄만 보인다. 거절되면 이유를 잠깐 빨갛게 띄운다.
    /// </summary>
    public sealed class RadialMenu : MonoBehaviour
    {
        const int Dots = 8;
        const float SectorRadius = 0.09f;

        sealed class Sector
        {
            public Transform Root;
            public Renderer Panel;
            public Text Charges;
            public Renderer[] Dots;
        }

        SkillController _skills;
        PlayerRig _rig;
        Hand _hand;
        string[] _names;
        Sector[] _sectors;
        Transform _root;
        Transform _wheel;
        Text _status;
        float _deniedFor;
        string _deniedText;

        /// <summary>손 위로 띄우는 거리(m). 크기 보정이 바꾼다.</summary>
        public float Offset { get; set; } = 0.17f; // 아래 칸이 손바닥에 가리지 않을 만큼

        /// <summary>메뉴 크기 배율. 크기 보정이 바꾼다.</summary>
        public float Scale { get; set; } = 1f;

        /// <summary>슬롯별 덧붙일 상태(부스터 연료 등).</summary>
        public Func<int, string> Extra { get; set; }

        public bool IsOpen => _wheel != null && _wheel.gameObject.activeSelf;
        public Transform Root => _root;
        public string StatusText => _status != null ? _status.text : string.Empty;

        public void Configure(SkillController skills, PlayerRig rig, Hand hand, string[] names)
        {
            _skills = skills;
            _rig = rig;
            _hand = hand;
            _names = names;

            _root = new GameObject("RadialMenu").transform;
            _root.SetParent(transform, false);
            _wheel = new GameObject("Wheel").transform;
            _wheel.SetParent(_root, false);

            _sectors = new Sector[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / names.Length;
                var sector = new Sector { Root = new GameObject($"Sector{i}").transform };
                sector.Root.SetParent(_wheel, false);
                sector.Root.localPosition = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * SectorRadius;
                sector.Panel = Kit.Primitive(PrimitiveType.Cube, sector.Root, "Panel", Vector3.zero,
                    new Vector3(0.08f, 0.05f, 0.006f), Kit.Surface).GetComponent<Renderer>();
                Kit.Label(sector.Root, "Name", new Vector3(0f, 0.006f, -0.005f), new Vector2(0.08f, 0.022f), 17).text = names[i];
                sector.Charges = Kit.Label(sector.Root, "Charges", new Vector3(0f, -0.013f, -0.005f), new Vector2(0.08f, 0.016f), 12,
                    TextAnchor.MiddleCenter, Kit.Ink);
                sector.Dots = new Renderer[Dots];
                for (int d = 0; d < Dots; d++)
                {
                    sector.Dots[d] = Kit.Primitive(PrimitiveType.Sphere, sector.Root, $"Dot{d}",
                        new Vector3((d - (Dots - 1) * 0.5f) * 0.009f, -0.033f, 0f), Vector3.one * 0.006f, Kit.Surface)
                        .GetComponent<Renderer>();
                }

                _sectors[i] = sector;
            }

            _status = Kit.Label(_root, "Status", new Vector3(0f, 0f, -0.005f), new Vector2(0.2f, 0.03f), 18,
                TextAnchor.MiddleCenter, Kit.Warn);
            skills.Loadout.Denied += OnDenied;
        }

        void OnDenied(int slot, SkillDenyReason reason)
        {
            _deniedFor = 1.2f;
            _deniedText = $"{_names[slot]}  {Describe(reason)}";
        }

        static string Describe(SkillDenyReason reason)
        {
            switch (reason)
            {
                case SkillDenyReason.NotReady: return "no target";
                case SkillDenyReason.CoolingDown: return "cooling down";
                case SkillDenyReason.NoCharges: return "empty";
                case SkillDenyReason.Busy: return "busy";
                default: return reason.ToString().ToLowerInvariant();
            }
        }

        void LateUpdate()
        {
            if (_skills == null || _hand == null)
                return;

            Transform head = _rig != null ? _rig.Head : null;
            Vector3 up = head != null ? head.up : Vector3.up;
            Vector3 position = _hand.transform.position + up * Offset;
            Quaternion rotation = head != null ? Quaternion.LookRotation(position - head.position, up) : _root.rotation;
            _root.SetPositionAndRotation(position, rotation);
            _root.localScale = Vector3.one * Scale;

            RadialSelector radial = _skills.Radial;
            SkillLoadout loadout = _skills.Loadout;
            bool open = radial.IsOpen;
            _wheel.gameObject.SetActive(open);
            _status.gameObject.SetActive(!open);

            for (int i = 0; i < _sectors.Length; i++)
            {
                Sector sector = _sectors[i];
                bool pointed = open && radial.Current == i;
                bool selected = loadout.Selected == i;
                sector.Root.localScale = Vector3.one * (pointed ? 1.3f : 1f);
                sector.Panel.material.color = pointed ? Kit.Accent : selected ? Color.Lerp(Kit.Surface, Kit.Accent, 0.4f) : Kit.Surface;
                int charges = loadout.Charges(i);
                sector.Charges.text = charges == SkillLoadout.Unlimited ? string.Empty : $"x{charges}";
                int lit = Mathf.RoundToInt((float)loadout.CooldownProgress(i) * Dots);
                for (int d = 0; d < Dots; d++)
                    sector.Dots[d].material.color = d < lit ? Kit.Good : new Color(0.18f, 0.19f, 0.22f);
            }

            _deniedFor = Mathf.Max(0f, _deniedFor - Time.deltaTime);
            if (_deniedFor > 0f)
            {
                _status.text = _deniedText;
                _status.color = Kit.Bad;
            }
            else
            {
                _status.text = StatusOf(loadout);
                _status.color = Kit.Warn;
            }
        }

        string StatusOf(SkillLoadout loadout)
        {
            int slot = loadout.Selected;
            string text = _names[slot];
            string extra = Extra?.Invoke(slot);
            if (loadout.PreparingSlot == slot)
                text += "  aiming";
            else if (loadout.IsRunning(slot))
                text += "  ...";
            else if (loadout.CooldownRemaining(slot) > 0)
                text += $"  {loadout.CooldownRemaining(slot):0.0}s";
            else if (loadout.Charges(slot) == 0)
                text += "  empty";
            else if (loadout.Charges(slot) > 0)
                text += $"  x{loadout.Charges(slot)}";
            return string.IsNullOrEmpty(extra) ? text : $"{text}  {extra}";
        }
    }
}
