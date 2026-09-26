# PS2000GUI

WinForms control app for an **EA Elektro-Automatik PS 2000 B** lab power supply
(0–84 V / 0–3 A), talking EA's binary telegram protocol over USB serial.

Built as a course project for **IKT300 Software Architecture and Design** at the
University of Agder (UiA). The first version was a deliberately simple
single-project app; the second assignment turned it into the layered architecture
described below. The tags `assignment1` and `assignment2` mark both versions, and
each step went through its own issue and pull request.

## Features

- Auto-detects the serial port the power supply is connected to
- Shows device type, serial number, article number and nominal voltage
- Live actual voltage, refreshed every 500 ms
- Toggles remote control and power output
- Gets and sets the voltage setpoint (disabled while remote control is off)

## Architecture

Three layers, with the device protocol fully hidden from the UI:

```
┌──────────────────────────────────────────────┐
│ PS2000GUI       (net8.0-windows, WinForms)   │  UI only: controls, timer, display
└──────────────────────┬───────────────────────┘
                       │ depends on IPowerSupply only
┌──────────────────────▼───────────────────────┐
│ PS2000Lib       (net8.0 class library)       │
│   IPowerSupply        public interface       │
│   PowerSupplyFactory  public static factory  │
│   PsuStatus           public record          │
│   PS2000              internal: protocol,    │
│                       serial port, scaling   │
└──────────────────────┬───────────────────────┘
                       │ USB (virtual COM port)
┌──────────────────────▼───────────────────────┐
│ EA PS 2000 B                                 │
└──────────────────────────────────────────────┘
```

- **The UI knows nothing about the device.** No object numbers, bit masks, byte
  decoding or `System.IO.Ports` in `PS2000GUI` — only `IPowerSupply`, `PsuStatus`
  and `PowerSupplyFactory`.
- **`PS2000` is `internal`.** The only way to get an instance is
  `PowerSupplyFactory.Create()`, which returns the interface. Swapping in another
  implementation (a simulator, another PSU model) doesn't touch the UI.
- **The library targets plain `net8.0`**, not Windows: it has no dependency on
  WinForms and could back a console app or tests.
- **`ReadStatus()` returns an immutable `PsuStatus` record** — remote, output and
  actual voltage come from a single telegram (object 71), so one serial round-trip
  per poll instead of three.

## Build and run

Requires Windows (WinForms and the USB CDC driver) and the .NET 8 SDK.

```bash
dotnet run --project PS2000GUI
```

Or open `PS2000GUI.sln` in Visual Studio. Connect the power supply over USB first;
the app scans the COM ports at startup and uses the first one where a PS 2000 B
answers.

## Protocol reference

EA's programming guide for the PS 2000 B series and its object list are available
from the [EA downloads page](https://elektroautomatik.com/shop/en/service/downloads/programming-guide-for-modbus/).
Despite the page title, the PS 2000 B uses EA's own binary telegram format, not
Modbus.
