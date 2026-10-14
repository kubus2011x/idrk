using UnityEngine;

namespace ShiftMeal
{
    /// <summary>Makes sure exactly one GameManager exists, in ANY scene (even an empty one).</summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ProcAudio.ClearCache();
            Mats.ClearCache();
            GameManager.I = null;
            Cargo.All.Clear();
            Inspector.All.Clear();
            GreaseSpill.All.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (GameManager.I == null && Object.FindFirstObjectByType<GameManager>() == null)
                new GameObject("ShiftMeal Game").AddComponent<GameManager>();
        }
    }
}
