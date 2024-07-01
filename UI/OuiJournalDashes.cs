namespace Celeste.Mod.DashCountMod.UI {
    public class OuiJournalDashes : AbstractOuiJournal {
        public OuiJournalDashes(OuiJournal journal) : base(journal, "journal_dashes") { }

        protected override int GetBestCountFor(AreaKey areaKey) {
            return getAreaStatsForIncludingCeleste(areaKey).Modes[(int) areaKey.Mode].BestDashes;
        }

        // behold: the exact same as SaveDataExt.GetAreaStatsFor except it includes vanilla levels!
        private static AreaStats getAreaStatsForIncludingCeleste(AreaKey key) {
            return SaveData.Instance
                .LevelSets.Find(set => set.Name == key.GetLevelSet())
                .AreasIncludingCeleste.Find(area => area.SID == key.GetSID());
        }
    }
}
