using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.DashCountMod.Features {
    public static class CustomDashCounting {
        public static void Load() {
            IL.Celeste.Player.CallDashEvents += countDashes;
            On.Celeste.Session.ctor_AreaKey_string_AreaStats += saveOldDashCount;
        }

        public static void Unload() {
            IL.Celeste.Player.CallDashEvents -= countDashes;
            On.Celeste.Session.ctor_AreaKey_string_AreaStats -= saveOldDashCount;
        }

        private static DashCountModSaveData ModSaveData {
            get {
                return (DashCountModSaveData) DashCountModModule.Instance._SaveData;
            }
        }

        public static void AddDash(Entity entityInScene) {
            if (entityInScene.Scene != null) {
                AreaKey area = entityInScene.SceneAs<Level>().Session.Area;

                if (ModSaveData.DashCountPerLevel.TryGetValue(area.GetSID(), out Dictionary<AreaMode, int> dashCounts)) {
                    if (dashCounts.TryGetValue(area.Mode, out int _)) {
                        // area and mode stats exist, we should increment it
                        dashCounts[area.Mode]++;
                    } else {
                        // area stats exist, mode stats don't
                        dashCounts[area.Mode] = 1;
                    }
                } else {
                    // area stats don't exist, create them
                    Dictionary<AreaMode, int> areaStats = new Dictionary<AreaMode, int> {
                        [area.Mode] = 1
                    };
                    ModSaveData.DashCountPerLevel[area.GetSID()] = areaStats;
                }
            }
        }

        private static void countDashes(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            if (cursor.TryGotoNext(instr => instr.MatchLdfld<SaveData>("TotalDashes"))) {
                // this is the place where vanilla increments the TotalDashes count in the save file: increment our own dash count as well.
                Logger.Log("DashCountMod", $"Adding code to count dashes at {cursor.Index} in IL for Player.CallDashEvents()");

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Action<Player>>(AddDash);
            }
        }

        private static void saveOldDashCount(On.Celeste.Session.orig_ctor_AreaKey_string_AreaStats orig, Session self, AreaKey area, string checkpoint, AreaStats oldStats) {
            orig(self, area, checkpoint, oldStats);

            if (oldStats == null) {
                int oldDashCount = 0;

                if (ModSaveData.DashCountPerLevel.TryGetValue(area.GetSID(), out Dictionary<AreaMode, int> areaModes)) {
                    if (areaModes.TryGetValue(area.Mode, out int totalDashes)) {
                        oldDashCount = totalDashes;
                    }
                }

                ModSaveData.OldDashCount = oldDashCount;
            }
        }
    }
}
