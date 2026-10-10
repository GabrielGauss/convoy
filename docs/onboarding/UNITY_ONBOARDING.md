# Unity onboarding (F-00)

Team starts at zero Unity experience. Target: ~8 h each, done in week 0–1. Learn only what M1 needs.

## 1. Install (30 min)
1. Install **Unity Hub**, then the exact editor version in `README.md` (Unity 6.3 LTS). Modules: *Windows Build Support (IL2CPP)*, *Documentation*.
2. Install Git and Git LFS; run `git lfs install` once.
3. IDE: JetBrains Rider (free for non-commercial use — check license terms for your case) or Visual Studio Community with the Unity workload.

## 2. Learn (by role)
| Topic | Who | Why for M1 |
|---|---|---|
| Unity Learn: **Unity Essentials** pathway | All | Editor, GameObjects, prefabs, scenes |
| Rigidbody, `FixedUpdate`, raycasts, forces | GV, DEV | V-01 vehicle |
| ScriptableObjects | All | All tuning lives there |
| Input System (actions, `PlayerInputManager`) | DEV, GV | F-03 seats |
| Cinemachine 3 basics | GV | C-01 camera |
| ProBuilder basics | ND | T-01 greybox track |
| Unity Test Framework (EditMode) | DEV | Tests gate PRs |
| Prefabs vs scenes, merge conflicts | All | Avoid lost work |

DEV note: C# is familiar; focus on Unity's lifecycle (`Awake/Start/Update/FixedUpdate`), serialization rules, and why `MonoBehaviour` constructors are not used.

## 3. Exit check
Open the project, enter Play mode, create and edit a prefab, change a ScriptableObject value at runtime, make a branch, commit, open a PR.

## Git setup
One-time per machine, after cloning (adjust the path to your exact editor version):

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.3.X/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
```

Unity editor settings (set once in F-01, committed in `ProjectSettings/`):
- *Project Settings → Editor → Asset Serialization:* **Force Text**
- *Version Control mode:* **Visible Meta Files**

Rules: always commit `.meta` files with their asset. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`.
