using HarmonyLib;

namespace TurboBoats
{
    /// <summary>
    /// ECSManager.Init() converts every EntityAuthoringDataBlock into entities. Attaching our cloned boat prefabs
    /// to their data blocks (and seeding their pinned ObjectIDs) right before it runs guarantees they are converted
    /// with everything else.
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
