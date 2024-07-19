using Monocle;
using System.Collections.Generic;

namespace Celeste.Mod.DashCountMod.Features {
    public static class CustomJumpCounting {
        public static void Load() {
            On.Celeste.Player.Jump += countJumps;
            On.Celeste.Player.SuperJump += countSuperJumps;

            On.Celeste.Session.ctor_AreaKey_string_AreaStats += saveOldJumpCount;

            On.Celeste.Level.Reload += onLevelReload;
            On.Celeste.Session.UpdateLevelStartDashes += onUpdateLevelStartDashes;
            On.Celeste.SaveData.RegisterCompletion += onSaveDataRegisterCompletion;
        }

        public static void Unload() {
            On.Celeste.Player.Jump -= countJumps;
            On.Celeste.Player.SuperJump -= countSuperJumps;

            On.Celeste.Session.ctor_AreaKey_string_AreaStats -= saveOldJumpCount;

            On.Celeste.Level.Reload -= onLevelReload;
            On.Celeste.Session.UpdateLevelStartDashes -= onUpdateLevelStartDashes;
            On.Celeste.SaveData.RegisterCompletion -= onSaveDataRegisterCompletion;
        }

        private static DashCountModSaveData ModSaveData {
            get {
                return (DashCountModSaveData) DashCountModModule.Instance._SaveData;
            }
        }

        private static DashCountModSession ModSession {
            get {
                return (DashCountModSession) DashCountModModule.Instance._Session;
            }
        }

        public static void AddJump(Entity entityInScene) {
            ModSession.JumpCount++;

            if (entityInScene.Scene != null) {
                AreaKey area = entityInScene.SceneAs<Level>().Session.Area;

                if (ModSaveData.JumpCountPerLevel.TryGetValue(area.GetSID(), out Dictionary<AreaMode, int> jumpCounts)) {
                    if (jumpCounts.TryGetValue(area.Mode, out int _)) {
                        // area and mode stats exist, we should increment it
                        jumpCounts[area.Mode]++;
                    } else {
                        // area stats exist, mode stats don't
                        jumpCounts[area.Mode] = 1;
                    }
                } else {
                    // area stats don't exist, create them
                    Dictionary<AreaMode, int> areaStats = new Dictionary<AreaMode, int> {
                        [area.Mode] = 1
                    };
                    ModSaveData.JumpCountPerLevel[area.GetSID()] = areaStats;
                }
            }
        }

        private static void countJumps(On.Celeste.Player.orig_Jump orig, Player self, bool particles, bool playsfx) {
            orig(self, particles, playsfx);
            AddJump(self);
        }

        private static void countSuperJumps(On.Celeste.Player.orig_SuperJump orig, Player self) {
            orig(self);
            AddJump(self);
        }

        private static void saveOldJumpCount(On.Celeste.Session.orig_ctor_AreaKey_string_AreaStats orig, Session self, AreaKey area, string checkpoint, AreaStats oldStats) {
            orig(self, area, checkpoint, oldStats);

            if (oldStats == null) {
                int oldJumpCount = 0;

                if (ModSaveData.JumpCountPerLevel.TryGetValue(area.GetSID(), out Dictionary<AreaMode, int> areaModes)) {
                    if (areaModes.TryGetValue(area.Mode, out int totalJumps)) {
                        oldJumpCount = totalJumps;
                    }
                }

                ModSaveData.OldJumpCount = oldJumpCount;
            }
        }

        private static void onLevelReload(On.Celeste.Level.orig_Reload orig, Level self) {
            if (!((DashCountModSettings) DashCountModModule.Instance._Settings).DoNotResetJumpCountOnDeath) {
                ModSession.JumpCount = ModSession.JumpCountAtLevelStart;
            }
            orig(self);
        }

        private static void onUpdateLevelStartDashes(On.Celeste.Session.orig_UpdateLevelStartDashes orig, Session self) {
            orig(self);
            ModSession.JumpCountAtLevelStart = ModSession.JumpCount;
        }

        private static void onSaveDataRegisterCompletion(On.Celeste.SaveData.orig_RegisterCompletion orig, SaveData self, Session session) {
            orig(self, session);
            if (!session.StartedFromBeginning) return;

            int? oldJumpCount = null;

            if (ModSaveData.BestJumpCountPerLevel.TryGetValue(session.Area.GetSID(), out Dictionary<AreaMode, int> jumpCounts)) {
                if (jumpCounts.TryGetValue(session.Area.Mode, out int _)) {
                    // area and mode stats exist
                    oldJumpCount = jumpCounts[session.Area.Mode]++;
                }
            } else {
                // area stats don't exist, create them
                ModSaveData.BestJumpCountPerLevel[session.Area.GetSID()] = new Dictionary<AreaMode, int>();
            }

            if (oldJumpCount == null || oldJumpCount > ModSession.JumpCount) {
                // create or update the best jump count
                ModSaveData.BestJumpCountPerLevel[session.Area.GetSID()][session.Area.Mode] = ModSession.JumpCount;
            }
        }
    }
}
