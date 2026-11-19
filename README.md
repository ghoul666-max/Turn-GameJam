# Turn GameJam

Game Jam 回合制 2D 项目（Unity 6 / URP 2D）。

## 环境

- Unity 版本：**6000.6.4f1**（Unity 6.6）
- 渲染管线：Universal Render Pipeline (2D)
- 主要包：2D Animation、2D PSD Importer、Aseprite Importer、Input System、Visual Scripting

## 打开方式

1. Unity Hub → **Add project from disk** → 选择本仓库根目录（Unity 会自动按 `Packages/manifest.json` 还原依赖）。
2. 首次导入会重新生成 `Library/`（已通过 `.gitignore` 排除，不进入版本库）。
3. 打开主场景 `Assets/Turn/Scenes/TurnGame.unity` 后直接 Play。

## 目录结构

```
Assets/
  Turn/            回合制玩法：Scripts、Prefabs、Art、Animations、Resources、Scenes
    Scenes/TurnGame.unity      主玩法场景
    Scenes/TurnTuning.unity    数值调试场景
  Survivor/        生存模式原型（Survivor.unity）
  Scenes/          模板示例场景（SampleScene.unity）
  Settings/        URP / 输入 / 渲染等工程设置
  Welcome/         2D 模板欢迎页资源
  _backup_*/       手工备份的 Prefab 快照（不参与构建）
Packages/          Package 清单与锁文件
ProjectSettings/   Unity 工程配置（版本、输入、物理、质量等）
```

## 构建场景顺序

`SampleScene` → `Survivor` → `TurnGame`（见 `ProjectSettings/EditorBuildSettings.asset`）

## 说明

- 仓库只包含源码与资源（`Assets`、`Packages`、`ProjectSettings`），`Library`、`Logs`、`UserSettings`、`obj`、`*.csproj`、`*.sln` 等生成物均已忽略。
- 若在其他机器上拉取后打开，请使用相同或更高版本的 Unity 6 以免资源序列化不兼容。
