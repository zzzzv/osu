// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.ComponentModel;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania
{
    [Cached] // Used for touch input, see Column.OnTouchDown/OnTouchUp.
    public partial class ManiaInputManager : RulesetInputManager<ManiaAction>
    {
        public ManiaInputManager(RulesetInfo ruleset, int variant)
            : base(ruleset, variant, SimultaneousBindingMode.Unique)
        {
        }

        protected override KeyBindingContainer<ManiaAction> CreateKeyBindingContainer(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
            => new ManiaKeyBindingContainer(ruleset, variant, unique);

        /// <summary>
        /// COLUMN-INPUT：列级 <see cref="Column.OnPressed"/> 优先于列内 drawable，避免每键 N 路冒泡。
        /// </summary>
        private partial class ManiaKeyBindingContainer : RulesetKeyBindingContainer
        {
            private readonly List<Drawable> columnFirstQueue = new List<Drawable>();
            private readonly List<Drawable> nonColumnQueue = new List<Drawable>();

            public ManiaKeyBindingContainer(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
                : base(ruleset, variant, unique)
            {
            }

            protected override IEnumerable<Drawable> KeyBindingInputQueue
            {
                get
                {
                    columnFirstQueue.Clear();
                    nonColumnQueue.Clear();

                    foreach (var drawable in base.KeyBindingInputQueue)
                    {
                        if (drawable is Column)
                            columnFirstQueue.Add(drawable);
                        else
                            nonColumnQueue.Add(drawable);
                    }

                    if (columnFirstQueue.Count == 0)
                        return base.KeyBindingInputQueue;

                    columnFirstQueue.AddRange(nonColumnQueue);
                    return columnFirstQueue;
                }
            }
        }
    }

    public enum ManiaAction
    {
        [Description("Key 1")]
        Key1,

        [Description("Key 2")]
        Key2,

        [Description("Key 3")]
        Key3,

        [Description("Key 4")]
        Key4,

        [Description("Key 5")]
        Key5,

        [Description("Key 6")]
        Key6,

        [Description("Key 7")]
        Key7,

        [Description("Key 8")]
        Key8,

        [Description("Key 9")]
        Key9,

        [Description("Key 10")]
        Key10,

        [Description("Key 11")]
        Key11,

        [Description("Key 12")]
        Key12,

        [Description("Key 13")]
        Key13,

        [Description("Key 14")]
        Key14,

        [Description("Key 15")]
        Key15,

        [Description("Key 16")]
        Key16,

        [Description("Key 17")]
        Key17,

        [Description("Key 18")]
        Key18,

        [Description("Key 19")]
        Key19,

        [Description("Key 20")]
        Key20,
    }
}
