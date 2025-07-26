// Example script for Space Engineers programmable block
// This script provides a simple autonomous mining drone
// The drone mines ore until its cargo container is full
// Then returns to the base, dumps the ore, and continues mining.
// Note: This script is a simplified demonstration and may need
// adjustments for specific ships or additional safety checks.

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

private void Init()
{
    rc = GridTerminalSystem.GetBlockWithName("RC") as IMyRemoteControl;
    GridTerminalSystem.GetBlocksOfType(thrusters);
    GridTerminalSystem.GetBlocksOfType(gyros);
    connector = GridTerminalSystem.GetBlockWithName("Connector") as IMyShipConnector;
    cargo = GridTerminalSystem.GetBlockWithName("Cargo") as IMyCargoContainer;
    drill = GridTerminalSystem.GetBlockWithName("Drill") as IMyShipDrill;
    battery = GridTerminalSystem.GetBlockWithName("Battery") as IMyBatteryBlock;
}

private void FlyTo(Vector3D pos)
{
    rc.ClearWaypoints();
    rc.AddWaypoint(pos, "target");
    rc.SetAutoPilotEnabled(true);
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
}

