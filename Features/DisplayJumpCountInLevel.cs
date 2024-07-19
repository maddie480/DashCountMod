using Celeste.Mod.DashCountMod.UI;
using Monocle;
using static Celeste.Mod.DashCountMod.DashCountModSettings;

namespace Celeste.Mod.DashCountMod.Features {
    public static class DisplayJumpCountInLevel {
        private static ShowCountInGameOptions showJumpCountInGame = ShowCountInGameOptions.None;

        public static void SetValue(ShowCountInGameOptions value) {
            bool wasEnabled = (showJumpCountInGame != ShowCountInGameOptions.None);
            bool isEnabled = (value != ShowCountInGameOptions.None);

            // (un)hook methods
            if (isEnabled && !wasEnabled) {
                Logger.Log("DashCountMod", "Hooking level enter to add in-game jump counter");
                On.Celeste.Level.Begin += onLevelBegin;

            } else if (!isEnabled && wasEnabled) {
                Logger.Log("DashCountMod", "Unhooking level enter to stop adding in-game jump counter");
                On.Celeste.Level.Begin -= onLevelBegin;
            }

            // add/remove/update the jump count accordingly if we already are in a level.
            if (Engine.Scene is Level level) {
                JumpCountDisplayInLevel currentDisplay = level.Entities.FindFirst<JumpCountDisplayInLevel>();

                if (value == ShowCountInGameOptions.None) {
                    currentDisplay?.RemoveSelf();
                } else if (currentDisplay != null) {
                    currentDisplay.SetFormat(value);
                } else {
                    level.Add(new JumpCountDisplayInLevel(level.Session, value));
                }
            }

            showJumpCountInGame = value;
        }

        private static void onLevelBegin(On.Celeste.Level.orig_Begin orig, Level self) {
            orig(self);
            self.Add(new JumpCountDisplayInLevel(self.Session, showJumpCountInGame));
        }

    }
}
