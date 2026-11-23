# 音频交接文档 · 版权标注与接入位置

> 版本：2026-11-23 ｜ 素材位置：`D:\BGM` ｜ 代码：`Assets/Turn/Scripts/View/TurnAudio.cs`（程序A 所有）
> 读这份文档的人：接手音效 / 做发布准备的 AI 或程序员。

---

## 0. 一页速览

| 项 | 现状 |
| --- | --- |
| 一次性音效 | **17 个槽位全部接好**（事件驱动，代码零改动即可发声） |
| 背景音乐 | **6 个位面 + 1 首标题 = 7 首已齐**；6 个位面槽 + 交叉淡入已实现 |
| 标题页音乐 | **已实现**：`titleMusic` 槽 + `PlayTitleMusic()` / `PlayPlaneMusic()`，标题→开局自动换曲 |
| 工位困意 | 循环音槽 `drowsyLoop` 已实现，音量自动跟 `Environment.Drowsiness` |
| 版权风险 | Kenney = CC0（免署名）；Incompetech = **CC BY 4.0，必须署名** |

---

## 1. 版权与标注（发布前必须完成）

### 1.1 素材来源与许可

| 来源 | 覆盖范围 | 许可 | 必须署名？ | 可商用 | 备注 |
| --- | --- | --- | --- | --- | --- |
| Kenney（kenney.nl） | `D:\BGM\SFX` 全部 37 个 | CC0 1.0 | ❌ 不需要 | ✅ | 最安全，可改可商用 |
| Incompetech / Kevin MacLeod | `D:\BGM\BGM` 全部 5 首 | CC BY 4.0 | ✅ **必须** | ✅ | 少了署名就是侵权 |

### 1.2 必须原样粘贴的署名文本

放进游戏内 Credits、README、itch.io 页面描述（任一都要，建议全放）：

```
Music by Kevin MacLeod (incompetech.com), licensed under CC BY 4.0.
```

- 中文版可写：`音乐：Kevin MacLeod（incompetech.com），CC BY 4.0 授权`，但**曲名与作者名不要翻译**。
- CC BY 允许改（剪辑、淡入淡出都算），但**必须**保留作者署名 + 许可名称。

### 1.3 游戏内标注放哪

| 位置 | 做法 |
| --- | --- |
| 标题页 | `FlowUI` 的标题界面底部加一行小字（Credits 按钮或直接文本） |
| 结算页 | `FlowUI` 胜利结算里加「Credits」段落 |
| 仓库 | 新建 `THIRD_PARTY_AUDIO.md`，逐条列出：曲名 / 作者 / 来源 URL / 许可 / 是否修改 |

### 1.4 禁止事项

- ❌ 不能把 Kenney / Incompetech 的**原始素材文件单独再分发**（放进游戏里没问题）。
- ❌ 不能声称素材是自己做的，也不能暗示 Kenney/MacLeod 给你背书。
- ❌ 不要把 CC BY 的曲目改成 CC0 或"免费商用无署名"再传播。
- ✅ 允许改名（建议**不要**：`ambient_dream__Ossuary 5 - Rest.mp3` 这种带曲名的命名就是为了追溯）。

### 1.5 追溯规则（给 AI 的硬约束）

改动音频素材时：

1. 保留 `D:\BGM\README.md` 里的对照表，新增素材必须在表里补一行。
2. 新下载任何音效前先确认许可，**CC BY-NC 一律不能用**（Freesound 上很多）。
3. 若替换了 Incompetech 的曲目，同步更新署名文本里的曲名列表（如要求）。

---

## 2. 文件应该放哪

Unity 内目标路径（都在 `Assets/Turn/` 下，符合分工归属）：

```
Assets/Turn/Audio/
  SFX/01-roll/forceField_000.ogg
  SFX/02-rollRejected/error_003.ogg
  ...（18 个文件夹，编号 = 槽位顺序）
  BGM/battle__Volatile Reaction.mp3
  BGM/ambient_office__Meditation Impromptu 01.mp3
```

导入参数：

| 类型 | Load Type | 其他 |
| --- | --- | --- |
| SFX（全部 .ogg） | Decompress On Load | Force To Mono ✅、Vorbis 70 |
| BGM（.mp3/.ogg） | Streaming | Loop ✅、Vorbis 80、Preload Audio Data 关闭 |

---

## 3. 一次性音效：槽位 → 触发事件 → 代码位置

全部在 `Assets/Turn/Scripts/View/TurnAudio.cs`。`Bind()`（第 94–118 行）订阅事件，各 `OnXxx`（第 158–175 行）只做一件事：`Play(槽位)`。

| 槽位字段 | 触发事件 | 代码行 | 素材文件夹 |
| --- | --- | --- | --- |
| `roll` | `RollPerformedEvent` | L160 | `01-roll` |
| `rollRejected` | `RollRejectedEvent` | L161 | `02-rollRejected` |
| `playerHurt` | `PlayerDamagedEvent` | L162 | `03-playerHurt` |
| `damageBlocked` | `DamageBlockedEvent` | L163 | `04-damageBlocked` |
| `playerDeath` | `PlayerDiedEvent` | L164 | `05-playerDeath` |
| `shot` | `ShotFiredEvent`（B 侧事件） | L165 | `06-shot` |
| `ammoEmpty` | `AmmoEmptiedEvent`（B 侧事件） | L166 | `07-ammoEmpty` |
| `monsterDeath` | `MonsterDiedEvent`（B 侧事件） | L167 | `08-monsterDeath` |
| `orbPickup` | `OrbCollectedEvent`（B 侧事件） | L168 | `09-orbPickup` |
| `levelUp` | `LeveledUpEvent`（B 侧事件） | L169 | `10-levelUp` |
| `lavaEruption` | `LavaEruptedEvent` | L170 | `11-lavaEruption` |
| `sharkAppear` | `SharksSpawnedEvent` | L171 | `12-sharkAppear` |
| `shockwave` | `ShockwaveTriggeredEvent` | L172 | `13-shockwave` |
| `phoneWarning` | `HazardWarnedEvent` | L173 | `14-phoneWarning`（占位，见 §8） |
| `phoneLand` | `HazardLandedEvent` | L174 | `15-phoneLand` |
| `pageTurn` | 由 `GameManager` 直接调 `PlayPageTurn()`（L151） | L151 | `16-pageTurn` |
| `victory` | `LevelClearedEvent`（仅 `Victory == true`） | L175 | `17-victory` |
| `99-uiClick` | **无事件**，留给 B 的升级卡 UI 手调 | — | `99-uiClick` |

**改事件名要同步**：`ShotFiredEvent` / `AmmoEmptiedEvent` / `MonsterDiedEvent` / `OrbCollectedEvent` / `LeveledUpEvent` 定义在 `Core/Events.Combat.cs`，归程序B。B 改签名必须同时改 `TurnAudio.Bind()`，否则静音（不会报错）。

播放细节（`Play()`，L179–190）：10 路 voice 轮转（`VoiceCount` L39）、`minInterval` 限频、`pitchJitter` 随机音高、音量 = `cue.volume × masterVolume`。

---

## 4. 背景音乐：6 个位面槽 + 交叉淡入

字段：`volcanoMusic / oceanMusic / officeMusic / dreamMusic / primalMusic / futureMusic`（L48–53），`MusicFor()` L192 按 `PlaneId` 取。

触发点只有两处：

- `OnRunStarted` → `PlayMusicFor(evt.Plane)`（L158）：开局起第一首
- `OnPlaneSwitched` → `PlayMusicFor(evt.To)`（L159）：每次翻滚切位面换曲

淡入淡出在 `LateUpdate()`（L221–235）：A/B 两个 `AudioSource` 交叉过渡，时长 = `musicCrossfade`（L45，默认 0.6s），音量 = `musicVolume × masterVolume`。

### 曲目 → 槽位（已补全：6 个位面 + 1 首标题，共 7 首）

| 槽位 | 文件 | 理由 |
| --- | --- | --- |
| `titleMusic` | `title__Funkorama.mp3` | 轻快、绘本开场 |
| `volcanoMusic` | `volcano__Volatile Reaction.mp3` | 有律动，配岩浆推进 |
| `oceanMusic` | `ocean__Ossuary 5 - Rest.mp3` | 空旷、水下感 |
| `officeMusic` | `office__Meditation Impromptu 01.mp3` | 钢琴、压抑的办公室 |
| `dreamMusic` | `dream__Ossuary 6 - Air.mp3` | 飘忽失真 |
| `primalMusic` | `primal__Killers.mp3` | 原始位面要凶 |
| `futureMusic` | `future__Digital Lemonade.mp3` | 电子轻快，贴"未来" |

### 标题页音乐（已实现）

- `TurnAudio`：`titleMusic` 字段（L55）+ `PlayTitleMusic()`（L214）+ `PlayPlaneMusic()`（L220），内部共用 `PlayMusic(clip)`（L225）走同一套 A/B 交叉淡入。
- `GameManager`：`Start()` 显示标题后调 `PlayTitleMusic()`（L130）；封面翻开时 `OnTitleOpened()` 调 `PlayPlaneMusic()`（L140）把标题曲换回关卡曲；`ReturnToTitle()`（L167）回标题时重新起标题曲。
- 未填 `titleMusic` 时行为与之前完全一致（标题静音），不会报错。

---

## 5. 工位困意循环

- 槽位：`drowsyLoop`（L76）+ `drowsyVolume`（L77）
- 逻辑：`LateUpdate()` L237–248，只在 `Plane.Active == Office` 时读 `_ctx.Environment.Drowsiness`，音量 = `drowsyVolume × masterVolume × Drowsiness`；进度为 0 自动停。
- 素材：目前**没配**，建议补一条 10s 心跳/呼吸循环（可 Loop，Vorbis 70）。

---

## 6. 混音参数速查

| 参数 | 位置 | 默认 |
| --- | --- | --- |
| `masterVolume` | L42 | 1.0 |
| `musicVolume` | L43 | 0.6 |
| `musicCrossfade` | L45 | 0.6s |
| 每个 `SoundCue.volume` / `pitchJitter` / `minInterval` | L9–26 | 1.0 / 0.05 / 视槽位 |

调平衡时先动 `SoundCue.volume`，再动 `musicVolume`；`masterVolume` 留给设置菜单。

---

## 7. 接入步骤（10 分钟）

1. 把 `D:\BGM\SFX\*` 拷到 `Assets/Turn/Audio/SFX/`，`D:\BGM\BGM\*` 拷到 `Assets/Turn/Audio/BGM/`。
2. 场景里选中挂 `GameManager` 的物体 → **Add Component → Turn Audio**（若已有则跳过）。
3. 按第 3 节表格把文件拖进对应槽位（一个槽可放多个变体做随机，但 `SoundCue` 目前只支持单 clip；多变体要改代码）。
4. 第 4 节把 6 首 BGM 挂进 `volcanoMusic`…`futureMusic`。
5. Play：翻滚、开枪、切位面听一遍；切位面应听到 0.6s 交叉淡入。

---

## 8. 已知缺口 / 风险

| 项 | 状态 | 建议 |
| --- | --- | --- |
| `phoneWarning` | 电子提示音**占位**，不是电话铃 | 去 Freesound 找 CC0 `telephone ring` 替换 |
| BGM 数量 | ✅ 已补齐 7 首（6 位面 + 标题） | — |
| 环境循环 | 除困意外全无（岩浆咕嘟、海浪、空调） | Sonniss GDC 包最全，但整包数 GB，按需取 |
| `SoundCue` 多变体 | 每个槽只支持单 clip | 需要随机变体时改成 `AudioClip[]`（A 的文件） |
| B 侧事件改名 | 会静默失效 | 改 `Events.Combat.cs` 必须同步 `TurnAudio.Bind()` |
| 音乐版权 | 5 首全是 CC BY | 上线前务必完成 §1.2 署名 |

---

## 9. 变更记录

- 2026-11-23：建立本文件；素材落地 `D:\BGM`（Kenney 37 SFX + Incompetech 5 BGM），并核对 `TurnAudio.cs` 的 17 槽 / 6 位面音乐 / 困意循环。
