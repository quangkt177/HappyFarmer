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

            // 2. Create Animator Controller
            var controller = CreateAnimatorController(
                clipIdleDown, clipIdleUp, clipIdleLeft, clipIdleRight,
                clipRunDown, clipRunUp, clipRunLeft, clipRunRight
            );

            // 3. Attach to Player in Scene if Scene exists
            AttachToScenePlayer(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HappyFarmer] Successfully created Player animations and AnimatorController at " + AnimFolder);
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

        private static AnimatorController CreateAnimatorController(
            AnimationClip idleDown, AnimationClip idleUp, AnimationClip idleLeft, AnimationClip idleRight,
            AnimationClip runDown, AnimationClip runUp, AnimationClip runLeft, AnimationClip runRight)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            // Ensure parameters exist
            EnsureParameter(controller, "MoveX", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "MoveY", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "LastMoveX", AnimatorControllerParameterType.Float, 0f);
            EnsureParameter(controller, "LastMoveY", AnimatorControllerParameterType.Float, -1f);

            var rootStateMachine = controller.layers[0].stateMachine;

            // Clear old states to avoid duplicating
            var oldStates = rootStateMachine.states;
            foreach (var childState in oldStates)
            {
                rootStateMachine.RemoveState(childState.state);
            }

            // Create Idle Blend Tree
            BlendTree idleBlendTree;
            var idleState = controller.CreateBlendTreeInController("Idle", out idleBlendTree);
            idleBlendTree.blendType = BlendTreeType.SimpleDirectional2D;
            idleBlendTree.blendParameter = "LastMoveX";
            idleBlendTree.blendParameterY = "LastMoveY";
            if (idleDown != null) idleBlendTree.AddChild(idleDown, new Vector2(0, -1));
            if (idleUp != null) idleBlendTree.AddChild(idleUp, new Vector2(0, 1));
            if (idleLeft != null) idleBlendTree.AddChild(idleLeft, new Vector2(-1, 0));
            if (idleRight != null) idleBlendTree.AddChild(idleRight, new Vector2(1, 0));

            // Create Run Blend Tree
            BlendTree runBlendTree;
            var runState = controller.CreateBlendTreeInController("Run", out runBlendTree);
            runBlendTree.blendType = BlendTreeType.SimpleDirectional2D;
            runBlendTree.blendParameter = "MoveX";
            runBlendTree.blendParameterY = "MoveY";
            if (runDown != null) runBlendTree.AddChild(runDown, new Vector2(0, -1));
            if (runUp != null) runBlendTree.AddChild(runUp, new Vector2(0, 1));
            if (runLeft != null) runBlendTree.AddChild(runLeft, new Vector2(-1, 0));
            if (runRight != null) runBlendTree.AddChild(runRight, new Vector2(1, 0));

            // Set Idle as Default State
            rootStateMachine.defaultState = idleState;

            // Idle -> Run Transition
            var idleToRun = idleState.AddTransition(runState);
            idleToRun.hasExitTime = false;
            idleToRun.duration = 0f;
            idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.01f, "Speed");

            // Run -> Idle Transition
            var runToIdle = runState.AddTransition(idleState);
            runToIdle.hasExitTime = false;
            runToIdle.duration = 0f;
            runToIdle.AddCondition(AnimatorConditionMode.Less, 0.01f, "Speed");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnsureParameter(AnimatorController controller, string paramName, AnimatorControllerParameterType type, float defaultValue = 0f)
        {
            var p = controller.parameters.FirstOrDefault(param => param.name == paramName);
            if (p == null)
            {
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = paramName,
                    type = type,
                    defaultFloat = defaultValue
                });
            }
            else if (type == AnimatorControllerParameterType.Float)
            {
                p.defaultFloat = defaultValue;
            }
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

