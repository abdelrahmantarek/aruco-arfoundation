#if !(PLATFORM_LUMIN && !UNITY_EDITOR)

using OpenCVForUnity.UnityIntegration;
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;
using static OpenCVForUnity.UnityIntegration.OpenCVARUtils;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
// using UnityMessageManager; // Removed incorrect namespace
// If you are using UnityFlutter plugin, use:
// using FlutterUnityIntegration;
using UnityEngine.SceneManagement;


namespace ARFoundationWithOpenCVForUnityExample
{
    /// <summary>
    /// Manager for AR objects and their transformations
    /// Handles object positioning, filtering, and coordinate transformations
    /// </summary>
    public class ARObjectManager : MonoBehaviour
    {
        #region Private Fields

        [SerializeField] public ARGameObject arGameObject;


        [SerializeField] public XROrigin xrOrigin;
        [SerializeField] public ARRaycastManager raycastManager;


        private Matrix4x4 fitARFoundationBackgroundMatrix;
        private Matrix4x4 fitHelpersFlipMatrix;
        private bool isInitialized = false;


        /// <summary>
        /// Determines if enable leap filter.
        /// </summary>
        [SerializeField()]
        public bool enableLerpFilter = false;

        private Pose? lastDetectedPose = null;


        // Lerp filtering parameters
        private float lerpSpeed = 5.0f;
        private bool hasValidPose = false;
        private Matrix4x4 lastValidMatrix = Matrix4x4.identity;

        // Multiple objects support
        private Dictionary<int, GameObject> markerObjects = new Dictionary<int, GameObject>();
        private GameObject prefabTemplate;

        // Performance optimization - frame rate control

        public enum PlacementMode { Direct, SnapToPlane }
        [SerializeField] private PlacementMode placementMode = PlacementMode.SnapToPlane;
        [SerializeField] public bool forceUpdate = false;


        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether lerp filtering is enabled
        /// </summary>

        /// <summary>
        /// Gets or sets the lerp speed for smooth transitions
        /// </summary>
        public float LerpSpeed
        {
            get => lerpSpeed;
            set => lerpSpeed = Mathf.Max(0.1f, value);
        }

        /// <summary>
        /// Gets the current AR game object
        /// </summary>
        public ARGameObject ARGameObject => arGameObject;

        /// <summary>
        /// Returns true if the manager is initialized
        /// </summary>
        public bool IsInitialized => isInitialized;

        /// <summary>
        /// Returns true if there's a valid pose for the AR object
        /// </summary>
        public bool HasValidPose => hasValidPose;



        #endregion

        #region Constructor & Initialization

        /// <summary>
        /// Initialize AR object manager
        /// </summary>
        /// <param name="arGameObject">The AR game object to manage</param>
        /// <param name="xrOrigin">The XR Origin for coordinate transformations</param>
        /// <param name="enableLerpFilter">Enable smooth filtering</param>
        public ARObjectManager()
        {

        }

        void Start()
        {
            Initialize();
            if (arGameObject != null)
            {
                originalScale = arGameObject.transform.localScale; // نخزن الحجم اللي جه بيه
            }
        }

        /// <summary>
        /// Initialize the AR object manager
        /// </summary>
        /// <param name="arGameObject">The AR game object to manage</param>
        /// <param name="xrOrigin">The XR Origin for coordinate transformations</param>
        /// <param name="enableLerpFilter">Enable smooth filtering</param>
        public void Initialize()
        {

            if (arGameObject == null)
            {
                Debug.LogError("ARObjectManager: AR game object is null");
                return;
            }

            if (xrOrigin == null)
            {
                Debug.LogError("ARObjectManager: XR Origin is null");
                return;
            }

            // Reset state
            hasValidPose = false;
            lastValidMatrix = Matrix4x4.identity;

            isInitialized = true;

            Debug.Log("ARObjectManager: Initialized successfully");
        }

        /// <summary>
        /// Setup transformation matrices for AR Foundation compatibility
        /// </summary>
        /// <param name="cameraHelper">Camera helper for flip properties</param>
        public void SetupTransformationMatrices(ARFoundationWithOpenCVForUnity.UnityIntegration.Helper.Source2Mat.ARFoundationCamera2MatHelper cameraHelper)
        {
            if (cameraHelper == null)
            {
                Debug.LogWarning("ARObjectManager: Camera helper is null, using identity matrices");
                fitARFoundationBackgroundMatrix = Matrix4x4.identity;
                fitHelpersFlipMatrix = Matrix4x4.identity;
                return;
            }

            // Create the transform matrix to fit the ARM to the background display by ARFoundationBackground component
            fitARFoundationBackgroundMatrix = Matrix4x4.Scale(new Vector3(
                cameraHelper.GetDisplayFlipHorizontal() ? -1 : 1,
                cameraHelper.GetDisplayFlipVertical() ? -1 : 1,
                1)) * Matrix4x4.identity;

            // Create the transform matrix to fit the ARM to the flip properties of the camera helper
            fitHelpersFlipMatrix = Matrix4x4.Scale(new Vector3(
                cameraHelper.FlipHorizontal ? -1 : 1,
                cameraHelper.FlipVertical ? -1 : 1,
                1)) * Matrix4x4.identity;

            Debug.Log("ARObjectManager: Transformation matrices setup completed");
        }

        #endregion

        #region Object Transformation Methods



        [SerializeField] private float updateDistanceThreshold = 0.010f; // 2 سم تقريباً
        [SerializeField] private float moveSpeed = 16f;                  // متر/ثانية
        [SerializeField] private float rotationSpeed = 180f;            // درجة/ثانية
        [SerializeField] private float markerLength = 0.188f;           // طول الماركر الحقيقي بالمتر
        [SerializeField] private float markerScaleFactor = 100f;        // تكبير إضافي للماركر
        [SerializeField] private float planeScaleFactor = 1f;           // تكبير إضافي للبلان

        [SerializeField] private bool showOnlyOnPlane = false; // خيار: اعرض فقط فوق البلان


        private Dictionary<int, bool> isPlacedOnPlane = new Dictionary<int, bool>();




        /// <summary>
        /// Reset AR object transform to identity
        /// </summary>
        public void ResetObjectTransform()
        {
            if (!isInitialized)
                return;

            Matrix4x4 identityMatrix = Matrix4x4.identity;

            if (arGameObject != null && arGameObject.transform != null)
            {
                OpenCVARUtils.SetTransformFromMatrix(arGameObject.transform, ref identityMatrix);
            }

            hasValidPose = false;
            lastValidMatrix = identityMatrix;

            Debug.Log("ARObjectManager: Object transform reset to identity");
        }

        /// <summary>
        /// Hide the AR object
        /// </summary>
        public void HideObject()
        {
            if (arGameObject != null && arGameObject.gameObject != null)
            {
                if (arGameObject.gameObject.activeSelf)
                {
                    arGameObject.gameObject.SetActive(false);
                    Debug.Log("ARObjectManager: Object hidden");
                }
            }
        }

        /// <summary>
        /// Show the AR object
        /// </summary>
        public void ShowObject()
        {
            if (arGameObject != null && arGameObject.gameObject != null)
            {
                if (!arGameObject.gameObject.activeSelf)
                {
                    arGameObject.gameObject.SetActive(true);
                    Debug.Log("ARObjectManager: Object shown");
                }
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get the current world position of the AR object
        /// </summary>
        /// <returns>World position</returns>
        public Vector3 GetWorldPosition()
        {
            if (arGameObject != null && arGameObject.transform != null)
            {
                return arGameObject.transform.position;
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Get the current world rotation of the AR object
        /// </summary>
        /// <returns>World rotation</returns>
        public Quaternion GetWorldRotation()
        {
            if (arGameObject != null && arGameObject.transform != null)
            {
                return arGameObject.transform.rotation;
            }
            return Quaternion.identity;
        }

        /// <summary>
        /// Get the current world scale of the AR object
        /// </summary>
        /// <returns>World scale</returns>
        public Vector3 GetWorldScale()
        {
            if (arGameObject != null && arGameObject.transform != null)
            {
                return arGameObject.transform.lossyScale;
            }
            return Vector3.one;
        }

        /// <summary>
        /// Check if the AR object is currently visible
        /// </summary>
        /// <returns>True if visible</returns>
        public bool IsObjectVisible()
        {
            return arGameObject != null &&
                   arGameObject.gameObject != null &&
                   arGameObject.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Get distance from camera to AR object
        /// </summary>
        /// <returns>Distance in world units</returns>
        public float GetDistanceFromCamera()
        {
            if (xrOrigin != null && xrOrigin.Camera != null && arGameObject != null)
            {
                return Vector3.Distance(xrOrigin.Camera.transform.position, arGameObject.transform.position);
            }
            return 0f;
        }

        #endregion

        #region Multiple Objects Support

        /// <summary>
        /// Set the prefab template for creating multiple objects
        /// </summary>
        /// <param name="prefab">Prefab to use as template</param>
        public void SetPrefabTemplate(GameObject prefab)
        {
            prefabTemplate = prefab;
        }

        /// <summary>
        /// Update multiple AR objects from multiple pose data with frame rate control
        /// </summary>
        /// <param name="poseDataList">List of pose data from marker detection</param>
        /// <param name="markerIds">List of corresponding marker IDs</param>
        /// <param name="forceUpdate">Force update regardless of frame count</param>
        public void UpdateMultipleObjects(List<MarkerData> markers)
        {
            if (!isInitialized || markers == null || markers.Count == 0)
            {
                Debug.LogWarning("ARObjectManager: Not initialized or no markers to update");
                return;
            }

            HideObject();

            foreach (var marker in markers)
            {
                // لو الماركر موجود مسبقاً → لا تحدثه
                if (markerObjects.ContainsKey(marker.markerId) && !forceUpdate)
                {
                    continue;
                }

                GameObject markerObject = GetOrCreateMarkerObject(marker.markerId);

                if (markerObject != null && marker.pose != null)
                {
                    markerObject.SetActive(true);

                    // أول تحديث فقط
                    UpdateObjectTransform(markerObject, marker, marker.markerId);

                    // ضبط الحجم
                    float scaleFactor = markerLength / 1.0f;
                    markerObject.transform.localScale = originalScale * scaleFactor;
                }
            }
        }

        private void UpdateObjectTransform(GameObject obj, MarkerData marker, int markerId)
        {
            if (obj == null) return;

            var pos = new PoseData();

            if (marker.pose.HasValue)
            {
                pos.position = marker.pose.Value.position;
                pos.rotation = marker.pose.Value.rotation;
            }

            // Pose من ArUco
            Matrix4x4 armMatrix = OpenCVARUtils.ConvertPoseDataToMatrix(ref pos, true);
            Matrix4x4 worldMatrix = xrOrigin.Camera.transform.localToWorldMatrix * armMatrix;

            Vector3 markerPos = worldMatrix.GetColumn(3);
            Quaternion markerRot = Quaternion.LookRotation(worldMatrix.GetColumn(2), worldMatrix.GetColumn(1));

            // الوضع الافتراضي: اعتمد على ArUco
            Vector3 targetPos = markerPos;
            Quaternion targetRot = markerRot;

            bool placedOnPlane = false;

            // (اختياري) جرب Raycast عشان position بس
            Vector2 screenPoint = xrOrigin.Camera.WorldToScreenPoint(markerPos);
            marker.screenPoint = screenPoint;

            List<ARRaycastHit> hits = new List<ARRaycastHit>();

            if (raycastManager.Raycast(screenPoint, hits, TrackableType.PlaneWithinPolygon))
            {
                Pose hitPose = hits[0].pose;
                targetPos = hitPose.position;

                placedOnPlane = true;
                isPlacedOnPlane[markerId] = true;
            }
            else
            {
                isPlacedOnPlane[markerId] = false;
            }

            // تأكد أنه ظاهر
            if (showOnlyOnPlane && !placedOnPlane)
            {
                obj.SetActive(false);
                return;
            }
            else
            {
                if (!obj.activeInHierarchy)
                    obj.SetActive(true);
            }

            // ✅ تحديث مباشر بدون smoothing
            obj.transform.position = targetPos;
            obj.transform.rotation = targetRot;

            // تحديث الحجم
            float scaleFactor = markerLength / 1.0f;
            obj.transform.localScale = originalScale * scaleFactor;

            // 📝 تحديث بيانات الماركر
            AddOrUpdateMarker(markerId, marker, placedOnPlane);
        }



        private void AddOrUpdateMarker(int id, MarkerData marker, bool placedOnPlane)
        {
            MarkerData existing = markers.Find(m => m.markerId == id);
            if (existing != null)
            {
                existing = marker;
            }
            else
            {
                markers.Add(marker);
            }
        }


        /// <summary>
        /// Get existing object for marker or create new one
        /// </summary>
        /// <param name="markerId">Marker ID</param>
        /// <returns>GameObject for the marker</returns>
        private GameObject GetOrCreateMarkerObject(int markerId)
        {
            // Check if object already exists for this marker
            if (markerObjects.ContainsKey(markerId))
            {
                return markerObjects[markerId];
            }

            // Create new object
            GameObject newObject = null;

            if (prefabTemplate != null)
            {
                // Use prefab template
                newObject = Object.Instantiate(prefabTemplate);
                newObject.name = $"ARObject_Marker_{markerId}";
            }
            else if (arGameObject != null)
            {
                // Use original ARGameObject as template
                newObject = Object.Instantiate(arGameObject.gameObject);
                newObject.name = $"ARObject_Marker_{markerId}";
            }
            else
            {
                // Create simple cube as fallback
                newObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                newObject.name = $"ARObject_Marker_{markerId}";
                newObject.transform.localScale = Vector3.one * 0.1f; // 10cm cube

                // Add a colored material
                Renderer renderer = newObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material mat = new Material(Shader.Find("Standard"));
                    mat.color = GetColorForMarker(markerId);
                    renderer.material = mat;
                }
            }

            if (newObject != null)
            {
                markerObjects[markerId] = newObject;
                Debug.Log($"ARObjectManager: Created object for marker {markerId}");
            }

            return newObject;
        }


        private Vector3 originalScale; // نخزن الحجم الأصلي



        /// <summary>
        /// Hide all marker objects
        /// </summary>
        public void HideAllObjects()
        {
            foreach (var kvp in markerObjects)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.SetActive(false);
                }
            }
        }



        /// <summary>
        /// Get a unique color for each marker
        /// </summary>
        /// <param name="markerId">Marker ID</param>
        /// <returns>Color for the marker</returns>
        private Color GetColorForMarker(int markerId)
        {
            // Generate different colors based on marker ID
            float hue = (markerId * 0.618033988749895f) % 1.0f; // Golden ratio for good distribution
            return Color.HSVToRGB(hue, 0.8f, 0.9f);
        }

        /// <summary>
        /// Get count of active marker objects
        /// </summary>
        /// <returns>Number of active objects</returns>
        public int GetActiveObjectsCount()
        {
            int count = 0;
            foreach (var kvp in markerObjects)
            {
                if (kvp.Value != null && kvp.Value.activeSelf)
                {
                    count++;
                }
            }
            return count;
        }


        private List<MarkerData> markers = new List<MarkerData>();


        void Update()
        {
            if (markers.Count > 0)
            {
                // حوّل الماركرز إلى JSON Array
                List<MarkerExport> exports = MarkerMapper.MapList(markers);
                string markersJson = JsonHelper.ToJson(exports.ToArray(), true);

                // حوّل الكائن نفسه إلى JSON

                // أرسل للفلتر
                SendToFlutter.Send(markersJson);

                // Debug.Log($"[Markers JSON Payload] {markersJson}");
            }
        }


        #endregion

        #region Cleanup

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Dispose()
        {
            // Destroy all marker objects
            foreach (var kvp in markerObjects)
            {
                if (kvp.Value != null)
                {
                    Object.DestroyImmediate(kvp.Value);
                }
            }
            markerObjects.Clear();

            arGameObject = null;
            xrOrigin = null;
            prefabTemplate = null;
            hasValidPose = false;
            isInitialized = false;

            Debug.Log("ARObjectManager: Disposed");
        }

        public void resetObjects()
        {
            foreach (var kvp in markerObjects)
            {
                if (kvp.Value != null)
                {
                    Object.Destroy(kvp.Value); // استخدم Destroy العادي بدل Clear بس
                }
            }
            markerObjects.Clear();
            Debug.Log("ARObjectManager: resetObjects Object Destroy");
        }


        #endregion
    }


}

[System.Serializable]
public class MarkerData
{
    public int markerId;
    public PoseData? pose; // nullable
    public Vector3[] corners;
    public Vector2? screenPoint;
    public float lastSeenTime;
    public bool placedOnPlane;
}



[System.Serializable]
public class MarkerDataWrapper
{
    public List<MarkerData> markers;
}

#endif
