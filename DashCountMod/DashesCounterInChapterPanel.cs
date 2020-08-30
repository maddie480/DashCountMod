
namespace Celeste.Mod.DashCountMod {
    public class DashesCounterInChapterPanel : DeathsCounter {
        public DashesCounterInChapterPanel(bool centeredX, int amount, int minDigits = 0) : base(AreaMode.Normal, centeredX, amount, minDigits) {
            // should be the same as the Deaths counter, except for the icon which should be the Dashes one: let's change this with reflection
            typeof(DeathsCounter).GetField("icon", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(this, GFX.Gui["collectables/dashes_75px"]);
        }
    }
}
