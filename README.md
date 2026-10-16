# PS2000GUI

WinForms control app for an **EA Elektro-Automatik PS 2000 B** lab power supply
(0–84 V / 0–3 A), talking EA's binary telegram protocol over USB serial, and
controlled remotely over **MQTT**.

Built as a course project for **IKT300 Software Architecture and Design** at the
University of Agder (UiA). The first version was a deliberately simple
single-project app; the second assignment turned it into a layered architecture;
the third one split it in two over an MQTT broker, as described below. The tags
`assignment1`, `assignment2` and `assignment3` mark each version, and each step
went through its own issue and pull request.

## Features

- Remote control over MQTT: the GUI and the power supply can run on different machines
- Auto-detects the serial port the power supply is connected to
- Falls back to a simulated power supply when no device answers, to work without the hardware
- Shows device type, serial number, article number and nominal voltage
- Live actual voltage, pushed by the power supply side every second
- Toggles remote control and power output
- Gets and sets the voltage setpoint (disabled while remote control is off)

## Architecture

Two applications that never talk to each other directly, only through the broker:

```
┌──────────────────────────────────────────────┐
│ PS2000GUI       (net8.0-windows, WinForms)   │  UI only: publishes commands,
│                                              │  displays the state it receives
└──────────────────────┬───────────────────────┘
                       │ MQTT (M2Mqtt)
┌──────────────────────▼───────────────────────┐
│ MQTT broker     (Mosquitto, port 1883)       │
└──────────────────────▲───────────────────────┘
                       │ MQTT (M2Mqtt)
┌──────────────────────┴───────────────────────┐
│ PS2000Mqtt      (net8.0 console app)         │  bridge: MQTT on one side,
│                                              │  IPowerSupply on the other
└──────────────────────┬───────────────────────┘
                       │ depends on IPowerSupply only
┌──────────────────────▼───────────────────────┐
│ PS2000Lib       (net8.0 class library)       │
│   IPowerSupply        public interface       │
│   PowerSupplyFactory  public static factory  │
│   PsuStatus           public record          │
│   PS2000              internal: protocol,    │
│                       serial port, scaling   │
│   DummyPowerSupply    internal: in-memory    │
│                       simulator              │
└──────────────────────┬───────────────────────┘
                       │ USB (virtual COM port)
┌──────────────────────▼───────────────────────┐
│ EA PS 2000 B                                 │
└──────────────────────────────────────────────┘
```

- **The GUI does not reference `PS2000Lib` at all.** Its only dependency is the
  M2Mqtt client, so the compiler guarantees that it can reach the device only
  through the broker. The contract between the two apps is the set of MQTT topics
  below, not a C# interface.
- **`PS2000Mqtt` is the device's MQTT client.** The power supply only speaks its
  serial protocol, so this bridge runs on the PC it is plugged into: it publishes
  the device info and status, and applies the commands it receives.
- **Push instead of polling.** The GUI no longer has a timer: `PS2000Mqtt` reads the
  device every second and publishes the status, and the broker pushes it to every
  subscribed GUI.
- **Commands are confirmed by the state.** Clicking a button only publishes a
  command; the button changes once `PS2000Mqtt` has applied it and republished the
  status. The GUI always shows what the device actually is, not what was asked.
- **Threads.** M2Mqtt calls the message handler on its own thread. In `PS2000Mqtt`, a
  `lock` keeps that handler and the status loop from interleaving telegrams on the
  serial port. In the GUI, `Invoke` moves the update onto the UI thread, the only
  one allowed to touch WinForms controls.
- **`PS2000` and `DummyPowerSupply` are `internal`.** The only way to get an
  instance is through `PowerSupplyFactory`, which returns the interface.
  `PowerSupplyFactory.ConnectOrDummy()` tries the real device first and falls back
  to the simulator, so the whole chain can be tested away from the lab.
- **`ReadStatus()` returns an immutable `PsuStatus` record**: remote, output and
  actual voltage come from a single telegram (object 71), so one serial round-trip
  per poll instead of three.

## MQTT topics

Every topic is prefixed with `/PSU/PSU2000/{PSU ID}/`, where the PSU ID is the
device's serial number (`DUMMY001` for the simulator). All messages use QoS 2.

| Topic             | Published by | Payload                   | Retained |
|-------------------|--------------|---------------------------|----------|
| `info/type`       | PS2000Mqtt   | device type               | yes      |
| `info/serial`     | PS2000Mqtt   | serial number             | yes      |
| `info/article`    | PS2000Mqtt   | article number            | yes      |
| `info/nominal`    | PS2000Mqtt   | nominal voltage, `84.00`  | yes      |
| `status/remote`   | PS2000Mqtt   | `ON` / `OFF`              | yes      |
| `status/output`   | PS2000Mqtt   | `ON` / `OFF`              | yes      |
| `status/voltage`  | PS2000Mqtt   | actual voltage, `12.50`   | yes      |
| `status/setpoint` | PS2000Mqtt   | voltage setpoint, `12.50` | yes      |
| `error`           | PS2000Mqtt   | error message             | no       |
| `cmd/remote`      | PS2000GUI    | `ON` / `OFF`              | no       |
| `cmd/output`      | PS2000GUI    | `ON` / `OFF`              | no       |
| `cmd/voltage`     | PS2000GUI    | voltage, `12.5`           | no       |

- **`info` and `status` are retained**, so a GUI that connects later gets the
  current state immediately instead of waiting for the next update.
- **`cmd` is not retained**: otherwise the broker would replay the last command
  every time `PS2000Mqtt` restarts, and could turn the output back on by itself.
- **`error` is not retained** either: it is a one-off event, not a state.
- **Numbers always use a dot** (`CultureInfo.InvariantCulture`), whatever the
  Windows language, so both sides parse them the same way.

Any MQTT client can drive the device: publishing `cmd/remote = ON` from
[MQTT Explorer](https://mqtt-explorer.com/) works just like clicking the button.

## Build and run

Requires Windows (WinForms and the USB CDC driver), the .NET 8 SDK and the
[Mosquitto](https://mosquitto.org/) broker, which runs as a Windows service once
installed.

1. On the PC the power supply is plugged into, start the bridge, passing the broker's
   IP address (default `127.0.0.1`):

   ```bash
   dotnet run --project PS2000Mqtt -- 127.0.0.1
   ```

   It scans the COM ports and uses the first one where a PS 2000 B answers. With no
   device connected it runs the simulator instead, and prints `on dummy`.

2. Start the GUI, enter the broker's IP address and the PSU ID, then click **Connect**:

   ```bash
   dotnet run --project PS2000GUI
   ```

Or open `PS2000GUI.sln` in Visual Studio and start both projects.

### Running on two machines

Mosquitto 2.x only accepts local connections by default. On the machine hosting the
broker, add these lines to `C:\Program Files\mosquitto\mosquitto.conf`:

```
listener 1883 0.0.0.0
allow_anonymous true
```

Then restart the service from an administrator prompt (`net stop mosquitto` then
`net start mosquitto`) and allow inbound TCP port 1883 in the Windows firewall.

## Protocol reference

EA's programming guide for the PS 2000 B series and its object list are available
from the [EA downloads page](https://elektroautomatik.com/shop/en/service/downloads/programming-guide-for-modbus/).
Despite the page title, the PS 2000 B uses EA's own binary telegram format, not
Modbus.
