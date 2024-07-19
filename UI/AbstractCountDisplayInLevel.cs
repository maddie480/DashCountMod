using Celeste.Mod.CollabUtils2;
using Celeste.Mod.CollabUtils2.UI;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using static Celeste.Mod.DashCountMod.DashCountModSettings;

namespace Celeste.Mod.DashCountMod.UI {
    public abstract class AbstractCountDisplayInLevel : Entity {
        protected DashCountModSaveData ModSaveData {
            get {
                return (DashCountModSaveData) DashCountModModule.Instance._SaveData;
            }
        }

        protected readonly Session session;
        protected ShowCountInGameOptions format;
        private readonly float spriteExtraY;

        private Level level;
        private TotalStrawberriesDisplay berryCounter;
        private SpeedrunTimerDisplay speedrunTimer;
        private bool collabUtilsExists;

        private readonly MTexture bg = GFX.Gui["DashCountMod/extendedStrawberryCountBG"];
        private readonly MTexture icon;
        private readonly MTexture x = GFX.Gui["x"];

        protected AbstractCountDisplayInLevel(Session session, ShowCountInGameOptions format, MTexture icon, float spriteExtraY) {
            this.session = session;
            this.format = format;
            this.icon = icon;
            this.spriteExtraY = spriteExtraY;

            Tag = (Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.TransitionUpdate);
            Position = new Vector2(0, -1000);
        }

        public void SetFormat(ShowCountInGameOptions format) {
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

            IEnumerator<AbstractCountDisplayInLevel> enumerator = EnumeratePreviousCountDisplays();
            while (enumerator.MoveNext()) {
                y += 78f;
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

            icon.Draw(Position + new Vector2(9, -36 + spriteExtraY));
            x.Draw(Position + new Vector2(94, -14));
            ActiveFont.DrawOutline(count, Position + new Vector2(144, -26), Vector2.Zero, Vector2.One, Color.White, 2f, Color.Black);
        }

        private string getCountToDisplay() {
            switch (format) {
                case ShowCountInGameOptions.Session:
                    return "" + GetCountForSession();
                case ShowCountInGameOptions.Chapter:
                    return "" + GetCountForChapter();
                case ShowCountInGameOptions.File:
                    return "" + GetCountForFile();
                default:
                    return GetCountForFile() + " (" + GetCountForChapter() + ")";
            }
        }

        protected int GetCountFromSaveData(Dictionary<string, Dictionary<AreaMode, int>> saveDataMap) {
            if (saveDataMap.TryGetValue(session.Area.GetSID(), out Dictionary<AreaMode, int> areaModes)) {
                if (areaModes.TryGetValue(session.Area.Mode, out int count)) {
                    return count;
                }
            }

            return 0;
        }

        protected abstract int GetCountForSession();

        protected abstract int GetCountForChapter();

        protected abstract int GetCountForFile();

        protected abstract IEnumerator<AbstractCountDisplayInLevel> EnumeratePreviousCountDisplays();
    }

}
