#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PsProbeDormantCleaner
{
    private const string ProbeCanvasName = "PSProbeOverlayCanvas";

    static PsProbeDormantCleaner()
    {
        EditorApplication.delayCall += CleanupDormantArtifacts;
        EditorSceneManager.sceneOpened += (_, _) => CleanupDormantArtifacts();
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode ||
            state == PlayModeStateChange.ExitingEditMode ||
            state == PlayModeStateChange.EnteredPlayMode)
        {
            CleanupDormantArtifacts();
        }
    }

    private static void CleanupDormantArtifacts()
    {
        CleanupProbeComponents();
        CleanupProbeCanvases();
    }

    private static void CleanupProbeComponents()
    {
        BullfightPsControllerCompatibilityProbe[] probes = Object.FindObjectsOfType<BullfightPsControllerCompatibilityProbe>(true);
        for (int i = 0; i < probes.Length; i++)
        {
            BullfightPsControllerCompatibilityProbe probe = probes[i];
            if (probe == null || EditorUtility.IsPersistent(probe))
                continue;

            Object.DestroyImmediate(probe);
        }
    }

    private static void CleanupProbeCanvases()
    {
        GameObject[] gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < gameObjects.Length; i++)
        {
            GameObject gameObject = gameObjects[i];
            if (gameObject == null || gameObject.name != ProbeCanvasName || EditorUtility.IsPersistent(gameObject))
                continue;

            Object.DestroyImmediate(gameObject);
        }
    }
}
#endif
