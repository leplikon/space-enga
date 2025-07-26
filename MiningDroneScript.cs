// Example script for Space Engineers programmable block
// This script provides a simple autonomous mining drone
// The drone mines ore until its cargo container is full
// Then returns to the base, dumps the ore, and continues mining.
// Note: This script is a simplified demonstration and may need
// adjustments for specific ships or additional safety checks.

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
private IMyCargoContainer cargo;
private IMyShipDrill drill;
private IMyBatteryBlock battery;

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
    return inv.CurrentVolume >= inv.MaxVolume * cargoFullPercent; // threshold from config
}

private bool HasEnoughPower()
{
    return battery.CurrentStoredPower / battery.MaxStoredPower > batteryThreshold; // from config
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

