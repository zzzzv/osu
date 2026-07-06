// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// 单局 Mania 判定上下文：开局冻结环境、策略与派生标志，热路径禁止再解析全局配置。
    /// </summary>
    public sealed class ManiaJudgementRound
    {
        public GameplayEnvironment Environment { get; }

        public IManiaHitModeJudgement? Strategy { get; }

        public bool PoorEnabled { get; }

        public bool PillModeEnabled { get; }

        public EzEnumJudgePrecedence JudgePrecedence { get; }

        public ManiaReplayJudgementState MutableState { get; }

        private ManiaJudgementRound(
            GameplayEnvironment environment,
            IManiaHitModeJudgement? strategy,
            bool poorEnabled,
            bool pillModeEnabled,
            ManiaReplayJudgementState mutableState)
        {
            Environment = environment;
            Strategy = strategy;
            PoorEnabled = poorEnabled;
            PillModeEnabled = pillModeEnabled;
            JudgePrecedence = environment.JudgePrecedence;
            MutableState = mutableState;
        }

        public static ManiaJudgementRound Create(GameplayEnvironment environment)
        {
            var strategy = ManiaJudgementRegistry.GetHitModeJudgement(environment.ManiaHitMode);
            bool poorEnabled = HealthModeHelper.ComputeKPoorEnabled(environment.ManiaHealthMode, environment.BmsPoorHitResultEnable);
            bool pillModeEnabled = environment.ManiaHealthMode.ToString().Contains("O2Jam");

            return new ManiaJudgementRound(
                environment,
                strategy,
                poorEnabled,
                pillModeEnabled,
                new ManiaReplayJudgementState());
        }

        public bool IsEzHitMode => ManiaJudgementRegistry.IsEzHitMode(Environment.ManiaHitMode);
    }
}
