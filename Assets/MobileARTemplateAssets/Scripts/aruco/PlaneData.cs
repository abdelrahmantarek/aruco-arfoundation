using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[System.Serializable]
public class PlaneData
{
    [Header("Plane Info")]
    public TrackableId trackableId;
    public PlaneAlignment alignment;
    public PlaneClassifications classifications;

    [Header("Geometry")]
    public Vector2 size;
    public Vector3 center;
    public Vector3 normal;
    public float area;

    [Header("Tracking")]
    public float firstDetectionTime;
    public float lastUpdateTime;
    public int updateCount;
    public bool isStable;

    [Header("State")]
    public TrackingState trackingState;
    public bool isSubsumed;

    public PlaneData(ARPlane plane)
    {
        UpdatePlane(plane);
        firstDetectionTime = Time.time;
        updateCount = 1;
    }

    public void UpdatePlane(ARPlane plane)
    {
        trackableId = plane.trackableId;
        alignment = plane.alignment;
        classifications = plane.classifications;

        size = plane.size;
        center = plane.center;
        normal = plane.normal;
        area = size.x * size.y;

        trackingState = plane.trackingState;
        isSubsumed = plane.subsumedBy != null;

        lastUpdateTime = Time.time;
        updateCount++;

        // Consider stable after 5 updates and 2 seconds
        isStable = updateCount >= 5 && (Time.time - firstDetectionTime) > 2f;
    }

    public bool IsHorizontal()
    {
        return alignment == PlaneAlignment.HorizontalUp ||
               alignment == PlaneAlignment.HorizontalDown;
    }

    public bool IsVertical()
    {
        return alignment == PlaneAlignment.Vertical;
    }

    public bool IsRecentlyUpdated(float timeThreshold = 0.5f)
    {
        return Time.time - lastUpdateTime < timeThreshold;
    }

    public override string ToString()
    {
        return $"Plane {trackableId}: {alignment} | Area: {area:F2}m² | " +
               $"Stable: {isStable} | Updates: {updateCount}";
    }
}
