using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System.Text;
using UnityEngine.UI;
using Newtonsoft.Json;  
using System.Linq;


public class WebcamCaptureVR : MonoBehaviour
{
    public Camera vrCamera;
    public Renderer displayRenderer;
    public Transform webcamScreen;
    public RectTransform boundingBoxParent;
    public GameObject boundingBoxPrefab;

    private WebCamTexture webcamTexture;
    private Texture2D captureTexture;
    private string targetCameraName = "Anker PowerConf C200";

    [Range(0.1f, 3.0f)] public float screenDistance = 2.0f;
    private float baseScale = 2.0f;
    private float headsetFOV = 105f;
    private float webcamFOV = 95f;

    public string serverUrl = "";
    public string yoloUrl = ""; // ✅ NEW: Separate URL for YOLO
    private DatabaseReference dbReference;
    public string apiKey = APIManager.FirebaseApiKey;

    private bool isProcessing = false;

    private List<Detection> latestDetections = new List<Detection>(); // ✅ Stores latest detections

    void Start()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        Debug.Log("Available Webcams: " + devices.Length);

        string selectedCamera = "";
        foreach (var device in devices)
        {
            Debug.Log($"Camera: {device.name}");
            if (device.name.Contains(targetCameraName))
            {
                selectedCamera = device.name;
            }
        }

        if (!string.IsNullOrEmpty(selectedCamera))
        {
            Debug.Log($"Using Camera: {selectedCamera}");
            webcamTexture = new WebCamTexture(selectedCamera, 1920, 1080, 30);
            displayRenderer.material.mainTexture = webcamTexture;
            webcamTexture.Play();
            captureTexture = new Texture2D(webcamTexture.width, webcamTexture.height, TextureFormat.RGB24, false);
        }
        else
        {
            Debug.LogError($"Camera '{targetCameraName}' not found!");
        }

        FetchCloudflareURLs();
        InvokeRepeating("CaptureAndSendFrame", 1f, 1.5f); // ✅ Send every 1.5 sec
    }

    void FetchCloudflareURLs()
    {
        dbReference = FirebaseDatabase.DefaultInstance.RootReference;

        FirebaseDatabase.DefaultInstance.GetReference("cloudflare_url").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && task.Result != null)
            {
                serverUrl = task.Result.Value.ToString();
                Debug.Log($"✅ Captioning/TTS URL Retrieved: {serverUrl}");
            }
            else
            {
                Debug.LogError("❌ Failed to fetch captioning/TTS URL from Firebase.");
            }
        });

        FirebaseDatabase.DefaultInstance.GetReference("cloudflare_yolo_url").GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted && task.Result != null)
            {
                yoloUrl = task.Result.Value.ToString();
                Debug.Log($"✅ YOLO URL Retrieved: {yoloUrl}");
            }
            else
            {
                Debug.LogError("❌ Failed to fetch YOLO URL from Firebase.");
            }
        });
    }

    void Update()
    {
        if (webcamScreen != null && vrCamera != null)
        {
            webcamScreen.position = vrCamera.transform.position + vrCamera.transform.forward * screenDistance;
            webcamScreen.rotation = vrCamera.transform.rotation;

            float aspectRatio = (float)webcamTexture.width / (float)webcamTexture.height;
            float scaleFactor = screenDistance / 2.0f;
            webcamScreen.localScale = new Vector3(baseScale * scaleFactor * aspectRatio, baseScale * scaleFactor, 1);
            vrCamera.fieldOfView = Mathf.Lerp(webcamFOV, headsetFOV, (screenDistance - 0.5f) / 2.0f);
        }

        // ✅ Trigger captioning
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("🚀 Spacebar Pressed! Capturing and sending frame for captioning.");
            StartCoroutine(SendFrameForCaptioning());
        }

        // ✅ Trigger YOLO detection
        if (Input.GetKeyDown(KeyCode.Y))
        {
            Debug.Log("🟡 Y key pressed! Sending frame to YOLO.");
            StartCoroutine(SendFrameToYolo());
        }
    }

    public IEnumerator SendFrameToYolo()
    {
        if (string.IsNullOrEmpty(yoloUrl))
        {
            Debug.LogError("❌ YOLO URL is empty.");
            yield break;
        }

        if (webcamTexture == null || !webcamTexture.isPlaying)
        {
            Debug.LogError("❌ Webcam is not running!");
            yield break;
        }

        // ✅ Fresh capture here
        captureTexture.SetPixels(webcamTexture.GetPixels());
        captureTexture.Apply();

        byte[] imageBytes = captureTexture.EncodeToPNG();

        using (UnityWebRequest request = new UnityWebRequest(yoloUrl + "/detect", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(imageBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/octet-stream");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ YOLO Response: " + request.downloadHandler.text);
                ProcessDetectionResults(request.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"❌ YOLO Error: {request.responseCode} - {request.error}");
            }
        }
    }

    public void TriggerYoloDetection()
    {
        Debug.Log("🟡 Triggered YOLO detection (keyboard or voice)");
        StartCoroutine(SendFrameToYolo());
    }




    void CaptureAndSendFrame()
    {
        // Still capturing the frame but not sending it
        captureTexture.SetPixels(webcamTexture.GetPixels());
        captureTexture.Apply();

        // Commented out the sending part
        // if (!string.IsNullOrEmpty(serverUrl) && !isProcessing)
        // {
        //     StartCoroutine(SendFrameToServer());
        // }
        // ✅ New coroutine for captioning

        Debug.Log("✅ Frame captured and sent for captioning & TTS.");

        Debug.Log("✅ Frame captured for captioning & TTS but NOT sent to YOLO.");
    }

    public IEnumerator SendFrameForCaptioning()
    {
        if (string.IsNullOrEmpty(serverUrl))
        {
            Debug.LogError("❌ Captioning Server URL is empty.");
            yield break;
        }

        Debug.Log($"📡 Sending frame to: {serverUrl}/describe_image");

        // Convert frame to PNG bytes
        byte[] imageBytes = captureTexture.EncodeToPNG();

        using (UnityWebRequest request = new UnityWebRequest(serverUrl + "/describe_image", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(imageBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/octet-stream");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                Debug.Log("✅ Captioning Response: " + jsonResponse);
                ProcessCaptioningResponse(jsonResponse);
            }
            else
            {
                Debug.LogError($"❌ Error Sending Frame for Captioning: {request.responseCode} - {request.error}");
            }
        }
    }

    void ProcessCaptioningResponse(string jsonResponse)
    {
        Debug.Log($"📝 Captioning Response: {jsonResponse}");

        try
        {
            // Parse the JSON response
            var captionData = JsonUtility.FromJson<CaptionResponse>(jsonResponse);
            string captionText = captionData.description;

            Debug.Log($"📖 Caption Generated: {captionText}");

            // Send the caption text to TTS
            StartCoroutine(SendTextToSpeech(captionText));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ JSON Parsing Error (Captioning): {e.Message}");
        }
    }

    // JSON Response Model for Captioning
    [System.Serializable]
    public class CaptionResponse
    {
        public string description;
    }


    public IEnumerator SendTextToSpeech(string text)
    {
        Debug.Log($"🔊 Sending text for TTS: {text}");

        if (string.IsNullOrEmpty(serverUrl))
        {
            Debug.LogError("❌ TTS Server URL is empty.");
            yield break;
        }

        // Convert text to JSON
        string jsonPayload = JsonUtility.ToJson(new TTSRequest { text = text });

        using (UnityWebRequest request = new UnityWebRequest(serverUrl + "/text_to_speech", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                byte[] audioBytes = request.downloadHandler.data;
                Debug.Log("✅ TTS Response Received. Playing audio...");
                StartCoroutine(PlayWavAudio(audioBytes));  // Play the WAV file
            }
            else
            {
                Debug.LogError($"❌ Error Sending Text for TTS: {request.responseCode} - {request.error}");
            }
        }
    }

    // Function to Convert and Play Audio in Unity
    IEnumerator PlayWavAudio(byte[] audioBytes)
    {
        Debug.Log("🎵 Playing generated speech...");

        AudioClip clip = WavUtility.ToAudioClip(audioBytes, "TTS_Audio");

        if (clip == null)
        {
            Debug.LogError("❌ Failed to convert TTS audio to clip.");
            yield break;
        }

        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.clip = clip;
        audioSource.Play();
        yield return new WaitForSeconds(clip.length);
    }



    // JSON Request Model for TTS
    [System.Serializable]
    public class TTSRequest
    {
        public string text;
    }



    // JSON Response Model for TTS
    [System.Serializable]
    public class TTSResponse
    {
        public string audio;
    }

    // Coroutine to Convert and Play Audio







    IEnumerator SendFrameToServer()
    {
        isProcessing = true;
        yield return new WaitForEndOfFrame();

        if (string.IsNullOrEmpty(serverUrl))
        {
            Debug.LogError("❌ Server URL is empty.");
            isProcessing = false;
            yield break;
        }

        Debug.Log($"📡 Sending frame to: {serverUrl}/detect");
        captureTexture.SetPixels(webcamTexture.GetPixels());
        captureTexture.Apply();
        byte[] imageBytes = captureTexture.EncodeToPNG();

        using (UnityWebRequest request = new UnityWebRequest(serverUrl + "/detect", "POST"))
        {
            byte[] authBytes = Encoding.UTF8.GetBytes(apiKey);
            string encodedAuth = System.Convert.ToBase64String(authBytes);
            request.SetRequestHeader("Authorization", "Basic " + encodedAuth);
            request.uploadHandler = new UploadHandlerRaw(imageBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/octet-stream");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ YOLO Response: " + request.downloadHandler.text);
                ProcessDetectionResults(request.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"❌ Error Sending Frame: {request.responseCode} - {request.error}");
            }
        }

        isProcessing = false;
        yield return new WaitForSeconds(0.3f);
    }

    void ProcessDetectionResults(string jsonResponse)
    {
        Debug.Log($"🟢 Raw JSON Response from Server: {jsonResponse}");

        try
        {
            DetectionResponse detections = JsonConvert.DeserializeObject<DetectionResponse>(jsonResponse);
            latestDetections = detections?.detections ?? new List<Detection>(); // Store latest detections

            if (latestDetections.Count == 0)
            {
                Debug.Log("❌ No detections received!");
                return;
            }

            Debug.Log($"✅ Parsed {latestDetections.Count} objects!");
            foreach (var det in latestDetections)
            {
                Debug.Log($"🔍 Detected: {det.@class} at ({det.x1}, {det.y1}) -> ({det.x2}, {det.y2})");
            }

            Debug.Log($"✅ Parsed {latestDetections.Count} objects!");
            DrawBoundingBoxes(detections);
            AnnounceDetections();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ JSON Parsing Error: {e.Message}");
        }
    }

    void AnnounceDetections()
    {
        if (latestDetections == null || latestDetections.Count == 0)
        {
            Debug.LogWarning("⚠ No detections to announce.");
            return;
        }

        List<string> objectNames = new List<string>();

        foreach (var detection in latestDetections)
        {
            if (!objectNames.Contains(detection.@class.ToLower()))
            {
                objectNames.Add(detection.@class.ToLower()); 
            }
        }

        string announcement = "";

        if (objectNames.Count == 1)
        {
            announcement = $"I see a {objectNames[0]} in front of you.";
        }
        else
        {
            announcement = $"I see {string.Join(", ", objectNames.Take(objectNames.Count - 1))} and {objectNames.Last()} in front of you.";
        }

        Debug.Log($"🗣️ Announcing: {announcement}");

        StartCoroutine(SendTextToSpeech(announcement));
    }


    void DrawBoundingBoxes(DetectionResponse response)
    {
        foreach (Transform child in boundingBoxParent)
        {
            Destroy(child.gameObject);
        }

        foreach (Detection detection in response.detections)
        {
            GameObject newBox = Instantiate(boundingBoxPrefab, boundingBoxParent);
            RectTransform rt = newBox.GetComponent<RectTransform>();

            float x = detection.x1 / 640f;
            float y = detection.y1 / 384f;
            float width = (detection.x2 - detection.x1) / 640f;
            float height = (detection.y2 - detection.y1) / 384f;

            rt.anchorMin = new Vector2(x, 1 - y - height);
            rt.anchorMax = new Vector2(x + width, 1 - y);

            Debug.Log($"🟩 Bounding Box Drawn for {detection.@class} at ({x}, {y})");
        }
    }

    // Function to Get a Specific Object for Voice Commands
    public Detection GetDetectedObject(string objectName)
    {
        return latestDetections.Find(d => d.@class.ToLower() == objectName.ToLower());
    }

    public byte[] GetLatestFrameBytes()
    {
        if (captureTexture == null) return null;
        return captureTexture.EncodeToPNG();
    }

}

[System.Serializable]
public class Detection
{
    public string @class;
    public int x1;
    public int y1;
    public int x2;
    public int y2;
}



[System.Serializable]
public class DetectionResponse
{
    public List<Detection> detections;
}



