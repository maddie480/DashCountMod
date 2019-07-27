using System;

namespace Celeste.Mod.DashCountMod {
    public class DashCountModModule : EverestModule {

        public static DashCountModModule Instance;

        public override Type SettingsType => null;

        public DashCountModModule() {
            Instance = this;
        }

        // ================ Module loading ================

        public override void Load() {
            // mod methods here
            Everest.Events.Journal.OnEnter += OnJournalEnter;
        }

        public override void Unload() {
            // unmod methods here
            Everest.Events.Journal.OnEnter -= OnJournalEnter;
        }

        private void OnJournalEnter(OuiJournal journal, Oui from) {
            // add the "dashes" page just after the "deaths" one
            for(int i = 0; i < journal.Pages.Count; i++) {
                if(journal.Pages[i].GetType() == typeof(OuiJournalDeaths)) {
                    journal.Pages.Insert(i + 1, new OuiJournalDashes(journal));
                }
            }
        }
    }
}
