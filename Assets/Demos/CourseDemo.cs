using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Jongreul.XrLocomotion.Demos
{
    /// <summary>
    /// 이동 스킬 코스 데모 부트스트랩. 씬에는 이 컴포넌트 하나만 두고 환경·리그·구역을 코드로 만든다.
    /// 구역은 X축으로 나란히(1 훅샷 · 2 부스터 · 3 백스텝 · 4 큐브 발판 · 5 크기) — 숫자 키로 그 출발점에 순간이동한다.
    /// 구덩이에 빠지면 지금 구역 출발점으로 돌아온다.
    /// </summary>
    public sealed class CourseDemo : MonoBehaviour
    {
        public static readonly Color Backdrop = new Color(0.07f, 0.08f, 0.1f);
        public const int AvatarLayer = 30;

        public const float HookX = 0f;
        public const float BoostX = 6f;
        public const float BackX = 12f;
        public const float CubeX = 18f;
        public const float ScaleX = 24f;

        static readonly float[] Spots = { HookX, BoostX, BackX, CubeX, ScaleX };
        static readonly Key[] SpotKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
        static readonly Color FloorColor = new Color(0.15f, 0.16f, 0.19f);
        static readonly Color Stone = new Color(0.36f, 0.3f, 0.26f);

        int _spot;

        public SkillRig Player { get; private set; }
        public Camera HeadCamera => Player.Body.Camera;
        public DesktopHandSimulator Simulator => Player.Body.Simulator;
        public Transform Ledge { get; private set; }
        public Transform Glass { get; private set; }
        public ScalePad GrowPad { get; private set; }
        public ScalePad ShrinkPad { get; private set; }
        public ScalePad ResetPad { get; private set; }
        public int CurrentSpot => _spot;

        public static Vector3 SpotPosition(int spot) => new Vector3(Spots[Mathf.Clamp(spot, 0, Spots.Length - 1)], 0f, 0f);

        void Awake()
        {
            BuildLight();
            Player = SkillRig.Build(transform);
            SetUpBody();
            BuildHookSection();
            BuildBoostSection();
            BuildBackSection();
            BuildCubeSection();
            BuildScaleSection();
            BuildHelp();
            TeleportTo(0);
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && Simulator.KeyboardAndMouse)
            {
                for (int i = 0; i < SpotKeys.Length; i++)
                {
                    if (keyboard[SpotKeys[i]].wasPressedThisFrame)
                    {
                        TeleportTo(i);
                        break;
                    }
                }
            }

            if (Player.Body.Root.transform.position.y < -8f)
                TeleportTo(_spot);
        }

        /// <summary>구역 출발점으로. 쓰던 스킬은 끊는다.</summary>
        public void TeleportTo(int spot)
        {
            _spot = Mathf.Clamp(spot, 0, Spots.Length - 1);
            Player.Skills.Loadout.CancelAll();
            Player.Body.Motor.Teleport(SpotPosition(_spot));
        }

        #region 몸

        void SetUpBody()
        {
            LocomotionRig body = Player.Body;
            body.Head.gameObject.tag = "MainCamera";
            HeadCamera.clearFlags = CameraClearFlags.SolidColor;
            HeadCamera.backgroundColor = Backdrop;
            HeadCamera.cullingMask &= ~(1 << AvatarLayer);

            AddPalm(body.LeftHand, new Color(0.45f, 0.62f, 1f));
            AddPalm(body.RightHand, new Color(1f, 0.62f, 0.45f));

            // 관찰자 카메라용 아바타(1인칭에서는 레이어로 숨긴다). 트래킹 공간 아래라 크기 보정을 같이 받는다.
            var skin = new Color(0.62f, 0.66f, 0.74f);
            GameObject head = Kit.Primitive(PrimitiveType.Sphere, body.Head, "AvatarHead", Vector3.zero, Vector3.one * 0.2f, skin);
            GameObject torso = Kit.Primitive(PrimitiveType.Capsule, body.TrackingSpace, "AvatarBody", new Vector3(0f, 1f, 0f),
                new Vector3(0.34f, 0.45f, 0.22f), skin);
            head.layer = AvatarLayer;
            torso.layer = AvatarLayer;
            body.Root.AddComponent<AvatarBody>().Bind(body.Head, torso.transform);
        }

        void AddPalm(Hand hand, Color color)
        {
            GameObject palm = Kit.Primitive(PrimitiveType.Sphere, hand.transform, "Palm", Vector3.zero, Vector3.one * 0.07f, color);
            Kit.Primitive(PrimitiveType.Cube, hand.transform, "Pointer", new Vector3(0f, 0f, 0.05f), new Vector3(0.015f, 0.015f, 0.05f), color);
            hand.gameObject.AddComponent<HandVisual>().Bind(hand, palm.GetComponent<Renderer>(), color, Player);
        }

        #endregion

        #region 구역

        GameObject Floor(string floorName, Vector3 center, Vector3 size) =>
            Kit.Primitive(PrimitiveType.Cube, transform, floorName, center, size, FloorColor, collider: true);

        void Sign(Vector3 position, string title, string body)
        {
            Kit.Label(transform, "Title", position, new Vector2(3.2f, 0.26f), 200).text = $"<b>{title}</b>";
            Kit.Label(transform, "Body", position + Vector3.down * 0.3f, new Vector2(3.6f, 0.32f), 90, TextAnchor.UpperCenter, Kit.Muted)
                .text = body;
        }

        void Flag(Vector3 foot, Color color)
        {
            Kit.Primitive(PrimitiveType.Cylinder, transform, "FlagPole", foot + Vector3.up * 0.6f, new Vector3(0.04f, 0.6f, 0.04f), Kit.Muted);
            Kit.Primitive(PrimitiveType.Cube, transform, "Flag", foot + new Vector3(0.2f, 1.05f, 0f), new Vector3(0.4f, 0.26f, 0.02f), color);
        }

        void BuildHookSection()
        {
            float x = HookX;
            Floor("HookFloor", new Vector3(x, -0.25f, 5f), new Vector3(5f, 0.5f, 16f));
            Ledge = Kit.Primitive(PrimitiveType.Cube, transform, "Ledge", new Vector3(x - 0.4f, 1.25f, 7f), new Vector3(3f, 2.5f, 2f),
                Stone, collider: true).transform; // 앞면 z 6, 윗면 y 2.5
            Flag(new Vector3(x - 0.4f, 2.5f, 7.3f), Kit.Good);
            GameObject glass = Kit.Primitive(PrimitiveType.Cube, transform, "Glass", new Vector3(x + 1.9f, 1.5f, 5f),
                new Vector3(1.3f, 3f, 0.08f), new Color(0.55f, 0.78f, 1f), collider: true);
            glass.layer = HookShotSkill.NoHookLayer;
            Glass = glass.transform;
            Kit.Label(transform, "GlassLabel", new Vector3(x + 1.9f, 3.25f, 4.95f), new Vector2(1.4f, 0.14f), 90, TextAnchor.MiddleCenter,
                new Color(0.55f, 0.78f, 1f)).text = "GLASS · NO HOOK";
            Sign(new Vector3(x - 0.4f, 4.4f, 8.1f), "1  HOOKSHOT",
                "aim near the top of the wall, release to climb · glass can't be hooked");
        }

        void BuildBoostSection()
        {
            float x = BoostX;
            Floor("BoostFloor", new Vector3(x, -0.25f, 11f), new Vector3(3f, 0.5f, 28f));
            for (int meters = 2; meters <= 22; meters += 2)
            {
                Kit.Primitive(PrimitiveType.Cube, transform, $"Tick{meters}", new Vector3(x, 0.006f, meters), new Vector3(2.6f, 0.004f, 0.05f),
                    new Color(0.3f, 0.33f, 0.38f));
                if (meters % 4 == 0)
                    Kit.Label(transform, $"Meters{meters}", new Vector3(x - 1.2f, 0.25f, meters), new Vector2(0.6f, 0.2f), 150,
                        TextAnchor.MiddleCenter, Kit.Muted).text = $"{meters} m";
            }

            Sign(new Vector3(x, 3.2f, 7f), "2  BOOSTER", "burst forward along your gaze · weaker in the air · fuel refills");
        }

        void BuildBackSection()
        {
            float x = BackX;
            Floor("BackFloor", new Vector3(x, -0.25f, 3f), new Vector3(4f, 0.5f, 9f));
            Kit.Primitive(PrimitiveType.Cube, transform, "BackWall", new Vector3(x, 1.25f, -1.5f), new Vector3(4f, 2.5f, 0.2f), Stone,
                collider: true); // 앞면 z -1.4
            Sign(new Vector3(x, 3.2f, 5f), "3  BACKSTEP", "dash away from your gaze · stops before the wall · short invincibility");
        }

        void BuildCubeSection()
        {
            float x = CubeX;
            Floor("CubeFloorNear", new Vector3(x, -0.25f, -1f), new Vector3(3f, 0.5f, 4f)); // z -3 ~ 1
            Floor("CubeFloorFar", new Vector3(x, -0.25f, 7.05f), new Vector3(3f, 0.5f, 7.9f)); // z 3.1 ~ 11
            Kit.Primitive(PrimitiveType.Cube, transform, "PitEdge", new Vector3(x, -0.02f, 1f), new Vector3(3f, 0.04f, 0.06f), Kit.Warn);
            Flag(new Vector3(x, 0f, 6f), Kit.Good);
            Sign(new Vector3(x, 3.2f, 8f), "4  CUBE TILES", "make tiles at your feet over the pit and walk across · standing keeps a tile");
        }

        void BuildScaleSection()
        {
            float x = ScaleX;
            ScaleController scale = Player.Scale;
            Transform body = Player.Body.Root.transform;
            Floor("ScaleFloor", new Vector3(x, -0.25f, 4f), new Vector3(7f, 0.5f, 12f));
            ShrinkPad = ScalePad.Create(transform, "ShrinkPad", new Vector3(x - 1.7f, 0f, 1.8f), new Vector2(1.1f, 1.1f), 0.5f, Kit.Warn,
                scale, body, "x0.5");
            ResetPad = ScalePad.Create(transform, "ResetPad", new Vector3(x, 0f, 1.8f), new Vector2(1.1f, 1.1f), 1f, Kit.Muted,
                scale, body, "x1");
            GrowPad = ScalePad.Create(transform, "GrowPad", new Vector3(x + 1.7f, 0f, 1.8f), new Vector2(1.1f, 1.1f), 2f, Kit.Good,
                scale, body, "x2");

            // 작아져야 지나가는 낮은 굴(천장 높이 1 m)
            float tx = x - 1.7f;
            Kit.Primitive(PrimitiveType.Cube, transform, "TunnelLeft", new Vector3(tx - 0.65f, 0.5f, 7f), new Vector3(0.2f, 1f, 2.4f), Stone, collider: true);
            Kit.Primitive(PrimitiveType.Cube, transform, "TunnelRight", new Vector3(tx + 0.65f, 0.5f, 7f), new Vector3(0.2f, 1f, 2.4f), Stone, collider: true);
            Kit.Primitive(PrimitiveType.Cube, transform, "TunnelRoof", new Vector3(tx, 1.1f, 7f), new Vector3(1.5f, 0.2f, 2.4f), Stone, collider: true);

            // 눈높이가 바뀌는 것을 보여 주는 높이 자
            Kit.Primitive(PrimitiveType.Cylinder, transform, "Ruler", new Vector3(x + 2.9f, 2f, 4f), new Vector3(0.06f, 2f, 0.06f), Kit.Muted);
            for (int meters = 1; meters <= 4; meters++)
            {
                Kit.Primitive(PrimitiveType.Cube, transform, $"Mark{meters}", new Vector3(x + 2.9f, meters, 4f), new Vector3(0.3f, 0.03f, 0.03f), Kit.Warn);
                Kit.Label(transform, $"MarkLabel{meters}", new Vector3(x + 2.45f, meters, 3.98f), new Vector2(0.5f, 0.2f), 140,
                    TextAnchor.MiddleCenter, Kit.Warn).text = $"{meters} m";
            }

            Sign(new Vector3(x, 3.6f, 9.5f), "5  PLAYER SCALE",
                "step on a pad: x2 · x0.5 · x1 — speed, reach, body, near clip and menu follow");
        }

        void BuildLight()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        }

        void BuildHelp()
        {
            var canvasGo = new GameObject("Help", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = HeadCamera;
            canvas.planeDistance = 0.3f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)textGo.transform;
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.offsetMin = new Vector2(20, 12);
            rect.offsetMax = new Vector2(-20, 44);
            var text = textGo.AddComponent<Text>();
            text.font = Kit.Font;
            text.fontSize = 16;
            text.color = Kit.Muted;
            text.alignment = TextAnchor.LowerLeft;
            text.raycastTarget = false;
            text.text = "1-5: section   WASD: move   IJKL: skill wheel (release to pick)   RMB hold/release: aim/use   Mouse: hand   R+Mouse: rotate hand   Arrows: look";
        }

        #endregion
    }

    /// <summary>손 표시: 트리거를 누르고 있으면 노랑, 백스텝 무적 동안 흰색으로 깜빡.</summary>
    public sealed class HandVisual : MonoBehaviour
    {
        Hand _hand;
        Renderer _renderer;
        Color _baseColor;
        SkillRig _player;

        public void Bind(Hand hand, Renderer palm, Color baseColor, SkillRig player)
        {
            _hand = hand;
            _renderer = palm;
            _baseColor = baseColor;
            _player = player;
        }

        void LateUpdate()
        {
            if (_hand == null || _renderer == null)
                return;

            bool invincible = _player != null && _player.Back.IsInvincible;
            Color color = invincible && Mathf.Repeat(Time.time * 12f, 1f) < 0.5f ? Color.white
                : _hand.Trigger > 0.5f ? Kit.Warn
                : _baseColor;
            _renderer.material.color = color;
        }
    }

    /// <summary>아바타 몸통을 머리 아래에 두고 머리의 좌우 방향만 따라 돌린다(트래킹 공간 좌표).</summary>
    public sealed class AvatarBody : MonoBehaviour
    {
        Transform _head;
        Transform _body;

        public void Bind(Transform head, Transform body)
        {
            _head = head;
            _body = body;
        }

        void LateUpdate()
        {
            if (_head == null || _body == null)
                return;

            Vector3 forward = Vector3.ProjectOnPlane(_head.localRotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
                forward = Vector3.forward;
            _body.localPosition = _head.localPosition + Vector3.down * 0.62f;
            _body.localRotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
