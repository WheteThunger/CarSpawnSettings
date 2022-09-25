## Features

Allows configuring the modules, health, fuel, and engine parts that modular cars spawn with.

Supports skinned modules for compatibility with plugins that define custom modules according to their skin ID.

## Commands

- `carspawnsettings.fillcars` -- **(Admin command only)** Adds fuel and engine parts to existing cars, according to the configuration of the plugin. This is intended to be used only during first time installation, to update cars that spawned before the plugin loaded.
  - Tip: You can run the `carstats` command to see the status of all cars in the server.

## Configuration

Default configuration (vanilla equivalent):

```json
{
  "Engine parts": {
    "Tier 1 chance": 0,
    "Tier 2 chance": 0,
    "Tier 3 chance": 0,
    "Min condition percent": 100.0,
    "Max condition percent": 100.0
  },
  "Min fuel amount": 0,
  "Max fuel amount": 0,
  "Min health percent": 15.0,
  "Max health percent": 50.0,
  "Module presets": {
    "2 sockets": {
      "Use vanilla presets": true,
      "Custom presets": []
    },
    "3 sockets": {
      "Use vanilla presets": true,
      "Custom presets": []
    },
    "4 sockets": {
      "Use vanilla presets": true,
      "Custom presets": []
    }
  }
}
```

- `Engine parts`
  - `Tier * chance` (`0` - `100`) -- These three options control the chance that an engine part slot will be filled with a part of the corresponding quality. For example, setting `Tier 1 chance` to `100` will guarantee that each engine part slot is filled with at least a low quality engine part. Additionally setting `Tier 2 chance` to 50 would grant a 50% chance that each slot will receive a medium quality part instead of a low quality part. Each tier gets a separate roll, so the total part chance is multiplicative with dimishing returns. Formula: `OverallChance = 100 - 100 * (1 - 0.01 * T3) * (1 - 0.01 * T2) * (1 - 0.01 * T1)`.
  - `Min condition percent` / `Max condition percent` (`0` - `100`) -- These options determine the condition that each engine part will be assigned. You can set them both to the same value if you don't want randomization.
- `Min fuel amount` / `Max fuel amount` -- These options determine the amount of low grade fuel to put in the car's fuel tank when it spawns. You can set them both to the same value if you don't want randomization.
- `Module presets` -- These settings allow you to customize the modules that cars spawn with, using presets. Each car size is configured separately. You can use either vanilla presets, custom presets, or a combination of both. The exact preset used will be randomly selected from the list when a car spawns.
  - `Use vanilla presets` (`true` or `false`) -- While `true`, vanilla presets will be used in conjunction with any `Custom presets` you defined. While `false`, only your `Custom Presets` will be used.
  - `Custom presets` -- List of module presets, where each preset is a list of module item short names and Item skin IDs, which determines the module items to add. Using an empty item short name like `""` will cause that particular socket to be skipped. Item short names can be found on the [uMod item list page](https://umod.org/documentation/games/rust/definitions). See below for some examples.

### Example module presets

In the below example, there are two 2-socket presets, two 3-socket presets, and two 4-socket presets.

```json
  "Module presets": {
    "2 sockets": {
      "Use vanilla presets": false,
      "Custom presets": [
        [
          {
            "Item short name": "vehicle.1mod.cockpit.with.engine",
            "Item skin ID": 0
          },
          {
            "Item short name": "vehicle.1mod.taxi",
            "Item skin ID": 0
          }
        ],
        [
          {
            "Item short name": "vehicle.1mod.engine",
            "Item skin ID": 0
          },
          {
            "Item short name": "vehicle.1mod.cockpit.armored",
            "Item skin ID": 0
          }
        ]
      ]
    },
    "3 sockets": {
      "Use vanilla presets": false,
      "Custom presets": [
        [
          {
            "Item short name": "vehicle.1mod.engine",
            "Item skin ID": 0
          },
          {
            "Item short name": "vehicle.1mod.cockpit.with.engine",
            "Item skin ID": 0
          },
          {
            "Item short name": "vehicle.1mod.taxi",
            "Item skin ID": 0
          }
        ],
        [
          {
            "Item short name": "vehicle.1mod.engine",
            "Item skin ID": 0
          },
          {
            "Item short name": "vehicle.1mod.cockpit.armored",
            "Item skin ID": 0
          },
          {
            "Item short name": "vehicle.1mod.passengers.armored",
            "Item skin ID": 0
          }
        ]
      ]
    },
    "4 sockets": {
      "Use vanilla presets": false,
      "Custom presets": [
        [
          {
            "Item short name": "vehicle.1mod.engine",
            "Item skin ID": 0
          },
          {
            "Item short name": "vehicle.1mod.cockpit.with.engine",
            "Item skin ID": 0
          },
          {
            "Item short name": "vehicle.1mod.taxi",
            "Item skin ID": 0
          },
          {
            "Item short name": "vehicle.1mod.taxi",
            "Item skin ID": 0
          }
        ],
        [
          {
            "Item short name": "vehicle.1mod.engine",
            "Item skin ID": 0
          },
          {
            "Item short name": "vehicle.1mod.cockpit.armored",
            "Item skin ID": 0
          },
          {
            "Item short name": "vehicle.1mod.passengers.armored",
            "Item skin ID": 0
          },
          {
            "Item short name": "vehicle.1mod.engine",
            "Item skin ID": 0
          }
        ]
      ]
    }
  }
```

### Partial starter configs

#### High-end cars

Fully repaired, high quality parts, 500 low grade.

```json
  "Engine parts": {
    "Tier 1 chance": 0,
    "Tier 2 chance": 0,
    "Tier 3 chance": 100,
    "Min condition percent": 100.0,
    "Max condition percent": 100.0
  },
  "Min fuel amount": 500,
  "Max fuel amount": 500,
  "Min health percent": 100.0,
  "Max health percent": 100.0,
```

#### Random health, fuel, parts and condition

Based on vanilla settings and loot tables. Overall 35.5% chance for engine parts in each slot.

```json
  "Engine parts": {
    "Tier 1 chance": 27,
    "Tier 2 chance": 9,
    "Tier 3 chance": 3,
    "Min condition percent": 25.0,
    "Max condition percent": 75.0
  },
  "Min fuel amount": 0,
  "Max fuel amount": 25,
  "Min health percent": 15.0,
  "Max health percent": 50.0,
```

## FAQ

#### Q: How can I prevent players from recycling the engine parts added by this plugin?

The following two plugins were developed to solve that problem in different ways. They can be used independently or alongside Car Spawn Settings.

- [Auto Engine Parts](https://umod.org/plugins/auto-engine-parts) -- Automatically fills engine modules with parts and prevents players from removing them. If using both Auto Engine Parts and Car Spawn Settings, we recommend that you disable the engine parts options in Car Spawn Settings since they will be overriden by Auto Engine Parts anyway.
- [No Engine Parts](https://umod.org/plugins/no-engine-parts) -- Allows car engines to work without engine parts. A great way to use No Engine Parts in conjunction with Car Spawn Settings is to configure No Engine Parts to allow cars with missing engine parts to be driven but with reduced stats.

#### Q: How can I respawn all cars?

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

- Cars spawned as a chassis (cars with `car.spawnSettings.useSpawnSettings` set to `false`) will **not** be affected by this plugin. This means other plugins can safely spawn a chassis using the correct prefab and add modules to it without interference. This applies to the following prefabs.
  - `assets/content/vehicles/modularcar/car_chassis_2module.entity.prefab`
  - `assets/content/vehicles/modularcar/car_chassis_3module.entity.prefab`
  - `assets/content/vehicles/modularcar/car_chassis_4module.entity.prefab`
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
