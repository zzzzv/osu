// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.ReplayJudge
{
    [TestFixture]
    public class OsuReplaySessionServiceParityTest
    {
        [Test]
        public async Task TestRunAsyncTotalScoreMatchesSessionRun()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateTwoCircleTap();

            var sessionResult = OsuReplaySession.Run(score, beatmap, environment);
            var serviceResult = await new OsuReplaySessionService().RunAsync(score.DeepClone(), beatmap, ReplayRunPurpose.ForStored).ConfigureAwait(true);

            Assert.That(serviceResult.ScoreInfo.TotalScore, Is.EqualTo(sessionResult.ScoreInfo.TotalScore));
        }

        [Test]
        public async Task TestRunTimelineAsyncMatchesSessionRunTimeline()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateTwoCircleTap();

            var sessionTimeline = OsuReplaySession.RunTimeline(score, beatmap, environment);
            var serviceTimeline = await new OsuReplaySessionService().RunTimelineAsync(score.DeepClone(), beatmap, ReplayRunPurpose.ForStored).ConfigureAwait(true);

            Assert.That(serviceTimeline.FinalTotalScore, Is.EqualTo(sessionTimeline.FinalTotalScore));
        }

        [Test]
        public async Task TestRunRequestAsyncMatchesRunWithTimeline()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateTwoCircleTap();
            var service = new OsuReplaySessionService();

            var (directScore, directTimeline) = OsuReplaySession.RunWithTimeline(score.DeepClone(), beatmap, environment);

            var requestResult = await service.RunRequestAsync(new ReplayRunRequest(
                score.DeepClone(),
                beatmap,
                ReplayRunPurpose.ForStored)).ConfigureAwait(true);

            Assert.That(requestResult.Score.ScoreInfo.TotalScore, Is.EqualTo(directScore.ScoreInfo.TotalScore));
            Assert.That(requestResult.Timeline!.FinalTotalScore, Is.EqualTo(directTimeline.FinalTotalScore));
        }

        [Test]
        public async Task TestServiceCacheReusesSingleRun()
        {
            var (score, beatmap, _) = OsuReplayFixtures.CreateTwoCircleTap();
            var service = new OsuReplaySessionService();

            var first = await service.RunAsync(score.DeepClone(), beatmap, ReplayRunPurpose.ForStored).ConfigureAwait(true);
            var second = await service.RunAsync(score.DeepClone(), beatmap, ReplayRunPurpose.ForStored).ConfigureAwait(true);

            Assert.That(second.ScoreInfo.TotalScore, Is.EqualTo(first.ScoreInfo.TotalScore));
            Assert.That(second.ScoreInfo.HitEvents.Count, Is.EqualTo(first.ScoreInfo.HitEvents.Count));
        }
    }
}
