#if !(PLATFORM_LUMIN && !UNITY_EDITOR)

using OpenCVForUnity.UnityIntegration;
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;
using static OpenCVForUnity.UnityIntegration.OpenCVARUtils;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

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
        private int frameSkipCount = 30;  // Update every 30 frames
        private int currentFrameCount = 0;


        public enum PlacementMode { Direct, SnapToPlane }
        [SerializeField] private PlacementMode placementMode = PlacementMode.SnapToPlane;


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

        /// <summary>
        /// Gets or sets the frame skip count for performance optimization
        /// </summary>
        public int FrameSkipCount
        {
            get => frameSkipCount;
            set => frameSkipCount = Mathf.Max(1, value);
        }

        /// <summary>
        /// Gets the current frame count
        /// </summary>
        public int CurrentFrameCount => currentFrameCount;

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

        private Dictionary<int, bool> isPlacedOnPlane = new Dictionary<int, bool>();

        private void UpdateObjectTransform(GameObject obj, PoseData poseData, int markerId)
        {
            if (obj == null) return;

            // Pose من ArUco
            Matrix4x4 armMatrix = OpenCVARUtils.ConvertPoseDataToMatrix(ref poseData, true);
            Matrix4x4 worldMatrix = xrOrigin.Camera.transform.localToWorldMatrix * armMatrix;

            Vector3 markerPos = worldMatrix.GetColumn(3);
            Quaternion markerRot = Quaternion.LookRotation(worldMatrix.GetColumn(2), worldMatrix.GetColumn(1));

            // الوضع الافتراضي: في الهواء
            Vector3 targetPos = markerPos;
            Quaternion targetRot = markerRot;

            bool placedOnPlane = false;

            // جرب Raycast على الـ plane
            Vector2 screenPoint = xrOrigin.Camera.WorldToScreenPoint(markerPos);
            List<ARRaycastHit> hits = new List<ARRaycastHit>();

            if (raycastManager.Raycast(screenPoint, hits, TrackableType.PlaneWithinPolygon))
            {
                Pose hitPose = hits[0].pose;
                targetPos = hitPose.position;

                // اعتبره مثبت على plane
                placedOnPlane = true;
                isPlacedOnPlane[markerId] = true;
            }
            else
            {
                isPlacedOnPlane[markerId] = false;
            }

            // تأكد أن الكائن ظاهر
            if (!obj.activeInHierarchy)
                obj.SetActive(true);

            float distance = Vector3.Distance(obj.transform.position, targetPos);

            // لو المسافة بعيدة جداً → نقفز مباشرة
            if (distance > 0.5f)
            {
                obj.transform.position = targetPos;
                obj.transform.rotation = targetRot;
            }
            else
            {
                // تحديث الموقع فقط إذا الفرق أكبر من threshold
                if (distance > updateDistanceThreshold)
                {
                    obj.transform.position = Vector3.MoveTowards(
                        obj.transform.position,
                        targetPos,
                        Time.deltaTime * moveSpeed
                    );
                }

                if (Quaternion.Angle(obj.transform.rotation, targetRot) > 1f)
                {
                    obj.transform.rotation = Quaternion.RotateTowards(
                        obj.transform.rotation,
                        targetRot,
                        rotationSpeed * Time.deltaTime
                    );
                }
            }

            // تحديث الحجم
            float scaleFactor = markerLength / 1.0f;
            obj.transform.localScale = originalScale * scaleFactor;

            // 🎨 تحديث اللون حسب الحالة
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (placedOnPlane)
                    renderer.material.color = Color.red; // مثبت على plane
                else
                    renderer.material.color = Color.blue;  // في الهواء
            }
        }



        /// <summary>
        /// Apply transformation directly without filtering
        /// </summary>
        /// <param name="matrix">Transformation matrix</param>
        private void ApplyDirectTransform(Matrix4x4 matrix)
        {
            if (arGameObject != null && arGameObject.transform != null)
            {
                OpenCVARUtils.SetTransformFromMatrix(arGameObject.transform, ref matrix);
            }
        }


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
        public void UpdateMultipleObjects(List<PoseData> poseDataList, List<int> markerIds, bool forceUpdate = false)
        {
            if (!isInitialized || poseDataList == null || markerIds == null)
            {
                Debug.LogWarning("ARObjectManager: Not initialized or invalid data");
                return;
            }

            if (poseDataList.Count != markerIds.Count)
            {
                Debug.LogWarning("ARObjectManager: Pose data and marker IDs count mismatch");
                return;
            }

            // Check if we should update based on frame rate control
            currentFrameCount++;
            bool shouldUpdate = forceUpdate || (currentFrameCount >= frameSkipCount);

            if (!shouldUpdate)
            {
                return; // Skip this frame for performance
            }

            // Reset frame counter
            currentFrameCount = 0;

            // Hide the original AR object to prevent duplication
            HideObject();

            // Hide all existing marker objects first
            // HideAllObjects();

            // Update or create objects for each detected marker
            for (int i = 0; i < poseDataList.Count; i++)
            {
                int markerId = markerIds[i];
                PoseData poseData = poseDataList[i];

                GameObject markerObject = GetOrCreateMarkerObject(markerId);

                if (markerObject != null)
                {
                    markerObject.SetActive(true);

                    // حدّث مكانه بس إذا في PoseData جديد
                    UpdateObjectTransform(markerObject, poseData, markerId);

                    // ضبط الحجم
                    float scaleFactor = markerLength / 1.0f;
                    markerObject.transform.localScale = originalScale * scaleFactor;
                }
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

        // public void SnapToPlane()
        // {
        //     if (!isInitialized || arGameObject == null || raycastManager == null)
        //         return;

        //     Vector3 objectScreenPos = xrOrigin.Camera.WorldToScreenPoint(arGameObject.transform.position);
        //     Vector2 screenPoint = new Vector2(objectScreenPos.x, objectScreenPos.y);

        //     List<ARRaycastHit> hits = new List<ARRaycastHit>();

        //     if (raycastManager.Raycast(screenPoint, hits, TrackableType.PlaneWithinPolygon))
        //     {
        //         Pose hitPose = hits[0].pose;

        //         // ثبت على الـ Plane
        //         arGameObject.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);

        //         // احسب scale نسبةً للحجم الأصلي
        //         float targetSize = markerLength; // الحجم اللي نبغاه (بالمتر)
        //         float prefabSize = 1.0f; // اعتبر الحجم الأصلي للمكعب 1 متر

        //         float scaleFactor = targetSize / prefabSize;

        //         arGameObject.transform.localScale = originalScale * scaleFactor;

        //         hasValidPose = true;
        //         lastValidMatrix = Matrix4x4.TRS(hitPose.position, hitPose.rotation, arGameObject.transform.localScale);

        //         Debug.Log($"ARObjectManager: Snapped with scaleFactor={scaleFactor}");
        //     }
        // }
        /// <summary>
        /// Update transform for a specific object
        /// </summary>
        /// <param name="obj">GameObject to update</param>
        /// <param name="poseData">Pose data</param>
        // private void UpdateObjectTransform(GameObject obj, PoseData poseData)
        // {
        //     if (obj == null) return;

        //     // Convert pose data to transformation matrix
        //     Matrix4x4 armMatrix = OpenCVARUtils.ConvertPoseDataToMatrix(ref poseData, true);

        //     // Transform to world space
        //     Matrix4x4 worldMatrix = xrOrigin.Camera.transform.localToWorldMatrix * armMatrix;

        //     // Apply transformation
        //     OpenCVARUtils.SetTransformFromMatrix(obj.transform, ref worldMatrix);
        // }

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

        /// <summary>
        /// Reset frame counter (useful for forcing immediate update)
        /// </summary>
        public void ResetFrameCounter()
        {
            currentFrameCount = 0;
        }

        /// <summary>
        /// Check if update should occur based on frame rate control
        /// </summary>
        /// <returns>True if update should occur</returns>
        public bool ShouldUpdate()
        {
            return currentFrameCount >= frameSkipCount;
        }

        /// <summary>
        /// Force next update regardless of frame count
        /// </summary>
        public void ForceNextUpdate()
        {
            currentFrameCount = frameSkipCount; // Force update on next call
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

        #endregion
    }
}



#endif
