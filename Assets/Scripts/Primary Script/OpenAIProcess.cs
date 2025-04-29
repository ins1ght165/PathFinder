using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System;
using Newtonsoft.Json.Linq;

public class OpenAIImageProcessor : MonoBehaviour
{
    int targetWidth = 256;
    int targetHeight = 256;

    public string googleApiKey = APIManager.GoogleGeminiApiKey;

    public Renderer displayRenderer; 
    public WebcamCaptureVR webcamCaptureVR; 

    private Texture2D captureTexture;
    private string apiUrl = "https://generativelanguage.googleapis.com/v1/models/gemini-1.5-pro:generateContent";
    private bool isProcessing = false;

    void Start()
    {
        if (displayRenderer == null)
        {
            Debug.LogError("❌ Display Renderer is not assigned!");
            return;
        }

        if (webcamCaptureVR == null)
        {
            Debug.LogError("❌ WebcamCaptureVR script is not assigned!");
            return;
        }
    }

    public void CaptureAndSendImage()
    {
        if (!isProcessing)
        {
            StartCoroutine(SendImageToGemini());
        }
        else
        {
            Debug.LogWarning("⚠ Already processing a request, ignoring new request.");
        }
    }

    public bool testingFailover = false;

    IEnumerator SendImageToGemini()
    {
        isProcessing = true;
        yield return new WaitForEndOfFrame();

        RenderTexture renderTexture = new RenderTexture(targetWidth, targetHeight, 24);
        RenderTexture.active = renderTexture;
        Graphics.Blit(displayRenderer.material.mainTexture, renderTexture);

        captureTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
        captureTexture.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        captureTexture.Apply();
        RenderTexture.active = null;

        byte[] imageBytes = captureTexture.EncodeToPNG();
        string base64Image = Convert.ToBase64String(imageBytes);

        Debug.Log($"📸 Image captured! Base64 Length: {base64Image.Length}");

        string jsonPayload = @"
    {
      ""contents"": [
        {
          ""parts"": [
            {
              ""text"": ""Describe this room for a visually impaired user.""
            },
            {
              ""inlineData"": {
                ""mimeType"": ""image/png"",
                ""data"": """ + base64Image + @"""
              }
            }
          ]
        }
      ]
    }";

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

       
        string urlToUse = $"{apiUrl}?key={googleApiKey}";

        if (testingFailover)
        {
            urlToUse = "https://this-api-will-fail.com/fake_endpoint"; // Broken URL to simulate failure
            Debug.LogWarning("⚡ TEST MODE: Using broken Gemini URL to simulate failure!");
        }
       

        using (UnityWebRequest request = new UnityWebRequest(urlToUse, "POST")) 
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"📡 Sending request to: {urlToUse}");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ Gemini Response: " + request.downloadHandler.text);
                ProcessAndSendToTTS(request.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"❌ Error sending to Gemini Vision: {request.responseCode} - {request.error}");

                // 🛟 Fallback to your Colab captioning server
                Debug.LogWarning("⚠ Falling back to Colab captioning server...");
                StartCoroutine(webcamCaptureVR.SendFrameForCaptioning());
            }
        }

        isProcessing = false;
    }



    void ProcessAndSendToTTS(string jsonResponse)
    {
        Debug.Log($"🟢 Raw Gemini Response: {jsonResponse}");

        try
        {
            JObject responseJson = JObject.Parse(jsonResponse);

            string description = responseJson["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

            if (!string.IsNullOrEmpty(description))
            {
                Debug.Log($"📖 Gemini Room Description: {description}");
                StartCoroutine(webcamCaptureVR.SendTextToSpeech(description));
            }
            else
            {
                Debug.LogError("❌ Description is empty, cannot send to TTS!");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Failed to parse Gemini response: {e.Message}");
        }
    }

}
