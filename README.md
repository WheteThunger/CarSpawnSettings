**Car Spawn Settings** allows modular cars to spawn with configurable health, fuel, and engine parts.

## Plugin Compatibility

This plugin may affect cars spawned by other plugins. It is recommended that other plugins be resilient to the presence of existing engine parts and fuel (in case this plugin adds them first), and to use the `CanBootstrapSpawnedCar` hook to prevent this plugin from affecting specific cars if desired.

If another plugin updates a car's fuel or engine parts before this plugin, it will only add fuel if missing, and only add engine parts to empty slots.

- [Craft Car Chassis](https://umod.org/plugins/craft-car-chassis)
  - The chassis is ignored by this plugin, meaning it is not given fuel, unless you configure this plugin with `IncludeChassis: true`.
- [Spawn Modular Car](https://umod.org/plugins/spawn-modular-car)
  - Cars spawned from a preset are ignored by this plugin unless you configure this plugin with both `IncludeChassis: true` and `IncludeOwnedCars: true`.
  - Cars spawned with a random module configuration are ignored unless you configure this plugin with `IncludeOwnedCars: true`.
  - When both plugins are configured to add engine parts or fuel to a car, the higher engine part tier or fuel amount will win.
  - Cars are always spawned at full health.
  - These rules also apply to any car spawned using the API from Spawn Modular Car.

## Configuration

Default configuration:
```json
{
  "EnginePartsTier": 0,
  "FuelAmount": 0,
  "HealthPercentage": -1.0,
  "IncludeChassis": false,
  "IncludeOwnedCars": false
}
```

- `EnginePartsTier` (`0` - `3`) -- The quality of engine parts to add to all of the car's engine modules when it spawns (`0` for no engine parts).
- `FuelAmount` -- The amount of low grade fuel to put in the car's fuel tank when it spawns (`-1` for max stack size).
- `HealthPercentage` -- The percentage of health that each module should be set to when the car spawns (`-1` to not alter health).
- `IncludeChassis` (`true` or `false`) -- Whether to affect cars that are spawned as just a chassis (cars with `car.spawnSettings.useSpawnSettings = false`). Currently, that can only be done by a plugin, so this option defaults to `false` to avoid conflicts.
- `IncludeOwnedCars` (`true` or `false`) -- Whether to affect cars spawned with a non-zero `OwnerID`. Currently, that can only be done by a plugin, so this option defaults to `false` to avoid conflicts.

## Hooks

#### CanBootstrapSpawnedCar

- Called at least 100ms after a modular car is spawned. This delay is partly a requirement so that vehicle modules have time to spawn, but this also gives other plugins plenty of time to save a reference to the car they spawned in case they want to check for it in the hook method.
- Not called if either of the following are true:
  - The car was spawned as a chassis and the plugin is configured with `IncludeChassis: false`.
  - The car has an `OwnerID` set and the plugin is configured with `IncludeOwnedCars: false`.
- Returning `false` will prevent this plugin from altering health, fuel or engine parts.
- Returning `null` will result in the default behavior.

```csharp
object CanBootstrapSpawnedCar(ModularCar car)
```
