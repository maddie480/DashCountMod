using Monocle;

namespace Celeste.Mod.DashCountMod.Features {
    public static class DoNotResetDashCountOnDeath {
        private static bool dashCountOnDeathHooked = false;

        public static void SetEnabled(bool doNotResetDashCountOnDeath) {
            if (doNotResetDashCountOnDeath && !dashCountOnDeathHooked) {
                On.Celeste.Player.CallDashEvents += onCallDashEvents;
                dashCountOnDeathHooked = true;
            } else if (!doNotResetDashCountOnDeath && dashCountOnDeathHooked) {
                On.Celeste.Player.CallDashEvents -= onCallDashEvents;
                dashCountOnDeathHooked = false;
            }
        }

        private static void onCallDashEvents(On.Celeste.Player.orig_CallDashEvents orig, Player self) {
            orig(self);
            (Engine.Scene as Level)?.Session.UpdateLevelStartDashes();
        }
    }
}
