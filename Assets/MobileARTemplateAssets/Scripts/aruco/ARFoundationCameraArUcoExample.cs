#if !(PLATFORM_LUMIN && !UNITY_EDITOR)

using ARFoundationWithOpenCVForUnity.UnityIntegration.Helper.Source2Mat;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityIntegration;
using OpenCVForUnity.UnityIntegration.Helper.Source2Mat;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using static OpenCVForUnity.UnityIntegration.OpenCVARUtils;
using OpenCVForUnity.UnityIntegration.Helper.AR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ARFoundationWithOpenCVForUnityExample
{
    /// <summary>
    /// ARFoundationCamera ArUco Example
    /// An example of ArUco marker detection from an ARFoundation camera image.
    /// Refactored to use modular managers for better code organization.
    /// </summary>
    [RequireComponent(typeof(ARFoundationCamera2MatHelper))]
    public class ARFoundationCameraArUcoExample : MonoBehaviour
    {

        /// <summary>
        /// The dictionary identifier.
        /// </summary>
        public ArUcoDictionary dictionaryId = ArUcoDictionary.DICT_6X6_250;

        /// <summary>
        /// Determines if applied the pose estimation.
        /// </summary>
        public bool applyEstimationPose = true;


        [Space(10)]

        /// <summary>
        /// The length of the markers' side. Normally, unit is meters.
        /// </summary>
        public float markerLength = 0.188f;

        /// <summary>
        /// The AR game object.
        /// </summary>
        public ARGameObject arGameObject;

        /// <summary>
        /// The XR Origin.
        /// </summary>
        public XROrigin xROrigin;

        [Space(10)]

        /// <summary>
        /// Determines if enable leap filter.
        /// </summary>
        public bool enableLerpFilter;

        [Header("Performance")]
        /// <summary>
        /// Frame skip count for performance optimization (update every N frames)
        /// </summary>
        [Range(1, 60)]
        public int frameSkipCount = 3;

        /// <summary>
        /// The texture.
        /// </summary>
        Texture2D texture;

        /// <summary>
        /// The webcam texture to mat helper.
        /// </summary>
        ARFoundationCamera2MatHelper webCamTextureToMatHelper;
        /// <summary>
        /// The rgb mat.
        /// </summary>
        Mat rgbMat;

        // Manager instances for modular functionality
        private ArUcoDetectionManager arucoDetectionManager;
        private CameraParametersManager cameraParametersManager;
        private PoseEstimationManager poseEstimationManager;
        private ARObjectManager arObjectManager;

        // Performance optimization for ProcessFrame
        private int currentFrameCount = 0;

#if ENABLE_INPUT_SYSTEM
        private InputAction forceProcessAction;
#endif

        // Use this for initialization
        void Start()
        {

            // Hide the original AR object immediately to prevent it from showing at startup
            if (arGameObject != null && arGameObject.gameObject != null)
            {
                arGameObject.gameObject.SetActive(false);
                Debug.Log("ARFoundationCameraArUcoExample: Original AR object hidden at startup");
            }

            webCamTextureToMatHelper = gameObject.GetComponent<ARFoundationCamera2MatHelper>();
            webCamTextureToMatHelper.OutputColorFormat = Source2MatHelperColorFormat.RGBA;
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR && !DISABLE_ARFOUNDATION_API
            webCamTextureToMatHelper.FrameMatAcquired += OnFrameMatAcquired;
#endif // (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR && !DISABLE_ARFOUNDATION_API
            webCamTextureToMatHelper.Initialize();


            // Initialize managers
            InitializeManagers();

#if ENABLE_INPUT_SYSTEM
            // إعداد Input Action للاختبار
            forceProcessAction = new InputAction("ForceProcess", InputActionType.Button, "<Keyboard>/space");
            forceProcessAction.performed += OnForceProcessPerformed;
            forceProcessAction.Enable();
#endif
        }

        /// <summary>
        /// Initialize all manager instances
        /// </summary>
        private void InitializeManagers()
        {
            Debug.Log("Initializing managers...");

            // Initialize ArUco detection manager
            Debug.Log($"Creating ArUcoDetectionManager with dictionary: {dictionaryId}");
            arucoDetectionManager = new ArUcoDetectionManager(dictionaryId);
            Debug.Log($"ArUcoDetectionManager initialized: {arucoDetectionManager != null}");

            // Initialize camera parameters manager
            cameraParametersManager = new CameraParametersManager();
            Debug.Log($"CameraParametersManager initialized: {cameraParametersManager != null}");

            // Initialize pose estimation manager
            poseEstimationManager = new PoseEstimationManager(markerLength);
            Debug.Log($"PoseEstimationManager initialized: {poseEstimationManager != null}");

            // Initialize AR object manager
            arObjectManager = new ARObjectManager(arGameObject, xROrigin, enableLerpFilter);
            Debug.Log($"ARObjectManager initialized: {arObjectManager != null}");

            // Set performance optimization settings
            if (arObjectManager != null)
            {
                arObjectManager.FrameSkipCount = frameSkipCount;
                arObjectManager.HideObject();
            }
        }

        /// <summary>
        /// Setup UI event handlers
        /// </summary>
        private void SetupUIEventHandlers()
        {
            // UI components removed - no event handlers needed
        }

        /// <summary>
        /// Raises the webcam texture to mat helper initialized event.
        /// </summary>
        public void OnWebCamTextureToMatHelperInitialized()
        {
            Debug.Log("OnWebCamTextureToMatHelperInitialized");

            // التأكد من أن الكاميرا تعمل
            Debug.Log($"Camera playing: {webCamTextureToMatHelper.IsPlaying()}");
            Debug.Log($"Frame skip count: {frameSkipCount}");

            // Ensure original AR object stays hidden during camera initialization
            if (arObjectManager != null)
            {
                arObjectManager.HideObject();
            }

            Mat rgbaMat = webCamTextureToMatHelper.GetMat();

            texture = new Texture2D(rgbaMat.cols(), rgbaMat.rows(), TextureFormat.RGBA32, false);
            OpenCVMatUtils.MatToTexture2D(rgbaMat, texture);


            Debug.Log("Screen.width " + Screen.width + " Screen.height " + Screen.height + " Screen.orientation " + Screen.orientation);

            // Update UI with camera information - removed UI dependency
            Debug.Log($"Camera info: {rgbaMat.width()}x{rgbaMat.height()}, orientation: {Screen.orientation}");
            Debug.Log($"Front facing: {webCamTextureToMatHelper.IsFrontFacing()}");

            webCamTextureToMatHelper.FlipHorizontal = webCamTextureToMatHelper.IsFrontFacing();

            // Initialize camera parameters using the manager
            bool cameraInitialized = false;
            if (cameraParametersManager != null)
            {
                cameraInitialized = cameraParametersManager.InitializeFromARFoundation(webCamTextureToMatHelper);
            }

            if (!cameraInitialized)
            {
                Debug.LogError("Failed to initialize camera parameters");
                return;
            }

            // Log camera parameters for debugging
            if (cameraParametersManager.AreParametersValid())
            {
                Debug.Log("Camera parameters initialized successfully");
                Debug.Log(cameraParametersManager.GetParametersString());

                // Update UI with camera intrinsics if available - removed UI dependency
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR && !DISABLE_ARFOUNDATION_API
                var cameraIntrinsics = webCamTextureToMatHelper.GetCameraIntrinsics();
                Debug.Log($"Camera intrinsics: focal={cameraIntrinsics.focalLength}, principal={cameraIntrinsics.principalPoint}");
#endif
            }

            // Initialize RGB matrix
            rgbMat = new Mat(rgbaMat.rows(), rgbaMat.cols(), CvType.CV_8UC3);

            // Setup AR object manager transformation matrices
            if (arObjectManager != null)
            {
                arObjectManager.SetupTransformationMatrices(webCamTextureToMatHelper);
            }

            // Re-initialize ArUco detection manager to ensure it's properly set up
            Debug.Log("Re-initializing ArUcoDetectionManager after camera initialization...");
            if (arucoDetectionManager != null)
            {
                arucoDetectionManager.Dispose();
            }
            arucoDetectionManager = new ArUcoDetectionManager(dictionaryId);
            Debug.Log($"ArUcoDetectionManager re-initialized: {arucoDetectionManager != null}");
        }

        /// <summary>
        /// Raises the webcam texture to mat helper disposed event.
        /// </summary>
        public void OnWebCamTextureToMatHelperDisposed()
        {
            Debug.Log("OnWebCamTextureToMatHelperDisposed");

            if (rgbMat != null)
            {
                rgbMat.Dispose();
                rgbMat = null;
            }

            if (texture != null)
            {
                Texture2D.Destroy(texture);
                texture = null;
            }

            // Dispose managers
            arucoDetectionManager?.Dispose();
            cameraParametersManager?.Dispose();
            poseEstimationManager?.Dispose();
            arObjectManager?.Dispose();
            // uiControlsManager removed
        }

        /// <summary>
        /// Raises the webcam texture to mat helper error occurred event.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        /// <param name="message">Message.</param>
        public void OnWebCamTextureToMatHelperErrorOccurred(Source2MatHelperErrorCode errorCode, string message)
        {
            Debug.Log("OnWebCamTextureToMatHelperErrorOccurred " + errorCode + ":" + message);
        }

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR && !DISABLE_ARFOUNDATION_API

        void OnFrameMatAcquired(Mat mat, Matrix4x4 projectionMatrix, Matrix4x4 cameraToWorldMatrix, XRCameraIntrinsics cameraIntrinsics, long timestamp)
        {
            ProcessFrame(mat);
        }

#else // (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR && !DISABLE_ARFOUNDATION_API

        // Update is called once per frame
        void Update()
        {
            if (webCamTextureToMatHelper.IsPlaying() && webCamTextureToMatHelper.DidUpdateThisFrame())
            {
                Mat rgbaMat = webCamTextureToMatHelper.GetMat();
                ProcessFrame(rgbaMat);
            }
        }

#endif // (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR && !DISABLE_ARFOUNDATION_API

        /// <summary>
        /// Process camera frame using the new manager system with performance optimization
        /// </summary>
        /// <param name="rgbaMat">Input RGBA frame</param>
        private void ProcessFrame(Mat rgbaMat)
        {
            if (rgbaMat == null || rgbMat == null) return;

            // Performance optimization - check if we should process this frame
            currentFrameCount++;
            bool shouldProcess = currentFrameCount >= frameSkipCount;

            // Debug log للتأكد من استدعاء الدالة
            if (currentFrameCount % 30 == 0) // كل 30 إطار
            {
                Debug.Log($"ProcessFrame called - Frame: {currentFrameCount}, ShouldProcess: {shouldProcess}");
                Debug.Log($"Image size: {rgbaMat.cols()}x{rgbaMat.rows()}");
            }

            // Always update the display texture, but skip heavy processing if not needed
            if (!shouldProcess)
            {
                // Just update the texture without processing
                Imgproc.cvtColor(rgbaMat, rgbMat, Imgproc.COLOR_RGBA2RGB);
                Imgproc.cvtColor(rgbMat, rgbaMat, Imgproc.COLOR_RGB2RGBA);
                OpenCVMatUtils.MatToTexture2D(rgbaMat, texture);
                return;
            }

            // Reset frame counter for heavy processing
            currentFrameCount = 0;
            Debug.Log($"Processing frame for ArUco detection... Dictionary: {dictionaryId}");
            Debug.Log($"Apply pose estimation: {applyEstimationPose}");

            // Convert RGBA to RGB
            Imgproc.cvtColor(rgbaMat, rgbMat, Imgproc.COLOR_RGBA2RGB);

            // Debug: Check mat status
            Debug.Log($"rgbMat status: rows={rgbMat.rows()}, cols={rgbMat.cols()}, empty={rgbMat.empty()}");
            Debug.Log($"arucoDetectionManager null? {arucoDetectionManager == null}");

            // Undistort image using camera parameters
            if (cameraParametersManager != null && cameraParametersManager.AreParametersValid())
            {
                OpenCVForUnity.Calib3dModule.Calib3d.undistort(rgbMat, rgbMat,
                    cameraParametersManager.CameraMatrix,
                    cameraParametersManager.DistortionCoeffs);
                Debug.Log("Image undistorted successfully");
            }
            else
            {
                Debug.Log("Skipping undistortion - camera parameters not valid");
            }

            // Detect ArUco markers
            if (arucoDetectionManager != null)
            {
                Debug.Log("About to call DetectMarkers...");
                bool markersDetected = arucoDetectionManager.DetectMarkers(rgbMat);
                Debug.Log($"DetectMarkers returned: {markersDetected}");

                if (markersDetected)
                {
                    Debug.Log("Markers detected! Drawing them...");
                    // Draw detected markers
                    // arucoDetectionManager.DrawDetectedMarkers(rgbMat);

                    // Estimate pose if enabled
                    if (applyEstimationPose && poseEstimationManager != null && cameraParametersManager != null)
                    {
                        var poseDataList = poseEstimationManager.EstimateMarkerspose(
                            arucoDetectionManager, cameraParametersManager, rgbMat);

                        // Update AR objects for ALL detected markers
                        if (poseDataList.Count > 0 && arObjectManager != null)
                        {
                            // Get marker IDs
                            List<int> markerIds = new List<int>();
                            int markerCount = arucoDetectionManager.GetDetectedMarkerCount();
                            for (int i = 0; i < markerCount; i++)
                            {
                                markerIds.Add(arucoDetectionManager.GetMarkerId(i));
                            }

                            // Hide the original object to avoid duplication
                            arObjectManager.HideObject();

                            // Update multiple objects (force update since we already checked frame rate)
                            arObjectManager.UpdateMultipleObjects(poseDataList, markerIds, true);
                        }
                        else if (arObjectManager != null)
                        {
                            // Hide all objects if pose estimation failed
                            arObjectManager.HideAllObjects();
                        }
                    }
                    else if (arObjectManager != null)
                    {
                        // Hide the original object when not using pose estimation
                        arObjectManager.HideObject();
                        // Note: Multiple objects system doesn't work without pose estimation
                    }
                }
                else
                {
                    Debug.Log("No markers detected");
                    if (arObjectManager != null)
                    {
                        // Hide ALL AR objects if no markers detected
                        arObjectManager.HideAllObjects();
                    }
                }
            }
            else
            {
                Debug.LogError("arucoDetectionManager is null!");
            }

            // Convert back to RGBA
            Imgproc.cvtColor(rgbMat, rgbaMat, Imgproc.COLOR_RGB2RGBA);

            // Update texture
            OpenCVMatUtils.MatToTexture2D(rgbaMat, texture);
        }

        #region UI Event Handlers

        /// <summary>
        /// Handle dictionary change event
        /// </summary>
        /// <param name="newDictionary">New dictionary to use</param>
        private void OnDictionaryChanged(ArUcoDictionary newDictionary)
        {
            if (dictionaryId != newDictionary)
            {
                dictionaryId = newDictionary;

                // Update ArUco detection manager
                if (arucoDetectionManager != null)
                {
                    arucoDetectionManager.ChangeDictionary(newDictionary);
                }

                // Reset AR object transform
                if (arObjectManager != null)
                {
                    arObjectManager.ResetObjectTransform();
                }

                // Reinitialize camera helper if needed
                if (webCamTextureToMatHelper != null && webCamTextureToMatHelper.IsInitialized())
                {
                    webCamTextureToMatHelper.Initialize();
                }
            }
        }

        /// <summary>
        /// Handle pose estimation toggle event
        /// </summary>
        /// <param name="enabled">Whether pose estimation is enabled</param>
        private void OnPoseEstimationToggled(bool enabled)
        {
            applyEstimationPose = enabled;
        }

        /// <summary>
        /// Handle lerp filter toggle event
        /// </summary>
        /// <param name="enabled">Whether lerp filter is enabled</param>
        private void OnLerpFilterToggled(bool enabled)
        {
            enableLerpFilter = enabled;

            // Update AR object manager
            if (arObjectManager != null)
            {
                arObjectManager.EnableLerpFilter = enabled;
            }
        }

        /// <summary>
        /// Update frame skip count for performance optimization
        /// </summary>
        /// <param name="newFrameSkipCount">New frame skip count</param>
        public void UpdateFrameSkipCount(int newFrameSkipCount)
        {
            frameSkipCount = Mathf.Clamp(newFrameSkipCount, 1, 60);

            if (arObjectManager != null)
            {
                arObjectManager.FrameSkipCount = frameSkipCount;
            }

            // Reset frame counter to apply new setting immediately
            currentFrameCount = 0;

            Debug.Log($"Frame skip count updated to: {frameSkipCount}");
        }

        /// <summary>
        /// Force immediate processing of next frame
        /// </summary>
        public void ForceNextFrameProcessing()
        {
            currentFrameCount = frameSkipCount; // This will trigger processing on next frame
        }

        /// <summary>
        /// Get current frame processing status
        /// </summary>
        /// <returns>Information about frame processing</returns>
        public string GetFrameProcessingStatus()
        {
            return $"Frame: {currentFrameCount}/{frameSkipCount} - Next process in: {frameSkipCount - currentFrameCount} frames";
        }

        /// <summary>
        /// Handle camera facing change request
        /// </summary>
        private void OnCameraFacingChangeRequested()
        {
            if (webCamTextureToMatHelper != null)
            {
                webCamTextureToMatHelper.RequestedIsFrontFacing = !webCamTextureToMatHelper.RequestedIsFrontFacing;
            }
        }

        /// <summary>
        /// Handle light estimation change request
        /// </summary>
        private void OnLightEstimationChangeRequested()
        {
            if (webCamTextureToMatHelper != null && webCamTextureToMatHelper.IsInitialized())
            {
                webCamTextureToMatHelper.RequestedLightEstimation =
                    (webCamTextureToMatHelper.RequestedLightEstimation == LightEstimation.None)
                        ? LightEstimation.AmbientColor | LightEstimation.AmbientIntensity
                        : LightEstimation.None;
            }
        }

        /// <summary>
        /// Handle play request
        /// </summary>
        private void OnPlayRequested()
        {
            if (webCamTextureToMatHelper != null)
            {
                webCamTextureToMatHelper.Play();
            }
        }

        /// <summary>
        /// Handle pause request
        /// </summary>
        private void OnPauseRequested()
        {
            if (webCamTextureToMatHelper != null)
            {
                webCamTextureToMatHelper.Pause();
            }
        }

        /// <summary>
        /// Handle stop request
        /// </summary>
        private void OnStopRequested()
        {
            if (webCamTextureToMatHelper != null)
            {
                webCamTextureToMatHelper.Stop();
            }
        }

        #endregion

        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        void OnDestroy()
        {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR && !DISABLE_ARFOUNDATION_API
            webCamTextureToMatHelper.FrameMatAcquired -= OnFrameMatAcquired;
#endif // (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR && !DISABLE_ARFOUNDATION_API
            webCamTextureToMatHelper.Dispose();

#if ENABLE_INPUT_SYSTEM
            if (forceProcessAction != null)
            {
                forceProcessAction.performed -= OnForceProcessPerformed;
                forceProcessAction.Dispose();
            }
#endif
        }

        #region Public Button Event Handlers (called from UI)


        #endregion

#if ENABLE_INPUT_SYSTEM
        private void OnForceProcessPerformed(InputAction.CallbackContext context)
        {
            ForceNextFrameProcessing();
            Debug.Log("Forced next frame processing");
        }
#endif
    }
}

#endif
