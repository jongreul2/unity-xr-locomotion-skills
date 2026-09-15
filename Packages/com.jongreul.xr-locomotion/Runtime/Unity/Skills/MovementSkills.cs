using System.Collections.Generic;
using Jongreul.XrLocomotion.Framework;
using Jongreul.XrLocomotion.Skills;
using UnityEngine;

namespace Jongreul.XrLocomotion
{
    /// <summary>
    /// 부스터. 실행하면 시선 수평 방향으로 가속(Core <see cref="Booster"/>)해 모터의 더하는 속도로 넣는다.
    /// 스킬이 끝나도 남은 속도가 반감기로 줄어드는 동안 계속 넣어야 하므로 매 프레임 스스로 돈다.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public sealed class BoosterSkill : MonoBehaviour, ISkill
    {
        [SerializeField] BoosterSettings settings = new BoosterSettings();
        [SerializeField] float cooldown = 2f;

        PlayerRig _rig;
        RigMotor _motor;
        Booster _booster;

        public string Id => "boost";
        public double Cooldown => cooldown;
        public bool LocksOthers => true;
        public Booster Booster => _booster;

        void Awake() => _booster = new Booster(settings);

        public void Configure(PlayerRig rig, RigMotor motor)
        {
            _rig = rig;
            _motor = motor;
        }

        public void Prepare()
        {
        }

        public bool Execute() => _booster.Start();

        public bool Tick(double deltaTime) => !_booster.IsBoosting;

        public void Cancel() => _booster.Stop();

        void Update()
        {
            if (_motor == null)
                return;
            Vector3 forward = _rig != null && _rig.Head != null ? _rig.Head.forward : transform.forward;
            _motor.AdditiveVelocity = _booster.Step(Time.deltaTime, forward.ToNumerics(), _motor.IsGrounded).ToUnity();
        }
    }

    /// <summary>
    /// 백스텝. 시선 반대 방향으로 몸 크기의 캡슐을 쏴 뒤쪽 장애물 거리를 재고, 그 앞까지만 짧게 대시한다(Core <see cref="BackStep"/>).
    /// 대시와 무적 시간이 모두 끝나면 스킬이 끝난다.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public sealed class BackStepSkill : MonoBehaviour, ISkill
    {
        [SerializeField] BackStepSettings settings = new BackStepSettings();
        [SerializeField] float cooldown = 1f;

        PlayerRig _rig;
        RigMotor _motor;
        BackStep _step;

        public string Id => "back";
        public double Cooldown => cooldown;
        public bool LocksOthers => true;
        public BackStep Dash => _step;
        public bool IsInvincible => _step.IsInvincible;

        /// <summary>마지막 실행 때 잰 뒤쪽 장애물 거리(없으면 +∞).</summary>
        public float LastObstacleDistance { get; private set; } = float.PositiveInfinity;

        void Awake() => _step = new BackStep(settings);

        public void Configure(PlayerRig rig, RigMotor motor)
        {
            _rig = rig;
            _motor = motor;
        }

        public void Prepare()
        {
        }

        public bool Execute()
        {
            Vector3 look = _rig != null && _rig.Head != null ? _rig.Head.forward : transform.forward;
            Vector3 body = _motor.transform.forward;
            Vector3 direction = BackStep.DirectionFrom(look.ToNumerics(), body.ToNumerics()).ToUnity();
            if (direction == Vector3.zero)
                return false;

            CharacterController controller = _motor.Controller;
            float radius = controller.radius * 0.95f;
            Vector3 feet = _motor.transform.position;
            Vector3 bottom = feet + Vector3.up * (radius + controller.stepOffset); // 발밑 턱은 넘어간다
            Vector3 top = feet + Vector3.up * Mathf.Max(radius + controller.stepOffset, controller.height - radius);
            float reach = settings.Distance + settings.WallMargin + 0.1f;
            LastObstacleDistance = Physics.CapsuleCast(bottom, top, radius, direction, out RaycastHit hit, reach,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.distance
                : float.PositiveInfinity;

            return _step.Begin(look.ToNumerics(), body.ToNumerics(), LastObstacleDistance);
        }

        public bool Tick(double deltaTime)
        {
            _motor.MoveBy(_step.Step(deltaTime).ToUnity());
            return !_step.IsDashing && !_step.IsInvincible;
        }

        public void Cancel() => _step.Cancel();
    }

    /// <summary>
    /// 큐브 발판. 실행하면 손 앞(시선 수평 방향)에 윗면이 발 높이와 같은 발판을 만든다 — 구덩이를 건너는 다리.
    /// 개수·수명·밟으면 연장은 Core <see cref="CubePlatforms"/>. 곧 사라질 발판은 깜빡인다.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public sealed class MakeCubeSkill : MonoBehaviour, ISkill
    {
        public static readonly Vector3 CubeSize = new Vector3(0.8f, 0.3f, 0.8f);
        static readonly Color CubeColor = new Color(0.3f, 0.8f, 0.75f);

        [SerializeField] CubeSettings settings = new CubeSettings();
        [SerializeField] float cooldown = 0.4f;
        [SerializeField] float aheadOfHand = 0.45f;

        readonly Dictionary<int, GameObject> _objects = new Dictionary<int, GameObject>();
        PlayerRig _rig;
        RigMotor _motor;
        Hand _hand;
        Transform _world;
        CubePlatforms _cubes;

        public string Id => "cube";
        public double Cooldown => cooldown;
        public bool LocksOthers => false;
        public CubePlatforms Cubes => _cubes;
        public IReadOnlyDictionary<int, GameObject> Objects => _objects;
        public int LastSpawnedId { get; private set; }

        void Awake()
        {
            _cubes = new CubePlatforms(new GameClock(), settings);
            _cubes.Removed += OnRemoved;
        }

        /// <param name="world">발판을 둘 부모(리그를 따라다니지 않게 리그 밖).</param>
        public void Configure(PlayerRig rig, RigMotor motor, Hand hand, Transform world)
        {
            _rig = rig;
            _motor = motor;
            _hand = hand;
            _world = world;
        }

        public void Prepare()
        {
        }

        public bool Execute()
        {
            Vector3 look = _rig != null && _rig.Head != null ? _rig.Head.forward : transform.forward;
            Vector3 forward = Vector3.ProjectOnPlane(look, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
                forward = _motor.transform.forward;
            forward.Normalize();

            Vector3 hand = _hand.transform.position;
            Vector3 center = new Vector3(hand.x, _motor.transform.position.y - CubeSize.y * 0.5f, hand.z) + forward * aheadOfHand;

            int id = _cubes.Spawn();
            GameObject cube = Kit.Primitive(PrimitiveType.Cube, _world, $"Cube{id}", Vector3.zero, CubeSize, CubeColor, collider: true);
            cube.transform.SetPositionAndRotation(center, Quaternion.LookRotation(forward, Vector3.up));
            _objects[id] = cube;
            LastSpawnedId = id;
            Physics.SyncTransforms();
            return true;
        }

        public bool Tick(double deltaTime) => true;

        public void Cancel()
        {
        }

        public bool TryGetId(Collider collider, out int id)
        {
            foreach (KeyValuePair<int, GameObject> pair in _objects)
            {
                if (collider != null && pair.Value == collider.gameObject)
                {
                    id = pair.Key;
                    return true;
                }
            }

            id = 0;
            return false;
        }

        void Update()
        {
            if (_motor != null && TryGetId(_motor.Ground, out int standing))
                _cubes.Stand(standing);

            _cubes.Update();

            foreach (KeyValuePair<int, GameObject> pair in _objects)
            {
                double remaining = _cubes.Remaining(pair.Key);
                bool blink = remaining < 1.5 && Mathf.Repeat(Time.time * 6f, 1f) < 0.5f;
                Kit.SetColor(pair.Value, blink ? Color.Lerp(CubeColor, Color.white, 0.6f) : CubeColor);
            }
        }

        void OnRemoved(int id, CubeRemoveReason reason)
        {
            if (_objects.TryGetValue(id, out GameObject cube))
            {
                Destroy(cube);
                _objects.Remove(id);
            }
        }

        void OnDestroy()
        {
            foreach (GameObject cube in _objects.Values)
            {
                if (cube != null)
                    Destroy(cube);
            }

            _objects.Clear();
        }
    }
}
