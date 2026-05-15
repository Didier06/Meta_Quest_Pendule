# Unity Meta Quest 3 Project: MQTT Pendulum Simulation

This project is a technical demonstration for the **Meta Quest 3**, featuring a physical simulation of a simple pendulum that can be remotely controlled and monitored using the **MQTT** protocol.

The goal is to study the physics of a pendulum (free fall, viscous friction) while visualizing the model in Unity and controlling the experiment via JSON requests from an external dashboard.

---

## 1. Key Features

- **Native Physics Engine:** Utilizes Unity's native PhysX engine with `Rigidbody` and `Hinge Joint` components for a perfectly realistic, gravity-driven oscillation.
- **Real-Time Telemetry (Out):** The pendulum broadcasts its angular position and elapsed time at high frequency to the MQTT broker.
- **Remote Control (In):** Receives commands to modify the starting angle, the mass, or the viscous friction of the hinge.
- **Zero-Drift Reset:** The control script uses absolute spatial interpolation to smoothly lift the pendulum back to its starting position without "breaking" the Hinge Joint or accumulating rounding errors.

---

## 2. MQTT Architecture

The project integrates an **MQTT Manager** (`MqttManager.cs`) coupled with a script attached to the pendulum (`PendulumMqttPublisher.cs`).

### A. Listening / Remote Control (Topic IN)
The pendulum listens to JSON messages to reset itself or modify its physical properties on the fly.

- **Listen Topic:** `FABLAB_21_22/Unity/metaquest/in` *(configurable in MqttManager)*
- **Example Command JSON:**
```json
{
  "id": "pendule_simple_1",
  "ang_init": 45.0,
  "alpha": 15.0,
  "m": 1.5
}
```
- `id`: The unique identifier of the pendulum in the scene.
- `ang_init`: The starting angle. Unity will perform a **smooth lift** (geometric movement, physics engine frozen) for 3 seconds. During this time, the timer is frozen. Upon release, the free fall begins.
- `alpha`: The viscous damping coefficient (Damper) of the Hinge Joint. Ideal for observing critical or overdamped regimes.
- `m`: (Optional) Modifies the mass of the pendulum.

> **Note:** The variables `ang_init` and `alpha` are **memorized**. If you send only one of these variables (e.g., only `"alpha": 10.0`), the pendulum will use the previous starting angle, and vice versa.

### B. Telemetry / Publication (Topic OUT)
During its free fall, the pendulum publishes its state at regular intervals.

- **Publish Topic:** `FABLAB_21_22/Unity/meta/pendule/out/`
- **Example Output JSON:**
```json
{
  "id": "pendule_simple_1",
  "angle": 60.0,
  "temps": 12.2
}
```
- `id`: Dynamically retrieved from the main parent GameObject's name.
- `angle`: The local angle (`localEulerAngles.x`), corrected to be between -180° and 180°.
- `temps`: The elapsed time in seconds since the last reset/release.

---

## 3. Pendulum Physics Setup in Unity

To recreate or modify the oscillation, here is the expected physical structure in the scene:

#### 1️⃣ Hierarchy
```text
sys_pendule
 ├─ Axe (Axis)
 ├─ Pendule (Pendulum)
 └─ Pivot
```

#### 2️⃣ Axe (Fixed Support)
- **Component:** `Rigidbody`
- **Settings:** `Use Gravity = OFF`, `Is Kinematic = ON`

#### 3️⃣ Pendule (Moving Part)
- **Component:** `Rigidbody`
- **Typical Settings:** `Use Gravity = ON`, `Mass = 1`, `Angular Damping = 0.05 to 0.2` (simulates fluid air friction).

#### 4️⃣ Pivot (Hinge)
- **Component:** `Rigidbody`
- **Settings:** `Use Gravity = OFF`, `Is Kinematic = OFF`

#### 5️⃣ Attaching the Pivot to the Axe
- On the **Pivot**, add a `Fixed Joint`.
- **Setting:** `Connected Body = Axe`. (The pivot remains securely attached to the axis).

#### 6️⃣ Adding Pendulum Rotation
- Still on the **Pivot**, add a `Hinge Joint`.
- **Settings:** `Connected Body = Pendule`, `Anchor = 0 0 0`.

#### 7️⃣ Rotation Axis
- In the `Hinge Joint` -> `Axis`.
- **Settings:** `X=1 Y=0 Z=0` (or depending on desired orientation). Only one axis must be free.

#### 8️⃣ MQTT-Controlled Viscous Friction
The `"alpha"` parameter in the JSON modifies the `Damper` property of the `Spring` included in the `Hinge Joint`. It must be enabled (`Use Spring = true` but with `Spring = 0` to provide only a resistance force without an arbitrary return point).

---

## 4. Prerequisites and Security
Credentials for the MQTT connection are secure and **not committed** to Git. They must be stored locally in `Assets/Resources/secrets.json`.

```json
{
  "mqttAddress": "broker.hivemq.com",
  "mqttPort": 1883,
  "mqttUsername": "",
  "mqttPassword": ""
}
```

---

## 5. Coupled Pendulums (Pendules Couplés)

In addition to the simple pendulum, this project now includes a **Coupled Pendulum** simulation. Two independent pendulums are connected by a virtual torsional spring, allowing the study of energy transfer, beats (battements), and normal modes (in-phase and anti-phase oscillations).

### A. Remote Control (Topic IN)
The coupled pendulums use a separate listening topic to avoid interference with the simple pendulum.

- **Listen Topic:** `FABLAB_21_22/Unity/meta/pend_coupl/in/`
- **Example Command JSON:**
```json
{
  "id": "pendules_couples",
  "ang_init1": 40.0,
  "ang_init2": -40.0,
  "alpha1": 0.5,
  "alpha2": 0.5,
  "Kc": 5.0,
  "m1": 1.0,
  "m2": 1.0
}
```
- `ang_init1` / `ang_init2`: Starting angles for Pendulum 1 and 2. 
  - *Tip: Set them to the same value (e.g., 40, 40) for an in-phase normal mode. Set them to opposite values (e.g., 40, -40) for an anti-phase normal mode.*
- `alpha1` / `alpha2`: Viscous friction applied to the respective Hinge Joints.
- `Kc`: The stiffness constant of the virtual coupling spring. A higher value means faster energy transfer between the two pendulums.
- `m1` / `m2`: (Optional) Masses of the individual pendulums.

### B. Telemetry (Topic OUT)
The coupled system publishes the state of **both** pendulums simultaneously to guarantee perfectly synchronized data for external graphing (e.g., in Node-RED).

- **Publish Topic:** `FABLAB_21_22/Unity/meta/pendule/out/` (shared with the simple pendulum)
- **Example Output JSON:**
```json
{
  "id": "pendules_couples",
  "ang1": 25.4,
  "ang2": -12.1,
  "temps": 4.5
}
```

### C. Physics Engine Optimizations
To perfectly simulate the coupled normal modes without phase drift over long periods, the script (`PendulesCouples.cs`) implements strict physics optimizations:
- Absolute geometric angle detection (`ObtenirAngleAbsolu`), making it immune to 3D prefab orientation flips.
- Automated `Physics.IgnoreCollision` between the pendulums to prevent microscopic friction.
- Elevated `solverIterations` to maintain integration accuracy for the torsional spring forces.

---

## 6. Overview

![Meta Quest Pendule](Assets/images/meta_pendule.png)

