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
    public abstract class AbstractCountInChapterPanel<TCounter> where TCounter : DeathsCounter {
        private FieldInfo speedBerryPBInChapterPanel;

        private CountOptionsInChapterPanel counterInChapterPanel = CountOptionsInChapterPanel.None;
        private Hook collabUtilsHook = null;

        public void Load() {
            On.Celeste.OuiChapterPanel.ctor += modOuiChapterPanelConstructor;
        }

        public void Unload() {
            On.Celeste.OuiChapterPanel.ctor -= modOuiChapterPanelConstructor;
        }

        public void Initialize() {
            EverestModule collabUtils = Everest.Modules.FirstOrDefault(module => module.Metadata.Name == "CollabUtils2");
            if (collabUtils != null) {
                speedBerryPBInChapterPanel = collabUtils.GetType().Assembly.GetType("Celeste.Mod.CollabUtils2.UI.SpeedBerryPBInChapterPanel")
                    .GetField("speedBerryPBDisplay", BindingFlags.NonPublic | BindingFlags.Static);

                Logger.Log("DashCountMod", $"I found the speed berry PB component: {speedBerryPBInChapterPanel.Name} " +
                    $"(type {speedBerryPBInChapterPanel.DeclaringType} in {speedBerryPBInChapterPanel.DeclaringType.Assembly})");

                // be sure other mods are hooked (they might not have been loaded when the dash count mod was loaded).

                if (GetOptionValue() != CountOptionsInChapterPanel.None) {
                    hookCollabUtils();
                }
            }
        }

        public void SetValue(CountOptionsInChapterPanel newValue) {
            bool wasEnabled = (counterInChapterPanel != CountOptionsInChapterPanel.None);
            bool isEnabled = (newValue != CountOptionsInChapterPanel.None);

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

                // hide the counter if currently shown: as we unhooked everything updating it, it will stay invisible.
                counter.Visible = false;
            }

            counterInChapterPanel = newValue;
        }

        private void hookCollabUtils() {
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

        private Vector2 modSpeedBerryPBApproach(Func<Vector2, Vector2, bool, Vector2> orig, Vector2 from, Vector2 to, bool snap) {
            if ((counter?.Visible ?? false) && counterOffset.Y < 160f) {
                to.Y -= 40f;
            }

            return orig(from, to, snap);
        }

        private TCounter counter;
        private Vector2 counterOffset;

        private void modOuiChapterPanelConstructor(On.Celeste.OuiChapterPanel.orig_ctor orig, OuiChapterPanel self) {
            orig(self);

            // add the counter as well, but have it hidden by default
            self.Add(counter = NewCounter());
            counter.CanWiggle = false;
            counter.Visible = false;
        }

        private void modOuiChapterPanelRender(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // move after the deaths counter positioning, and place ourselves after that to update counter position as well
            if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchStfld(typeof(DeathsCounter), "Position"))) {
                Logger.Log("DashCountMod", $"Injecting counter position updating at {cursor.Index} in CIL code for OuiChapterPanel.Render");

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiChapterPanel).GetField("contentOffset", BindingFlags.NonPublic | BindingFlags.Instance));
                cursor.EmitDelegate<Action<Vector2>>(updateCounterRenderedPosition);
            }
        }

        private void updateCounterRenderedPosition(Vector2 contentOffset) {
            counter.Position = contentOffset + new Vector2(0f, 170f) + counterOffset;
        }

        private int getCountForChapterPanel(AreaKey areaKey) {
            if (counterInChapterPanel == CountOptionsInChapterPanel.Fewest) {
                return GetFewestCount(areaKey);
            }

            return GetTotalCount(areaKey);
        }

        private void modOuiChapterPanelUpdateStats(On.Celeste.OuiChapterPanel.orig_UpdateStats orig, OuiChapterPanel self, bool wiggle, bool? overrideStrawberryWiggle, bool? overrideDeathWiggle, bool? overrideHeartWiggle) {
            orig(self, wiggle, overrideStrawberryWiggle, overrideDeathWiggle, overrideHeartWiggle);

            counter.Visible = self.DisplayedStats.Modes[(int) self.Area.Mode].SingleRunCompleted && !AreaData.Get(self.Area).Interlude;
            counter.Amount = getCountForChapterPanel(self.Area);

            if (counterInChapterPanel == CountOptionsInChapterPanel.Total && self.DisplayedStats != self.RealStats) {
                // this is a sign that we are returning from a level, and we should display the old count so that it can animate to the new count.
                counter.Amount = GetOldCount();
            }

            if (wiggle && counter.Visible && (overrideDeathWiggle ?? true)) {
                counter.Wiggle();
            }
        }

        private void modOuiChapterPanelSetStatsPosition(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // this is a tricky one... in lines like this:
            // this.strawberriesOffset = this.Approach(this.strawberriesOffset, new Vector2(120f, (float)(this.deaths.Visible ? -40 : 0)), !approach);
            // we want to catch the result of (float)(this.deaths.Visible ? -40 : 0) and transform it to shift the things up if the counter is there.
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchConvR4())) {
                Logger.Log("DashCountMod", $"Modifying strawberry/death counter positioning at {cursor.Index} in CIL code for OuiChapterPanel.SetStatsPosition");
                cursor.EmitDelegate<Func<float, float>>(shiftCountersPosition);
            }

            cursor.Index = 0;

            // we will cross 2 occurrences when deathsOffset will be set: first time with the heart, second time without.
            // the only difference is the X offset, so put the code in common.
            bool hasHeart = true;
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchStfld(typeof(OuiChapterPanel), "deathsOffset"))) {
                Logger.Log("DashCountMod", $"Injecting counter position updating at {cursor.Index} in CIL code for OuiChapterPanel.SetStatsPosition (has heart = {hasHeart})");

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
                cursor.EmitDelegate<Action<bool, StrawberriesCounter, DeathsCounter, bool>>(updateCounterOffset);

                hasHeart = false;
            }

            cursor.Index = 0;

            if (cursor.TryGotoNext(MoveType.After,
                instr => instr.MatchLdfld<OuiChapterPanel>("deaths"),
                instr => instr.MatchLdfld<Component>("Visible"))) {

                Logger.Log("DashCountMod", $"Patching chapter panel columns count at {cursor.Index} in CIL code for OuiChapterPanel.SetStatsPosition");
                cursor.EmitDelegate<Func<bool, bool>>(orig => orig || (counter?.Visible ?? false));
            }
        }

        private float shiftCountersPosition(float position) {
            return counter.Visible && counterOffset.Y < 160f ? position - 40 : position;
        }

        private void updateCounterOffset(bool doApproach, StrawberriesCounter strawberries, DeathsCounter deaths, bool hasHeart) {
            int shift = 0;
            if (strawberries.Visible) shift += 40;
            if (deaths.Visible) shift += 40;
            if (speedBerryPBInChapterPanel != null && speedBerryPBInChapterPanel.GetValue(null) is Component component && component.Visible) shift += 40;
            shift += GetExtraOffset();
            if (shift >= 120f) shift += 40;
            counterOffset = approach(counterOffset, new Vector2(hasHeart ? 120f : 0f, shift), !doApproach);
        }

        // vanilla method copypaste
        private Vector2 approach(Vector2 from, Vector2 to, bool snap) {
            if (snap) return to;
            return from + (to - from) * (1f - (float) Math.Pow(0.0010000000474974513, Engine.DeltaTime));
        }

        private IEnumerator modOuiChapterPanelIncrementStatsDisplay(On.Celeste.OuiChapterPanel.orig_IncrementStatsDisplay orig, OuiChapterPanel self, AreaModeStats modeStats,
            AreaModeStats newModeStats, bool doHeartGem, bool doStrawberries, bool doDeaths, bool doRemixUnlock) {

            IEnumerator origMethod = orig(self, modeStats, newModeStats, doHeartGem, doStrawberries, doDeaths, doRemixUnlock);
            while (origMethod.MoveNext()) yield return origMethod.Current;

            int oldBest = counterInChapterPanel == CountOptionsInChapterPanel.Fewest ? GetFewestCount(self.Area) : GetOldCount();
            int newBest = getCountForChapterPanel(self.Area);

            if (newModeStats.SingleRunCompleted && oldBest != newBest) {
                yield return 0.5f;

                Audio.Play("event:/ui/postgame/death_appear");
                counter.CanWiggle = true;
                counter.Visible = true;
                while (newBest != oldBest) {
                    int jumpSize;
                    yield return handleTick(oldBest, newBest, out jumpSize);
                    oldBest += Math.Sign(newBest - oldBest) * jumpSize;
                    counter.Amount = oldBest;
                    if (oldBest == newBest) {
                        Audio.Play("event:/ui/postgame/death_final");
                    } else {
                        Audio.Play("event:/ui/postgame/death_count");
                    }
                    Input.Rumble(RumbleStrength.Light, RumbleLength.Short);
                }
                counter.CanWiggle = false;

                yield return 0.8f;
            }
        }

        private int modOuiChapterPanelGetModeHeight(On.Celeste.OuiChapterPanel.orig_GetModeHeight orig, OuiChapterPanel self) {
            int origModeHeight = orig(self);
            if (origModeHeight == 540 && counterOffset.Y >= 160f) {
                return 610;
            }
            return origModeHeight;
        }

        // nearly another vanilla method copypaste
        private float handleTick(int oldDashes, int newDashes, out int jumpSize) {
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

        protected int GetCountFromSaveData(Dictionary<string, Dictionary<AreaMode, int>> saveDataMap, AreaKey area) {
            if (saveDataMap.TryGetValue(area.GetSID(), out Dictionary<AreaMode, int> areaModes)) {
                if (areaModes.TryGetValue(area.Mode, out int count)) {
                    return count;
                }
            }

            return 0;
        }

        protected abstract TCounter NewCounter();
        protected abstract int GetFewestCount(AreaKey areaKey);
        protected abstract int GetTotalCount(AreaKey areaKey);
        protected abstract int GetOldCount();
        protected abstract CountOptionsInChapterPanel GetOptionValue();
        protected abstract int GetExtraOffset();
    }
}
