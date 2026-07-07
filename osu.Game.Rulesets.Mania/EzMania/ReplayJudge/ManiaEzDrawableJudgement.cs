// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.CompilerServices;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    internal static class ManiaEzDrawableJudgement
    {
        private static readonly ConditionalWeakTable<DrawableNote, BmsHitModeJudgement.BmsRouteState> note_bms_states =
            new ConditionalWeakTable<DrawableNote, BmsHitModeJudgement.BmsRouteState>();

        private static readonly ConditionalWeakTable<DrawableHoldNoteTail, BmsHitModeJudgement.BmsRouteState> tail_bms_states =
            new ConditionalWeakTable<DrawableHoldNoteTail, BmsHitModeJudgement.BmsRouteState>();

        private static readonly ConditionalWeakTable<DrawableHoldNote, O2HitModeJudgement.HoldBreakState> o2_hold_states =
            new ConditionalWeakTable<DrawableHoldNote, O2HitModeJudgement.HoldBreakState>();

        public static bool CanRouteToKPoor(DrawableNote note) => GetBmsState(note).CanRouteToKPoor;

        public static bool CanRouteToKPoor(DrawableHoldNoteTail tail) => GetBmsState(tail).CanRouteToKPoor;

        public static bool ShouldHideTailDisplayResult()
            => GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForLive).ManiaHitMode == EzEnumHitMode.O2Jam;

        internal static BmsHitModeJudgement.BmsRouteState GetBmsState(DrawableNote note)
        {
            if (note.FindClosestParent<Column>() is Column column && column.TryGetBmsRoute(note, out var route))
                return route;

            return note_bms_states.GetValue(note, _ => new BmsHitModeJudgement.BmsRouteState());
        }

        internal static BmsHitModeJudgement.BmsRouteState GetBmsState(DrawableHoldNoteTail tail)
            => tail_bms_states.GetValue(tail, _ => new BmsHitModeJudgement.BmsRouteState());

        private static ManiaJudgementRound getJudgementRound(DrawableHitObject? drawable = null)
        {
            if (drawable is DrawableManiaHitObject maniaDrawable
                && maniaDrawable.EzDrawableManiaRuleset?.JudgementRound is { } cachedRound)
            {
                return cachedRound;
            }

            if (drawable?.FindClosestParent<DrawableManiaRuleset>() is { JudgementRound: { } round })
                return round;

            var ruleset = drawable?.FindClosestParent<DrawableRuleset>();
            var purpose = ruleset?.ReplayScore != null ? ReplayRunPurpose.ForStored : ReplayRunPurpose.ForLive;
            var env = GlobalConfigStore.EzConfig.ResolveEnvironment(purpose, ruleset?.ReplayScore?.ScoreInfo);
            return ManiaJudgementRound.Create(env);
        }

        internal static bool TryMalodyHoldOnReleased(DrawableHoldNote hold)
        {
            var round = getJudgementRound(hold);

            if (!MalodyHitModeJudgement.IsMalodyMode(round.Environment.ManiaHitMode))
                return false;

            if (!hold.IsHolding.Value)
                return false;

            hold.Tail.UpdateResult();
            hold.EzTriggerMalodyBodyOnRelease();
            hold.EzReportHoldReleased();
            return true;
        }

        internal static bool TryMalodyHoldCheckForResult(DrawableHoldNote hold, bool userTriggered, double timeOffset)
        {
            var round = getJudgementRound(hold);

            if (!MalodyHitModeJudgement.IsMalodyMode(round.Environment.ManiaHitMode))
                return false;

            if (!hold.Tail.AllJudged)
                return false;

            hold.EzFinalizeMalodyHoldFromTail();
            return true;
        }

        internal static bool TryHitModeCheckForResult(DrawableNote note, bool userTriggered, double timeOffset)
            => TryApplyEzNoteCheckForResult(note, userTriggered, timeOffset);

        internal static bool TryHoldTailCheckForResult(DrawableHoldNoteTail tail, bool userTriggered, double timeOffset)
            => TryApplyEzHoldTailCheckForResult(tail, userTriggered, timeOffset);

        internal static bool TryBmsOnPressed(DrawableNote note, KeyBindingPressEvent<ManiaAction> e)
        {
            if (note.HitObject.HitWindows is not ManiaHitWindows maniaWindows)
                return false;

            var round = getJudgementRound(note);
            var state = GetBmsState(note);
            var action = BmsHitModeJudgement.Instance.TryPostBadOnPressed(maniaWindows, state, round.PoorEnabled);

            if (!action.Handled)
                return false;

            ApplyBmsAction(note, round, action, state);
            return true;
        }

        internal static void TryO2HoldUpdate(DrawableHoldNote hold)
        {
            var round = getJudgementRound(hold);

            if (round.Environment.ManiaHitMode != EzEnumHitMode.O2Jam)
                return;

            var state = o2_hold_states.GetValue(hold, _ => new O2HitModeJudgement.HoldBreakState());
            O2HitModeJudgement.Instance.ApplyDrawableHoldBreakUpdate(hold, state);
        }

        internal static bool TryO2HoldCheckForResult(DrawableHoldNote hold, bool userTriggered, double timeOffset)
        {
            var round = getJudgementRound(hold);

            if (round.Environment.ManiaHitMode != EzEnumHitMode.O2Jam)
                return false;

            return O2HitModeJudgement.Instance.TryO2HoldCheckForResult(hold, userTriggered, timeOffset);
        }

        internal static void ApplyNoteOutcome(DrawableNote drawable, ManiaJudgementRound round, ManiaNoteJudgementOutcome outcome, bool userTriggered = true)
        {
            switch (outcome.Kind)
            {
                case ManiaNoteJudgementOutcomeKind.Apply:
                    if (!userTriggered && outcome.Result == HitResult.Miss)
                        drawable.EzApplyPassiveMissWithStoredOffset();
                    else
                        drawable.EzApplyFinalResult(outcome.Result, round.Environment.ManiaHitMode);

                    break;

                case ManiaNoteJudgementOutcomeKind.DispatchExtra:
                    drawable.EzDispatchExtraResult(outcome.Result);
                    break;
            }
        }

        internal static void ApplyBmsAction(DrawableNote drawable, ManiaJudgementRound round, BmsHitModeJudgement.DrawableAction action, BmsHitModeJudgement.BmsRouteState state)
            => applyBmsAction(drawable, r => drawable.EzApplyFinalResult(r, round.Environment.ManiaHitMode), drawable.EzDispatchExtraResult, action, state);

        internal static void ApplyBmsAction(DrawableHoldNoteTail drawable, ManiaJudgementRound round, BmsHitModeJudgement.DrawableAction action, BmsHitModeJudgement.BmsRouteState state)
            => applyBmsAction(drawable, r => drawable.EzApplyFinalResult(r, round.Environment.ManiaHitMode), drawable.EzDispatchExtraResult, action, state);

        private static void applyBmsAction<T>(
            T _,
            Action<HitResult> applyFinal,
            Action<HitResult> dispatchExtra,
            BmsHitModeJudgement.DrawableAction action,
            BmsHitModeJudgement.BmsRouteState state)
        {
            if (!action.Handled)
                return;

            BmsHitModeJudgement.ApplyRouteState(state, action);

            var result = BmsHitModeJudgement.MapTo(action.Judge);

            if (action.DispatchExtra)
                dispatchExtra(result);
            else if (action.ApplyFinal)
                applyFinal(result);
        }

        internal static bool TryColumnHoldTailRelease(DrawableHoldNote hold, double currentTime, ManiaJudgementRound round)
        {
            if (!hold.IsHolding.Value)
                return false;

            if (ManiaEzDrawableJudgement.TryMalodyHoldOnReleased(hold))
                return true;

            hold.Tail.UpdateResult();
            hold.Body.TriggerResult(hold.Tail.IsHit);
            hold.Result.ReportHoldState(currentTime, false);
            return true;
        }

        internal static bool TryApplyEzNoteCheckForResult(DrawableNote drawable, bool userTriggered, double timeOffset)
        {
            ManiaJudgeHotPathTrace.RecordCheckForResult();

            var round = getJudgementRound(drawable);

            if (!userTriggered && !ManiaAutoMissGate.ShouldEvaluateAutoMiss(drawable.HitObject, timeOffset))
                return true;

            if (!round.IsEzHitMode)
                return false;

            var hitMode = round.Strategy;
            if (hitMode == null)
                return false;

            if (hitMode is BmsHitModeJudgement bms)
            {
                if (drawable.HitObject.HitWindows is not ManiaHitWindows maniaWindows)
                    return true;

                var state = GetBmsState(drawable);

                var action = userTriggered
                    ? bms.EvaluateDrawablePress(maniaWindows, timeOffset, state, round.PoorEnabled)
                    : bms.EvaluateDrawableAutoMiss(maniaWindows, timeOffset);

                ApplyBmsAction(drawable, round, action, state);
                return true;
            }

            if (hitMode is O2HitModeJudgement o2)
            {
                if (userTriggered)
                {
                    bool upgradeToPerfect = false;
                    bool cont = !round.PillModeEnabled
                                || O2HitModeExtension.PillCheckWithBpm(timeOffset, round.O2PressBpm, out bool _, out upgradeToPerfect);
                    var outcome = o2.EvaluatePress(timeOffset, drawable.HitObject.HitWindows!, new O2HitModeJudgement.NotePressContext
                    {
                        RawOffset = timeOffset,
                        Bpm = round.O2PressBpm,
                        UsePressTimeBpmForJudgement = true,
                        PillModeEnabled = round.PillModeEnabled,
                        PillCheckPassed = cont,
                        UpgradeToPerfect = upgradeToPerfect,
                        State = round.MutableState,
                    });

                    ApplyNoteOutcome(drawable, round, outcome, userTriggered: true);
                    return true;
                }

                long frameId = (long)(drawable.Time.Current * 1000);
                double bpm = round.GetO2BpmForAutoMiss(drawable.Time.Current, frameId);
                ApplyNoteOutcome(drawable, round, o2.EvaluateAutoMiss(timeOffset, drawable.HitObject.HitWindows!, bpm), userTriggered: false);
                return true;
            }

            if (hitMode is Ez2AcHitModeJudgement ez2Ac)
            {
                if (userTriggered)
                {
                    ApplyNoteOutcome(drawable, round, ez2Ac.EvaluateDrawablePress(timeOffset, drawable.HitObject.HitWindows!, drawable.HitObject is HeadNote), userTriggered: true);
                    return true;
                }

                ApplyNoteOutcome(drawable, round, ez2Ac.EvaluateAutoMiss(timeOffset, drawable.HitObject.HitWindows!), userTriggered: false);
                return true;
            }

            if (userTriggered)
            {
                ApplyNoteOutcome(drawable, round, hitMode.EvaluatePress(timeOffset, drawable.HitObject.HitWindows!), userTriggered: true);
                return true;
            }

            ApplyNoteOutcome(drawable, round, hitMode.EvaluateAutoMiss(timeOffset, drawable.HitObject.HitWindows!), userTriggered: false);
            return true;
        }

        internal static bool TryApplyEzHoldTailCheckForResult(DrawableHoldNoteTail drawable, bool userTriggered, double timeOffset)
        {
            ManiaJudgeHotPathTrace.RecordCheckForResult();

            var round = getJudgementRound(drawable);

            if (!userTriggered && !ManiaAutoMissGate.ShouldEvaluateAutoMiss(drawable.HitObject, timeOffset))
                return true;

            if (!round.IsEzHitMode)
                return false;

            var hitMode = round.Strategy;

            if (hitMode is MalodyHitModeJudgement)
            {
                if (!userTriggered && timeOffset >= 0)
                    drawable.EzApplyFinalResult(HitResult.IgnoreHit, round.Environment.ManiaHitMode);

                return true;
            }

            if (hitMode is BmsHitModeJudgement bms)
            {
                if (drawable.HitObject.HitWindows is not ManiaHitWindows maniaWindows)
                    return true;

                var state = GetBmsState(drawable);
                bool forcePoor = !drawable.HoldNote.IsHolding.Value && drawable.HoldNote.Body.HasHoldBreak;

                var action = userTriggered
                    ? bms.EvaluateDrawablePress(maniaWindows, timeOffset, state, round.PoorEnabled, forcePoorOnTailHoldBreak: forcePoor)
                    : bms.EvaluateDrawableAutoMiss(maniaWindows, timeOffset);

                ApplyBmsAction(drawable, round, action, state);
                return true;
            }

            if (hitMode is O2HitModeJudgement o2)
            {
                (drawable.HitObject.HitWindows as ManiaHitWindows)?.UpdateO2JamBpmFromTime(drawable.Time.Current);

                if (!userTriggered)
                {
                    if (drawable.HoldNote.Body.HasHoldBreak)
                    {
                        if (timeOffset < 0)
                            return true;

                        drawable.EzApplyFinalResult(O2HitModeJudgement.MapTo(O2Judge.Miss), round.Environment.ManiaHitMode);
                        return true;
                    }

                    if (!drawable.HitObject.HitWindows!.CanBeHit(timeOffset))
                        drawable.EzApplyMinResult();

                    return true;
                }

                bool cont = O2HitModeExtension.PillCheck(timeOffset, drawable.Time.Current, out bool _, out bool upgradeToPerfect);
                var result = o2.EvaluateDrawableTailPress(timeOffset, drawable.HitObject.HitWindows!, new O2HitModeJudgement.DrawableTailContext
                {
                    CurrentTime = drawable.Time.Current,
                    PillCheckPassed = cont,
                    UpgradeToPerfect = upgradeToPerfect,
                    HeadHit = drawable.HoldNote.Head.IsHit,
                    HoldBroken = drawable.HoldNote.Body.HasHoldBreak,
                    WasHolding = drawable.HoldNote.IsHolding.Value,
                    PillModeEnabled = round.PillModeEnabled,
                }, round.MutableState);

                if (result != null)
                    drawable.EzApplyFinalResult(result.Value, round.Environment.ManiaHitMode);

                return true;
            }

            if (hitMode is Ez2AcHitModeJudgement ez2Ac)
            {
                bool headMissOrBreak = !drawable.HoldNote.Head.IsHit || drawable.HoldNote.Body.HasHoldBreak;

                if (!userTriggered)
                {
                    if (timeOffset < 0)
                        return true;

                    if (headMissOrBreak)
                    {
                        if (!drawable.HitObject.HitWindows!.CanBeHit(timeOffset))
                            drawable.EzApplyMinResult();

                        return true;
                    }
                }

                var tailJudge = ez2Ac.EvaluateTailJudge(new HoldTailEvaluationContext
                {
                    RawOffset = timeOffset,
                    TimeOffsetForJudgement = timeOffset,
                    HitWindows = drawable.HitObject.HitWindows!,
                    HeadHit = drawable.HoldNote.Head.IsHit,
                    HoldBreak = ez2Ac.IsHoldBreak(timeOffset, drawable.HitObject.HitWindows!),
                    HoldBroken = drawable.HoldNote.Body.HasHoldBreak,
                    WasHoldingBeforeRelease = drawable.HoldNote.IsHolding.Value,
                });

                if (tailJudge == Ez2AcJudge.None)
                {
                    if (!userTriggered && !drawable.HitObject.HitWindows!.CanBeHit(timeOffset))
                        drawable.EzApplyMinResult();

                    return true;
                }

                drawable.EzApplyFinalResult(Ez2AcHitModeJudgement.MapTo(tailJudge), round.Environment.ManiaHitMode);
                return true;
            }

            if (hitMode == null)
                return false;

            var genericResult = hitMode.EvaluateTail(new HoldTailEvaluationContext
            {
                RawOffset = timeOffset,
                TimeOffsetForJudgement = timeOffset,
                HitWindows = drawable.HitObject.HitWindows!,
                HeadHit = drawable.HoldNote.Head.IsHit,
                HoldBreak = hitMode.IsHoldBreak(timeOffset, drawable.HitObject.HitWindows!),
                HoldBroken = drawable.HoldNote.Body.HasHoldBreak,
                WasHoldingBeforeRelease = drawable.HoldNote.IsHolding.Value,
            });

            if (genericResult == HitResult.None)
                return true;

            drawable.EzApplyFinalResult(genericResult, round.Environment.ManiaHitMode);
            return true;
        }
    }
}
