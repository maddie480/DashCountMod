using Celeste.Mod.DashCountMod.UI;

namespace Celeste.Mod.DashCountMod.Features {
    public static class JumpCountJournalPage {
        public static void Load() {
            Everest.Events.Journal.OnEnter += onJournalEnter;
        }

        public static void Unload() {
            Everest.Events.Journal.OnEnter -= onJournalEnter;
        }

        private static void onJournalEnter(OuiJournal journal, Oui from) {
            // add the "jumps" page just after the "deaths" one
            for (int i = 0; i < journal.Pages.Count; i++) {
                if (journal.Pages[i].GetType() == typeof(OuiJournalDeaths)) {
                    journal.Pages.Insert(i + 1, new OuiJournalJumps(journal));
                }
            }
        }

    }
}
