
using UnityEngine;

public class DebugObjectMarker : MonoBehaviour
{
    public static DebugObjectMarker Instance;
    public GameObject markerPrefab;  // Assign a simple sphere prefab in the Inspector
    private GameObject currentMarker;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void ShowMarkerAt(Vector3 worldPosition)
    {
        if (markerPrefab == null)
        {
            Debug.LogWarning("⚠️ No marker prefab assigned.");
            return;
        }

        if (currentMarker == null)
        {
            currentMarker = Instantiate(markerPrefab, worldPosition, Quaternion.identity);
        }
        else
        {
            currentMarker.transform.position = worldPosition;
        }

        currentMarker.SetActive(true);
    }

    public void HideMarker()
    {
        if (currentMarker != null)
        {
            currentMarker.SetActive(false);
        }
    }
}
