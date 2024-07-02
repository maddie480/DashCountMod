using Celeste.Mod.DashCountMod.UI;
using static Celeste.Mod.DashCountMod.DashCountModSettings;

namespace Celeste.Mod.DashCountMod.Features {
    public class DashCountInChapterPanel : AbstractCountInChapterPanel<DashesCounterInChapterPanel> {
        public static DashCountInChapterPanel Instance { get; } = new DashCountInChapterPanel();

        protected override DashesCounterInChapterPanel NewCounter() {
            return new DashesCounterInChapterPanel(true, 0);
        }
        protected override int GetFewestCount(AreaKey areaKey) {
            return SaveData.Instance.GetAreaStatsForIncludingCeleste(areaKey).Modes[(int) areaKey.Mode].BestDashes;
        }
        protected override int GetTotalCount(AreaKey areaKey) {
            return GetCountFromSaveData(((DashCountModSaveData) DashCountModModule.Instance._SaveData).DashCountPerLevel, areaKey);
        }
        protected override int GetOldCount() {
            return ((DashCountModSaveData) DashCountModModule.Instance._SaveData).OldDashCount;
        }
        protected override CountOptionsInChapterPanel GetOptionValue() {
            return ((DashCountModSettings) DashCountModModule.Instance._Settings).DashCountInChapterPanel;
        }
        protected override int GetExtraOffset() {
            return 0;
        }
    }
}
