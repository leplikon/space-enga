# Space Enga Mining Drone

This repository contains `MiningDroneScript.cs`, a simple C# script for the **Programmable Block** in Space Engineers. The script controls an autonomous mining drone that travels between a base and a mining waypoint. When the cargo is nearly full or the battery charge is low, the drone returns to base, unloads and resumes mining.

## Features
- Autonomous travel between two waypoints
- Basic obstacle detection using a forward camera or sensor
- Configurable thresholds via `Me.CustomData`

## Configuration
Place the following INI section in the Programmable Block's Custom Data:

```
[Settings]
BaseGPS=GPS:Base:0:0:0:
MineGPS=GPS:Mine:100:0:0:
CargoFullPercent=0.9
BatteryThreshold=0.3
```

`BaseGPS` and `MineGPS` may also be provided as comma-separated coordinates. The percentage values control when the drone heads back to base.

## Usage
1. Load `MiningDroneScript.cs` into a programmable block.
2. Ensure the following blocks exist with these names:
   - **Remote Control** named `RC`
   - **Connector** named `Connector`
   - **Cargo Container** named `Cargo`
   - **Ship Drill** named `Drill`
   - **Battery** named `Battery`
3. Optionally place a forward facing camera or sensor for obstacle detection.
4. Run the script.

## Limitations
- Obstacle checks scan only 50 m ahead and assume the camera faces forward.
- The waypoint is not adjusted if an obstacle is detected; the drone simply halts.
