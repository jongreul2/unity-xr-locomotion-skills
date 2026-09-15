using System.Collections.Generic;
using Jongreul.XrLocomotion.Scale;
using UnityEngine;

namespace Jongreul.XrLocomotion
{
    /// <summary>
    /// 플레이어 크기 변경. Core <see cref="ScaleAdapter"/>가 부드럽게 바꾼 크기를 한 번에 적용한다:
    /// 트래킹 공간 스케일(머리 높이·팔 길이·눈 사이 거리가 함께 바뀐다) · 이동 속도 · 몸 캡슐 높이·반경·계단 높이 ·
    /// 손 잡는 반경 · 카메라 근접 클리핑 · 라디얼 메뉴 거리 · 내 몸에서 나는 소리의 감쇠 거리.
    /// </summary>
    public sealed class ScaleController : MonoBehaviour
    {
        [SerializeField] ScaleBaseline baseline = new ScaleBaseline();
        [SerializeField] float minScale = 0.25f;
        [SerializeField] float maxScale = 4f;
        [SerializeField] float transitionSeconds = 0.6f;

        readonly List<AudioSource> _audio = new List<AudioSource>();
        LocomotionRig _rig;
        RadialMenu _menu;
        ScaleAdapter _adapter;

        public ScaleAdapter Adapter => _adapter;
        public float Current => _adapter.Current;

        void Awake()
        {
            _adapter = new ScaleAdapter(baseline, minScale, maxScale, transitionSeconds);
            _adapter.Changed += Apply;
        }

        public void Configure(LocomotionRig rig, RadialMenu menu = null)
        {
            _rig = rig;
            _menu = menu;
            Apply(_adapter.Values);
        }

        public void SetTarget(float scale) => _adapter.SetTarget(scale);

        /// <summary>내 몸에서 나는 소리(발소리·스킬 소리 등). 감쇠 거리를 크기에 맞춘다.</summary>
        public void RegisterAudio(AudioSource source)
        {
            if (source == null || _audio.Contains(source))
                return;
            _audio.Add(source);
            Apply(_adapter.Values);
        }

        void Update() => _adapter.Step(Time.deltaTime);

        void Apply(ScaledValues values)
        {
            if (_rig == null)
                return;

            _rig.TrackingSpace.localScale = Vector3.one * values.Scale;
            _rig.Motor.MoveSpeed = values.MoveSpeed;
            _rig.Motor.SpeedScale = values.Scale;
            _rig.Motor.ApplyBody(values.BodyHeight, values.BodyRadius, values.StepHeight);
            _rig.LeftHand.GrabRadius = values.GrabRadius;
            _rig.RightHand.GrabRadius = values.GrabRadius;
            if (_rig.Camera != null)
                _rig.Camera.nearClipPlane = values.NearClip;
            if (_menu != null)
            {
                _menu.Offset = values.UiDistance * 0.25f;
                _menu.Scale = values.Scale;
            }

            foreach (AudioSource source in _audio)
            {
                if (source == null)
                    continue;
                source.minDistance = values.AudioMinDistance;
                source.maxDistance = values.AudioMaxDistance;
            }
        }
    }

    /// <summary>밟으면 플레이어 크기를 바꾸는 발판(데모용). 들어서는 순간 한 번.</summary>
    public sealed class ScalePad : MonoBehaviour
    {
        ScaleController _controller;
        Transform _body;
        Vector2 _size;
        bool _inside;

        public float TargetScale { get; private set; }

        public static ScalePad Create(Transform parent, string name, Vector3 localPosition, Vector2 size, float scale,
            Color color, ScaleController controller, Transform body, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            Kit.Primitive(PrimitiveType.Cube, go.transform, "Plate", new Vector3(0f, 0.02f, 0f), new Vector3(size.x, 0.04f, size.y),
                color, collider: true);
            Kit.Label(go.transform, "Label", new Vector3(0f, 0.9f, 0f), new Vector2(1.2f, 0.12f), 90).text = label;

            var pad = go.AddComponent<ScalePad>();
            pad._controller = controller;
            pad._body = body;
            pad._size = size;
            pad.TargetScale = scale;
            return pad;
        }

        void Update()
        {
            if (_body == null || _controller == null)
                return;

            Vector3 local = transform.InverseTransformPoint(_body.position);
            bool inside = Mathf.Abs(local.x) <= _size.x * 0.5f && Mathf.Abs(local.z) <= _size.y * 0.5f &&
                          local.y > -0.3f && local.y < 0.6f;
            if (inside && !_inside)
                _controller.SetTarget(TargetScale);
            _inside = inside;
        }
    }
}
