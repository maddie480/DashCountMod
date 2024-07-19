using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.DashCountMod.UI {
    public class JumpCountDisplayInLevel : AbstractCountDisplayInLevel {
        public JumpCountDisplayInLevel(Session session, DashCountModSettings.ShowCountInGameOptions format)
            : base(session, format, GFX.Gui["collectables/jumps"], -10f) {
        }

        protected override int GetCountForSession() {
            return ((DashCountModSession) DashCountModModule.Instance._Session).JumpCount;
        }

        protected override int GetCountForChapter() {
            return GetCountFromSaveData(ModSaveData.JumpCountPerLevel);
        }
        protected override int GetCountForFile() {
            return ModSaveData.JumpCountPerLevel.Values
                .Select(level => level.Values.Sum())
                .Sum();
        }
        protected override IEnumerator<AbstractCountDisplayInLevel> EnumeratePreviousCountDisplays() {
            foreach (DashCountDisplayInLevel e in Scene.Tracker.GetEntities<DashCountDisplayInLevel>().OfType<DashCountDisplayInLevel>()) {
                yield return e;
            }
        }
    }
}
