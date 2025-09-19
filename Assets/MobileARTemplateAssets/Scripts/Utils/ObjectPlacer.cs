using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class ObjectPlacer : MonoBehaviour
{
    [SerializeField] private ARRaycastManager raycastManager;

    private List<ARRaycastHit> hits = new List<ARRaycastHit>();

    /// <summary>
    /// يسقط أي GameObject على أقرب plane تحته
    /// </summary>
    public void SnapObjectToPlane(GameObject arGameObject)
    {
        if (arGameObject == null || raycastManager == null || Camera.main == null)
            return;

        // ناخذ مكان الـ object في الشاشة
        Vector3 objectScreenPos = Camera.main.WorldToScreenPoint(arGameObject.transform.position);
        Vector2 screenPoint = new Vector2(objectScreenPos.x, objectScreenPos.y);

        // نعمل Raycast على الـ plane
        if (raycastManager.Raycast(screenPoint, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            // نثبت الـ object في مكان الـ plane
            arGameObject.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
        }
    }
}
