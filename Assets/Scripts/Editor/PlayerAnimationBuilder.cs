using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HappyFarmer.EditorTools
{
    [InitializeOnLoad]
    public static class PlayerAnimationBuilder
    {
        private const string IdleSheetPath = "Assets/Sprites/LittleDreamyland/Sprites/Characters/Bunny/IDLE/Bunny_Idle.png";
        private const string RunSheetPath = "Assets/Sprites/LittleDreamyland/Sprites/Characters/Bunny/RUN/Bunny_Run.png";
        private const string HoeSheetPath = "Assets/Sprites/LittleDreamyland/Sprites/Characters/Bunny/HOE/Bunny_Hoe.png";
        private const string WaterSheetPath = "Assets/Sprites/LittleDreamyland/Sprites/Characters/Bunny/WATERING CAN/Bunny_WateringCan.png";
        private const string ScytheSheetPath = "Assets/Sprites/LittleDreamyland/Sprites/Characters/Bunny/SCYTHE/Bunny_Scythe.png";

        private const string AnimFolder = "Assets/Animations/Player";
        private const string ControllerPath = "Assets/Animations/Player/Bunny.controller";

        private static readonly string[] TargetScenePaths = new[]
        {
            "Assets/Scenes/FarmMap.unity",
            "Assets/Scenes/PastureScene.unity"
        };

        static PlayerAnimationBuilder()
        {
            EditorApplication.delayCall += AutoBuildIfNeeded;
        }

        private static void AutoBuildIfNeeded()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null || !controller.parameters.Any(p => p.name == "Hoe"))
            {
                Debug.Log("[HappyFarmer] Auto-building Player Animations (Idle, Run, Hoe, Water, Scythe)...");
                GenerateAnimationsAndController();
            }
        }

        [MenuItem("Happy Farmer/4. Generate Player Animations and Controller", false, 4)]
        public static void GenerateAnimationsAndController()
        {
            EnsureFolder("Assets/Animations");
            EnsureFolder(AnimFolder);

            // 1. Load sprite dictionaries
            var idleSprites = LoadSprites(IdleSheetPath);
            var runSprites = LoadSprites(RunSheetPath);
            var hoeSprites = LoadSprites(HoeSheetPath);
            var waterSprites = LoadSprites(WaterSheetPath);
            var scytheSprites = LoadSprites(ScytheSheetPath);

            // 2. Create Animation Clips (Row 0: Up, Row 1: Right, Row 2: Left, Row 3: Down)
            // Idle (5 frames, 6 FPS, Loop)
            var clipIdleUp = CreateAnimationClip("Bunny_Idle_Up", GetSprites(idleSprites, "bunny_idle_0", 5), 6f, true);
            var clipIdleRight = CreateAnimationClip("Bunny_Idle_Right", GetSprites(idleSprites, "bunny_idle_1", 5), 6f, true);
            var clipIdleLeft = CreateAnimationClip("Bunny_Idle_Left", GetSprites(idleSprites, "bunny_idle_2", 5), 6f, true);
            var clipIdleDown = CreateAnimationClip("Bunny_Idle_Down", GetSprites(idleSprites, "bunny_idle_3", 5), 6f, true);

            // Run (8 frames, 10 FPS, Loop)
            var clipRunUp = CreateAnimationClip("Bunny_Run_Up", GetSprites(runSprites, "bunny_run_0", 8), 10f, true);
            var clipRunRight = CreateAnimationClip("Bunny_Run_Right", GetSprites(runSprites, "bunny_run_1", 8), 10f, true);
            var clipRunLeft = CreateAnimationClip("Bunny_Run_Left", GetSprites(runSprites, "bunny_run_2", 8), 10f, true);
            var clipRunDown = CreateAnimationClip("Bunny_Run_Down", GetSprites(runSprites, "bunny_run_3", 8), 10f, true);

            // 1. Hoe (9 frames, 12 FPS, No Loop)
            var clipHoeUp = CreateAnimationClip("Bunny_Hoe_Up", GetSprites(hoeSprites, "bunny_hoe_0", 9), 12f, false);
            var clipHoeRight = CreateAnimationClip("Bunny_Hoe_Right", GetSprites(hoeSprites, "bunny_hoe_1", 9), 12f, false);
            var clipHoeLeft = CreateAnimationClip("Bunny_Hoe_Left", GetSprites(hoeSprites, "bunny_hoe_2", 9), 12f, false);
            var clipHoeDown = CreateAnimationClip("Bunny_Hoe_Down", GetSprites(hoeSprites, "bunny_hoe_3", 9), 12f, false);

            // 2. Watering Can (9 frames, 12 FPS, No Loop)
            var clipWaterUp = CreateAnimationClip("Bunny_Water_Up", GetSprites(waterSprites, "bunny_wateringcan_0", 9), 12f, false);
            var clipWaterRight = CreateAnimationClip("Bunny_Water_Right", GetSprites(waterSprites, "bunny_wateringcan_1", 9), 12f, false);
            var clipWaterLeft = CreateAnimationClip("Bunny_Water_Left", GetSprites(waterSprites, "bunny_wateringcan_2", 9), 12f, false);
            var clipWaterDown = CreateAnimationClip("Bunny_Water_Down", GetSprites(waterSprites, "bunny_wateringcan_3", 9), 12f, false);

            // 3. Scythe (9 frames, 12 FPS, No Loop)
            var clipScytheUp = CreateAnimationClip("Bunny_Scythe_Up", GetSprites(scytheSprites, "bunny_scythe_0", 9), 12f, false);
            var clipScytheRight = CreateAnimationClip("Bunny_Scythe_Right", GetSprites(scytheSprites, "bunny_scythe_1", 9), 12f, false);
            var clipScytheLeft = CreateAnimationClip("Bunny_Scythe_Left", GetSprites(scytheSprites, "bunny_scythe_2", 9), 12f, false);
            var clipScytheDown = CreateAnimationClip("Bunny_Scythe_Down", GetSprites(scytheSprites, "bunny_scythe_3", 9), 12f, false);

            // 3. Create Clean 2D Blend Tree Animator Controller
            var controller = BuildAnimatorController(
                clipIdleDown, clipIdleUp, clipIdleLeft, clipIdleRight,
                clipRunDown, clipRunUp, clipRunLeft, clipRunRight,
                clipHoeDown, clipHoeUp, clipHoeLeft, clipHoeRight,
                clipWaterDown, clipWaterUp, clipWaterLeft, clipWaterRight,
                clipScytheDown, clipScytheUp, clipScytheLeft, clipScytheRight
            );

            // 4. Attach to Player in all target scenes
            AttachToScenePlayer(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HappyFarmer] Successfully created Farmer Action Animations and Controller at " + AnimFolder);
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

        private static AnimationClip CreateAnimationClip(string clipName, Sprite[] frames, float fps, bool loop)
        {
            if (frames == null || frames.Length == 0) return null;

            string clipPath = $"{AnimFolder}/{clipName}.anim";
            var existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            AnimationClip clip = existingClip != null ? existingClip : new AnimationClip();
            clip.frameRate = fps;

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            EditorCurveBinding binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

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

        private static AnimatorController BuildAnimatorController(
            AnimationClip idleDown, AnimationClip idleUp, AnimationClip idleLeft, AnimationClip idleRight,
            AnimationClip runDown, AnimationClip runUp, AnimationClip runLeft, AnimationClip runRight,
            AnimationClip hoeDown, AnimationClip hoeUp, AnimationClip hoeLeft, AnimationClip hoeRight,
            AnimationClip waterDown, AnimationClip waterUp, AnimationClip waterLeft, AnimationClip waterRight,
            AnimationClip scytheDown, AnimationClip scytheUp, AnimationClip scytheLeft, AnimationClip scytheRight)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            // Reset parameters cleanly
            for (int i = controller.parameters.Length - 1; i >= 0; i--)
            {
                controller.RemoveParameter(i);
            }

            controller.AddParameter(new AnimatorControllerParameter { name = "MoveX", type = AnimatorControllerParameterType.Float, defaultFloat = 0f });
            controller.AddParameter(new AnimatorControllerParameter { name = "MoveY", type = AnimatorControllerParameterType.Float, defaultFloat = 0f });
            controller.AddParameter(new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float, defaultFloat = 0f });
            controller.AddParameter(new AnimatorControllerParameter { name = "LastMoveX", type = AnimatorControllerParameterType.Float, defaultFloat = 0f });
            controller.AddParameter(new AnimatorControllerParameter { name = "LastMoveY", type = AnimatorControllerParameterType.Float, defaultFloat = -1f });
            controller.AddParameter(new AnimatorControllerParameter { name = "Hoe", type = AnimatorControllerParameterType.Trigger });
            controller.AddParameter(new AnimatorControllerParameter { name = "Water", type = AnimatorControllerParameterType.Trigger });
            controller.AddParameter(new AnimatorControllerParameter { name = "Scythe", type = AnimatorControllerParameterType.Trigger });

            var rootStateMachine = controller.layers[0].stateMachine;

            // Clear old states
            var oldStates = rootStateMachine.states;
            foreach (var childState in oldStates)
            {
                rootStateMachine.RemoveState(childState.state);
            }

            // 1. Idle State
            BlendTree idleBlendTree;
            var idleState = controller.CreateBlendTreeInController("Idle", out idleBlendTree);
            idleBlendTree.blendType = BlendTreeType.SimpleDirectional2D;
            idleBlendTree.blendParameter = "LastMoveX";
            idleBlendTree.blendParameterY = "LastMoveY";
            if (idleDown != null) idleBlendTree.AddChild(idleDown, new Vector2(0, -1));
            if (idleUp != null) idleBlendTree.AddChild(idleUp, new Vector2(0, 1));
            if (idleLeft != null) idleBlendTree.AddChild(idleLeft, new Vector2(-1, 0));
            if (idleRight != null) idleBlendTree.AddChild(idleRight, new Vector2(1, 0));

            // 2. Run State
            BlendTree runBlendTree;
            var runState = controller.CreateBlendTreeInController("Run", out runBlendTree);
            runBlendTree.blendType = BlendTreeType.SimpleDirectional2D;
            runBlendTree.blendParameter = "MoveX";
            runBlendTree.blendParameterY = "MoveY";
            if (runDown != null) runBlendTree.AddChild(runDown, new Vector2(0, -1));
            if (runUp != null) runBlendTree.AddChild(runUp, new Vector2(0, 1));
            if (runLeft != null) runBlendTree.AddChild(runLeft, new Vector2(-1, 0));
            if (runRight != null) runBlendTree.AddChild(runRight, new Vector2(1, 0));

            // 3. Hoe State (Action 1)
            BlendTree hoeBlendTree;
            var hoeState = controller.CreateBlendTreeInController("Hoe", out hoeBlendTree);
            hoeBlendTree.blendType = BlendTreeType.SimpleDirectional2D;
            hoeBlendTree.blendParameter = "LastMoveX";
            hoeBlendTree.blendParameterY = "LastMoveY";
            if (hoeDown != null) hoeBlendTree.AddChild(hoeDown, new Vector2(0, -1));
            if (hoeUp != null) hoeBlendTree.AddChild(hoeUp, new Vector2(0, 1));
            if (hoeLeft != null) hoeBlendTree.AddChild(hoeLeft, new Vector2(-1, 0));
            if (hoeRight != null) hoeBlendTree.AddChild(hoeRight, new Vector2(1, 0));

            // 4. Water State (Action 2)
            BlendTree waterBlendTree;
            var waterState = controller.CreateBlendTreeInController("Water", out waterBlendTree);
            waterBlendTree.blendType = BlendTreeType.SimpleDirectional2D;
            waterBlendTree.blendParameter = "LastMoveX";
            waterBlendTree.blendParameterY = "LastMoveY";
            if (waterDown != null) waterBlendTree.AddChild(waterDown, new Vector2(0, -1));
            if (waterUp != null) waterBlendTree.AddChild(waterUp, new Vector2(0, 1));
            if (waterLeft != null) waterBlendTree.AddChild(waterLeft, new Vector2(-1, 0));
            if (waterRight != null) waterBlendTree.AddChild(waterRight, new Vector2(1, 0));

            // 5. Scythe State (Action 3)
            BlendTree scytheBlendTree;
            var scytheState = controller.CreateBlendTreeInController("Scythe", out scytheBlendTree);
            scytheBlendTree.blendType = BlendTreeType.SimpleDirectional2D;
            scytheBlendTree.blendParameter = "LastMoveX";
            scytheBlendTree.blendParameterY = "LastMoveY";
            if (scytheDown != null) scytheBlendTree.AddChild(scytheDown, new Vector2(0, -1));
            if (scytheUp != null) scytheBlendTree.AddChild(scytheUp, new Vector2(0, 1));
            if (scytheLeft != null) scytheBlendTree.AddChild(scytheLeft, new Vector2(-1, 0));
            if (scytheRight != null) scytheBlendTree.AddChild(scytheRight, new Vector2(1, 0));

            // Default State
            rootStateMachine.defaultState = idleState;

            // Movement Transitions: Idle <-> Run
            var idleToRun = idleState.AddTransition(runState);
            idleToRun.hasExitTime = false;
            idleToRun.duration = 0f;
            idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.01f, "Speed");

            var runToIdle = runState.AddTransition(idleState);
            runToIdle.hasExitTime = false;
            runToIdle.duration = 0f;
            runToIdle.AddCondition(AnimatorConditionMode.Less, 0.01f, "Speed");

            // Action 1: Hoe Transitions
            AddActionTransition(idleState, hoeState, "Hoe");
            AddActionTransition(runState, hoeState, "Hoe");
            AddReturnTransition(hoeState, idleState);

            // Action 2: Water Transitions
            AddActionTransition(idleState, waterState, "Water");
            AddActionTransition(runState, waterState, "Water");
            AddReturnTransition(waterState, idleState);

            // Action 3: Scythe Transitions
            AddActionTransition(idleState, scytheState, "Scythe");
            AddActionTransition(runState, scytheState, "Scythe");
            AddReturnTransition(scytheState, idleState);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddActionTransition(AnimatorState from, AnimatorState to, string trigger)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0f;
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        }

        private static void AddReturnTransition(AnimatorState from, AnimatorState to)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 1f;
            t.duration = 0f;
        }

        private static void AttachToScenePlayer(AnimatorController controller)
        {
            foreach (var scenePath in TargetScenePaths)
            {
                if (!File.Exists(scenePath)) continue;

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var player = GameObject.Find("Player");
                if (player != null)
                {
                    var shadow = player.transform.Find("Shadow");
                    if (shadow != null)
                    {
                        Object.DestroyImmediate(shadow.gameObject);
                    }

                    var anim = player.GetComponent<Animator>();
                    if (anim == null)
                    {
                        anim = player.AddComponent<Animator>();
                    }
                    anim.runtimeAnimatorController = controller;
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[HappyFarmer] Attached Animator and Controller to Player in {Path.GetFileName(scenePath)}.");
                }
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
