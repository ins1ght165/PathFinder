using UnityEngine;
using System.Collections;
using System.IO;

public class CompositorCapture : MonoBehaviour
{
    private Texture2D captureTexture;
    private int frameCount = 0;

    void Start()
    {
        int width = Screen.width;
        int height = Screen.height;

        // Create a Texture2D to store the captured frame
        captureTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
    }

    void Update()
    {
        StartCoroutine(CaptureVRCompositorFrame());
    }

    IEnumerator CaptureVRCompositorFrame()
    {
        yield return new WaitForEndOfFrame();

        // Capture the entire compositor view
        ScreenCapture.CaptureScreenshotIntoRenderTexture(null);
        captureTexture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        captureTexture.Apply();

        // Save as PNG
        byte[] imageBytes = captureTexture.EncodeToPNG();
        string filePath = Application.persistentDataPath + "/passthroughFrame_" + frameCount.ToString("D4") + ".png";
        File.WriteAllBytes(filePath, imageBytes);

        Debug.Log("Saved: " + filePath);
        frameCount++;
    }
}
