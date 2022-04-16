﻿namespace Celeste.Mod.DashCountMod {
    public class DashCountModSettings : EverestModuleSettings {
        public enum DashCountOptions { None, Fewest, Total }
        public enum ShowDashCountInGameOptions { None, Session, Chapter, File, Both }

        private DashCountOptions dashCountInChapterPanel = DashCountOptions.None;
        private DashCountOptions dashCountOnProgressPage = DashCountOptions.None;
        private ShowDashCountInGameOptions showDashCountInGame = ShowDashCountInGameOptions.None;

        public DashCountOptions DashCountInChapterPanel {
            get { return dashCountInChapterPanel; }
            set {
                dashCountInChapterPanel = value;
                DashCountModModule.Instance.SetDashCounterInChapterPanel(value);
            }
        }

        public DashCountOptions DashCountOnProgressPage {
            get { return dashCountOnProgressPage; }
            set {
                dashCountOnProgressPage = value;
                DashCountModModule.Instance.SetFewestDashesInProgressPage(value);
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
