namespace Celeste.Mod.DashCountMod.UI {
    public class OuiJournalDashes : AbstractOuiJournal {
        public OuiJournalDashes(OuiJournal journal) : base(journal, "journal_dashes") { }

        protected override int GetBestCountFor(AreaKey areaKey) {
            return SaveData.Instance.GetAreaStatsForIncludingCeleste(areaKey).Modes[(int) areaKey.Mode].BestDashes;
        }
    }
}
