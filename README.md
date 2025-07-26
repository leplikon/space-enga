 codex/implement-waypoint-obstacle-detection
# Space Enga Mining Drone

This repository contains a simple programmable block script for **Space Engineers**. The script controls a mining drone that flies between a base and a mining waypoint. When the cargo is nearly full or power is low, the drone returns to base, unloads and resumes mining.

## Features
- Autonomous travel between two waypoints
- Basic obstacle detection using a forward camera or sensor (optional)

## Usage
1. Load `MiningDroneScript.cs` into a programmable block.
2. Configure block names in the script (`RC`, `Connector`, `Cargo`, `Drill`, `Battery`).
3. Ensure at least one camera faces forward or a sensor is placed to detect obstacles.

## Limitations
- Obstacle checks are performed only up to **50 m** ahead and assume the camera faces forward.
- The waypoint is not adjusted; the drone simply halts if something is detected in front.

=======
# space-enga

This repository contains `MiningDroneScript.cs`, a simple C# script for the **Programmable Block** in Space Engineers. The script controls an autonomous mining drone that travels to an ore deposit, mines until its cargo is nearly full, and then returns to base to unload and recharge.

## Required blocks
Make sure the ship running the script has the following blocks named exactly as listed:

- **Remote Control** block named `RC`
- **Connector** named `Connector`
- **Cargo Container** named `Cargo`
- **Ship Drill** named `Drill`
- **Battery** named `Battery`
- Thrusters and gyros (any names) for movement and orientation

## Configuration
Edit `MiningDroneScript.cs` and set the following variables to the GPS coordinates of your base and the mining location:

```csharp
private Vector3D basePosition = new Vector3D(0,0,0);   // replace with base coords
private Vector3D miningPosition = new Vector3D(100,0,0); // replace with ore coords
```

## Running the script
1. Add a **Programmable Block** to your drone.
2. Open the block's *Edit* interface and paste the contents of `MiningDroneScript.cs` into the editor.
3. Click *Check Code*, then *Remember & Exit*.
4. Run the Programmable Block once to start the script (either through the terminal or with a button).

## How it works
During operation the drone alternates between two modes:

- **Mining**: The drill is enabled and the ship stays at the `miningPosition` until the cargo container reaches 90% capacity and the battery has more than 30% charge.
- **Returning**: When cargo is nearly full or the battery level is low, the ship flies to `basePosition` and docks using the connector.

The cycle repeats automatically, allowing for unattended mining runs.
 main
