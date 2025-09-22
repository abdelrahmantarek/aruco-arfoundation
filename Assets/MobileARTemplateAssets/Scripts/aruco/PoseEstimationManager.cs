#if !(PLATFORM_LUMIN && !UNITY_EDITOR)

using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityIntegration;
using System.Collections.Generic;
using UnityEngine;
using static OpenCVForUnity.UnityIntegration.OpenCVARUtils;

namespace ARFoundationWithOpenCVForUnityExample
{
  /// <summary>
  /// Manager for pose estimation functionality
  /// Handles marker pose calculation and coordinate transformations
  /// </summary>
  public class PoseEstimationManager
  {
    #region Private Fields

    private float markerLength;
    private MatOfPoint3f objectPoints;
    private Mat rvecs;
    private Mat tvecs;
    private bool isInitialized = false;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the marker length in world units (usually meters)
    /// </summary>
    public float MarkerLength
    {
      get => markerLength;
      set
      {
        if (markerLength != value)
        {
          markerLength = value;
          UpdateObjectPoints();
        }
      }
    }

    /// <summary>
    /// Returns true if the pose estimation manager is initialized
    /// </summary>
    public bool IsInitialized => isInitialized;

    #endregion

    #region Constructor & Initialization

    /// <summary>
    /// Initialize pose estimation manager
    /// </summary>
    /// <param name="markerLength">Length of the marker side in world units</param>
    public PoseEstimationManager(float markerLength = 0.188f)
    {
      Initialize(markerLength);
    }

    /// <summary>
    /// Initialize the pose estimation system
    /// </summary>
    /// <param name="markerLength">Length of the marker side in world units</param>
    public void Initialize(float markerLength)
    {
      this.markerLength = markerLength;

      // Dispose existing resources
      Dispose();

      // Initialize matrices
      rvecs = new Mat(1, 10, CvType.CV_64FC3);
      tvecs = new Mat(1, 10, CvType.CV_64FC3);

      // Create object points for canonical marker
      UpdateObjectPoints();

      isInitialized = true;

      Debug.Log($"PoseEstimationManager: Initialized with marker length {markerLength}");
    }

    #endregion

    #region Pose Estimation Methods

    /// <summary>
    /// Estimate pose for all detected markers
    /// </summary>
    /// <param name="detectionManager">ArUco detection manager with detected markers</param>
    /// <param name="cameraManager">Camera parameters manager</param>
    /// <param name="rgbMat">RGB image matrix for drawing axes</param>
    /// <returns>List of pose data for each detected marker</returns>
    public List<PoseData> EstimateMarkerspose(ArUcoDetectionManager detectionManager,
                                               CameraParametersManager cameraManager,
                                               Mat rgbMat = null)
    {
      var poseDataList = new List<PoseData>();

      if (!isInitialized || detectionManager == null || cameraManager == null)
      {
        Debug.LogWarning("PoseEstimationManager: Not initialized or invalid parameters");
        return poseDataList;
      }

      if (!detectionManager.HasDetectedMarkers || !cameraManager.IsInitialized)
      {
        return poseDataList;
      }

      int markerCount = detectionManager.GetDetectedMarkerCount();

      for (int i = 0; i < markerCount; i++)
      {
        var poseData = EstimateSingleMarkerPose(detectionManager, cameraManager, i, rgbMat);
        if (poseData.HasValue)
        {
          poseDataList.Add(poseData.Value);
        }
      }

      return poseDataList;
    }

    /// <summary>
    /// Estimate pose for a single marker
    /// </summary>
    /// <param name="detectionManager">ArUco detection manager</param>
    /// <param name="cameraManager">Camera parameters manager</param>
    /// <param name="markerIndex">Index of the marker to estimate pose for</param>
    /// <param name="rgbMat">RGB image matrix for drawing axes (optional)</param>
    /// <returns>Pose data if successful, null otherwise</returns>
    public PoseData? EstimateSingleMarkerPose(ArUcoDetectionManager detectionManager,
                                              CameraParametersManager cameraManager,
                                              int markerIndex,
                                              Mat rgbMat = null)
    {
      if (!isInitialized || detectionManager == null || cameraManager == null)
        return null;

      Mat corners = detectionManager.GetMarkerCorners(markerIndex);
      if (corners == null)
        return null;

      using (Mat rvec = new Mat(1, 1, CvType.CV_64FC3))
      using (Mat tvec = new Mat(1, 1, CvType.CV_64FC3))
      using (Mat corner_4x1 = corners.reshape(2, 4)) // 1*4*CV_32FC2 => 4*1*CV_32FC2
      using (MatOfPoint2f imagePoints = new MatOfPoint2f(corner_4x1))
      {
        // Calculate pose for the marker
        bool success = Calib3d.solvePnP(objectPoints, imagePoints,
                                       cameraManager.CameraMatrix,
                                       cameraManager.DistortionCoeffs,
                                       rvec, tvec,
                                       false,
                                       Calib3d.SOLVEPNP_IPPE_SQUARE);

        if (!success)
        {
          Debug.LogWarning($"PoseEstimationManager: Failed to solve PnP for marker {markerIndex}");
          return null;
        }


        // Convert to pose data
        double[] rvecArr = new double[3];
        rvec.get(0, 0, rvecArr);
        double[] tvecArr = new double[3];
        tvec.get(0, 0, tvecArr);

        PoseData poseData = OpenCVARUtils.ConvertRvecTvecToPoseData(rvecArr, tvecArr);
        return poseData;
      }
    }

    /// <summary>
    /// Convert pose data to Unity transformation matrix
    /// </summary>
    /// <param name="poseData">Pose data from OpenCV</param>
    /// <param name="rightHandedCoordinateSystem">Use right-handed coordinate system</param>
    /// <returns>Unity transformation matrix</returns>
    public Matrix4x4 ConvertPoseToMatrix(PoseData poseData, bool rightHandedCoordinateSystem = true)
    {
      return OpenCVARUtils.ConvertPoseDataToMatrix(ref poseData, rightHandedCoordinateSystem);
    }

    /// <summary>
    /// Apply transformation matrix to Unity transform
    /// </summary>
    /// <param name="transform">Unity transform to modify</param>
    /// <param name="matrix">Transformation matrix</param>
    public void ApplyMatrixToTransform(Transform transform, Matrix4x4 matrix)
    {
      if (transform == null)
      {
        Debug.LogWarning("PoseEstimationManager: Transform is null");
        return;
      }

      OpenCVARUtils.SetTransformFromMatrix(transform, ref matrix);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Update object points based on current marker length
    /// </summary>
    private void UpdateObjectPoints()
    {
      // Dispose existing object points
      objectPoints?.Dispose();

      // Create new object points for canonical marker
      // Points are defined in marker coordinate system (Z=0 plane)
      float halfLength = markerLength / 2f;
      objectPoints = new MatOfPoint3f(
          new Point3(-halfLength, halfLength, 0),   // Top-left
          new Point3(halfLength, halfLength, 0),    // Top-right
          new Point3(halfLength, -halfLength, 0),   // Bottom-right
          new Point3(-halfLength, -halfLength, 0)   // Bottom-left
      );
    }

    /// <summary>
    /// Get the object points for the current marker
    /// </summary>
    /// <returns>Object points matrix</returns>
    public MatOfPoint3f GetObjectPoints()
    {
      return objectPoints;
    }

    /// <summary>
    /// Calculate distance from camera to marker
    /// </summary>
    /// <param name="poseData">Pose data containing translation vector</param>
    /// <returns>Distance in world units</returns>
    public float CalculateDistanceToMarker(PoseData poseData)
    {
      Vector3 translation = poseData.position;
      return translation.magnitude;
    }

    /// <summary>
    /// Get marker orientation as Euler angles
    /// </summary>
    /// <param name="poseData">Pose data containing rotation</param>
    /// <returns>Euler angles in degrees</returns>
    public Vector3 GetMarkerEulerAngles(PoseData poseData)
    {
      return poseData.rotation.eulerAngles;
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Dispose all resources
    /// </summary>
    public void Dispose()
    {
      objectPoints?.Dispose();
      objectPoints = null;

      rvecs?.Dispose();
      rvecs = null;

      tvecs?.Dispose();
      tvecs = null;

      isInitialized = false;

      Debug.Log("PoseEstimationManager: Resources disposed");
    }

    #endregion
  }
}

#endif
