# space-enga

This repository contains a simple Space Engineers programmable block script for an autonomous mining drone.

## Configuration

The script reads settings from `Me.CustomData` using an INI format. Example:

```
[Settings]
BaseGPS=GPS:Base:0:0:0:
MineGPS=GPS:Mine:100:0:0:
CargoFullPercent=0.9
BatteryThreshold=0.3
```

`BaseGPS` and `MineGPS` accept GPS strings or comma separated coordinates. The percentage values control when the drone heads back to base.
