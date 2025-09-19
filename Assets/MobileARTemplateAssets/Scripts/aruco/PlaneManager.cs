using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class PlaneManager : MonoBehaviour
{
    // Plane visualization and occlusion management system

    [Header("AR Foundation")]
    public ARPlaneManager arPlaneManager;
    public ARCameraManager arCameraManager;

    [Header("Plane Settings")]
    public PlaneDetectionMode detectionMode = PlaneDetectionMode.Horizontal;
    public bool visualizePlanes = true;
    public Material planeMaterial;

    [Header("Plane Filtering")]
    public float minPlaneArea = 0.1f;
    public bool filterSmallPlanes = true;

    [Header("Occlusion Prevention")]
    [Tooltip("Prevent planes above this height from being visualized to avoid occluding AR objects")]
    public bool enableHeightFiltering = true;
    [Tooltip("Maximum height above camera for plane visualization (meters)")]
    public float maxVisualizationHeight = 2.0f;
    [Tooltip("Visualization mode for planes")]
    public PlaneVisualizationMode visualizationMode = PlaneVisualizationMode.TransparentOnly;

    [Header("Materials")]
    [Tooltip("Transparent material for visible planes")]
    public Material transparentPlaneMaterial;
    [Tooltip("Occlusion material for invisible depth-only planes")]
    public Material occlusionPlaneMaterial;

    public enum PlaneVisualizationMode
    {
        TransparentOnly,    // Only show transparent planes
        OcclusionOnly,      // Only depth-write for occlusion
        Smart,              // Transparent for ground, occlusion for overhead
        Disabled            // No plane visualization
    }

    // Events
    public event Action<ARPlane> OnPlaneAdded;
    public event Action<ARPlane> OnPlaneUpdated;
    public event Action<ARPlane> OnPlaneRemoved;

    // Private fields
    private Dictionary<TrackableId, PlaneData> _trackedPlanes;



    private void Start()
    {
        _trackedPlanes = new Dictionary<TrackableId, PlaneData>();
        InitializePlaneManager();
    }

    private void Awake()
    {
        // Ensure PlaneManager is initialized early
        if (arPlaneManager == null)
        {
            InitializePlaneManager();
        }
    }

    public void InitializePlaneManager()
    {
        // Find ARPlaneManager if not assigned
        if (arPlaneManager == null)
            arPlaneManager = FindFirstObjectByType<ARPlaneManager>();
        // Create ARPlaneManager if it doesn't exist
        if (arPlaneManager == null)
        {
            // Try to find XROrigin to add ARPlaneManager to it
            XROrigin xrOrigin = FindFirstObjectByType<XROrigin>();
            if (xrOrigin != null)
            {
                arPlaneManager = xrOrigin.gameObject.AddComponent<ARPlaneManager>();
                Debug.Log("✅ PlaneManager: Created ARPlaneManager component on XROrigin");
            }
            else
            {
                Debug.LogError("❌ PlaneManager: XROrigin not found, cannot create ARPlaneManager");
                return;
            }
        }

        if (arCameraManager == null)
            arCameraManager = FindFirstObjectByType<ARCameraManager>();

        if (arPlaneManager != null)
        {

            arPlaneManager.requestedDetectionMode = detectionMode;
            // Enable plane detection
            arPlaneManager.enabled = true;

            // Subscribe to plane events
            arPlaneManager.trackablesChanged.AddListener(OnPlanesChanged);

            Debug.Log($"✅ PlaneManager initialized successfully - Detection Mode: {arPlaneManager.requestedDetectionMode}");
            Debug.Log($"✅ PlaneManager enabled: {arPlaneManager.enabled}");
        }
        else
        {
            Debug.LogError("❌ ARPlaneManager not found!");
        }
    }

    private void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> eventArgs)
    {
        // Handle added planes
        foreach (var plane in eventArgs.added)
        {
            Debug.Log($"✅ PlaneManager: New plane detected - ID: {plane.trackableId}, Size: {plane.size}, Alignment: {plane.alignment}");
            HandlePlaneAdded(plane);
        }

        // Handle updated planes
        foreach (var plane in eventArgs.updated)
        {
            Debug.Log($"🔄 PlaneManager: Plane updated - ID: {plane.trackableId}, Size: {plane.size}");
            HandlePlaneUpdated(plane);
        }

        // Handle removed planes
        foreach (var removedPlane in eventArgs.removed)
        {
            Debug.Log($"❌ PlaneManager: Plane removed - ID: {removedPlane.Key}");
            if (_trackedPlanes.ContainsKey(removedPlane.Key))
            {
                _trackedPlanes.Remove(removedPlane.Key);
                Debug.Log($"➖ Removed plane from tracking: {removedPlane.Key}");
            }
        }

        // Log total plane count
        int totalPlanes = arPlaneManager != null ? arPlaneManager.trackables.count : 0;
        Debug.Log($"📊 PlaneManager: Total planes detected: {totalPlanes}");
    }

    private void HandlePlaneAdded(ARPlane plane)
    {
        if (ShouldTrackPlane(plane))
        {
            PlaneData planeData = new PlaneData(plane);
            _trackedPlanes[plane.trackableId] = planeData;

            SetupPlaneVisualization(plane);

            OnPlaneAdded?.Invoke(plane);

            // Debug.Log($"➕ Plane added: {plane.trackableId} | Area: {plane.size.x * plane.size.y:F2}m²");
        }
    }

    private void HandlePlaneUpdated(ARPlane plane)
    {
        if (_trackedPlanes.ContainsKey(plane.trackableId))
        {
            _trackedPlanes[plane.trackableId].UpdatePlane(plane);

            // ✅ إعادة تطبيق إعدادات الـ visualization للطائرات المحدثة
            SetupPlaneVisualization(plane);

            OnPlaneUpdated?.Invoke(plane);

            Debug.Log($"🔄 Plane updated and visualization refreshed: {plane.trackableId}");
        }
    }

    private void HandlePlaneRemoved(ARPlane plane)
    {
        if (_trackedPlanes.ContainsKey(plane.trackableId))
        {
            _trackedPlanes.Remove(plane.trackableId);
            OnPlaneRemoved?.Invoke(plane);

            // Debug.Log($"➖ Plane removed: {plane.trackableId}");
        }
    }

    private bool ShouldTrackPlane(ARPlane plane)
    {
        if (!filterSmallPlanes) return true;

        float area = plane.size.x * plane.size.y;
        return area >= minPlaneArea;
    }

    private void SetupPlaneVisualization(ARPlane plane)
    {
        if (!visualizePlanes || visualizationMode == PlaneVisualizationMode.Disabled)
        {
            DisablePlaneVisualization(plane);
            return;
        }

        // Get or add MeshRenderer
        MeshRenderer meshRenderer = plane.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = plane.gameObject.AddComponent<MeshRenderer>();

        // Determine visualization approach based on mode and plane properties
        bool shouldVisualize = ShouldVisualizePlane(plane);
        bool useTransparentMaterial = ShouldUseTransparentMaterial(plane);

        if (!shouldVisualize)
        {
            meshRenderer.enabled = false;
            Debug.Log($"🚫 Plane {plane.trackableId} hidden due to height/occlusion filtering");
            return;
        }

        // Apply appropriate material based on visualization strategy
        Material materialToUse = GetMaterialForPlane(plane, useTransparentMaterial);
        if (materialToUse != null)
        {
            meshRenderer.material = materialToUse;
            meshRenderer.enabled = true;

            // Adjust rendering order to prevent occlusion of AR objects
            if (useTransparentMaterial)
            {
                // Transparent planes render BEFORE AR objects (lower queue)
                meshRenderer.material.renderQueue = 2900; // Before AR objects (3100)

                // Ensure transparency settings
                if (meshRenderer.material.HasProperty("_Mode"))
                {
                    meshRenderer.material.SetFloat("_Mode", 3); // Transparent mode
                }
                if (meshRenderer.material.HasProperty("_SrcBlend"))
                {
                    meshRenderer.material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    meshRenderer.material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                }
                if (meshRenderer.material.HasProperty("_ZWrite"))
                {
                    meshRenderer.material.SetFloat("_ZWrite", 0); // No depth write for transparency
                }
            }
            else
            {
                // Occlusion planes render before everything else (depth only)
                meshRenderer.material.renderQueue = 1999; // Geometry-1 queue

                // Ensure occlusion settings
                if (meshRenderer.material.HasProperty("_ZWrite"))
                {
                    meshRenderer.material.SetFloat("_ZWrite", 1); // Write depth
                }
                if (meshRenderer.material.HasProperty("_ColorMask"))
                {
                    meshRenderer.material.SetFloat("_ColorMask", 0); // No color output
                }
            }

            Debug.Log($"✅ Plane {plane.trackableId} visualized with {(useTransparentMaterial ? "transparent" : "occlusion")} material");
        }
        else
        {
            // Fallback to legacy material
            if (planeMaterial != null)
                meshRenderer.material = planeMaterial;
            meshRenderer.enabled = true;
            Debug.LogWarning($"⚠️ Using fallback material for plane {plane.trackableId}");
        }
    }

    /// <summary>
    /// Determine if a plane should be visualized based on height and occlusion settings
    /// </summary>
    private bool ShouldVisualizePlane(ARPlane plane)
    {
        if (!enableHeightFiltering) return true;

        // Get camera position for height comparison
        Camera arCamera = arCameraManager?.GetComponent<Camera>();
        if (arCamera == null) return true;

        Vector3 cameraPosition = arCamera.transform.position;
        Vector3 planePosition = plane.center;

        // Check if plane is above the maximum visualization height
        float heightDifference = planePosition.y - cameraPosition.y;

        if (heightDifference > maxVisualizationHeight)
        {
            Debug.Log($"🔍 Plane {plane.trackableId} is {heightDifference:F2}m above camera (max: {maxVisualizationHeight:F2}m) - filtering out");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determine if a plane should use transparent material based on visualization mode
    /// </summary>
    private bool ShouldUseTransparentMaterial(ARPlane plane)
    {
        switch (visualizationMode)
        {
            case PlaneVisualizationMode.TransparentOnly:
                return true;

            case PlaneVisualizationMode.OcclusionOnly:
                return false;

            case PlaneVisualizationMode.Smart:
                // Use transparent for ground planes, occlusion for overhead planes
                Camera arCamera = arCameraManager?.GetComponent<Camera>();
                if (arCamera == null) return true;

                Vector3 cameraPosition = arCamera.transform.position;
                Vector3 planePosition = plane.center;
                float heightDifference = planePosition.y - cameraPosition.y;

                // Ground planes (below or slightly above camera) use transparent material
                // Overhead planes use occlusion material
                return heightDifference <= 0.5f;

            case PlaneVisualizationMode.Disabled:
            default:
                return true;
        }
    }

    /// <summary>
    /// Get the appropriate material for a plane based on visualization settings
    /// </summary>
    private Material GetMaterialForPlane(ARPlane plane, bool useTransparent)
    {
        if (useTransparent && transparentPlaneMaterial != null)
        {
            return transparentPlaneMaterial;
        }
        else if (!useTransparent && occlusionPlaneMaterial != null)
        {
            return occlusionPlaneMaterial;
        }

        // Fallback to legacy material
        return planeMaterial;
    }

    /// <summary>
    /// Disable visualization for a specific plane
    /// </summary>
    private void DisablePlaneVisualization(ARPlane plane)
    {
        MeshRenderer meshRenderer = plane.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }
    }

    public void SetPlaneDetectionMode(PlaneDetectionMode mode)
    {
        detectionMode = mode;
        if (arPlaneManager != null)
        {
            arPlaneManager.requestedDetectionMode = mode;
            // Debug.Log($"🔧 Plane detection mode set to: {mode}");
        }
    }

    public void TogglePlaneVisualization(bool enable)
    {
        visualizePlanes = enable;

        foreach (var plane in arPlaneManager.trackables)
        {
            if (enable)
            {
                // Re-setup visualization with current settings
                SetupPlaneVisualization(plane);
            }
            else
            {
                // Disable visualization
                DisablePlaneVisualization(plane);
            }
        }

        Debug.Log($"👁️ Plane visualization: {(enable ? "ON" : "OFF")} with mode: {visualizationMode}");
    }

    /// <summary>
    /// Update visualization mode and refresh all planes
    /// </summary>
    public void SetVisualizationMode(PlaneVisualizationMode mode)
    {
        visualizationMode = mode;
        Debug.Log($"🎨 Plane visualization mode set to: {mode}");

        // Refresh all existing planes with new mode
        if (arPlaneManager != null)
        {
            foreach (var plane in arPlaneManager.trackables)
            {
                SetupPlaneVisualization(plane);
            }
        }
    }

    /// <summary>
    /// Set height filtering parameters and refresh visualization
    /// </summary>
    public void SetHeightFiltering(bool enable, float maxHeight = 2.0f)
    {
        enableHeightFiltering = enable;
        maxVisualizationHeight = maxHeight;

        Debug.Log($"� Height filtering: {(enable ? "ON" : "OFF")}, Max height: {maxHeight:F1}m");

        // Refresh all existing planes with new settings
        RefreshAllPlaneVisualizations();
    }

    /// <summary>
    /// Force refresh visualization for all existing planes
    /// </summary>
    public void RefreshAllPlaneVisualizations()
    {
        if (arPlaneManager != null)
        {
            int refreshedCount = 0;
            foreach (var plane in arPlaneManager.trackables)
            {
                SetupPlaneVisualization(plane);
                refreshedCount++;
            }
            Debug.Log($"🔄 Refreshed visualization for {refreshedCount} existing planes");
        }
    }

    public List<ARPlane> GetHorizontalPlanes()
    {
        List<ARPlane> horizontalPlanes = new List<ARPlane>();

        foreach (var plane in arPlaneManager.trackables)
        {
            if (plane.alignment == PlaneAlignment.HorizontalUp ||
                plane.alignment == PlaneAlignment.HorizontalDown)
            {
                horizontalPlanes.Add(plane);
            }
        }

        return horizontalPlanes;
    }

    public List<ARPlane> GetVerticalPlanes()
    {
        List<ARPlane> verticalPlanes = new List<ARPlane>();

        foreach (var plane in arPlaneManager.trackables)
        {
            if (plane.alignment == PlaneAlignment.Vertical)
            {
                verticalPlanes.Add(plane);
            }
        }

        return verticalPlanes;
    }

    public ARPlane GetLargestPlane()
    {
        ARPlane largestPlane = null;
        float largestArea = 0f;

        foreach (var plane in arPlaneManager.trackables)
        {
            float area = plane.size.x * plane.size.y;
            if (area > largestArea)
            {
                largestArea = area;
                largestPlane = plane;
            }
        }

        return largestPlane;
    }

    public bool IsPlaneStable(ARPlane plane, float timeThreshold = 2f)
    {
        if (_trackedPlanes.TryGetValue(plane.trackableId, out PlaneData data))
        {
            return Time.time - data.firstDetectionTime > timeThreshold;
        }
        return false;
    }

    private void OnDestroy()
    {
        if (arPlaneManager != null)
        {
            arPlaneManager.trackablesChanged.RemoveListener(OnPlanesChanged);
        }
    }


}
