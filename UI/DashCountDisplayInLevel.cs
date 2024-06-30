using System.Collections.Generic;

namespace Celeste.Mod.DashCountMod.UI {
    public class DashCountDisplayInLevel : AbstractCountDisplayInLevel {
        public DashCountDisplayInLevel(Session session, DashCountModSettings.ShowCountInGameOptions format)
            : base(session, format, GFX.Gui["collectables/dashes"], new List<AbstractCountDisplayInLevel>()) {
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
    }
}
