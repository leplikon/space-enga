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
private List<IMyCargoContainer> cargoContainers = new List<IMyCargoContainer>();
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

