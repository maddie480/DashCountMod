using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.DashCountMod {
    public class DashCountModModule : EverestModule {

        public static DashCountModModule Instance;

        private static FieldInfo speedBerryPBInChapterPanel;

        public override Type SettingsType => typeof(DashCountModSettings);

        public DashCountModModule() {
            Instance = this;
        }

        public override void Load() {
            // mod methods here
            Everest.Events.Journal.OnEnter += OnJournalEnter;
            On.Celeste.OuiChapterPanel.ctor += ModOuiChapterPanelConstructor;
        }

        public override void Unload() {
            // unmod methods here
            Everest.Events.Journal.OnEnter -= OnJournalEnter;
            On.Celeste.OuiChapterPanel.ctor -= ModOuiChapterPanelConstructor;
        }

        public override void Initialize() {
            base.Initialize();

            // is SpeedBerryPBInChapterPanel a thing?
            EverestModule collabUtils = Everest.Modules.FirstOrDefault(module => module.Metadata.Name == "CollabUtils2");
            if (collabUtils != null) {
                speedBerryPBInChapterPanel = collabUtils.GetType().Assembly.GetType("Celeste.Mod.CollabUtils2.UI.SpeedBerryPBInChapterPanel")
                    .GetField("speedBerryPBDisplay", BindingFlags.NonPublic | BindingFlags.Static);

                Logger.Log("DashCountMod", $"I found the speed berry PB component: {speedBerryPBInChapterPanel.Name} " +
                    $"(type {speedBerryPBInChapterPanel.DeclaringType} in {speedBerryPBInChapterPanel.DeclaringType.Assembly})");

                if ((_Settings as DashCountModSettings).DashCountInChapterPanel) {
                    // be sure the collab utils are hooked (they might not have been loaded when the dash count mod was loaded).
                    hookCollabUtils();
                }
            }
        }

        // ================ Display Dash Count In Level ================

        DashCountModSettings.ShowDashCountInGameOptions showDashCountInGame = DashCountModSettings.ShowDashCountInGameOptions.None;

        public void SetShowDashCountInGame(DashCountModSettings.ShowDashCountInGameOptions value) {
            // (un)hook methods
            bool wasEnabled = (showDashCountInGame != DashCountModSettings.ShowDashCountInGameOptions.None);
            bool isEnabled = (value != DashCountModSettings.ShowDashCountInGameOptions.None);

            if (isEnabled && !wasEnabled) {
                Logger.Log("DashCountMod", "Hooking level enter to add in-game dash counter");
                On.Celeste.Level.Begin += OnLevelBegin;

            } else if (!isEnabled && wasEnabled) {
                Logger.Log("DashCountMod", "Unhooking level enter to stop adding in-game dash counter");
                On.Celeste.Level.Begin -= OnLevelBegin;
            }

            // add/remove/update the dash count accordingly if we already are in a level.
            if (Engine.Scene is Level level) {
                DashCountDisplayInLevel currentDisplay = level.Entities.FindFirst<DashCountDisplayInLevel>();

                if (value == DashCountModSettings.ShowDashCountInGameOptions.None) {
                    currentDisplay?.RemoveSelf();
                } else if (currentDisplay != null) {
                    currentDisplay.SetFormat(value);
                } else {
                    level.Add(new DashCountDisplayInLevel(level.Session, value));
                }
            }

            showDashCountInGame = value;
        }

        private void OnLevelBegin(On.Celeste.Level.orig_Begin orig, Level self) {
            orig(self);
            self.Add(new DashCountDisplayInLevel(self.Session, showDashCountInGame));
        }

        // ================ Journal Page ================

        private void OnJournalEnter(OuiJournal journal, Oui from) {
            // add the "dashes" page just after the "deaths" one
            for (int i = 0; i < journal.Pages.Count; i++) {
                if (journal.Pages[i].GetType() == typeof(OuiJournalDeaths)) {
                    journal.Pages.Insert(i + 1, new OuiJournalDashes(journal));
                }
            }
        }

        // ================ Dash Count in Progress Page ================

        bool fewestDashesInProgressPageEnabled = false;

        public void SetFewestDashesInProgressPageEnabled(bool enabled) {
            if (enabled && !fewestDashesInProgressPageEnabled) {
                Logger.Log("DashCountMod", "Hooking journal progress page rendering methods");

                IL.Celeste.OuiJournalProgress.ctor += ModOuiJournalProgressConstructor;
            } else if (!enabled && fewestDashesInProgressPageEnabled) {
                Logger.Log("DashCountMod", "Unhooking journal progress page rendering methods");

                IL.Celeste.OuiJournalProgress.ctor -= ModOuiJournalProgressConstructor;
            }

            fewestDashesInProgressPageEnabled = enabled;
        }

        private void ModOuiJournalProgressConstructor(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // patch columns to be narrower (100 => 80, 150 => 120, 20 => 0)
            while (cursor.TryGotoNext(instr => instr.MatchLdcR4(100f) || instr.MatchLdcR4(150f) || instr.MatchLdcR4(20f))) {
                float currentValue = (float) cursor.Next.Operand;
                float newValue = (currentValue == 100f ? 80f : (currentValue == 150f ? 120f : 0f));
                Logger.Log("DashCountMod", $"Modding column size from {currentValue} to {newValue} at {cursor.Index} in CIL code for OuiJournalProgress constructor");
                cursor.Next.Operand = newValue;
            }

            cursor.Index = 0;
            // add a column header for dash counts, just before the "time" column
            if (cursor.TryGotoNext(instr => instr.MatchLdstr("time"))) {
                Logger.Log("DashCountMod", $"Adding column header for fewest dashes at {cursor.Index} in CIL code for OuiJournalProgress constructor");

                // At this point, we loaded this.table into the stack. Just use it.
                cursor.EmitDelegate<Action<OuiJournalPage.Table>>(AddColumnHeaderForFewestDashes);

                // Then inject this.table back, so that the game can add the time column.
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiJournalProgress).GetField("table", BindingFlags.NonPublic | BindingFlags.Instance));
            }

            // and actually add cells on each line, once again just before the "total time" column
            if (cursor.TryGotoNext(instr => instr.MatchCallvirt<AreaStats>("get_TotalTimePlayed"))) {
                // at this point, we loaded AreaStats into the stack. (the instruction is ldloc.1 on XNA, ldloc.3 on FNA)
                OpCode loadOpCodeForAreaStats = cursor.Prev.OpCode;

                // let's load the row too. we just happen to know it's local variable 8 on XNA and 17 on FNA.
                if (cursor.TryFindNext(out ILCursor[] nextLocalVariableCursor, instr => instr.OpCode == OpCodes.Ldloc_S
                     && (((VariableDefinition) instr.Operand).Index == 8 || ((VariableDefinition) instr.Operand).Index == 17))) {

                    Logger.Log("DashCountMod", $"Adding column value for fewest dashes at {cursor.Index} in CIL code for OuiJournalProgress constructor: " +
                        $"loading area stats with {loadOpCodeForAreaStats} and current row with ldloc.s {nextLocalVariableCursor[0].Next.Operand}");

                    // load row and this into the stack, then call our delegate which will build and add the cell.
                    cursor.Emit(OpCodes.Ldloc_S, nextLocalVariableCursor[0].Next.Operand);
                    cursor.Emit(OpCodes.Ldarg_0);
                    cursor.EmitDelegate<Action<AreaStats, OuiJournalPage.Row, OuiJournalProgress>>(AddColumnValueForFewestDashes);

                    // then, put back AreaStats in the stack for the Time cell to use it.
                    cursor.Emit(loadOpCodeForAreaStats);
                }
            }

            // finally, inject ourselves in the Totals line, just before Time
            if (cursor.TryGotoNext(instr => instr.MatchLdfld<SaveData>("Time"))) {
                // step back before loading SaveData.Instance. The instruction before that loads the row in the stack
                cursor.Index--;
                object varIndexForRow = cursor.Prev.Operand;

                Logger.Log("DashCountMod", $"Adding column total for fewest dashes at {cursor.Index} in CIL code for OuiJournalProgress constructor: loading row with {varIndexForRow}");

                // at this point, we have row in the stack. Add this, then call our delegate which will build and add the cell.
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Action<OuiJournalPage.Row, OuiJournalProgress>>(AddColumnTotalForFewestDashes);

                // Then inject row back, so that the game can add the time column.
                cursor.Emit(OpCodes.Ldloc_S, varIndexForRow);
            }
        }

        private void AddColumnHeaderForFewestDashes(OuiJournalPage.Table table) {
            table.AddColumn(new OuiJournalPage.IconCell("max480/DashCountMod/dashes", 80f));
        }

        private void AddColumnValueForFewestDashes(AreaStats areaStats, OuiJournalPage.Row row, OuiJournalProgress self) {
            // we only show values for SingleRunCompleted sides. so, the total only appears for chapters with only SingleRunCompleted sides
            bool allSingleRunCompleted = true;
            int mode = 0;
            foreach (AreaModeStats modeStats in areaStats.Modes ?? new AreaModeStats[0]) {
                if (AreaData.Areas[areaStats.ID].HasMode((AreaMode) mode++) && !modeStats.SingleRunCompleted) {
                    allSingleRunCompleted = false;
                }
            }

            if (allSingleRunCompleted) {
                row.Add(new OuiJournalPage.TextCell(Dialog.Deaths(areaStats.BestTotalDashes), self.TextJustify, 0.5f, self.TextColor));
            } else {
                row.Add(new OuiJournalPage.IconCell("dot"));
            }
        }

        private void AddColumnTotalForFewestDashes(OuiJournalPage.Row row, OuiJournalProgress self) {
            // go across all areas that are not interludes, and check if they are all completed in a single run
            bool allSingleRunCompleted = true;
            int totalDashes = 0;
            foreach (AreaStats areaStats in SaveData.Instance.Areas) {
                if (!AreaData.Areas[areaStats.ID].Interlude) {
                    int mode = 0;
                    foreach (AreaModeStats modeStats in areaStats.Modes ?? new AreaModeStats[0]) {
                        if (AreaData.Areas[areaStats.ID].HasMode((AreaMode) mode++) && !modeStats.SingleRunCompleted) {
                            allSingleRunCompleted = false;
                        }
                    }
                    if (allSingleRunCompleted) totalDashes += areaStats.BestTotalDashes;
                }
            }

            if (allSingleRunCompleted) {
                row.Add(new OuiJournalPage.TextCell(Dialog.Deaths(totalDashes), self.TextJustify, 0.6f, self.TextColor));
            } else {
                row.Add(new OuiJournalPage.IconCell("dot"));
            }
        }

        // ================ Dash Count in Chapter Panel ================

        bool dashCounterInChapterPanelEnabled = false;
        private static Hook collabUtilsHook = null;

        public void SetDashCounterInChapterPanelEnabled(bool enabled) {
            if (enabled && !dashCounterInChapterPanelEnabled) {
                Logger.Log("DashCountMod", "Hooking chapter panel rendering methods");

                using (new DetourContext() { After = { "*" } }) { // be sure to apply _after_ the collab utils.
                    IL.Celeste.OuiChapterPanel.Render += ModOuiChapterPanelRender;
                    On.Celeste.OuiChapterPanel.UpdateStats += ModOuiChapterPanelUpdateStats;
                    IL.Celeste.OuiChapterPanel.SetStatsPosition += ModOuiChapterPanelSetStatsPosition;
                    On.Celeste.OuiChapterPanel.IncrementStatsDisplay += ModOuiChapterPanelIncrementStatsDisplay;
                    On.Celeste.OuiChapterPanel.GetModeHeight += ModOuiChapterPanelGetModeHeight;
                }

                hookCollabUtils();
            } else if (!enabled && dashCounterInChapterPanelEnabled) {
                Logger.Log("DashCountMod", "Unhooking chapter panel rendering methods");

                IL.Celeste.OuiChapterPanel.Render -= ModOuiChapterPanelRender;
                On.Celeste.OuiChapterPanel.UpdateStats -= ModOuiChapterPanelUpdateStats;
                IL.Celeste.OuiChapterPanel.SetStatsPosition -= ModOuiChapterPanelSetStatsPosition;
                On.Celeste.OuiChapterPanel.IncrementStatsDisplay -= ModOuiChapterPanelIncrementStatsDisplay;
                On.Celeste.OuiChapterPanel.GetModeHeight -= ModOuiChapterPanelGetModeHeight;

                collabUtilsHook?.Dispose();
                collabUtilsHook = null;

                // hide the dash counter if currently shown: as we unhooked everything updating it, it will stay invisible.
                dashesCounter.Visible = false;
            }

            dashCounterInChapterPanelEnabled = enabled;
        }

        private static void hookCollabUtils() {
            if (collabUtilsHook == null) {
                // is SpeedBerryPBInChapterPanel a thing?
                EverestModule collabUtils = Everest.Modules.FirstOrDefault(module => module.Metadata.Name == "CollabUtils2");

                if (collabUtils != null) {
                    collabUtilsHook = new Hook(
                        collabUtils.GetType().Assembly.GetType("Celeste.Mod.CollabUtils2.UI.SpeedBerryPBInChapterPanel").GetMethod("approach", BindingFlags.NonPublic | BindingFlags.Static),
                        typeof(DashCountModModule).GetMethod("modSpeedBerryPBApproach", BindingFlags.NonPublic | BindingFlags.Static));

                    Logger.Log("DashCountMod", "Collab utils speed berry PB counter was hooked");
                }
            }
        }

        private delegate Vector2 orig_approach(Vector2 from, Vector2 to, bool snap);
        private static Vector2 modSpeedBerryPBApproach(orig_approach orig, Vector2 from, Vector2 to, bool snap) {
            if ((Instance.dashesCounter?.Visible ?? false) && Instance.dashesOffset.Y != 160f) {
                to.Y -= 40f;
            }

            return orig(from, to, snap);
        }

        private DashesCounterInChapterPanel dashesCounter;
        private Vector2 dashesOffset;

        private void ModOuiChapterPanelConstructor(On.Celeste.OuiChapterPanel.orig_ctor orig, OuiChapterPanel self) {
            orig(self);

            // add the dashes counter as well, but have it hidden by default
            self.Add(dashesCounter = new DashesCounterInChapterPanel(true, 0));
            dashesCounter.CanWiggle = false;
            dashesCounter.Visible = false;
        }

        private void ModOuiChapterPanelRender(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // move after the deaths counter positioning, and place ourselves after that to update dashes counter position as well
            if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchStfld(typeof(DeathsCounter), "Position"))) {
                Logger.Log("DashCountMod", $"Injecting dashes counter position updating at {cursor.Index} in CIL code for OuiChapterPanel.Render");

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiChapterPanel).GetField("contentOffset", BindingFlags.NonPublic | BindingFlags.Instance));
                cursor.EmitDelegate<Action<Vector2>>(UpdateDashesCounterRenderedPosition);
            }
        }

        private void UpdateDashesCounterRenderedPosition(Vector2 contentOffset) {
            dashesCounter.Position = contentOffset + new Vector2(0f, 170f) + dashesOffset;
        }

        private void ModOuiChapterPanelUpdateStats(On.Celeste.OuiChapterPanel.orig_UpdateStats orig, OuiChapterPanel self, bool wiggle, bool? overrideStrawberryWiggle, bool? overrideDeathWiggle, bool? overrideHeartWiggle) {
            orig(self, wiggle, overrideStrawberryWiggle, overrideDeathWiggle, overrideHeartWiggle);

            dashesCounter.Visible = self.DisplayedStats.Modes[(int) self.Area.Mode].SingleRunCompleted && !AreaData.Get(self.Area).Interlude;
            dashesCounter.Amount = self.DisplayedStats.Modes[(int) self.Area.Mode].BestDashes;

            if (wiggle && dashesCounter.Visible && (overrideDeathWiggle ?? true)) {
                dashesCounter.Wiggle();
            }
        }

        private void ModOuiChapterPanelSetStatsPosition(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // this is a tricky one... in lines like this:
            // this.strawberriesOffset = this.Approach(this.strawberriesOffset, new Vector2(120f, (float)(this.deaths.Visible ? -40 : 0)), !approach);
            // we want to catch the result of (float)(this.deaths.Visible ? -40 : 0) and transform it to shift the things up if the dashes counter is there.
            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchConvR4())) {
                Logger.Log("DashCountMod", $"Modifying strawberry/death counter positioning at {cursor.Index} in CIL code for OuiChapterPanel.SetStatsPosition");
                cursor.EmitDelegate<Func<float, float>>(ShiftCountersPosition);
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
                cursor.EmitDelegate<Action<bool, StrawberriesCounter, DeathsCounter, bool>>(UpdateDashesCounterOffset);

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

        private float ShiftCountersPosition(float position) {
            return dashesCounter.Visible && dashesOffset.Y != 160f ? position - 40 : position;
        }

        private void UpdateDashesCounterOffset(bool approach, StrawberriesCounter strawberries, DeathsCounter deaths, bool hasHeart) {
            int shift = 0;
            if (strawberries.Visible) shift += 40;
            if (deaths.Visible) shift += 40;
            if (speedBerryPBInChapterPanel != null && speedBerryPBInChapterPanel.GetValue(null) is Component component && component.Visible) shift += 40;
            if (shift == 120f) shift += 40;
            dashesOffset = Approach(dashesOffset, new Vector2(hasHeart ? 120f : 0f, shift), !approach);
        }

        // vanilla method copypaste
        private Vector2 Approach(Vector2 from, Vector2 to, bool snap) {
            if (snap) return to;
            return from += (to - from) * (1f - (float) Math.Pow(0.0010000000474974513, Engine.DeltaTime));
        }

        private IEnumerator ModOuiChapterPanelIncrementStatsDisplay(On.Celeste.OuiChapterPanel.orig_IncrementStatsDisplay orig, OuiChapterPanel self, AreaModeStats modeStats,
            AreaModeStats newModeStats, bool doHeartGem, bool doStrawberries, bool doDeaths, bool doRemixUnlock) {

            IEnumerator origMethod = orig(self, modeStats, newModeStats, doHeartGem, doStrawberries, doDeaths, doRemixUnlock);
            while (origMethod.MoveNext()) yield return origMethod.Current;

            if (newModeStats.SingleRunCompleted && modeStats.BestDashes != newModeStats.BestDashes) {
                yield return 0.5f;

                Audio.Play("event:/ui/postgame/death_appear");
                dashesCounter.CanWiggle = true;
                dashesCounter.Visible = true;
                while (newModeStats.BestDashes != modeStats.BestDashes) {
                    int jumpSize;
                    yield return HandleDashTick(modeStats.BestDashes, newModeStats.BestDashes, out jumpSize);
                    modeStats.BestDashes += Math.Sign(newModeStats.BestDashes - modeStats.BestDashes) * jumpSize;
                    dashesCounter.Amount = modeStats.BestDashes;
                    if (modeStats.BestDashes == newModeStats.BestDashes) {
                        Audio.Play("event:/ui/postgame/death_final");
                    } else {
                        Audio.Play("event:/ui/postgame/death_count");
                    }
                    Input.Rumble(RumbleStrength.Light, RumbleLength.Short);
                }
                dashesCounter.CanWiggle = false;

                yield return 0.8f;
            }

            yield break;
        }

        private int ModOuiChapterPanelGetModeHeight(On.Celeste.OuiChapterPanel.orig_GetModeHeight orig, OuiChapterPanel self) {
            int origModeHeight = orig(self);
            if (origModeHeight == 540 && dashesOffset.Y == 160f) {
                return 610;
            }
            return origModeHeight;
        }

        // nearly another vanilla method copypaste
        private float HandleDashTick(int oldDashes, int newDashes, out int jumpSize) {
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
