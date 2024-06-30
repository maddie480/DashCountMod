﻿namespace Celeste.Mod.DashCountMod {
    public class DashCountModSettings : EverestModuleSettings {
        public enum CountOptionsInChapterPanel { None, Fewest, Total }
        public enum ShowCountInGameOptions { None, Session, Chapter, File, Both }

        private CountOptionsInChapterPanel dashCountInChapterPanel = CountOptionsInChapterPanel.None;
        private CountOptionsInChapterPanel dashCountOnProgressPage = CountOptionsInChapterPanel.None;
        private ShowCountInGameOptions showDashCountInGame = ShowCountInGameOptions.None;
        private bool countDreamDashRedirectsAsDashes = false;
        private bool doNotResetDashCountOnDeath = false;

        public CountOptionsInChapterPanel DashCountInChapterPanel {
            get { return dashCountInChapterPanel; }
            set {
                dashCountInChapterPanel = value;
                Features.DashCountInChapterPanel.Instance.SetValue(value);
            }
        }

        public CountOptionsInChapterPanel DashCountOnProgressPage {
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
