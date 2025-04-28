using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;

public class FirebaseInitializer : MonoBehaviour
{
    void Awake()  // Ensure Firebase initializes before other scripts run
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                FirebaseApp app = FirebaseApp.DefaultInstance;
                Debug.Log("✅ Firebase Initialized Successfully!");
            }
            else
            {
                Debug.LogError("❌ Firebase Initialization Failed: " + task.Result);
            }
        });
    }
}
