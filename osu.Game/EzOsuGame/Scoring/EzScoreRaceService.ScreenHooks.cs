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

        private void subscribeScreenHooks()
        {
            game.ScreenStack.ScreenPushed += onScreenPushed;
            game.ScreenStack.ScreenExited += onScreenExited;

            // 服务关闭时不做任何屏幕绑定，保证 0 影响；启用后由 onServiceEnabledChanged 补绑当前屏幕。
            if (isServiceActive)
                bindModsFromScreen(game.ScreenStack.CurrentScreen as OsuScreen);
        }

        private void unsubscribeScreenHooks()
        {
            game.ScreenStack.ScreenPushed -= onScreenPushed;
            game.ScreenStack.ScreenExited -= onScreenExited;
            unbindScreenMods();
        }

        private void onScreenPushed(IScreen lastScreen, IScreen newScreen)
        {
            if (!isServiceActive)
                return;

            if (newScreen is PlayerLoader)
            {
                bindModsFromScreen(newScreen as OsuScreen);
                beginLoaderPreparation();
                return;
            }

            bindModsFromScreen(newScreen as OsuScreen);
        }

        private void onScreenExited(IScreen lastScreen, IScreen newScreen)
        {
            if (!isServiceActive)
                return;

            if (lastScreen is PlayerLoader)
                endLoaderPreparation(advancingToPlayer: newScreen is Player);

            bindModsFromScreen(newScreen as OsuScreen);
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
