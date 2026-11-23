using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Turn.Core;
using Turn.View;
using Object = UnityEngine.Object;

namespace Turn.EditorTools
{
    /// <summary>
    /// 白模生成器的「世界」部分（程序A）：玩家、鲨鱼、传送门 / 冲击波特效、场景、位面 Root、HUD Canvas。
    /// 共用常量、菜单入口、Sprite、Prefab 总装与通用工具在 TurnWhiteboxBuilder.cs；怪物 / 子弹 / 经验球在 TurnWhiteboxBuilder.Combat.cs。
    /// </summary>
    public static partial class TurnWhiteboxBuilder
    {
        // ================================================================
        // Animator：五个状态，AnyState → 状态（State == n）
        // ================================================================

        private static RuntimeAnimatorController BuildAnimator()
        {
            if (File.Exists(AnimatorPath)) AssetDatabase.DeleteAsset(AnimatorPath);
            AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);
            ctrl.AddParameter("State", AnimatorControllerParameterType.Int);
            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

            string[] names = Enum.GetNames(typeof(PlayerAnimState)); // Idle, Move, Roll, Hurt, Death
            for (int i = 0; i < names.Length; i++)
            {
                AnimatorState state = sm.AddState(names[i], new Vector3(300f, 60f * i, 0f));
                state.writeDefaultValues = false; // 没放动画片段时不会改写占位表现
                if (i == 0) sm.defaultState = state;
                AnimatorStateTransition t = sm.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.Equals, i, "State");
                t.hasExitTime = false;
                t.duration = 0f;
                t.canTransitionToSelf = false;
            }
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        private static GameObject BuildPlayer(RuntimeAnimatorController animator, PrefabSet set)
        {
            GameObject root = new GameObject("Player");
            root.tag = "Player";
            var controller = root.AddComponent<PlayerController>();
            var detector = root.AddComponent<TerrainDetector>();

            MakeSprite("Shadow", root.transform, S("shadow"), new Color(0f, 0f, 0f, 0.35f), ViewMath.ShadowOrder,
                new Vector3(0f, -0.36f, 0f), new Vector3(0.9f, 0.35f, 1f));
            SpriteRenderer marker = MakeSprite("TerrainMarker", root.transform, S("ring"), Color.white, ViewMath.ShadowOrder + 1,
                new Vector3(0f, -0.36f, 0f), new Vector3(1f, 0.4f, 1f));
            marker.enabled = false;

            Transform body = Child("Body", root.transform, Vector3.zero, Vector3.one);
            SpriteRenderer bodySprite = MakeSprite("Sprite", body, S("circle"), new Color(0.92f, 0.95f, 1f, 1f), ViewMath.PlayerOrder,
                Vector3.zero, new Vector3(0.75f, 0.75f, 1f));
            MakeSprite("Eye", body, S("circle"), new Color(0.1f, 0.12f, 0.2f, 1f), ViewMath.PlayerOrder + 1,
                new Vector3(0.14f, 0.1f, 0f), new Vector3(0.16f, 0.16f, 1f));
            var anim = body.gameObject.AddComponent<Animator>();
            anim.runtimeAnimatorController = animator;

            Transform arrow = Child("FacingArrow", root.transform, Vector3.zero, Vector3.one);
            MakeSprite("Arrow", arrow, S("triangle"), new Color(1f, 1f, 1f, 0.8f), ViewMath.PlayerOrder + 1,
                new Vector3(0.58f, 0f, 0f), new Vector3(0.22f, 0.22f, 1f));

            SpriteRenderer shield = MakeSprite("ShieldRing", root.transform, S("ring"), new Color(0.6f, 0.9f, 1f, 0.85f), ViewMath.PlayerOrder + 2,
                Vector3.zero, new Vector3(1.15f, 1.15f, 1f));
            shield.enabled = false;

            Transform weaponNode = Child("Weapon", root.transform, Vector3.zero, Vector3.one);
            var weapon = weaponNode.gameObject.AddComponent<WeaponController>();
            SpriteRenderer icon = MakeSprite("FormIcon", weaponNode, S("circle"), ViewMath.MapColor(PlaneId.Volcano), ViewMath.PlayerOrder + 3,
                new Vector3(0.36f, 0.32f, 0f), new Vector3(0.24f, 0.24f, 1f));

            Set(controller, "body", body);
            Set(controller, "bodyRenderer", bodySprite);
            Set(controller, "animator", anim);
            Set(controller, "shieldRing", shield);
            Set(controller, "facingArrow", arrow);
            Set(detector, "marker", marker);
            Set(weapon, "formIcon", icon);
            Set(weapon, "fireball.prefab", set.Fireball != null ? set.Fireball.GetComponent<Projectile>() : null);
            Set(weapon, "harpoon.prefab", set.Harpoon != null ? set.Harpoon.GetComponent<Projectile>() : null);
            return root;
        }

        private static GameObject BuildShark()
        {
            GameObject root = new GameObject("Shark");
            root.tag = TagShark;
            var ai = root.AddComponent<SharkAI>();

            MakeSprite("Shadow", root.transform, S("shadow"), new Color(0f, 0f, 0f, 0.3f), ViewMath.ShadowOrder,
                new Vector3(0f, -0.3f, 0f), new Vector3(1.4f, 0.4f, 1f));
            Transform body = Child("Body", root.transform, Vector3.zero, Vector3.one);
            MakeSprite("Hull", body, S("circle"), new Color(0.38f, 0.48f, 0.62f, 1f), ViewMath.SharkOrder,
                Vector3.zero, new Vector3(1.4f, 0.7f, 1f));
            MakeSprite("Nose", body, S("triangle"), new Color(0.38f, 0.48f, 0.62f, 1f), ViewMath.SharkOrder,
                new Vector3(0.75f, 0f, 0f), new Vector3(0.4f, 0.5f, 1f));
            MakeSprite("Tail", body, S("triangle"), new Color(0.3f, 0.38f, 0.5f, 1f), ViewMath.SharkOrder,
                new Vector3(-0.8f, 0f, 0f), new Vector3(-0.45f, 0.7f, 1f));
            MakeSprite("Fin", body, S("triangle"), new Color(0.22f, 0.28f, 0.4f, 1f), ViewMath.SharkOrder + 1,
                new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0.45f, 1f));

            Set(ai, "body", body);
            return root;
        }

        private static GameObject BuildPortal()
        {
            GameObject root = new GameObject("PortalVFX");
            var vfx = root.AddComponent<TimedVfx>();
            SpriteRenderer outer = MakeSprite("Outer", root.transform, S("ring"), new Color(0.7f, 0.4f, 1f, 0.9f), ViewMath.VfxOrder,
                Vector3.zero, new Vector3(1.3f, 1.3f, 1f));
            SpriteRenderer inner = MakeSprite("Inner", root.transform, S("ring"), new Color(0.4f, 0.9f, 1f, 0.75f), ViewMath.VfxOrder + 1,
                Vector3.zero, new Vector3(0.85f, 0.85f, 1f));
            SetFloat(vfx, "lifetime", 0.5f);
            SetFloat(vfx, "startScale", 0.6f);
            SetFloat(vfx, "endScale", 1.5f);
            SetFloat(vfx, "spinSpeed", 360f);
            SetObjectArray(vfx, "renderers", new Object[] { outer, inner });
            return root;
        }

        private static GameObject BuildShockwave()
        {
            GameObject root = new GameObject("RollShockwaveVFX");
            var vfx = root.AddComponent<TimedVfx>();
            SpriteRenderer ring = MakeSprite("Ring", root.transform, S("ring"), new Color(1f, 0.75f, 0.3f, 0.9f), ViewMath.VfxOrder,
                Vector3.zero, Vector3.one);
            // ring 贴图直径 1 单位：scale = 1 时半径 0.5（VfxController.shockwaveUnitRadius），endScale = 1 → 最终半径 = 实际判定半径
            SetFloat(vfx, "lifetime", 0.35f);
            SetFloat(vfx, "startScale", 0.3f);
            SetFloat(vfx, "endScale", 1f);
            SetFloat(vfx, "spinSpeed", 0f);
            SetObjectArray(vfx, "renderers", new Object[] { ring });
            return root;
        }

        // ================================================================
        // 场景
        // ================================================================

        private static void BuildScene(PrefabSet prefabs)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Rect rect = new Rect(-MapSize * 0.5f, MapSize);

            // ---- 相机 / EventSystem ----
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f, 1f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<CameraFollow>();

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            Type module = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (module != null) eventSystem.AddComponent(module);
            else eventSystem.AddComponent<StandaloneInputModule>();

            // ---- 管理器 ----
            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            var mapGo = new GameObject("MapManager");
            var mapManager = mapGo.AddComponent<MapManager>();

            // ---- 两套地图：同一 Scene、同一原点、同一尺寸 ----
            MapParts volcano = BuildMapRoot("Volcano", rect, new Color(0.22f, 0.13f, 0.11f, 1f), new Color(0.55f, 0.2f, 0.08f, 1f),
                TerrainType.Lava, "LavaGrid", new Color(1f, 0.42f, 0.05f, 0.9f), 11, 7, true);
            MapParts ocean = BuildMapRoot("Ocean", rect, new Color(0.07f, 0.2f, 0.32f, 1f), new Color(0.2f, 0.5f, 0.72f, 1f),
                TerrainType.ShallowWater, "ShallowWaterGrid", new Color(0.45f, 0.85f, 0.95f, 0.55f), 23, 8, false);

            // ---- 统一边界 ----
            var boundsGo = new GameObject("WorldBounds");
            var worldBounds = boundsGo.AddComponent<WorldBounds>();
            var colGo = new GameObject("BoundsCollider");
            colGo.transform.SetParent(boundsGo.transform, false);
            var col = colGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = MapSize;
            var lineGo = new GameObject("BoundsDebug");
            lineGo.transform.SetParent(boundsGo.transform, false);
            var line = lineGo.AddComponent<LineRenderer>();
            line.sharedMaterial = _material;
            SetVector2(worldBounds, "center", Vector2.zero);
            SetVector2(worldBounds, "size", MapSize);
            Set(worldBounds, "boundsCollider", col);
            Set(worldBounds, "boundsDebug", line);
            worldBounds.SyncHelpers();

            // ---- 玩家（Prefab 实例）----
            var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.Player, scene);
            playerGo.name = "Player";
            playerGo.transform.position = Vector3.zero;
            var playerController = playerGo.GetComponent<PlayerController>();
            var weapon = playerGo.GetComponentInChildren<WeaponController>(true);
            var detector = playerGo.GetComponent<TerrainDetector>();

            Transform monsterContainer = new GameObject("MonsterContainer").transform;
            Transform sharkContainer = new GameObject("SharkContainer").transform;
            Transform projectileContainer = new GameObject("ProjectileContainer").transform;
            Transform experienceContainer = new GameObject("ExperienceContainer").transform;

            var spawnGo = new GameObject("SpawnManager");
            var spawnManager = spawnGo.AddComponent<SpawnManager>();
            Set(spawnManager, "monsterPrefab", prefabs.Monster != null ? prefabs.Monster.GetComponent<MonsterController>() : null);
            Set(spawnManager, "sharkPrefab", prefabs.Shark != null ? prefabs.Shark.GetComponent<SharkAI>() : null);

            var upgradeGo = new GameObject("UpgradeManager");
            var upgradeManager = upgradeGo.AddComponent<UpgradeManager>();
            Set(upgradeManager, "experienceOrbPrefab", prefabs.Orb != null ? prefabs.Orb.GetComponent<ExperienceOrb>() : null);

            // ---- UI ----
            UiParts ui = BuildCanvas();

            // ---- 特效 ----
            var vfxGo = new GameObject("VFXContainer");
            var vfx = vfxGo.AddComponent<VfxController>();
            Set(vfx, "portalPrefab", prefabs.Portal != null ? prefabs.Portal.GetComponent<TimedVfx>() : null);
            Set(vfx, "shockwavePrefab", prefabs.Shockwave != null ? prefabs.Shockwave.GetComponent<TimedVfx>() : null);
            SetFloat(vfx, "shockwaveUnitRadius", 0.5f);
            Set(vfx, "player", playerGo.transform);
            Set(vfx, "hud", ui.Hud);

            // ---- 拖引用 ----
            Set(mapManager, "volcanoRoot", volcano.Root);
            Set(mapManager, "oceanRoot", ocean.Root);
            Set(mapManager, "volcanoGround", volcano.Ground);
            Set(mapManager, "oceanGround", ocean.Ground);
            Set(mapManager, "lavaGrid", volcano.Grid);
            Set(mapManager, "shallowWaterGrid", ocean.Grid);
            Set(mapManager, "volcanoSpawnPoints", volcano.SpawnPoints);
            Set(mapManager, "oceanSpawnPoints", ocean.SpawnPoints);
            Set(mapManager, "worldBounds", worldBounds);

            Set(follow, "target", playerGo.transform);
            Set(follow, "bounds", worldBounds);

            Set(gm, "mapManager", mapManager);
            Set(gm, "worldBounds", worldBounds);
            Set(gm, "player", playerController);
            Set(gm, "weapon", weapon);
            Set(gm, "terrainDetector", detector);
            Set(gm, "spawnManager", spawnManager);
            Set(gm, "upgradeManager", upgradeManager);
            Set(gm, "hud", ui.Hud);
            Set(gm, "upgradeUI", ui.Upgrade);
            Set(gm, "vfx", vfx);
            Set(gm, "cameraFollow", follow);
            Set(gm, "monsterContainer", monsterContainer);
            Set(gm, "sharkContainer", sharkContainer);
            Set(gm, "projectileContainer", projectileContainer);
            Set(gm, "experienceContainer", experienceContainer);

            // ---- Hierarchy 顺序（与题目一致）----
            int order = 0;
            foreach (Transform t in new[]
                     {
                         gmGo.transform, mapGo.transform, volcano.Root.transform, ocean.Root.transform, boundsGo.transform,
                         playerGo.transform, monsterContainer, sharkContainer, projectileContainer, experienceContainer,
                         spawnGo.transform, upgradeGo.transform, ui.Canvas.transform, vfxGo.transform, camGo.transform,
                         eventSystem.transform,
                     })
            {
                t.SetSiblingIndex(order++);
            }

            // ---- 地形网格：两张图按同一个边界生成 ----
            volcano.Grid.Generate(rect, CellSize);
            ocean.Grid.Generate(rect, CellSize);
            mapManager.SyncTerrainShape(rect); // 水池完全复制岩浆池的形状（一一对应）
            EditorUtility.SetDirty(volcano.Grid);
            EditorUtility.SetDirty(ocean.Grid);

            // ---- 其他 4 个位面的空 Root + 火山 / 海洋的白模遮挡物 ----
            AddMissingPlaneRoots(mapManager, rect);
            PlaceWhiteboxObstacles(mapManager);

            mapManager.AlignRoots();
            mapManager.Apply(PlaneId.Volcano); // 场景里同样只有一张图是激活的
            ApplyMaterialInScene(scene);

            // ---- 音频：有素材就自动挂上 TurnAudio 并填好槽位 ----
            if (AssetDatabase.IsValidFolder(TurnAudioSetup.AudioDir))
            {
                int missingAudio;
                TurnAudioSetup.Apply(gmGo, out missingAudio);
            }

            Directory.CreateDirectory(SceneDir);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Selection.activeGameObject = gmGo;
        }

        private sealed class MapParts
        {
            public GameObject Root;
            public SpriteRenderer Ground;
            public TerrainGrid Grid;
            public Transform SpawnPoints;
        }

        private static MapParts BuildMapRoot(string prefix, Rect rect, Color groundColor, Color borderColor,
            TerrainType paint, string gridName, Color cellColor, int seed, int blobs, bool active)
        {
            var parts = new MapParts();
            parts.Root = new GameObject(prefix + "Root");
            parts.Root.tag = TagMapRoot;
            Transform root = parts.Root.transform;

            // Visual：边框 + 装饰（纯表现）
            Transform visual = Child(prefix + "Visual", root, Vector3.zero, Vector3.one);
            float t = 0.5f;
            MakeSprite("BorderTop", visual, S("square"), borderColor, ViewMath.DecorOrder, new Vector3(rect.center.x, rect.yMax + t * 0.5f, 0f), new Vector3(rect.width + t * 2f, t, 1f));
            MakeSprite("BorderBottom", visual, S("square"), borderColor, ViewMath.DecorOrder, new Vector3(rect.center.x, rect.yMin - t * 0.5f, 0f), new Vector3(rect.width + t * 2f, t, 1f));
            MakeSprite("BorderLeft", visual, S("square"), borderColor, ViewMath.DecorOrder, new Vector3(rect.xMin - t * 0.5f, rect.center.y, 0f), new Vector3(t, rect.height, 1f));
            MakeSprite("BorderRight", visual, S("square"), borderColor, ViewMath.DecorOrder, new Vector3(rect.xMax + t * 0.5f, rect.center.y, 0f), new Vector3(t, rect.height, 1f));
            var rng = new System.Random(seed * 7);
            Color decor = Color.Lerp(groundColor, borderColor, 0.45f);
            for (int i = 0; i < 14; i++)
            {
                float x = Mathf.Lerp(rect.xMin + 1f, rect.xMax - 1f, (float)rng.NextDouble());
                float y = Mathf.Lerp(rect.yMin + 1f, rect.yMax - 1f, (float)rng.NextDouble());
                float s = Mathf.Lerp(0.3f, 0.8f, (float)rng.NextDouble());
                MakeSprite("Decor" + i, visual, S(paint == TerrainType.Lava ? "circle" : "diamond"), decor, ViewMath.DecorOrder,
                    new Vector3(x, y, 0f), new Vector3(s, s, 1f));
            }

            // Ground：铺满边界
            parts.Ground = MakeSprite(prefix + "Ground", root, S("square"), groundColor, ViewMath.GroundOrder,
                new Vector3(rect.center.x, rect.center.y, 0f), new Vector3(rect.width, rect.height, 1f));

            // 地形网格
            var gridGo = new GameObject(gridName, typeof(SpriteRenderer));
            gridGo.transform.SetParent(root, false);
            parts.Grid = gridGo.AddComponent<TerrainGrid>();
            SetEnum(parts.Grid, "paintType", (int)paint);
            SetColor(parts.Grid, "cellColor", cellColor);
            SetInt(parts.Grid, "seed", seed);
            SetInt(parts.Grid, "blobCount", blobs);

            // 刷怪点（离边界 2 格的一圈）
            parts.SpawnPoints = Child(prefix + "SpawnPoints", root, Vector3.zero, Vector3.one);
            Vector2[] points =
            {
                new Vector2(rect.xMin + 2f, rect.yMin + 2f), new Vector2(rect.center.x, rect.yMin + 2f), new Vector2(rect.xMax - 2f, rect.yMin + 2f),
                new Vector2(rect.xMax - 2f, rect.center.y), new Vector2(rect.xMax - 2f, rect.yMax - 2f), new Vector2(rect.center.x, rect.yMax - 2f),
                new Vector2(rect.xMin + 2f, rect.yMax - 2f), new Vector2(rect.xMin + 2f, rect.center.y),
            };
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 offset = paint == TerrainType.Lava ? Vector2.zero : new Vector2(i % 2 == 0 ? 3f : -3f, 0f);
                Child("SpawnPoint_" + i, parts.SpawnPoints, (Vector3)(points[i] + offset), Vector3.one);
            }

            parts.Root.SetActive(active);
            return parts;
        }

        // ================================================================
        // UI
        // ================================================================

        private sealed class UiParts
        {
            public GameObject Canvas;
            public HudUI Hud;
            public UpgradeUI Upgrade;
        }

        private static UiParts BuildCanvas()
        {
            var parts = new UiParts();
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            parts.Canvas = go;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            parts.Hud = go.AddComponent<HudUI>();
            RectTransform root = (RectTransform)go.transform;

            Image flash = UIImage("ScreenFlash", root, new Color(0f, 0f, 0f, 0f));
            Stretch(flash.rectTransform);

            // HealthUI（左上）
            RectTransform health = UIRect("HealthUI", root);
            Place(health, new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(320f, 26f));
            Image hpBack = UIImage("Back", health, new Color(0.1f, 0.05f, 0.06f, 0.85f));
            Stretch(hpBack.rectTransform);
            Image hpFill = UIImage("Fill", hpBack.rectTransform, new Color(0.85f, 0.2f, 0.25f, 1f));
            FillBar(hpFill.rectTransform);
            Text hpText = UIText("Text", health, "生命", 16, TextAnchor.MiddleLeft, Color.white);
            Stretch(hpText.rectTransform);
            hpText.rectTransform.offsetMin = new Vector2(8f, 0f);

            // AmmoUI（左上第二行 + 屏幕下方警告）
            RectTransform ammo = UIRect("AmmoUI", root);
            Place(ammo, new Vector2(0f, 1f), new Vector2(16f, -48f), new Vector2(560f, 30f));
            Text ammoText = UIText("Text", ammo, "火球 ■■■■■■ 6/6", 20, TextAnchor.MiddleLeft, Color.white);
            Stretch(ammoText.rectTransform);
            Text ammoWarning = UIText("EmptyWarning", root, "弹匣已空！按 Space 翻滚装填", 26, TextAnchor.MiddleCenter,
                new Color(1f, 0.35f, 0.3f, 1f));
            Place(ammoWarning.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(700f, 40f));
            ammoWarning.enabled = false;

            // ExperienceUI（底部）
            RectTransform xp = UIRect("ExperienceUI", root);
            xp.anchorMin = new Vector2(0f, 0f);
            xp.anchorMax = new Vector2(1f, 0f);
            xp.pivot = new Vector2(0.5f, 0f);
            xp.offsetMin = new Vector2(0f, 0f);
            xp.offsetMax = new Vector2(0f, 16f);
            Image xpBack = UIImage("Back", xp, new Color(0.05f, 0.06f, 0.1f, 0.9f));
            Stretch(xpBack.rectTransform);
            Image xpFill = UIImage("Fill", xpBack.rectTransform, new Color(0.3f, 0.85f, 1f, 1f));
            FillBar(xpFill.rectTransform);
            Text levelText = UIText("Level", root, "Lv 1", 18, TextAnchor.MiddleLeft, new Color(0.7f, 0.95f, 1f, 1f));
            Place(levelText.rectTransform, new Vector2(0f, 0f), new Vector2(16f, 20f), new Vector2(300f, 26f));

            // MapUI（右上）
            RectTransform mapUi = UIRect("MapUI", root);
            Place(mapUi, new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(460f, 130f));
            Text mapText = UIText("Map", mapUi, "当前地图：火山", 22, TextAnchor.UpperRight, Color.white);
            Place(mapText.rectTransform, new Vector2(1f, 1f), Vector2.zero, new Vector2(460f, 30f));
            RectTransform rollBar = UIRect("RollBar", mapUi);
            Place(rollBar, new Vector2(1f, 1f), new Vector2(0f, -36f), new Vector2(260f, 14f));
            Image rollBack = UIImage("Back", rollBar, new Color(0.1f, 0.1f, 0.14f, 0.9f));
            Stretch(rollBack.rectTransform);
            Image rollFill = UIImage("Fill", rollBack.rectTransform, new Color(0.5f, 1f, 0.6f, 1f));
            FillBar(rollFill.rectTransform);
            Text rollText = UIText("Roll", mapUi, "翻滚就绪 [Space]", 16, TextAnchor.UpperRight, new Color(0.85f, 0.9f, 1f, 1f));
            Place(rollText.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -54f), new Vector2(460f, 24f));
            Text terrainText = UIText("Terrain", mapUi, "脚下：普通地面", 16, TextAnchor.UpperRight, new Color(0.85f, 0.85f, 0.85f, 1f));
            Place(terrainText.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -78f), new Vector2(460f, 24f));
            Text stats = UIText("Stats", root, "00:00", 18, TextAnchor.UpperCenter, new Color(0.9f, 0.9f, 0.9f, 1f));
            Place(stats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(520f, 28f));

            // UpgradePanel（三选一）
            RectTransform upgradeRoot = UIRect("UpgradePanel", root);
            Stretch(upgradeRoot);
            parts.Upgrade = upgradeRoot.gameObject.AddComponent<UpgradeUI>();
            Image window = UIImage("Window", upgradeRoot, new Color(0f, 0f, 0f, 0.65f));
            window.raycastTarget = true;
            Stretch(window.rectTransform);
            Text header = UIText("Header", window.rectTransform, "升级！", 32, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.5f, 1f));
            Place(header.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(900f, 50f));
            SerializedObject upgradeSo = new SerializedObject(parts.Upgrade);
            SerializedProperty optionList = upgradeSo.FindProperty("options");
            optionList.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                Image card = UIImage("Option" + (i + 1), window.rectTransform, new Color(0.16f, 0.17f, 0.24f, 1f));
                card.raycastTarget = true;
                Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 300f, 0f), new Vector2(280f, 300f));
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = card;
                ColorBlock colors = button.colors;
                colors.highlightedColor = new Color(1f, 1f, 0.8f, 1f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                button.colors = colors;
                Text title = UIText("Title", card.rectTransform, (i + 1) + ". 词条", 22, TextAnchor.UpperCenter, new Color(1f, 0.9f, 0.55f, 1f));
                Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(250f, 60f));
                Text desc = UIText("Description", card.rectTransform, "", 18, TextAnchor.UpperCenter, new Color(0.88f, 0.88f, 0.92f, 1f));
                Place(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(240f, 180f));

                SerializedProperty element = optionList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("button").objectReferenceValue = button;
                element.FindPropertyRelative("title").objectReferenceValue = title;
                element.FindPropertyRelative("description").objectReferenceValue = desc;
            }
            upgradeSo.FindProperty("panel").objectReferenceValue = window.gameObject;
            upgradeSo.FindProperty("header").objectReferenceValue = header;
            upgradeSo.ApplyModifiedPropertiesWithoutUndo();
            window.gameObject.SetActive(false);

            // 暂停 / 失败
            Image pause = UIImage("PausePanel", root, new Color(0f, 0f, 0f, 0.5f));
            Stretch(pause.rectTransform);
            Text pauseText = UIText("Text", pause.rectTransform, "暂停\n按 Esc 继续", 40, TextAnchor.MiddleCenter, Color.white);
            Stretch(pauseText.rectTransform);
            pause.gameObject.SetActive(false);

            Image defeat = UIImage("DefeatPanel", root, new Color(0f, 0f, 0f, 0.72f));
            defeat.raycastTarget = true;
            Stretch(defeat.rectTransform);
            Text defeatText = UIText("Text", defeat.rectTransform, "倒下了……", 32, TextAnchor.MiddleCenter, new Color(1f, 0.6f, 0.55f, 1f));
            Stretch(defeatText.rectTransform);
            defeat.gameObject.SetActive(false);

            HudUI hud = parts.Hud;
            Set(hud, "healthFill", hpFill.rectTransform);
            Set(hud, "healthText", hpText);
            Set(hud, "ammoText", ammoText);
            Set(hud, "ammoWarning", ammoWarning);
            Set(hud, "xpFill", xpFill.rectTransform);
            Set(hud, "levelText", levelText);
            Set(hud, "mapText", mapText);
            Set(hud, "rollFill", rollFill.rectTransform);
            Set(hud, "rollFillImage", rollFill);
            Set(hud, "rollText", rollText);
            Set(hud, "terrainText", terrainText);
            Set(hud, "statsText", stats);
            Set(hud, "screenFlash", flash);
            Set(hud, "pausePanel", pause.gameObject);
            Set(hud, "defeatPanel", defeat.gameObject);
            Set(hud, "defeatText", defeatText);
            return parts;
        }

        private static RectTransform UIRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static Image UIImage(string name, Transform parent, Color color)
        {
            RectTransform rt = UIRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text UIText(string name, Transform parent, string content, int size, TextAnchor anchor, Color color)
        {
            RectTransform rt = UIRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        /// <summary>锚点与轴心都设为 anchor，再按 pos 偏移、size 定尺寸。</summary>
        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>进度条填充：左对齐，HudUI 通过 anchorMax.x 设置比例。</summary>
        private static void FillBar(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ================================================================
        // M1：补齐空位面 + 白模遮挡物（可以直接在现有场景上执行，不重建场景、不动其他参数）
        // ================================================================

        private static readonly PlaneId[] ExtraPlanes = { PlaneId.Office, PlaneId.Dream, PlaneId.Primal, PlaneId.Future };
        private static readonly string[] ExtraPlaneSlots = { "office", "dream", "primal", "future" };

        [MenuItem("Turn/M1 · 补齐工位 / 梦境 / 原始 / 未来 4 个空位面（不重建场景）", false, 40)]
        public static void AddMissingPlanesMenu()
        {
            MapManager map;
            WorldBounds bounds;
            if (!FindSceneParts(out map, out bounds)) return;
            PrepareSharedAssets();
            int added = AddMissingPlaneRoots(map, bounds.Rect);
            map.AlignRoots();
            map.Apply(PlaneId.Volcano);
            EditorUtility.SetDirty(map);
            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            Debug.Log(added > 0
                ? "[Turn] 已补齐 " + added + " 个空位面 Root（地面 + 边框 + 刷怪点），并拖进 MapManager。记得保存场景。"
                : "[Turn] 4 个位面的 Root 都已存在，没有改动。");
        }

        [MenuItem("Turn/M1 · 摆放 6 个位面的白模遮挡物（不重建场景，会替换上次生成的白模遮挡物）", false, 41)]
        public static void PlaceWhiteboxObstaclesMenu()
        {
            MapManager map;
            WorldBounds bounds;
            if (!FindSceneParts(out map, out bounds)) return;
            PrepareSharedAssets();
            int count = PlaceWhiteboxObstacles(map);
            EditorUtility.SetDirty(map);
            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            Debug.Log("[Turn] 已摆放 " + count + " 个白模遮挡物（各位面 Root 下的 *Obstacles）。没有 Root 的位面会跳过，先点「补齐 4 个空位面」。记得保存场景。F2 可在运行时显示判定框。");
        }

        private static bool FindSceneParts(out MapManager map, out WorldBounds bounds)
        {
            map = null;
            bounds = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Turn] 请先退出 Play 模式。");
                return false;
            }
            SearchLoadedScenes(out map, out bounds);
            if (map == null || bounds == null)
            {
                // 当前打开的不是 TurnGame：先问要不要保存当前场景，再自动打开 TurnGame
                if (!File.Exists(ScenePath))
                {
                    Debug.LogWarning("[Turn] 找不到 " + ScenePath + "，请先用 Turn → ① 生成白模。");
                    return false;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                SearchLoadedScenes(out map, out bounds);
            }
            if (map == null || bounds == null)
            {
                Debug.LogWarning("[Turn] " + ScenePath + " 里没有 MapManager / WorldBounds。"); 
                return false;
            }
            return true;
        }

        /// <summary>在所有已加载的场景里找 MapManager / WorldBounds。</summary>
        private static void SearchLoadedScenes(out MapManager map, out WorldBounds bounds)
        {
            map = null;
            bounds = null;
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (map == null) map = root.GetComponentInChildren<MapManager>(true);
                    if (bounds == null) bounds = root.GetComponentInChildren<WorldBounds>(true);
                }
            }
        }

        /// <summary>不重新生成 Sprite：直接加载 Art 目录里已有的占位图和材质。</summary>
        private static void PrepareSharedAssets()
        {
            foreach (string key in new[] { "square", "circle", "ring", "diamond", "triangle", "harpoon", "shadow" })
            {
                if (S(key) != null) continue;
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/" + key + ".png");
                if (sprite != null) Sprites[key] = sprite;
            }
            if (_material == null) _material = LoadMaterial();
            EnsureTags(TagMapRoot);
        }

        /// <summary>给 MapManager 里还是空的位面槽位建 Root（地面 + 边框 + 刷怪点，默认隐藏），返回新建数量。</summary>
        private static int AddMissingPlaneRoots(MapManager map, Rect rect)
        {
            int added = 0;
            GameObject anchor = map.GetRoot(PlaneId.Ocean);
            for (int i = 0; i < ExtraPlanes.Length; i++)
            {
                PlaneId plane = ExtraPlanes[i];
                if (map.GetRoot(plane) != null) continue;

                Color tint = ViewMath.MapColor(plane);
                Color ground = new Color(tint.r * 0.28f, tint.g * 0.28f, tint.b * 0.28f, 1f);
                Color border = new Color(tint.r * 0.7f, tint.g * 0.7f, tint.b * 0.7f, 1f);
                MapParts parts = BuildEmptyPlaneRoot(plane.ToString(), rect, ground, border);

                string slot = ExtraPlaneSlots[i];
                Set(map, slot + ".root", parts.Root);
                Set(map, slot + ".ground", parts.Ground);
                Set(map, slot + ".spawnPoints", parts.SpawnPoints);

                if (anchor != null && anchor.transform.parent == null)
                {
                    parts.Root.transform.SetSiblingIndex(anchor.transform.GetSiblingIndex() + 1 + added);
                }
                ApplyMaterial(parts.Root);
                added++;
            }
            return added;
        }

        /// <summary>空位面：边框 + 地面 + 刷怪点（离边界 2 格的一圈），不带地形网格与装饰。M2 再按策划书补机制与美术。</summary>
        private static MapParts BuildEmptyPlaneRoot(string prefix, Rect rect, Color groundColor, Color borderColor)
        {
            var parts = new MapParts();
            parts.Root = new GameObject(prefix + "Root");
            parts.Root.tag = TagMapRoot;
            Transform root = parts.Root.transform;

            Transform visual = Child(prefix + "Visual", root, Vector3.zero, Vector3.one);
            float t = 0.5f;
            MakeSprite("BorderTop", visual, S("square"), borderColor, ViewMath.DecorOrder, new Vector3(rect.center.x, rect.yMax + t * 0.5f, 0f), new Vector3(rect.width + t * 2f, t, 1f));
            MakeSprite("BorderBottom", visual, S("square"), borderColor, ViewMath.DecorOrder, new Vector3(rect.center.x, rect.yMin - t * 0.5f, 0f), new Vector3(rect.width + t * 2f, t, 1f));
            MakeSprite("BorderLeft", visual, S("square"), borderColor, ViewMath.DecorOrder, new Vector3(rect.xMin - t * 0.5f, rect.center.y, 0f), new Vector3(t, rect.height, 1f));
            MakeSprite("BorderRight", visual, S("square"), borderColor, ViewMath.DecorOrder, new Vector3(rect.xMax + t * 0.5f, rect.center.y, 0f), new Vector3(t, rect.height, 1f));

            parts.Ground = MakeSprite(prefix + "Ground", root, S("square"), groundColor, ViewMath.GroundOrder,
                new Vector3(rect.center.x, rect.center.y, 0f), new Vector3(rect.width, rect.height, 1f));

            parts.SpawnPoints = Child(prefix + "SpawnPoints", root, Vector3.zero, Vector3.one);
            Vector2[] points =
            {
                new Vector2(rect.xMin + 2f, rect.yMin + 2f), new Vector2(rect.center.x, rect.yMin + 2f), new Vector2(rect.xMax - 2f, rect.yMin + 2f),
                new Vector2(rect.xMax - 2f, rect.center.y), new Vector2(rect.xMax - 2f, rect.yMax - 2f), new Vector2(rect.center.x, rect.yMax - 2f),
                new Vector2(rect.xMin + 2f, rect.yMax - 2f), new Vector2(rect.xMin + 2f, rect.center.y),
            };
            for (int i = 0; i < points.Length; i++) Child("SpawnPoint_" + i, parts.SpawnPoints, (Vector3)points[i], Vector3.one);

            parts.Root.SetActive(false);
            return parts;
        }

        private struct ObstacleSpec
        {
            public string Name;
            public ObstacleKind Kind;
            public ObstacleShape Shape;
            public Vector2 Position;
            public float Radius;
            public Vector2 Size;
            public Color Color;

            public static ObstacleSpec Circle(string name, float x, float y, float radius, Color color)
            {
                return new ObstacleSpec { Name = name, Kind = ObstacleKind.Solid, Shape = ObstacleShape.Circle, Position = new Vector2(x, y), Radius = radius, Size = Vector2.one, Color = color };
            }

            public static ObstacleSpec Box(string name, float x, float y, float w, float h, Color color)
            {
                return new ObstacleSpec { Name = name, Kind = ObstacleKind.Solid, Shape = ObstacleShape.Box, Position = new Vector2(x, y), Radius = 0.5f, Size = new Vector2(w, h), Color = color };
            }

            /// <summary>改成软遮挡（只挡人 / 只挡弹）。</summary>
            public ObstacleSpec As(ObstacleKind kind)
            {
                ObstacleSpec copy = this;
                copy.Kind = kind;
                return copy;
            }
        }

        /// <summary>
        /// 火山 / 海洋的白模遮挡物（策划书 4.1：熔岩巨石、断石柱、岩丘 / 珊瑚礁、沉船残骸、巨贝壳，全是实体）。
        /// 两张图的位置刻意错开：翻滚后人和怪会被新位面的遮挡物推开。中心 4 格内（出生点）和边缘刷怪圈留空。
        /// 坐标按 40 × 24 的默认边界摆放；边界改过的话在 Scene 里手动挪即可。
        /// </summary>
        private static int PlaceWhiteboxObstacles(MapManager map)
        {
            Color boulder = new Color(0.36f, 0.27f, 0.24f, 1f);
            Color pillar = new Color(0.5f, 0.44f, 0.4f, 1f);
            Color mound = new Color(0.28f, 0.2f, 0.17f, 1f);
            ObstacleSpec[] volcano =
            {
                ObstacleSpec.Circle("熔岩巨石_1", -11f, 5f, 1.0f, boulder),
                ObstacleSpec.Circle("熔岩巨石_2", 9f, -6f, 1.0f, boulder),
                ObstacleSpec.Circle("熔岩巨石_3", 14f, 6f, 0.9f, boulder),
                ObstacleSpec.Box("断石柱_1", -6f, -6f, 0.9f, 2.6f, pillar),
                ObstacleSpec.Box("断石柱_2", 6f, 7f, 2.6f, 0.9f, pillar),
                ObstacleSpec.Box("断石柱_3", -15f, -4f, 0.9f, 2.2f, pillar),
                ObstacleSpec.Circle("岩丘_1", 2f, -8f, 1.4f, mound),
                ObstacleSpec.Circle("岩丘_2", -4f, 8f, 1.3f, mound),
                ObstacleSpec.Circle("岩丘_3", 13f, -1f, 1.2f, mound),
            };

            Color coral = new Color(0.95f, 0.45f, 0.55f, 1f);
            Color wreck = new Color(0.45f, 0.35f, 0.25f, 1f);
            Color shell = new Color(0.92f, 0.86f, 0.76f, 1f);
            ObstacleSpec[] ocean =
            {
                ObstacleSpec.Circle("珊瑚礁_1", -12f, -5f, 1.2f, coral),
                ObstacleSpec.Circle("珊瑚礁_2", 7f, 5f, 1.1f, coral),
                ObstacleSpec.Circle("珊瑚礁_3", -5f, -8f, 1.0f, coral),
                ObstacleSpec.Circle("珊瑚礁_4", 15f, -7f, 1.0f, coral),
                ObstacleSpec.Box("沉船残骸_1", -8f, 6f, 3.4f, 1.2f, wreck),
                ObstacleSpec.Box("沉船残骸_2", 10f, -3f, 1.2f, 3.0f, wreck),
                ObstacleSpec.Circle("巨贝壳_1", 3f, 9f, 0.9f, shell),
                ObstacleSpec.Circle("巨贝壳_2", -16f, 4f, 0.9f, shell),
                ObstacleSpec.Circle("巨贝壳_3", 12f, 8f, 0.8f, shell),
            };

            // ---- 工位（策划书 4.2）：办公桌 / 隔断屏风 / 机箱 = 实体，文件堆 = 软·只挡人 ----
            Color desk = new Color(0.62f, 0.6f, 0.56f, 1f);
            Color screen = new Color(0.45f, 0.5f, 0.55f, 1f);
            Color tower = new Color(0.25f, 0.27f, 0.3f, 1f);
            Color paper = new Color(0.95f, 0.95f, 0.9f, 1f);
            ObstacleSpec[] office =
            {
                ObstacleSpec.Box("办公桌_1", -10f, 5f, 3.0f, 1.4f, desk),
                ObstacleSpec.Box("办公桌_2", 9f, -5f, 3.0f, 1.4f, desk),
                ObstacleSpec.Box("办公桌_3", 10f, 6f, 1.4f, 3.0f, desk),
                ObstacleSpec.Box("隔断屏风_1", -5f, -7f, 0.4f, 4.0f, screen),
                ObstacleSpec.Box("隔断屏风_2", 5f, 7.5f, 4.0f, 0.4f, screen),
                ObstacleSpec.Box("机箱_1", -14f, -5f, 0.8f, 1.0f, tower),
                ObstacleSpec.Box("机箱_2", 14f, -1f, 0.8f, 1.0f, tower),
                ObstacleSpec.Circle("文件堆_1", 3f, -9f, 1.0f, paper).As(ObstacleKind.BlocksUnitsOnly),
                ObstacleSpec.Circle("文件堆_2", -13f, 2f, 0.9f, paper).As(ObstacleKind.BlocksUnitsOnly),
            };

            // ---- 梦境（策划书 4.2）：悬浮书本 / 扭曲家具 = 实体，云朵平台 = 软·只挡弹 ----
            Color book = new Color(0.55f, 0.35f, 0.85f, 1f);
            Color furniture = new Color(0.95f, 0.55f, 0.35f, 1f);
            Color cloud = new Color(0.92f, 0.95f, 1f, 1f);
            ObstacleSpec[] dream =
            {
                ObstacleSpec.Box("悬浮书本_1", -9f, -4f, 1.6f, 1.0f, book),
                ObstacleSpec.Box("悬浮书本_2", 12f, 4f, 1.0f, 1.6f, book),
                ObstacleSpec.Circle("扭曲家具_1", 6f, -7f, 1.2f, furniture),
                ObstacleSpec.Circle("扭曲家具_2", -6f, 7f, 1.1f, furniture),
                ObstacleSpec.Box("扭曲家具_3", 15f, -6f, 1.4f, 1.4f, furniture),
                ObstacleSpec.Circle("云朵平台_1", -13f, 6f, 1.6f, cloud).As(ObstacleKind.BlocksProjectilesOnly),
                ObstacleSpec.Circle("云朵平台_2", 8f, 8f, 1.4f, cloud).As(ObstacleKind.BlocksProjectilesOnly),
                ObstacleSpec.Circle("云朵平台_3", -3f, -9f, 1.4f, cloud).As(ObstacleKind.BlocksProjectilesOnly),
            };

            // ---- 原始（策划书 4.3）：巨石 / 古树 / 兽骨堆 = 实体，灌木 = 软（策划书没写挡什么，暂定只挡弹）----
            Color stone = new Color(0.5f, 0.47f, 0.42f, 1f);
            Color tree = new Color(0.35f, 0.25f, 0.15f, 1f);
            Color bush = new Color(0.3f, 0.55f, 0.25f, 1f);
            Color bone = new Color(0.9f, 0.88f, 0.78f, 1f);
            ObstacleSpec[] primal =
            {
                ObstacleSpec.Circle("巨石_1", -10f, -6f, 1.2f, stone),
                ObstacleSpec.Circle("巨石_2", 11f, 5f, 1.1f, stone),
                ObstacleSpec.Circle("古树_1", -6f, 6f, 0.9f, tree),
                ObstacleSpec.Circle("古树_2", 7f, -7f, 0.9f, tree),
                ObstacleSpec.Circle("古树_3", 15f, -3f, 0.8f, tree),
                ObstacleSpec.Circle("兽骨堆_1", 12f, -8f, 0.9f, bone),
                ObstacleSpec.Circle("灌木_1", -14f, 3f, 1.3f, bush).As(ObstacleKind.BlocksProjectilesOnly),
                ObstacleSpec.Circle("灌木_2", 4f, 9f, 1.2f, bush).As(ObstacleKind.BlocksProjectilesOnly),
                ObstacleSpec.Circle("灌木_3", -2f, -8f, 1.2f, bush).As(ObstacleKind.BlocksProjectilesOnly),
            };

            // ---- 未来（策划书 4.3）：集装箱 / 能量立柱 / 报废机甲 = 实体，全息挡板 = 软·只挡弹 ----
            Color container = new Color(0.25f, 0.4f, 0.65f, 1f);
            Color pylon = new Color(0.6f, 0.9f, 1f, 1f);
            Color mech = new Color(0.4f, 0.4f, 0.45f, 1f);
            Color holo = new Color(0.55f, 0.95f, 0.95f, 1f);
            ObstacleSpec[] future =
            {
                ObstacleSpec.Box("集装箱_1", -9f, 5f, 3.4f, 1.4f, container),
                ObstacleSpec.Box("集装箱_2", 10f, -5f, 1.4f, 3.4f, container),
                ObstacleSpec.Circle("能量立柱_1", -5f, -6f, 0.6f, pylon),
                ObstacleSpec.Circle("能量立柱_2", 6f, 6f, 0.6f, pylon),
                ObstacleSpec.Circle("能量立柱_3", 14f, 4f, 0.6f, pylon),
                ObstacleSpec.Box("报废机甲_1", -14f, -4f, 2.0f, 2.0f, mech),
                ObstacleSpec.Box("全息挡板_1", 3f, -8f, 3.0f, 0.3f, holo).As(ObstacleKind.BlocksProjectilesOnly),
                ObstacleSpec.Box("全息挡板_2", -3f, 8f, 3.0f, 0.3f, holo).As(ObstacleKind.BlocksProjectilesOnly),
            };

            return FillObstacles(map.GetRoot(PlaneId.Volcano), "VolcanoObstacles", volcano)
                 + FillObstacles(map.GetRoot(PlaneId.Ocean), "OceanObstacles", ocean)
                 + FillObstacles(map.GetRoot(PlaneId.Office), "OfficeObstacles", office)
                 + FillObstacles(map.GetRoot(PlaneId.Dream), "DreamObstacles", dream)
                 + FillObstacles(map.GetRoot(PlaneId.Primal), "PrimalObstacles", primal)
                 + FillObstacles(map.GetRoot(PlaneId.Future), "FutureObstacles", future);
        }

        /// <summary>在位面 Root 下重建名为 containerName 的遮挡物容器（旧的整个替换掉）。</summary>
        private static int FillObstacles(GameObject root, string containerName, ObstacleSpec[] specs)
        {
            if (root == null) return 0;
            Transform old = root.transform.Find(containerName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            Transform container = Child(containerName, root.transform, Vector3.zero, Vector3.one);
            for (int i = 0; i < specs.Length; i++)
            {
                ObstacleSpec spec = specs[i];
                Transform t = Child(spec.Name, container, new Vector3(spec.Position.x, spec.Position.y, 0f), Vector3.one);
                var obstacle = t.gameObject.AddComponent<Obstacle>();
                obstacle.Configure(spec.Kind, spec.Shape, spec.Radius, spec.Size);

                bool circle = spec.Shape == ObstacleShape.Circle;
                Vector3 scale = circle ? new Vector3(spec.Radius * 2f, spec.Radius * 2f, 1f) : new Vector3(spec.Size.x, spec.Size.y, 1f);
                MakeSprite("Visual", t, S(circle ? "circle" : "square"), spec.Color, ViewMath.ObstacleOrder, Vector3.zero, scale);
            }
            return specs.Length;
        }
    }
}
