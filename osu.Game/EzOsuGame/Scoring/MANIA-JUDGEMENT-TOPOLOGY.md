# Mania 判定 — 总拓扑（活文档）

> **用途**：全场景部件上台面、M/N 分叉、与「中央判定机」设想的差距；**实施优先级与批次**以此为准。  
> **姊妹文档**：[`MANIA-JUDGEMENT-RUNTIME.md`](./MANIA-JUDGEMENT-RUNTIME.md)（叙事/角色）、[`MANIA-SCORE-DATA-SOURCE-REGISTRY.md`](./MANIA-SCORE-DATA-SOURCE-REGISTRY.md)（数据面）。  
> **Cursor 详稿**：`osu-framework/.cursor/plans/mania_判定总拓扑_04712845.plan.md`（讨论过程可更长，本文件收收敛结论）。  
> **状态**：2026-07-13 初版；随批次推进 **随时增删改**。

---

## 0. 实施优先级（分批次）

| 批次 | 内容 | 状态 |
|------|------|------|
| **1** | 本文件落地 + 链到 RUNTIME / REGISTRY | 完成 `f161089` |
| **2** | Drawable 验证：`ManiaJudgeHotPathTrace`（`pressTimes` max、MissOffset、PressSnapAlloc） | 完成 |
| **3** | **Fix-1**：`Column.pressTimes` 有界裁剪；`ManiaDrawableMissTiming` 零分配 | 完成 |
| **4** | **Fix-2**：automiss 更早 gate，减少未进 miss 窗的 `UpdateResult` 入口 | 完成 |
| **5** | **MLPS 扩充**（Earliest/Combo/Duration/BMS post-Bad）+ MLC 同位置直调；**overlap 边界缓存**（register 时算 max 窗，按不再整列扫） | 完成 |
| **6** | `ManiaScoreHitEventGenerator` 收口到 `RunHitEventsAsync`；补 HitEvents 路径 p50/p95 bench（目标参考 &lt;10ms） | 完成 |
| **7+** | ReplayFrame 边沿解析抽出 + Race `FeedMode`（BatchAllEvents / StreamByClock）开关 | 完成 |

**原则**：先证 P0 疑点（批次 2）再改（3–4）；架构收拢（5–7）与 parity 测试同批。局内 FPS（Work/SwapBuffer）另案，见后续「热路径下一刀」。

---

## 1. 中央机设想（对照基准）

判定语义 **一份**；场景只差「谁、何时、以何格式送按键时间」。

```mermaid
flowchart TB
    subgraph adapters [场景适配器]
        Live[本地玩]
        Replay[回放/成绩]
        Race[Race ghost]
    end
    subgraph mid [中间层]
        Norm[ReplayFrame 或 press边沿 + 时钟]
    end
    subgraph core [中央判定 L2]
        Router[列路由 本列选一个]
        Judge[Kernel + HitMode]
        Miss[automiss / 被动Miss]
    end
    SP[ScoreProcessor]
    adapters --> mid --> Router --> Judge --> Miss --> SP
```

---

## 2. 现状分层

| 层 | 现状 |
|----|------|
| **L1** | `ManiaJudgementRound` + `ManiaWindowBaker` — M/N 各建一份，语义同源 |
| **L2** | `ManiaJudgementKernel` + Strategy — **Ez M/N 共用**；Lazer = M inline / N Replica |
| **L2.5 列路由** | **双份**：M=`ManiaLaneController`；N=`LaneTargetState` + 碎片逻辑 + `ManiaLanePressSelector`（仅 fold） |
| **L3** | M：Column/Drawable/每帧 automiss；N：`parseReplay`→边沿；ReplayPlayer 另路径 |
| **L4** | 光效/keysound — M 独有 |

**保留**：M≡N 目标；Graph/Race/重算走 N；Ez 判定公式单源。  
**收拢**：列 press 选择 → **扩充 MLPS**，MLC 只保留 Drawable 列状态；ReplayFrame 消费上提 Ez 层。

---

## 3. 总拓扑图（全部件）

```mermaid
flowchart TB
    subgraph inputs [输入]
        KB[键盘]
        RP[ReplayPlayer]
        DB[ReplayFrame DB]
    end
    subgraph M [M Drawable]
        Col[Column]
        MLC[ManiaLaneController]
        DHO[DrawableNote Hold]
        MEJ[ManiaEzDrawableJudgement]
        AMG[ManiaAutoMissGate]
    end
    subgraph N [N Session]
        Parse[FrameEdgeParser]
        Sim[Simulator]
        MLPS[ManiaLanePressSelector]
        FM[applyForcedMisses]
    end
    subgraph shared [M+N 共享]
        Round[ManiaJudgementRound]
        Kernel[ManiaJudgementKernel]
    end
    subgraph out [输出]
        SP[ScoreProcessor]
        Graph[EzScoreGraphMania]
        Race[EzScoreRaceService]
    end
    KB --> Col --> MLC --> MLPS
    MLC --> DHO --> MEJ --> Kernel
    DHO --> AMG
    RP --> Col
    DB --> Parse --> Sim --> MLPS --> Kernel
    Sim --> FM
    Round --> MLC
    Round --> MEJ
    Round --> Sim
    MEJ --> SP
    Sim --> SP
    SP --> Graph
    SP --> Race
```

---

## 4. 部件登记（简表）

| 部件 | M/N | 接入 Kernel？ | 备注 |
|------|-----|--------------|------|
| `ManiaJudgementKernel` | M+N | **核心** | note/hold 评估 |
| `ManiaJudgementRound` | M+N | 配置 | 开局冻结 |
| `ManiaLaneController` | M | 否 | Drawable 列状态；press 路由委托 MLPS |
| `ManiaLanePressSelector` | M+N | 否 | **press 选目标**（Earliest/Combo/Duration/BMS post-Bad Drawable + Session） |
| `collectCandidatesForInput` | N | 否 | Session 列候选；与 M `CollectOverlappingEntries` 语义对齐 |
| `Column.pressTimes` | M | 否 | **Fix-1** 有界列表 |
| `ManiaDrawableMissTiming` | M | 公式=N | **Fix-1** 零分配 |
| `ManiaAutoMissGate` | M | 门前 | **Fix-2** `ShouldDeferAutoMissUpdate` 跳过 `UpdateResult` |
| `parseReplay` / `ManiaReplayFrameEdgeParser` | N | 否 | Session 边沿解析；Batch / Stream 游标共用 |
| `ManiaFramedReplayInputHandler` | M | 否 | Drawable 回放帧态喂入；与边沿解析分工（不合并成一类） |
| `ManiaScoreHitEventGenerator` | — | — | **Obsolete**；请用 `RunHitEventsAsync` |
| `EzScoreRaceService` | N | 预建 / 流式 | `EzReplayFeedMode`：BatchAllEvents 阻塞进局；StreamByClock 不阻塞 |

**关注**：登记表中 M 专用 / N 专用 多，真正 M+N 少 — 调整拓扑时有意识收拢，不必一次做完。

---

## 5. 已确认的设计决策

### 5.1 MLPS 与 MLC

- **MLPS**（可改名 `ManiaLanePressPolicy`）：独立轻量 **press 选目标**；扩全 Earliest / Combo / Duration / BMS post-Bad 后 **够 N 用**。
- **MLC**：保留 Drawable 注册/游标/ActiveHold；收集候选后 **与 N 相同位置调 MLPS**。

### 5.2 列路由（Combo/Duration）

- 按 **本列** 一次 press，看 **overlap 候选**（`CollectOverlappingEntries` 用 register 时缓存的列 max miss 窗，不再每按扫全列）。
- BMS Drawable 侧 post-Bad KPoor 已进 MLPS；Session 侧仍用 `BmsHitModeJudgement.TryRoutePostBadKPoor`（待与 MLPS 合并）。

### 5.3 ReplayFrame

- Session：`ManiaReplayFrameEdgeParser`（`ParseAll` / `ManiaReplayFrameEdgeCursor.DrainUntil`）产出边沿；`ManiaReplaySession` 委托之。
- Drawable 回放仍走 `ManiaFramedReplayInputHandler`（帧态→按键系统）；与边沿解析 **分工保留**。
- 枚举 `EzReplayFeedMode` 在 `osu.Game/EzOsuGame/Scoring`；Race / Session 共用语义。

### 5.4 Race

- 预建 timeline + HUD 插值 **不算偏离设计**。
- **`Ez2Setting.EzScoreRaceFeedMode`**：`BatchAllEvents`（默认，PlayerLoader 等 timeline）/ `StreamByClock`（进局不阻塞，后台就绪后插值）。

### 5.5 Generator

- `ManiaScoreHitEventGenerator` 已 **Obsolete**；生产代码走 `ManiaReplaySessionService.RunHitEventsAsync`。
- Bench：`BenchmarkManiaReplaySession.BenchmarkRunHitEventsAsync`；暖机延迟烟测 `ManiaRunHitEventsLatencyTest`。
- **DRAWABLE-MICRO-BENCH**：`ManiaLaneHotPathWorkload` / `ManiaLaneHotPathMicroBenchTest` / `BenchmarkManiaLaneHotPath`（10 列 × PeakKps × alive/col 8/24/40；Select+Gate+pressTimes，**不含** SwapBuffer）。
  - **gate 调用量主导**墙钟；CPU 随 alive 近似线性。
  - Combo/Duration 曾因 (1) 每按 `new List`/Sort/`Func`、(2) `IsHitResultAllowed`→`GetHitModeValidHitResults` **每次 `new[]`**（经 `ResultFor`/`SelectFold` 放大）抬高 alloc；已改为 scratch + 静态表 + `IsHitResultValidForMode`。实测 Combo dense ~45 B/press（此前 ~1.8 KB）。

---

## 6. 场景喂入对照

| 场景 | 喂入 | 路径 |
|------|------|------|
| 本地玩 | 实时按键 | M |
| 看回放 | ReplayPlayer 帧→Column | M |
| 入库 | M 的 SP | Realm Statistics |
| 重算 / Graph Now | ReplayFrame→边沿 | N |
| 补 HitEvents | 同 N 全 Session | N（Generator 壳） |
| Race | N 预建 timeline | 打歌时插值 |
| Graph offset 拖动 | Rejudge | 非 Session |

---

## 7. 体感 ↔ 疑点

| 体感 | 优先疑点 |
|------|---------|
| 越久越卡 | `pressTimes` 无限增长 + Miss 快照 |
| 列多越卡 | 每列未 Judged drawable × 每帧 automiss 入口 |
| LN 多更卡 | 存活 drawable 多 + hold Update；**Empty 窗现已 defer 到 EndTime** |
| offset 偏后（不稳/周期/非正态） | 与帧时/Present 抖同源嫌疑；**关 Race 仍矮 → 非粘档主案**；选歌掉帧与 BDSP backfill+carousel Replace 相关 |
| 选歌 3–5s 掉帧 | `BackgroundDataStoreProcessor` 回填 + `RealmDetachedBeatmapStore` 无界 dequeue（已限流/延迟开工） |

---

## 8. 相关文档

- 叙事：[`MANIA-JUDGEMENT-RUNTIME.md`](./MANIA-JUDGEMENT-RUNTIME.md)
- 数据：[`MANIA-SCORE-DATA-SOURCE-REGISTRY.md`](./MANIA-SCORE-DATA-SOURCE-REGISTRY.md)
- Parity：[`REPLAY_JUDGE_MERGE.md`](../../osu.Game.Rulesets.Mania/EzMania/ReplayJudge/REPLAY_JUDGE_MERGE.md)
- 性能 backlog：[`HIGH_KPS_JUDGE_BACKLOG.md`](../../osu.Game.Rulesets.Mania/EzMania/ReplayJudge/HIGH_KPS_JUDGE_BACKLOG.md)

---

## 变更记录

| 日期 | 说明 |
|------|------|
| 2026-07-13 | 批次 2–3：`ManiaJudgeHotPathTrace` 扩展；Fix-1 有界 pressTimes + 零分配 MissTiming |
| 2026-07-14 | 批次 4：`DrawableHitObject.ShouldDeferAutoMissUpdate` + `ManiaAutoMissGate` 跳过 automiss 热路径 |
| 2026-07-14 | 批次 5：MLPS M/N 共用；MLC/Session 直调；overlap max 窗边界缓存 |
| 2026-07-14 | 批次 6：Generator Obsolete；Graph/测试改 RunHitEventsAsync；HitEvents BDN + 暖机延迟烟测 |
| 2026-07-14 | 批次 7+：`ManiaReplayFrameEdgeParser`；`EzReplayFeedMode` + Race 开关（StreamByClock 不阻塞进局） |
| 2026-07-14 | 非 Race FPS：Empty 窗 automiss defer；DetachedStore 每帧限流；BDSP 开工延迟 5s；pressTimes 保留窗收紧 |
| 2026-07-14 | MICRO-BENCH：alive 扫描 + Select overlap scratch / Func 缓存；`GetHitModeValidHitResults` 静态表（斩 ResultFor 热路径 `new[]`） |
