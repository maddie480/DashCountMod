using Monocle;
using System.Collections.Generic;

namespace Celeste.Mod.DashCountMod.UI {
    [Tracked]
    public class DashCountDisplayInLevel : AbstractCountDisplayInLevel {
        public DashCountDisplayInLevel(Session session, DashCountModSettings.ShowCountInGameOptions format)
            : base(session, format, GFX.Gui["collectables/dashes"], 0f) {
        }

        protected override int GetCountForSession() {
            return session.Dashes;
        }

        protected override int GetCountForChapter() {
            return GetCountFromSaveData(ModSaveData.DashCountPerLevel);
        }
        protected override int GetCountForFile() {
            return SaveData.Instance.TotalDashes;
        }

        protected override IEnumerator<AbstractCountDisplayInLevel> EnumeratePreviousCountDisplays() {
            yield break;
        }
    }
}
