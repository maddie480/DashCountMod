using Celeste.Mod.DashCountMod.Features;
using System;
using static Celeste.Mod.DashCountMod.DashCountModSettings;

namespace Celeste.Mod.DashCountMod {
    public class DashCountModModule : EverestModule {

        public static DashCountModModule Instance;

        public override Type SettingsType => typeof(DashCountModSettings);
        public override Type SaveDataType => typeof(DashCountModSaveData);

        public DashCountModModule() {
            Instance = this;
            Logger.SetLogLevel("DashCountMod", LogLevel.Info);
        }

        public override void Load() {
            CustomDashCounting.Load();
            DashCountJournalPage.Load();
            DashCountInChapterPanel.Instance.Load();
        }

        public override void Unload() {
            CustomDashCounting.Unload();
            DashCountJournalPage.Unload();
            DashCountInChapterPanel.Instance.Unload();

            // "disable" all options in order to unhook associated stuff
            CountDreamDashRedirectsAsDashes.SetEnabled(false);
            DashCountInChapterPanel.Instance.SetValue(CountOptionsInChapterPanel.None);
            DashCountOnProgressPage.SetValue(CountOptionsInChapterPanel.None);
            DisplayDashCountInLevel.SetValue(ShowCountInGameOptions.None);
            DoNotResetDashCountOnDeath.SetEnabled(false);
        }

        public override void Initialize() {
            base.Initialize();

            CountDreamDashRedirectsAsDashes.Initialize();
            DashCountOnProgressPage.Initialize();
            DashCountInChapterPanel.Instance.Initialize();
        }
    }
}
