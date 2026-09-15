using System;
using System.Collections;
using Jongreul.XrLocomotion.Framework;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jongreul.XrLocomotion.Tests
{
    /// <summary>시험장: 바닥 + 스킬 리그(시뮬레이터 입력). 전역 captureFramerate는 Dispose에서 되돌린다.</summary>
    sealed class SkillWorld
    {
        readonly int _previousCaptureFramerate;

        public SkillWorld(bool floor = true)
        {
            _previousCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = 60;
            Root = new GameObject("World");
            if (floor)
                Box("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f));
            Rig = SkillRig.Build(Root.transform, withCamera: true, useXRDevice: false);
            Sim.KeyboardAndMouse = false;
            Sim.SetHead(new Vector3(0f, 1.6f, 0f), 0f, 0f);
            Sim.SetHand(HandSide.Left, new Vector3(-0.2f, 1.1f, 0.3f), Quaternion.identity);
            Sim.SetHand(HandSide.Right, new Vector3(0.2f, 1.1f, 0.35f), Quaternion.identity);
        }

        public GameObject Root { get; }
        public SkillRig Rig { get; }
        public DesktopHandSimulator Sim => Rig.Body.Simulator;
        public RigMotor Motor => Rig.Body.Motor;
        public SkillLoadout Loadout => Rig.Skills.Loadout;
        public Vector3 Feet => Rig.Body.Root.transform.position;

        public GameObject Box(string name, Vector3 center, Vector3 size, int layer = 0)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.layer = layer;
            box.transform.SetParent(Root.transform, false);
            box.transform.position = center;
            box.transform.localScale = size;
            Physics.SyncTransforms();
            return box;
        }

        public static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        /// <summary>오른손 스틱으로 슬롯을 고르고 트리거를 눌렀다 뗀다.</summary>
        public IEnumerator Use(int slot, int aimFrames = 2)
        {
            Sim.SetStick(HandSide.Right, SkillRig.StickFor(slot));
            yield return Frames(2);
            Sim.SetStick(HandSide.Right, Vector2.zero);
            yield return Frames(2);
            Sim.SetTrigger(HandSide.Right, 1f);
            yield return Frames(aimFrames);
            Sim.SetTrigger(HandSide.Right, 0f);
            yield return Frames(2); // 코루틴은 LateUpdate 전에 돌아온다 — 메뉴 표시까지 한 프레임 더
        }

        public IEnumerator WaitWhileRunning(int slot, int maxFrames = 600)
        {
            for (int i = 0; i < maxFrames && Loadout.IsRunning(slot); i++)
                yield return null;
            Assert.That(Loadout.IsRunning(slot), Is.False, "스킬이 끝나지 않음");
        }

        /// <summary>오른손을 world 지점을 향하게 돌린다(손 위치는 그대로).</summary>
        public void AimRightHandAt(Vector3 world)
        {
            Vector3 local = Sim.GetHandPosition(HandSide.Right);
            Vector3 handWorld = Rig.Body.TrackingSpace.TransformPoint(local);
            Vector3 direction = Rig.Body.TrackingSpace.InverseTransformDirection(world - handWorld);
            Sim.SetHand(HandSide.Right, local, Quaternion.LookRotation(direction, Vector3.up));
        }

        public void Dispose()
        {
            Object.Destroy(Root);
            Time.captureFramerate = _previousCaptureFramerate;
        }
    }

    public class MotorAndRadialPlayModeTests
    {
        SkillWorld _world;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = new SkillWorld();
            yield return SkillWorld.Frames(5);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _world.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LeftStick_WalksWhereTheHeadLooks_OnTheFloor()
        {
            _world.Sim.SetHead(new Vector3(0f, 1.6f, 0f), 90f, 10f); // +X를 본다
            _world.Sim.SetStick(HandSide.Left, new Vector2(0f, 1f));

            yield return SkillWorld.Frames(30);

            Assert.That(_world.Feet.x, Is.EqualTo(_world.Motor.MoveSpeed * 0.5f).Within(0.15f));
            Assert.That(Mathf.Abs(_world.Feet.z), Is.LessThan(0.05f));
            Assert.That(_world.Feet.y, Is.EqualTo(0f).Within(0.05f));
            Assert.That(_world.Motor.IsGrounded, Is.True);
        }

        [UnityTest]
        public IEnumerator Gravity_BringsTheRigDownToTheFloor()
        {
            _world.Motor.Teleport(new Vector3(0f, 3f, 0f));

            yield return SkillWorld.Frames(90);

            Assert.That(_world.Feet.y, Is.EqualTo(0f).Within(0.05f));
            Assert.That(_world.Motor.IsGrounded, Is.True);
        }

        [UnityTest]
        public IEnumerator RightStickFlick_OpensTheMenu_AndReleaseSelectsTheSlot()
        {
            _world.Sim.SetStick(HandSide.Right, SkillRig.StickFor(SkillRig.BoostSlot));
            yield return SkillWorld.Frames(2);
            Assert.That(_world.Rig.Menu.IsOpen, Is.True);
            Assert.That(_world.Rig.Skills.Radial.Current, Is.EqualTo(SkillRig.BoostSlot));

            _world.Sim.SetStick(HandSide.Right, Vector2.zero);
            yield return SkillWorld.Frames(2);

            Assert.That(_world.Loadout.Selected, Is.EqualTo(SkillRig.BoostSlot));
            Assert.That(_world.Rig.Menu.IsOpen, Is.False);
            StringAssert.StartsWith("BOOST", _world.Rig.Menu.StatusText);
        }
    }

    public class SkillsPlayModeTests
    {
        SkillWorld _world;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _world.Dispose();
            yield return null;
        }

        IEnumerator Begin(bool floor = true)
        {
            _world = new SkillWorld(floor);
            yield return SkillWorld.Frames(5);
        }

        [UnityTest]
        public IEnumerator Hook_AimedNearALedgeTop_MantlesOntoIt()
        {
            yield return Begin();
            _world.Box("Ledge", new Vector3(0f, 1f, 6f), new Vector3(4f, 2f, 2f)); // 윗면 y 2, 앞면 z 5
            _world.AimRightHandAt(new Vector3(0f, 1.85f, 5f)); // 손이 턱보다 낮아 윗면은 안 보인다 — 벽면 윗부분을 겨눈다

            _world.Sim.SetStick(HandSide.Right, SkillRig.StickFor(SkillRig.HookSlot));
            yield return SkillWorld.Frames(2);
            _world.Sim.SetStick(HandSide.Right, Vector2.zero);
            yield return SkillWorld.Frames(2);
            _world.Sim.SetTrigger(HandSide.Right, 1f);
            yield return SkillWorld.Frames(3);
            Assert.That(_world.Rig.Hook.HasHookableTarget, Is.True, "조준 중 표적이 잡혀야 한다");
            _world.Sim.SetTrigger(HandSide.Right, 0f);
            yield return SkillWorld.Frames(1);
            Assert.That(_world.Loadout.IsRunning(SkillRig.HookSlot), Is.True);

            yield return _world.WaitWhileRunning(SkillRig.HookSlot);
            yield return SkillWorld.Frames(40);

            Assert.That(_world.Rig.Hook.Pull.State, Is.EqualTo(Skills.HookState.Arrived));
            Assert.That(_world.Feet.y, Is.EqualTo(2f).Within(0.08f), "턱 위에 섰다");
            Assert.That(_world.Feet.z, Is.GreaterThan(5f));
        }

        [UnityTest]
        public IEnumerator Hook_AtANoHookSurface_IsNotReady_AndCostsNothing()
        {
            yield return Begin();
            _world.Box("Glass", new Vector3(0f, 1.5f, 4f), new Vector3(3f, 3f, 0.1f), HookShotSkill.NoHookLayer);
            _world.AimRightHandAt(new Vector3(0f, 1.6f, 4f));
            SkillDenyReason? denied = null;
            _world.Loadout.Denied += (_, reason) => denied = reason;

            yield return _world.Use(SkillRig.HookSlot, aimFrames: 3);

            Assert.That(denied, Is.EqualTo(SkillDenyReason.NotReady));
            Assert.That(_world.Loadout.IsRunning(SkillRig.HookSlot), Is.False);
            Assert.That(_world.Loadout.CooldownRemaining(SkillRig.HookSlot), Is.EqualTo(0));
            StringAssert.Contains("no target", _world.Rig.Menu.StatusText);
        }

        [UnityTest]
        public IEnumerator Booster_PushesForwardFasterThanWalking_AndUsesFuel()
        {
            yield return Begin();

            yield return _world.Use(SkillRig.BoostSlot);
            yield return SkillWorld.Frames(30);

            Assert.That(_world.Feet.z, Is.GreaterThan(1.5f), $"z={_world.Feet.z}");
            Assert.That(_world.Rig.Boost.Booster.Fuel, Is.LessThan(0.7f));
        }

        [UnityTest]
        public IEnumerator BackStep_StopsShortOfAWallBehind()
        {
            yield return Begin();
            _world.Box("Wall", new Vector3(0f, 1.5f, -1.3f), new Vector3(4f, 3f, 0.2f)); // 앞면 z -1.2

            yield return _world.Use(SkillRig.BackSlot);
            yield return _world.WaitWhileRunning(SkillRig.BackSlot);

            float radius = _world.Motor.Controller.radius;
            Assert.That(_world.Feet.z, Is.LessThan(-0.4f), "뒤로 물러났다");
            Assert.That(_world.Feet.z - radius, Is.GreaterThan(-1.2f), "벽을 파고들지 않았다");
            Assert.That(_world.Rig.Back.LastObstacleDistance, Is.LessThan(1.2f));
        }

        [UnityTest]
        public IEnumerator Cubes_BridgeAPit_AndStandingOnOneKeepsItAlive()
        {
            yield return Begin(floor: false);
            _world.Box("NearFloor", new Vector3(0f, -0.5f, -2f), new Vector3(4f, 1f, 6f));  // z -5 ~ 1
            _world.Box("FarFloor", new Vector3(0f, -0.5f, 6.1f), new Vector3(4f, 1f, 6f));  // z 3.1 ~ 9.1
            _world.Motor.Teleport(new Vector3(0f, 0f, 0.5f));
            yield return SkillWorld.Frames(5);
            float lowest = 0f;
            bool stoodOnCube = false;

            for (int i = 0; i < 3; i++)
            {
                yield return _world.Use(SkillRig.CubeSlot);
                _world.Sim.SetStick(HandSide.Left, new Vector2(0f, 1f));
                for (int f = 0; f < 21; f++)
                {
                    yield return null;
                    lowest = Mathf.Min(lowest, _world.Feet.y);
                    if (_world.Rig.Cube.TryGetId(_world.Motor.Ground, out int id))
                    {
                        stoodOnCube = true;
                        Assert.That(_world.Rig.Cube.Cubes.Remaining(id), Is.GreaterThan(1.5));
                    }
                }

                _world.Sim.SetStick(HandSide.Left, Vector2.zero);
            }

            _world.Sim.SetStick(HandSide.Left, new Vector2(0f, 1f));
            yield return SkillWorld.Frames(30);
            _world.Sim.SetStick(HandSide.Left, Vector2.zero);

            Assert.That(stoodOnCube, Is.True);
            Assert.That(lowest, Is.GreaterThan(-0.1f), "구덩이에 빠지지 않았다");
            Assert.That(_world.Feet.z, Is.GreaterThan(3.3f), "건너편에 도착");
            Assert.That(_world.Loadout.Charges(SkillRig.CubeSlot), Is.EqualTo(SkillRig.CubeCharges - 3));
        }

        [UnityTest]
        public IEnumerator Scale_Grow_ScalesEverything_AndBackToOneRestoresExactly()
        {
            yield return Begin();
            LocomotionRig body = _world.Rig.Body;

            _world.Rig.Scale.SetTarget(2f);
            yield return SkillWorld.Frames(60);

            Assert.That(body.TrackingSpace.localScale.x, Is.EqualTo(2f));
            Assert.That(body.Motor.Controller.height, Is.EqualTo(3.4f));
            Assert.That(body.Motor.MoveSpeed, Is.EqualTo(5f));
            Assert.That(body.RightHand.GrabRadius, Is.EqualTo(0.16f));
            Assert.That(body.Camera.nearClipPlane, Is.EqualTo(0.04f).Within(1e-6f));
            Assert.That(_world.Rig.Menu.Scale, Is.EqualTo(2f));
            Assert.That(_world.Feet.y, Is.EqualTo(0f).Within(0.05f), "몸이 커져도 발은 바닥에");

            _world.Rig.Scale.SetTarget(1f);
            yield return SkillWorld.Frames(60);

            Assert.That(body.TrackingSpace.localScale.x, Is.EqualTo(1f));
            Assert.That(body.Motor.Controller.height, Is.EqualTo(1.7f));
            Assert.That(body.Motor.MoveSpeed, Is.EqualTo(2.5f));
            Assert.That(body.RightHand.GrabRadius, Is.EqualTo(0.08f));
            Assert.That(body.Camera.nearClipPlane, Is.EqualTo(0.02f).Within(1e-6f));
        }
    }
}
