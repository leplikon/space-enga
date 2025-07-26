// Example script for Space Engineers programmable block
// This script provides a simple autonomous mining drone
// The drone mines ore until its cargo container is full
// Then returns to the base, dumps the ore, and continues mining.
// Note: This script is a simplified demonstration and may need
// adjustments for specific ships or additional safety checks.
// Basic obstacle detection is performed using a forward raycast or sensor.
// Cameras must face forward; the raycast range is limited to 50m, so
// obstacles outside this range will not be detected.

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100; // run every ~1.6s
}

private IMyRemoteControl rc;
private List<IMyThrust> thrusters = new List<IMyThrust>();
private List<IMyGyro> gyros = new List<IMyGyro>();
private IMyShipConnector connector;
private IMyCargoContainer cargo;
private List<IMyCargoContainer> cargoContainers = new List<IMyCargoContainer>();
private IMyShipDrill drill;
private IMyBatteryBlock battery;
// Blocks used for basic obstacle detection
private List<IMyCameraBlock> cameras = new List<IMyCameraBlock>();
private List<IMySensorBlock> sensors = new List<IMySensorBlock>();

// Waypoints
private Vector3D basePosition = new Vector3D(0,0,0); // replace with base coords
private Vector3D miningPosition = new Vector3D(100,0,0); // replace with ore coords

public void Main(string arg, UpdateType updateSource)
{
    if(rc == null) Init();

    if(arg == "dock")
    {
        StartDockingSequence();
        return;
    }

    if(IsCargoFull() || !HasEnoughPower())
    {
        FlyTo(basePosition);
    }
    else if(!IsAtPosition(miningPosition))
    {
        FlyTo(miningPosition);
    }
    else
    {
        if(!drill.Enabled) drill.Enabled = true;
    }
}

// Checks for an obstacle directly in front of the drone within a small safety range
private bool ObstacleInPath(Vector3D destination)
{
    double dist = Vector3D.Distance(rc.GetPosition(), destination);
    double range = Math.Min(dist, 50); // don't scan further than 50 m

    if(cameras.Count > 0)
    {
        var cam = cameras[0];
        if(cam.EnableRaycast && cam.CanScan(range))
        {
            var info = cam.Raycast(range);
            if(!info.IsEmpty()) return true;
        }
    }
    foreach(var s in sensors)
    {
        if(s.IsActive) return true;
    }
    return false;
}

private void Init()
{
    rc = GridTerminalSystem.GetBlockWithName("RC") as IMyRemoteControl;
    GridTerminalSystem.GetBlocksOfType(thrusters);
    GridTerminalSystem.GetBlocksOfType(gyros);
    connector = GridTerminalSystem.GetBlockWithName("Connector") as IMyShipConnector;
    if(connector == null)
    {
        Echo("Error: Connector not found!");
    }

    GridTerminalSystem.GetBlocksOfType(cargoContainers);
    cargo = GridTerminalSystem.GetBlockWithName("Cargo") as IMyCargoContainer;
    if(cargo == null && cargoContainers.Count > 0)
    {
        cargo = cargoContainers[0];
    }
    drill = GridTerminalSystem.GetBlockWithName("Drill") as IMyShipDrill;
    battery = GridTerminalSystem.GetBlockWithName("Battery") as IMyBatteryBlock;

    // Grab cameras or sensors that may be used for obstacle detection
    GridTerminalSystem.GetBlocksOfType(cameras);
    GridTerminalSystem.GetBlocksOfType(sensors);
}

private void FlyTo(Vector3D pos)
{
    rc.ClearWaypoints();
    rc.AddWaypoint(pos, "target");
    // Perform a simple forward raycast before enabling autopilot
    if(ObstacleInPath(pos))
    {
        // Obstacle detected within safety range, stop autopilot
        rc.SetAutoPilotEnabled(false);
    }
    else
    {
        rc.SetAutoPilotEnabled(true);
    }
}

private bool IsAtPosition(Vector3D pos)
{
    return Vector3D.Distance(rc.GetPosition(), pos) < 5; // within 5 m
}

private bool IsCargoFull()
{
    foreach(var container in cargoContainers)
    {
        var inv = container.GetInventory();
        if(inv.CurrentVolume >= inv.MaxVolume * 0.9f)
        {
            return true;
        }
    }
    return false;
}

private bool HasEnoughPower()
{
    // Roughly estimate the power needed to fly back to the base.
    // We multiply the distance to the base by the current ship mass and a
    // constant factor representing average power consumption. This is a very
    // coarse approximation but helps prevent the drone from running out of
    // energy mid-flight.
    double distance = Vector3D.Distance(rc.GetPosition(), basePosition); // meters
    double mass = rc.CalculateShipMass().TotalMass; // kg

    // Assume the ship uses about 0.00001 MW for each kilogram of mass while
    // traveling at cruise speed (~50 m/s). Energy needed is power * time.
    double avgPower = mass * 0.00001; // MW
    double travelTimeHours = (distance / 50.0) / 3600.0; // convert seconds to hours
    double energyNeeded = avgPower * travelTimeHours; // MWh required to reach base

    // Return false if we don't have enough stored power for the return trip.
    double remainingPower = battery.CurrentStoredPower; // MWh
    if (remainingPower < energyNeeded)
        return false;

    // Otherwise ensure we still keep a 30% reserve as before.
    return remainingPower / battery.MaxStoredPower > 0.3f;
}

private void StartDockingSequence()
{
    rc.SetAutoPilotEnabled(false);
    if(connector.Status != MyShipConnectorStatus.Connected)
    {
        rc.ClearWaypoints();
        rc.AddWaypoint(basePosition, "base");
        rc.SetAutoPilotEnabled(true);
    }
    else
    {
        TransferCargoToBase();
    }
}

private void TransferCargoToBase()
{
    if(connector == null) return;
    var target = connector.GetInventory();
    foreach(var container in cargoContainers)
    {
        var inv = container.GetInventory();
        for(int i = inv.ItemCount - 1; i >= 0; i--)
        {
            inv.TransferItemTo(target, i, null, true);
        }
    }
}

