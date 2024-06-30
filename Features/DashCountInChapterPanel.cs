using Celeste.Mod.DashCountMod.UI;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static Celeste.Mod.DashCountMod.DashCountModSettings;

namespace Celeste.Mod.DashCountMod.Features {
    public static class DashCountInChapterPanel {
        private static DashCountModSaveData ModSaveData {
            get {
                return (DashCountModSaveData) DashCountModModule.Instance._SaveData;
            }
        }

        private static FieldInfo speedBerryPBInChapterPanel;

        private static DashCountOptions dashCounterInChapterPanel = DashCountOptions.None;
        private static Hook collabUtilsHook = null;

        public static void Load() {
            On.Celeste.OuiChapterPanel.ctor += modOuiChapterPanelConstructor;
        }

        public static void Unload() {
            On.Celeste.OuiChapterPanel.ctor -= modOuiChapterPanelConstructor;
        }

        public static void Initialize() {
            EverestModule collabUtils = Everest.Modules.FirstOrDefault(module => module.Metadata.Name == "CollabUtils2");
            if (collabUtils != null) {
                speedBerryPBInChapterPanel = collabUtils.GetType().Assembly.GetType("Celeste.Mod.CollabUtils2.UI.SpeedBerryPBInChapterPanel")
                    .GetField("speedBerryPBDisplay", BindingFlags.NonPublic | BindingFlags.Static);

                Logger.Log("DashCountMod", $"I found the speed berry PB component: {speedBerryPBInChapterPanel.Name} " +
                    $"(type {speedBerryPBInChapterPanel.DeclaringType} in {speedBerryPBInChapterPanel.DeclaringType.Assembly})");

                // be sure other mods are hooked (they might not have been loaded when the dash count mod was loaded).

                if (((DashCountModSettings) DashCountModModule.Instance._Settings).DashCountInChapterPanel != DashCountOptions.None) {
                    hookCollabUtils();
                }
            }
        }

        public static void SetValue(DashCountOptions newValue) {
            bool wasEnabled = (dashCounterInChapterPanel != DashCountOptions.None);
            bool isEnabled = (newValue != DashCountOptions.None);

            // (un)hook methods
            if (isEnabled && !wasEnabled) {
                Logger.Log("DashCountMod", "Hooking chapter panel rendering methods");

                using (new DetourContext() { After = { "*" } }) { // be sure to apply _after_ the collab utils.
                    IL.Celeste.OuiChapterPanel.Render += modOuiChapterPanelRender;
                    On.Celeste.OuiChapterPanel.UpdateStats += modOuiChapterPanelUpdateStats;
                    IL.Celeste.OuiChapterPanel.SetStatsPosition += modOuiChapterPanelSetStatsPosition;
                    On.Celeste.OuiChapterPanel.IncrementStatsDisplay += modOuiChapterPanelIncrementStatsDisplay;
                    On.Celeste.OuiChapterPanel.GetModeHeight += modOuiChapterPanelGetModeHeight;
                }

                hookCollabUtils();
            } else if (!isEnabled && wasEnabled) {
                Logger.Log("DashCountMod", "Unhooking chapter panel rendering methods");

                IL.Celeste.OuiChapterPanel.Render -= modOuiChapterPanelRender;
                On.Celeste.OuiChapterPanel.UpdateStats -= modOuiChapterPanelUpdateStats;
                IL.Celeste.OuiChapterPanel.SetStatsPosition -= modOuiChapterPanelSetStatsPosition;
                On.Celeste.OuiChapterPanel.IncrementStatsDisplay -= modOuiChapterPanelIncrementStatsDisplay;
                On.Celeste.OuiChapterPanel.GetModeHeight -= modOuiChapterPanelGetModeHeight;

                collabUtilsHook?.Dispose();
                collabUtilsHook = null;

                // hide the dash counter if currently shown: as we unhooked everything updating it, it will stay invisible.
                dashesCounter.Visible = false;
            }

            dashCounterInChapterPanel = newValue;
        }

        private static void hookCollabUtils() {
            if (collabUtilsHook == null) {
                // is SpeedBerryPBInChapterPanel a thing?
                EverestModule collabUtils = Everest.Modules.FirstOrDefault(module => module.Metadata.Name == "CollabUtils2");

                if (collabUtils != null) {
                    collabUtilsHook = new Hook(
                        collabUtils.GetType().Assembly.GetType("Celeste.Mod.CollabUtils2.UI.SpeedBerryPBInChapterPanel").GetMethod("approach", BindingFlags.NonPublic | BindingFlags.Static),
                        typeof(DashCountInChapterPanel).GetMethod("modSpeedBerryPBApproach", BindingFlags.NonPublic | BindingFlags.Static));

                    Logger.Log("DashCountMod", "Collab utils speed berry PB counter was hooked");
                }
            }
        }

        private static Vector2 modSpeedBerryPBApproach(Func<Vector2, Vector2, bool, Vector2> orig, Vector2 from, Vector2 to, bool snap) {
            if ((dashesCounter?.Visible ?? false) && dashesOffset.Y != 160f) {
                to.Y -= 40f;
            }

            return orig(from, to, snap);
        }

        private static DashesCounterInChapterPanel dashesCounter;
        private static Vector2 dashesOffset;

        private static void modOuiChapterPanelConstructor(On.Celeste.OuiChapterPanel.orig_ctor orig, OuiChapterPanel self) {
            orig(self);

            // add the dashes counter as well, but have it hidden by default
            self.Add(dashesCounter = new DashesCounterInChapterPanel(true, 0));
            dashesCounter.CanWiggle = false;
            dashesCounter.Visible = false;
        }

        private static void modOuiChapterPanelRender(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // move after the deaths counter positioning, and place ourselves after that to update dashes counter position as well
            if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchStfld(typeof(DeathsCounter), "Position"))) {
                Logger.Log("DashCountMod", $"Injecting dashes counter position updating at {cursor.Index} in CIL code for OuiChapterPanel.Render");

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiChapterPanel).GetField("contentOffset", BindingFlags.NonPublic | BindingFlags.Instance));
                cursor.EmitDelegate<Action<Vector2>>(updateDashesCounterRenderedPosition);
            }
        }

        private static void updateDashesCounterRenderedPosition(Vector2 contentOffset) {
            dashesCounter.Position = contentOffset + new Vector2(0f, 170f) + dashesOffset;
        }

        private static int getDashCountForChapterPanel(AreaModeStats areaModeStats, AreaKey areaKey) {
            if (dashCounterInChapterPanel == DashCountOptions.Fewest) {
                return areaModeStats.BestDashes;
            }

            if (ModSaveData.DashCountPerLevel.TryGetValue(areaKey.GetSID(), out Dictionary<AreaMode, int> areaModes)) {
                if (areaModes.TryGetValue(areaKey.Mode, out int totalDashes)) {
                    return totalDashes;
                }
            }

            return 0;
        }

        private static void modOuiChapterPanelUpdateStats(On.Celeste.OuiChapterPanel.orig_UpdateStats orig, OuiChapterPanel self, bool wiggle, bool? overrideStrawberryWiggle, bool? overrideDeathWiggle, bool? overrideHeartWiggle) {
            orig(self, wiggle, overrideStrawberryWiggle, overrideDeathWiggle, overrideHeartWiggle);

            dashesCounter.Visible = self.DisplayedStats.Modes[(int) self.Area.Mode].SingleRunCompleted && !AreaData.Get(self.Area).Interlude;
            dashesCounter.Amount = getDashCountForChapterPanel(self.DisplayedStats.Modes[(int) self.Area.Mode], self.Area);

            if (dashCounterInChapterPanel == DashCountOptions.Total && self.DisplayedStats != self.RealStats) {
                // this is a sign that we are returning from a level, and we should display the old dash count so that it can animate to the new dash count.
                dashesCounter.Amount = ModSaveData.OldDashCount;
            }

            if (wiggle && dashesCounter.Visible && (overrideDeathWiggle ?? true)) {
                dashesCounter.Wiggle();
            }
        }

        private static void modOuiChapterPanelSetStatsPosition(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // this is a tricky one... in lines like this:
            // this.strawberriesOffset = this.Approach(this.strawberriesOffset, new Vector2(120f, (float)(this.deaths.Visible ? -40 : 0)), !approach);
            // we want to catch the result of (float)(this.deaths.Visible ? -40 : 0) and transform it to shift the things up if the dashes counter is there.
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchConvR4())) {
                Logger.Log("DashCountMod", $"Modifying strawberry/death counter positioning at {cursor.Index} in CIL code for OuiChapterPanel.SetStatsPosition");
                cursor.EmitDelegate<Func<float, float>>(shiftCountersPosition);
            }

            cursor.Index = 0;

            // we will cross 2 occurrences when deathsOffset will be set: first time with the heart, second time without.
            // the only difference is the X offset, so put the code in common.
            bool hasHeart = true;
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchStfld(typeof(OuiChapterPanel), "deathsOffset"))) {
                Logger.Log("DashCountMod", $"Injecting dashes counter position updating at {cursor.Index} in CIL code for OuiChapterPanel.SetStatsPosition (has heart = {hasHeart})");

                // bool approach
                cursor.Emit(OpCodes.Ldarg_1);
                // StrawberriesCounter strawberries
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiChapterPanel).GetField("strawberries", BindingFlags.NonPublic | BindingFlags.Instance));
                // DeathsCounter deaths
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiChapterPanel).GetField("deaths", BindingFlags.NonPublic | BindingFlags.Instance));
                // bool hasHeart
                cursor.Emit(hasHeart ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                // function call
                cursor.EmitDelegate<Action<bool, StrawberriesCounter, DeathsCounter, bool>>(updateDashesCounterOffset);

                hasHeart = false;
            }

            cursor.Index = 0;

            if (cursor.TryGotoNext(MoveType.After,
                instr => instr.MatchLdfld<OuiChapterPanel>("deaths"),
                instr => instr.MatchLdfld<Component>("Visible"))) {

                Logger.Log("DashCountMod", $"Patching chapter panel columns count at {cursor.Index} in CIL code for OuiChapterPanel.SetStatsPosition");
                cursor.EmitDelegate<Func<bool, bool>>(orig => orig || (dashesCounter?.Visible ?? false));
            }
        }

        private static float shiftCountersPosition(float position) {
            return dashesCounter.Visible && dashesOffset.Y != 160f ? position - 40 : position;
        }

        private static void updateDashesCounterOffset(bool approach, StrawberriesCounter strawberries, DeathsCounter deaths, bool hasHeart) {
            int shift = 0;
            if (strawberries.Visible) shift += 40;
            if (deaths.Visible) shift += 40;
            if (speedBerryPBInChapterPanel != null && speedBerryPBInChapterPanel.GetValue(null) is Component component && component.Visible) shift += 40;
            if (shift == 120f) shift += 40;
            dashesOffset = DashCountInChapterPanel.approach(dashesOffset, new Vector2(hasHeart ? 120f : 0f, shift), !approach);
        }

        // vanilla method copypaste
        private static Vector2 approach(Vector2 from, Vector2 to, bool snap) {
            if (snap) return to;
            return from + (to - from) * (1f - (float) Math.Pow(0.0010000000474974513, Engine.DeltaTime));
        }

        private static IEnumerator modOuiChapterPanelIncrementStatsDisplay(On.Celeste.OuiChapterPanel.orig_IncrementStatsDisplay orig, OuiChapterPanel self, AreaModeStats modeStats,
            AreaModeStats newModeStats, bool doHeartGem, bool doStrawberries, bool doDeaths, bool doRemixUnlock) {

            IEnumerator origMethod = orig(self, modeStats, newModeStats, doHeartGem, doStrawberries, doDeaths, doRemixUnlock);
            while (origMethod.MoveNext()) yield return origMethod.Current;

            int oldBestDashes = dashCounterInChapterPanel == DashCountOptions.Fewest ? modeStats.BestDashes : ModSaveData.OldDashCount;
            int newBestDashes = getDashCountForChapterPanel(newModeStats, self.Area);

            if (newModeStats.SingleRunCompleted && oldBestDashes != newBestDashes) {
                yield return 0.5f;

                Audio.Play("event:/ui/postgame/death_appear");
                dashesCounter.CanWiggle = true;
                dashesCounter.Visible = true;
                while (newBestDashes != oldBestDashes) {
                    int jumpSize;
                    yield return handleDashTick(oldBestDashes, newBestDashes, out jumpSize);
                    oldBestDashes += Math.Sign(newBestDashes - oldBestDashes) * jumpSize;
                    dashesCounter.Amount = oldBestDashes;
                    if (oldBestDashes == newBestDashes) {
                        Audio.Play("event:/ui/postgame/death_final");
                    } else {
                        Audio.Play("event:/ui/postgame/death_count");
                    }
                    Input.Rumble(RumbleStrength.Light, RumbleLength.Short);
                }
                dashesCounter.CanWiggle = false;

                yield return 0.8f;
            }
        }

        private static int modOuiChapterPanelGetModeHeight(On.Celeste.OuiChapterPanel.orig_GetModeHeight orig, OuiChapterPanel self) {
            int origModeHeight = orig(self);
            if (origModeHeight == 540 && dashesOffset.Y == 160f) {
                return 610;
            }
            return origModeHeight;
        }

        // nearly another vanilla method copypaste
        private static float handleDashTick(int oldDashes, int newDashes, out int jumpSize) {
            int difference = Math.Abs(newDashes - oldDashes);
            if (difference < 3) {
                jumpSize = 1;
                return 0.3f;
            }
            if (difference < 8) {
                jumpSize = 2;
                return 0.2f;
            }
            if (difference < 30) {
                jumpSize = 5;
                return 0.1f;
            }
            if (difference < 100) {
                jumpSize = 10;
                return 0.1f;
            }
            if (difference < 1000) {
                jumpSize = 25;
                return 0.1f;
            }
            jumpSize = 100;
            return 0.1f;
        }
    }
}
