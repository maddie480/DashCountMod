using Celeste.Mod.CollabUtils2;
using Celeste.Mod.CollabUtils2.UI;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.DashCountMod {
    class DashCountDisplayInLevel : Entity {
        private Session session;
        private DashCountModSettings.ShowDashCountInGameOptions format;

        private Level level;
        private TotalStrawberriesDisplay berryCounter;
        private SpeedrunTimerDisplay speedrunTimer;
        private bool collabUtilsExists;

        private MTexture bg = GFX.Gui["DashCountMod/extendedStrawberryCountBG"];
        private MTexture dashIcon = GFX.Gui["collectables/dashes"];
        private MTexture x = GFX.Gui["x"];

        public DashCountDisplayInLevel(Session session, DashCountModSettings.ShowDashCountInGameOptions format) {
            this.session = session;
            this.format = format;

            Tag = (Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.TransitionUpdate);
            Position = new Vector2(0, -1000);
        }

        public void SetFormat(DashCountModSettings.ShowDashCountInGameOptions format) {
            this.format = format;
        }

        public override void Awake(Scene scene) {
            base.Awake(scene);

            level = SceneAs<Level>();
            berryCounter = Scene.Entities.FindFirst<TotalStrawberriesDisplay>();
            speedrunTimer = Scene.Entities.FindFirst<SpeedrunTimerDisplay>();
            collabUtilsExists = Everest.Loader.DependencyLoaded(new EverestModuleMetadata { Name = "CollabUtils2", Version = new Version(1, 6, 7) });
        }

        public override void Update() {
            base.Update();

            float y = 85f;

            // leave room for the speedrun timer.
            if (speedrunTimer.DrawLerp > 0f) {
                if (Settings.Instance.SpeedrunClock == SpeedrunType.Chapter) {
                    y += 58f;
                } else if (Settings.Instance.SpeedrunClock == SpeedrunType.File) {
                    y += 78f;
                }
            }

            // leave room for the strawberries counter.
            if (berryCounter.DrawLerp > 0f) {
                y += 78f;
            }

            if (collabUtilsExists) {
                y += getSpeedBerryOffset();
            }

            Y = y;
        }

        private float getSpeedBerryOffset() {
            SpeedBerryTimerDisplay speedBerryTimer = level.Tracker.GetEntity<SpeedBerryTimerDisplay>();

            if (speedBerryTimer != null && CollabModule.Instance.Settings.SpeedBerryTimerPosition == CollabSettings.SpeedBerryTimerPositions.TopLeft) {
                float offsetY = 110f;

                if (new DynData<SpeedBerryTimerDisplay>(speedBerryTimer).Get<long>("startChapterTimer") == -1) {
                    // more times are displayed.
                    offsetY += 70f;
                }

                return offsetY;
            }

            // no speed berry timer, or it is not on the top left so it doesn't affect us.
            return 0f;
        }

        public override void Render() {
            base.Render();

            string count = getCountToDisplay();

            float dashCountSize = ActiveFont.Measure(count).X;
            bg.Draw(Position + new Vector2(-402 + dashCountSize, 0));

            dashIcon.Draw(Position + new Vector2(9, -36));
            x.Draw(Position + new Vector2(94, -14));
            ActiveFont.DrawOutline(count, Position + new Vector2(144, -26), Vector2.Zero, Vector2.One, Color.White, 2f, Color.Black);
        }

        private string getCountToDisplay() {
            int dashCountInLevel = 0;

            AreaKey area = SceneAs<Level>().Session.Area;
            if ((DashCountModModule.Instance._SaveData as DashCountModSaveData).DashCountPerLevel.TryGetValue(area.GetSID(), out Dictionary<AreaMode, int> areaModes)) {
                if (areaModes.TryGetValue(area.Mode, out int totalDashes)) {
                    dashCountInLevel = totalDashes;
                }
            }

            switch (format) {
                case DashCountModSettings.ShowDashCountInGameOptions.Session:
                    return "" + session.Dashes;
                case DashCountModSettings.ShowDashCountInGameOptions.Chapter:
                    return "" + dashCountInLevel;
                case DashCountModSettings.ShowDashCountInGameOptions.File:
                    return "" + SaveData.Instance.TotalDashes;
                default:
                    return SaveData.Instance.TotalDashes + " (" + dashCountInLevel + ")";
            }
        }
    }
}
