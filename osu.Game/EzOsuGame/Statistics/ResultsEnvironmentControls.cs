// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.EzOsuGame.Statistics
{
    /// <summary>
    /// 结算界面底部 HitMode / HealthMode / 设置 / Offset 控件组。
    /// </summary>
    public partial class ResultsEnvironmentControls : FillFlowContainer
    {
        public IBindable<EzEnumHitMode> HitModeBindable { get; private set; } = null!;

        public IBindable<EzEnumHealthMode> HealthModeBindable { get; private set; } = null!;

        public ResultsEnvironmentControls()
        {
            AutoSizeAxes = Axes.Both;
            Spacing = new Vector2(5);
            Direction = FillDirection.Horizontal;
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            var hitModeBindable = ezConfig.GetBindable<EzEnumHitMode>(Ez2Setting.ManiaHitMode);
            var healthModeBindable = ezConfig.GetBindable<EzEnumHealthMode>(Ez2Setting.ManiaHealthMode);

            HitModeBindable = hitModeBindable;
            HealthModeBindable = healthModeBindable;

            Children = new Drawable[]
            {
                wrapVerticallyCentred(new EnumModeButton<EzEnumHitMode>(hitModeBindable)),
                wrapVerticallyCentred(new EnumModeButton<EzEnumHealthMode>(healthModeBindable)),
                wrapVerticallyCentred(new IconButton
                {
                    Icon = FontAwesome.Solid.Cog,
                    Action = () =>
                    {
                        /* TODO: show settings menu */
                    }
                }),
                new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Child = new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = "HitResult Offset",
                                Font = OsuFont.Default.With(size: 14),
                                Margin = new MarginPadding { Left = 5 },
                            },
                            new SettingsSlider<double>
                            {
                                AutoSizeAxes = Axes.Y,
                                RelativeSizeAxes = Axes.None,
                                Width = 220,
                                Current = ezConfig.GetBindable<double>(Ez2Setting.OffsetPlusMania),
                            }
                        }
                    }
                },
            };
        }

        private static Container wrapVerticallyCentred(Drawable drawable)
        {
            return new Container
            {
                AutoSizeAxes = Axes.Both,
                Child = drawable.With(d =>
                {
                    d.Anchor = Anchor.Centre;
                    d.Origin = Anchor.Centre;
                })
            };
        }
    }
}
