# EZ-SR-TL 注册表（Session / Timeline / Race）

Score Race Timeline 架构的**唯一权威文档**。代码中 `TODO(EZ-SR-TL-*)` 须与本表同步维护。

相关深文档：

- Mania Session 黄金标准：[REPLAY_JUDGE_MERGE.md](../../../osu.Game.Rulesets.Mania/EzMania/ReplayJudge/REPLAY_JUDGE_MERGE.md)
- Wiki：[时间线服务](https://github.com/SK-la/Ez2Lazer/wiki/时间线服务-中文) · [角逐服务](https://github.com/SK-la/Ez2Lazer/wiki/角逐服务-中文)

---

## §1 原则

1. **Statistics 不可被 Session 覆盖** — Realm 的 Statistics / Acc / TotalScore 为权威；Session 只 patch 内存 HitEvents 或供 Graph Now 读取，禁止写回 Statistics。
2. **同 score+env 只仿真一遍** — Score / Timeline / HitEvents 是同次 replay 仿真的多出口；消除 Service 层双倍 `Run`+`RunTimeline`（TL-026）。Mania **禁止 F 类**（非 replay 产出的 HitEvents 喂 SP 建 Timeline）。
3. **StatisticsPanel 与 Graph Now 基线共享 Session cache** — 同一 `score + env` 一次 `Run`；Graph offset 的 C/D 分层不污染 base cache。
4. **HitEvents 不持久化** — 补 HitEvents = 读同一 Run 的 HitEvents 子集。

---

## §1.5 执行模型（一遍 SP · 多出口）

### 规则集层（Mania 已实现）

`ManiaReplaySession.run(..., recordTimeline)` — replay → ApplyResult →（可选）Timeline 快照 → PopulateScore。

| API | 出口 | SP 仿真遍数 |
|-----|------|------------|
| `Run` | 完整 Score | 1 |
| `RunTimeline` | EzScoreTimeline | 1 |
| `RunHitEvents` | HitEvents（= Run） | 1 |

### Service 层缺口（TL-026）

| 调用 | 现状仿真遍数 | 目标 |
|------|-------------|------|
| `RunCombinedAsyncFunc` | 2（Run + RunTimeline） | 1（`recordTimeline:true` 多出口） |
| 同 score+env 分别 `RunAsync` + `RunTimelineAsync` | 最多 2（不同 cache key） | 共享底层结果 |

角逐 Builder 使用 `IEzReplaySession.RunTimelineDirectAsync`（不经 TimelineCache）。

### 共享 cache 与 Graph offset（§1.6a）

| 消费方 | 读法 | Cache |
|--------|------|-------|
| StatisticsPanel 补 HitEvents | HitEvents 子集 | score+env base |
| Graph Now 基线 | 完整 Score | 同上 |
| Graph offset 拖动（C） | RefreshDisplayOnly | 无 Session |
| Graph offset 落定（D） | RefreshFromService | 新 env → 新 key |

### 「第二遍」场景对照（§1.6b）

| 代号 | 场景 | Plan 态度 |
|------|------|-----------|
| **C** | Graph 拖 offset，UI 预览 | ✓ 合法 |
| **D** | offset debounce 后精确 Session | ✓ 合法 |
| **E** | Mania 用 Session HitEvents 再 `buildFromHitEvents` | ✗ 文档禁令（无代码路径） |
| **F** | Osu generator → `buildFromHitEvents` | OSU-TRANSITIONAL |

---

## §2 消费场景矩阵

| 消费场景 | 所需出口 | Mania | Osu | 原则 |
|----------|----------|-------|-----|------|
| Realm 持久化 | Statistics / Acc / TotalScore | ✓ | ✓ | HitEvents `[Ignored]` |
| StatisticsPanel 补 HitEvents | HitEvents | `ReplaySession.RunHitEventsAsync` ✓ | **路由缺口 TL-023**（Osu 误走 Mania 单例） | 调用方不增 API；按 ruleset 解析 Session |
| Graph Original | Realm 静态 | ✓ | — | 不跑 Session |
| Graph Now 基线 | 完整 Score | RunAsync(ForLive) | — | 共享 base cache |
| Graph offset | C / D | ✓ | — | 不污染 base |
| 角逐 Timeline | EzScoreTimeline | RunTimelineDirect | F 类 legacy | Mania 一遍 SP |
| 角逐 HUD 实时分 | Timeline 快照 | ✓ | ✓ | 不用终局 TotalScore |
| Parity | Score + HitEvents 字段级 | Drawable ≡ Session | 未建立 | REPLAY_JUDGE_MERGE |

---

## §3 框架 vs 规则集边界

| 层级 | 负责 | 不负责 |
|------|------|--------|
| `osu.Game/EzOsuGame/Scoring/*` | IEzReplaySession、Timeline/Race 编排、cache 接口、ReplayRunPurpose | 判定、press 匹配、HitMode Mapping |
| `Rulesets.Mania/.../ReplayJudge/*` | ManiaReplaySession、RunTimeline、CreateEzReplaySession | Race HUD |
| `Rulesets.Osu/.../OsuScoreHitEventGenerator` | Osu HitEvents 生成 + fallback 注册 | 长期框架逻辑 |
| ~~EzScoreTimelineBridge~~ | **已删除（TL-005）** | 静态注册反模式 |

目标：`ruleset.CreateEzReplaySession()` → `RunTimelineDirectAsync` / `RunAsync`。

---

## §4 Phase 路线图

| Phase | 范围 | 状态 |
|-------|------|------|
| **1** | Mania Session + Timeline + Race | 基本完成 |
| **1.5** | Graph Now；P3-Rest → RunRequestAsync（TL-024/025） | 进行中 |
| **1.5b** | TL-026 单次 run 多出口 | blocked |
| **2** | Mania 环境分层；Session 零 GlobalConfig | 文档预览 |
| **3** | Osu/Taiko/Catch Session | Osu **blocked** |
| **Osu 过渡** | `EzScoreTimelineHitEventsLegacy` + 角逐 ghost | 当前 |

---

## §5 TODO 注册表

| ID | 状态 | 范围 | 文件 / 说明 |
|----|------|------|-------------|
| TL-001 | blocked | Osu | OsuReplaySession — 待 Phase 3 |
| TL-002 | reserved | — | （未使用） |
| TL-003 | blocked | Osu | Generate → OsuReplaySession.Run |
| TL-004 | reserved | — | （未使用） |
| TL-005 | **done** | Mania | 删 Bridge；Builder → CreateEzReplaySession |
| TL-006 | blocked | Osu | OsuSession 枚举 |
| TL-007 | blocked | Osu | Osu Session 后再删 HitEvents 路径 |
| TL-008 | blocked | Osu | Osu 缓存键策略 |
| TL-009 | reserved | — | （未使用） |
| TL-010~015 | active→legacy | Osu | HitEvents 重放 → `EzScoreTimelineHitEventsLegacy` |
| TL-016 | reserved | — | （未使用） |
| TL-017 | active→legacy | Osu | `EzScoreTimelineJudgementTime` 迁入 legacy |
| TL-018 | blocked | Osu | 删 press 匹配 |
| TL-019 | blocked | Osu | OsuSession 枚举名 |
| TL-020 | **done** | All | Builder 类注释 → 指向本 REGISTRY |
| TL-021 | doc-only | All | 动态变速 Mod ghost 时钟 — 见 wiki 角逐服务 |
| TL-022 | reserved | — | 已合并至 **TL-023**（勿在 StatisticsPanel 加 per-ruleset API） |
| TL-023 | active | All | `OsuGameBase`：`ReplaySession` 按 `score.Ruleset` → `CreateEzReplaySession()` 分发；现「第一个非 null」单例 |
| TL-024 | active | Mania | EzScoreGraphBase P3-Rest |
| TL-025 | active | Mania | EzScoreGraphMania P3-Rest |
| TL-026 | blocked | Mania | RunCombined 单次 run 多出口 |

维护：新增 `TODO(EZ-SR-TL-*)` 须先在本表加行；PR 合并时更新 status。

---

## §6 Cache 分层

| Cache | 持有者 | 用途 |
|-------|--------|------|
| `IEzScoreTimelineCache` | EzScoreRaceService / Player | 角逐 timeline 结果 |
| `EzReplaySession` Score/Timeline/Combined | Session Service | Panel / Graph / RunRequest |
| Graph offset debounce | 新 env key | 精确重算，独立条目 |

角逐 Builder **不**经 Session TimelineCache（`RunTimelineDirectAsync`）。

---

## §7 PR 拆分（本 epic）

| 提交 | 内容 | TODO |
|------|------|------|
| Phase-0 | 本 REGISTRY + TODO 标记 | TL-023~025 |
| PR-A | Mania Builder → IEzReplaySession；删 Bridge | TL-005 |
| PR-B | Osu legacy 模块；删 Mania HitEvents 补丁 | TL-010~017 |
| PR-C | wiki TL-021、REGISTRY 维护说明 | TL-020/021 |

分支 `ez/sr-tl-arch` 按上表分步提交。
