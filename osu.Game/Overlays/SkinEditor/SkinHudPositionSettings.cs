// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Overlays.SkinEditor
{
    /// <summary>
    /// 皮肤编辑器 HUD 组件通用的 X/Y 坐标滑条。
    /// </summary>
    internal sealed class SkinHudPositionSettings
    {
        private const float position_slider_range = 2000;

        private readonly Drawable component;

        private readonly BindableFloat positionX = new BindableFloat
        {
            MinValue = -position_slider_range,
            MaxValue = position_slider_range,
            Precision = 1,
            Default = 0,
            Value = 0,
        };

        private readonly BindableFloat positionY = new BindableFloat
        {
            MinValue = -position_slider_range,
            MaxValue = position_slider_range,
            Precision = 1,
            Default = 0,
            Value = 0,
        };

        private bool internalPositionUpdate;

        public static SkinHudPositionSettings? TryCreate(Drawable component)
        {
            if (component is not ISerialisableDrawable)
                return null;

            return new SkinHudPositionSettings(component);
        }

        private SkinHudPositionSettings(Drawable component)
        {
            this.component = component;

            positionX.Value = component.Position.X;
            positionY.Value = component.Position.Y;

            positionX.BindValueChanged(_ => updateComponentPositionFromSliders());
            positionY.BindValueChanged(_ => updateComponentPositionFromSliders());
        }

        public Drawable[] CreateControls() => new Drawable[]
        {
            new SettingsSlider<float>
            {
                LabelText = "Position X",
                TooltipText = SkinEditorStrings.ResetPosition,
                Current = positionX,
                KeyboardStep = positionX.Precision,
            },
            new SettingsSlider<float>
            {
                LabelText = "Position Y",
                TooltipText = SkinEditorStrings.ResetPosition,
                Current = positionY,
                KeyboardStep = positionY.Precision,
            },
        };

        public void SyncFromComponent()
        {
            if (internalPositionUpdate)
                return;

            if (positionX.Value != component.Position.X)
                positionX.Value = component.Position.X;

            if (positionY.Value != component.Position.Y)
                positionY.Value = component.Position.Y;
        }

        private void updateComponentPositionFromSliders()
        {
            internalPositionUpdate = true;
            component.Position = new Vector2(positionX.Value, positionY.Value);
            internalPositionUpdate = false;
        }
    }
}
