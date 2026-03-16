# Black Hole (Absorb Portal) System

This folder contains the core scripts and prefabs for the Black Hole/Absorb Portal system.

## Files included:
### Scripts
- **AbsorbPortalManager.cs**: The main singleton that handles spawning and toggling the portal (default key 'Z').
- **AbsorbPortal.cs**: High-level logic on the portal object itself, handles the absorption hook.
- **PortalVfxController.cs**: The procedural particle system controller that creates the "Black Hole" look.
- **ProjectileStandard.cs**: Modified version of the base projectile script to support being absorbed by the portal.
- **ProjectileBase.cs**: The base class required for projectiles.

### Prefabs
- **EnergyPortal_Prefab.prefab**: The main portal prefab designed to work with the `PortalVfxController`.

## Setup
1. Place the `AbsorbPortalManager` script on a persistent object in your scene (or the Player).
2. Assign the `EnergyPortal_Prefab` to the `AbsorbPortalPrefab` slot in the Manager.
3. Assign any projectile prefab (like `Projectile_Blaster`) to the `ReleasedProjectilePrefab` slot. This is what will be fired back when you release the stored energy.
4. Ensure your projectiles use the `ProjectileStandard` script so they can be detected by the portal.
