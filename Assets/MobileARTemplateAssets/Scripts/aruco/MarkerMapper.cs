using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using static OpenCVForUnity.UnityIntegration.OpenCVARUtils;

public static class MarkerMapper
{
  public static List<MarkerExport> MapList(List<MarkerData> markers)
  {
    var exports = new List<MarkerExport>();

    foreach (var data in markers)
    {
      Vector2 screenPos = Vector2.zero;
      if (data.screenPoint.HasValue)
      {
        screenPos = data.screenPoint.Value;
      }

      var export = new MarkerExport
      {
        id = data.markerId.ToString(),
        screenPosition = new ScreenPosition
        {
          x = screenPos.x.ToString("F2"),
          y = screenPos.y.ToString("F2")
        },
        rotation = data.pose.HasValue ? new RotationExport
        {
          x = data.pose.Value.rotation.x.ToString(),
          y = data.pose.Value.rotation.y.ToString(),
          z = data.pose.Value.rotation.z.ToString(),
          angle = data.pose.Value.rotation.w.ToString()
        } : null,
        marker = new MarkerDetails
        {
          id = data.markerId.ToString(),
          corners = data.corners
                  .SelectMany(c => new[] { c.x.ToString(), c.y.ToString() })
                  .ToArray(),
          lastSeen = data.lastSeenTime.ToString()
        }
      };

      exports.Add(export);
    }

    return exports;
  }
}


[System.Serializable]
public class MarkerExport
{
  public string id;
  public ScreenPosition screenPosition;
  public RotationExport rotation;
  public MarkerDetails marker;
}

[System.Serializable]
public class ScreenPosition
{
  public string x;
  public string y;
}

[System.Serializable]
public class RotationExport
{
  public string x;
  public string y;
  public string z;
  public string angle;
}

[System.Serializable]
public class MarkerDetails
{
  public string id;
  public string[] corners;
  public string lastSeen;
}

public static class JsonHelper
{
  public static string ToJson<T>(T[] array, bool prettyPrint = false)
  {
    Wrapper<T> wrapper = new Wrapper<T> { aruco_data_list = array };
    return JsonUtility.ToJson(wrapper, prettyPrint);
  }

  [System.Serializable]
  private class Wrapper<T>
  {
    public T[] aruco_data_list;
  }
}


[System.Serializable]
public class MarkerData
{
  public int markerId;
  public PoseData? pose; // nullable
  public Vector3[] corners;
  public Vector2? screenPoint;
  public float lastSeenTime;
  public bool placedOnPlane;
  public GameObject gameObject; // nullable

  // public UnityEngine.XR.ARFoundation.ARAnchor anchor; // ✨ هنا نخزن الأنكور

}



[System.Serializable]
public class MarkerDataWrapper
{
  public List<MarkerData> markers;
}
