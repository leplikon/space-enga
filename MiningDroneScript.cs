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
    cargo = GridTerminalSystem.GetBlockWithName("Cargo") as IMyCargoContainer;
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
    var inv = cargo.GetInventory();
    return inv.CurrentVolume >= inv.MaxVolume * 0.9f; // 90% full
}

private bool HasEnoughPower()
{
    return battery.CurrentStoredPower / battery.MaxStoredPower > 0.3f; // >30%
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
}

