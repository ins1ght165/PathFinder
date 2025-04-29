using UnityEngine;
using UnityEngine.Windows.Speech;
using System.Collections.Generic;
using System.Linq;

public class VoiceCommandProcessor : MonoBehaviour
{
    private KeywordRecognizer keywordRecognizer;
    private Dictionary<string, System.Action> voiceCommands = new Dictionary<string, System.Action>();

    public OpenAIImageProcessor aiProcessor;
    public HandGuidanceController handGuidanceController;
    public WebcamCaptureVR webcamCapture;

    void Start()
    {
        if (aiProcessor == null)
        {
            Debug.LogError("❌ AI Processor not assigned!");
            return;
        }

        if (handGuidanceController == null)
        {
            Debug.LogError("❌ Hand Guidance Controller not assigned!");
            return;
        }

        // Predefined commands
        voiceCommands.Add("describe the room for me", () => aiProcessor.CaptureAndSendImage());
        voiceCommands.Add("what is currently in front of me", () => webcamCapture.TriggerYoloDetection());
        voiceCommands.Add("stop", () => handGuidanceController.StopGuidance());
        voiceCommands.Add("help", ListAvailableCommands);


        void ListAvailableCommands()
        {
            Debug.Log("🗣 Listing available voice commands...");

            string availableCommands = "You can say: ";

            bool guideCommandAdded = false;

            foreach (var cmd in voiceCommands.Keys)
            {
                if (cmd.StartsWith("guide me to"))
                {
                    if (!guideCommandAdded)
                    {
                        availableCommands += "guide me to an object, ";
                        guideCommandAdded = true;
                    }
                    // Skiping  multiple guide commands since it's just redundant
                }
                else
                {
                    availableCommands += cmd + ", ";
                }
            }

            availableCommands = availableCommands.TrimEnd(',', ' '); 
            Debug.Log($"📖 Available Commands: {availableCommands}");

            if (webcamCapture != null)
            {
                webcamCapture.StartCoroutine(webcamCapture.SendTextToSpeech(availableCommands));
            }
            else
            {
                Debug.LogWarning("⚠️ WebcamCaptureVR not assigned for TTS playback.");
            }
        }



        
        string[] objectNames = { "tv", "bottle", "bed", "chair", "laptop", "person", "couch"}; 

        foreach (string objName in objectNames)
        {
            string command = $"guide me to the {objName}";
            voiceCommands.Add(command, () => handGuidanceController.GuideToObjectByName(objName));
        }

        keywordRecognizer = new KeywordRecognizer(voiceCommands.Keys.ToArray());
        keywordRecognizer.OnPhraseRecognized += OnVoiceCommandRecognized;
        keywordRecognizer.Start();
    }

    void OnVoiceCommandRecognized(PhraseRecognizedEventArgs args)
    {
        Debug.Log($"🎤 Voice Command Recognized: {args.text}");

        if (voiceCommands.TryGetValue(args.text.ToLower(), out System.Action action))
        {
            action.Invoke();
        }
        else
        {
            Debug.LogWarning($"⚠ No action mapped for: {args.text}");
        }
    }
}
