using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Survivor.Content;
using Survivor.Core;
using Survivor.Data;
using Survivor.Systems;
using Survivor.View;

namespace Survivor.Bootstrap
{
    /// <summary>
    /// 启动入口 + 一局的编排：读内容 → 组装系统 → 绑定表现 → 固定步长推进模拟 → 结算 → 重开。
    /// 只在名为 “Survivor” 的场景里自动创建（菜单 Survivor → 打开白模场景），不需要在场景里摆任何物体。
    /// 输入在这里读取并转成系统接口调用；规则全在系统里。
    /// </summary>
    public sealed class SurvivorRoot : MonoBehaviour
    {
        public const string SceneName = "Survivor";
        /// <summary>模拟固定 60Hz；帧率低时一帧最多补 MaxStepsPerFrame 步。</summary>
        public const float SimStep = 1f / 60f;
        public const int MaxStepsPerFrame = 5;

        private WorldView _world;
        private HudView _hud;
        private EntityLayer _entities;
        private ISurvivorContext _ctx;
        private float _accumulator;
        private bool _paused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (SceneManager.GetActiveScene().name != SceneName) return;
            var go = new GameObject("[Survivor] Root");
            go.AddComponent<SurvivorRoot>();
        }

        private void Start()
        {
            Camera cam = EnsureCamera();
            EnsureEventSystem();

            _world = WorldView.Create(cam);
            SurvivorPostFx.Ensure(cam);
            _hud = HudView.Create();

            StartNewRun();
        }

        // ---------------- 一局 ----------------

        public void StartNewRun()
        {
            SurvivorContentBundle content = SurvivorContentLoader.Load();
            if (content == null) return; // 资产缺失，加载器已输出报错

            if (_entities != null)
            {
                _entities.Dispose();
                _entities = null;
            }

            StageData stage = SurvivorContent.CreateStage();
            _ctx = SurvivorFactory.Create(content.Character, content.Weapons, content.Passives, stage, Environment.TickCount);

            _world.ApplyTheme(stage.Theme);
            _entities = EntityLayer.Create(_ctx, _world);
            _hud.Bind(_ctx, OnChoose, StartNewRun);
            _ctx.Session.Start();

            _accumulator = 0f;
            _paused = false;
            _hud.SetPaused(false);
        }

        private void OnChoose(int index)
        {
            if (_ctx != null) _ctx.Session.TryChooseUpgrade(index);
        }

        private void Update()
        {
            if (_ctx == null) return;
            ISessionSystem session = _ctx.Session;

            if (session.IsOver)
            {
                if (SurvivorInput.ConfirmPressed) StartNewRun();
                return;
            }

            if (SurvivorInput.PausePressed && session.Phase == RunPhase.Playing)
            {
                _paused = !_paused;
                _hud.SetPaused(_paused);
            }

            if (session.Phase == RunPhase.LevelUp)
            {
                int choice = SurvivorInput.ChoicePressed;
                if (choice >= 0) OnChoose(choice);
            }

            _ctx.Player.SetMoveInput(ViewMath.ToVec2(SurvivorInput.Move));

            if (_paused || session.Phase != RunPhase.Playing)
            {
                _accumulator = 0f;
                return;
            }

            _accumulator += Mathf.Min(Time.deltaTime, 0.1f);
            int steps = 0;
            while (_accumulator >= SimStep && steps < MaxStepsPerFrame)
            {
                session.Tick(SimStep);
                _accumulator -= SimStep;
                steps++;
                if (session.Phase != RunPhase.Playing)
                {
                    _accumulator = 0f;
                    break;
                }
            }
        }

        // ---------------- 环境准备 ----------------

        private static Camera EnsureCamera()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam;

            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            return cam;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));

#if ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<StandaloneInputModule>();
#else
            // 项目只启用了新 Input System 时，用反射挂它的 UI 输入模块，避免硬依赖该包。
            Type moduleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType != null) go.AddComponent(moduleType);
            else go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
