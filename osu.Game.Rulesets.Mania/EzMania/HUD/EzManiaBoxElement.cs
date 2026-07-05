// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.HUD;
using osu.Game.Rulesets.Mania.EzMania.Localization;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.EzMania.HUD
{
    public partial class EzManiaBoxElement : EzBoxElement
    {
        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.MATCH_PANEL_WIDTH_LABEL), nameof(EzHUDManiaStrings.MATCH_PANEL_WIDTH_DESCRIPTION))]
        public BindableBool MatchManiaPanelWidth { get; } = ManiaHudPanelWidthHelper.CreateDefaultBindable();

        private Bindable<double> columnWidth = null!;
        private Bindable<double> specialFactor = null!;
        private Bindable<ColumnWidthStyle> columnWidthStyle = null!;
        private Ez2ConfigManager ezSkinConfig = null!;

        [Resolved(canBeNull: true)]
        private InputCountController? inputCountController { get; set; }

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezSkinConfig)
        {
            this.ezSkinConfig = ezSkinConfig;
            columnWidth = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnWidth);
            specialFactor = ezSkinConfig.GetBindable<double>(Ez2Setting.SpecialFactor);
            columnWidthStyle = ezSkinConfig.GetBindable<ColumnWidthStyle>(Ez2Setting.ColumnWidthStyle);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            columnWidth.BindValueChanged(_ => updateWidth());
            specialFactor.BindValueChanged(_ => updateWidth());
            columnWidthStyle.BindValueChanged(_ => updateWidth());
            MatchManiaPanelWidth.BindValueChanged(_ => updateWidth(), true);
            BoxWidth.BindValueChanged(_ => updateWidth());

            ezSkinConfig.ColumnTypeChanged += (_, __, ___) => updateWidth();
            skin.SourceChanged += updateWidth;
        }

        private void updateWidth()
        {
            if (MatchManiaPanelWidth.Value && tryGetManiaPanelWidth(out float panelWidth))
            {
                Width = panelWidth;
                return;
            }

            Width = BoxWidth.Value;
        }

        private bool tryGetManiaPanelWidth(out float panelWidth)
        {
            panelWidth = 0;

            int keyMode = inputCountController?.Triggers.Count ?? 0;
            int displayColumns = ManiaEzColumnLayout.GetDisplayColumnCount(keyMode);

            if (displayColumns <= 0)
                return false;

            panelWidth = ManiaColumnLayoutHelper.CalculatePanelTotalWidth(
                keyMode,
                displayColumns,
                skin,
                skinManager,
                ezSkinConfig,
                columnWidth.Value,
                specialFactor.Value,
                columnWidthStyle.Value,
                mobileAdjust: 1f,
                applyGlobalWidthSettings: true);

            return panelWidth > 0;
        }
    }
}
