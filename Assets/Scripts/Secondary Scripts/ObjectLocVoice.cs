using UnityEngine;
using UnityEngine.Windows.Speech;
using System.Linq;
using System.Collections.Generic;

public class VoiceCommandFinder : MonoBehaviour
{
    private KeywordRecognizer keywordRecognizer;
    private Dictionary<string, string> objectCommands = new Dictionary<string, string>
    {
        { "find me the bottle", "bottle" },
        { "find me the phone", "phone" },
        { "find me the laptop", "laptop" },
        { "find me the chair", "chair" }
    };

    public WebcamCaptureVR webcamCapture; // ✅ Ensure this is assigned in Inspector

    void Start()
    {
        if (webcamCapture == null)
        {
            Debug.LogError("❌ WebcamCaptureVR reference is missing!");
            return;
        }

        Debug.Log("🎤 Initializing VoiceCommandFinder...");

        keywordRecognizer = new KeywordRecognizer(objectCommands.Keys.ToArray());
        keywordRecognizer.OnPhraseRecognized += OnVoiceCommandRecognized;
        keywordRecognizer.Start();

        Debug.Log("✅ VoiceCommandFinder Recognizer Started!");
    }

    void OnVoiceCommandRecognized(PhraseRecognizedEventArgs args)
    {
        string spokenCommand = args.text;
        Debug.Log($"🎤 VoiceCommandFinder Recognized: '{spokenCommand}'");

        if (objectCommands.ContainsKey(spokenCommand))
        {
            string detectedObject = objectCommands[spokenCommand];
            Debug.Log($"🔍 Extracted Object Name: {detectedObject}");

            Detection foundObject = webcamCapture.GetDetectedObject(detectedObject);
            if (foundObject != null)
            {
                Debug.Log($"✅ Object '{detectedObject}' found at ({foundObject.x1}, {foundObject.y1})");
                // ✅ Play sound or guide hand
            }
            else
            {
                Debug.LogError($"❌ Object '{detectedObject}' not found!");
            }
        }
        else
        {
            Debug.LogError("❌ Command not recognized!");
        }
    }
}
