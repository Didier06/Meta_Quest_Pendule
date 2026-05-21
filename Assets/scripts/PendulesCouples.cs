using UnityEngine;

public class PendulesCouples : MonoBehaviour
{
    [Header("Physique")]
    public Rigidbody pendule1;
    public Rigidbody pendule2;

    public float k = 5f;          // couplage (Kc)
    public float alpha1 = 0.1f;   // amortissement pendule 1
    public float alpha2 = 0.1f;   // amortissement pendule 2

    [Header("Configuration MQTT")]
    public MqttManager mqttManager;
    public string publishTopic = "FABLAB_21_22/Unity/meta/pendule/out/";
    public float messagesParSeconde = 20f;
    public bool afficherDansConsole = true;

    [Header("Reset / Remontée")]
    public float dureeRemontee = 3.0f;

    private float tempsEcoule = 0f;
    private float timer = 0f;
    private bool estEnRemontee = false;

    // Mémoire des dernières valeurs
    private float lastAngInit1 = 0f;
    private float lastAngInit2 = 0f;

    void Update()
    {
        if (!estEnRemontee)
        {
            tempsEcoule += Time.deltaTime;
        }

        timer += Time.deltaTime;
        float intervalle = 1f / messagesParSeconde;

        if (timer >= intervalle)
        {
            timer = 0f;
            EnvoyerDonneesMQTT();
        }
    }

    void EnvoyerDonneesMQTT()
    {
        if (mqttManager != null)
        {
            // On récupère les angles en degrés
            float theta1_deg = ObtenirAngleAbsolu(pendule1) * Mathf.Rad2Deg;
            float theta2_deg = ObtenirAngleAbsolu(pendule2) * Mathf.Rad2Deg;

            // On récupère le nom du parent (ex: pendules_couples)
            string parentId = transform.root.name;

            // Un seul message JSON contenant ang1 et ang2
            string messageJson = $"{{\"id\":\"{parentId}\", \"ang1\":{theta1_deg.ToString("F3").Replace(",", ".")}, \"ang2\":{theta2_deg.ToString("F3").Replace(",", ".")}, \"temps\":{tempsEcoule.ToString("F3").Replace(",", ".")}}}";

            mqttManager.PublishCustom(publishTopic, messageJson);

            if (afficherDansConsole)
            {
                // Debug.Log($"[Pendules Couples] Envoi MQTT: {messageJson}");
            }
        }
    }

    void Start()
    {
        // 1. Ignorer les collisions microscopiques entre les deux pendules qui pourraient causer une dérive de phase
        Collider[] colliders1 = pendule1.GetComponentsInChildren<Collider>();
        Collider[] colliders2 = pendule2.GetComponentsInChildren<Collider>();
        foreach (Collider c1 in colliders1)
        {
            foreach (Collider c2 in colliders2)
            {
                Physics.IgnoreCollision(c1, c2);
            }
        }

        // Diagnostic de symétrie physique pour identifier la cause de la dérive de phase
        Vector3 pivot1, axis1;
        GetPivotAndAxis(pendule1, out pivot1, out axis1);
        Vector3 pivot2, axis2;
        GetPivotAndAxis(pendule2, out pivot2, out axis2);
        float dist1 = Vector3.Distance(pivot1, pendule1.worldCenterOfMass);
        float dist2 = Vector3.Distance(pivot2, pendule2.worldCenterOfMass);
        Debug.LogWarning($"[Diagnostic Physique Pendules]\n" +
                         $"* Pendule 1: Masse = {pendule1.mass:F3} kg | Inertie = {pendule1.inertiaTensor} | Distance Pivot-CdM = {dist1:F5} m\n" +
                         $"* Pendule 2: Masse = {pendule2.mass:F3} kg | Inertie = {pendule2.inertiaTensor} | Distance Pivot-CdM = {dist2:F5} m\n" +
                         $"* Écart de longueur (Pivot-CdM) = {Mathf.Abs(dist1 - dist2):F5} m");

        // 2. Augmenter la précision du solveur physique pour les calculs de ressorts
        pendule1.solverIterations = 30;
        pendule2.solverIterations = 30;
        
        // 3. Augmenter la précision du solveur de vitesse pour les frottements/dampers et joints
        pendule1.solverVelocityIterations = 30;
        pendule2.solverVelocityIterations = 30;

        // 4. Augmenter la fréquence de rafraîchissement physique (100 Hz au lieu de 50 Hz par défaut)
        // pour réduire l'accumulation d'erreurs d'intégration numérique.
        Time.fixedDeltaTime = 0.01f;

        // 5. Empêcher le moteur de brider la vitesse angulaire (défaut = 7 rad/s)
        pendule1.maxAngularVelocity = 50f;
        pendule2.maxAngularVelocity = 50f;
    }

    void FixedUpdate()
    {
        if (estEnRemontee) return;

        // Angles absolus basés sur la géométrie physique (0 = vers le bas, peu importe le sens du modèle 3D)
        float theta1 = ObtenirAngleAbsolu(pendule1);
        float theta2 = ObtenirAngleAbsolu(pendule2);

        // Vitesses angulaires projetées sur l'axe X du PARENT
        float omega1 = Vector3.Dot(pendule1.angularVelocity, transform.right);
        float omega2 = Vector3.Dot(pendule2.angularVelocity, transform.right);

        // Différence d'angle sécurisée
        float deltaTheta = theta1 - theta2;
        while (deltaTheta > Mathf.PI) deltaTheta -= 2f * Mathf.PI;
        while (deltaTheta < -Mathf.PI) deltaTheta += 2f * Mathf.PI;

        // Couple de couplage uniquement
        float torque = -k * deltaTheta;

        // L'amortissement (alpha1, alpha2) est maintenant géré nativement par le paramètre Damper des HingeJoints.
        // Application des couples EN WORLD SPACE sur l'axe X du PARENT
        pendule1.AddTorque(transform.right * torque);
        pendule2.AddTorque(transform.right * -torque);
    }

    public void OnMqttResetCouples(MqttManager.ObjectTransformData data)
    {
        Debug.Log($"[Pendules Couples] REÇU ORDRE DE RESET ! ang1: {data.ang_init1}, ang2: {data.ang_init2}, Kc: {data.Kc}");
        
        // Reset tempsEcoule et timer immédiatement
        tempsEcoule = 0f;
        timer = 0f;

        if (data.ang_init1 != -999f) lastAngInit1 = data.ang_init1;
        if (data.ang_init2 != -999f) lastAngInit2 = data.ang_init2;
        if (data.alpha1 != -999f) alpha1 = data.alpha1;
        if (data.alpha2 != -999f) alpha2 = data.alpha2;
        if (data.Kc != -999f) k = data.Kc;

        // Met à jour les HingeJoints pour que la valeur s'affiche dans l'inspecteur
        AppliquerFrottementHinge(pendule1, alpha1);
        AppliquerFrottementHinge(pendule2, alpha2);

        StopAllCoroutines();
        StartCoroutine(RemonteeDouce(data));
    }

    private System.Collections.IEnumerator RemonteeDouce(MqttManager.ObjectTransformData data)
    {
        estEnRemontee = true;

        if (data.m1 != -999f && data.m1 > 0) pendule1.mass = data.m1;
        if (data.m2 != -999f && data.m2 > 0) pendule2.mass = data.m2;

        pendule1.isKinematic = true;
        pendule1.linearVelocity = Vector3.zero;
        pendule1.angularVelocity = Vector3.zero;

        pendule2.isKinematic = true;
        pendule2.linearVelocity = Vector3.zero;
        pendule2.angularVelocity = Vector3.zero;

        float startAngle1 = ObtenirAngleAbsolu(pendule1) * Mathf.Rad2Deg;
        float startAngle2 = ObtenirAngleAbsolu(pendule2) * Mathf.Rad2Deg;

        Vector3 pivot1, axis1;
        GetPivotAndAxis(pendule1, out pivot1, out axis1);

        Vector3 pivot2, axis2;
        GetPivotAndAxis(pendule2, out pivot2, out axis2);

        float elapsed = 0f;
        float prevAng1 = startAngle1;
        float prevAng2 = startAngle2;

        while (elapsed < dureeRemontee)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dureeRemontee);

            float currentAng1 = Mathf.LerpAngle(startAngle1, lastAngInit1, t);
            float delta1 = Mathf.DeltaAngle(prevAng1, currentAng1);
            pendule1.transform.RotateAround(pivot1, axis1, delta1);
            prevAng1 = currentAng1;

            float currentAng2 = Mathf.LerpAngle(startAngle2, lastAngInit2, t);
            float delta2 = Mathf.DeltaAngle(prevAng2, currentAng2);
            pendule2.transform.RotateAround(pivot2, axis2, delta2);
            prevAng2 = currentAng2;

            yield return null;
        }

        pendule1.isKinematic = false;
        pendule2.isKinematic = false;
        pendule1.WakeUp();
        pendule2.WakeUp();

        // Ré-appliquer l'amortissement après avoir relâché les pendules (isKinematic = false)
        AppliquerFrottementHinge(pendule1, alpha1);
        AppliquerFrottementHinge(pendule2, alpha2);

        // Attendre que le moteur physique effectue la première mise à jour (FixedUpdate)
        // après la libération des Rigidbodies kinematic, pour s'assurer que les pendules ont commencé
        // à bouger avant de démarrer l'acquisition des données à t=0.
        yield return new WaitForFixedUpdate();

        tempsEcoule = 0f;
        timer = 0f;
        estEnRemontee = false;
    }

    // Applique physiquement la valeur alpha au ressort (Damper) du HingeJoint
    void AppliquerFrottementHinge(Rigidbody rb, float alpha)
    {
        if (rb == null) return;

        HingeJoint hinge = rb.GetComponent<HingeJoint>();
        
        // Recherche locale robuste en premier
        if (hinge == null)
        {
            foreach (HingeJoint h in GetComponentsInChildren<HingeJoint>())
            {
                if (h.connectedBody == rb || h.gameObject == rb.gameObject)
                {
                    hinge = h;
                    break;
                }
            }
        }

        // Recherche globale en fallback si non trouvé localement
        if (hinge == null && rb.transform.root != null)
        {
            foreach (HingeJoint h in rb.transform.root.GetComponentsInChildren<HingeJoint>())
            {
                if (h.connectedBody == rb)
                {
                    hinge = h;
                    break;
                }
            }
        }

        if (hinge != null)
        {
            hinge.useSpring = true;
            JointSpring spring = hinge.spring;
            spring.spring = 0f; // Ne ramène pas au centre
            spring.targetPosition = 0f;
            spring.damper = alpha; // Amortissement
            hinge.spring = spring;
            Debug.Log($"[Pendules Couples] Amortissement appliqué sur '{rb.name}' : Damper = {alpha}");
        }
        else
        {
            Debug.LogError($"[Pendules Couples] ERREUR : Impossible de trouver le HingeJoint associé au Rigidbody '{rb.name}' !");
        }
    }

    void GetPivotAndAxis(Rigidbody rb, out Vector3 worldPivot, out Vector3 worldAxis)
    {
        worldPivot = rb.transform.position;
        worldAxis = transform.right; 

        HingeJoint hingeOnRb = rb.GetComponent<HingeJoint>();
        if (hingeOnRb != null)
        {
            worldPivot = hingeOnRb.transform.TransformPoint(hingeOnRb.anchor);
            return;
        }

        foreach (HingeJoint h in rb.transform.root.GetComponentsInChildren<HingeJoint>())
        {
            if (h.connectedBody == rb)
            {
                worldPivot = rb.transform.TransformPoint(h.connectedAnchor);
                return;
            }
        }
    }

    // Calcule l'angle réel physique en regardant la direction du centre vers le pivot.
    // Garanti 0 quand le pendule pend vers le bas, même si le modèle 3D a été tourné à l'envers.
    float ObtenirAngleAbsolu(Rigidbody rb)
    {
        Vector3 pivot, axis;
        GetPivotAndAxis(rb, out pivot, out axis);

        // Si le centre du pendule est exactement sur le pivot, on recourt à l'ancienne méthode
        if (Vector3.Distance(pivot, rb.transform.position) < 0.001f)
        {
            Vector3 localUp = rb.transform.localRotation * Vector3.up;
            return Mathf.Atan2(localUp.z, localUp.y);
        }

        // Vecteur allant du centre du pendule vers le pivot (pointe vers le Haut quand le pendule est au repos)
        Vector3 directionVersPivot = (pivot - rb.transform.position).normalized;
        
        // On exprime cette direction dans le repère du PARENT
        Vector3 localDir = transform.InverseTransformDirection(directionVersPivot);
        
        return Mathf.Atan2(localDir.z, localDir.y);
    }
}
