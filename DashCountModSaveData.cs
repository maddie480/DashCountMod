using System.Collections.Generic;

namespace Celeste.Mod.DashCountMod {
    public class DashCountModSaveData : EverestModuleSaveData {
        public Dictionary<string, Dictionary<AreaMode, int>> DashCountPerLevel { get; set; } = new Dictionary<string, Dictionary<AreaMode, int>>();
        public int OldDashCount { get; set; } = 0;
    }
}
