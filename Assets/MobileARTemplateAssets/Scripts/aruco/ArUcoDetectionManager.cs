#if !(PLATFORM_LUMIN && !UNITY_EDITOR)

using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using System.Collections.Generic;
using UnityEngine;

namespace ARFoundationWithOpenCVForUnityExample
{
    /// <summary>
    /// Manager for ArUco marker detection functionality
    /// Handles dictionary management, marker detection, and drawing
    /// </summary>
    public class ArUcoDetectionManager
    {
        #region Private Fields

        private Dictionary dictionary;
        private ArucoDetector arucoDetector;
        private Mat ids;
        private List<Mat> corners;
        private List<Mat> rejectedCorners;

        #endregion

        #region Properties

        /// <summary>
        /// The current ArUco dictionary being used
        /// </summary>
        public ArUcoDictionary CurrentDictionary { get; private set; }

        /// <summary>
        /// Gets the detected marker IDs
        /// </summary>
        public Mat DetectedIds => ids;

        /// <summary>
        /// Gets the detected marker corners
        /// </summary>
        public List<Mat> DetectedCorners => corners;

        /// <summary>
        /// Gets the rejected corners
        /// </summary>
        public List<Mat> RejectedCorners => rejectedCorners;

        /// <summary>
        /// Returns true if any markers were detected in the last detection
        /// </summary>
        public bool HasDetectedMarkers => ids != null && ids.total() > 0;

        #endregion

        #region Constructor & Initialization

        /// <summary>
        /// Initialize the ArUco detection manager with specified dictionary
        /// </summary>
        /// <param name="dictionaryId">The ArUco dictionary to use</param>
        public ArUcoDetectionManager(ArUcoDictionary dictionaryId = ArUcoDictionary.DICT_6X6_250)
        {
            Initialize(dictionaryId);
        }

        /// <summary>
        /// Initialize or reinitialize the detection system
        /// </summary>
        /// <param name="dictionaryId">The ArUco dictionary to use</param>
        public void Initialize(ArUcoDictionary dictionaryId)
        {
            CurrentDictionary = dictionaryId;

            // Dispose existing resources safely
            DisposeResources();

            // Initialize new resources
            ids = new Mat();
            corners = new List<Mat>();
            rejectedCorners = new List<Mat>();

            // Create dictionary and detector
            dictionary = Objdetect.getPredefinedDictionary((int)dictionaryId);

            DetectorParameters detectorParams = new DetectorParameters();
            detectorParams.set_useAruco3Detection(true);
            RefineParameters refineParameters = new RefineParameters(10f, 3f, true);
            arucoDetector = new ArucoDetector(dictionary, detectorParams, refineParameters);
        }

        #endregion

        #region Detection Methods

        /// <summary>
        /// Detect ArUco markers in the provided image
        /// </summary>
        /// <param name="inputMat">The input image matrix</param>
        /// <returns>True if markers were detected, false otherwise</returns>
        public bool DetectMarkers(Mat inputMat)
        {
            if (inputMat == null || arucoDetector == null)
            {
                Debug.LogWarning("ArUcoDetectionManager: Input mat or detector is null");
                return false;
            }

            // Clear previous results
            ClearDetectionResults();

            // Perform detection
            arucoDetector.detectMarkers(inputMat, corners, ids, rejectedCorners);

            return HasDetectedMarkers;
        }

        /// <summary>
        /// Draw detected markers on the image
        /// </summary>
        /// <param name="imageMat">The image to draw on</param>
        /// <param name="markerColor">Color for drawing markers (default: green)</param>
        public void DrawDetectedMarkers(Mat imageMat, Scalar markerColor = null)
        {
            if (!HasDetectedMarkers || imageMat == null)
                return;

            if (markerColor == null)
                markerColor = new Scalar(0, 255, 0); // Default green color

            Objdetect.drawDetectedMarkers(imageMat, corners, ids, markerColor);
        }

        /// <summary>
        /// Get the corner points for a specific marker by index
        /// </summary>
        /// <param name="markerIndex">Index of the marker</param>
        /// <returns>Corner points matrix or null if invalid index</returns>
        public Mat GetMarkerCorners(int markerIndex)
        {
            if (!HasDetectedMarkers || markerIndex < 0 || markerIndex >= corners.Count)
                return null;

            return corners[markerIndex];
        }

        /// <summary>
        /// Get the ID of a specific marker by index
        /// </summary>
        /// <param name="markerIndex">Index of the marker</param>
        /// <returns>Marker ID or -1 if invalid index</returns>
        public int GetMarkerId(int markerIndex)
        {
            if (!HasDetectedMarkers || markerIndex < 0 || markerIndex >= ids.total())
                return -1;

            // Use int array instead of double array for marker IDs
            int[] idArray = new int[1];
            ids.get(markerIndex, 0, idArray);
            return idArray[0];
        }

        /// <summary>
        /// Get the total number of detected markers
        /// </summary>
        /// <returns>Number of detected markers</returns>
        public int GetDetectedMarkerCount()
        {
            return HasDetectedMarkers ? (int)ids.total() : 0;
        }

        #endregion

        #region Dictionary Management

        /// <summary>
        /// Change the ArUco dictionary
        /// </summary>
        /// <param name="newDictionaryId">New dictionary to use</param>
        public void ChangeDictionary(ArUcoDictionary newDictionaryId)
        {
            if (CurrentDictionary != newDictionaryId)
            {
                Initialize(newDictionaryId);
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Clear detection results
        /// </summary>
        private void ClearDetectionResults()
        {
            // Clear corners list
            if (corners != null)
            {
                foreach (var corner in corners)
                {
                    corner?.Dispose();
                }
                corners.Clear();
            }

            // Clear rejected corners list
            if (rejectedCorners != null)
            {
                foreach (var rejectedCorner in rejectedCorners)
                {
                    rejectedCorner?.Dispose();
                }
                rejectedCorners.Clear();
            }

            // Reset ids
            ids?.setTo(new Scalar(0));
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Dispose resources safely without clearing detection results
        /// </summary>
        private void DisposeResources()
        {
            // Dispose main containers
            ids?.Dispose();
            ids = null;

            if (corners != null)
            {
                foreach (var corner in corners)
                {
                    corner?.Dispose();
                }
                corners.Clear();
                corners = null;
            }

            if (rejectedCorners != null)
            {
                foreach (var rejectedCorner in rejectedCorners)
                {
                    rejectedCorner?.Dispose();
                }
                rejectedCorners.Clear();
                rejectedCorners = null;
            }

            // Dispose detector and dictionary
            arucoDetector?.Dispose();
            arucoDetector = null;

            dictionary?.Dispose();
            dictionary = null;
        }

        /// <summary>
        /// Dispose all resources
        /// </summary>
        public void Dispose()
        {
            // Dispose detection results
            ClearDetectionResults();

            // Dispose all resources
            DisposeResources();
        }

        #endregion
    }

    /// <summary>
    /// ArUco dictionary enumeration
    /// </summary>
    public enum ArUcoDictionary
    {
        DICT_4X4_50 = Objdetect.DICT_4X4_50,
        DICT_4X4_100 = Objdetect.DICT_4X4_100,
        DICT_4X4_250 = Objdetect.DICT_4X4_250,
        DICT_4X4_1000 = Objdetect.DICT_4X4_1000,
        DICT_5X5_50 = Objdetect.DICT_5X5_50,
        DICT_5X5_100 = Objdetect.DICT_5X5_100,
        DICT_5X5_250 = Objdetect.DICT_5X5_250,
        DICT_5X5_1000 = Objdetect.DICT_5X5_1000,
        DICT_6X6_50 = Objdetect.DICT_6X6_50,
        DICT_6X6_100 = Objdetect.DICT_6X6_100,
        DICT_6X6_250 = Objdetect.DICT_6X6_250,
        DICT_6X6_1000 = Objdetect.DICT_6X6_1000,
        DICT_7X7_50 = Objdetect.DICT_7X7_50,
        DICT_7X7_100 = Objdetect.DICT_7X7_100,
        DICT_7X7_250 = Objdetect.DICT_7X7_250,
        DICT_7X7_1000 = Objdetect.DICT_7X7_1000,
        DICT_ARUCO_ORIGINAL = Objdetect.DICT_ARUCO_ORIGINAL,
    }
}

#endif
