using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
using System.Collections.Generic;
using System.Linq;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.ArucoModule;
using OpenCVForUnity.Calib3dModule;
using Unity.XR.CoreUtils;


public class ArUcoMarker
{
  public int id;
  public Vector3 position;
  public Quaternion rotation; // تغيير من Vector3 إلى Quaternion
  public Mat corners;
}


public static class ArUcoHelper
{
  public static List<ArUcoMarker> DetectMarkersWithPose(Texture2D texture, int dictionaryId, float markerSize, Mat cameraMatrix, Mat distCoeffs, Camera arCamera = null)
  {
    var markers = new List<ArUcoMarker>();

    Debug.Log($"Starting ArUco detection on {texture.width}x{texture.height} texture with dictionary {dictionaryId}");

    Mat rgbMat = null;
    Mat grayMat = null;
    Dictionary dictionary = null;
    DetectorParameters detectorParams = null;
    ArucoDetector detector = null;
    Mat ids = null;
    Mat rvecs = null;
    Mat tvecs = null;

    try
    {
      rgbMat = new Mat(texture.height, texture.width, CvType.CV_8UC3);
      Utils.texture2DToMat(texture, rgbMat);
      Debug.Log($"RGB Mat created: {rgbMat.rows()}x{rgbMat.cols()}");

      grayMat = new Mat();
      Imgproc.cvtColor(rgbMat, grayMat, Imgproc.COLOR_RGB2GRAY);
      Debug.Log($"Gray Mat created: {grayMat.rows()}x{grayMat.cols()}");

      dictionary = Objdetect.getPredefinedDictionary(dictionaryId);
      Debug.Log($"Dictionary loaded: {dictionaryId}");

      detectorParams = new DetectorParameters();
      // تحسين معاملات الكشف لتحسين الأداء
      detectorParams.set_adaptiveThreshWinSizeMin(3);
      detectorParams.set_adaptiveThreshWinSizeMax(23);
      detectorParams.set_adaptiveThreshWinSizeStep(10);
      detectorParams.set_adaptiveThreshConstant(7);
      detectorParams.set_minMarkerPerimeterRate(0.01); // أقل لكشف ماركرات أصغر
      detectorParams.set_maxMarkerPerimeterRate(4.0);
      detectorParams.set_polygonalApproxAccuracyRate(0.03);
      detectorParams.set_minCornerDistanceRate(0.05);
      detectorParams.set_minDistanceToBorder(3);
      detectorParams.set_minMarkerDistanceRate(0.05);
      detectorParams.set_cornerRefinementMethod(Objdetect.CORNER_REFINE_SUBPIX);
      detectorParams.set_cornerRefinementWinSize(5);
      detectorParams.set_cornerRefinementMaxIterations(30);
      detectorParams.set_cornerRefinementMinAccuracy(0.1);
      Debug.Log("Enhanced detector parameters set");

      detector = new ArucoDetector(dictionary, detectorParams);
      Debug.Log("ArUco detector created");

      ids = new Mat();
      List<Mat> corners = new List<Mat>();
      List<Mat> rejectedCorners = new List<Mat>();

      Debug.Log("Starting marker detection...");
      detector.detectMarkers(grayMat, corners, ids, rejectedCorners);

      Debug.Log($"Detection completed - IDs found: {ids.total()}, Corners: {corners.Count}, Rejected: {rejectedCorners.Count}");

      if (ids.total() > 0)
      {
        int[] idsArray = new int[ids.total()];
        ids.get(0, 0, idsArray);

        Debug.Log($"Detected marker IDs: [{string.Join(", ", idsArray)}]");

        rvecs = new Mat();
        tvecs = new Mat();

        Debug.Log($"Starting pose estimation with markerSize: {markerSize}");
        Debug.Log($"Camera matrix size: {cameraMatrix.rows()}x{cameraMatrix.cols()}");
        Debug.Log($"Dist coeffs size: {distCoeffs.rows()}x{distCoeffs.cols()}");

        try
        {
          Aruco.estimatePoseSingleMarkers(corners, markerSize, cameraMatrix, distCoeffs, rvecs, tvecs);
          Debug.Log("Pose estimation completed successfully");
        }
        catch (System.Exception poseEx)
        {
          Debug.LogError($"Pose estimation failed: {poseEx.Message}");
          Debug.LogError($"Stack trace: {poseEx.StackTrace}");

          // استخدام موضع افتراضي في حالة فشل pose estimation
          for (int i = 0; i < idsArray.Length; i++)
          {
            Vector3 position = new Vector3(0, 0, 1.5f);
            Quaternion rotation = Quaternion.identity;

            markers.Add(new ArUcoMarker
            {
              id = idsArray[i],
              corners = corners[i],
              position = position,
              rotation = rotation
            });
          }
          return markers;
        }

        Debug.Log($"Rvecs size: {rvecs.rows()}x{rvecs.cols()}, Tvecs size: {tvecs.rows()}x{tvecs.cols()}");

        for (int i = 0; i < idsArray.Length; i++)
        {
          try
          {
            double[] rvecArray = new double[3];
            double[] tvecArray = new double[3];

            rvecs.get(i, 0, rvecArray);
            tvecs.get(i, 0, tvecArray);

            Debug.Log($"Marker {idsArray[i]} raw pose - T: [{tvecArray[0]:F3}, {tvecArray[1]:F3}, {tvecArray[2]:F3}], R: [{rvecArray[0]:F3}, {rvecArray[1]:F3}, {rvecArray[2]:F3}]");
            Debug.Log($"markerSize={markerSize}m, Distance from camera: {Mathf.Sqrt((float)(tvecArray[0] * tvecArray[0] + tvecArray[1] * tvecArray[1] + tvecArray[2] * tvecArray[2])):F3}m");

            // تصحيح نظام الإحداثيات: OpenCV → Unity
            Vector3 position = new Vector3(
                (float)tvecArray[0],
                -(float)tvecArray[1],
                (float)tvecArray[2]
            );

            // احفظ الموضع في camera space بدون تحويل
            Vector3 cameraSpacePosition = position;

            // تحويل rotation vector إلى rotation matrix ثم إلى quaternion
            Mat rvecMat = new Mat(3, 1, CvType.CV_64FC1);
            rvecMat.put(0, 0, rvecArray);

            Mat rotationMatrix = new Mat();
            Calib3d.Rodrigues(rvecMat, rotationMatrix);

            // تحويل rotation matrix إلى Unity Matrix4x4
            Matrix4x4 transformMatrix = Matrix4x4.identity;
            double[] rotData = new double[9];
            rotationMatrix.get(0, 0, rotData);

            transformMatrix[0, 0] = (float)rotData[0]; transformMatrix[0, 1] = (float)rotData[1]; transformMatrix[0, 2] = (float)rotData[2];
            transformMatrix[1, 0] = (float)rotData[3]; transformMatrix[1, 1] = (float)rotData[4]; transformMatrix[1, 2] = (float)rotData[5];
            transformMatrix[2, 0] = (float)rotData[6]; transformMatrix[2, 1] = (float)rotData[7]; transformMatrix[2, 2] = (float)rotData[8];

            // تصحيح مصفوفة الدوران من OpenCV إلى Unity بقلب محور Y على الصفوف والأعمدة
            // مكافئ رياضياً لـ F * R * F حيث F = diag(1,-1,1)
            transformMatrix[0, 0] = (float)rotData[0]; transformMatrix[0, 1] = -(float)rotData[1]; transformMatrix[0, 2] = (float)rotData[2];
            transformMatrix[1, 0] = -(float)rotData[3]; transformMatrix[1, 1] = (float)rotData[4]; transformMatrix[1, 2] = -(float)rotData[5];
            transformMatrix[2, 0] = (float)rotData[6]; transformMatrix[2, 1] = -(float)rotData[7]; transformMatrix[2, 2] = (float)rotData[8];
            // استخراج Quaternion بعد التصحيح
            Quaternion cameraSpaceRotation = QuaternionFromMatrix(transformMatrix);

            // تحويل إلى world space إذا كانت الكاميرا متوفرة
            Vector3 worldPosition = cameraSpacePosition;
            Quaternion worldRotation = cameraSpaceRotation;

            if (arCamera != null)
            {
              worldPosition = arCamera.transform.TransformPoint(cameraSpacePosition);
              worldRotation = arCamera.transform.rotation * cameraSpaceRotation;
              Debug.Log($"Marker {idsArray[i]} - Camera space: {cameraSpacePosition}, World space: {worldPosition}");
            }

            markers.Add(new ArUcoMarker
            {
              id = idsArray[i],
              corners = corners[i],
              position = worldPosition,  // استخدم world space إذا متوفر، وإلا camera space
              rotation = worldRotation   // استخدم world space إذا متوفر، وإلا camera space
            });

            Debug.Log($"Marker {idsArray[i]} processed successfully - Unity Pos: {cameraSpacePosition}, Rot: {cameraSpaceRotation}");

            rvecMat?.Dispose();
            rotationMatrix?.Dispose();
          }
          catch (System.Exception markerEx)
          {
            Debug.LogError($"Error processing marker {idsArray[i]}: {markerEx.Message}");
          }
        }
      }
      else
      {
        Debug.LogWarning("No markers detected in this frame");
      }

      foreach (var rejected in rejectedCorners) rejected?.Dispose();
    }
    catch (System.Exception e)
    {
      Debug.LogError($"ArUcoHelper Detection Error: {e.Message}\nStack: {e.StackTrace}");
    }
    finally
    {
      rgbMat?.Dispose();
      grayMat?.Dispose();
      ids?.Dispose();
      dictionary?.Dispose();
      detectorParams?.Dispose();
      detector?.Dispose();
      rvecs?.Dispose();
      tvecs?.Dispose();
    }

    Debug.Log($"ArUco detection finished, returning {markers.Count} markers");
    return markers;
  }

  private static Quaternion QuaternionFromMatrix(Matrix4x4 m)
  {
    // استخراج الاتجاهات من المصفوفة
    Vector3 forward = new Vector3(m[0, 2], m[1, 2], m[2, 2]);
    Vector3 upward = new Vector3(m[0, 1], m[1, 1], m[2, 1]);

    return Quaternion.LookRotation(forward, upward);
  }
}
