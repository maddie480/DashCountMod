using System.Collections.Generic;

namespace Celeste.Mod.DashCountMod {
    public class DashCountModSaveData : EverestModuleSaveData {
        public Dictionary<string, Dictionary<AreaMode, int>> DashCountPerLevel { get; set; } = new Dictionary<string, Dictionary<AreaMode, int>>();
        public Dictionary<string, Dictionary<AreaMode, int>> JumpCountPerLevel { get; set; } = new Dictionary<string, Dictionary<AreaMode, int>>();
        public Dictionary<string, Dictionary<AreaMode, int>> BestJumpCountPerLevel { get; set; } = new Dictionary<string, Dictionary<AreaMode, int>>();

        public int OldDashCount { get; set; } = 0;
        public int OldJumpCount { get; set; } = 0;
    }
}
