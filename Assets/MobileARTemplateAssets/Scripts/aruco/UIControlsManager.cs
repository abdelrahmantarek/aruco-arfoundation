#if !(PLATFORM_LUMIN && !UNITY_EDITOR)

using ARFoundationWithOpenCVForUnity.UnityIntegration.Helper.Source2Mat;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARSubsystems;
using System;

namespace ARFoundationWithOpenCVForUnityExample
{
    /// <summary>
    /// Manager for UI controls and user interactions
    /// Handles buttons, dropdowns, toggles, and their events
    /// </summary>
    public class UIControlsManager
    {
        #region Events

        /// <summary>
        /// Event triggered when dictionary ID changes
        /// </summary>
        public event Action<ArUcoDictionary> OnDictionaryChanged;

        /// <summary>
        /// Event triggered when pose estimation toggle changes
        /// </summary>
        public event Action<bool> OnPoseEstimationToggled;

        /// <summary>
        /// Event triggered when lerp filter toggle changes
        /// </summary>
        public event Action<bool> OnLerpFilterToggled;

        /// <summary>
        /// Event triggered when camera facing direction should change
        /// </summary>
        public event Action OnCameraFacingChangeRequested;

        /// <summary>
        /// Event triggered when light estimation should change
        /// </summary>
        public event Action OnLightEstimationChangeRequested;

        /// <summary>
        /// Event triggered when play button is clicked
        /// </summary>
        public event Action OnPlayRequested;

        /// <summary>
        /// Event triggered when pause button is clicked
        /// </summary>
        public event Action OnPauseRequested;

        /// <summary>
        /// Event triggered when stop button is clicked
        /// </summary>
        public event Action OnStopRequested;

        #endregion

        #region Private Fields

        private Dropdown dictionaryIdDropdown;
        private Toggle applyEstimationPoseToggle;
        private Toggle enableLerpFilterToggle;
        private FpsMonitor fpsMonitor;
        private ARFoundationCamera2MatHelper cameraHelper;

        private bool isInitialized = false;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current dictionary ID from dropdown
        /// </summary>
        public ArUcoDictionary CurrentDictionaryId
        {
            get
            {
                if (dictionaryIdDropdown != null)
                    return (ArUcoDictionary)dictionaryIdDropdown.value;
                return ArUcoDictionary.DICT_6X6_250;
            }
        }

        /// <summary>
        /// Gets the current pose estimation toggle state
        /// </summary>
        public bool IsPoseEstimationEnabled
        {
            get
            {
                if (applyEstimationPoseToggle != null)
                    return applyEstimationPoseToggle.isOn;
                return true;
            }
        }

        /// <summary>
        /// Gets the current lerp filter toggle state
        /// </summary>
        public bool IsLerpFilterEnabled
        {
            get
            {
                if (enableLerpFilterToggle != null)
                    return enableLerpFilterToggle.isOn;
                return false;
            }
        }

        /// <summary>
        /// Returns true if the manager is initialized
        /// </summary>
        public bool IsInitialized => isInitialized;

        #endregion

        #region Constructor & Initialization

        /// <summary>
        /// Initialize UI controls manager
        /// </summary>
        /// <param name="dictionaryDropdown">Dictionary selection dropdown</param>
        /// <param name="poseToggle">Pose estimation toggle</param>
        /// <param name="lerpToggle">Lerp filter toggle</param>
        /// <param name="fpsMonitor">FPS monitor for displaying information</param>
        /// <param name="cameraHelper">Camera helper for camera controls</param>
        public UIControlsManager(Dropdown dictionaryDropdown,
                                Toggle poseToggle,
                                Toggle lerpToggle,
                                FpsMonitor fpsMonitor = null,
                                ARFoundationCamera2MatHelper cameraHelper = null)
        {
            Initialize(dictionaryDropdown, poseToggle, lerpToggle, fpsMonitor, cameraHelper);
        }

        /// <summary>
        /// Initialize the UI controls manager
        /// </summary>
        /// <param name="dictionaryDropdown">Dictionary selection dropdown</param>
        /// <param name="poseToggle">Pose estimation toggle</param>
        /// <param name="lerpToggle">Lerp filter toggle</param>
        /// <param name="fpsMonitor">FPS monitor for displaying information</param>
        /// <param name="cameraHelper">Camera helper for camera controls</param>
        public void Initialize(Dropdown dictionaryDropdown,
                              Toggle poseToggle,
                              Toggle lerpToggle,
                              FpsMonitor fpsMonitor = null,
                              ARFoundationCamera2MatHelper cameraHelper = null)
        {
            this.dictionaryIdDropdown = dictionaryDropdown;
            this.applyEstimationPoseToggle = poseToggle;
            this.enableLerpFilterToggle = lerpToggle;
            this.fpsMonitor = fpsMonitor;
            this.cameraHelper = cameraHelper;

            SetupEventListeners();

            isInitialized = true;

            Debug.Log("UIControlsManager: Initialized successfully");
        }

        /// <summary>
        /// Setup event listeners for UI controls
        /// </summary>
        private void SetupEventListeners()
        {
            // Dictionary dropdown
            if (dictionaryIdDropdown != null)
            {
                dictionaryIdDropdown.onValueChanged.AddListener(OnDictionaryIdDropdownValueChanged);
            }

            // Pose estimation toggle
            if (applyEstimationPoseToggle != null)
            {
                applyEstimationPoseToggle.onValueChanged.AddListener(OnApplyEstimationPoseToggleValueChanged);
            }

            // Lerp filter toggle
            if (enableLerpFilterToggle != null)
            {
                enableLerpFilterToggle.onValueChanged.AddListener(OnEnableLeapFilterToggleValueChanged);
            }
        }

        #endregion

        #region UI Control Methods

        /// <summary>
        /// Set initial UI values
        /// </summary>
        /// <param name="dictionaryId">Initial dictionary ID</param>
        /// <param name="poseEstimation">Initial pose estimation state</param>
        /// <param name="lerpFilter">Initial lerp filter state</param>
        public void SetInitialValues(ArUcoDictionary dictionaryId, bool poseEstimation, bool lerpFilter)
        {
            if (dictionaryIdDropdown != null)
            {
                dictionaryIdDropdown.value = (int)dictionaryId;
            }

            if (applyEstimationPoseToggle != null)
            {
                applyEstimationPoseToggle.isOn = poseEstimation;
            }

            if (enableLerpFilterToggle != null)
            {
                enableLerpFilterToggle.isOn = lerpFilter;
            }
        }

        /// <summary>
        /// Update FPS monitor with camera information
        /// </summary>
        /// <param name="width">Image width</param>
        /// <param name="height">Image height</param>
        /// <param name="orientation">Screen orientation</param>
        /// <param name="isFrontFacing">Is front facing camera</param>
        /// <param name="requestedFacing">Requested facing direction</param>
        /// <param name="currentFacing">Current facing direction</param>
        /// <param name="requestedLightEstimation">Requested light estimation</param>
        /// <param name="currentLightEstimation">Current light estimation</param>
        public void UpdateFpsMonitorInfo(int width, int height, ScreenOrientation orientation,
                                        bool isFrontFacing, string requestedFacing, string currentFacing,
                                        string requestedLightEstimation, string currentLightEstimation)
        {
            if (fpsMonitor == null) return;

            fpsMonitor.Add("width", width.ToString());
            fpsMonitor.Add("height", height.ToString());
            fpsMonitor.Add("orientation", orientation.ToString());
            fpsMonitor.Add("IsFrontFacing", isFrontFacing.ToString());
            fpsMonitor.Add("requestedFacingDirection", requestedFacing);
            fpsMonitor.Add("currentFacingDirection", currentFacing);
            fpsMonitor.Add("requestedLightEstimation", requestedLightEstimation);
            fpsMonitor.Add("currentLightEstimation", currentLightEstimation);
        }

        /// <summary>
        /// Update FPS monitor with camera intrinsics information
        /// </summary>
        /// <param name="focalLength">Focal length</param>
        /// <param name="principalPoint">Principal point</param>
        /// <param name="resolution">Resolution</param>
        public void UpdateCameraIntrinsicsInfo(Vector2 focalLength, Vector2 principalPoint, Vector2Int resolution)
        {
            if (fpsMonitor == null) return;

            string intrinsicsInfo = $"\nFL: {focalLength.x}x{focalLength.y}\nPP: {principalPoint.x}x{principalPoint.y}\nR: {resolution.x}x{resolution.y}";
            fpsMonitor.Add("cameraIntrinsics", intrinsicsInfo);
        }

        /// <summary>
        /// Show error message in FPS monitor
        /// </summary>
        /// <param name="errorCode">Error code</param>
        /// <param name="message">Error message</param>
        public void ShowError(string errorCode, string message)
        {
            if (fpsMonitor != null)
            {
                fpsMonitor.consoleText = $"ErrorCode: {errorCode}:{message}";
            }

            Debug.LogError($"UIControlsManager: {errorCode} - {message}");
        }

        /// <summary>
        /// Show toast message
        /// </summary>
        /// <param name="message">Message to show</param>
        public void ShowToast(string message)
        {
            if (fpsMonitor != null)
            {
                fpsMonitor.Toast(message);
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle dictionary dropdown value change
        /// </summary>
        /// <param name="value">New dropdown value</param>
        public void OnDictionaryIdDropdownValueChanged(int value)
        {
            ArUcoDictionary newDictionary = (ArUcoDictionary)value;
            OnDictionaryChanged?.Invoke(newDictionary);
        }

        /// <summary>
        /// Handle pose estimation toggle change
        /// </summary>
        /// <param name="value">New toggle value</param>
        public void OnApplyEstimationPoseToggleValueChanged(bool value)
        {
            OnPoseEstimationToggled?.Invoke(value);
        }

        /// <summary>
        /// Handle lerp filter toggle change
        /// </summary>
        /// <param name="value">New toggle value</param>
        public void OnEnableLeapFilterToggleValueChanged(bool value)
        {
            OnLerpFilterToggled?.Invoke(value);
        }

        #endregion

        #region Button Click Handlers

        /// <summary>
        /// Handle back button click
        /// </summary>
        public void OnBackButtonClick()
        {
            SceneManager.LoadScene("ARFoundationWithOpenCVForUnityExample");
        }

        /// <summary>
        /// Handle play button click
        /// </summary>
        public void OnPlayButtonClick()
        {
            OnPlayRequested?.Invoke();
        }

        /// <summary>
        /// Handle pause button click
        /// </summary>
        public void OnPauseButtonClick()
        {
            OnPauseRequested?.Invoke();
        }

        /// <summary>
        /// Handle stop button click
        /// </summary>
        public void OnStopButtonClick()
        {
            OnStopRequested?.Invoke();
        }

        /// <summary>
        /// Handle change camera button click
        /// </summary>
        public void OnChangeCameraButtonClick()
        {
            OnCameraFacingChangeRequested?.Invoke();

            // Show warning toast
            ShowToast("If LightEstimation is enabled, the camera facing direction may not be changed depending on the device's capabilities.");
        }

        /// <summary>
        /// Handle change light estimation button click
        /// </summary>
        public void OnChangeLightEstimationButtonClick()
        {
            OnLightEstimationChangeRequested?.Invoke();

            // Show warning toast
            ShowToast("If LightEstimation is enabled, the camera facing direction may not be changed depending on the device's capabilities.");
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Remove event listeners and cleanup
        /// </summary>
        public void Dispose()
        {
            // Remove event listeners
            if (dictionaryIdDropdown != null)
            {
                dictionaryIdDropdown.onValueChanged.RemoveListener(OnDictionaryIdDropdownValueChanged);
            }

            if (applyEstimationPoseToggle != null)
            {
                applyEstimationPoseToggle.onValueChanged.RemoveListener(OnApplyEstimationPoseToggleValueChanged);
            }

            if (enableLerpFilterToggle != null)
            {
                enableLerpFilterToggle.onValueChanged.RemoveListener(OnEnableLeapFilterToggleValueChanged);
            }

            // Clear references
            dictionaryIdDropdown = null;
            applyEstimationPoseToggle = null;
            enableLerpFilterToggle = null;
            fpsMonitor = null;
            cameraHelper = null;

            isInitialized = false;

            Debug.Log("UIControlsManager: Disposed");
        }

        #endregion
    }
}

#endif
