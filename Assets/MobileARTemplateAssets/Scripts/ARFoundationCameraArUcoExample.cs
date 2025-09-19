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
        [Header("ARObject Manager")]
        [SerializeField]
        public ARObjectManager arObjectManager;


        [Header("Performance")]
        [SerializeField]
        public int detectFramesEvery = 30; // تم تغييرها من 15 إلى 30

        private int frameCount = 0;

        [Header("Processing Optimization")]
        [SerializeField]
        private float processingScale = 0.5f; // معالجة 50% من الدقة
        private Mat scaledMat;

        [Header("Auto Performance")]
        [SerializeField]
        private bool autoOptimizePerformance = true;
        private float lastPerformanceCheck = 0f;
        private int consecutiveLowFPS = 0;

        /// <summary>
        /// The dictionary identifier.
        /// </summary>
        public ArUcoDictionary dictionaryId = ArUcoDictionary.DICT_6X6_1000;

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



        private void ConfigurePerformance()
        {
            int ram = SystemInfo.systemMemorySize; // بالميغابايت
            int vram = SystemInfo.graphicsMemorySize; // GPU memory MB
            string deviceModel = SystemInfo.deviceModel;
            string gpu = SystemInfo.graphicsDeviceName;

            Debug.Log($"Device: {deviceModel}, RAM={ram}MB, VRAM={vram}MB, GPU={gpu}");

            if (ram >= 6000 && vram >= 2000) // أجهزة قوية (6GB RAM وفوق + GPU جيد)
            {
                webCamTextureToMatHelper.RequestedFPS = 60f;
                detectFramesEvery = 1; // كل فريم
                processingScale = 1.0f; // دقة كاملة
                Debug.Log("Performance Mode: HIGH (60fps, full resolution)");
            }
            else if (ram >= 4000) // أجهزة متوسطة
            {
                webCamTextureToMatHelper.RequestedFPS = 30f;
                detectFramesEvery = 2; // كل فريمين
                processingScale = 0.75f; // 75% من الدقة
                Debug.Log("Performance Mode: MEDIUM (30fps, medium resolution)");
            }
            else // أجهزة ضعيفة
            {
                webCamTextureToMatHelper.RequestedFPS = 20f;
                detectFramesEvery = 3; // كل 3 فريمات
                processingScale = 0.5f; // 50% من الدقة
                Debug.Log("Performance Mode: LOW (20fps, half resolution)");
            }
        }

        // Performance optimization for ProcessFrame


#if ENABLE_INPUT_SYSTEM
        private InputAction forceProcessAction;
#endif

        // Use this for initialization
        void Start()
        {
            // Hide the original AR object immediately
            if (arGameObject != null && arGameObject.gameObject != null)
            {
                arGameObject.gameObject.SetActive(false);
                Debug.Log("ARFoundationCameraArUcoExample: Original AR object hidden at startup");
            }

            webCamTextureToMatHelper = gameObject.GetComponent<ARFoundationCamera2MatHelper>();

            if (webCamTextureToMatHelper != null)
            {
                webCamTextureToMatHelper.RequestedFPS = 60f;
                webCamTextureToMatHelper.OutputColorFormat = Source2MatHelperColorFormat.RGBA;
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR && !DISABLE_ARFOUNDATION_API
                webCamTextureToMatHelper.FrameMatAcquired += OnFrameMatAcquired;
#endif
                webCamTextureToMatHelper.Initialize();
            }
            else
            {
                Debug.LogError("ARFoundationCamera2MatHelper component not found!");
            }

            InitializeManagers();

            ConfigurePerformance();

#if ENABLE_INPUT_SYSTEM
            forceProcessAction = new InputAction("ForceProcess", InputActionType.Button, "<Keyboard>/space");
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

            // Set performance optimization settings
            if (arObjectManager != null)
            {
                // arObjectManager.FrameSkipCount = detectFramesEvery;
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
            // Auto performance optimization
            if (autoOptimizePerformance && Time.time - lastPerformanceCheck > 2f)
            {
                OptimizePerformanceIfNeeded();
                lastPerformanceCheck = Time.time;
            }

            if (webCamTextureToMatHelper != null && webCamTextureToMatHelper.IsPlaying() && webCamTextureToMatHelper.DidUpdateThisFrame())
            {
                Mat rgbaMat = webCamTextureToMatHelper.GetMat();
                ProcessFrame(rgbaMat);
            }
        }

#endif // (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR && !DISABLE_ARFOUNDATION_API

        private void OptimizePerformanceIfNeeded()
        {
            float currentFPS = 1f / Time.deltaTime;

            if (currentFPS < 20f) // إذا FPS أقل من 20
            {
                consecutiveLowFPS++;

                if (consecutiveLowFPS >= 3) // 3 مرات متتالية
                {
                    // تحسين تلقائي
                    detectFramesEvery = Mathf.Min(detectFramesEvery + 2, 10);
                    webCamTextureToMatHelper.RequestedFPS = 20f;

                    Debug.Log($"Auto-optimized: detectFramesEvery={detectFramesEvery}, FPS=20");
                    consecutiveLowFPS = 0;
                }
            }
            else
            {
                consecutiveLowFPS = 0;
            }
        }

        /// <summary>
        /// Process camera frame using the new manager system with performance optimization
        /// </summary>
        /// <param name="rgbaMat">Input RGBA frame</param>
        private void ProcessFrame(Mat rgbaMat)
        {
            frameCount++;
            if (frameCount % detectFramesEvery != 0) return;

            if (rgbaMat == null || rgbMat == null) return;

            // تحويل واحد فقط RGBA → RGB
            Imgproc.cvtColor(rgbaMat, rgbMat, Imgproc.COLOR_RGBA2RGB);

            // تعطيل undistortion للأجهزة الضعيفة
            bool enableUndistortion = SystemInfo.systemMemorySize > 3000; // فقط للأجهزة > 3GB RAM

            if (enableUndistortion && cameraParametersManager?.AreParametersValid() == true)
            {
                OpenCVForUnity.Calib3dModule.Calib3d.undistort(rgbMat, rgbMat,
                    cameraParametersManager.CameraMatrix,
                    cameraParametersManager.DistortionCoeffs);
            }

            Debug.Log($"Processing frame for ArUco detection... Dictionary: {dictionaryId}");
            Debug.Log($"Apply pose estimation: {applyEstimationPose}");

            Debug.Log($"rgbMat status: rows={rgbMat.rows()}, cols={rgbMat.cols()}, empty={rgbMat.empty()}");
            Debug.Log($"arucoDetectionManager null? {arucoDetectionManager == null}");

            // ArUco detection
            if (arucoDetectionManager != null)
            {
                bool markersDetected = arucoDetectionManager.DetectMarkers(rgbMat);
                Debug.Log($"DetectMarkers returned: {markersDetected}");

                if (markersDetected && applyEstimationPose && poseEstimationManager != null && cameraParametersManager != null)
                {
                    var poseDataList = poseEstimationManager.EstimateMarkerspose(
                        arucoDetectionManager, cameraParametersManager, rgbMat);

                    if (poseDataList.Count > 0 && arObjectManager != null)
                    {
                        List<int> markerIds = new List<int>();
                        int markerCount = arucoDetectionManager.GetDetectedMarkerCount();
                        for (int i = 0; i < markerCount; i++)
                        {
                            markerIds.Add(arucoDetectionManager.GetMarkerId(i));
                        }

                        arObjectManager.HideObject();
                        arObjectManager.UpdateMultipleObjects(poseDataList, markerIds, true);
                        // 👇 هنا ممكن تنادي SnapToPlane بعد ما تحط الـ object

                    }
                    else if (arObjectManager != null)
                    {
                        arObjectManager.HideAllObjects();
                    }
                }
                else if (arObjectManager != null)
                {
                    arObjectManager.HideObject();
                }
            }
            else
            {
                Debug.LogError("arucoDetectionManager is null!");
            }

            // تحويل مرة واحدة في النهاية
            Imgproc.cvtColor(rgbMat, rgbaMat, Imgproc.COLOR_RGB2RGBA);
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
                forceProcessAction.Dispose();
            }
#endif
        }

        #region Public Button Event Handlers (called from UI)


        #endregion

#if ENABLE_INPUT_SYSTEM

#endif
    }
}

#endif
