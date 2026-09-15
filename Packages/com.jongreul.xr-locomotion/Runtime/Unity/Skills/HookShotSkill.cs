using Jongreul.XrLocomotion.Framework;
using Jongreul.XrLocomotion.Skills;
using UnityEngine;

namespace Jongreul.XrLocomotion
{
    /// <summary>
    /// 훅샷. 트리거를 누르고 있는 동안 손 방향으로 조준선과 표적이 보이고(초록 = 걸 수 있음, 빨강 = 걸 수 없는 면·사거리 밖),
    /// 떼면 훅을 박고 발을 그 지점으로 당긴다(Core <see cref="HookPull"/>). 벽에 막히면 CharacterController가 면을 따라 미끄러지므로
    /// 턱 위를 겨누면 턱 위로 올라선다. 아래에서는 턱 윗면이 보이지 않으므로, 벽면 윗부분(윗면까지 <c>mantleHeight</c> 이내)에 걸면
    /// 그 윗면으로 당긴다(맨틀). <see cref="NoHookLayer"/> 레이어의 면(유리 등)에는 걸리지 않는다.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public sealed class HookShotSkill : MonoBehaviour, ISkill
    {
        public const int NoHookLayer = 29;

        [SerializeField] HookSettings settings = new HookSettings { ArrivalDistance = 0.3f };
        [SerializeField] float cooldown = 1.5f;
        [SerializeField] float mantleHeight = 0.8f;

        PlayerRig _rig;
        RigMotor _motor;
        Hand _hand;
        HookPull _pull;
        bool _aiming;
        bool _hasTarget;
        bool _hookable;
        Vector3 _targetPoint;
        Vector3 _targetNormal;
        Vector3 _hookPoint;
        Transform _reticle;
        Renderer _reticleRenderer;
        Transform _line;
        Renderer _lineRenderer;

        public string Id => "hook";
        public double Cooldown => cooldown;
        public bool LocksOthers => true;
        public HookPull Pull => _pull;
        public bool IsAiming => _aiming;
        public bool HasHookableTarget => _hasTarget && _hookable;
        public Vector3 TargetPoint => _targetPoint;

        /// <summary>손에서 재는 조준 사거리. 당김 사거리(발에서 잰다)보다 짧게 둬 조준되면 반드시 당길 수 있다.</summary>
        public float AimRange => Mathf.Max(1f, settings.MaxRange - 2f);

        void Awake() => _pull = new HookPull(settings);

        public void Configure(PlayerRig rig, RigMotor motor, Hand hand)
        {
            _rig = rig;
            _motor = motor;
            _hand = hand;
            _reticle = Kit.Primitive(PrimitiveType.Sphere, transform, "HookReticle", Vector3.zero, Vector3.one * 0.08f, Kit.Good).transform;
            _reticleRenderer = _reticle.GetComponent<Renderer>();
            _line = Kit.Primitive(PrimitiveType.Cylinder, transform, "HookLine", Vector3.zero, Vector3.one * 0.01f, Kit.Good).transform;
            _lineRenderer = _line.GetComponent<Renderer>();
            ShowVisuals(false);
        }

        public void Prepare() => _aiming = true;

        public bool Execute()
        {
            _aiming = false;
            UpdateAim();
            if (!HasHookableTarget)
            {
                ShowVisuals(false);
                return false;
            }

            // 벽면 윗부분이면 윗면으로(맨틀), 아니면 면에서 몸 반경만큼 띄운 지점으로 발을 당긴다.
            _hookPoint = _targetPoint;
            float radius = _motor.Controller.radius;
            Vector3 anchor = Mathf.Abs(_targetNormal.y) < 0.5f &&
                             TryFindLedgeTop(_targetPoint, _targetNormal, radius + 0.1f, mantleHeight, out Vector3 top)
                ? top + Vector3.up * 0.05f
                : _targetPoint + _targetNormal * (radius + 0.05f);
            if (!_pull.Begin(_motor.transform.position.ToNumerics(), anchor.ToNumerics()))
            {
                ShowVisuals(false);
                return false;
            }

            return true;
        }

        public bool Tick(double deltaTime)
        {
            Vector3 velocity = _pull.Step(_motor.transform.position.ToNumerics(), deltaTime).ToUnity();
            if (_pull.IsPulling)
            {
                _motor.SetOverrideVelocity(velocity);
                return false;
            }

            ShowVisuals(false);
            return true;
        }

        public void Cancel()
        {
            _aiming = false;
            _pull.Cancel();
            ShowVisuals(false);
        }

        void LateUpdate()
        {
            if (_hand == null)
                return;

            if (_aiming)
            {
                UpdateAim();
                Vector3 end = _hasTarget ? _targetPoint : _hand.transform.position + _hand.transform.forward * AimRange;
                Color color = HasHookableTarget ? Kit.Good : Kit.Bad;
                ShowVisuals(true);
                _reticle.gameObject.SetActive(_hasTarget);
                _reticle.position = end;
                _reticleRenderer.material.color = color;
                Stretch(_line, _hand.transform.position, end, 0.006f);
                _lineRenderer.material.color = new Color(color.r, color.g, color.b) * 0.8f;
            }
            else if (_pull.IsPulling)
            {
                ShowVisuals(true);
                _reticle.position = _hookPoint;
                _reticleRenderer.material.color = Kit.Warn;
                Stretch(_line, _hand.transform.position, _hookPoint, 0.012f);
                _lineRenderer.material.color = Kit.Warn;
            }
        }

        void UpdateAim()
        {
            Transform hand = _hand.transform;
            _hasTarget = Physics.Raycast(hand.position, hand.forward, out RaycastHit hit, AimRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (!_hasTarget)
            {
                _hookable = false;
                return;
            }

            _targetPoint = hit.point;
            _targetNormal = hit.normal;
            _hookable = hit.collider.gameObject.layer != NoHookLayer;
        }

        /// <summary>벽면 hit 지점 바로 뒤, height 위에서 아래로 쏴 걸어 올라설 수 있는 윗면을 찾는다.</summary>
        static bool TryFindLedgeTop(Vector3 point, Vector3 normal, float inset, float height, out Vector3 top)
        {
            Vector3 origin = point - normal * inset + Vector3.up * height;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, height + 0.1f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) && hit.normal.y > 0.7f && hit.point.y > point.y)
            {
                top = hit.point;
                return true;
            }

            top = default;
            return false;
        }

        void ShowVisuals(bool visible)
        {
            if (_reticle == null)
                return;
            _reticle.gameObject.SetActive(visible);
            _line.gameObject.SetActive(visible);
        }

        static void Stretch(Transform cylinder, Vector3 from, Vector3 to, float thickness)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            cylinder.position = (from + to) * 0.5f;
            cylinder.rotation = length > 1e-4f ? Quaternion.FromToRotation(Vector3.up, delta) : Quaternion.identity;
            cylinder.localScale = new Vector3(thickness, length * 0.5f, thickness);
        }
    }
}
