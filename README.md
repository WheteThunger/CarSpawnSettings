**Car Spawn Settings** allows modular cars to spawn with configurable modules, health, fuel, and engine parts.

Note: This only affects cars that spawn after the plugin loads, not existing cars.

As an alternative (or supplement) to spawning cars with engine parts, you can also try out the [No Engine Parts](https://umod.org/plugins/no-engine-parts) plugin, which allows car engines to function without engine parts. Supports multiple use cases.

## Configuration

Default configuration (vanilla equivalent):
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
  "MinHealthPercent": 15.0,
  "MaxHealthPercent": 50.0,
  "ModulePresets": {
    "2Sockets": {
      "UseVanillaPresets": true,
      "CustomPresets": []
    },
    "3Sockets": {
      "UseVanillaPresets": true,
      "CustomPresets": []
    },
    "4Sockets": {
      "UseVanillaPresets": true,
      "CustomPresets": []
    }
  }
}
```

- `EngineParts`
  - `Tier*Chance` (`0` - `100`) -- These three options control the chance that an engine part slot will be filled with a part of the corresponding quality. For example, setting `Tier1Chance` to 100 will guarantee that each engine part slot is filled with at least a low quality engine part. Additionally setting `Tier2Chance` to 50 would grant a 50% chance that each slot will receive a medium quality part instead of a low quality part. Each tier gets a separate roll, so the total part chance is multiplicative with dimishing returns. Formula: `OverallChance = 100 - 100 * (1 - 0.01 * T3) * (1 - 0.01 * T2) * (1 - 0.01 * T1)`.
  - `MinConditionPercent` / `MaxConditionPercent` (`0` - `100`) -- These options determine the condition that each engine part will be assigned. You can set them both to the same value if you don't want randomization.
- `MinFuelAmount` / `MaxFuelAmount` -- These options determine the amount of low grade fuel to put in the car's fuel tank when it spawns. You can set them both to the same value if you don't want randomization.
- `ModulePresets` -- These settings allow you to customize the modules that cars spawn with, using presets. Each car size is configured separately. You can use either vanilla presets, custom presets, or a combination of both. The exact preset used will be randomly selected from the list when a car spawns.
  - `UseVanillaPresets` (`true` or `false`) -- While `true`, vanilla presets will be used in conjunction with any `CustomPresets` you defined. While `false`, only your `CustomPresets` will be used.
  - `CustomPresets` -- List of module presets, where each preset is a list of module item ids or short names for which module items to add. The number `0` represents an empty socket. Item names and ids can be found on the [uMod item list page](https://umod.org/documentation/games/rust/definitions). See below for some examples.

### Legacy options

These options were introduced in a previous version. They still work for backwards compatibility.

- `EnginePartsTier` (`0` - `3`) -- The quality of engine parts to add to all of the car's engine modules when it spawns.
  - When 0 or not present, the `EngineParts.Tier*Chance` options will be used instead.
- `FuelAmount` -- The amount of low grade fuel to put in the car's fuel tank when it spawns (`-1` for max stack size).
  - When 0 or not present, the `MinFuelAmount` and `MaxFuelAmount` will be used instead.
- `HealthPercentage` (`0` - `100`) -- The minimum health percentage that each module should be set to when the car spawns (`-1` to not alter health).
  - When -1 or not present, the `MinHealthPercent` and `MaxHealthPercent` will be used instead.

### Example module presets

```json
  "ModulePresets": {
    "2Sockets": {
      "UseVanillaPresets": false,
      "CustomPresets": [
        ["vehicle.1mod.cockpit.with.engine", "vehicle.1mod.taxi"],
        ["vehicle.1mod.engine", "vehicle.1mod.cockpit.armored"]
      ]
    },
    "3Sockets": {
      "UseVanillaPresets": false,
      "CustomPresets": [
        ["vehicle.1mod.engine", "vehicle.1mod.cockpit.with.engine", "vehicle.1mod.taxi"],
        ["vehicle.1mod.engine", "vehicle.1mod.cockpit.armored", "vehicle.1mod.passengers.armored"]
      ]
    },
    "4Sockets": {
      "UseVanillaPresets": false,
      "CustomPresets": [
        ["vehicle.1mod.engine", "vehicle.1mod.cockpit.with.engine", "vehicle.1mod.taxi", "vehicle.1mod.taxi"],
        ["vehicle.1mod.engine", "vehicle.1mod.cockpit.armored", "vehicle.1mod.passengers.armored", "vehicle.1mod.engine"]
      ]
    }
  }
```

### Partial starter configs

#### High-end cars

Fully repaired, high quality parts, 500 low grade.

```json
  "EngineParts": {
    "Tier1Chance": 0,
    "Tier2Chance": 0,
    "Tier3Chance": 100,
    "MinConditionPercent": 100.0,
    "MaxConditionPercent": 100.0
  },
  "MinFuelAmount": 500,
  "MaxFuelAmount": 500,
  "MinHealthPercent": 100.0,
  "MaxHealthPercent": 100.0,
```

#### Random health, fuel, parts and condition

Based on vanilla settings and loot tables. Overall 35.5% chance for engine parts in each slot.

```json
  "EngineParts": {
    "Tier1Chance": 27,
    "Tier2Chance": 9,
    "Tier3Chance": 3,
    "MinConditionPercent": 25.0,
    "MaxConditionPercent": 75.0
  },
  "MinFuelAmount": 0,
  "MaxFuelAmount": 25,
  "MinHealthPercent": 15.0,
  "MaxHealthPercent": 50.0,
```

## Plugin compatibility

This plugin may affect cars spawned by other plugins, but the following rules should automatically prevent most conflicts.

- Cars spawned as a chassis (cars with `car.spawnSettings.useSpawnSettings` set to `false`) will **not** be affected by this plugin. This means other plugins can safely spawn a chassis and add modules to it without interference.
- Non-chassis cars spawned with an explicit `OwnerID` (not 0) will still be subject to the module preset rules in this plugin, but will **not** have fuel or engine parts added to them. This is because only plugins set car ownership, so it's assumed that the plugin that spawned it can add fuel and engine parts if needed.
- Non-chassis cars spawned without an explicit `OwnerID` will have fuel and engine parts added to them if configured in this plugin.
  - Fuel is only added if there is none present.
  - Engine parts are only added to empty slots.

It is recommended that other plugins be resilient to the presence of existing fuel and engine parts, in case this plugin adds them first, and to use the `CanBootstrapSpawnedCar` hook to prevent this plugin from adding fuel or engine parts to specific cars if more precise control is needed.

## Developer hooks

#### CanBootstrapSpawnedCar

This hook can be used to prevent this plugin from adding fuel or engine parts to specific unowned cars. Not necessary for cars spawned as a chassis or with an explicit `OwnerID`.

- Called at least 0.5 seconds after a modular car is spawned. This delay is partly a requirement so that vehicle modules have time to spawn, but this also gives other plugins plenty of time to save a reference to the car they spawned in case they want to check for it in the hook method.
- Returning `false` will prevent this plugin from adding fuel or engine parts.
- Returning `null` will result in the default behavior.

```csharp
object CanBootstrapSpawnedCar(ModularCar car)
```
