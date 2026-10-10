# V-01 — Building the handling sandbox (≈20 min, in the Unity editor)

The code is in `Assets/_Convoy/Scripts`. This guide wires it into `Sandbox.unity`.
Do it once, save, commit. Everything below is greybox: cubes, planes and cylinders.

## 0. Pull the branch
```powershell
cd C:\Users\gabri\convoy
git fetch
git checkout feat/V-01-jackal-controller
```
Open the project. Unity compiles the scripts. **Console must show 0 red errors.**

## 1. Run the tests
*Window → General → Test Runner → EditMode → Run All.* Expect **21 green tests** (`VehicleMathTests`).

## 2. Vehicle config
Project panel → `_Convoy/Config` → right-click → *Create → Convoy → Vehicle Config* → name it `Jackal_VehicleConfig`.
Leave the defaults for now.

## 3. Ground and obstacles (open `_Convoy/Scenes/Sandbox`)
1. *GameObject → 3D Object → Plane* → name `Ground`, Position `0,0,0`, Scale `20,1,20` (200 × 200 m).
2. Ramp: *3D Object → Cube* → `Ramp`, Position `0,1,30`, Rotation `-15,0,0`, Scale `8,0.5,12`.
3. Bumps: 5–6 cubes, Scale about `3,0.4,3`, rotated a few degrees, scattered around `z = 50…80`.
4. A wall to crash into: Cube, Position `25,1.5,0`, Scale `1,3,30`.

## 4. The Jackal
1. *Create Empty* → name `Jackal`, Position `0,1.5,0`.
   - *Add Component → Rigidbody* (leave defaults; the script sets mass etc. from the config).
   - *Add Component → Vehicle Controller*.
   - *Add Component → Pilot Input Source* → drag `Jackal` into **Target**.
2. Body: right-click `Jackal` → *3D Object → Cube* → name `Body`, Position `0,0,0`, Scale `1.8,0.6,4`. Keep its Box Collider.
3. Wheel anchors: right-click `Jackal` → *Create Empty* ×4. **Remove nothing, these have no collider.**

   | Name | Position |
   |---|---|
   | `Anchor_FL` | `-0.85, -0.1, 1.35` |
   | `Anchor_FR` | `0.85, -0.1, 1.35` |
   | `Anchor_RL` | `-0.85, -0.1, -1.35` |
   | `Anchor_RR` | `0.85, -0.1, -1.35` |

4. Wheel visuals (optional but helps reading the car): for each wheel, right-click `Jackal` → *Create Empty* → `Wheel_FL` (etc.).
   Inside it: *3D Object → Cylinder*, Rotation `0,0,90`, Scale `0.76,0.12,0.76`, and **remove its Capsule Collider** (otherwise it fights the ground).
5. Select `Jackal`, in **Vehicle Controller**:
   - **Config** → `Jackal_VehicleConfig`.
   - **Wheels** → size `4`, then for each element:

   | Element | Anchor | Visual | Steers | Driven | Rear |
   |---|---|---|---|---|---|
   | 0 | Anchor_FL | Wheel_FL | ✅ | ✅ | ☐ |
   | 1 | Anchor_FR | Wheel_FR | ✅ | ✅ | ☐ |
   | 2 | Anchor_RL | Wheel_RL | ☐ | ✅ | ✅ |
   | 3 | Anchor_RR | Wheel_RR | ☐ | ✅ | ✅ |

   With the `Jackal` selected, the Scene view shows cyan suspension rays and a yellow centre-of-mass dot.

## 5. Camera
Select `Main Camera` → *Add Component → Sandbox Chase Camera* → drag `Jackal` into **Target**.

## 6. Drive
Press Play.

| Action | Keyboard | Gamepad |
|---|---|---|
| Throttle | W / ↑ | RT |
| Brake / reverse | S / ↓ | LT |
| Steer | A D / ← → | Left stick |
| Handbrake (drift) | Space | A / Cross |

**Live tuning:** while playing, select `Jackal_VehicleConfig` and change values — they apply instantly.
Changes to a ScriptableObject **persist after Play mode**, so note what you liked.

## 7. What to try (feeds PT-01)
- Full throttle from rest → top speed ≈ 125 km/h, no wobble.
- Hard turn at speed → it should slide, not flip.
- Handbrake into a corner → controllable drift.
- Ramp jump → lands without exploding; W/S pitches the car in the air.
- Hit the wall at speed → stops, does not launch into space.
- Try **rear-wheel drive** (untick *Driven* on the front wheels) and compare.

Things that are **not** in V-01 yet: auto-righting and reset (V-02), debug overlay (V-03), Cinemachine camera (C-01).

## 8. Commit the scene and config
Close Unity, then:
```powershell
git add -A
git status
git commit -m "V-01: sandbox scene wiring and Jackal config"
git push
```

## If something goes wrong
| Symptom | Likely cause |
|---|---|
| Car falls through the floor | `Ground` has no collider, or the anchors are below the ground |
| Car sits on its belly, wheels don't touch | Anchors too high: lower them, or raise *Suspension Rest Length* |
| Car shakes/bounces forever | *Spring Damping* too low: try 5000 |
| Car flips in every corner | Lower *Center Of Mass Offset* Y (e.g. -0.6) or lower *Tire Friction* |
| Won't turn at speed | Raise *High Speed Steer Factor* |
| Spins out too easily | Raise *Rear Lateral Grip* or lower *Engine Force* |
| Red error "has no VehicleConfig assigned" | Step 5, Config field |
