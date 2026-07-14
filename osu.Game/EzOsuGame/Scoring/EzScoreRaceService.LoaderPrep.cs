// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Scoring
{
    public partial class EzScoreRaceService : IEzScoreRacePlayerStartGate
    {
        /// <inheritdoc/>
        public bool CanStartPlayer => !isBlockingPlayerLoaderStart();

        private bool loaderPreparationActive;
        private bool loaderPreparationPending;

        private bool shouldPerformScoreRaceWork => hasConsumers || loaderPreparationActive;

        /// <summary>
        /// <see cref="PlayerLoader"/> 进入：拉取 ghost 元数据并全速构建 timeline，直至 <see cref="CanStartPlayer"/> 为 true。
        /// </summary>
        private void beginLoaderPreparation()
        {
            if (!isServiceActive)
                return;

            loaderPreparationActive = true;
            loaderPreparationPending = true;

            if (currentBeatmap.Value?.BeatmapInfo != null)
                refreshMetadata(currentBeatmap.Value);

            loaderPreparationPending = false;
            requestTimelineBuild(priority: true);
        }

        /// <summary>
        /// <see cref="PlayerLoader"/> 退出：结束 loader 门控；返回选歌时取消在途 build。
        /// </summary>
        private void endLoaderPreparation(bool advancingToPlayer)
        {
            loaderPreparationActive = false;
            loaderPreparationPending = false;

            if (!advancingToPlayer)
                cancelTimelineBuild();
        }

        private bool isBlockingPlayerLoaderStart()
        {
            if (!loaderPreparationActive)
                return false;

            // StreamByClock：不阻塞进局；timeline 后台就绪后由 HUD 插值接管。
            if (feedMode.Value == EzReplayFeedMode.StreamByClock)
                return false;

            if (loaderPreparationPending)
                return true;

            if (!requiresGhostTimelinePreparation())
                return false;

            return !areAllGhostTimelinesReady();
        }

        private bool requiresGhostTimelinePreparation()
        {
            var beatmapInfo = currentBeatmap.Value?.BeatmapInfo;

            if (beatmapInfo == null || !EzScoreRaceRulesetSupport.SupportsGhostRace(beatmapInfo.Ruleset))
                return false;

            return states.Count > 0;
        }

        private bool areAllGhostTimelinesReady()
        {
            if (states.Count == 0)
                return true;

            if (IsTimelineBuildInProgress)
                return false;

            return states.Values.All(s => s.Timeline != null);
        }
    }
}
