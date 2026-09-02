using PugMod;
using UnityEngine;

namespace TurboBoats
{
    /// <summary>
    /// Mod entry point. Adds three crafted boat tiers (2x / 3x / 4x the Speeder's speed, recipe scaled by the same
    /// multiplier) to the Boat Workbench. Must be installed on the server AND every client (requiredOn = 3):
    /// boat movement is client-predicted, so both sides must agree on the boat's speedMultiplier.
    ///
    /// Registration happens in a Harmony prefix on ECSManager.Init (see TurboBoatsPatches) - that is the last
    /// moment before the game converts the mod's extra authoring objects. Update() is a belt-and-braces fallback
    /// that does the same thing earlier if the prefix did not apply; both paths are idempotent.
    /// </summary>
    public class TurboBoatsMod : IMod
    {
        public const string Name = "Turbo Boats";
        public const string Version = "1.0.0";

        public void EarlyInit()
        {
            Debug.Log("[TurboBoats] " + Version + " loaded - Turbo Speeder (2x / 3x / 4x) at the Boat Workbench.");
        }

        public void Init() { }
        public void Shutdown() { }
        public void ModObjectLoaded(Object obj) { }

        public void Update()
        {
            TurboBoatsRegistry.TryRegister("IMod.Update");
        }
    }
}
