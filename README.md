# Cheezecake's Linux HOTAS

A Space Engineers 1 plugin for native Linux (Pulsar's Interim launcher with CometWorks' linux-compat, no Proton): fly with
joysticks, a separate stick and throttle included, flat or in VR with Cheezecake's SE VR.

**Early release: not yet tried with a real HOTAS.** It compiles against the game, its joystick reading has been run against
a virtual stick, and its game patches were checked against the game's code, but no physical stick and throttle have flown
a ship with it yet. Reports from real hardware, working or not, are very welcome as issues.

On Windows, use ananace's [Analog Grid Control](https://github.com/ananace/dotnet-SEAnalogGridControlPlugin) instead.

## What it does

- Reads every joystick, throttle and pedal set at once, straight from the kernel (`/dev/input/event*`, which a desktop
  session can read for its own user). Devices can come and go while the game runs.
- In a cockpit, adds the joysticks to whatever the keyboard, mouse, gamepad and SE VR's hands ask of the ship that frame:
  move, pitch, yaw, roll and the camera. Full deflection is full gyro torque on every axis, roll included.
- Buttons and hat directions: fire, lock and release target, lights, dampeners, handbrake, power, parking, wheel jump,
  brakes, toolbar slots and pages, and a reverse for a throttle lever (tap to flip, or hold for as long as it is held).
- Wheels: steering follows the stick; drive force follows the throttle (in single player or on a lobby you host; on someone
  else's server wheels drive at full force or none); a brake lever pulses the brakes in proportion to how far it is pulled.
- Programmable blocks named `[AnalogThrottle]` on your grid that you have access to get every axis and button change in
  the format of wolfe's AnalogThrottle plugin, so scripts written for it work unchanged. Devices are named as Linux names
  them, which is usually, not always, the name Windows gives them.

## Setting up

Pulsar's plugin list, Cheezecake's Linux HOTAS, Settings: pick a device, Add bind, then Detect and move the axis or press the button
you want, and choose what it does. Each axis has invert, deadzone and curve. The settings are saved in
`Storage/LinuxHotas.cfg` under the game's user data folder, which can also be edited by hand:

```xml
<?xml version="1.0" encoding="utf-8"?>
<LinuxHotas>
  <!-- Fly as soon as seated; off waits for the ToggleActive action. -->
  <ActiveByDefault>true</ActiveByDefault>
  <!-- Send to [AnalogThrottle] scripts, on one tick in this many in multiplayer. -->
  <WolfeScripts>true</WolfeScripts>
  <MultiplayerScriptEveryTicks>3</MultiplayerScriptEveryTicks>
  <Devices>
    <Device Key="Thrustmaster T.16000M">
      <Bind Input="Y" Output="Pitch" />
      <Bind Input="Z" Output="Throttle" Invert="true" Deadzone="0.02" />
      <Bind Input="X" Output="Roll" Curve="0.5" />
      <Bind Input="Button1" Output="FirePrimary" />
      <Bind Input="Hat1Up" Output="NextToolbarSlot" />
    </Device>
  </Devices>
</LinuxHotas>
```

A device's `Key` is its name as Linux reports it, followed by its serial in brackets when it has one, as the settings
screen's device list shows it. Inputs are `X` `Y` `Z` `RX` `RY` `RZ` `Slider0`..`Slider7`, `Button1`.., and `Hat1Up`/`Right`/`Down`/`Left`;
outputs are the names the settings screen shows, without spaces or the "(stick)"/"(lever)" note.

The game reads one joystick of its own, the one picked in Options > Controls. If that is one of the devices bound here,
the game's own joystick controls act on the ship as well; pick no joystick there, or a different one.

If a device is missing from the list, check that your user can read its `/dev/input/event*` node; the game's log
lists every joystick the plugin found, with its axes, buttons and hats.

## Credits

- Written with ananace's [dotnet-SEAnalogGridControlPlugin](https://github.com/ananace/dotnet-SEAnalogGridControlPlugin)
  (MIT) as the reference for how joysticks fly a ship and how they are set up, and wolfe-labs'
  [SE-AnalogThrottlePlugin](https://github.com/wolfe-labs/SE-AnalogThrottlePlugin) (BSD-3-Clause) as the reference
  for how they reach programmable block scripts.
- Runs on CometWorks' [linux-compat](https://github.com/CometWorks/linux-compat) and Pulsar's Interim launcher.

## Licence

MIT, see [LICENSE](LICENSE).
