// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// 角逐 HUD 组件基类：解析 <see cref="GameplayClockContainer"/>，并协助绑定 <see cref="EzScoreRaceService"/>。
    /// </summary>
    public abstract partial class EzHUDScoreRaceComponent : CompositeDrawable
    {
        [Resolved(canBeNull: true)]
        protected GameplayState? GameplayState { get; private set; }

        [Resolved(canBeNull: true)]
        protected ScoreProcessor? ScoreProcessor { get; private set; }

        /// <summary>
        /// 已就位的 <see cref="Game.Screens.Play.GameplayClockContainer"/>，可作为 processor 的 ReferenceClock。
        /// 退出 / 重新进入 Player 时该引用会失效，调用方需重新解析。
        /// </summary>
        protected IClock? GameplayClock => GameplayClockContainer;

        public GameplayClockContainer? GameplayClockContainer;

        /// <summary>
        /// 用于派生类判断当前规则集是否支持 ghost 角逐。
        /// </summary>
        protected bool SupportsGhostRace => EzScoreRaceRulesetSupport.SupportsGhostRace(GameplayState?.Ruleset.RulesetInfo);

        protected EzScoreRaceService? ScoreRaceService { get; private set; }

        private protected OsuSpriteText? LoadingText;

        private bool scoreRaceInterestRegistered;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Schedule(() =>
            {
                if (GameplayClockContainer == null)
                {
                    GameplayClockContainer = this.FindClosestParent<GameplayClockContainer>();
                    if (GameplayClockContainer != null)
                        OnGameplayClockResolved(GameplayClockContainer);
                }
            });

            OnSessionReady();
        }

        /// <summary>
        /// 当 <see cref="Game.Screens.Play.GameplayClockContainer"/> 已就位时调用，派生类可在此将 clock 注入到自己的 processor。
        /// </summary>
        protected virtual void OnGameplayClockResolved(GameplayClockContainer clock)
        {
        }

        /// <summary>
        /// 在 <see cref="LoadComplete"/> 完成后调用，组件可在此绑定 <see cref="EzScoreRaceService.States"/>。
        /// </summary>
        protected virtual void OnSessionReady()
        {
        }

        protected double GetCurrentClockTime()
        {
            return GameplayClockContainer?.CurrentTime ?? 0;
        }

        protected long GetLiveDisplayScore(ScoringMode mode = ScoringMode.Standardised) => ScoreProcessor?.GetDisplayScore(mode) ?? 0;

        protected void EnsureLoadingOverlay()
        {
            if (LoadingText != null)
                return;

            LoadingText = new OsuSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = EzHUDStrings.SCORE_RACE_LOADING_LABEL,
                Font = OsuFont.GetFont(size: 14),
            };

            AddInternal(LoadingText);
        }

        protected virtual void OnEntriesChangedScheduled()
        {
        }

        /// <summary>
        /// 绑定全局 <see cref="EzScoreRaceService"/> 的 States / ModFilter / MaxEntries，并注册 consumer interest。
        /// </summary>
        protected bool TryBindScoreRaceService(
            ref IBindableDictionary<string, EzScoreRaceState>? stateLookup,
            Bindable<EzScoreModFilter>? modFilter = null,
            BindableNumber<int>? maxEntries = null)
        {
            ScoreRaceService ??= (EzScoreRaceService?)Dependencies.Get(typeof(EzScoreRaceService));

            if (ScoreRaceService == null)
                return false;

            registerScoreRaceInterest();

            modFilter?.BindTo(ScoreRaceService.ModFilter);
            maxEntries?.BindTo(ScoreRaceService.MaxEntries);

            if (stateLookup == null)
            {
                stateLookup = ScoreRaceService.States;
                stateLookup.BindCollectionChanged(onScoreRaceStatesChanged, true);
            }

            return true;
        }

        protected void RegisterScoreRaceInterest() => registerScoreRaceInterest();

        protected void UnregisterScoreRaceInterest() => unregisterScoreRaceInterest();

        private void registerScoreRaceInterest()
        {
            if (scoreRaceInterestRegistered)
                return;

            ScoreRaceService ??= (EzScoreRaceService?)Dependencies.Get(typeof(EzScoreRaceService));

            if (ScoreRaceService == null)
                return;

            ScoreRaceService.RegisterInterest();
            scoreRaceInterestRegistered = true;
        }

        private void unregisterScoreRaceInterest()
        {
            if (!scoreRaceInterestRegistered)
                return;

            ScoreRaceService?.UnregisterInterest();
            scoreRaceInterestRegistered = false;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                unregisterScoreRaceInterest();

            base.Dispose(isDisposing);
        }

        private void onScoreRaceStatesChanged(object? sender, NotifyDictionaryChangedEventArgs<string, EzScoreRaceState> e)
            => OnScoreRaceStatesChanged();

        /// <summary>States 字典变化时调用（默认转发到 <see cref="OnEntriesChangedScheduled"/>）。</summary>
        protected virtual void OnScoreRaceStatesChanged() => OnEntriesChangedScheduled();
    }
}
