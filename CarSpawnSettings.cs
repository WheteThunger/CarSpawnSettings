using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Oxide.Core;
using Rust.Modular;

namespace Oxide.Plugins
{
    [Info("Car Spawn Settings", "WhiteThunder", "2.1.0")]
    [Description("Allows modular cars to spawn with configurable modules, health, fuel, and engine parts.")]
    internal class CarSpawnSettings : CovalencePlugin
    {
        #region Fields

        private static CarSpawnSettings pluginInstance;

        private Configuration _pluginConfig;
        private object _boxedFalse = false;

        #endregion

        #region Hooks

        private void Init()
        {
            pluginInstance = this;

            // Make sure presets are ready as soon as possible
            // Cars can spawn while generating a new map before OnServerInitialized()
            _pluginConfig.ModulePresetMap.ParseAndValidatePresets();
        }

        private void Unload()
        {
            pluginInstance = null;
        }

        private object OnVehicleModulesAssign(ModularCar car)
        {
            var presetConfiguration = GetPresetConfigurationForSockets(car.TotalSockets);

            var vanillaPresets = car.spawnSettings.configurationOptions;
            var numCustomPresets = presetConfiguration?.NormalizedPresets.Length ?? 0;

            var numTotalPresets = numCustomPresets;
            if (presetConfiguration != null && presetConfiguration.UseVanillaPresets)
                numTotalPresets += vanillaPresets.Length;

            if (numTotalPresets > 0)
            {
                int[] moduleIds;

                var randomPresetIndex = UnityEngine.Random.Range(0, numTotalPresets);
                if (randomPresetIndex < numCustomPresets)
                    moduleIds = presetConfiguration.NormalizedPresets[randomPresetIndex];
                else
                    moduleIds = VanillaPresetToModuleIds(vanillaPresets[randomPresetIndex - numCustomPresets].socketItemDefs);

                AddCarModules(car, moduleIds);
            }

            NextTick(() =>
            {
                if (car == null || car.IsDestroyed)
                    return;

                ProcessCar(car);
            });

            return _boxedFalse;
        }

        #endregion

        #region Commands

        [Command("carspawnsettings.fillcars")]
        private void CommandFillCars(IPlayer player)
        {
            if (!player.IsAdmin)
                return;

            var carsProcessed = 0;

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var car = entity as ModularCar;
                if (car == null || car.IsDestroyed)
                    continue;

                ProcessCar(car);
                carsProcessed++;
            }

            player.Reply(GetMessage(player.Id, Lang.FillSuccess, carsProcessed));
        }

        #endregion

        #region Helper Methods

        private bool BootstrapWasBlocked(ModularCar car)
        {
            object hookResult = Interface.CallHook("CanBootstrapSpawnedCar", car);
            return hookResult is bool && (bool)hookResult == false;
        }

        private void ProcessCar(ModularCar car)
        {
            if (car.OwnerID != 0 || BootstrapWasBlocked(car))
                return;

            BootstrapAfterModules(car);
        }

        private ModulePresetConfiguration GetPresetConfigurationForSockets(int totalSockets)
        {
            if (totalSockets == 4)
                return _pluginConfig.ModulePresetMap.PresetsFor4Sockets;
            else if (totalSockets == 3)
                return _pluginConfig.ModulePresetMap.PresetsFor3Sockets;
            else if (totalSockets == 2)
                return _pluginConfig.ModulePresetMap.PresetsFor2Sockets;
            else
                return null;
        }

        private int[] VanillaPresetToModuleIds(ItemModVehicleModule[] modules)
        {
            var moduleIds = new List<int>();

            foreach (var module in modules)
            {
                if (module == null)
                    continue;

                var itemDefinition = module.GetComponent<ItemDefinition>();
                if (itemDefinition == null)
                    continue;

                moduleIds.Add(itemDefinition.itemid);

                for (var i = 0; i < module.SocketsTaken - 1; i++)
                {
                    moduleIds.Add(0);
                }
            }

            return moduleIds.ToArray();
        }

        private void AddCarModules(ModularCar car, int[] moduleIDs)
        {
            for (int socketIndex = 0; socketIndex < car.TotalSockets && socketIndex < moduleIDs.Length; socketIndex++)
            {
                var desiredItemID = moduleIDs[socketIndex];
                var existingItem = car.Inventory.ModuleContainer.GetSlot(socketIndex);

                // We are using 0 to represent an empty socket which we skip
                if (existingItem == null && desiredItemID != 0)
                {
                    var moduleItem = ItemManager.CreateByItemID(desiredItemID);
                    if (moduleItem != null)
                    {
                        moduleItem.conditionNormalized = _pluginConfig.GetRandomNormalizedModuleCondition();

                        if (!car.TryAddModule(moduleItem, socketIndex))
                        {
                            moduleItem.Remove();
                        }
                    }
                }
            }
        }

        private void BootstrapAfterModules(ModularCar car)
        {
            MaybeAddFuel(car);
            MaybeAddEngineParts(car);
        }

        private void MaybeAddFuel(ModularCar car)
        {
            var fuelAmount = _pluginConfig.GetPossiblyRandomFuelAmount();
            if (fuelAmount == 0)
                return;

            var fuelContainer = car.GetFuelSystem().GetFuelContainer();
            if (fuelAmount < 0)
                fuelAmount = fuelContainer.allowedItem.stackable;

            var fuelItem = fuelContainer.inventory.FindItemByItemID(fuelContainer.allowedItem.itemid);
            if (fuelItem == null)
                fuelContainer.inventory.AddItem(fuelContainer.allowedItem, fuelAmount);
        }

        private void MaybeAddEngineParts(ModularCar car)
        {
            if (!_pluginConfig.CanHaveEngineParts())
                return;

            foreach (var child in car.children)
            {
                var engineModule = child as VehicleModuleEngine;
                if (engineModule == null)
                    continue;

                var engineStorage = engineModule.GetContainer() as EngineStorage;
                if (engineStorage == null || !engineStorage.inventory.IsEmpty())
                    continue;

                AddPartsToEngineStorage(engineStorage);
                engineModule.RefreshPerformanceStats(engineStorage);
            }
        }

        private void AddPartsToEngineStorage(EngineStorage engineStorage)
        {
            if (engineStorage.inventory == null)
                return;

            var inventory = engineStorage.inventory;
            for (var i = 0; i < inventory.capacity; i++)
            {
                // Do nothing if there is an existing engine part
                var item = inventory.GetSlot(i);
                if (item != null)
                    continue;

                var tier = _pluginConfig.GetPossiblyRandomEnginePartTier();
                if (tier > 0)
                {
                    TryAddEngineItem(engineStorage, i, tier);
                }
            }
        }

        private bool TryAddEngineItem(EngineStorage engineStorage, int slot, int tier)
        {
            ItemModEngineItem output;
            if (!engineStorage.allEngineItems.TryGetItem(tier, engineStorage.slotTypes[slot], out output))
                return false;

            var component = output.GetComponent<ItemDefinition>();
            var item = ItemManager.Create(component);
            if (item == null)
                return false;

            item.conditionNormalized = _pluginConfig.GetRandomNormalizedPartCondition();
            item.MoveToContainer(engineStorage.inventory, slot, allowStack: false);
            return true;
        }

        #endregion

        #region Configuration

        private Configuration GetDefaultConfig() => new Configuration();

        private class Configuration : SerializableConfiguration
        {
            // Deprecated
            [JsonProperty("EnginePartsTier", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int EnginePartsTier = 0;

            // Deprecated
            [JsonProperty("FuelAmount", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int FuelAmount = 0;

            [JsonProperty("EngineParts")]
            public EnginePartConfiguration EngineParts = new EnginePartConfiguration();

            [JsonProperty("MinFuelAmount")]
            public int MinFuelAmount = 0;

            [JsonProperty("MaxFuelAmount")]
            public int MaxFuelAmount = 0;

            [JsonProperty("MinHealthPercent")]
            public float MinHealthPercent = 15.0f;

            [JsonProperty("MaxHealthPercent")]
            public float MaxHealthPercent = 50.0f;

            // Deprecated
            [JsonProperty("HealthPercentage", DefaultValueHandling = DefaultValueHandling.Ignore)]
            [DefaultValue(-1.0f)]
            public float HealthPercentage = -1;

            [JsonProperty("ModulePresets")]
            public ModulePresetMap ModulePresetMap = new ModulePresetMap();

            public float GetRandomNormalizedModuleCondition() =>
                GetRandomNormalizedCondition(HealthPercentage != -1 ? HealthPercentage : MinHealthPercent, MaxHealthPercent);

            public bool CanHaveEngineParts() =>
                EnginePartsTier > 0 ||
                EngineParts.Tier1Chance > 0 ||
                EngineParts.Tier2Chance > 0 ||
                EngineParts.Tier3Chance > 0;

            public int GetPossiblyRandomEnginePartTier()
            {
                if (EnginePartsTier != 0)
                    return EnginePartsTier;

                if (EngineParts.Tier3Chance > 0 && UnityEngine.Random.Range(0, 100) < EngineParts.Tier3Chance)
                    return 3;
                else if (EngineParts.Tier2Chance > 0 && UnityEngine.Random.Range(0, 100) < EngineParts.Tier2Chance)
                    return 2;
                else if (EngineParts.Tier1Chance > 0 && UnityEngine.Random.Range(0, 100) < EngineParts.Tier1Chance)
                    return 1;
                else
                    return 0;
            }

            public float GetRandomNormalizedPartCondition() =>
                GetRandomNormalizedCondition(EngineParts.MinConditionPercent, EngineParts.MaxConditionPercent);

            public int GetPossiblyRandomFuelAmount()
            {
                if (FuelAmount != 0)
                    return FuelAmount;

                if (MinFuelAmount == 0 && MaxFuelAmount == 0)
                    return 0;

                return UnityEngine.Random.Range(MinFuelAmount, MaxFuelAmount + 1);
            }

            public float GetRandomNormalizedCondition(float minConditon, float maxCondition) =>
                minConditon == 100f
                ? 1.0f
                : UnityEngine.Mathf.Round(UnityEngine.Random.Range(minConditon, Math.Max(minConditon, maxCondition))) / 100f;
        }

        private class EnginePartConfiguration
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

        private class ModulePresetMap
        {
            [JsonProperty("2Sockets")]
            public ModulePresetConfiguration PresetsFor2Sockets = new ModulePresetConfiguration();

            [JsonProperty("3Sockets")]
            public ModulePresetConfiguration PresetsFor3Sockets = new ModulePresetConfiguration();

            [JsonProperty("4Sockets")]
            public ModulePresetConfiguration PresetsFor4Sockets = new ModulePresetConfiguration();

            public void ParseAndValidatePresets()
            {
                // ItemManager may not have initialized yet
                // Safe to call multiple times since it will only initialize once
                ItemManager.Initialize();

                PresetsFor2Sockets.ParseAndValidatePresets();
                PresetsFor3Sockets.ParseAndValidatePresets();
                PresetsFor4Sockets.ParseAndValidatePresets();
            }
        }

        private class ModulePresetConfiguration
        {
            [JsonProperty("UseVanillaPresets")]
            public bool UseVanillaPresets = true;

            [JsonProperty("CustomPresets")]
            public object[][] CustomPresets = new object[0][];

            [JsonIgnore]
            public int[][] NormalizedPresets = new int[0][];

            public void ParseAndValidatePresets()
            {
                var presets = new List<int[]>();
                foreach (var presetModules in CustomPresets)
                {
                    var moduleIds = ParseModules(presetModules);
                    if (moduleIds.Length > 0)
                        presets.Add(moduleIds);
                }
                NormalizedPresets = presets.ToArray();
            }

            private int[] ParseModules(object[] moduleArray)
            {
                var moduleIDList = new List<int>();

                foreach (var module in moduleArray)
                {
                    ItemDefinition itemDef;

                    if (module is int || module is long)
                    {
                        var moduleInt = module is long ? Convert.ToInt32((long)module) : (int)module;
                        if (moduleInt == 0)
                        {
                            moduleIDList.Add(0);
                            continue;
                        }
                        itemDef = ItemManager.FindItemDefinition(moduleInt);
                    }
                    else if (module is string)
                    {
                        int parsedItemId;
                        if (int.TryParse(module as string, out parsedItemId))
                        {
                            if (parsedItemId == 0)
                            {
                                moduleIDList.Add(0);
                                continue;
                            }
                            itemDef = ItemManager.FindItemDefinition(parsedItemId);
                        }
                        else
                            itemDef = ItemManager.FindItemDefinition(module as string);
                    }
                    else
                    {
                        pluginInstance.LogWarning("Unable to parse module id or name: '{0}'", module);
                        continue;
                    }

                    if (itemDef == null)
                    {
                        pluginInstance.LogWarning("No item definition found for: '{0}'", module);
                        continue;
                    }

                    var vehicleModule = itemDef.GetComponent<ItemModVehicleModule>();
                    if (vehicleModule == null)
                    {
                        pluginInstance.LogWarning("No vehicle module found for item: '{0}'", module);
                        continue;
                    }

                    moduleIDList.Add(itemDef.itemid);

                    // Normalize module IDs by adding 0s after the module if it takes multiple sockets
                    for (var i = 0; i < vehicleModule.SocketsTaken - 1; i++)
                        moduleIDList.Add(0);
                }

                return moduleIDList.ToArray();
            }
        }

        #region Configuration Boilerplate

        private class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
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

        protected override void LoadDefaultConfig() => _pluginConfig = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _pluginConfig = Config.ReadObject<Configuration>();
                if (_pluginConfig == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_pluginConfig))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_pluginConfig, true);
        }

        #endregion

        #endregion

        #region Localization

        private string GetMessage(string userId, string messageName, params object[] args)
        {
            var message = lang.GetMessage(messageName, this, userId);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        private class Lang
        {
            public const string FillSuccess = "Fill.Success";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.FillSuccess] = "Processed {0} cars.",
            }, this, "en");
        }

        #endregion
    }
}
