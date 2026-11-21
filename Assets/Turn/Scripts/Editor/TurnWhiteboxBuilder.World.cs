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

            mapManager.AlignRoots();
            mapManager.Apply(PlaneId.Volcano); // 场景里同样只有一张图是激活的
            ApplyMaterialInScene(scene);

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
    }
}
