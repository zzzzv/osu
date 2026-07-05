// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 单局游玩环境快照。由 <see cref="Configuration.Ez2ConfigManager.ResolveForSession"/> 或
    /// <see cref="Configuration.Ez2ConfigManager.ResolveForDrawable"/> 在 resolve 时刻从 EzConfig 读出；
    /// 下游仿真/判定须使用已解析实例，禁止再读全局 bindable。
    /// </summary>
    public interface IGameplayEnvironment
    {
        EzEnumHitMode ManiaHitMode { get; }

        EzEnumHealthMode ManiaHealthMode { get; }

        EzEnumJudgePrecedence JudgePrecedence { get; }

        double OffsetPlusMania { get; }

        bool BmsPoorHitResultEnable { get; }
    }
}
