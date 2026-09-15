using UnityEngine;

namespace Jongreul.XrLocomotion
{
    /// <summary>
    /// 리그를 옮기는 모터. CharacterController로 벽·바닥과 부딪히고, 왼손 스틱으로 시선 기준 이동, 중력을 받는다.
    /// 스킬은 세 가지 방법으로 몸을 움직인다: 덮어쓰는 속도(훅샷 — 이번 프레임만, 중력 끔) · 더하는 속도(부스터) ·
    /// 한 번에 옮기는 변위(백스텝). 스킬이 먼저 돌고(실행 순서 -75·-70) 모터가 그 결과로 한 번 움직인다(-50).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-50)]
    public sealed class RigMotor : MonoBehaviour
    {
        [SerializeField] PlayerRig rig;
        [SerializeField] float moveSpeed = 2.5f;
        [SerializeField] float gravity = 9.81f;
        [SerializeField, Range(0f, 0.9f)] float stickDeadzone = 0.15f;
        [SerializeField] bool stickMovement = true;

        CharacterController _controller;
        float _verticalSpeed;
        bool _hasOverride;
        Vector3 _override;
        Vector3 _pendingDisplacement;
        Collider _groundCandidate;

        public CharacterController Controller =>
            _controller != null ? _controller : (_controller = GetComponent<CharacterController>());

        public bool IsGrounded { get; private set; }

        /// <summary>밟고 있는 콜라이더(공중이면 null).</summary>
        public Collider Ground { get; private set; }

        /// <summary>지난 프레임 실제로 움직인 속도.</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>걷기에 더해지는 속도(부스터). <see cref="SpeedScale"/>를 곱해 쓴다.</summary>
        public Vector3 AdditiveVelocity { get; set; }

        /// <summary>플레이어 크기 배율 — 더하는 속도도 크기에 비례하게.</summary>
        public float SpeedScale { get; set; } = 1f;

        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = value;
        }

        public bool StickMovement
        {
            get => stickMovement;
            set => stickMovement = value;
        }

        public void Configure(PlayerRig playerRig) => rig = playerRig;

        /// <summary>이번 프레임의 속도를 덮어쓴다(중력 없음). 계속 쓰려면 매 프레임 부른다.</summary>
        public void SetOverrideVelocity(Vector3 velocity)
        {
            _hasOverride = true;
            _override = velocity;
        }

        /// <summary>다음 이동에 변위를 더한다(충돌은 CharacterController가 처리).</summary>
        public void MoveBy(Vector3 displacement) => _pendingDisplacement += displacement;

        public void ApplyBody(float height, float radius, float stepOffset)
        {
            CharacterController controller = Controller;
            controller.height = height;
            controller.radius = radius;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
            controller.stepOffset = Mathf.Min(stepOffset, height * 0.5f);
        }

        /// <summary>충돌 없이 옮긴다(CharacterController를 잠깐 끈다 — 그냥 옮기면 물리 동기화 전 위치로 되돌아간다).</summary>
        public void Teleport(Vector3 position)
        {
            CharacterController controller = Controller;
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.position = position;
            controller.enabled = wasEnabled;
            _verticalSpeed = 0f;
            _pendingDisplacement = Vector3.zero;
            Velocity = Vector3.zero;
            IsGrounded = false;
            Ground = null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            Vector3 motion;
            if (_hasOverride)
            {
                motion = _override * dt;
                _verticalSpeed = 0f;
            }
            else
            {
                if (IsGrounded && _verticalSpeed < 0f)
                    _verticalSpeed = -1f; // 바닥에 붙어 있게 살짝 누른다
                _verticalSpeed -= gravity * dt;
                motion = (StickVelocity() + AdditiveVelocity * SpeedScale) * dt + Vector3.up * (_verticalSpeed * dt);
            }

            motion += _pendingDisplacement;
            _pendingDisplacement = Vector3.zero;
            _hasOverride = false;

            Vector3 before = transform.position;
            _groundCandidate = null;
            CollisionFlags flags = Controller.Move(motion);
            IsGrounded = (flags & CollisionFlags.Below) != 0;
            Ground = IsGrounded ? _groundCandidate : null;
            if ((flags & CollisionFlags.Above) != 0 && _verticalSpeed > 0f)
                _verticalSpeed = 0f;
            Velocity = (transform.position - before) / dt;
        }

        Vector3 StickVelocity()
        {
            if (!stickMovement || rig == null || rig.ActiveSource == null ||
                !rig.ActiveSource.TryGetHand(HandSide.Left, out HandInputFrame frame))
                return Vector3.zero;

            float magnitude = Mathf.Min(1f, frame.Stick.magnitude);
            if (magnitude < stickDeadzone)
                return Vector3.zero;

            Transform view = rig.Head != null ? rig.Head : transform;
            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
                forward = transform.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 direction = (forward * frame.Stick.y + right * frame.Stick.x).normalized;
            return direction * (moveSpeed * magnitude);
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.5f)
                _groundCandidate = hit.collider;
        }
    }
}
