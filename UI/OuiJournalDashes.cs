using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.DashCountMod.UI {
    public class OuiJournalDashes : AbstractOuiJournal {
        public OuiJournalDashes(OuiJournal journal) : base(journal, "journal_dashes") { }

        protected override int GetBestCountFor(AreaData areaData, int mode) {
            return SaveData.Instance.GetAreaStatsFor(areaData.ToKey()).Modes[mode].BestDashes;
        }
    }
}
