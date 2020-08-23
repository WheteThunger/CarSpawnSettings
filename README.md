**Car Spawn Settings** allows modular cars to spawn with configurable health, fuel, and engine parts.

## Plugin Compatibility

This plugin's code may run before other plugins that spawn cars. It is recommended that other plugins either be resilient to the presence of existing engine parts and fuel, or alternatively use hooks to prevent this plugin from affecting specific cars.

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

- Called on the next tick after a modular car is spawned. This is delayed to give other plugins time to save a reference to the car in case they want to check it in the hook method.
- Returning `false` will prevent this plugin from altering health, fuel or engine parts.
- Returning `null` will result in the default behavior.

```csharp
object CanBootstrapSpawnedCar(ModularCar car)
```
