namespace Celeste.Mod.DashCountMod {
    public class DashCountModSession : EverestModuleSession {
        public int JumpCount { get; set; } = 0;
        public int JumpCountAtLevelStart { get; set; } = 0;
    }
}
