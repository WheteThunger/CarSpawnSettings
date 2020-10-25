**Car Spawn Settings** allows modular cars to spawn with configurable health, fuel, and engine parts. This only affects cars that spawn after the plugin loads, not existing cars.

## Plugin Compatibility

This plugin may affect cars spawned by other plugins. It is recommended that other plugins be resilient to the presence of existing engine parts and fuel (in case this plugin adds them first), and to use the `CanBootstrapSpawnedCar` hook to prevent this plugin from affecting specific cars if desired.

If another plugin updates a car's health, fuel or engine parts before this plugin, this plugin will at most increase the health to the configured amount (never reduce health), will only add fuel if there is none, and will only add engine parts to empty slots.

- [Craft Car Chassis](https://umod.org/plugins/craft-car-chassis)
  - The chassis is ignored by this plugin, meaning it is not given fuel, unless you configure this plugin with `IncludeChassis: true`. May also require `IncludeOwnedCars: true` depending on whether the Craft Car Chassis plugin is configured to set an owner.
- [Spawn Modular Car](https://umod.org/plugins/spawn-modular-car)
  - Cars spawned from a preset are ignored by this plugin unless you configure this plugin with both `IncludeChassis: true` and `IncludeOwnedCars: true`. This includes cars spawned using the API from Spawn Modular Car.
  - Cars spawned with a random module configuration are ignored unless you configure this plugin with `IncludeOwnedCars: true`.

## Configuration

Default configuration:
```json
{
  "EngineParts": {
    "Tier1Chance": 0,
    "Tier2Chance": 0,
    "Tier3Chance": 0,
    "MinConditionPercent": 100.0,
    "MaxConditionPercent": 100.0
  },
  "MinFuelAmount": 0,
  "MaxFuelAmount": 0,
  "HealthPercentage": -1.0,
  "IncludeChassis": false,
  "IncludeOwnedCars": false
}
```

- `EnginePartsTier*Chance` (`0` - `100`) -- These three options control the chance that an engine part slot will be filled with a part of the corresponding quality. For example, setting `EnginePartsTier1Chance` to 100 will guarantee that each engine part slot is filled with at least a low quality engine part. Additionally setting `EnginePartsTier2Chance` to 50 would grant a 50% chance that each slot will receive a medium quality part instead of a low quality part.
- `EnginePartMinConditionPercent` / `EnginePartMaxConditionPercent` (max `100`) -- These options determine the condition that each engine part will be assigned. You can set them both to the same value if you don't want randomization.
- `MinFuelAmount` / `MaxFuelAmount` -- These options determine the amount of low grade fuel to put in the car's fuel tank when it spawns. You can set them both to the same value if you don't want randomization.
- `HealthPercentage` (max `100`) -- The minimum health percentage that each module should be set to when the car spawns (`-1` to not alter health). Note: To avoid compatibility issues with other plugins, this plugin will only increase health if it's below the configured amount, never reduce it.

### Advanced compatibility options

- `IncludeChassis` (`true` or `false`) -- Whether to affect cars that are spawned as just a chassis (cars with `car.spawnSettings.useSpawnSettings = false`). Currently, that can only be done by a plugin, so this option defaults to `false` to avoid conflicts. Keep in mind that a plugin may spawn a chassis and then add modules to it, so altering this option may affect more plugins than you expect.
- `IncludeOwnedCars` (`true` or `false`) -- Whether to affect cars spawned with a non-zero `OwnerID`. Currently, that can only be done by a plugin, so this option defaults to `false` to avoid conflicts.

### Legacy options

These options were present in a previous version. They still work for backwards compatibility.

- `EnginePartsTier` (`0` - `3`) -- The quality of engine parts to add to all of the car's engine modules when it spawns.
  - When 0, the `EnginePartsTier*Chance` options will be used instead.
- `FuelAmount` -- The amount of low grade fuel to put in the car's fuel tank when it spawns (`-1` for max stack size).
  - When 0, the `MinFuelAmount` and `MaxFuelAmount` will be used instead.

### Starter configs

#### High-end cars

Fully repaired, tier 3 parts, 500 low grade.

```json
{
  "EngineParts": {
    "Tier1Chance": 0,
    "Tier2Chance": 0,
    "Tier3Chance": 100,
    "MinConditionPercent": 100.0,
    "MaxConditionPercent": 100.0
  },
  "MinFuelAmount": 500,
  "MaxFuelAmount": 500,
  "HealthPercentage": 100.0,
  "IncludeChassis": false,
  "IncludeOwnedCars": false
}
```

#### Random health, fuel, parts and condition

Based on vanilla loot tables.

```json
{
  "EngineParts": {
    "Tier1Chance": 27,
    "Tier2Chance": 9,
    "Tier3Chance": 3,
    "MinConditionPercent": 25.0,
    "MaxConditionPercent": 75.0
  },
  "MinFuelAmount": 0,
  "MaxFuelAmount": 25,
  "HealthPercentage": -1.0,
  "IncludeChassis": false,
  "IncludeOwnedCars": false
}
```

## Hooks

#### CanBootstrapSpawnedCar

- Called at least 0.5 seconds after a modular car is spawned. This delay is partly a requirement so that vehicle modules have time to spawn, but this also gives other plugins plenty of time to save a reference to the car they spawned in case they want to check for it in the hook method.
- Not called if this plugin ignores the car for either of the following reasons.
  - The car was spawned as a chassis and the plugin is configured with `IncludeChassis: false`.
  - The car has an `OwnerID` set and the plugin is configured with `IncludeOwnedCars: false`.
- Returning `false` will prevent this plugin from altering health, fuel or engine parts.
- Returning `null` will result in the default behavior.

```csharp
object CanBootstrapSpawnedCar(ModularCar car)
```
