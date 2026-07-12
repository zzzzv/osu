// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.EzOsuGame.Statistics
{
    public partial class EnumModeButton<TEnum> : RoundedButton, IHasPopover
        where TEnum : struct, Enum
    {
        private readonly Bindable<TEnum> current;
        private readonly Action<TEnum> setValue;

        public EnumModeButton(Bindable<TEnum> current, Action<TEnum>? setValue = null)
        {
            this.current = current;
            this.setValue = setValue ?? (value => current.Value = value);

            Size = new Vector2(75, 30);
            Text = current.Value.ToString();

            current.BindValueChanged(v => Text = v.NewValue.ToString());

            Action = this.ShowPopover;
        }

        public Popover GetPopover() => new EnumModePopover(setValue);

        private partial class EnumModePopover : OsuPopover
        {
            public EnumModePopover(Action<TEnum> setValue)
                : base(false)
            {
                Body.CornerRadius = 4;
                AllowableAnchors = new[] { Anchor.TopCentre };

                Children = new[]
                {
                    new OsuMenu(Direction.Vertical, true)
                    {
                        Items = Enum.GetValues<TEnum>()
                                    .Select(mode => new OsuMenuItem(mode.ToString(), MenuItemType.Standard, () => setValue(mode)))
                                    .ToArray(),
                        MaxHeight = 375,
                    },
                };
            }
        }
    }
}
