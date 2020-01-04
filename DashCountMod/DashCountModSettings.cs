namespace Celeste.Mod.DashCountMod {
    [SettingInGame(false)]
    class DashCountModSettings : EverestModuleSettings {
        private bool dashCountInChapterPanel = false;
        private bool fewestDashCountOnProgressPage = false;

        public bool DashCountInChapterPanel {
            get { return dashCountInChapterPanel; }
            set {
                dashCountInChapterPanel = value;
                DashCountModModule.Instance.SetDashCounterInChapterPanelEnabled(value);
            }
        }

        public bool FewestDashCountOnProgressPage {
            get { return fewestDashCountOnProgressPage; }
            set {
                fewestDashCountOnProgressPage = value;
                DashCountModModule.Instance.SetFewestDashesInProgressPageEnabled(value);
            }
        }
    }
}
