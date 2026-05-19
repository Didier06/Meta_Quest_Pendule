using UnityEngine;

public class PendulumMqttPublisher : MonoBehaviour
{
    [Header("Configuration MQTT")]
    public MqttManager mqttManager;
    public string publishTopic = "FABLAB_21_22/Unity/meta/pendule/out/";

    [Header("Paramètres du Pendule")]
    [Tooltip("Nombre de messages envoyés par seconde")]
    public float messagesParSeconde = 20f;
    [Tooltip("Coche cette case pour voir l'angle dans la console (désactive-la une fois que ça marche pour ne pas spammer)")]
    public bool afficherDansConsole = true;

    [Header("Physique (Réception MQTT)")]
    [Tooltip("Glisse ici l'objet Pivot qui contient le HingeJoint")]
    public HingeJoint pivotJoint;
    [Tooltip("Durée de la remontée en douceur (en secondes)")]
    public float dureeRemontee = 3.0f;

    // Variable pour suivre le temps depuis le début du jeu
    private float tempsEcoule = 0f;
    private float timer = 0f;
    private bool estEnRemontee = false;

    // Mémoire des dernières valeurs
    private float lastAngInit = 0f;
    private float lastAlpha = 0.1f;

    void Update()
    {
        // On continue de publier l'angle en temps réel, même pendant qu'on le remonte !
        // Mais on fige l'horloge interne pour qu'elle parte bien de t=0 au moment du lâcher.
        if (!estEnRemontee)
        {
            tempsEcoule += Time.deltaTime;
        }

        // Timer pour limiter la fréquence d'envoi
        timer += Time.deltaTime;
        float intervalle = 1f / messagesParSeconde;

        if (timer >= intervalle)
        {
            timer = 0f;
            EnvoyerDonnees();
        }
    }

    void EnvoyerDonnees()
    {
        if (mqttManager != null)
        {
            // Calcul robuste de l'angle X sans l'inversion d'axes d'Unity (Gimbal Lock au-delà de 90°)
            // Au lieu de lire localEulerAngles.x qui "rebondit" à 90°, on projette le vecteur "Haut" local.
            Vector3 localUp = transform.localRotation * Vector3.up;
            float angle = Mathf.Atan2(localUp.z, localUp.y) * Mathf.Rad2Deg;

            // Le nom du pendule est récupéré depuis l'objet parent principal (ex: pendule_simple)
            string nomPendule = transform.root.name;

            // Construire le message JSON
            // Utilise la culture invariante (Replace) pour garantir un point au lieu d'une virgule
            string messageJson = $"{{\"id\":\"{nomPendule}\", \"angle\":{angle.ToString("F3").Replace(",", ".")}, \"temps\":{tempsEcoule.ToString("F3").Replace(",", ".")}}}";

            // Envoyer le message
            mqttManager.PublishCustom(publishTopic, messageJson);

            // Afficher dans la console si l'option est cochée
            if (afficherDansConsole)
            {
                Debug.Log($"[Pendule {nomPendule}] Angle envoyé : {angle:F1}° | JSON : {messageJson}");
            }
        }
    }

    // Fonction appelée automatiquement par MqttManager lorsqu'un message JSON contenant ang_init, alpha ou m est reçu
    public void OnMqttReset(MqttManager.ObjectTransformData data)
    {
        // Reset tempsEcoule et timer immédiatement
        tempsEcoule = 0f;
        timer = 0f;

        // Mémorisation des valeurs si elles sont fournies (différentes de -999)
        if (data.ang_init != -999f) lastAngInit = data.ang_init;
        if (data.alpha != -999f) lastAlpha = data.alpha;

        string masseAffichee = data.m != -999f ? data.m.ToString() : "inchangée";
        // Debug.Log($"Reçu commande de reset pour {gameObject.name} : ang_init={lastAngInit}, alpha={lastAlpha}, m={masseAffichee}");
        StopAllCoroutines();
        StartCoroutine(RemonteeDouce(data));
    }

    private System.Collections.IEnumerator RemonteeDouce(MqttManager.ObjectTransformData data)
    {
        estEnRemontee = true;
        
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (data.m != -999f && data.m > 0) rb.mass = data.m;

            // On fige la physique
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // On récupère la rotation de base pour éviter les inversions d'axes de Unity (Gimbal Lock)
            Vector3 baseEuler = transform.localEulerAngles;
            Quaternion startRot = transform.localRotation;
            Quaternion endRot = Quaternion.Euler(lastAngInit, baseEuler.y, baseEuler.z);

            // On cherche la position exacte du pivot dans le monde
            Vector3 pivotWorldPos = transform.position;
            if (pivotJoint != null)
            {
                pivotWorldPos = pivotJoint.transform.TransformPoint(pivotJoint.anchor);
            }
            // On calcule où se trouve ce pivot dans le repère local du pendule (cette coordonnée doit rester constante)
            Vector3 localAnchorOnPendule = transform.InverseTransformPoint(pivotWorldPos);

            float elapsed = 0f;

            // Boucle d'animation fluide absolue (pas d'incrémentation = ZÉRO dérive)
            while (elapsed < dureeRemontee)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / dureeRemontee);
                
                // 1. On applique la rotation parfaite via Slerp (évite les inversions de signe)
                transform.localRotation = Quaternion.Slerp(startRot, endRot, t);

                // 2. On repositionne l'objet pour que l'ancre locale coïncide EXACTEMENT avec le pivot mondial
                transform.position = pivotWorldPos - transform.TransformVector(localAnchorOnPendule);
                
                yield return null;
            }

            // Sécurité de fin : on s'assure d'être exactement à l'angle demandé
            transform.localRotation = endRot;
            transform.position = pivotWorldPos - transform.TransformVector(localAnchorOnPendule);

            // On relâche le pendule
            rb.isKinematic = false;
            rb.WakeUp();
        }

        // Appliquer le frottement visqueux (Damper)
        if (pivotJoint != null)
        {
            pivotJoint.useSpring = true;
            JointSpring spring = pivotJoint.spring;
            spring.spring = 0f; 
            spring.targetPosition = 0f;
            spring.damper = lastAlpha;
            pivotJoint.spring = spring;
            
            // Debug.Log($"[Physique] HingeJoint mis à jour : Damper = {pivotJoint.spring.damper}");
        }
        else
        {
            Debug.LogError("ATTENTION : La variable 'Pivot Joint' n'est pas assignée !");
        }

        // Attendre que le moteur physique effectue la première mise à jour (FixedUpdate)
        // après la libération du Rigidbody kinematic, pour s'assurer que le pendule a commencé
        // à bouger avant de démarrer l'acquisition des données à t=0.
        yield return new WaitForFixedUpdate();

        tempsEcoule = 0f;
        timer = 0f;
        estEnRemontee = false;
    }
}
