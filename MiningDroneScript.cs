// Example script for Space Engineers programmable block
// This script provides a simple autonomous mining drone
// The drone mines ore until its cargo container is full
// Then returns to the base, dumps the ore, and continues mining.
// Note: This script is a simplified demonstration and may need
// adjustments for specific ships or additional safety checks.
// Basic obstacle detection is performed using a forward raycast or sensor.
// Cameras must face forward; the raycast range is limited to 50m, so
// obstacles outside this range will not be detected.

private MyIni _ini = new MyIni();
private float cargoFullPercent = 0.9f;
private float batteryThreshold = 0.3f;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100; // run every ~1.6s
    ParseConfig();
}

private IMyRemoteControl rc;
private List<IMyThrust> thrusters = new List<IMyThrust>();
private List<IMyGyro> gyros = new List<IMyGyro>();
private IMyShipConnector connector;
private List<IMyCargoContainer> cargoContainers = new List<IMyCargoContainer>();
private IMyShipDrill drill;
private IMyBatteryBlock battery;
// Blocks used for basic obstacle detection
private List<IMyCameraBlock> cameras = new List<IMyCameraBlock>();
private List<IMySensorBlock> sensors = new List<IMySensorBlock>();

// Waypoints loaded from configuration
private Vector3D basePosition = new Vector3D(0,0,0);
private Vector3D miningPosition = new Vector3D(100,0,0);

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
        if(inv.CurrentVolume >= inv.MaxVolume * cargoFullPercent)
            return true;
    }
    return false;
}

private bool HasEnoughPower()
{
    // Estimate power required to return to base and compare with current reserves
    double distance = Vector3D.Distance(rc.GetPosition(), basePosition);
    double mass = rc.CalculateShipMass().TotalMass;
    double avgPower = mass * 0.00001; // MW consumption per kg at cruise speed
    double travelTimeHours = (distance / 50.0) / 3600.0; // speed ~50 m/s
    double energyNeeded = avgPower * travelTimeHours; // MWh

    double remainingPower = battery.CurrentStoredPower; // MWh
    if (remainingPower < energyNeeded)
        return false;

    return remainingPower / battery.MaxStoredPower > batteryThreshold;
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

private void ParseConfig()
{
    MyIniParseResult result;
    if(!_ini.TryParse(Me.CustomData, out result))
        return;

    basePosition = ParseGPS(_ini.Get("Settings", "BaseGPS").ToString(), basePosition);
    miningPosition = ParseGPS(_ini.Get("Settings", "MineGPS").ToString(), miningPosition);
    cargoFullPercent = _ini.Get("Settings", "CargoFullPercent").ToSingle(cargoFullPercent);
    batteryThreshold = _ini.Get("Settings", "BatteryThreshold").ToSingle(batteryThreshold);
}

private Vector3D ParseGPS(string value, Vector3D fallback)
{
    if(string.IsNullOrWhiteSpace(value)) return fallback;
    if(value.StartsWith("GPS:"))
    {
        var parts = value.Split(':');
        if(parts.Length >= 5)
        {
            double x, y, z;
            if(double.TryParse(parts[2], out x) && double.TryParse(parts[3], out y) && double.TryParse(parts[4], out z))
                return new Vector3D(x, y, z);
        }
    }
    else
    {
        char[] sep = new char[] { ',', ';', ' ' };
        var parts = value.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length == 3)
        {
            double x, y, z;
            if(double.TryParse(parts[0], out x) && double.TryParse(parts[1], out y) && double.TryParse(parts[2], out z))
                return new Vector3D(x, y, z);
        }
    }
    return fallback;
}

