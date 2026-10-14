#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftMeal.EditorTools
{
    /// <summary>
    /// Idempotent project setup. Runs automatically on first open (creates Assets/Scenes/Main.unity),
    /// and can be re-run any time from Tools > Shift Meal > Generate Prototype.
    /// </summary>
    [InitializeOnLoad]
    public static class ShiftMealSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        static ShiftMealSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureShaders();
            ConfigureProject();
            if (!File.Exists(ScenePath)) Generate(true);
        }

        [MenuItem("Tools/Shift Meal/Generate Prototype")]
        public static void GenerateMenu() { Generate(true); }

        [MenuItem("Tools/Shift Meal/Open Main Scene")]
        public static void OpenMain()
        {
            if (!File.Exists(ScenePath)) Generate(true);
            else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/Shift Meal/Build Windows (x64)")]
        public static void BuildWindows()
        {
            Generate(false);
            string dir = "Builds/Windows";
            Directory.CreateDirectory(dir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = dir + "/ShiftMeal.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[ShiftMeal] Build result: " + report.summary.result + " -> " + opts.locationPathName);
        }

        static void Generate(bool open)
        {
            EnsureFolders();
            EnsureShaders();
            ConfigureProject();

            Scene scene;
            if (File.Exists(ScenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            // exactly one GameManager in the scene (repairs duplicates / missing)
            var gms = Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None);
            for (int i = 1; i < gms.Length; i++) Object.DestroyImmediate(gms[i].gameObject);
            if (gms.Length == 0)
            {
                var go = new GameObject("ShiftMeal Game");
                go.AddComponent<GameManager>();
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ShiftMeal] Prototype scene ready: " + ScenePath + "  (press Play)");
        }

        static void EnsureFolders()
        {
            string[] folders = { "Assets/Scenes", "Assets/Materials", "Assets/Prefabs", "Assets/Audio" };
            foreach (var f in folders)
                if (!AssetDatabase.IsValidFolder(f)) AssetDatabase.CreateFolder("Assets", f.Substring("Assets/".Length));
        }

        static void ConfigureProject()
        {
            PlayerSettings.productName = "Shift Meal - Nightmare Delivery";
            PlayerSettings.companyName = "MunchCorp Interactive";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
        }

        /// <summary>Runtime code uses Shader.Find("Standard"); make sure it is included in player builds.</summary>
        static void EnsureShaders()
        {
            var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (gs == null) return;
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;
            string[] names = { "Standard", "Legacy Shaders/Diffuse", "Sprites/Default" };
            bool changed = false;
            foreach (var n in names)
            {
                var sh = Shader.Find(n);
                if (sh == null) continue;
                bool has = false;
                for (int i = 0; i < arr.arraySize; i++)
                    if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) { has = true; break; }
                if (has) continue;
                arr.arraySize++;
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
                changed = true;
            }
            if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets(); }
        }
    }
}
#endif
