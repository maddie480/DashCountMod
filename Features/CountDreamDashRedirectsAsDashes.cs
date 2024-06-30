using Monocle;
using MonoMod.RuntimeDetour;
using System;
using System.Linq;
using System.Reflection;

namespace Celeste.Mod.DashCountMod.Features {
    public static class CountDreamDashRedirectsAsDashes {
        private static Hook pandorasBoxHook = null;

        public static void Initialize() {
            if (((DashCountModSettings) DashCountModModule.Instance._Settings).CountDreamDashRedirectsAsDashes) {
                hookPandorasBox();
            }
        }

        public static void SetEnabled(bool enabled) {
            if (enabled && pandorasBoxHook == null) {
                Logger.Log("DashCountMod", "Hooking Pandora's Box dream dash redirects");
                hookPandorasBox();

            } else if (!enabled && pandorasBoxHook != null) {
                Logger.Log("DashCountMod", "Unhooking Pandora's Box dream dash redirects");

                pandorasBoxHook?.Dispose();
                pandorasBoxHook = null;
            }
        }

        private static void hookPandorasBox() {
            if (pandorasBoxHook == null) {
                // is Pandora's Box a thing?
                EverestModule pandorasBox = Everest.Modules.FirstOrDefault(module => module.Metadata.Name == "PandorasBox");

                if (pandorasBox != null) {
                    pandorasBoxHook = new Hook(
                        pandorasBox.GetType().Assembly.GetType("Celeste.Mod.PandorasBox.DreamDashController").GetMethod("dreamDashRedirect", BindingFlags.NonPublic | BindingFlags.Instance),
                        typeof(DashCountModModule).GetMethod("countPandorasBoxDashes", BindingFlags.NonPublic | BindingFlags.Static));

                    Logger.Log("DashCountMod", "Pandora's Box Dream Dash Controller hooked");
                }
            }
        }

        private static bool countPandorasBoxDashes(Func<Entity, Player, bool> orig, Entity self, Player player) {
            bool didRedirect = orig(self, player);

            if (didRedirect) {
                SaveData.Instance.TotalDashes++;
                self.SceneAs<Level>().Session.Dashes++;
                Stats.Increment(Stat.DASHES);
                CustomDashCounting.AddDash(self);
            }

            return didRedirect;
        }
    }
}
