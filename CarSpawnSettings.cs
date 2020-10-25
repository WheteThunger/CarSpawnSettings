using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Rust.Modular;

namespace Oxide.Plugins
{
    [Info("Car Spawn Settings", "WhiteThunder", "1.1.0")]
    [Description("Allows modular cars to spawn with configurable health, fuel, and engine parts.")]
    internal class CarSpawnSettings : CovalencePlugin
    {
        #region Fields

        private Configuration pluginConfig;

        #endregion

        #region Hooks

        private void OnEntitySpawned(ModularCar car)
        {
            if (Rust.Application.isLoadingSave) return;
            if (!pluginConfig.IncludeChassis && !car.spawnSettings.useSpawnSettings) return;

            timer.Once(0.5f, () =>
            {
                if (car == null) return;
                if (!pluginConfig.IncludeOwnedCars && car.OwnerID != 0) return;
                if (BootstrapWasBlocked(car)) return;

                BootstrapAfterModules(car);
            });
        }

        #endregion

        #region Helper Methods

        private bool BootstrapWasBlocked(ModularCar car)
        {
            object hookResult = Interface.CallHook("CanBootstrapSpawnedCar", car);
            return hookResult is bool && (bool)hookResult == false;
        }

        private void BootstrapAfterModules(ModularCar car)
        {
            MaybeAddFuel(car);
            MaybeRepairModules(car);
            MaybeAddEngineParts(car);
        }

        private void MaybeRepairModules(ModularCar car)
        {
            var healthPercentage = pluginConfig.HealthPercentage;
            if (healthPercentage < 0 || healthPercentage > 100) return;
            healthPercentage /= 100;

            if (car.Health() < car.MaxHealth() * healthPercentage)
            {
                car.SetHealth(car.MaxHealth() * healthPercentage);
                car.SendNetworkUpdate();
            }

            foreach (var module in car.AttachedModuleEntities)
            {
                if (module.Health() < module.MaxHealth() * healthPercentage)
                {
                    module.SetHealth(module.MaxHealth() * healthPercentage);
                    module.SendNetworkUpdate();
                }
            }
        }

        private void MaybeAddFuel(ModularCar car)
        {
            var fuelAmount = pluginConfig.GetPossiblyRandomFuelAmount();
            if (fuelAmount == 0) return;

            var fuelContainer = car.fuelSystem.GetFuelContainer();
            if (fuelAmount < 0)
                fuelAmount = fuelContainer.allowedItem.stackable;

            var fuelItem = fuelContainer.inventory.FindItemByItemID(fuelContainer.allowedItem.itemid);
            if (fuelItem == null)
                fuelContainer.inventory.AddItem(fuelContainer.allowedItem, fuelAmount);
        }

        private void MaybeAddEngineParts(ModularCar car)
        {
            if (!pluginConfig.CanHaveEngineParts()) return;

            foreach (var module in car.AttachedModuleEntities)
            {
                var engineModule = module as VehicleModuleEngine;
                if (engineModule != null)
                {
                    var engineStorage = engineModule.GetContainer() as EngineStorage;
                    if (engineStorage != null)
                    {
                        AddPartsToEngineStorage(engineStorage);
                        engineModule.RefreshPerformanceStats(engineStorage);
                    }
                }
            }
        }

        private void AddPartsToEngineStorage(EngineStorage engineStorage)
        {
            if (engineStorage.inventory == null) return;

            var inventory = engineStorage.inventory;
            for (var i = 0; i < inventory.capacity; i++)
            {
                // Do nothing if there is an existing engine part
                var item = inventory.GetSlot(i);
                if (item != null) continue;

                var tier = pluginConfig.GetPossiblyRandomEnginePartTier();
                if (tier > 0)
                    TryAddEngineItem(engineStorage, i, tier);
            }
        }

        private bool TryAddEngineItem(EngineStorage engineStorage, int slot, int tier)
        {
            ItemModEngineItem output;
            if (!engineStorage.allEngineItems.TryGetItem(tier, engineStorage.slotTypes[slot], out output)) return false;

            var component = output.GetComponent<ItemDefinition>();
            var item = ItemManager.Create(component);
            if (item == null) return false;
            
            item.conditionNormalized = pluginConfig.GetRandomNormalizedCondition();
            item.MoveToContainer(engineStorage.inventory, slot, allowStack: false);
            return true;
        }

        #endregion

        #region Configuration

        private Configuration GetDefaultConfig() => new Configuration();

        internal class Configuration : SerializableConfiguration
        {
            [JsonProperty("EnginePartsTier", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int EnginePartsTier = 0;

            [JsonProperty("FuelAmount", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int FuelAmount = 0;

            [JsonProperty("EngineParts")]
            public EnginePartConfiguration EngineParts = new EnginePartConfiguration();

            [JsonProperty("MinFuelAmount")]
            public int MinFuelAmount = 0;

            [JsonProperty("MaxFuelAmount")]
            public int MaxFuelAmount = 0;

            [JsonProperty("HealthPercentage")]
            public float HealthPercentage = -1;

            [JsonProperty("IncludeChassis")]
            public bool IncludeChassis = false;

            [JsonProperty("IncludeOwnedCars")]
            public bool IncludeOwnedCars = false;

            public bool CanHaveEngineParts() =>
                EnginePartsTier > 0 ||
                EngineParts.Tier1Chance > 0 ||
                EngineParts.Tier2Chance > 0 ||
                EngineParts.Tier3Chance > 0;

            public int GetPossiblyRandomEnginePartTier()
            {
                if (EnginePartsTier != 0)
                    return EnginePartsTier;

                var tierRoll = UnityEngine.Random.Range(0, 100);
                if (tierRoll < EngineParts.Tier3Chance)
                    return 3;
                else if (tierRoll < EngineParts.Tier2Chance)
                    return 2;
                else if (tierRoll < EngineParts.Tier1Chance)
                    return 1;
                else
                    return 0;
            }

            public float GetRandomNormalizedCondition() =>
                EngineParts.MinConditionPercent == 100f
                ? 1.0f
                : UnityEngine.Mathf.Round(UnityEngine.Random.Range(EngineParts.MinConditionPercent, EngineParts.MaxConditionPercent)) / 100f;

            public int GetPossiblyRandomFuelAmount()
            {
                if (FuelAmount != 0)
                    return FuelAmount;

                if (MinFuelAmount == 0 && MaxFuelAmount == 0)
                    return 0;

                return UnityEngine.Random.Range(MinFuelAmount, MaxFuelAmount + 1);
            }
        }

        internal class EnginePartConfiguration
        {
            [JsonProperty("Tier1Chance")]
            public int Tier1Chance = 0;

            [JsonProperty("Tier2Chance")]
            public int Tier2Chance = 0;

            [JsonProperty("Tier3Chance")]
            public int Tier3Chance = 0;

            [JsonProperty("MinConditionPercent")]
            public float MinConditionPercent = 100.0f;

            [JsonProperty("MaxConditionPercent")]
            public float MaxConditionPercent = 100.0f;
        }

        #endregion

        #region Configuration Boilerplate

        internal class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        internal static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => pluginConfig = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                pluginConfig = Config.ReadObject<Configuration>();
                if (pluginConfig == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(pluginConfig))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(pluginConfig, true);
        }

        #endregion
    }
}
