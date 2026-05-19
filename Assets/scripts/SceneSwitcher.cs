using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class SceneSwitcher : MonoBehaviour
{
    void Update()
    {
        // Toggle scene using the 'B' or 'Y' button on the Meta Quest controllers (Button.Two)
        if (OVRInput.GetDown(OVRInput.Button.Two))
        {
            SwitchScene();
        }
        
        // Also allow keyboard testing in the Unity Editor (Spacebar) using the New Input System
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            SwitchScene();
        }
    }

    void SwitchScene()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        
        if (currentScene == "Pendule_simple")
        {
            SceneManager.LoadScene("Pendule_couples");
        }
        else
        {
            // By default, fallback to Pendule_simple
            SceneManager.LoadScene("Pendule_simple");
        }
    }
}
