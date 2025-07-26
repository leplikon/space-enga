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

