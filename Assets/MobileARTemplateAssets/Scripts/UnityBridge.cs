using ARFoundationWithOpenCVForUnityExample;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UnityBridge : MonoBehaviour
{


    [SerializeField] ARObjectManager aRObjectManager;

    // هذا الميثود لازم يكون public 
    public void OnDispose(string message)
    {
        Debug.Log($"[Flutter->Unity] OnDispose called with: Application {message}");
        // مثال: إغلاق المشهد الحالي
        var currentScene = SceneManager.GetActiveScene();
        SceneManager.UnloadSceneAsync(currentScene);
    }

    public void Reset(string message)
    {
        Debug.Log($"[Flutter->Unity] OnDispose called with: Application {message}");
        aRObjectManager.resetObjects();

    }

}
