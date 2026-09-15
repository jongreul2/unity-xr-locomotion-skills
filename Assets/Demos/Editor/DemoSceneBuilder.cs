using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jongreul.XrLocomotion.Demos.Editor
{
    /// <summary>
    /// 데모 씬을 코드로 다시 만든다. 씬에는 부트스트랩(<see cref="CourseDemo"/>) 하나만 둔다.
    /// 배치 실행: -executeMethod Jongreul.XrLocomotion.Demos.Editor.DemoSceneBuilder.BuildAll
    /// </summary>
    public static class DemoSceneBuilder
    {
        public const string CourseScenePath = "Assets/Demos/LocomotionCourse.unity";

        [MenuItem("Tools/XR Locomotion Skills/Rebuild Demo Scene")]
        public static void BuildAll()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("LocomotionCourse", typeof(CourseDemo));
            EditorSceneManager.SaveScene(scene, CourseScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(CourseScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[DemoSceneBuilder] {CourseScenePath}");
        }
    }
}
