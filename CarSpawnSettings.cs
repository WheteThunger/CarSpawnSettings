using Newtonsoft.Json;
using Oxide.Core;
using Rust.Modular;

namespace Oxide.Plugins
{
    [Info("Car Spawn Settings", "WhiteThunder", "1.0.2")]
    [Description("Allows modular cars to spawn with configurable health, fuel, and engine parts.")]
    internal class CarSpawnSettings : CovalencePlugin
    {
        #region Fields

        private CarSpawnSettingsConfig PluginConfig;

        #endregion

        #region Hooks

        private void Init()
        {
            PluginConfig = Config.ReadObject<CarSpawnSettingsConfig>();
        }

        private void OnEntitySpawned(ModularCar car)
        {
            if (Rust.Application.isLoadingSave) return;
            if (!PluginConfig.IncludeChassis && !car.spawnSettings.useSpawnSettings) return;

            timer.Once(0.1f, () =>
            {
                if (car == null) return;
                if (!PluginConfig.IncludeOwnedCars && car.OwnerID != 0) return;
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
            var healthPercentage = PluginConfig.HealthPercentage;
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
            var fuelAmount = PluginConfig.FuelAmount;
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
            var enginePartsTier = PluginConfig.EnginePartsTier;
            if (enginePartsTier < 1 || enginePartsTier > 3) return;

            foreach (var module in car.AttachedModuleEntities)
            {
                var engineModule = module as VehicleModuleEngine;
                if (engineModule != null)
                {
                    var engineStorage = engineModule.GetContainer() as EngineStorage;
                    if (engineStorage != null)
                    {
                        AddPartsToEngineStorage(engineStorage, enginePartsTier);
                        engineModule.RefreshPerformanceStats(engineStorage);
                    }
                }
            }
        }

        private void AddPartsToEngineStorage(EngineStorage engineStorage, int desiredTier)
        {
            if (engineStorage.inventory == null) return;

            var inventory = engineStorage.inventory;
            for (var i = 0; i < inventory.capacity; i++)
            {
                // Do nothing if there is an existing engine part
                var item = inventory.GetSlot(i);
                if (item == null)
                    TryAddEngineItem(engineStorage, i, desiredTier);
            }
        }

        private bool TryAddEngineItem(EngineStorage engineStorage, int slot, int tier)
        {
            ItemModEngineItem output;
            if (!engineStorage.allEngineItems.TryGetItem(tier, engineStorage.slotTypes[slot], out output)) return false;

            var component = output.GetComponent<ItemDefinition>();
            var item = ItemManager.Create(component);
            if (item == null) return false;

            item.condition = component.condition.max;
            item.MoveToContainer(engineStorage.inventory, slot, allowStack: false);

            return true;
        }

        #endregion

        #region Configuration

        protected override void LoadDefaultConfig() => Config.WriteObject(new CarSpawnSettingsConfig(), true);

        internal class CarSpawnSettingsConfig
        {
            [JsonProperty("EnginePartsTier")]
            public int EnginePartsTier = 0;

            [JsonProperty("FuelAmount")]
            public int FuelAmount = 0;

            [JsonProperty("HealthPercentage")]
            public float HealthPercentage = -1;

            [JsonProperty("IncludeChassis")]
            public bool IncludeChassis = false;

            [JsonProperty("IncludeOwnedCars")]
            public bool IncludeOwnedCars = false;
        }

        #endregion
    }
}
