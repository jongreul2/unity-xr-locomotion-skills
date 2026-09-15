using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jongreul.XrLocomotion.Demos.Tests
{
    /// <summary>
    /// 촬영 스크립트 공통: 데모 생성, 게임 시간 기준 손·스틱·시선 조작, 관찰자 카메라.
    /// 촬영은 게임 시간을 프레임에 고정하므로(<see cref="DemoCapture"/> lockTime) 대기는 모두 게임 시간이다.
    /// </summary>
    public abstract class CaptureBase
    {
        protected CourseDemo Demo;
        protected Camera Spectator;

        protected DesktopHandSimulator Sim => Demo.Simulator;
        protected SkillRig Player => Demo.Player;
        protected Transform Body => Player.Body.Root.transform;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Demo != null)
                Object.Destroy(Demo.gameObject);
            if (Spectator != null)
                Object.Destroy(Spectator.gameObject);
            yield return null;
        }

        protected void CreateDemo(int spot)
        {
            Demo = new GameObject("LocomotionCourse").AddComponent<CourseDemo>();
            Player.Body.Rig.PreferXRDevice = false;
            Sim.KeyboardAndMouse = false;
            Demo.TeleportTo(spot);
            Sim.SetHead(new Vector3(0f, 1.6f, 0f), 0f, 0f);
            Sim.SetHand(HandSide.Left, new Vector3(-0.25f, 1.05f, 0.25f), Quaternion.identity);
            Sim.SetHand(HandSide.Right, new Vector3(0.25f, 1.05f, 0.25f), Quaternion.identity);
        }

        protected Camera CreateSpectator(float fieldOfView = 50f, bool hideAvatar = false)
        {
            Spectator = new GameObject("Spectator").AddComponent<Camera>();
            Spectator.fieldOfView = fieldOfView;
            Spectator.clearFlags = CameraClearFlags.SolidColor;
            Spectator.backgroundColor = CourseDemo.Backdrop;
            if (hideAvatar)
                Spectator.cullingMask &= ~(1 << CourseDemo.AvatarLayer);
            return Spectator;
        }

        protected static IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        protected IEnumerator MoveHand(HandSide side, Vector3 toLocal, Quaternion rotation, float seconds)
        {
            Vector3 from = Sim.GetHandPosition(side);
            float start = Time.time;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Clamp01((Time.time - start) / seconds);
                float eased = t * t * (3f - 2f * t);
                Sim.SetHand(side, Vector3.Lerp(from, toLocal, eased), rotation);
                yield return null;
            }
        }

        /// <summary>오른손을 그 자리에서 world 지점을 향하게 돌린다.</summary>
        protected void AimRightHandAt(Vector3 world)
        {
            Vector3 local = Sim.GetHandPosition(HandSide.Right);
            Transform tracking = Player.Body.TrackingSpace;
            Vector3 direction = tracking.InverseTransformDirection(world - tracking.TransformPoint(local));
            Sim.SetHand(HandSide.Right, local, Quaternion.LookRotation(direction, Vector3.up));
        }

        /// <summary>오른손 스틱을 슬롯 방향으로 밀었다가 놓아 고른다.</summary>
        protected IEnumerator PickSlot(int slot, float holdSeconds = 0.5f)
        {
            Sim.SetStick(HandSide.Right, SkillRig.StickFor(slot));
            yield return Wait(holdSeconds);
            Sim.SetStick(HandSide.Right, Vector2.zero);
            yield return Wait(0.15f);
        }

        protected IEnumerator Trigger(bool down, float after = 0.1f)
        {
            Sim.SetTrigger(HandSide.Right, down ? 1f : 0f);
            yield return Wait(after);
        }

        protected IEnumerator Walk(Vector2 stick, float seconds)
        {
            Sim.SetStick(HandSide.Left, stick);
            yield return Wait(seconds);
            Sim.SetStick(HandSide.Left, Vector2.zero);
        }

        protected IEnumerator Look(float yaw, float pitch, float seconds, float startYaw, float startPitch)
        {
            float start = Time.time;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Clamp01((Time.time - start) / seconds);
                float eased = t * t * (3f - 2f * t);
                Sim.SetHead(new Vector3(0f, 1.6f, 0f), Mathf.Lerp(startYaw, yaw, eased), Mathf.Lerp(startPitch, pitch, eased));
                yield return null;
            }
        }

        protected static void AssertFinished(bool condition, string message) => Assert.That(condition, Is.True, message);
    }
}
