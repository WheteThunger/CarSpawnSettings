## Features

Allows modular cars to **spawn** with configurable modules, health, fuel, and engine parts.

## Related compatible plugins

- [Auto Engine Parts](https://umod.org/plugins/auto-engine-parts) -- Automatically fills engine modules with parts and prevents players from removing them.
  - When configuring Auto Engine Parts to apply to all cars, it's recommended to disable the engine parts options in Car Spawn Settings since they will be overriden anyway.
- [No Engine Parts](https://umod.org/plugins/no-engine-parts) -- Allows car engines to work without engine parts.
  - A great way to use this in conjunction with Car Spawn Settings is to allow cars with incomplete engine part sets to be driven but with reduced stats.

## Commands

- `carspawnsettings.fillcars` -- (Admin command only) Adds fuel and engine parts to existing cars, according to the configuration of the plugin. This is intended to be used only during first time installation, to update cars that spawned before the plugin loaded.
  - Tip: You can also run the `carstats` command to see the status of all cars in the server.

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

## FAQ

#### How can I respawn all cars?

One way is to temporarily increase car decay to get them to all despawn. The following steps can guide you through that.

1. Run the command `modularcar.population` and take note of its value.
2. Run the command `modularcar.outsidedecayminutes` and take note of its value.
3. Run the command `modularcar.population 0` to temporarily prevent cars from respawning.
4. Run the command `modularcar.outsidedecayminutes 1` to significantly increase car decay.
5. Wait 12 minutes. During this time, run the `carstats` command to see how many cars there are and their status. All cars should reach 0 health within 2 minutes, then despawn within 10 minutes later. When all the cars are gone, proceed to the next steps.
6. Run the command `modularcar.outsidedecayminutes X` (replace `X` with the value you saved earlier).
7. Run the command `modularcar.population X` (replace `X` with the value you saved earlier).
8. Wait a few minutes and run the `carstats` command again to verify the result.

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

- Called some time after a modular car has spawned (on next tick or later).
- Returning `false` will prevent this plugin from adding fuel or engine parts.
- Returning `null` will result in the default behavior.

```csharp
object CanBootstrapSpawnedCar(ModularCar car)
```
