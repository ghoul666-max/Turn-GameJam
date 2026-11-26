# 第三方音频素材

素材位置：`Assets/Turn/Audio/`（原始包：`D:\BGM`，见其中 README.md）。

## 背景音乐 — Kevin MacLeod（incompetech.com），CC BY 4.0（必须署名）

署名文本（放进游戏内 Credits / itch.io 页面）：

```
Music by Kevin MacLeod (incompetech.com), licensed under CC BY 4.0.
```

| 曲名 | 作者 | 来源 | 许可 | 用途（TurnAudio 槽位） | 是否修改 |
| --- | --- | --- | --- | --- | --- |
| Funkorama | Kevin MacLeod | https://incompetech.com | CC BY 4.0 | titleMusic | 否（仅改文件名） |
| Volatile Reaction | Kevin MacLeod | https://incompetech.com | CC BY 4.0 | volcanoMusic | 否（仅改文件名） |
| Ossuary 5 - Rest | Kevin MacLeod | https://incompetech.com | CC BY 4.0 | oceanMusic | 否（仅改文件名） |
| Meditation Impromptu 01 | Kevin MacLeod | https://incompetech.com | CC BY 4.0 | officeMusic | 否（仅改文件名） |
| Ossuary 6 - Air | Kevin MacLeod | https://incompetech.com | CC BY 4.0 | dreamMusic | 否（仅改文件名） |
| Killers | Kevin MacLeod | https://incompetech.com | CC BY 4.0 | primalMusic | 否（仅改文件名） |
| Digital Lemonade | Kevin MacLeod | https://incompetech.com | CC BY 4.0 | futureMusic | 否（仅改文件名） |

## 音效 — Kenney（kenney.nl），CC0 1.0（无需署名）

`Assets/Turn/Audio/SFX/` 下全部 .ogg。

（注：亚空间版加入的 `.wav` 不是 Kenney 的，见下面効果音ラボ一节；`06-shot/fireball_shot.ogg` 是程序生成的原创音效。）

## 背景音乐 — 魔王魂（maou.audio），站点自有条款（2026-11-26 加入）

免费商用、可以修改；署名可选（写的话：「音楽：魔王魂」）。禁止：声称是自己创作的 / 把曲子单独再分发或出售 / 用 AI 编曲或训练。

| 曲名 | 文件 | 用途（TurnAudio 槽位） | 是否修改 |
| --- | --- | --- | --- |
| ファンタジー15「駆け抜ける戦場の嵐」 | BGM/battle__maou_bgm_fantasy15.ogg | battleMusic | 否（仅改文件名） |
| ファンタジー12「戦術的構想」 | BGM/battle_alt__maou_bgm_fantasy12.ogg | 备选（未接入） | 否（仅改文件名） |
| ボス08 | BGM/boss__maou_game_boss08.ogg | bossMusic | 否（仅改文件名） |

## 音效 — 効果音ラボ（soundeffect-lab.info），站点自有条款（2026-11-26 加入）

免费商用、可以修改；署名不需要（可选）。禁止：把音效单独再分发 / 出售（做进游戏不算）、直链、AI 训练、做成「音效介绍」类视频。

`Assets/Turn/Audio/SFX/20-subspace` ~ `27-ui` 下全部 `.wav`，原始文件名保留在 `__` 后面（例如 `blade__iainuki1.wav` = 効果音ラボ的 iainuki1.mp3）。
修改：去掉首尾静音、峰值统一到 −1 dB、转单声道 44.1 kHz；`subspaceEnter__text-impact2` 截到 1.4 s、`subspaceEnter_alt__buun1` 截到 1.0 s、`sniperWarn__beam-gun-charge1` 截到 0.8 s、`spadeBomb__bomb2` 截到 1.6 s，并加了淡出。
原始下载与挑选说明：`D:\desk\BGM\README_Turn音效挑选.md`。

**注意**：标题曲 titleMusic 仍然是 Kevin MacLeod 的 Funkorama（CC BY 4.0），只要还在用，就必须保留上面的署名。
