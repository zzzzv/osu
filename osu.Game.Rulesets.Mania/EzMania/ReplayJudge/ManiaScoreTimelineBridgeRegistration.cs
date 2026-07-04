// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// 注册 Mania 对 <see cref="EzScoreTimelineBridge"/> HitEvents 路径的 ScoreProcessor 上下文（无反射）。
    /// </summary>
    internal static class ManiaScoreTimelineBridgeRegistration
    {
        private static bool registered;

        internal static void EnsureRegistered()
        {
            if (registered)
                return;

            registered = true;

            EzScoreTimelineBridge.RegisterHitEventsScoreProcessorContext(applyHitEventsScoreProcessorContext);
        }

        private static void applyHitEventsScoreProcessorContext(ScoreProcessor scoreProcessor, ScoreInfo scoreInfo)
        {
            if (scoreInfo.Ruleset.OnlineID != 3)
                return;

            if (scoreProcessor is not ManiaScoreProcessor maniaScoreProcessor)
                return;

            // HitEvents 重放：使用 ScoreInfo 嵌入的 ManiaHitMode（统计/测试）；未嵌入时与 Session 路径一致走全局环境。
            maniaScoreProcessor.TimelineHitModeOverride = scoreInfo.ManiaHitMode != 0 || scoreInfo.ManiaHealthMode != 0
                ? (EzEnumHitMode)scoreInfo.ManiaHitMode
                : GlobalConfigStore.EzConfig.GetGameplayEnvironment().ManiaHitMode;
        }
    }
}
