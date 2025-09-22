#if !(PLATFORM_LUMIN && !UNITY_EDITOR)

using ARFoundationWithOpenCVForUnity.UnityIntegration.Helper.Source2Mat;
using OpenCVForUnity.CoreModule;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ARFoundationWithOpenCVForUnityExample
{
    /// <summary>
    /// Manager for camera parameters and calibration
    /// Handles camera matrix calculation and distortion coefficients
    /// </summary>
    public class CameraParametersManager
    {
        #region Private Fields

        private Mat cameraMatrix;
        private MatOfDouble distortionCoeffs;
        private bool isInitialized = false;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the camera matrix
        /// </summary>
        public Mat CameraMatrix => cameraMatrix;

        /// <summary>
        /// Gets the distortion coefficients
        /// </summary>
        public MatOfDouble DistortionCoeffs => distortionCoeffs;

        /// <summary>
        /// Returns true if camera parameters are initialized
        /// </summary>
        public bool IsInitialized => isInitialized;

        /// <summary>
        /// Gets the focal length X
        /// </summary>
        public double FocalLengthX { get; private set; }

        /// <summary>
        /// Gets the focal length Y
        /// </summary>
        public double FocalLengthY { get; private set; }

        /// <summary>
        /// Gets the principal point X
        /// </summary>
        public double PrincipalPointX { get; private set; }

        /// <summary>
        /// Gets the principal point Y
        /// </summary>
        public double PrincipalPointY { get; private set; }

        #endregion

        #region Constructor & Initialization

        /// <summary>
        /// Initialize camera parameters manager
        /// </summary>
        public CameraParametersManager()
        {
            // Constructor - initialization will be done through specific methods
        }

        #endregion

        #region Camera Parameters Setup

        /// <summary>
        /// Initialize camera parameters from ARFoundation camera intrinsics
        /// </summary>
        /// <param name="cameraHelper">The camera helper instance</param>
        /// <returns>True if initialization was successful</returns>
        public bool InitializeFromARFoundation(ARFoundationCamera2MatHelper cameraHelper)
        {
            if (cameraHelper == null)
            {
                Debug.LogError("CameraParametersManager: Camera helper is null");
                return false;
            }

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR && !DISABLE_ARFOUNDATION_API
            
            try
            {
                XRCameraIntrinsics cameraIntrinsics = cameraHelper.GetCameraIntrinsics();
                
                // Apply the rotate and flip properties of camera helper to the camera intrinsics
                Vector2 fl = cameraIntrinsics.focalLength;
                Vector2 pp = cameraIntrinsics.principalPoint;
                Vector2Int r = cameraIntrinsics.resolution;

                Matrix4x4 tM = Matrix4x4.Translate(new Vector3(-r.x / 2, -r.y / 2, 0));
                pp = tM.MultiplyPoint3x4(pp);

                Matrix4x4 rotationAndFlipM = Matrix4x4.TRS(Vector3.zero, 
                    Quaternion.Euler(0, 0, cameraHelper.Rotate90Degree ? 90 : 0),
                    new Vector3(cameraHelper.FlipHorizontal ? -1 : 1, cameraHelper.FlipVertical ? -1 : 1, 1));
                pp = rotationAndFlipM.MultiplyPoint3x4(pp);

                if (cameraHelper.Rotate90Degree)
                {
                    fl = new Vector2(fl.y, fl.x);
                    r = new Vector2Int(r.y, r.x);
                }

                Matrix4x4 _tM = Matrix4x4.Translate(new Vector3(r.x / 2, r.y / 2, 0));
                pp = _tM.MultiplyPoint3x4(pp);

                cameraIntrinsics = new XRCameraIntrinsics(fl, pp, r);

                FocalLengthX = cameraIntrinsics.focalLength.x;
                FocalLengthY = cameraIntrinsics.focalLength.y;
                PrincipalPointX = cameraIntrinsics.principalPoint.x;
                PrincipalPointY = cameraIntrinsics.principalPoint.y;

                CreateCameraMatrix(FocalLengthX, FocalLengthY, PrincipalPointX, PrincipalPointY);
                CreateDistortionCoeffs();

                Debug.Log("CameraParametersManager: Created camera parameters from ARFoundation intrinsics");
                Debug.Log($"Focal Length: {FocalLengthX}x{FocalLengthY}, Principal Point: {PrincipalPointX}x{PrincipalPointY}");
                
                isInitialized = true;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"CameraParametersManager: Error initializing from ARFoundation: {e.Message}");
                return InitializeDummyParameters(cameraHelper);
            }
            
#else
            return InitializeDummyParameters(cameraHelper);
#endif
        }

        /// <summary>
        /// Initialize dummy camera parameters for editor/unsupported platforms
        /// </summary>
        /// <param name="cameraHelper">The camera helper instance</param>
        /// <returns>True if initialization was successful</returns>
        public bool InitializeDummyParameters(ARFoundationCamera2MatHelper cameraHelper)
        {
            if (cameraHelper == null)
            {
                Debug.LogError("CameraParametersManager: Camera helper is null");
                return false;
            }

            Mat rgbaMat = cameraHelper.GetMat();
            if (rgbaMat == null)
            {
                Debug.LogError("CameraParametersManager: Could not get mat from camera helper");
                return false;
            }

            float width = rgbaMat.width();
            float height = rgbaMat.height();

            int max_d = (int)Mathf.Max(width, height);
            FocalLengthX = max_d;
            FocalLengthY = max_d;
            PrincipalPointX = width / 2.0f;
            PrincipalPointY = height / 2.0f;

            CreateCameraMatrix(FocalLengthX, FocalLengthY, PrincipalPointX, PrincipalPointY);
            CreateDistortionCoeffs();

            Debug.Log("CameraParametersManager: Created dummy camera parameters");
            Debug.Log($"Image size: {width}x{height}, Focal length: {FocalLengthX}, Principal point: {PrincipalPointX}x{PrincipalPointY}");

            isInitialized = true;
            return true;
        }

        /// <summary>
        /// Initialize camera parameters with custom values
        /// </summary>
        /// <param name="fx">Focal length X</param>
        /// <param name="fy">Focal length Y</param>
        /// <param name="cx">Principal point X</param>
        /// <param name="cy">Principal point Y</param>
        /// <returns>True if initialization was successful</returns>
        public bool InitializeCustomParameters(double fx, double fy, double cx, double cy)
        {
            FocalLengthX = fx;
            FocalLengthY = fy;
            PrincipalPointX = cx;
            PrincipalPointY = cy;

            CreateCameraMatrix(fx, fy, cx, cy);
            CreateDistortionCoeffs();

            Debug.Log($"CameraParametersManager: Initialized with custom parameters - fx:{fx}, fy:{fy}, cx:{cx}, cy:{cy}");

            isInitialized = true;
            return true;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Create the camera matrix
        /// </summary>
        /// <param name="fx">Focal length X</param>
        /// <param name="fy">Focal length Y</param>
        /// <param name="cx">Principal point X</param>
        /// <param name="cy">Principal point Y</param>
        private void CreateCameraMatrix(double fx, double fy, double cx, double cy)
        {
            // Dispose existing matrix
            cameraMatrix?.Dispose();

            // Create new camera matrix
            cameraMatrix = new Mat(3, 3, CvType.CV_64FC1);
            cameraMatrix.put(0, 0, fx);
            cameraMatrix.put(0, 1, 0);
            cameraMatrix.put(0, 2, cx);
            cameraMatrix.put(1, 0, 0);
            cameraMatrix.put(1, 1, fy);
            cameraMatrix.put(1, 2, cy);
            cameraMatrix.put(2, 0, 0);
            cameraMatrix.put(2, 1, 0);
            cameraMatrix.put(2, 2, 1.0f);
        }

        /// <summary>
        /// Create distortion coefficients (assuming no distortion)
        /// </summary>
        private void CreateDistortionCoeffs()
        {
            // Dispose existing coefficients
            distortionCoeffs?.Dispose();

            // Create new distortion coefficients (no distortion)
            distortionCoeffs = new MatOfDouble(0, 0, 0, 0);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get camera parameters as string for debugging
        /// </summary>
        /// <returns>String representation of camera parameters</returns>
        public string GetParametersString()
        {
            if (!isInitialized)
                return "Camera parameters not initialized";

            return $"Camera Matrix:\n{cameraMatrix?.dump()}\nDistortion Coeffs:\n{distortionCoeffs?.dump()}";
        }

        /// <summary>
        /// Check if camera parameters are valid
        /// </summary>
        /// <returns>True if parameters are valid</returns>
        public bool AreParametersValid()
        {
            return isInitialized &&
                   cameraMatrix != null &&
                   distortionCoeffs != null &&
                   FocalLengthX > 0 &&
                   FocalLengthY > 0;
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Dispose all resources
        /// </summary>
        public void Dispose()
        {
            cameraMatrix?.Dispose();
            cameraMatrix = null;

            distortionCoeffs?.Dispose();
            distortionCoeffs = null;

            isInitialized = false;

            Debug.Log("CameraParametersManager: Resources disposed");
        }

        #endregion
    }
}

#endif