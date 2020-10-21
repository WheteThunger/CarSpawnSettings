using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using Rust.Modular;

namespace Oxide.Plugins
{
    [Info("Car Spawn Settings", "WhiteThunder", "1.0.3")]
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

            timer.Once(0.5f, () =>
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
            var maxFuelAmount = PluginConfig.MaxFuelAmount;
			var minFuelAmount = PluginConfig.MinFuelAmount;
            if (maxFuelAmount == 0) return;

            var fuelContainer = car.fuelSystem.GetFuelContainer();
            if (maxFuelAmount < 0)
                maxFuelAmount = fuelContainer.allowedItem.stackable;

			if (minFuelAmount < 0)
				minFuelAmount = 0;

            var fuelItem = fuelContainer.inventory.FindItemByItemID(fuelContainer.allowedItem.itemid);
			var totalFuel = UnityEngine.Random.Range(minFuelAmount, maxFuelAmount+1);
            if (fuelItem == null && totalFuel > 0)
                fuelContainer.inventory.AddItem(fuelContainer.allowedItem, totalFuel);
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

			var partchance = PluginConfig.EnginePartChance;
			var t3_chance = PluginConfig.EnginePartsTier3Chance;
			var t2_chance = PluginConfig.EnginePartsTier2Chance;
			var t1_chance = PluginConfig.EnginePartsTier1Chance;
			var t_roll = UnityEngine.Random.Range(0, 100);
			var curtier = desiredTier;
            var inventory = engineStorage.inventory;
            for (var i = 0; i < inventory.capacity; i++)
            {
                // Do nothing if there is an existing engine part
                var item = inventory.GetSlot(i);
				t_roll = UnityEngine.Random.Range(0, 100);
                if (item == null)
					if(t_roll < t3_chance){
						curtier = 3;
					} else if(t_roll < t2_chance){
						curtier = 2;
					} else {
						curtier = 1;
					}
                    TryAddEngineItem(engineStorage, i, curtier, partchance);
            }
        }

        private bool TryAddEngineItem(EngineStorage engineStorage, int slot, int tier, int chance)
        {
            ItemModEngineItem output;
            if (!engineStorage.allEngineItems.TryGetItem(tier, engineStorage.slotTypes[slot], out output)) return false;

            var component = output.GetComponent<ItemDefinition>();
            var item = ItemManager.Create(component);
            if (item == null) return false;

            item.condition = UnityEngine.Random.Range(1.0f, 100.0f);
            if(UnityEngine.Random.Range(0, 100) < chance){
				item.MoveToContainer(engineStorage.inventory, slot, allowStack: false);
			} else {
				return false;
			}

            return true;
        }

        #endregion

        #region Configuration

        protected override void LoadDefaultConfig() => Config.WriteObject(new CarSpawnSettingsConfig(), true);

        internal class CarSpawnSettingsConfig
        {
            [JsonProperty("EnginePartsTier")]
            public int EnginePartsTier = 3;

			[JsonProperty("EnginePartsTier3Chance")]
            public int EnginePartsTier3Chance = 5;

			[JsonProperty("EnginePartsTier2Chance")]
            public int EnginePartsTier2Chance = 20;

			[JsonProperty("EnginePartsTier1Chance")]
            public int EnginePartsTier1Chance = 75;

            [JsonProperty("MaxFuelAmount")]
            public int MaxFuelAmount = 11;

			[JsonProperty("MinFuelAmount")]
            public int MinFuelAmount = 0;

			[JsonProperty("EnginePartChance")]
            public int EnginePartChance = 25;

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
