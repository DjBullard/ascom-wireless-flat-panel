# Changelog

## 1.1 (unreleased)

### Firmware

* Added support for the Adafruit Feather nRF52832 as an alternative to the nRF52840 Express. The only firmware change actually required is `VBATPIN`, which moves from `A6` (nRF52840) to `A7` (nRF52832) — this pin is wired internally on the Feather module itself, not on the carrier PCB, so it's the one thing that has to change per-board. Everything else (LED control pin, indicator LEDs) sits on standard Feather header positions that are identical between the two boards.
* The device now always advertises over BLE at the fast interval, instead of dropping to a slower interval after 30 seconds. Since this device is only ever powered on right before use (a hardware power switch cuts power entirely otherwise), there's no "idle but powered" battery concern to optimize for, and the faster interval makes the ASCOM driver's device discovery noticeably more reliable.

### ASCOM Driver

* Fixed COM registration: `RegisterForComInterop` was `false` for every build configuration, including the one the solution's "Release" configuration actually maps to, so a normal build never registered the driver with ASCOM despite the (now corrected) documentation saying it would.
* Fixed a bug where the driver could get stuck reporting a stale "connected" state after the device lost power without a clean disconnect (e.g., physically switching the panel off mid-session). Previously, nothing detected this, so the driver kept believing it was still connected — and because the `Connected` setter no-ops when asked to set the same value it already thinks is current, this silently blocked all further reconnect attempts until the driver/client was restarted. The driver now subscribes to the underlying BLE connection status and updates its state when Windows actually detects the link is gone.
* Increased the connect timeout (15s → 30s) and the driver's BLE advertisement watcher now periodically restarts itself while waiting, working around known WinRT `BluetoothLEAdvertisementWatcher` flakiness on long-running scans.
* Device setup dialog:
  * Discovered devices now show live signal strength (RSSI), refreshed as new advertisements arrive, instead of just the raw Bluetooth address — useful for telling nearby panels apart in the field.
  * Added a "What's this?" link next to the trace logging checkbox explaining what it does and where the resulting log file goes.
  * The dialog's title bar now shows the driver version.
  * Added an optional, off-by-default live log (no persistence — just for the current dialog session) showing scan/discovery activity: devices found, signal updates, device selection. Intentionally scoped to the setup dialog only; ASCOM driver convention prohibits showing UI outside of Setup during normal operation, so this can't (and shouldn't) surface during an actual observing session.

### Documentation

* Corrected and expanded the driver build instructions: the referenced `ASCOM.*` assemblies come from the main ASCOM Platform install (via the GAC), not a separate "Developer Components" download; building also requires a Windows 10/11 SDK (for the WinRT Bluetooth metadata used by the driver), which the ".NET desktop development" Visual Studio workload does not install on its own.

## Planned for 1.2

* Investigating a softer reconnect experience: rather than surfacing a hard disconnect to the ASCOM client (N.I.N.A., etc.) the moment the BLE link drops, keep the driver's `Connected` state stable as long as a device remains paired, and have it silently retry the BLE connection in the background — only failing outright when a command actually needs the hardware and it isn't reachable. Not yet implemented.
