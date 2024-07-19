using System.Collections.Generic;

namespace Celeste.Mod.DashCountMod.UI {
    public class OuiJournalJumps : AbstractOuiJournal {
        public OuiJournalJumps(OuiJournal journal) : base(journal, "journal_jumps") { }

        protected override int GetBestCountFor(AreaKey areaKey) {
            if (((DashCountModSaveData) DashCountModModule.Instance._SaveData).BestJumpCountPerLevel
                .TryGetValue(areaKey.GetSID(), out Dictionary<AreaMode, int> areaModes)) {

                if (areaModes.TryGetValue(areaKey.Mode, out int count)) {
                    return count;
                }
            }

            return 0;
        }
    }
}
