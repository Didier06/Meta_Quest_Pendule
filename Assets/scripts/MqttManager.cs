using UnityEngine;
using System;
using System.Text;
using System.Collections.Concurrent;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Net;
using System.Collections.Generic;
using TMPro;

public class MqttManager : MonoBehaviour
{
    [Header("MQTT Configuration")]
    public string mqttBroker = "mqtt.univ-cotedazur.fr";
    public int mqttPort = 1883;
    public bool useEncrypted = false; // Set to true if using SSL (usually port 8883)
    public string mqttTopic = "FABLAB_21_22/Unity/metaquest/in";
    public string mqttTopicCouples = "FABLAB_21_22/Unity/meta/pend_coupl/in/"; // NOUVEAU TOPIC
    public string mqttTopicOut = "FABLAB_21_22/Unity/metaquest/out";
    
    [Header("Credentials")]
    public string username = "";
    public string password = "";

    [Header("Settings")]
    public bool processMessages = true;
    public float minUpdateInterval = 0.05f; // Faster updates for smooth movement
    public float reconnectDelay = 5.0f;

    private MqttClient client;
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
    private float lastUpdateTime = 0f;
    
    // Store objects that need continuous rotation: GameObject -> RotationSpeed (Euler/sec)
    private Dictionary<GameObject, Vector3> rotatingObjects = new Dictionary<GameObject, Vector3>();
    // Store objects that need smooth transition to a target rotation: Transform -> TargetLocalRotation
    private Dictionary<Transform, Quaternion> smoothTargets = new Dictionary<Transform, Quaternion>();
    public float smoothingSpeed = 5.0f;

    [Serializable]
    public class ObjectTransformData
    {
        public string id;         // Nouveau nom recommandé
        public string targetName; // Fallback pour la compatibilité
        public Vector3 position;
        public Vector3 rotation; // Absolute rotation
        public Vector3 scale;    // Zoom/Scale (default 0,0,0 if omitted)
        public Vector3 rotationSpeed; // Continuous rotation speed (degrees/sec)
        
        // --- Pendule Physics ---
        public float ang_init = -999f;
        public float alpha = -999f;
        public float m = -999f;

        // --- Pendules Couples ---
        public float ang_init1 = -999f;
        public float ang_init2 = -999f;
        public float alpha1 = -999f;
        public float alpha2 = -999f;
        public float Kc = -999f;
        public float m1 = -999f;
        public float m2 = -999f;
    }

    void Start()
    {
        // Ensure secrets are loaded
        SecretsLoader.LoadSecrets();

        if (SecretsLoader.Data != null)
        {
            username = SecretsLoader.Data.mqttUser;
            password = SecretsLoader.Data.mqttPassword;
            mqttPort = SecretsLoader.Data.mqttPort;
            
            // If port suggests SSL (8883 or 8443), enable encryption automatically, or respect the boolean
            // For now, let's keep the user's boolean preference or auto-switch if port is typical SSL
            if (mqttPort == 8883 || mqttPort == 8443) useEncrypted = true;
            
            Debug.Log($"Loaded credentials from secrets. User: {username}, Port: {mqttPort}, Encrypted: {useEncrypted}");
        }
        else
        {
            Debug.LogWarning("No secrets loaded. Using Inspector values.");
        }
        
        StartCoroutine(ConnectionRoutine());
    }

    System.Collections.IEnumerator ConnectionRoutine()
    {
        while (true)
        {
            if (client == null || !client.IsConnected)
            {
                Debug.Log("Connecting to MQTT broker...");
                Connect();
            }
            yield return new WaitForSeconds(reconnectDelay);
        }
    }

    void Connect()
    {
        try
        {
            if (client != null && client.IsConnected) return;

            // Use specified encryption setting
            if (useEncrypted)
            {
                // Recommended constructor: brokerHostName, brokerPort, secure, caCert (null or yours), clientCert (null or yours), sslProtocol
                client = new MqttClient(mqttBroker, mqttPort, true, null, null, MqttSslProtocols.TLSv1_2);
            }
            else
            {
                // Recommended constructor without SSL
                client = new MqttClient(mqttBroker, mqttPort, false, null, null, MqttSslProtocols.None);
            }

            string clientId = Guid.NewGuid().ToString();
            
            // Connect with or without credentials
            if (!string.IsNullOrEmpty(username))
                client.Connect(clientId, username, password);
            else
                client.Connect(clientId);

            if (client.IsConnected)
            {
                Debug.Log($"Connected to MQTT Broker: {mqttBroker}:{mqttPort}");
                client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
                client.ConnectionClosed += Client_ConnectionClosed;
                
                // Subscribe to Main JSON Topic AND Couples Topic
                List<string> topics = new List<string>();
                List<byte> qos = new List<byte>();

                // Helper pour s'abonner avec et sans slash
                Action<string> addTopic = (t) => {
                    if (string.IsNullOrEmpty(t)) return;
                    string clean = t.TrimEnd('/');
                    topics.Add(clean);
                    qos.Add(MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE);
                    topics.Add(clean + "/");
                    qos.Add(MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE);
                };

                addTopic(mqttTopic);
                addTopic(mqttTopicCouples);

                client.Subscribe(topics.ToArray(), qos.ToArray());

                // Send Hello Message
                string helloMsg = "Hello from meta Quest 3";
                client.Publish(mqttTopicOut, Encoding.UTF8.GetBytes(helloMsg), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"MQTT Connection Failed: {e.Message}");
        }
    }

    private void Client_ConnectionClosed(object sender, EventArgs e)
    {
        Debug.LogWarning("MQTT Connection Closed.");
    }

    private void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
    {
        string msg = Encoding.UTF8.GetString(e.Message);
        string topic = e.Topic;

        // On enlève les slashs à la fin pour éviter les erreurs bêtes
        string cleanTopic = topic.TrimEnd('/');
        string cleanMqttTopic = mqttTopic.TrimEnd('/');
        string cleanMqttTopicCouples = mqttTopicCouples != null ? mqttTopicCouples.TrimEnd('/') : "";

        // Dispatch based on topic
        if (cleanTopic == cleanMqttTopic || cleanTopic == cleanMqttTopicCouples)
        {
            // Main JSON Channel
            messageQueue.Enqueue(msg);
        }
    }

    void Update()
    {
        if (processMessages && !messageQueue.IsEmpty)
        {
            if (Time.time - lastUpdateTime < minUpdateInterval)
                return;

            lastUpdateTime = Time.time;

            string msg;
            while (messageQueue.TryDequeue(out msg))
            {
                ProcessMessage(msg);
            }
        }

        // Test sending positions (New Input System)
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.sKey.wasPressedThisFrame)
        {
            PublishExampleMessage();
        }

        // Apply continuous rotations
        if (rotatingObjects.Count > 0)
        {
            List<GameObject> toRemove = new List<GameObject>();
            foreach (var kvp in rotatingObjects)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.transform.Rotate(kvp.Value * Time.deltaTime);
                }
                else
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var dead in toRemove) rotatingObjects.Remove(dead);
        }

        // Apply smooth target rotations (e.g. Thermometer)
        if (smoothTargets.Count > 0)
        {
            List<Transform> finished = new List<Transform>();
            foreach (var kvp in smoothTargets)
            {
                Transform t = kvp.Key;
                if (t != null)
                {
                    t.localRotation = Quaternion.Slerp(t.localRotation, kvp.Value, Time.deltaTime * smoothingSpeed);
                    
                    // Stop if close enough to save performance
                    if (Quaternion.Angle(t.localRotation, kvp.Value) < 0.1f) finished.Add(t);
                }
                else
                {
                    finished.Add(t);
                }
            }
            foreach (var t in finished) smoothTargets.Remove(t);
        }

    }

    void ProcessMessage(string json)
    {
        Debug.Log($"[Diagnostic] JSON brut reçu : {json}");
        try
        {
            // Parse JSON into data object
            ObjectTransformData data = JsonUtility.FromJson<ObjectTransformData>(json);
            
            Debug.Log($"[Diagnostic] Parsé -> id: '{data.id}', targetName: '{data.targetName}'");

            string finalTarget = !string.IsNullOrEmpty(data.id) ? data.id : data.targetName;
            
            if (data != null && !string.IsNullOrEmpty(finalTarget))
            {
                GameObject target = GameObject.Find(finalTarget);

                if (target != null)
                {
                    Rigidbody rb = target.GetComponent<Rigidbody>();
                    bool hasRb = (rb != null);

                    // --- POSITION ---
                    // Only update if key provided or value is non-zero (fallback)
                    // (Simple "Contains" check helps distinguish "missing" from "explicit 0")
                    if (json.Contains("\"position\""))
                    {
                        if (hasRb)
                        {
                            rb.position = data.position; // Teleport physics
                            // If kinematic, transform update is implicit, but for dynamic this is safer
                        }
                        else
                        {
                            target.transform.position = data.position;
                        }
                    }

                    // --- ROTATION ---
                    // Fix: Check if JSON contains "rotation" key to allow setting 0,0,0
                    if (json.Contains("\"rotation\""))
                    {
                        Quaternion newRot = Quaternion.Euler(data.rotation);
                        if (hasRb)
                        {
                            rb.rotation = newRot;
                        }
                        else
                        {
                            target.transform.rotation = newRot;
                        }
                    }

                    // --- SCALE ---
                    if (json.Contains("\"scale\"") && data.scale != Vector3.zero)
                    {
                        target.transform.localScale = data.scale;
                    }

                    // --- CONTINUOUS ROTATION ---
                    if (json.Contains("\"rotationSpeed\""))
                    {
                        if (data.rotationSpeed != Vector3.zero)
                        {
                            if (!rotatingObjects.ContainsKey(target))
                                rotatingObjects.Add(target, data.rotationSpeed);
                            else
                                rotatingObjects[target] = data.rotationSpeed;
                        }
                        else
                        {
                            // Explicitly set to 0 -> Stop
                            if (rotatingObjects.ContainsKey(target))
                                rotatingObjects.Remove(target);
                        }
                    }

                    // --- PENDULUM RESET ---
                    if (json.Contains("\"ang_init\"") || json.Contains("\"alpha\"") || json.Contains("\"m\""))
                    {
                        target.BroadcastMessage("OnMqttReset", data, SendMessageOptions.DontRequireReceiver);
                    }

                    // --- PENDULES COUPLES RESET ---
                    if (json.Contains("\"ang_init1\"") || json.Contains("\"ang_init2\"") || json.Contains("\"Kc\"") || json.Contains("\"alpha1\"") || json.Contains("\"alpha2\"") || json.Contains("\"m1\"") || json.Contains("\"m2\""))
                    {
                        Debug.Log($"[Diagnostic] Condition PENDULES COUPLES validée. Envoi de 'OnMqttResetCouples' à l'objet '{target.name}'");
                        target.BroadcastMessage("OnMqttResetCouples", data, SendMessageOptions.DontRequireReceiver);
                    }

                    // Physics WakeUp ensures changes are registered immediately
                    if (hasRb && !rb.isKinematic) rb.WakeUp();

                    Debug.Log($"MQTT Update '{finalTarget}': Pos={(json.Contains("\"position\"") ? "Set" : "Skip")}");
                }
                else
                {
                    Debug.LogWarning($"Target object '{finalTarget}' not found in scene.");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing/applying JSON update: {e.Message} \nJSON: {json}");
        }
    }

    // Helper to find a child recursively
    Transform FindDeepChild(Transform aParent, string aName)
    {
        foreach (Transform child in aParent)
        {
            if (child.name == aName) return child;
            var result = FindDeepChild(child, aName);
            if (result != null) return result;
        }
        return null;
    }

    public void PublishExampleMessage()
    {
        if (client != null && client.IsConnected)
        {
            // Example message
            string msg = "{\"targetName\":\"Cube\", \"position\":{\"x\":0,\"y\":1,\"z\":2}}";
            client.Publish(mqttTopicOut, Encoding.UTF8.GetBytes(msg), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);
            Debug.Log("Sent example message: " + msg);
        }
    }

    /// <summary>
    /// Publishes a custom message to a specific topic using the existing client.
    /// </summary>
    public void PublishCustom(string topic, string message)
    {
        if (client != null && client.IsConnected)
        {
            client.Publish(topic, Encoding.UTF8.GetBytes(message), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);
            // Debug.Log($"[MqttManager] Published to {topic}: {message}");
        }
        else
        {
            // Debug.LogWarning($"[MqttManager] Cannot publish to {topic}, client not connected.");
        }
    }

    void OnDestroy()
    {
        if (client != null && client.IsConnected)
        {
            client.Disconnect();
        }
    }
}
