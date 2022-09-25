using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Rust.Modular;

namespace Oxide.Plugins
{
    [Info("Car Spawn Settings", "WhiteThunder", "2.3.0")]
    [Description("Allows modular cars to spawn with configurable modules, health, fuel, and engine parts.")]
    internal class CarSpawnSettings : CovalencePlugin
    {
        #region Fields

        private readonly object False = false;
        private Configuration _pluginConfig;
        private VanillaPresetCache _vanillaPresetCache = new VanillaPresetCache();

        #endregion

        #region Hooks

        private void Init()
        {
            // Make sure presets are ready as soon as possible
            // Cars can spawn while generating a new map before OnServerInitialized()
            _pluginConfig.Init(this);
        }

        private object OnVehicleModulesAssign(ModularCar car)
        {
            var vanillaPresets = car.spawnSettings.configurationOptions;
            if (vanillaPresets.Length == 1)
            {
                // Ignore the car if there's only 1 preset because it's probably a spawnable preset.
                return null;
            }

            var presetConfiguration = _pluginConfig.ModulePresetMap.GetPresetConfigurationForSockets(car.TotalSockets);
            var numCustomPresets = presetConfiguration?.CustomPresets.Length ?? 0;

            var numTotalPresets = numCustomPresets;
            if (presetConfiguration != null && presetConfiguration.UseVanillaPresets)
            {
                numTotalPresets += vanillaPresets.Length;
            }

            if (numTotalPresets > 0)
            {
                IList<IModuleDefinition> moduleDefinitions;

                var randomPresetIndex = UnityEngine.Random.Range(0, numTotalPresets);
                if (randomPresetIndex < numCustomPresets)
                {
                    moduleDefinitions = presetConfiguration.CustomPresets[randomPresetIndex];
                }
                else
                {
                    moduleDefinitions = _vanillaPresetCache.GetModulePreset(
                        vanillaPresets[randomPresetIndex - numCustomPresets]
                    );
                }

                AddCarModules(car, moduleDefinitions);
            }

            NextTick(() =>
            {
                if (car == null || car.IsDestroyed)
                    return;

                ProcessCar(car);
            });

            return False;
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

        private void AddCarModules(ModularCar car, IList<IModuleDefinition> modulePreset)
        {
            for (int i = 0; i < car.TotalSockets && i < modulePreset.Count; i++)
            {
                var moduleDefinition = modulePreset[i];
                var existingItem = car.Inventory.ModuleContainer.GetSlot(i);
                if (existingItem != null)
                    continue;

                var moduleItem = moduleDefinition.Create();
                if (moduleItem == null)
                    continue;

                moduleItem.conditionNormalized = _pluginConfig.RandomizeModuleCondition();

                if (!car.TryAddModule(moduleItem, i))
                {
                    moduleItem.Remove();
                    break;
                }

                // Skip ahead if the current module takes multiple sockets.
                i += moduleDefinition.NumSockets - 1;
            }
        }

        private void BootstrapAfterModules(ModularCar car)
        {
            MaybeAddFuel(car);
            MaybeAddEngineParts(car);
        }

        private void MaybeAddFuel(ModularCar car)
        {
            var fuelAmount = _pluginConfig.RandomizeFuelAmount();
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

                var tier = _pluginConfig.RandomizeEnginePartTier();
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

            item.conditionNormalized = _pluginConfig.RandomizePartCondition();
            item.MoveToContainer(engineStorage.inventory, slot, allowStack: false);
            return true;
        }

        #endregion

        #region Module Definitions

        private interface IModuleDefinition
        {
            Item Create();
            int NumSockets { get; }
        }

        private class VanillaModuleDefinition : IModuleDefinition
        {
            public int NumSockets { get; private set; }

            private ItemDefinition _itemDefinition;

            public VanillaModuleDefinition(ItemModVehicleModule socketItemDefinition)
            {
                NumSockets = socketItemDefinition.SocketsTaken;
                _itemDefinition = socketItemDefinition.GetComponent<ItemDefinition>();
            }

            public Item Create()
            {
                if ((object)_itemDefinition == null)
                    return null;

                return ItemManager.Create(_itemDefinition);
            }
        }

        private class VanillaPresetCache
        {
            private Dictionary<ModularCarPresetConfig, IList<IModuleDefinition>> _cache = new Dictionary<ModularCarPresetConfig, IList<IModuleDefinition>>();

            public IList<IModuleDefinition> GetModulePreset(ModularCarPresetConfig presetConfig)
            {
                IList<IModuleDefinition> modules;
                if (!_cache.TryGetValue(presetConfig, out modules))
                {
                    var moduleDefinitionList = new List<IModuleDefinition>();

                    foreach (var socketItemDefinition in presetConfig.socketItemDefs)
                    {
                        if (socketItemDefinition == null)
                            continue;

                        moduleDefinitionList.Add(new VanillaModuleDefinition(socketItemDefinition));
                    }

                    modules = moduleDefinitionList.ToArray();
                    _cache[presetConfig] = modules;
                }

                return modules;
            }
        }

        #endregion

        #region Configuration

        private Configuration GetDefaultConfig() => new Configuration();

        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("EnginePartsTier")]
            private int DeprecatedEnginePartsTier
            {
                set
                {
                    if (value == 1)
                    {
                        EngineParts.Tier1Chance = 100;
                    }
                    else if (value == 2)
                    {
                        EngineParts.Tier2Chance = 100;
                    }
                    else if (value == 3)
                    {
                        EngineParts.Tier3Chance = 100;
                    }
                }
            }

            [JsonProperty("FuelAmount", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private int DeprecatedFuelAmount = 0;

            [JsonProperty("Engine parts")]
            public EnginePartConfiguration EngineParts = new EnginePartConfiguration();
            [JsonProperty("EngineParts")]
            public EnginePartConfiguration DeprecatedEngineParts { set { EngineParts = value; } }

            [JsonProperty("Min fuel amount")]
            public int MinFuelAmount = 0;
            [JsonProperty("MinFuelAmount")]
            public int DeprecatedMinFuelAmount { set { MinFuelAmount = value;} }

            [JsonProperty("Max fuel amount")]
            public int MaxFuelAmount = 0;
            [JsonProperty("MaxFuelAmount")]
            private int DeprecatedMaxFuelAmount { set { MaxFuelAmount = value;} }

            [JsonProperty("Min health percent")]
            public float MinHealthPercent = 15.0f;
            [JsonProperty("MinHealthPercent")]
            private float DeprecatedMinHealthPercent { set { MinHealthPercent = value;} }

            [JsonProperty("Max health percent")]
            public float MaxHealthPercent = 50.0f;
            [JsonProperty("MaxHealthPercent")]
            private float DeprecatedMaxHealthPercent { set { MaxHealthPercent = value;} }
            [JsonProperty("HealthPercentage")]
            private float DeprecatedHealthPercentage
            {
                set
                {
                    MinHealthPercent = value;
                    MaxHealthPercent = value;
                }
            }

            [JsonProperty("Module presets")]
            public ModulePresetMap ModulePresetMap = new ModulePresetMap();
            [JsonProperty("ModulePresets")]
            private ModulePresetMap DeprecatedModulePresetMap { set { ModulePresetMap = value; } }

            public void Init(CarSpawnSettings plugin)
            {
                ModulePresetMap.Init(plugin);
            }

            public float RandomizeModuleCondition()
            {
                return RandomizeCondition(MinHealthPercent, MaxHealthPercent);
            }

            public int RandomizeFuelAmount()
            {
                if (DeprecatedFuelAmount != 0)
                    return DeprecatedFuelAmount;

                if (MinFuelAmount == 0 && MaxFuelAmount == 0)
                    return 0;

                if (MinFuelAmount == MaxFuelAmount)
                    return MinFuelAmount;

                return UnityEngine.Random.Range(MinFuelAmount, MaxFuelAmount + 1);
            }

            public bool CanHaveEngineParts() =>
                EngineParts.Tier1Chance > 0
                || EngineParts.Tier2Chance > 0
                || EngineParts.Tier3Chance > 0;

            public int RandomizeEnginePartTier()
            {
                if (EngineParts.Tier3Chance > 0
                    && (EngineParts.Tier3Chance >= 100 || UnityEngine.Random.Range(0, 100) < EngineParts.Tier3Chance))
                    return 3;

                if (EngineParts.Tier2Chance > 0
                    && (EngineParts.Tier2Chance >= 100 || UnityEngine.Random.Range(0, 100) < EngineParts.Tier2Chance))
                    return 2;

                if (EngineParts.Tier1Chance > 0
                    && (EngineParts.Tier1Chance >= 100 || UnityEngine.Random.Range(0, 100) < EngineParts.Tier1Chance))
                    return 1;

                return 0;
            }

            public float RandomizePartCondition()
            {
                return RandomizeCondition(
                    EngineParts.MinConditionPercent,
                    EngineParts.MaxConditionPercent
                );
            }

            private float RandomizeCondition(float minPercent, float maxPercent)
            {
                if (minPercent >= 100)
                    return 1;

                return UnityEngine.Mathf.Round(
                    UnityEngine.Random.Range(minPercent, Math.Max(minPercent, maxPercent))
                ) / 100f;
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class EnginePartConfiguration
        {
            [JsonProperty("Tier 1 chance")]
            public int Tier1Chance = 0;
            [JsonProperty("Tier1Chance")]
            private int DeprecatedTier1Chance { set { Tier1Chance = value; } }

            [JsonProperty("Tier 2 chance")]
            public int Tier2Chance = 0;
            [JsonProperty("Tier2Chance")]
            private int DeprecatedTier2Chance { set { Tier2Chance = value; } }

            [JsonProperty("Tier 3 chance")]
            public int Tier3Chance = 0;
            [JsonProperty("Tier3Chance")]
            private int DeprecatedTier3Chance { set { Tier3Chance = value; } }

            [JsonProperty("Min condition percent")]
            public float MinConditionPercent = 100f;
            [JsonProperty("MinConditionPercent")]
            private float DeprecatedMinConditionPercent { set { MinConditionPercent = value; } }

            [JsonProperty("Max condition percent")]
            public float MaxConditionPercent = 100f;
            [JsonProperty("MaxConditionPercent")]
            private float DeprecatedMaxConditionPercent { set { MaxConditionPercent = value; } }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class ModulePresetMap
        {
            [JsonProperty("2 sockets")]
            public ModulePresetConfiguration PresetsFor2Sockets = new ModulePresetConfiguration();
            [JsonProperty("2Sockets")]
            private ModulePresetConfiguration DeprecatedPresetsFor2Sockets { set { PresetsFor2Sockets = value; } }

            [JsonProperty("3 sockets")]
            public ModulePresetConfiguration PresetsFor3Sockets = new ModulePresetConfiguration();
            [JsonProperty("3Sockets")]
            private ModulePresetConfiguration DeprecatedPresetsFor3Sockets { set { PresetsFor3Sockets = value; } }

            [JsonProperty("4 sockets")]
            public ModulePresetConfiguration PresetsFor4Sockets = new ModulePresetConfiguration();
            [JsonProperty("4Sockets")]
            private ModulePresetConfiguration DeprecatedPresetsFor4Sockets { set { PresetsFor4Sockets = value; } }

            public void Init(CarSpawnSettings plugin)
            {
                PresetsFor2Sockets.Init(plugin);
                PresetsFor3Sockets.Init(plugin);
                PresetsFor4Sockets.Init(plugin);
            }

            public ModulePresetConfiguration GetPresetConfigurationForSockets(int totalSockets)
            {
                if (totalSockets == 4)
                    return PresetsFor4Sockets;

                if (totalSockets == 3)
                    return PresetsFor3Sockets;

                if (totalSockets == 2)
                    return PresetsFor2Sockets;

                return null;
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class ModuleDefinition : IModuleDefinition
        {
            [JsonProperty("Item short name")]
            public string ItemShortName;

            [JsonProperty("Item skin ID")]
            public ulong SkinId;

            [JsonIgnore]
            public int NumSockets { get; private set; }= 1;

            [JsonIgnore]
            private ItemDefinition _itemDefinition;

            public void Init(CarSpawnSettings plugin = null)
            {
                // Null/empty short name indicates a blank socket between modules.
                if (string.IsNullOrEmpty(ItemShortName))
                    return;

                _itemDefinition = ItemManager.FindItemDefinition(ItemShortName);
                if (_itemDefinition == null)
                {
                    plugin?.LogError($"Unrecognized module item short name: {ItemShortName}");
                    return;
                }

                var vehicleMod = _itemDefinition.GetComponent<ItemModVehicleModule>();
                if (vehicleMod == null)
                {
                    plugin?.LogError("No vehicle module found for item: {0}", ItemShortName);
                    _itemDefinition = null;
                    return;
                }

                NumSockets = vehicleMod.SocketsTaken;
            }

            public Item Create()
            {
                if ((object)_itemDefinition == null)
                    return null;

                return ItemManager.Create(_itemDefinition, 1, SkinId);
            }
        }

        private class ModulePresetConfiguration
        {
            [JsonProperty("Use vanilla presets")]
            public bool UseVanillaPresets = true;
            [JsonProperty("UseVanillaPresets")]
            private bool DeprecatedUseVanillaPresets { set { UseVanillaPresets = value; } }

            [JsonProperty("Custom presets")]
            public ModuleDefinition[][] CustomPresets = new ModuleDefinition[0][];
            [JsonProperty("CustomPresets")]
            private object[][] DeprecatedCustomPresets { set { CustomPresets = ParseLegacyPresets(value); } }

            public void Init(CarSpawnSettings plugin)
            {
                foreach (var presetList in CustomPresets)
                {
                    foreach (var moduleDefinition in presetList)
                    {
                        moduleDefinition.Init(plugin);
                    }
                }
            }

            private ModuleDefinition[][] ParseLegacyPresets(object[][] legacyPresetList)
            {
                var presetList = new List<ModuleDefinition[]>();

                foreach (var moduleIdentifierList in legacyPresetList)
                {
                    var modulePreset = new List<ModuleDefinition>();
                    foreach (var moduleIdentifier in moduleIdentifierList)
                    {
                        modulePreset.Add(ParseLegacyModuleDefinition(moduleIdentifier));
                    }
                    presetList.Add(modulePreset.ToArray());
                }

                return presetList.ToArray();
            }

            private ModuleDefinition ParseLegacyModuleDefinition(object moduleIdentifier)
            {
                if (moduleIdentifier is int || moduleIdentifier is long)
                {
                    var moduleId = moduleIdentifier is long
                        ? Convert.ToInt32((long)moduleIdentifier)
                        : (int)moduleIdentifier;

                    if (moduleId == 0)
                        return new ModuleDefinition();

                    var itemDefinition = ItemManager.FindItemDefinition(moduleId);
                    if (itemDefinition == null)
                        return new ModuleDefinition();

                    return new ModuleDefinition
                    {
                        ItemShortName = itemDefinition.shortname,
                    };
                }

                var moduleString = moduleIdentifier as string;
                if (moduleString != null)
                {
                    ItemDefinition itemDefinition;

                    int parsedItemId;
                    if (int.TryParse(moduleString, out parsedItemId))
                    {
                        if (parsedItemId == 0)
                            return new ModuleDefinition();

                        itemDefinition = ItemManager.FindItemDefinition(parsedItemId);
                        if (itemDefinition == null)
                            return new ModuleDefinition();
                    }

                    return new ModuleDefinition
                    {
                        ItemShortName = moduleString,
                    };
                }

                return new ModuleDefinition();
            }
        }

        #region Configuration Helpers

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
