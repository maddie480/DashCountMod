namespace Celeste.Mod.DashCountMod {
    public class DashCountModSettings : EverestModuleSettings {
        public enum ShowDashCountInGameOptions { None, Chapter, File, Both }

        private bool dashCountInChapterPanel = false;
        private bool fewestDashCountOnProgressPage = false;
        private ShowDashCountInGameOptions showDashCountInGame = ShowDashCountInGameOptions.None;

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

        public ShowDashCountInGameOptions ShowFewestDashCountInGame {
            get { return showDashCountInGame; }
            set {
                showDashCountInGame = value;
                DashCountModModule.Instance.SetShowDashCountInGame(value);
            }
        }
    }
}
