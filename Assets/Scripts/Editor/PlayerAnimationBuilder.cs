using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HappyFarmer.EditorTools
{
    public static class PlayerAnimationBuilder
    {
        private const string IdleSheetPath = "Assets/Sprites/LittleDreamyland/Sprites/Characters/Bunny/IDLE/Bunny_Idle.png";
        private const string RunSheetPath = "Assets/Sprites/LittleDreamyland/Sprites/Characters/Bunny/RUN/Bunny_Run.png";
        private const string AnimFolder = "Assets/Animations/Player";
        private const string ControllerPath = "Assets/Animations/Player/Bunny.controller";
        private const string ScenePath = "Assets/Scenes/FarmMap.unity";

        // Directions: 0 = Down, 1 = Up, 2 = Left, 3 = Right

        [MenuItem("Happy Farmer/4. Generate Player Animations and Controller", false, 4)]
        public static void GenerateAnimationsAndController()
        {
            EnsureFolder("Assets/Animations");
            EnsureFolder(AnimFolder);

            // 1. Load sub-sprites
            var idleSprites = LoadSprites(IdleSheetPath);
            var runSprites = LoadSprites(RunSheetPath);

            if (idleSprites.Count == 0 || runSprites.Count == 0)
            {
                Debug.LogError("[HappyFarmer] Could not find Bunny idle or run sprites! Check paths and sprite slicing.");
                return;
            }

            // Direction mappings based on sheet rows:
            // Row 0 = Up, Row 1 = Right, Row 2 = Left, Row 3 = Down
            var clipIdleUp = CreateAnimationClip("Bunny_Idle_Up", GetSprites(idleSprites, "bunny_idle_0", 5), 6f);
            var clipIdleRight = CreateAnimationClip("Bunny_Idle_Right", GetSprites(idleSprites, "bunny_idle_1", 5), 6f);
            var clipIdleLeft = CreateAnimationClip("Bunny_Idle_Left", GetSprites(idleSprites, "bunny_idle_2", 5), 6f);
            var clipIdleDown = CreateAnimationClip("Bunny_Idle_Down", GetSprites(idleSprites, "bunny_idle_3", 5), 6f);

            var clipRunUp = CreateAnimationClip("Bunny_Run_Up", GetSprites(runSprites, "bunny_run_0", 8), 10f);
            var clipRunRight = CreateAnimationClip("Bunny_Run_Right", GetSprites(runSprites, "bunny_run_1", 8), 10f);
            var clipRunLeft = CreateAnimationClip("Bunny_Run_Left", GetSprites(runSprites, "bunny_run_2", 8), 10f);
            var clipRunDown = CreateAnimationClip("Bunny_Run_Down", GetSprites(runSprites, "bunny_run_3", 8), 10f);

            // 2. Create Traditional State-based Animator Controller (No Blend Trees)
            var controller = CreateStateBasedAnimatorController(
                clipIdleDown, clipIdleUp, clipIdleLeft, clipIdleRight,
                clipRunDown, clipRunUp, clipRunLeft, clipRunRight
            );

            // 3. Attach to Player in Scene if Scene exists
            AttachToScenePlayer(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HappyFarmer] Successfully created Player state-based animations and AnimatorController at " + AnimFolder);
        }

        private static Dictionary<string, Sprite> LoadSprites(string assetPath)
        {
            var dict = new Dictionary<string, Sprite>();
            var all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var obj in all)
            {
                if (obj is Sprite s && !dict.ContainsKey(s.name))
                {
                    dict[s.name] = s;
                }
            }
            return dict;
        }

        private static Sprite[] GetSprites(Dictionary<string, Sprite> dict, string prefix, int count)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < count; i++)
            {
                string key = $"{prefix}_{i}";
                if (dict.TryGetValue(key, out var s))
                {
                    list.Add(s);
                }
                else
                {
                    Debug.LogWarning($"[HappyFarmer] Missing sprite frame: {key}");
                }
            }
            return list.ToArray();
        }

        private static AnimationClip CreateAnimationClip(string clipName, Sprite[] frames, float fps)
        {
            if (frames == null || frames.Length == 0) return null;

            string clipPath = $"{AnimFolder}/{clipName}.anim";
            var existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            AnimationClip clip = existingClip != null ? existingClip : new AnimationClip();
            clip.frameRate = fps;

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            EditorCurveBinding binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

            // Keyframes
            ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = (float)i / fps,
                    value = frames[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            if (existingClip == null)
            {
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            else
            {
                EditorUtility.SetDirty(clip);
            }

            return clip;
        }

        private static AnimatorController CreateStateBasedAnimatorController(
            AnimationClip idleDown, AnimationClip idleUp, AnimationClip idleLeft, AnimationClip idleRight,
            AnimationClip runDown, AnimationClip runUp, AnimationClip runLeft, AnimationClip runRight)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            // Remove all existing parameters and add clean parameters
            for (int i = controller.parameters.Length - 1; i >= 0; i--)
            {
                controller.RemoveParameter(i);
            }

            controller.AddParameter("direction", AnimatorControllerParameterType.Int);
            controller.AddParameter("isMoving", AnimatorControllerParameterType.Bool);

            var rootStateMachine = controller.layers[0].stateMachine;

            // Clear old states
            var oldStates = rootStateMachine.states;
            foreach (var childState in oldStates)
            {
                rootStateMachine.RemoveState(childState.state);
            }

            // Position layout coordinates
            // Left column: Idle states (x = 250)
            // Right column: Run states (x = 650)
            var stateIdleUp = rootStateMachine.AddState("Idle_Up", new Vector3(250, 0, 0));
            stateIdleUp.motion = idleUp;

            var stateIdleLeft = rootStateMachine.AddState("Idle_Left", new Vector3(250, 90, 0));
            stateIdleLeft.motion = idleLeft;

            var stateIdleRight = rootStateMachine.AddState("Idle_Right", new Vector3(250, 180, 0));
            stateIdleRight.motion = idleRight;

            var stateIdleDown = rootStateMachine.AddState("Idle_Down", new Vector3(250, 270, 0));
            stateIdleDown.motion = idleDown;

            var stateRunUp = rootStateMachine.AddState("Run_Up", new Vector3(650, 0, 0));
            stateRunUp.motion = runUp;

            var stateRunLeft = rootStateMachine.AddState("Run_Left", new Vector3(650, 90, 0));
            stateRunLeft.motion = runLeft;

            var stateRunRight = rootStateMachine.AddState("Run_Right", new Vector3(650, 180, 0));
            stateRunRight.motion = runRight;

            var stateRunDown = rootStateMachine.AddState("Run_Down", new Vector3(650, 270, 0));
            stateRunDown.motion = runDown;

            // Default state: Idle_Down
            rootStateMachine.defaultState = stateIdleDown;

            var idles = new[] { stateIdleDown, stateIdleUp, stateIdleLeft, stateIdleRight };
            var runs = new[] { stateRunDown, stateRunUp, stateRunLeft, stateRunRight };

            // 1. Idle -> Run transitions (when isMoving becomes true and direction matches)
            for (int fromIdx = 0; fromIdx < 4; fromIdx++)
            {
                for (int toIdx = 0; toIdx < 4; toIdx++)
                {
                    var t = idles[fromIdx].AddTransition(runs[toIdx]);
                    t.hasExitTime = false;
                    t.duration = 0f;
                    t.AddCondition(AnimatorConditionMode.If, 0, "isMoving");
                    t.AddCondition(AnimatorConditionMode.Equals, toIdx, "direction");
                }
            }

            // 2. Run -> Idle transitions (when isMoving becomes false, return to corresponding idle)
            for (int i = 0; i < 4; i++)
            {
                var t = runs[i].AddTransition(idles[i]);
                t.hasExitTime = false;
                t.duration = 0f;
                t.AddCondition(AnimatorConditionMode.IfNot, 0, "isMoving");
            }

            // 3. Run -> Run transitions (when direction changes while moving)
            for (int fromIdx = 0; fromIdx < 4; fromIdx++)
            {
                for (int toIdx = 0; toIdx < 4; toIdx++)
                {
                    if (fromIdx == toIdx) continue;
                    var t = runs[fromIdx].AddTransition(runs[toIdx]);
                    t.hasExitTime = false;
                    t.duration = 0f;
                    t.AddCondition(AnimatorConditionMode.If, 0, "isMoving");
                    t.AddCondition(AnimatorConditionMode.Equals, toIdx, "direction");
                }
            }

            // 4. Idle -> Idle transitions (when direction changes while standing)
            for (int fromIdx = 0; fromIdx < 4; fromIdx++)
            {
                for (int toIdx = 0; toIdx < 4; toIdx++)
                {
                    if (fromIdx == toIdx) continue;
                    var t = idles[fromIdx].AddTransition(idles[toIdx]);
                    t.hasExitTime = false;
                    t.duration = 0f;
                    t.AddCondition(AnimatorConditionMode.IfNot, 0, "isMoving");
                    t.AddCondition(AnimatorConditionMode.Equals, toIdx, "direction");
                }
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AttachToScenePlayer(AnimatorController controller)
        {
            if (!File.Exists(ScenePath)) return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = GameObject.Find("Player");
            if (player != null)
            {
                var anim = player.GetComponent<Animator>();
                if (anim == null)
                {
                    anim = player.AddComponent<Animator>();
                }
                anim.runtimeAnimatorController = controller;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[HappyFarmer] Attached Animator and Controller to Player in FarmMap scene.");
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
