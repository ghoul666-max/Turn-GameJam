# Turn 手机适配与构建验收

## 范围与状态

目标：Android / iOS 横屏，支持 Landscape Left 与 Landscape Right，禁止竖屏自动旋转；左侧虚拟摇杆移动，右侧翻滚按钮，保留自动攻击与自动索敌。

本说明列出适配验收标准，不代表已经通过真机测试。构建、签名、商店提交和设备安装均不自动执行。桌面键鼠路径应继续保留。当前工作区原有的 ProjectSettings / Packages 本地修改不属于本工具的自动迁移范围。

## 操作与 UI 验收约定

- 左摇杆：单指拖动控制移动；抬手、取消触摸、切换 UI 或失焦后停止，不应出现残留移动。
- 右翻滚：独立触点触发，允许摇杆与翻滚双指同时操作；一次按下最多触发一次，不因按住重复翻滚。冷却反馈应可见。
- 攻击保持自动，不新增攻击按钮；翻滚伤害、装填及武器切换继续走现有模拟层入口，不从触屏表现层直接改战斗状态。
- 选武器、升级卡片、确认、返回及暂停按钮可触屏点击，不依赖数字键、空格或 Esc。暂停 / 升级 / 选择页面不得让触摸穿透成移动或翻滚。
- HUD 与触屏按钮按 `Screen.safeArea` 布局，横屏左右翻转后重新计算。刘海、挖孔、圆角、系统手势区不能遮挡关键按钮；不同长宽比下左右控制区不互相重叠。
- 切后台、锁屏、焦点丢失时清空输入，避免恢复后自动移动 / 自动翻滚；恢复流程应显示清晰暂停状态，不自动恢复危险战斗。系统中断与玩家手动暂停需要分别验收。
- 运行时横屏限制与目标帧率由移动端运行时适配层负责；不要在构建脚本中加入运行时游戏逻辑。60 FPS 是验收目标之一，不是已测结论。

## 编辑器预览与可调参数

- 手机运行时由 GameManager 自动创建 MobileControls 与 MobileRuntimeSettings，无需手写场景或 Prefab。
- 在编辑器预览触屏：退出 Play，给 GameManager 挂 MobileControls，勾选 `Simulate In Editor`，再进入 Play。必须在开始前勾选，运行中切换不会重建菜单；鼠标可检查单点布局，多点触控仍需真机。
- MobileControls Inspector 可调摇杆大小、翻滚按钮大小、边缘间距、死区和透明度。MobileRuntimeSettings Inspector 可调目标帧率（默认60）、左右横屏旋转和游玩防熄屏；只在手机实际应用，不改变桌面帧率或玩法数值。
- 非战斗页面自动隐藏触控；升级、暂停、切后台、失焦或屏幕安全区改变会释放输入，恢复后需重新按下摇杆。
- 桌面保留原有 WASD/方向键、空格与菜单快捷键；手机不应用 PC 窗口模式存档。

## 编辑器构建工具

入口脚本：[TurnMobileBuild.cs](<../Assets/Turn/Scripts/Editor/TurnMobileBuild.cs>)，命名空间 `Turn.EditorTools`，菜单 `Turn > Mobile`。

**导入脚本本身没有副作用。** 不自动切平台、不自动改场景列表、不自动构建、不配置或猜测包名、账号、证书、签名密钥。Configure 菜单需明确确认才修改 Player Settings；方向设置是 Unity 共享设置，若恢复其他目标的方向策略，应由开发者主动改回。此工具不改桌面输入、桌面构建目标或桌面画质设置。

### 1. 安装构建模块

在 Unity Hub 为项目实际使用的同一 Editor 版本安装：

- Android：Android Build Support、Android SDK & NDK Tools、OpenJDK。
- iOS：iOS Build Support；最终 Xcode 导出、编译签名、安装与设备调试请使用 macOS。

菜单使用 `BuildPipeline.IsBuildTargetSupported` 检查模块是否可用；此检查不能代替 SDK 版本、许可证和签名检查。缺模块时查看 Console 的安装提示，不要通过修改 Packages 或随意更换 Editor 版本绕过。

### 2. 显式选择平台与应用标识

1. 保存场景，退出 Play Mode，等待脚本编译结束。
2. `File > Build Profiles`（部分 Editor 为 Build Settings）中明确选择 Android 或 iOS 并 Switch Platform；工具不会替你切换。
3. `Player Settings > Identification` 设置自己拥有且唯一的 Application Identifier / Bundle Identifier。格式示意为 `com.yourstudio.turn`，必须替换示意组织名；默认 `DefaultCompany`、`example`、`placeholder` 与 `yourstudio` 包名会被工具拒绝。
4. 检查版本号、Android version code / iOS build number、最低系统版本和目标 API 是否符合当前设备与分发渠道要求。本工具不猜测这些发布信息。
5. 在 Build Settings / 当前 Profile 的场景列表启用有效场景，检查启动顺序，通常以 TurnGame 作为游戏入口。工具只读取 `EditorBuildSettings.scenes` 中已经启用的场景，不会增加、覆盖或排序场景。若当前 Build Profile 使用覆盖场景列表，请先核对其与工具读取列表一致再构建。

### 3. Android 配置与打包

- 点击 `Turn > Mobile > Configure Android (landscape, ARM64 IL2CPP)` 并确认：仅配置横屏左右自动旋转、禁用竖屏、IL2CPP、ARM64；签名设置保持原样。
- 点击 `Preflight Android`。要求模块可用、主动切到 Android、有效已启用场景、非占位包名、正确方向及 IL2CPP / ARM64。
- 真机本地安装通常选 `Build Android APK...`；商店交付通常选 `Build Android App Bundle...`，按实际渠道要求决定。
- 选择输出文件并再次确认后才执行构建。AAB/APK 开关在构建后恢复为原值；工具不执行 Build And Run，不自动安装。
- 本地测试和正式分发的签名由开发者自行配置。不要把 keystore、密码或证书提交仓库；正式 AAB 需要符合渠道要求的签名。预检通过不代表正式签名或商店审核通过。

### 4. iOS 导出与签名

- macOS Unity Editor 中显式切到 iOS，然后点击 `Configure iOS (landscape)` 并确认，设置横屏和 IL2CPP。
- 设置唯一 Bundle Identifier，点击 `Preflight iOS`。
- 点击 `Export iOS Xcode project... (macOS)`，选择空输出目录，确认后导出。非空目录会拒绝，防止覆盖已有 Xcode 工程。
- 在 Xcode 中由拥有权限的开发者选择 Team、Signing & Capabilities、Provisioning，连接设备编译运行；发布时再做 Archive / 分发。
- **Windows 不能完成 Xcode 签名、安装及 iOS 真机测试。** 此菜单在非 macOS 下拒绝导出并给出提示；可在 Windows 开发共享代码，但本工具不承诺 Windows 生成可安装 IPA。
- 工具不写入 Apple Team、证书、Provisioning Profile，也不改自动签名选项。

## 最小真机测试矩阵

| 维度 | 必测项目 | 记录内容 |
|---|---|---|
| 平台 | Android ARM64；iOS 实机（macOS/Xcode） | 型号、系统、Unity 版本、包版本与提交 |
| 屏幕 | 16:9、长屏约 20:9；刘海 / 挖孔；平板 | HUD、卡片、安全区、按钮重叠与文字可读性 |
| 方向 | 左横屏、右横屏、游戏中翻转、尝试竖持 | 只保持横屏，安全区重新布局，触点无残留 |
| 多点输入 | 摇杆拖动时按翻滚，交换触点、快速连点、手指滑出控件 | 不抢指、不多次触发，不需要先停移动 |
| UI | 主菜单、武器选择、升级三选一、暂停、结算、返回 | 全部可点，战斗输入不穿透，手指释放及时 |
| 生命周期 | 来电 / 通知、切后台、锁屏、恢复、手动暂停后中断 | 无残留输入，暂停状态正确，不重复结算 |
| 战斗 | 一局开局到结算，所有主武器、双武器、黑洞 / 大量爱心弹 | 翻滚、自动攻击、成长与装填规则无回归 |
| 性能 | 低中档 Android 与目标 iPhone，连续战斗至少 15 分钟 | FPS、帧时间、内存、温度、耗电、卡顿与 GC |
| 构建 | 未装模块、错平台、空场景、无效包名、取消输出 | 有可操作错误，无意外切平台、改签名或出包 |

通过 Editor 编译或模拟触屏不等于以上真机矩阵通过。应保存测试结果和复现步骤；没有设备数据前，不宣称“已完成手机性能适配”或“iOS 真机验证通过”。
