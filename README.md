# Unity Meta Quest 3 Project: 3D Neuron in Augmented Reality

This project is an educational technical demonstration for the **Meta Quest 3**, leveraging **Passthrough (Mixed Reality)** and the **Meta XR Interaction SDK**.

The core objective is to visualize a highly detailed **3D animated Neuron** directly within the user's real-world environment and allow interactive learning by identifying its various anatomical parts using hand-tracking.

## Key Features

### 1. Mixed Reality (Passthrough)

- Utilizes the headset's AR/Passthrough camera to overlay the 3D neuron directly into the physical room.
- Transparent scene background ensures total immersion without breaking the user's connection to their surroundings.

### 2. Anatomical Interactions (Hands & Controllers)

The application allows users to physically reach out and interact with the neuron using Meta's Hand Tracking.
By touching specific areas of the model, floating 3D text panels (World Space UI) appear to identify the anatomical parts:

- **Dendrites**
- **Soma** (Cell Body)
- **Axon**
- **Myelin Sheath**
- *And more...*

### 3. Idle Animations

- The neuron features continuous ambient animations (e.g., cell pulsation, electrical impulses, or floating movement) managed through Unity's `Animator` and an assigned `Avatar` for complex rig interactions.

### 4. Remote Control (MQTT Connectivity)

The project includes a lightweight **MQTT Manager** (`MqttManager.cs`) allowing remote control of the neuron or other virtual objects via JSON messages.

- **Topic IN**: `FABLAB_21_22/Unity/metaquest/in` (Receives commands)
- **Topic OUT**: `FABLAB_21_22/Unity/metaquest/out` (Sends status)

#### Supported JSON Commands

You can dynamically update Position, Rotation, and Scale. The script identifies the target object via the `targetName` key.

```json
{
  "targetName": "Neurone",
  "position": { "x": 0.0, "y": 1.5, "z": 2.0 },
  "rotation": { "x": 0.0, "y": 45.0, "z": 0.0 },
  "scale": { "x": 1.5, "y": 1.5, "z": 1.5 }
}
```

Credentials for the MQTT connection are secure and **not committed** to Git. They must be stored locally in `Assets/Resources/secrets.json`.

## 5. Simulation Physique du Pendule

Cette application intègre également la simulation d'un pendule simple, dont l'angle d'oscillation est diffusé en temps réel via MQTT.

### Réglage physique du pendule dans Unity

Pour obtenir une oscillation réaliste, voici la structure et les réglages physiques appliqués :

#### 1️⃣ Hiérarchie
```text
sys_pendule
 ├─ Axe
 ├─ Pendule
 └─ Pivot
```

#### 2️⃣ Axe (support fixe)
- **Composant :** `Rigidbody`
- **Réglages :** `Use Gravity = OFF`, `Is Kinematic = ON`

#### 3️⃣ Pendule (mobile)
- **Composant :** `Rigidbody`
- **Réglages typiques :** `Use Gravity = ON`, `Mass = 1`, `Angular Damping = 0.05 à 0.2` (simule le frottement fluide).

#### 4️⃣ Pivot (charnière)
- **Composant :** `Rigidbody`
- **Réglages :** `Use Gravity = OFF`, `Is Kinematic = OFF`

#### 5️⃣ Fixer le Pivot à l’Axe
- Sur **Pivot**, ajouter un `Fixed Joint`.
- **Réglage :** `Connected Body = Axe`. (Le pivot reste attaché à l’axe).

#### 6️⃣ Ajouter la rotation du pendule
- Toujours sur **Pivot**, ajouter un `Hinge Joint`.
- **Réglages :** `Connected Body = Pendule`, `Anchor = 0 0 0`.

#### 7️⃣ Axe de rotation
- Dans le `Hinge Joint` -> `Axis`.
- **Réglages :** `X=1 Y=0 Z=0` (ou `X=0 Y=0 Z=1` selon l'orientation). Un seul axe doit être libre.

#### 8️⃣ Lancer l’oscillation
Avant de faire `Play`, il faut incliner légèrement le pendule (ex: 10° à 30°), sinon il restera à l’équilibre vertical.

#### 9️⃣ Frottement fluide
Pour ajuster le réalisme, modifier l'`Angular Damping` sur le `Rigidbody` du Pendule :
- `0.01` -> très fluide
- `0.1`  -> réaliste
- `0.5`  -> très amorti

#### 🔟 Frottement sec (optionnel)
Sur le `Hinge Joint` du Pivot, activer `Use Motor`.
- **Réglages :** `Target Velocity = 0`, `Force = 10 à 100`, `Free Spin = OFF`. (Simule un couple de frottement constant).

---

### Envoi des données du Pendule via MQTT

Pour analyser l'oscillation à distance, le script `PendulumMqttPublisher.cs` est attaché à l'objet Pendule. Il lit l'angle en temps réel (sur l'axe X) et l'envoie via le `MqttManager`.

#### Structure du message JSON (Envoi depuis Unity)
L'envoi est configuré (par défaut 10 messages par seconde) sous forme de JSON structuré pour inclure l'identifiant du pendule, son angle actuel, et le temps écoulé depuis le début de la simulation.

**Topic de publication :** `FABLAB_21_22/Unity/meta/pendule/out/`

**Exemple de JSON envoyé :**
```json
{
  "id": "pendule_simple_1",
  "angle": 60.0,
  "temps": 12.2
}
```
- `id` : Récupéré dynamiquement via le nom du GameObject parent principal (`transform.root.name`).
- `angle` : L'angle local (`transform.localEulerAngles.x`), corrigé pour être compris entre -180° et 180°.
- `temps` : Le temps écoulé en secondes depuis le lancement (ou depuis la dernière réinitialisation).

#### Contrôle à distance (Réception dans Unity)
Le script `PendulumMqttPublisher` écoute également les commandes MQTT via le `MqttManager` pour réinitialiser l'expérience en direct, sans casser la physique (Hinge Joint).

**Topic d'écoute (configuré dans MqttManager) :** `FABLAB_21_22/Unity/metaquest/in` *(peut être modifié)*

**Exemple de JSON de commande :**
```json
{
  "id": "pendule_simple_1",
  "ang_init": 45.0,
  "alpha": 15.0,
  "m": 1.5
}
```
- `id` : Permet de cibler précisément le pendule dans la scène.
- `ang_init` : Angle de départ. Unity effectuera une **remontée en douceur** (zéro-dérive géométrique autour du pivot) pendant 3 secondes. Pendant ce temps, le chronomètre (`temps`) est figé à 0.0s. Au moment du lâcher, la chute libre démarre.
- `alpha` : Coefficient d'amortissement visqueux (Damper) du Hinge Joint. Idéal pour observer le régime critique.
- `m` : (Optionnel) Modifie la masse du pendule.

> **Note de flexibilité :** Les variables `ang_init` et `alpha` sont **mémorisées**. Si vous n'envoyez qu'une seule de ces variables (ex: seulement `alpha`), le pendule gardera son précédent angle de départ, et inversement. Cela permet de régler l'amortissement à la volée !

## Technical Architecture

### Interaction Setup

To ensure precise and optimized interactions with the complex 3D neuron mesh:

- **Hitboxes**: Simplified invisible `Box Colliders` or `Sphere Colliders` (marked as *Is Trigger*) are placed strategically over the different anatomical parts of the 3D model.
- **Trigger Logic**: Custom scripts (e.g., `dendrites_interaction.cs`) detect when the player's hands (`Hand`, `Index`, or `Bone` tags) enter these trigger zones and trigger the display of the corresponding TextMeshPro floating UI.

### Frameworks & SDKs

This application strictly uses native Meta SDKs rather than AR Foundation for optimized performance and advanced hand-tracking physics:

1. **Meta XR Core SDK**: Camera rig, stereoscopic rendering, and passthrough.
2. **Meta XR Interaction SDK**: Detection of complex hand gestures, poking, and grabbing.

## How to Run

1. Open the project in **Unity 6** (or a Meta XR compatible version).
2. Ensure the build platform is set to **Android** (`File > Build Settings`).
3. Connect a **Meta Quest 3** headset via Link cable or build the `.apk` directly (`Build and Run`).
4. Once inside the headset, grant permission for Spatial Data (Passthrough) if prompted.
5. Walk up to the floating 3D neuron and use your bare hands to touch its different sections to learn more about its biology!
