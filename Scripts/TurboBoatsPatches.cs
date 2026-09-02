using HarmonyLib;

namespace TurboBoats
{
    /// <summary>
    /// ECSManager.Init() enqueues Manager.mod.ExtraAuthoring for entity conversion. Registering our cloned boat
    /// prefabs (and seeding their pinned ObjectIDs) right before it runs guarantees they are converted with
    /// everything else.
    /// </summary>
    [HarmonyPatch(typeof(ECSManager), "Init", new System.Type[0])]
    internal static class ECSManagerInitPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            TurboBoatsRegistry.TryRegister("ECSManager.Init prefix");
        }
    }
}
