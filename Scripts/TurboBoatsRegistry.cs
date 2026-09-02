using System;
using System.Collections.Generic;
using PugMod;
using Unity.Entities;
using UnityEngine;

namespace TurboBoats
{
    /// <summary>
    /// Clones the vanilla Speeder (SpeederBoat) authoring prefab three times at boot, turns each clone into a new
    /// object ("TurboBoats:Mk1/2/3/4" = 2x/3x/4x/5x the Speeder's speedMultiplier) with crafting cost scaled by the same
    /// multiplier, registers the clones with the official mod API so the game converts them like any other object,
    /// and appends them to the Boat Workbench's recipe list when the workbench is converted.
    ///
    /// PINNED ObjectIDs: unlike MinecartTurbo (whose carts took sequential IDs 32768-70 in name-hash order, fragile
    /// against any future entity-adding mod), the boats pre-seed Manager.mod.Authoring.ObjectIDLookup BEFORE
    /// conversion. ObjectAuthoring.TryGetPreferredObjectIndex finds the seeded entry and converts each boat at its
    /// pinned ID, and the sequential counter (_nextUnusedObjectIndex) never advances for them - so the live carts
    /// keep 32768/69/70 with this mod installed, and the boats keep 40001-4 no matter what mods come later.
    /// Ferry pin registry: 40001-4 taken by TurboBoats; the next Ferry mod starts at 40011.
    ///
    /// IDENTITY IS PERMANENT: the pinned IDs and the clone GameObject names (the NetCode ghost type is hashed from
    /// them) must NEVER change once boats exist in a world - renaming or re-pinning orphans them.
    ///
    /// LOCALIZATION TRAP (learned on MinecartTurbo): I2's LocalizationManager rewrites ':' to '_' in every
    /// looked-up term, but the CSV import keeps ':' as-is. Item name terms are "Items/" + objectName, so the CSV
    /// keys MUST use the underscore form (Items/TurboBoats_Mk1), never the object name's colon form.
    /// </summary>
    public static class TurboBoatsRegistry
    {
        private sealed class Tier
        {
            public string ObjectName;   // registered object name -> localization key "Items/<name with : as _>"
            public int PinnedID;        // permanent ObjectID, pre-seeded into ObjectIDLookup before conversion
            public float Multiplier;    // applied to the Speeder's speedMultiplier, every recipe ingredient and the sell value
            public Rarity Rarity;       // purely cosmetic: name colour in the UI marks the tier
            public string CloneName { get { return ObjectName.Replace(':', '_'); } }   // ghost type derives from this
        }

        private static readonly Tier[] Tiers =
        {
            new Tier { ObjectName = "TurboBoats:Mk1", PinnedID = 40001, Multiplier = 2.0f, Rarity = Rarity.Rare },
            new Tier { ObjectName = "TurboBoats:Mk2", PinnedID = 40002, Multiplier = 3.0f, Rarity = Rarity.Epic },
            new Tier { ObjectName = "TurboBoats:Mk3", PinnedID = 40003, Multiplier = 4.0f, Rarity = Rarity.Legendary },
            new Tier { ObjectName = "TurboBoats:Mk4", PinnedID = 40004, Multiplier = 5.0f, Rarity = Rarity.Legendary },
        };

        private const string Tag = "[TurboBoats] ";

        private static bool _attempted;
        private static bool _subscribed;
        private static readonly List<GameObject> _clones = new List<GameObject>();

        /// <summary>Idempotent. Safe to call every frame; does real work exactly once, before ECSManager.Init runs.</summary>
        public static void TryRegister(string source)
        {
            if (_attempted) return;
            if (Manager.main == null) return;
            ECSManager ecs = Manager.ecs;
            if (ecs == null || ecs.pugDatabase == null || API.Authoring == null) return;
            if (ecs.BlobAssetStore.IsCreated)
            {
                // Init's body has already started/finished: registering now would add authoring objects that never
                // get converted. Refuse loudly instead of half-working.
                _attempted = true;
                Debug.LogError(Tag + "too late to register (" + source + "): ECSManager.Init already ran. Turbo boats NOT added.");
                return;
            }
            _attempted = true;
            try
            {
                Register(ecs, source);
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "registration failed: " + e);
            }
        }

        private static void Register(ECSManager ecs, string source)
        {
            MonoBehaviour vanilla = FindVanillaSpeeder(ecs.pugDatabase.prefabList);
            if (vanilla == null)
            {
                Debug.LogError(Tag + "vanilla Speeder (SpeederBoat) authoring prefab not found in PugDatabase - turbo boats NOT added.");
                return;
            }
            if (!SeedPinnedObjectIDs())
            {
                Debug.LogError(Tag + "pinned ObjectID seeding failed - turbo boats NOT added.");
                return;
            }
            Subscribe();
            foreach (Tier tier in Tiers)
            {
                GameObject clone = CloneBoat(vanilla.gameObject, tier);
                if (clone == null) continue;
                API.Authoring.RegisterAuthoringGameObject(clone);
                _clones.Add(clone);
            }
            Debug.Log(Tag + "registered " + _clones.Count + " turbo boat prefab(s) cloned from '" + vanilla.name + "' (" + source + ")");
        }

        /// <summary>
        /// Pre-seeds the name->ObjectID lookup so ObjectAuthoring.TryGetPreferredObjectIndex converts each boat at
        /// its pinned ID instead of consuming a sequential index. Deliberately seeds ONLY the boat names: if
        /// existing sequential objects (the carts) were seeded too, the sequential counter would stay behind them
        /// and hand their IDs to the next entity-adding mod.
        /// </summary>
        private static bool SeedPinnedObjectIDs()
        {
            Dictionary<string, ObjectID> lookup = Manager.mod.Authoring.ObjectIDLookup;
            if (lookup == null)
            {
                Debug.LogError(Tag + "ObjectIDLookup unavailable - cannot pin IDs.");
                return false;
            }
            foreach (Tier tier in Tiers)
            {
                ObjectID existing;
                if (lookup.TryGetValue(tier.ObjectName, out existing))
                {
                    if ((int)existing != tier.PinnedID)
                    {
                        Debug.LogError(Tag + tier.ObjectName + " already mapped to ObjectID " + (int)existing
                                       + " (expected pin " + tier.PinnedID + ") - refusing to fight over it.");
                        return false;
                    }
                    continue;
                }
                lookup.Add(tier.ObjectName, (ObjectID)tier.PinnedID);
                Debug.Log(Tag + "pinned " + tier.ObjectName + " -> ObjectID " + tier.PinnedID);
            }
            return true;
        }

        private static MonoBehaviour FindVanillaSpeeder(List<MonoBehaviour> prefabList)
        {
            if (prefabList == null) return null;
            foreach (MonoBehaviour mono in prefabList)
            {
                if (mono == null) continue;
                EntityMonoBehaviourData embd = mono as EntityMonoBehaviourData;
                if (embd != null)
                {
                    if (embd.objectInfo != null && embd.objectInfo.objectID == ObjectID.SpeederBoat && embd.objectInfo.variation == 0) return mono;
                    continue;
                }
                ObjectAuthoring oa = mono as ObjectAuthoring;
                if (oa != null && oa.objectName == "SpeederBoat" && oa.variation == 0) return mono;
            }
            return null;
        }

        private static GameObject CloneBoat(GameObject source, Tier tier)
        {
            GameObject clone = UnityEngine.Object.Instantiate(source);
            clone.name = tier.CloneName;
            clone.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(clone);

            ObjectAuthoring oa = clone.GetComponent<ObjectAuthoring>();
            EntityMonoBehaviourData embd = clone.GetComponent<EntityMonoBehaviourData>();
            if (embd != null)
            {
                // Vanilla objects carry their data in an EntityMonoBehaviourData.objectInfo block with a fixed enum
                // ObjectID. A new object needs the string-named ObjectAuthoring form, so transplant the data.
                oa = ConvertToObjectAuthoring(clone, embd);
                UnityEngine.Object.DestroyImmediate(embd);
            }
            if (oa == null)
            {
                Debug.LogError(Tag + "'" + source.name + "' has neither ObjectAuthoring nor EntityMonoBehaviourData - cannot clone.");
                UnityEngine.Object.DestroyImmediate(clone);
                return null;
            }

            BoatAuthoring boat = clone.GetComponent<BoatAuthoring>();
            if (boat == null)
            {
                Debug.LogError(Tag + "'" + source.name + "' has no BoatAuthoring - cannot clone.");
                UnityEngine.Object.DestroyImmediate(clone);
                return null;
            }

            oa.objectName = tier.ObjectName;
            oa.variation = 0;
            oa.rarity = tier.Rarity;

            LocalizationAuthoring loc = GetOrAdd<LocalizationAuthoring>(clone);
            loc.termKey = tier.ObjectName;

            InventoryItemAuthoring item = GetOrAdd<InventoryItemAuthoring>(clone);
            List<InventoryItemAuthoring.CraftingObject> recipe = new List<InventoryItemAuthoring.CraftingObject>();
            string recipeText = "";
            if (item.requiredObjectsToCraft != null)
            {
                foreach (InventoryItemAuthoring.CraftingObject ingredient in item.requiredObjectsToCraft)
                {
                    int amount = Mathf.CeilToInt(ingredient.amount * tier.Multiplier);
                    recipe.Add(new InventoryItemAuthoring.CraftingObject { objectName = ingredient.objectName, amount = amount });
                    recipeText += (recipeText.Length > 0 ? ", " : "") + amount + "x " + ingredient.objectName;
                }
            }
            item.requiredObjectsToCraft = recipe;
            if (item.sellValue > 0) item.sellValue = Mathf.RoundToInt(item.sellValue * tier.Multiplier);

            if (recipe.Count == 0)
            {
                // A free boat would be worse than no boat. Refuse this tier; the workbench hook only adds recipes
                // for clones that registered.
                Debug.LogError(Tag + tier.ObjectName + ": source prefab has NO crafting recipe to scale - tier NOT added.");
                UnityEngine.Object.DestroyImmediate(clone);
                return null;
            }

            float baseMultiplier = boat.speedMultiplier;
            if (baseMultiplier <= 0f)
            {
                Debug.LogWarning(Tag + "'" + source.name + "' has speedMultiplier " + baseMultiplier + " - treating as 1.");
                baseMultiplier = 1f;
            }
            boat.speedMultiplier = baseMultiplier * tier.Multiplier;

            Debug.Log(Tag + tier.ObjectName + ": speedMultiplier " + baseMultiplier + " -> " + boat.speedMultiplier
                      + ", recipe " + recipeText + ", rarity " + tier.Rarity + ", clone '" + clone.name + "', pin " + tier.PinnedID);
            return clone;
        }

        private static ObjectAuthoring ConvertToObjectAuthoring(GameObject clone, EntityMonoBehaviourData embd)
        {
            ObjectInfo info = embd.objectInfo;
            ObjectAuthoring oa = clone.AddComponent<ObjectAuthoring>();
            oa.initialAmount = info.initialAmount;
            oa.variation = info.variation;
            oa.variationIsDynamic = info.variationIsDynamic;
            oa.variationToToggleTo = info.variationToToggleTo;
            oa.objectType = info.objectType;
            oa.tags = info.tags != null ? new List<ObjectCategoryTag>(info.tags) : new List<ObjectCategoryTag>();
            oa.rarity = info.rarity;
            oa.salvageMultiplier = info.salvageMultiplier;
            oa.isCustomScenePrefab = info.isCustomScenePrefab;
            oa.additionalSprites = info.additionalSprites;
            if (info.prefabInfos != null && info.prefabInfos.Count > 0 && info.prefabInfos[0].prefab != null)
            {
                oa.graphicalPrefab = info.prefabInfos[0].prefab.gameObject;   // same visuals as the vanilla Speeder
            }

            InventoryItemAuthoring item = GetOrAdd<InventoryItemAuthoring>(clone);
            item.sellValue = info.sellValue;
            item.buyValueMultiplier = info.buyValueMultiplier;
            item.icon = info.icon;
            item.iconOffset = info.iconOffset;
            item.smallIcon = info.smallIcon;
            item.isStackable = info.isStackable;
            item.craftingTime = info.craftingTime;
            item.requiredObjectsToCraft = new List<InventoryItemAuthoring.CraftingObject>();
            if (info.requiredObjectsToCraft != null)
            {
                foreach (CraftingObject ingredient in info.requiredObjectsToCraft)
                {
                    item.requiredObjectsToCraft.Add(new InventoryItemAuthoring.CraftingObject
                    {
                        objectName = ingredient.objectID.ToString(),
                        amount = ingredient.amount
                    });
                }
            }

            LocalizationAuthoring loc = GetOrAdd<LocalizationAuthoring>(clone);
            loc.languageGenders = info.languageGenders;

            if (clone.GetComponent<PlaceableObjectAuthoring>() == null)
            {
                // Not expected (the Speeder is placeable on water and should carry its rules here). If the vanilla
                // prefab keeps them elsewhere, copy only the generic layout fields - do NOT invent placement rules
                // for water; a boat that places wrong is better caught in testing than silently misplaced.
                Debug.LogWarning(Tag + "vanilla Speeder has no PlaceableObjectAuthoring - copying generic layout fields only; placement rules may be wrong, verify in game.");
                PlaceableObjectAuthoring placeable = clone.AddComponent<PlaceableObjectAuthoring>();
                placeable.prefabTileSize = info.prefabTileSize;
                placeable.prefabCornerOffset = info.prefabCornerOffset;
                placeable.centerIsAtEntityPosition = info.centerIsAtEntityPosition;
                placeable.appearInMapUI = info.appearInMapUI;
                placeable.mapColor = info.mapColor;
            }
            return oa;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static void Subscribe()
        {
            if (_subscribed) return;
            API.Authoring.OnObjectTypeAdded += OnObjectTypeAdded;
            _subscribed = true;
        }

        /// <summary>Fires once per converted object type (after all conversion). Hooks the Boat Workbench.</summary>
        private static void OnObjectTypeAdded(Entity entity, GameObject authoring, EntityManager entityManager)
        {
            try
            {
                if (!entityManager.HasComponent<ObjectDataCD>(entity)) return;
                ObjectID id = entityManager.GetComponentData<ObjectDataCD>(entity).objectID;
                bool ours = false;
                foreach (Tier tier in Tiers)
                {
                    if (authoring.name == tier.CloneName)
                    {
                        ours = true;
                        MakeProperPrefab(entity, entityManager, tier.ObjectName);
                        if ((int)id == tier.PinnedID)
                        {
                            Debug.Log(Tag + tier.ObjectName + " converted at pinned ObjectID " + (int)id + " in world '" + entityManager.World.Name + "'");
                        }
                        else
                        {
                            Debug.LogError(Tag + "PINNED ID MISMATCH: " + tier.ObjectName + " converted as ObjectID " + (int)id
                                           + ", expected " + tier.PinnedID + " (world '" + entityManager.World.Name
                                           + "') - the pin did not take. DO NOT DEPLOY; boat identity would be hash-fragile.");
                        }
                        LogRecipeResolution(tier, authoring);
                    }
                }
                if (!ours && (int)id >= 40001 && (int)id <= 40004)
                {
                    Debug.LogError(Tag + "FOREIGN OBJECT AT PINNED ID: '" + authoring.name + "' converted as ObjectID " + (int)id
                                   + " (world '" + entityManager.World.Name + "') - collides with the TurboBoats pin range (40001-4).");
                }
                if (id == ObjectID.BoatWorkbench) AddRecipes(entity, entityManager);
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "OnObjectTypeAdded failed: " + e);
            }
        }

        /// <summary>
        /// Every ingredient name must resolve to a real ObjectID by the time the database is built, or the item
        /// crafts for free with an empty requirement list. MinecartTurbo only ever verified the recipe COUNT on the
        /// forge; log the resolved content here so the boot log proves the recipes are real.
        /// </summary>
        private static void LogRecipeResolution(Tier tier, GameObject authoring)
        {
            InventoryItemAuthoring item = authoring.GetComponent<InventoryItemAuthoring>();
            if (item == null || item.requiredObjectsToCraft == null || item.requiredObjectsToCraft.Count == 0)
            {
                Debug.LogError(Tag + tier.ObjectName + ": no recipe on converted authoring - would craft for FREE. DO NOT DEPLOY.");
                return;
            }
            string resolved = "";
            bool broken = false;
            foreach (InventoryItemAuthoring.CraftingObject ingredient in item.requiredObjectsToCraft)
            {
                ObjectID ingredientId = API.Authoring.GetObjectID(ingredient.objectName);
                if (ingredientId == ObjectID.None) broken = true;
                resolved += (resolved.Length > 0 ? ", " : "") + ingredient.amount + "x " + ingredient.objectName + "=" + (int)ingredientId;
            }
            if (broken)
            {
                Debug.LogError(Tag + tier.ObjectName + " recipe has UNRESOLVED ingredient(s): " + resolved + " - DO NOT DEPLOY.");
            }
            else
            {
                Debug.Log(Tag + tier.ObjectName + " recipe resolved: " + resolved);
            }
        }

        /// <summary>
        /// Same fix MinecartTurbo shipped in 1.0.1: the game's ConversionManager only tags a converted entity with
        /// Unity.Entities.Prefab when the source GameObject is an asset outside any scene. Our clones are runtime
        /// scene objects, so without this every world gets LIVE boat entities that never register in the NetCode
        /// ghost collection, and clients crash with "Could not find ghost type in the collection" the moment their
        /// world ticks. This handler runs BEFORE GhostPostConverter, so tagging here makes ghost conversion treat
        /// the boats exactly like vanilla prefab assets.
        /// </summary>
        private static void MakeProperPrefab(Entity entity, EntityManager entityManager, string objectName)
        {
            bool wasLive = !entityManager.HasComponent<Prefab>(entity);
            Entity[] linked = null;
            if (entityManager.HasBuffer<LinkedEntityGroup>(entity))
            {
                DynamicBuffer<LinkedEntityGroup> buffer = entityManager.GetBuffer<LinkedEntityGroup>(entity);
                linked = new Entity[buffer.Length];
                for (int i = 0; i < buffer.Length; i++) linked[i] = buffer[i].Value;
            }
            if (wasLive) entityManager.AddComponent<Prefab>(entity);
            if (linked == null)
            {
                // No additional entities were attached by converters in this world: prefab groups always contain
                // themselves at index 0 (mirrors the ConversionManager asset branch).
                entityManager.AddBuffer<LinkedEntityGroup>(entity).Add(entity);
            }
            else
            {
                foreach (Entity other in linked)
                {
                    if (other != entity && !entityManager.HasComponent<Prefab>(other))
                    {
                        entityManager.AddComponent<Prefab>(other);
                    }
                }
                if (linked.Length == 0 || linked[0] != entity)
                {
                    DynamicBuffer<LinkedEntityGroup> buffer = entityManager.GetBuffer<LinkedEntityGroup>(entity);
                    buffer.Clear();
                    buffer.Add(entity);
                    foreach (Entity other in linked)
                    {
                        if (other != entity) buffer.Add(other);
                    }
                }
            }
            Debug.Log(Tag + objectName + (wasLive ? ": tagged as Prefab" : ": already a Prefab")
                      + ", linked entities " + ((linked != null) ? linked.Length : 1) + ", world '" + entityManager.World.Name + "'");
        }

        private static void AddRecipes(Entity workbench, EntityManager entityManager)
        {
            if (!entityManager.HasBuffer<CanCraftObjectsBuffer>(workbench))
            {
                Debug.LogWarning(Tag + "Boat Workbench has no CanCraftObjectsBuffer - recipes NOT added.");
                return;
            }
            DynamicBuffer<CanCraftObjectsBuffer> recipes = entityManager.GetBuffer<CanCraftObjectsBuffer>(workbench);
            int added = 0;
            foreach (Tier tier in Tiers)
            {
                ObjectID id = API.Authoring.GetObjectID(tier.ObjectName);
                if (id == ObjectID.None)
                {
                    Debug.LogWarning(Tag + tier.ObjectName + " has no ObjectID yet - recipe NOT added.");
                    continue;
                }
                bool registered = false;
                foreach (GameObject clone in _clones)
                {
                    if (clone != null && clone.name == tier.CloneName) { registered = true; break; }
                }
                if (!registered) continue;   // tier was refused at clone time (e.g. no recipe) - don't offer it
                bool present = false;
                for (int i = 0; i < recipes.Length; i++)
                {
                    if (recipes[i].objectID == id) { present = true; break; }
                }
                if (present) continue;
                CanCraftObjectsBuffer entry = new CanCraftObjectsBuffer
                {
                    objectID = id,
                    amount = 1,
                    entityAmountToConsume = 0,
                    allowCraftingNone = false,
                    craftingTimeOverride = 0f   // 0 = use the item's own craftingTime
                };
                if (recipes.Length > 0 && recipes[recipes.Length - 1].objectID == ObjectID.None)
                {
                    recipes[recipes.Length - 1] = entry;   // fill an empty trailing slot rather than appending after it
                }
                else
                {
                    recipes.Add(entry);
                }
                added++;
            }
            Debug.Log(Tag + "Boat Workbench: +" + added + " recipe(s), " + recipes.Length + " total, world '" + entityManager.World.Name + "'");
        }
    }
}
