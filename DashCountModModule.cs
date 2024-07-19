using Celeste.Mod.DashCountMod.Features;
using System;
using static Celeste.Mod.DashCountMod.DashCountModSettings;

namespace Celeste.Mod.DashCountMod {
    public class DashCountModModule : EverestModule {

        public static DashCountModModule Instance;

        public override Type SettingsType => typeof(DashCountModSettings);
        public override Type SaveDataType => typeof(DashCountModSaveData);
        public override Type SessionType => typeof(DashCountModSession);

        public DashCountModModule() {
            Instance = this;
            Logger.SetLogLevel("DashCountMod", LogLevel.Info);
        }

        public override void Load() {
            CustomDashCounting.Load();
            CustomJumpCounting.Load();
            DashCountJournalPage.Load();
            JumpCountJournalPage.Load();
            DashCountInChapterPanel.Instance.Load();
            JumpCountInChapterPanel.Instance.Load();
        }

        public override void Unload() {
            CustomDashCounting.Unload();
            CustomJumpCounting.Unload();
            DashCountJournalPage.Unload();
            JumpCountJournalPage.Unload();
            DashCountInChapterPanel.Instance.Unload();
            JumpCountInChapterPanel.Instance.Unload();

            // "disable" all options in order to unhook associated stuff
            CountDreamDashRedirectsAsDashes.SetEnabled(false);
            DashCountInChapterPanel.Instance.SetValue(CountOptionsInChapterPanel.None);
            JumpCountInChapterPanel.Instance.SetValue(CountOptionsInChapterPanel.None);
            DashCountOnProgressPage.SetValue(CountOptionsInChapterPanel.None);
            DisplayDashCountInLevel.SetValue(ShowCountInGameOptions.None);
            DisplayJumpCountInLevel.SetValue(ShowCountInGameOptions.None);
            DoNotResetDashCountOnDeath.SetEnabled(false);
        }

        public override void Initialize() {
            base.Initialize();

            CountDreamDashRedirectsAsDashes.Initialize();
            DashCountOnProgressPage.Initialize();
            DashCountInChapterPanel.Instance.Initialize();
            JumpCountInChapterPanel.Instance.Initialize();
        }
    }
}
