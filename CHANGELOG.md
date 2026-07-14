# Changelog

## 1.1.1 (unreleased)

### Firmware

* Added support for the Adafruit Feather nRF52832 as an alternative to the nRF52840 Express. Although the carrier PCB uses the standard Feather header footprint, the two chips' Arduino cores number their pins completely differently, so **every** GPIO the firmware uses has to change per board — not just `VBATPIN`. The nRF52832 pin map is: `LED_CONTROL_PIN` 13→16 (panel MOSFET gate), `VBATLED1` 9→27, `VBATLED2` 10→11, `VBATLED3` 11→7, `VBATLED4` 12→15, `VBATPIN` A6→A7.
* nRF52832 hardware bodge (required): on the '832 the battery-sense pin (A7 = P0.31) is the *same* GPIO as the D09 indicator-LED pad, and a populated LED there clamps the ADC and ruins the battery reading. Lift the D09 LED off P0.31 and rewire it to the unused D05 pad (P0.27); the firmware expects `VBATLED1` on pin 27 accordingly. This gives a clean battery reading *and* keeps all four indicator LEDs (including the red connect/disconnect status LED). The nRF52840 needs no rewiring — its battery pin (A6) has no LED on it.
* Kept the firmware as a single sketch (`Arduino_Firmware/Arduino_Firmware.ino`) that supports both boards, selecting the correct pin map automatically from the Arduino IDE's Board menu (via the Adafruit core's `ARDUINO_NRF52832_FEATHER` / `ARDUINO_NRF52840_FEATHER` board macros) — no per-board editing required, and no duplicated firmware to keep in sync.
* The device now always advertises over BLE at the fast interval, instead of dropping to a slower interval after 30 seconds. Since this device is only ever powered on right before use (a hardware power switch cuts power entirely otherwise), there's no "idle but powered" battery concern to optimize for, and the faster interval makes the ASCOM driver's device discovery noticeably more reliable.

### ASCOM Driver

* Fixed COM registration: `RegisterForComInterop` was `false` for every build configuration, including the one the solution's "Release" configuration actually maps to, so a normal build never registered the driver with ASCOM despite the (now corrected) documentation saying it would.
* Fixed a `NullReferenceException` that crashed the Setup dialog when clicking OK (pre-existing since the driver's first commit, `10d2cba`). NINA disposes the driver instance while the modal Setup dialog is still open, which nulls the trace logger (`tl`); the OK handler and `WriteProfile()` then dereferenced it. Trace state is now carried on a plain `traceState` field that survives `Dispose()`, so the OK handler and profile persistence never touch the possibly-null `tl`.
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

* **Softer reconnect experience** (main item): rather than surfacing a hard disconnect to the ASCOM client (N.I.N.A., etc.) the moment the BLE link drops, keep the driver's `Connected` state stable as long as a device remains paired, and have it silently retry the BLE connection in the background — only failing outright when a command actually needs the hardware and it isn't reachable. Not yet implemented; needs a design pass on how `CalibratorOn`/`CalibratorOff` should behave while a silent reconnect is in progress (fail fast vs. block/retry).
* **Re-tag `v1.1.0`** once ready to actually ship it — the tag currently points at an earlier commit than `HEAD`; the apostrophe/BOM fixes and the pre-existing "Bluetooth" trademark-symbol encoding bug fix landed after it was cut.
* **Fix the Inno Setup installer's `SourceDir`**: `Installer/Inno Setup Script.iss` still has the original author's hardcoded local path (`C:\Users\Julien\source\repos\ascom-wireless-flat-panel`). Needs updating (or making relative) before it'll actually compile on this fork, if/when a real installer build is needed.
* **Open the upstream PR** to `jlecomte/ascom-wireless-flat-panel` once this has had more real-world testing. Worth calling out the pre-existing "Bluetooth" trademark-symbol encoding bug specifically, since it predates this fork entirely (present since the very first upstream commit) and upstream would likely want it fixed regardless of the rest of this branch.
* Consider a repo-wide pass for other latent non-UTF-8/no-BOM encoding landmines like the ones found and fixed this round, rather than fixing them reactively one at a time.
