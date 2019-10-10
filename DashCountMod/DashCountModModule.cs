using System;
using System.Collections;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;

namespace Celeste.Mod.DashCountMod {
    public class DashCountModModule : EverestModule {

        public static DashCountModModule Instance;

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

        // ================ Journal Page ================

        private void OnJournalEnter(OuiJournal journal, Oui from) {
            // add the "dashes" page just after the "deaths" one
            for(int i = 0; i < journal.Pages.Count; i++) {
                if(journal.Pages[i].GetType() == typeof(OuiJournalDeaths)) {
                    journal.Pages.Insert(i + 1, new OuiJournalDashes(journal));
                }
            }
        }

        // ================ Dash Count in Chapter Panel ================

        bool dashCounterInChapterPanelEnabled = false;

        public void SetDashCounterInChapterPanelEnabled(bool enabled) {
            if (enabled && !dashCounterInChapterPanelEnabled) {
                Logger.Log("DashCountMod", "Hooking chapter panel rendering methods");

                IL.Celeste.OuiChapterPanel.Render += ModOuiChapterPanelRender;
                On.Celeste.OuiChapterPanel.UpdateStats += ModOuiChapterPanelUpdateStats;
                IL.Celeste.OuiChapterPanel.SetStatsPosition += ModOuiChapterPanelSetStatsPosition;
                On.Celeste.OuiChapterPanel.IncrementStatsDisplay += ModOuiChapterPanelIncrementStatsDisplay;
            } else if(!enabled && dashCounterInChapterPanelEnabled) {
                Logger.Log("DashCountMod", "Unhooking chapter panel rendering methods");

                IL.Celeste.OuiChapterPanel.Render -= ModOuiChapterPanelRender;
                On.Celeste.OuiChapterPanel.UpdateStats -= ModOuiChapterPanelUpdateStats;
                IL.Celeste.OuiChapterPanel.SetStatsPosition -= ModOuiChapterPanelSetStatsPosition;
                On.Celeste.OuiChapterPanel.IncrementStatsDisplay -= ModOuiChapterPanelIncrementStatsDisplay;

                // hide the dash counter if currently shown: as we unhooked everything updating it, it will stay invisible.
                dashesCounter.Visible = false;
            }

            dashCounterInChapterPanelEnabled = enabled;
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
            if(cursor.TryGotoNext(MoveType.After, instr => instr.MatchStfld(typeof(DeathsCounter), "Position"))) {
                Logger.Log("DashCountMod", $"Injecting dashes counter position updating at {cursor.Index} in CIL code for OuiChapterPanel.Render");

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiChapterPanel).GetField("contentOffset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
                cursor.EmitDelegate<Action<Vector2>>(UpdateDashesCounterRenderedPosition);
            }
        }

        private void UpdateDashesCounterRenderedPosition(Vector2 contentOffset) {
            dashesCounter.Position = contentOffset + new Vector2(0f, 170f) + dashesOffset;
        }

        private void ModOuiChapterPanelUpdateStats(On.Celeste.OuiChapterPanel.orig_UpdateStats orig, OuiChapterPanel self, bool wiggle, bool? overrideStrawberryWiggle, bool? overrideDeathWiggle, bool? overrideHeartWiggle) {
            orig(self, wiggle, overrideStrawberryWiggle, overrideDeathWiggle, overrideHeartWiggle);

            dashesCounter.Visible = self.DisplayedStats.Modes[(int)self.Area.Mode].SingleRunCompleted && !AreaData.Get(self.Area).Interlude;
            dashesCounter.Amount = self.DisplayedStats.Modes[(int)self.Area.Mode].BestDashes;

            if(wiggle && dashesCounter.Visible && (overrideDeathWiggle ?? true)) {
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
            while(cursor.TryGotoNext(MoveType.After, instr => instr.MatchStfld(typeof(OuiChapterPanel), "deathsOffset"))) {
                Logger.Log("DashCountMod", $"Injecting dashes counter position updating at {cursor.Index} in CIL code for OuiChapterPanel.SetStatsPosition (has heart = {hasHeart})");

                // bool approach
                cursor.Emit(OpCodes.Ldarg_1);
                // StrawberriesCounter strawberries
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiChapterPanel).GetField("strawberries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
                // DeathsCounter deaths
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(OuiChapterPanel).GetField("deaths", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
                // bool hasHeart
                cursor.Emit(hasHeart ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                // function call
                cursor.EmitDelegate<Action<bool, StrawberriesCounter, DeathsCounter, bool>>(UpdateDashesCounterOffset);

                hasHeart = false;
            }
        }

        private float ShiftCountersPosition(float position) {
            return dashesCounter.Visible ? position - 40 : position;
        }

        private void UpdateDashesCounterOffset(bool approach, StrawberriesCounter strawberries, DeathsCounter deaths, bool hasHeart) {
            int shift = 0;
            if (strawberries.Visible) shift += 40;
            if (deaths.Visible) shift += 40;
            dashesOffset = Approach(dashesOffset, new Vector2(hasHeart ? 120f : 0f, shift), !approach);
        }

        // vanilla method copypaste
        private Vector2 Approach(Vector2 from, Vector2 to, bool snap) {
            if (snap) return to;
            return from += (to - from) * (1f - (float)Math.Pow(0.0010000000474974513, Engine.DeltaTime));
        }

        private IEnumerator ModOuiChapterPanelIncrementStatsDisplay(On.Celeste.OuiChapterPanel.orig_IncrementStatsDisplay orig, OuiChapterPanel self, AreaModeStats modeStats, 
            AreaModeStats newModeStats, bool doHeartGem, bool doStrawberries, bool doDeaths, bool doRemixUnlock) {

            IEnumerator origMethod = orig(self, modeStats, newModeStats, doHeartGem, doStrawberries, doDeaths, doRemixUnlock);
            while(origMethod.MoveNext()) yield return origMethod.Current;

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
