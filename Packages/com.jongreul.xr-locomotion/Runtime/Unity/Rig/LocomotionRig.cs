using UnityEngine;

namespace Jongreul.XrLocomotion
{
    /// <summary>
    /// 코드로 조립하는 이동 리그.
    /// 루트(CharacterController · PlayerRig · RigMotor · 데스크톱 시뮬레이터, 레이어 2 = 레이캐스트 무시)
    /// └ TrackingSpace(플레이어 크기 보정이 이 스케일을 바꾼다) └ Head · 왼손 · 오른손.
    /// 몸(CharacterController)은 스케일 1인 루트에 두고 높이·반경을 직접 바꾼다 — 트랜스폼 스케일에 맡기지 않는다.
    /// </summary>
    public sealed class LocomotionRig
    {
        public const int BodyLayer = 2; // Ignore Raycast: 조준·장애물 검사가 자기 몸에 걸리지 않게

        public GameObject Root { get; private set; }
        public Transform TrackingSpace { get; private set; }
        public Transform Head { get; private set; }
        public Camera Camera { get; private set; }
        public Hand LeftHand { get; private set; }
        public Hand RightHand { get; private set; }
        public PlayerRig Rig { get; private set; }
        public RigMotor Motor { get; private set; }
        public DesktopHandSimulator Simulator { get; private set; }

        public Hand GetHand(HandSide side) => side == HandSide.Left ? LeftHand : RightHand;

        public static LocomotionRig Create(Transform parent, string name = "PlayerRig", bool withCamera = true,
            bool useXRDevice = true)
        {
            var root = new GameObject(name) { layer = BodyLayer };
            root.transform.SetParent(parent, false);

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.7f;
            controller.radius = 0.25f;
            controller.center = new Vector3(0f, 0.85f, 0f);
            controller.stepOffset = 0.35f;
            controller.skinWidth = 0.02f;
            controller.minMoveDistance = 0f;

            var simulator = root.AddComponent<DesktopHandSimulator>();
            var tracking = new GameObject("TrackingSpace").transform;
            tracking.SetParent(root.transform, false);

            var head = new GameObject("Head");
            head.transform.SetParent(tracking, false);
            Camera camera = null;
            if (withCamera)
            {
                camera = head.AddComponent<Camera>();
                camera.nearClipPlane = 0.02f;
                camera.fieldOfView = 70f;
            }

            Hand left = CreateHand(tracking, HandSide.Left);
            Hand right = CreateHand(tracking, HandSide.Right);

            var rig = root.AddComponent<PlayerRig>();
            rig.Configure(head.transform, left, right, simulator, useXRDevice);
            var motor = root.AddComponent<RigMotor>();
            motor.Configure(rig);

            return new LocomotionRig
            {
                Root = root,
                TrackingSpace = tracking,
                Head = head.transform,
                Camera = camera,
                LeftHand = left,
                RightHand = right,
                Rig = rig,
                Motor = motor,
                Simulator = simulator,
            };
        }

        static Hand CreateHand(Transform parent, HandSide side)
        {
            var go = new GameObject($"{side}Hand");
            go.transform.SetParent(parent, false);
            var hand = go.AddComponent<Hand>();
            hand.Side = side;
            return hand;
        }
    }
}
