// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Screens;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Scoring
{
    public partial class EzScoreRaceService
    {
        [Resolved]
        private OsuGame game { get; set; } = null!;

        private IBindable<IReadOnlyList<Mod>>? boundScreenMods;
        private OsuScreen? boundModsScreen;
        private bool screenHooksSubscribed;

        private void ensureScreenHooksSubscribed()
        {
            if (screenHooksSubscribed)
                return;

            game.ScreenStack.ScreenPushed += onScreenPushed;
            game.ScreenStack.ScreenExited += onScreenExited;
            bindModsFromScreen(game.ScreenStack.CurrentScreen as OsuScreen);
            screenHooksSubscribed = true;
        }

        private void unsubscribeScreenHooks()
        {
            if (!screenHooksSubscribed)
                return;

            game.ScreenStack.ScreenPushed -= onScreenPushed;
            game.ScreenStack.ScreenExited -= onScreenExited;
            unbindScreenMods();
            screenHooksSubscribed = false;
        }

        private void onScreenPushed(IScreen lastScreen, IScreen newScreen)
        {
            if (!isServiceActive || !hasConsumers)
                return;

            if (newScreen is Player)
            {
                cancelTimelineBuild();
                return;
            }

            bindModsFromScreen(newScreen as OsuScreen);

            if (newScreen is PlayerLoader)
                beginScoreRacePreparation();
        }

        private void onScreenExited(IScreen lastScreen, IScreen newScreen)
        {
            if (!isServiceActive || !hasConsumers)
                return;

            bindModsFromScreen(newScreen as OsuScreen);

            if (lastScreen is Player)
                scheduleTimelineBuildIfNeeded();
        }

        private void bindModsFromScreen(OsuScreen? screen)
        {
            if (boundModsScreen == screen)
                return;

            unbindScreenMods();
            boundModsScreen = screen;

            if (screen == null)
                return;

            boundScreenMods = screen.Mods.GetBoundCopy();
            boundScreenMods.BindValueChanged(_ => onQueryContextChanged(), true);
        }

        private void unbindScreenMods()
        {
            boundScreenMods?.UnbindAll();
            boundScreenMods = null;
            boundModsScreen = null;
        }

        private Mod[] getCurrentMods()
        {
            if (boundScreenMods?.Value == null || boundScreenMods.Value.Count == 0)
                return [];

            return boundScreenMods.Value.ToArray();
        }
    }
}
