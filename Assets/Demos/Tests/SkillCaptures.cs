using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jongreul.XrLocomotion.Demos.Tests
{
    /// <summary>
    /// 라디얼 선택 GIF(1인칭). 스틱으로 네 칸을 훑고 BOOST를 골라 달린 뒤, BACK으로 물러난다.
    /// 실행: -runTests -testPlatform PlayMode -testFilter Jongreul.XrLocomotion.Demos.Tests.RadialCapture (-nographics 없이)
    /// </summary>
    [Explicit, Category("Capture")]
    public class RadialCapture : CaptureBase
    {
        [UnityTest]
        public IEnumerator RecordRadial()
        {
            CreateDemo(1);
            Sim.SetHead(new Vector3(0f, 1.6f, 0f), 0f, 14f);
            Sim.SetHand(HandSide.Right, new Vector3(0.1f, 1.38f, 0.36f), Quaternion.identity);
            yield return null;

            Demo.StartCoroutine(Script());
            yield return DemoCapture.Record(new[] { Demo.HeadCamera }, new[] { "radial" }, seconds: 9f, fps: 15f, lockTime: true);
        }

        IEnumerator Script()
        {
            yield return Wait(0.5f);
            // 스틱을 돌려 네 칸을 훑는다(위 HOOK → 오른쪽 BOOST → 아래 BACK → 왼쪽 CUBE → 다시 오른쪽)
            foreach (float angle in new[] { 0f, 90f, 180f, 270f, 90f })
            {
                float radians = angle * Mathf.Deg2Rad;
                Sim.SetStick(HandSide.Right, new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)));
                yield return Wait(0.55f);
            }

            Sim.SetStick(HandSide.Right, Vector2.zero); // 놓으면 BOOST 확정
            yield return Wait(0.8f);
            yield return Trigger(true, 0.15f);
            yield return Trigger(false, 1.6f);         // 부스터로 앞으로

            yield return PickSlot(SkillRig.BackSlot);
            yield return Trigger(true, 0.15f);
            yield return Trigger(false, 1.2f);         // 뒤로 물러남
        }
    }

    /// <summary>
    /// 훅샷 GIF(관찰자 + 1인칭 작은 창). 유리를 겨누면 빨강·거절, 턱 벽면 윗부분을 겨누면 초록 → 떼면 당겨져 턱 위로 올라선다.
    /// 실행: -runTests -testPlatform PlayMode -testFilter Jongreul.XrLocomotion.Demos.Tests.HookCapture (-nographics 없이)
    /// </summary>
    [Explicit, Category("Capture")]
    public class HookCapture : CaptureBase
    {
        [UnityTest]
        public IEnumerator RecordHook()
        {
            CreateDemo(0);
            Sim.SetHead(new Vector3(0f, 1.6f, 0f), 8f, -8f);
            Sim.SetHand(HandSide.Right, new Vector3(0.22f, 1.25f, 0.35f), Quaternion.identity);

            Camera spectator = CreateSpectator(48f);
            // 왼쪽 앞에서: 출발점·유리(오른쪽)·턱과 그 위로 올라서는 몸이 함께 보인다.
            spectator.transform.position = new Vector3(CourseDemo.HookX - 4.8f, 3.6f, -1.4f);
            spectator.transform.LookAt(new Vector3(CourseDemo.HookX + 0.3f, 1.8f, 4.6f));
            yield return null;

            Demo.StartCoroutine(Script());
            yield return DemoCapture.Record(new[] { spectator, Demo.HeadCamera }, new[] { "hook", "hook-fp" },
                seconds: 9f, fps: 15f, lockTime: true);
        }

        IEnumerator Script()
        {
            yield return Wait(0.4f);
            yield return PickSlot(SkillRig.HookSlot);

            // 유리: 걸 수 없다
            AimRightHandAt(Demo.Glass.position + Vector3.up * 0.3f);
            yield return Trigger(true, 1.0f);
            yield return Trigger(false, 1.1f);

            // 턱 벽면 윗부분: 윗면으로 당겨 올라선다
            Vector3 ledgeFace = Demo.Ledge.position + new Vector3(0f, 0.85f, -1f); // 앞면(z 6), 윗면에서 0.4 m 아래
            AimRightHandAt(ledgeFace);
            yield return Trigger(true, 1.0f);
            yield return Trigger(false, 0.1f);
            while (Player.Skills.Loadout.IsRunning(SkillRig.HookSlot))
                yield return null;
            yield return Wait(0.4f);
            Sim.SetHand(HandSide.Right, new Vector3(0.25f, 1.05f, 0.25f), Quaternion.identity);
            yield return Look(20f, 15f, 1.0f, 8f, -8f);
        }
    }

    /// <summary>
    /// 크기 보정 GIF(관찰자 + 1인칭 작은 창). x2 발판에 올라 커졌다가, x1으로 돌아오고, x0.5로 작아져 낮은 굴로 들어간다.
    /// 실행: -runTests -testPlatform PlayMode -testFilter Jongreul.XrLocomotion.Demos.Tests.ScaleCapture (-nographics 없이)
    /// </summary>
    [Explicit, Category("Capture")]
    public class ScaleCapture : CaptureBase
    {
        [UnityTest]
        public IEnumerator RecordScale()
        {
            CreateDemo(4);
            Camera spectator = CreateSpectator(50f);
            spectator.transform.position = new Vector3(CourseDemo.ScaleX + 4.6f, 4.2f, -4.2f);
            spectator.transform.LookAt(new Vector3(CourseDemo.ScaleX - 0.2f, 1.0f, 3.2f));
            yield return null;

            Demo.StartCoroutine(Script());
            yield return DemoCapture.Record(new[] { spectator, Demo.HeadCamera }, new[] { "scale", "scale-fp" },
                seconds: 13f, fps: 15f, lockTime: true);
        }

        IEnumerator WalkTo(Vector3 target, float maxSeconds = 4f)
        {
            float start = Time.time;
            while (Time.time - start < maxSeconds)
            {
                Vector3 delta = target - Body.position;
                delta.y = 0f;
                if (delta.magnitude < 0.12f)
                    break;
                float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                Sim.SetHead(new Vector3(0f, 1.6f, 0f), yaw, 10f);
                Sim.SetStick(HandSide.Left, new Vector2(0f, Mathf.Clamp01(delta.magnitude * 2f)));
                yield return null;
            }

            Sim.SetStick(HandSide.Left, Vector2.zero);
        }

        IEnumerator Script()
        {
            float x = CourseDemo.ScaleX;
            yield return Wait(0.4f);
            yield return WalkTo(new Vector3(x + 1.7f, 0f, 1.8f)); // x2
            Sim.SetHead(new Vector3(0f, 1.6f, 0f), 60f, 5f);       // 높이 자 쪽
            yield return Wait(1.6f);
            yield return WalkTo(new Vector3(x, 0f, 1.8f));          // x1
            yield return Wait(1.0f);
            yield return WalkTo(new Vector3(x - 1.7f, 0f, 1.8f));   // x0.5
            yield return Wait(0.9f);
            yield return WalkTo(new Vector3(x - 1.7f, 0f, 7f));     // 낮은 굴 속으로
            Sim.SetHead(new Vector3(0f, 1.6f, 0f), 0f, 0f);
            yield return Wait(1.0f);
        }
    }
}
