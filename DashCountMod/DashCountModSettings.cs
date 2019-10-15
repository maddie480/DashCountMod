namespace Celeste.Mod.DashCountMod {
    class DashCountModSettings : EverestModuleSettings {
        private bool dashCountInChapterPanel = false;
        private bool fewestDashCountOnProgressPage = false;

        [SettingInGame(false)]
        public bool DashCountInChapterPanel {
            get { return dashCountInChapterPanel; }
            set {
                dashCountInChapterPanel = value;
                DashCountModModule.Instance.SetDashCounterInChapterPanelEnabled(value);
            }
        }

        [SettingInGame(false)]
        public bool FewestDashCountOnProgressPage {
            get { return fewestDashCountOnProgressPage; }
            set {
                fewestDashCountOnProgressPage = value;
                DashCountModModule.Instance.SetFewestDashesInProgressPageEnabled(value);
            }
        }
    }
}
