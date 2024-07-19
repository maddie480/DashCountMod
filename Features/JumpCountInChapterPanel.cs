using Celeste.Mod.DashCountMod.UI;
using System.Linq;
using static Celeste.Mod.DashCountMod.DashCountModSettings;

namespace Celeste.Mod.DashCountMod.Features {
    public class JumpCountInChapterPanel : AbstractCountInChapterPanel<JumpsCounterInChapterPanel> {
        public static JumpCountInChapterPanel Instance { get; } = new JumpCountInChapterPanel();

        protected override JumpsCounterInChapterPanel NewCounter() {
            return new JumpsCounterInChapterPanel(true, 0);
        }
        protected override int GetFewestCount(AreaKey areaKey) {
            return GetCountFromSaveData(((DashCountModSaveData) DashCountModModule.Instance._SaveData).BestJumpCountPerLevel, areaKey);
        }
        protected override int GetTotalCount(AreaKey areaKey) {
            return GetCountFromSaveData(((DashCountModSaveData) DashCountModModule.Instance._SaveData).JumpCountPerLevel, areaKey);
        }
        protected override int GetOldCount() {
            return ((DashCountModSaveData) DashCountModModule.Instance._SaveData).OldJumpCount;
        }
        protected override CountOptionsInChapterPanel GetOptionValue() {
            return ((DashCountModSettings) DashCountModModule.Instance._Settings).JumpCountInChapterPanel;
        }
        protected override int GetExtraOffset() {
            if (Scene?.Tracker.GetComponents<DashesCounterInChapterPanel>().Any(counter => counter.Visible) ?? false) {
                return 60;
            } else {
                return 0;
            }
        }
    }
}
