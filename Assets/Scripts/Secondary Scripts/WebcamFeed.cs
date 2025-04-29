using UnityEngine;

public class WebcamVRFilter : MonoBehaviour
{
    public Camera vrCamera;  // Assign `CenterEyeAnchor (Camera)`
    private WebCamTexture webcamTexture;
    public Material webcamMaterial;  // Assign `WebcamVRMaterial` (with the shader)
    private string targetCameraName = "Anker PowerConf C200"; // Change this to your webcam name

    void Start()
    {
        // Get available webcams
        WebCamDevice[] devices = WebCamTexture.devices;
        Debug.Log("Available Webcams: " + devices.Length);

        string selectedCamera = "";
        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log($"Camera {i}: {devices[i].name}");
            if (devices[i].name.Contains(targetCameraName))
            {
                selectedCamera = devices[i].name;
            }
        }

        if (selectedCamera != "")
        {
            Debug.Log($"Using Camera: {selectedCamera}");

            // Initialize webcam feed
            int width = 1920;  // Use higher resolution for better quality
            int height = 1080;
            int fps = 30;

            webcamTexture = new WebCamTexture(selectedCamera, width, height, fps);
            webcamMaterial.mainTexture = webcamTexture;  // Apply texture to material
            webcamTexture.Play();

            Debug.Log($"Webcam Resolution Set: {webcamTexture.width}x{webcamTexture.height}");
        }
        else
        {
            Debug.LogError($"Camera '{targetCameraName}' not found!");
        }

        // Apply the material to the VR camera
        vrCamera.clearFlags = CameraClearFlags.SolidColor;
        vrCamera.backgroundColor = Color.black;
        vrCamera.gameObject.GetComponent<Camera>().targetTexture = null;
        vrCamera.gameObject.GetComponent<Renderer>().material = webcamMaterial;
    }
}
