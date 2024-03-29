﻿namespace Celeste.Mod.DashCountMod {
    public class DashCountModSettings : EverestModuleSettings {
        public enum DashCountOptions { None, Fewest, Total }
        public enum ShowDashCountInGameOptions { None, Session, Chapter, File, Both }

        private DashCountOptions dashCountInChapterPanel = DashCountOptions.None;
        private DashCountOptions dashCountOnProgressPage = DashCountOptions.None;
        private ShowDashCountInGameOptions showDashCountInGame = ShowDashCountInGameOptions.None;
        private bool countDreamDashRedirectsAsDashes = false;
        private bool doNotResetDashCountOnDeath = false;

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

        public ShowDashCountInGameOptions DashCountInGame {
            get { return showDashCountInGame; }
            set {
                showDashCountInGame = value;
                DashCountModModule.Instance.SetShowDashCountInGame(value);
            }
        }

        public bool CountDreamDashRedirectsAsDashes {
            get { return countDreamDashRedirectsAsDashes; }
            set {
                countDreamDashRedirectsAsDashes = value;
                DashCountModModule.Instance.SetCountDreamDashRedirectsAsDashes(value);
            }
        }

        public bool DoNotResetDashCountOnDeath {
            get { return doNotResetDashCountOnDeath; }
            set {
                doNotResetDashCountOnDeath = value;
                DashCountModModule.Instance.SetDoNotResetDashCountOnDeath(value);
            }
        }
    }
}
