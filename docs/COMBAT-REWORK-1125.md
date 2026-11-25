# 2026-11-25 战斗调整对接与验证

## 已实现

| 项目 | 本轮规则与入口 |
|---|---|
| 剑 | SwordWeapon.NeedsTarget=false，沿最近移动朝向周期突进；无怪也正常耗弹；距离4.5、时长不变；冲刺中不重复出手 |
| 掌 | 360°圆形伤害、取消最近一圈限制；保留障碍视线；PalmAngle/PalmPierce不再进入可抽池 |
| 陌刀 | 出手快照目标实时位置，伤害即时；BladeWaveFiredEvent新增TargetPosition，刀光锚定此点而不跟踪 |
| 扑克牌 | 一张手牌按原DrawInterval不断替换；空格出当前显示牌；大王原有连抽连打特性保留；爆发后短暂空手，下一常态抽牌周期补牌 |
| 投矛 | 击杀停止穿透，当前携带矛和旧印记矛落地，爆发统一回收地面和活怪矛并结算路径伤害；退出亚空间清残留 |
| 沙漏 | 默认5秒从100%线性降到50%，维持下限，翻滚重置；衰减加速词条仍缩短达到下限所需时间 |
| 光标 | 固定首翻滚锚点、当前位置作为对角，轴对齐矩形；MaxLength限制对角线，面积词条增加上限；保留边界厚度 |
| 武器增伤 | Arsenal.FixedDamageMultiplier=1.25，仅WeaponKit固定伤害乘算；最大生命百分比伤害入口显式排除 |
| 普通怪速度 | Horde.NormalChaseSpeedMultiplier=.85，仅Rank=0普通追击；冲锋独立速度、弹速、精英/Boss不改 |
| Boss血量 | Horde.BossHpMultiplier=2，独立乘在原BossHp上；伤害/体型/经验/刷新不变 |
| 受伤保护 | 先复现20次同帧怪物伤害，真实系统仅扣一次；原保护逻辑有效，保护默认与存档0.3调整0.5秒 |
| 黑洞保护 | GravityDisplacedEvent只在怪物实际位移>.01时发布；PlayerSystem记录，退出亚空间后给予0.5秒保护；没有实际拉动不触发 |
| Boss宝箱 | ChestOpenedEvent新增Rank，Progression记录队列；Boss宝箱抽携带装备三项不重复未满词条，不放代价选项；候选不足显示实际数量 |

## 明确未完成/待用户或A

- 用户同意本轮先做Boss宝箱装备定向，品质表待接入。BuildDef目前无稀有度字段，**未实现“至少稀有”筛选**，不能标为完成。
- 全词条满级的补偿类型/数值尚未确认，仍沿已有无候选流程处理，**未新增回血补偿**。
- Unity Play视觉验收尚未进行。尤其陌刀新锚点和落地矛线段需在Game视图确认手感。
- 本轮尝试Unity批处理编译失败在许可证阶段：`Temp/combat-rework-1125.log`出现headless entitlement不可用/return code 198，尚未进入脚本编译。命令包装报告exit0不能当作编译通过。

## A归属文件（用户明确授权本轮保护逻辑最小修改）

- Models.World.cs：PlayerConfig.HurtInvincibleTime=.5、GravityExitProtectionTime=.5。
- PlayerSystem.cs：订阅GravityDisplacedEvent，退出亚空间给予保护，不改变翻滚原子时序。
- PlayerController.cs：Inspector默认与ToConfig透传。
- TurnTuningPanel/TurnTuning.json共享：仅本轮玩家保护及战斗分组；剑距离、单牌上限、线性沙漏、倍率同步存档，保留其他调参。

## 回归测试

- Tests/Combat：51断言（同帧伤害保护、固定/百分比倍率、Boss属性、定向宝箱不足候选/混合队列、黑洞空吸/零位移/Setup重置与实拉退出保护等）。
- Tests/Builds：791断言（已将旧矛穿透/掌扇形/多手牌/指数沙漏/轨迹光标用例更新为新规则）。
- Tests/Monsters：769断言。
- Tests/Roll：252断言（受伤保护更新为.5秒）。
- Tests/MeleeChanges：17断言。
- Tests/UtilityChanges：28断言。

以上六套均通过；不等同于Unity编译与Play验收。本文件随本轮战斗调整提交，Unity验证和奖励规则缺口仍以上文为准。
