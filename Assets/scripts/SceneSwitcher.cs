using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class SceneSwitcher : MonoBehaviour
{
    void Update()
    {
        // Raccourci 1 : Manettes Quest (Bouton B ou Y) pour le développement/test rapide
        if (OVRInput.GetDown(OVRInput.Button.Two))
        {
            ToggleScene();
        }
        
        // Raccourci 2 : Clavier dans l'Éditeur (Touche Espace) pour les tests rapides PC
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            ToggleScene();
        }
    }

    /// <summary>
    /// Méthode publique utilisable par la Méthode A (Boutons UI Virtuels).
    /// Charge directement la scène simple.
    /// </summary>
    public void LoadPenduleSimple()
    {
        SceneManager.LoadScene("Pendule_simple");
    }

    /// <summary>
    /// Méthode publique utilisable par la Méthode A (Boutons UI Virtuels).
    /// Charge directement la scène de pendules couplés.
    /// </summary>
    public void LoadPenduleCouples()
    {
        SceneManager.LoadScene("Pendule_couples");
    }

    /// <summary>
    /// Méthode publique utilisable par la Méthode A (Boutons UI Virtuels) ou par les raccourcis.
    /// Bascule automatiquement sur l'autre scène.
    /// </summary>
    public void ToggleScene()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        
        if (currentScene == "Pendule_simple")
        {
            SceneManager.LoadScene("Pendule_couples");
        }
        else
        {
            SceneManager.LoadScene("Pendule_simple");
        }
    }
}
