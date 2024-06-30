namespace Celeste.Mod.DashCountMod {
    public class DashCountModSettings : EverestModuleSettings {
        public enum DashCountOptions { None, Fewest, Total }
        public enum ShowCountInGameOptions { None, Session, Chapter, File, Both }

        private DashCountOptions dashCountInChapterPanel = DashCountOptions.None;
        private DashCountOptions dashCountOnProgressPage = DashCountOptions.None;
        private ShowCountInGameOptions showDashCountInGame = ShowCountInGameOptions.None;
        private bool countDreamDashRedirectsAsDashes = false;
        private bool doNotResetDashCountOnDeath = false;

        public DashCountOptions DashCountInChapterPanel {
            get { return dashCountInChapterPanel; }
            set {
                dashCountInChapterPanel = value;
                Features.DashCountInChapterPanel.SetValue(value);
            }
        }

        public DashCountOptions DashCountOnProgressPage {
            get { return dashCountOnProgressPage; }
            set {
                dashCountOnProgressPage = value;
                Features.DashCountOnProgressPage.SetValue(value);
            }
        }

        public ShowCountInGameOptions DashCountInGame {
            get { return showDashCountInGame; }
            set {
                showDashCountInGame = value;
                Features.DisplayDashCountInLevel.SetValue(value);
            }
        }

        public bool CountDreamDashRedirectsAsDashes {
            get { return countDreamDashRedirectsAsDashes; }
            set {
                countDreamDashRedirectsAsDashes = value;
                Features.CountDreamDashRedirectsAsDashes.SetEnabled(value);
            }
        }

        public bool DoNotResetDashCountOnDeath {
            get { return doNotResetDashCountOnDeath; }
            set {
                doNotResetDashCountOnDeath = value;
                Features.DoNotResetDashCountOnDeath.SetEnabled(value);
            }
        }
    }
}
