using UnityEngine;
using System.Collections;
using static WebcamCaptureVR;
using System.Text;
using UnityEngine.Networking;

public class HandGuidanceController : MonoBehaviour
{
    public Transform leftHand;
    public Transform rightHand;
    public AudioSource audioSource;
    public AudioSource ttsAudioSource;
    public AudioClip beepClip;
    public Transform webcamScreen;

    private Vector3 targetPosition;
    private bool guiding = false;
    private Coroutine guidanceCoroutine;
    private WebcamCaptureVR webcamCapture;

    void Start()
    {
        webcamCapture = FindObjectOfType<WebcamCaptureVR>();

        if (webcamCapture == null)
        {
            Debug.LogError("❌ WebcamCaptureVR not found! Make sure it's in the scene.");
        }

        if (audioSource == null)
        {
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                Debug.LogError("❌ AudioSource is missing! Add an AudioSource component.");
                return;
            }
        }

        if (ttsAudioSource == null)
        {
            ttsAudioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.spatialBlend = 1.0f;
        ttsAudioSource.spatialBlend = 1.0f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log("🟢 Manual Test Guidance Triggered!");
            //TestHandGuidance();
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            GuideToObjectByName("tv");
        }

    }

    public void GuideHandToObject(Detection detectedObject)
    {
        targetPosition = ConvertDetectionToWorldPosition(detectedObject);

        targetPosition.y = Mathf.Max(targetPosition.y, 1.0f); // 1.0 = about chest height

        Debug.Log($"🎯 Guiding hand to object at {targetPosition}");

        if (guidanceCoroutine != null)
        {
            StopCoroutine(guidanceCoroutine);
        }

        guidanceCoroutine = StartCoroutine(ProvideGuidance());
    }

    IEnumerator ProvideGuidance()
    {
        guiding = true;
        float lastCueTime = Time.time;

        while (guiding)
        {
            float leftDistance = Vector3.Distance(leftHand.position, targetPosition);
            float rightDistance = Vector3.Distance(rightHand.position, targetPosition);
            float minDistance = Mathf.Min(leftDistance, rightDistance);

            if (minDistance < 0.1f)
            {
                Debug.Log("🤚 Hand reached the object!");
                StopGuidance();
                yield break;
            }

            float beepInterval = Mathf.Lerp(2.0f, 0.1f, Mathf.InverseLerp(1.5f, 0.05f, minDistance));
            float pitch = Mathf.Lerp(0.5f, 2.0f, Mathf.InverseLerp(1.5f, 0.05f, minDistance));
            float volume = Mathf.Lerp(0.2f, 1.0f, Mathf.InverseLerp(1.5f, 0.05f, minDistance));

            audioSource.pitch = pitch;
            audioSource.volume = volume;
            PlayBeepAtTarget();

            Debug.Log($"🔊 Beeping at interval: {beepInterval}s | Pitch: {pitch} | Volume: {volume}");

            if (Time.time - lastCueTime > 2.5f)
            {
                string cue = GetDirectionalCue();
                if (!string.IsNullOrEmpty(cue))
                {
                    Debug.Log($"🗣️ Verbal Cue: {cue}");
                    PlayVoiceCue(cue);
                    lastCueTime = Time.time;
                }
            }

            yield return new WaitForSeconds(beepInterval);
        }
    }

    void PlayBeepAtTarget()
    {
        GameObject tempAudioObject = new GameObject("TempBeep");
        tempAudioObject.transform.position = targetPosition;

        AudioSource tempSource = tempAudioObject.AddComponent<AudioSource>();
        tempSource.clip = beepClip;
        tempSource.spatialBlend = 1.0f;
        tempSource.pitch = audioSource.pitch;
        tempSource.volume = audioSource.volume;

        // ✅ Important distance-based hearing config
        tempSource.minDistance = 0.5f;
        tempSource.maxDistance = 6.0f;
        tempSource.rolloffMode = AudioRolloffMode.Linear;

        tempSource.Play();
        Destroy(tempAudioObject, beepClip.length);
    }


    string GetDirectionalCue()
    {
        Vector3 handPosition = (leftHand.position + rightHand.position) / 2f;
        Vector3 direction = targetPosition - handPosition;

        float horizontalThreshold = 0.1f;
        float verticalThreshold = 0.1f;
        float forwardThreshold = 0.1f;

        string cue = "";

        // Forward/Backward
        if (direction.z > forwardThreshold) cue = "Move forward";
        else if (direction.z < -forwardThreshold) cue = "Move backward";

        // Left/Right
        if (direction.x > horizontalThreshold) cue = "Move right";
        else if (direction.x < -horizontalThreshold) cue = "Move left";

        // Up/Down
        if (direction.y > verticalThreshold) cue = "Move up";
        else if (direction.y < -verticalThreshold) cue = "Move down";

        // Encourage if very close
        if (direction.magnitude < 0.3f)
            cue = "You're close";

        Debug.Log($"📏 Y Difference: {direction.y}");
        return cue;
    }


    void PlayVoiceCue(string text)
    {
        if (webcamCapture == null || string.IsNullOrEmpty(webcamCapture.serverUrl))
        {
            Debug.LogError("❌ TTS Server URL is missing or WebcamCaptureVR is not assigned.");
            return;
        }

        webcamCapture.StartCoroutine(SendTextToSpeech(text));
    }

    IEnumerator SendTextToSpeech(string text)
    {
        Debug.Log($"🔊 Sending text for TTS: {text}");

        if (string.IsNullOrEmpty(webcamCapture.serverUrl))
        {
            Debug.LogError("❌ TTS Server URL is empty.");
            yield break;
        }

        string jsonPayload = JsonUtility.ToJson(new TTSRequest { text = text });

        using (UnityWebRequest request = new UnityWebRequest(webcamCapture.serverUrl + "/text_to_speech", "POST"))
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
                StartCoroutine(PlayWavAudio(audioBytes));
            }
            else
            {
                Debug.LogError($"❌ Error Sending Text for TTS: {request.responseCode} - {request.error}");
            }
        }
    }

    IEnumerator PlayWavAudio(byte[] audioBytes)
    {
        Debug.Log("🎵 Converting TTS audio to AudioClip...");

        AudioClip clip = WavUtility.ToAudioClip(audioBytes, "TTS_Audio");

        if (clip == null)
        {
            Debug.LogError("❌ Failed to convert TTS audio to clip.");
            yield break;
        }

        Debug.Log($"🎵 Playing TTS Audio: {clip.length} seconds");

        ttsAudioSource.volume = 1.0f;
        ttsAudioSource.mute = false;
        ttsAudioSource.spatialBlend = 0f;

        ttsAudioSource.clip = clip;
        ttsAudioSource.Play();

        yield return new WaitForSeconds(clip.length);
    }

    public void StopGuidance()
    {
        if (guidanceCoroutine != null)
        {
            StopCoroutine(guidanceCoroutine);
            guidanceCoroutine = null;
        }
        guiding = false;
        audioSource.Stop();

        if (DebugObjectMarker.Instance != null)
        {
            DebugObjectMarker.Instance.HideMarker();
        }
    }

    public bool useFixedDebugMarker = false;  

    Vector3 ConvertDetectionToWorldPosition(Detection detection)
    {
        if (useFixedDebugMarker)
        {
            // Place marker 1.5 meters in front of the headset for testing
            Vector3 debugPosition = Camera.main.transform.position + Camera.main.transform.forward * 1.5f;
            Debug.Log($"🧪 Using fixed debug marker position: {debugPosition}");
            DebugObjectMarker.Instance.ShowMarkerAt(debugPosition);
            return debugPosition;
        }

        // Normal YOLO-based coordinate conversion
        float normalizedX = (detection.x1 + detection.x2) / 2f / 2560f;
        float normalizedY = 1f - (detection.y1 + detection.y2) / 2f / 1440f;

        Vector3 viewportPoint = new Vector3(normalizedX, normalizedY, 0f);
        Ray ray = Camera.main.ViewportPointToRay(viewportPoint);

        float boxHeight = detection.y2 - detection.y1;
        float depth = Mathf.Lerp(1.5f, 0.4f, Mathf.Clamp01(boxHeight / 384f));

        Vector3 worldPoint = ray.GetPoint(depth);
        Debug.Log($"📍 YOLO-based target world point: {worldPoint}");

        DebugObjectMarker.Instance.ShowMarkerAt(worldPoint);
        return worldPoint;
    }

    public void GuideToObjectByName(string objectName)
    {
        if (webcamCapture == null)
        {
            Debug.LogError("❌ WebcamCaptureVR not assigned.");
            return;
        }

        Detection target = webcamCapture.GetDetectedObject(objectName);

        if (target == null)
        {
            Debug.LogWarning($"⚠️ Object '{objectName}' not found in latest YOLO detections.");
            PlayVoiceCue($"I can't find a {objectName}");
            return;
        }

        Debug.Log($"🧭 Guiding to detected object: {objectName}");
        GuideHandToObject(target);
        PlayVoiceCue($"Guiding you to the {objectName}");
    }


}