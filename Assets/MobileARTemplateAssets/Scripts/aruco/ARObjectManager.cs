#if !(PLATFORM_LUMIN && !UNITY_EDITOR)

using OpenCVForUnity.UnityIntegration;
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;
using static OpenCVForUnity.UnityIntegration.OpenCVARUtils;

namespace ARFoundationWithOpenCVForUnityExample
{
    /// <summary>
    /// Manager for AR objects and their transformations
    /// Handles object positioning, filtering, and coordinate transformations
    /// </summary>
    public class ARObjectManager
    {
        #region Private Fields

        private ARGameObject arGameObject;
        private XROrigin xrOrigin;
        private bool enableLerpFilter;
        private Matrix4x4 fitARFoundationBackgroundMatrix;
        private Matrix4x4 fitHelpersFlipMatrix;
        private bool isInitialized = false;

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

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets whether lerp filtering is enabled
        /// </summary>
        public bool EnableLerpFilter
        {
            get => enableLerpFilter;
            set => enableLerpFilter = value;
        }

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
        public ARObjectManager(ARGameObject arGameObject, XROrigin xrOrigin, bool enableLerpFilter = false)
        {
            Initialize(arGameObject, xrOrigin, enableLerpFilter);
        }

        /// <summary>
        /// Initialize the AR object manager
        /// </summary>
        /// <param name="arGameObject">The AR game object to manage</param>
        /// <param name="xrOrigin">The XR Origin for coordinate transformations</param>
        /// <param name="enableLerpFilter">Enable smooth filtering</param>
        public void Initialize(ARGameObject arGameObject, XROrigin xrOrigin, bool enableLerpFilter = false)
        {
            this.arGameObject = arGameObject;
            this.xrOrigin = xrOrigin;
            this.enableLerpFilter = enableLerpFilter;

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

        /// <summary>
        /// Update AR object transform from pose data
        /// </summary>
        /// <param name="poseData">Pose data from marker detection</param>
        public void UpdateObjectTransform(PoseData poseData)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("ARObjectManager: Not initialized");
                return;
            }

            // Ensure object is visible when updating transform
            ShowObject();

            // Convert pose data to transformation matrix
            Matrix4x4 armMatrix = OpenCVARUtils.ConvertPoseDataToMatrix(ref poseData, true);

            UpdateObjectTransform(armMatrix);
        }

        /// <summary>
        /// Update AR object transform from transformation matrix
        /// </summary>
        /// <param name="armMatrix">Transformation matrix in camera space</param>
        public void UpdateObjectTransform(Matrix4x4 armMatrix)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("ARObjectManager: Not initialized");
                return;
            }

            // Transform the matrix from camera space to world space using the ARFoundation camera's transform
            Matrix4x4 worldMatrix = xrOrigin.Camera.transform.localToWorldMatrix * armMatrix;

            // Apply transformation matrices if available
            if (fitARFoundationBackgroundMatrix != Matrix4x4.zero)
            {
                worldMatrix = fitARFoundationBackgroundMatrix * worldMatrix;
            }

            if (fitHelpersFlipMatrix != Matrix4x4.zero)
            {
                worldMatrix = fitHelpersFlipMatrix * worldMatrix;
            }

            // Apply the transformation
            ApplyDirectTransform(worldMatrix);

            hasValidPose = true;
            lastValidMatrix = worldMatrix;
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
            HideAllObjects();

            // Update or create objects for each detected marker
            for (int i = 0; i < poseDataList.Count; i++)
            {
                int markerId = markerIds[i];
                PoseData poseData = poseDataList[i];

                // Get or create object for this marker
                GameObject markerObject = GetOrCreateMarkerObject(markerId);

                if (markerObject != null)
                {
                    // Show and update the object
                    markerObject.SetActive(true);
                    UpdateObjectTransform(markerObject, poseData);
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

        /// <summary>
        /// Update transform for a specific object
        /// </summary>
        /// <param name="obj">GameObject to update</param>
        /// <param name="poseData">Pose data</param>
        private void UpdateObjectTransform(GameObject obj, PoseData poseData)
        {
            if (obj == null) return;

            // Convert pose data to transformation matrix
            Matrix4x4 armMatrix = OpenCVARUtils.ConvertPoseDataToMatrix(ref poseData, true);

            // Transform to world space
            Matrix4x4 worldMatrix = xrOrigin.Camera.transform.localToWorldMatrix * armMatrix;

            // Apply transformation
            OpenCVARUtils.SetTransformFromMatrix(obj.transform, ref worldMatrix);
        }

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
