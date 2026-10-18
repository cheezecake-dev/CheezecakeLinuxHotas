# Cheezecake's Linux HOTAS: support guide

Written for version 0.5.1 (assembly version 0.5.1.0, the one Pulsar's plugin list shows). Every default, name and log line
below is taken from the plugin's source at that version. Each section stands alone, so it can be quoted by itself.

## Summary

Cheezecake's Linux HOTAS is a Space Engineers 1 plugin that lets joysticks, a separate stick and throttle, and pedals fly
a ship on Linux. It reads the devices straight from the kernel and adds their input to whatever the keyboard, mouse,
gamepad and SE VR's hands already ask of the ship. It ships with no binds: nothing happens until the player binds something
in the plugin's settings.

Early release: the README says it has not yet been tried with a real HOTAS. It has been run against a virtual stick.
Reports from real hardware, working or not, are welcome as GitHub issues.

On Windows, use ananace's Analog Grid Control plugin instead: https://github.com/ananace/dotnet-SEAnalogGridControlPlugin

## What it does and does not do

It does:

- Read every joystick, throttle, pedal set and pad found under `/dev/input/event*`, all at once, and notice devices being
  plugged in or unplugged while the game runs.
- While the player sits in a ship seat, add the devices' axes to the ship's move (thrust and strafe), pitch, yaw and
  roll, and rotate the camera.
- Run buttons and hat directions as actions: fire, lock and release target, lights, dampeners, handbrake, power, parking,
  wheel jump, brake, toolbar slots and pages, reverse throttle, and a switch that turns the joysticks on and off.
- Drive wheeled grids: steering from the stick, drive force from the throttle, wheel brakes from a button or a lever.
- Send axis and button changes to programmable blocks named `[AnalogThrottle]`, in the format of wolfe's AnalogThrottle
  plugin, so scripts written for that plugin run unchanged.
- Work flat or in VR with Cheezecake's SE VR (the joystick input is added to the hands' request, not instead of it).

It does not (there is no code for any of these in the plugin):

- Control the player's character on foot. Only the ship seat the local player is flying is controlled.
- Remap the keyboard, mouse or gamepad.
- Send anything to the device (no force feedback, no LEDs).
- Take exclusive control of a device. It opens each device read-only and shares it with the game and anything else.
- Work on Windows. It calls the Linux kernel's input interface directly.

## Requirements

- Linux, 64-bit. The plugin calls `open`, `read` and `ioctl` in `libc.so.6` and uses the 64-bit layout of the kernel's
  `input_event` structure.
- Space Engineers 1 run natively through Pulsar's Interim launcher with CometWorks' linux-compat, as the README describes
  ("native Linux, no Proton").
- Pulsar, to install the plugin.
- Permission for the player's user to open the device nodes `/dev/input/event*` for reading. The plugin does nothing to
  grant this; see "Device is not in the list" below.
- Not used: SDL. The plugin reads evdev directly so it does not touch linux-compat's SDL thread. The game's own SDL joystick
  reading is separate and keeps working.

## Native versus Proton

The plugin has no code that detects or adapts to Proton or Wine. It reads `/dev/input/event*` through `libc.so.6` and
has no Windows code path. The README describes it as a plugin for native Linux with no Proton. A support report should
always say whether the game ran natively or under Proton, because only the native setup is described by the source.

## Install, update and uninstall

- Install: enable the plugin in Pulsar's plugin list (Cheezecake's Linux HOTAS). Restart the game so it loads.
- Update: update it through Pulsar. The version Pulsar shows is the version compiled in (0.5.1.0 at this release).
- Settings: Pulsar's plugin list, Cheezecake's Linux HOTAS, Settings.
- Uninstall: disable or remove the plugin in Pulsar. The plugin leaves its settings file behind (see below); delete it by
  hand if it should go too.

## Settings file: location, format and reset

The file is `Storage/LinuxHotas.cfg` inside the game's user data folder, which on native Linux is `~/.config/SpaceEngineers`,
so the full path is `~/.config/SpaceEngineers/Storage/LinuxHotas.cfg`. It appears once the settings screen has been closed.
It is XML. The plugin reads it once, when the game starts, and writes it when the settings screen is closed.

Example of what the plugin writes:

```xml
<?xml version="1.0" encoding="utf-8"?>
<LinuxHotas>
  <ActiveByDefault>true</ActiveByDefault>
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

(The plugin's own save does not write comments and may add XML namespace attributes to the root element.)

Rules the plugin follows:

- Edit the file only while the game is closed. The game holds the settings in memory and rewrites the file when the
  settings screen closes, so a hand edit made while the game runs is overwritten.
- When saving, the plugin removes binds with an empty `Input` or `Output`, removes devices left with no binds, writes to
  `LinuxHotas.cfg.tmp` first and then replaces the real file.
- Names are case-sensitive.
- A bind with an unknown input or output is ignored (and logged); the rest of the file still loads.
- If the file cannot be parsed, the plugin renames it to `LinuxHotas.cfg.unreadable-<yyyyMMdd-HHmmss>` next to the original
  and starts from defaults.

To reset all settings: close the game and delete (or rename) `Storage/LinuxHotas.cfg`. The next start uses the defaults
below, with no binds. To reset one device: delete its `<Device>` element, or remove its binds in the settings screen.

## Every setting

### Config file keys

| Key | Type | Allowed values | Default | In-game label |
| --- | --- | --- | --- | --- |
| `ActiveByDefault` | true/false | `true`, `false` | `true` | "Fly as soon as seated" |
| `WolfeScripts` | true/false | `true`, `false` | `true` | "Send to [AnalogThrottle] scripts" |
| `MultiplayerScriptEveryTicks` | whole number | any integer; 1 or less behaves as 1 | `3` | none (file only) |
| `Devices` / `Device` / `Key` | text | the device's key, see "Device names and keys" | none | the "Device" drop-down |
| `Bind` | element | one per bound input | none | one row of the bind table |

Bind attributes:

| Attribute | Type | Allowed values | Default | In-game label |
| --- | --- | --- | --- | --- |
| `Input` | text | `X` `Y` `Z` `RX` `RY` `RZ` `Slider0` to `Slider7`; `Button1` to `Button128`; `Hat1Up` `Hat1Right` `Hat1Down` `Hat1Left`, likewise `Hat2` to `Hat4` | empty (a bind with none is removed on save) | "Input" |
| `Output` | text | a name from the lists under "Outputs" | empty (removed on save) | "Does" |
| `Invert` | true/false | `true`, `false` | `false`; not written when false | "Invert" |
| `Deadzone` | decimal fraction | used from 0 to 0.99 (values outside are clamped); the settings slider covers 0 to 0.5 | `0.05` (5 %); written only for axis inputs and only when not 0.05 | "Deadzone" slider, shown in percent |
| `Curve` | decimal | used from 0 to 1 (values outside are clamped) | `0` (linear); written only for axis inputs and only when not 0 | "Curve" slider, 0 to 1 |

### What each setting does

- Fly as soon as seated (`ActiveByDefault`): when on, the joysticks fly a ship as soon as the player sits in a seat. When
  off, they stay idle after sitting down until a bind to Toggle Active is pressed. It is applied each time the player
  goes from no seat to a seat.
- Send to [AnalogThrottle] scripts (`WolfeScripts`): see "Programmable block scripts".
- `MultiplayerScriptEveryTicks`: in multiplayer, when the player is not the server, script updates are sent on one frame
  in this many. Single player and a lobby the player hosts send every frame. There is no control for it in the settings
  screen; edit the file.
- Invert: flips the axis before anything else happens to it.
- Deadzone: see "Axis processing".
- Curve: see "Axis processing".

## The settings screen

Open it from Pulsar's plugin list: Cheezecake's Linux HOTAS, Settings. The title is "Cheezecake's Linux HOTAS". Escape or
the Close button saves and closes. Controls, in order:

- Device: a drop-down of the connected devices, followed by devices that have saved binds but are not plugged in, marked
  "(not connected)". A line beside it shows "N axes, N buttons, N hats", or "not connected", or "No joysticks found".
- Fly as soon as seated, Send to [AnalogThrottle] scripts: the two check boxes.
- The bind table for the selected device, with columns Input, Does, Options and Now. Options shows "inverted",
  "deadzone N%" and "curve N.NN". Now shows the bind's live value with two decimals, which is a good way to check an
  axis moves and which way. A bind whose output is unknown is drawn in red-orange.
- Input: a drop-down of the selected device's axes, "Button N" and "Hat N Direction". A device that is not connected
  offers X to Slider1, 32 buttons and 1 hat.
- Detect: waits 10 seconds for any joystick to move an axis (more than about 30 % of its travel), press a button or push
  a hat, then sets the selected bind's input to it. If nothing moves, the status line says "Nothing moved." If it was
  another device that moved, the screen switches to that device. Buttons are checked first, then hats, then axes.
- Does: a drop-down of outputs. Axes are listed with "(stick)" or "(lever)" after the name.
- Invert, Deadzone (0 to 50 %, default 5 %), Curve (0 to 1, default 0).
- Add bind: adds a bind on the selected device. It starts as the device's first axis driving Pitch, or Button 1 driving
  Fire Primary if the device has no axes. The status line says "Added; choose its input (or Detect) and what it does." With
  no joystick it says "Connect a joystick first."
- Remove bind, Close.

## Outputs: what can be bound

Names in the settings screen put spaces in the config name ("ToolbarSlot1" shows as "Toolbar Slot 1"). In the file use the
name without spaces.

### Axis outputs

A stick output reads -1 to 1 with its deadzone in the middle. A lever output reads 0 to 1 with half the deadzone at each end.

| Settings screen | Config `Output` | Kind | Effect |
| --- | --- | --- | --- |
| Throttle (lever) | `Throttle` | lever | Forward thrust; Reverse Throttle turns it into backward thrust |
| Backward (lever) | `Backward` | lever | Backward thrust |
| Forward Backward (stick) | `ForwardBackward` | stick | Thrust forward and back |
| Left (lever) | `Left` | lever | Strafe left; steering left on wheeled grids |
| Right (lever) | `Right` | lever | Strafe right; steering right on wheeled grids |
| Left Right (stick) | `LeftRight` | stick | Strafe and steering |
| Up (lever) | `Up` | lever | Thrust up |
| Down (lever) | `Down` | lever | Thrust down |
| Up Down (stick) | `UpDown` | stick | Thrust up and down |
| Pitch (stick) | `Pitch` | stick | Pitch; stick forward noses down |
| Yaw (stick) | `Yaw` | stick | Yaw |
| Roll (stick) | `Roll` | stick | Roll |
| Camera Pitch (stick) | `CameraPitch` | stick | Turns the camera up and down |
| Camera Yaw (stick) | `CameraYaw` | stick | Turns the camera left and right |
| Wheel Brake (lever) | `WheelBrake` | lever | Wheel brakes, in proportion to the pull |

### Action outputs

An action fires when its bind's value reaches 0.75 (a button or hat direction: pressed). An axis bound to an action is
read as a lever and fires once it passes three quarters of its travel.

| Settings screen | Config `Output` | Effect |
| --- | --- | --- |
| Toggle Active | `ToggleActive` | Switches the joysticks' hold on the ship on and off |
| Reverse Throttle | `ReverseThrottle` | Flips the Throttle direction. A tap keeps it flipped; held longer than half a second, it flips back on release |
| Reverse Throttle Toggle | `ReverseThrottleToggle` | Flips the Throttle direction on each press |
| Reverse Throttle Hold | `ReverseThrottleHold` | Flips the Throttle direction while held, back on release |
| Fire Primary | `FirePrimary` | The seat's primary action (starts on press, stops on release) |
| Fire Secondary | `FireSecondary` | The seat's secondary action (starts on press, stops on release) |
| Lock Target | `LockTarget` | Requests a target lock, if target locking is enabled for the seat |
| Release Target | `ReleaseTarget` | Releases the target lock, if target locking is enabled for the seat |
| Wheel Jump | `WheelJump` | Wheel jump while held (wheeled grids) |
| Brake | `Brake` | Full wheel brake while held (wheeled grids) |
| Lights | `Lights` | Switches the grid's lights |
| Dampeners | `Dampeners` | Switches inertia dampeners |
| Handbrake | `Handbrake` | Switches the handbrake |
| Power | `Power` | Switches the grid's reactors |
| Parking | `Parking` | Switches landing gear |
| Next Toolbar Slot / Previous Toolbar Slot | `NextToolbarSlot` / `PreviousToolbarSlot` | Moves the toolbar selection |
| Toolbar Slot 1 to 9 | `ToolbarSlot1` to `ToolbarSlot9` | Activates that toolbar slot |
| Empty Hands | `EmptyHands` | Activates the toolbar's empty-hands slot |
| Next Toolbar / Previous Toolbar | `NextToolbar` / `PreviousToolbar` | Next or previous toolbar page |
| Toolbar 1 to 9 | `Toolbar1` to `Toolbar9` | Switches to that toolbar page |

## How input reaches the game

- Axes: the plugin hooks the ship seat's `MoveAndRotate` call, which the game makes every frame with the keyboard, mouse,
  gamepad and SE VR request. The joystick request is added to the values in that call. Nothing is replaced, so the joystick
  and the keyboard can be used together; they add.
- Camera Pitch and Camera Yaw rotate the active camera directly.
- Buttons and hat directions call the seat's own functions: shoot, switch lights, switch dampeners, switch handbrake,
  switch reactors, switch landing gears, and the seat's toolbar.
- Wheel brake and wheel jump are added to the game's own every-frame brake and jump calls, so the space key and the
  joystick do not cancel each other.
- Wheel drive force is scaled by how far the throttle is pushed (see "Wheeled grids").
- The joysticks act only when all of these are true: the local player is in a ship seat, the joysticks are active
  (Fly as soon as seated, or Toggle Active), no gameplay screen is open (the terminal or a cockpit text editor takes the
  input), and the game's input is not disabled. Fire buttons are still released when control lapses, so a gun does not
  keep firing.

## Axis processing

Exactly as coded, for an axis input:

1. The device's reading is mapped from the device's reported minimum and maximum to 0 to 65535. The centre is 32768. If
   the device reports an empty range, the axis reads 32768. An axis the device does not have reads 32768.
2. That is divided by 65535 to give 0 to 1.
3. If Invert is on, it becomes 1 minus that.
4. For a stick output (Forward Backward, Left Right, Up Down, Pitch, Yaw, Roll, Camera Pitch, Camera Yaw): the value is
   mapped to -1 to 1; anything with size at or below the deadzone becomes 0; the remainder is rescaled so the
   deadzone's edge is 0 and full deflection is 1; then the curve is applied to the size and the sign kept.
5. For every other output (levers, and any axis bound to an action): half the deadzone is cut from each end, so the stops
   read exactly 0 and 1 and the rest is rescaled between; then the curve is applied.
6. Curve: result = a x size cubed + (1 minus a) x size, where `a` is the Curve value limited to 0 to 1. 0 is linear; 1
   is gentle near rest and quick at full travel.
7. Deadzone is limited to 0 to 0.99 when used.
8. Scaling into the game: pitch and yaw are multiplied by 20, roll by 5, camera by 20. The game divides rotation by 20 and
   multiplies roll by 0.2, so full deflection is full gyro torque on every axis, roll included. Move values go in at -1 to 1.
9. Signs: Pitch and Camera Pitch are negated (a stick pushed forward reads -1 and noses down). Throttle is multiplied by the
   Throttle direction (forward by default, reversed by the Reverse Throttle actions). Down is negated; Left is negated.

When several binds drive the same output, the one pushed furthest (largest absolute value) wins; they are not added to each
other. The result is then added to the keyboard, mouse, gamepad and SE VR input for that frame.

For a button or a hat direction bound to an action, the value is 1 when pressed and 0 otherwise. Deadzone and Curve are not
used (and not saved). A button or hat bound to an axis output gives 1 or 0, which is useful for digital thrust.

## Device names and keys

- Each device is identified by a key: its name as the kernel reports it, followed by its serial in square brackets when it
  reports one, for example `Thrustmaster T.16000M [1234]`. If the kernel gives no name, the key is the device node path.
- Two identical devices with the same key are told apart by the order found: the later ones get " #2", " #3". That
  number is not stable between runs, so a pair without serials may swap binds. Give each its own binds again if so.
- Binds belong to the key, not to the `/dev/input/eventN` number, so they survive replugging and reboots.
- The key shown in the settings screen is the key to use in the file's `Key` attribute.
- Programmable block scripts get the device name without the serial and without any colon or newline.

## How devices are found and read

- API: the kernel's evdev interface (`linux/input.h`), through `open`, `read` and `ioctl` in `libc.so.6`. Not SDL.
- Path: every file named `event*` in `/dev/input`, opened read-only and non-blocking. Opened in order of node number (event2
  before event10).
- Scan: at start, then every 2 seconds. A device node that cannot be opened, or is not a joystick, is skipped silently and
  tried again about every 30 seconds.
- What counts as a joystick: the device must have absolute axes, and it must not be a pointer, a touch device (touchscreen
  or touchpad), a graphics tablet or a motion sensor, and it must have joystick or gamepad buttons (kernel codes 0x120 to
  0x13f), trigger-happy buttons (0x2c0 to 0x2e7), or any of the axes RX, RY, RZ, throttle, rudder, wheel, gas or brake.
  Gamepads therefore qualify. Mice and keyboards do not.
- Axes: the kernel's X, Y, Z, RX, RY, RZ axes (codes 0 to 5) become X, Y, Z, RX, RY, RZ. Every other absolute axis the device
  has, in kernel code order (throttle, rudder, wheel, gas, brake and so on), becomes Slider0, Slider1 and so on, up to
  Slider7. Further axes are ignored. Hat axes are not counted as axes.
- Hats: up to 4, from the kernel's hat axes, numbered from 1. Diagonal positions count as both directions
  (up-right presses Hat Up and Hat Right).
- Buttons: up to 128, numbered from 1. The order is the one SDL uses: buttons from joystick button code 0x120 upward first,
  then the codes below that. So "Button 1" is the first joystick button, which is usually the trigger. This matches the
  numbers the game shows for the same device.
- Unplugging: when reading a device fails (as when it is unplugged), the plugin closes it, logs the removal and carries on.
  When the device comes back it is found within about 2 seconds and its saved binds apply again.
- Dropped events: if the kernel reports it dropped events, the plugin reads the device's full state again.
- Sharing: the device is opened read-only and not grabbed. The game's own SDL joystick and any other program see the same
  events. If the device chosen in the game's Options, Controls is one of the devices bound in the plugin, the game's own
  joystick controls act on the ship as well (README).

## How to use it in play

1. Plug in the stick, throttle and pedals. Start the game with the plugin enabled.
2. Open Pulsar's plugin list, Cheezecake's Linux HOTAS, Settings. Pick the device.
3. Add bind. Press Detect and move the axis or press the button wanted (within 10 seconds). Choose what it does
   in "Does". Adjust Invert, Deadzone and Curve. The "Now" column shows the live value.
4. Typical set-up: stick Y to Pitch, stick X to Roll, twist (RZ or Z) to Yaw, throttle lever to Throttle, one button to Fire
   Primary, one to Reverse Throttle, hat directions to toolbar slots or pages. Pedals to Yaw or Wheel Brake.
5. Close (or Escape) to save.
6. Sit in a ship seat. With Fly as soon as seated on, the sticks fly the ship at once.

## Wheeled grids

- Steering follows the stick (Left, Right or Left Right outputs).
- Drive force follows the throttle in single player or on a lobby the player hosts. On someone else's server the wheels
  drive at full force or none.
- Wheel Brake (lever) pulses the wheel brakes in proportion to the pull, because the game's brakes are on or off: below 5 %
  of pull the brakes are off, above 95 % fully on, and in between they are on for that fraction of every 10 frames. The
  Brake action is a full brake while held.
- Wheel Jump works while its button is held. Brake and Wheel Jump need the seat to control wheels.
- Only the seat the local player is flying is affected. Other grids, including those of other players on a lobby host,
  keep the game's on/off wheel force.

## Programmable block scripts

With "Send to [AnalogThrottle] scripts" on, every change on any connected joystick is sent to programmable blocks whose custom
name contains `[AnalogThrottle]`, on the grid of the seat the player is in, and that the player has access to. The block
is run with the changes as its argument, in the format of wolfe's AnalogThrottle plugin: one change per line as
`device:axis:value`.

- device: the device name as Linux reports it (not the serial), with any `:` or newline replaced by a space. This is
  usually, not always, the name Windows gives the same device.
- axis: `X` `Y` `Z` `RX` `RY` `RZ`; `S0` to `S7` for sliders; `P0` to `P3` for hats; `B0` to `B127` for buttons (counted from
  0 here, unlike the settings screen).
- value: 0 to 65535 for axes, with the centre at 32768. A hat is its angle in hundredths of a degree clockwise from up
  (65535 when centred). A pressed button sends 0 and a released button 65535, as wolfe's plugin does.
- A device's state when it is first seen is the baseline; only changes after that are sent.
- Works whether or not the joysticks are active, and is not blocked by an open terminal. It needs the player to be in a ship seat.
- The list of blocks is refreshed when the grid changes and about once a second.
- In multiplayer, as a client, updates go out on one frame in `MultiplayerScriptEveryTicks` (default 3).

## Log lines

The plugin writes to the game's log. Every line begins with `LinuxHotas: `. Ask for the game's log, the newest
`SpaceEngineers_<date>_<time>.log` in `~/.config/SpaceEngineers/` (one file, `SpaceEngineers.log`, with Pulsar's
`-stableLogs` option), and search it for `LinuxHotas`. If the plugin does not appear to load at all, also ask for Pulsar's
log, `~/.config/Pulsar/Legacy/info.log` (or `Interim/info.log` instead of `Legacy` if the player created an `Interim`
folder in the Pulsar folder). Pulsar overwrites it on every launch, so ask for it right after the problem happens.

### Normal

- `LinuxHotas: loaded; N joystick(s), binds for M device(s)`: written once at start. N is how many devices were found at
  that moment, M how many devices have saved binds.
- `LinuxHotas: Joystick found: 'Name' (/dev/input/eventN; axes X Y ...; N buttons; N hats)`: a device was opened. The list
  shows exactly what the plugin sees.
- `LinuxHotas: Joystick removed: 'Name'`: the device was unplugged, or the kernel stopped serving it.

### Problems

- `LinuxHotas: Cannot list /dev/input: <reason>`: the plugin cannot read the folder. Check that `/dev/input` exists and is
  readable inside the environment the game runs in (for example a sandboxed game install that does not expose `/dev/input`).
- `LinuxHotas: Ignoring the bind '<Input> => <Output>' for '<Key>': unknown input or output`: the config file has a bind with
  a name the plugin does not know. Check spelling and capitals against the lists above, or remove the bind. The other
  binds still load.
- `LinuxHotas: Cannot read <path> (<reason>); moved it to <path>.unreadable-<date>-<time> and started from defaults`: the config
  file was not valid XML. The old file is kept under the new name; binds start empty. Fix the old file and rename it
  back (game closed), or rebuild the binds in the settings screen.
- `LinuxHotas: Cannot save <path>: <reason>`: the settings could not be written. Check that the folder exists, has space, and
  is writable.
- `LinuxHotas: stopped after an error: <details>`: an unexpected error happened while handling input. The plugin then stops
  doing anything until the game is restarted, so the sticks go dead mid-session. Send the line with its details in an issue.
- `LinuxHotas: [AnalogThrottle] '<block name>' failed to run: <reason>`: the programmable block refused the run. Check that
  the block is working and not locked, and that the script handles the argument.

## Troubleshooting: symptom, cause, fix

### The settings screen says "No joysticks found" or the device is missing

- Cause 1: the user cannot read the device node. The plugin skips a node it cannot open without any log line.
- Fix 1: in a terminal, run `ls -l /dev/input/by-id/` (or `ls -l /dev/input/event*`) and check that the stick's node is
  readable by the player's user. On a normal desktop session the logged-in user can usually read joystick nodes; if not,
  the node's group or a udev rule decides. The plugin cannot change that. After fixing, the device appears within about
  30 seconds without restarting the game.
- Cause 2: the device is not a joystick by the plugin's rules (no absolute axes, or it looks like a pointer or a touch device).
- Fix 2: look for a `Joystick found` line in the log. If the device is missing from it, report the output of
  `evtest` or `cat /proc/bus/input/devices` for it in an issue.
- Cause 3: the game cannot see `/dev/input` (for example a sandboxed install). Look for `Cannot list /dev/input`.
- Fix 3: give the sandbox access to `/dev/input`, or run the game from a non-sandboxed install.

### Joysticks are found but nothing moves the ship

- Cause: no binds. The plugin ships with none.
- Fix: bind the axes in the settings screen. Check the "Now" column moves when the stick moves.
- Cause: not seated in a ship seat, or the joysticks are switched off.
- Fix: sit in a cockpit or ship seat. If "Fly as soon as seated" is off, press the button bound to Toggle Active.
- Cause: a terminal or a cockpit text editor is open.
- Fix: close it.
- Cause: the plugin stopped after an error.
- Fix: search the log for `stopped after an error`; restart the game and report the line.

### An axis is backwards

- Fix: tick Invert on that bind. For a stick forward-and-back this is the normal case: the plugin treats a stick pushed
  forward as nose down.

### The ship drifts or turns by itself with the stick centred

- Cause: the deadzone is too small for that stick's centre noise.
- Fix: raise Deadzone on that bind. The default is 5 %.
- Cause: a throttle lever or slider bound as a stick axis, or an unused axis bound by mistake.
- Fix: bind a lever to a lever output (Throttle, Backward, Up, Down, Wheel Brake), not to a stick output.

### The throttle goes the wrong way or reverses

- Cause: Reverse Throttle was pressed, which flips the Throttle direction, and it stays flipped after a tap.
- Fix: press it again. Use Reverse Throttle Hold if it should only reverse while held.

### The wrong axis moves, or the numbers differ from Windows

- Cause: axes beyond X, Y, Z, RX, RY, RZ become Slider0 to Slider7 in the kernel's order, which can differ from the order
  Windows uses for the same device.
- Fix: use Detect and the "Now" column to find the right input rather than assuming.

### Binds were lost or applied to the wrong stick

- Cause: binds follow the device key. Two identical devices without serials are numbered by the order found.
- Fix: bind each again; they are told apart by order, which can change.
- Cause: the config file was unreadable and moved aside. Look for `Cannot read` in the log.
- Fix: see that entry above.

### The game's own joystick controls also act, so the ship doubles up

- Cause: the device picked in Options, Controls is also bound in the plugin.
- Fix: pick no joystick in Options, Controls, or a different one.

### Wheels drive at full force or none

- Cause: playing on someone else's server; the force is worked out where the grid is simulated.
- Fix: none; it works in single player and on a lobby the player hosts.

### A script does not receive input

- Fix: check the programmable block's name contains `[AnalogThrottle]`, that it is on the grid of the seat you sit in, that you
  have access to it, that "Send to [AnalogThrottle] scripts" is on, and that you are seated. Axis names are `X`, `S0`, `P0`,
  `B0` (buttons count from 0 here).

### The game under Proton

- The plugin's source has no Proton handling. Ask for the Pulsar launch method and whether the game is native, and send the
  player to an issue with the log if the log shows no `LinuxHotas:` lines.

## Known issues and limits

- Early release; not yet tried with a real HOTAS (README). Real-hardware reports are wanted.
- Linux 64-bit with glibc's `libc.so.6` only.
- Only a ship seat the local player is flying. No on-foot control.
- Up to 8 sliders, 4 hats and 128 buttons per device; further inputs are ignored.
- Buttons act only while the player is in control (not with the terminal open); script updates are the exception.
- Binds on the same output do not add; the strongest wins.
- On wheeled grids the brake lever pulses the brakes; the game's brakes are on or off.
- Wheel drive scaling works in single player and on a lobby the player hosts only.
- Identical devices without serials are numbered by the order found, which is not stable.
- After "stopped after an error" the plugin does nothing until the game restarts.
- `MultiplayerScriptEveryTicks` has no settings screen control.
- The device name given to scripts is the kernel's, which may differ from Windows.
- No default binds; nothing happens until the player binds something.

## Conflicts and alternatives

- On Windows, use ananace's Analog Grid Control: https://github.com/ananace/dotnet-SEAnalogGridControlPlugin
- Any other plugin that adds joystick input to the ship (ananace's plugin does this, and so does wolfe's AnalogThrottle)
  would add to this one's input if both are enabled. Run one of them.
- The game's own joystick: if the device chosen in Options, Controls is also bound here, both act. Pick no joystick there,
  or a different one.
- The plugin's source names no other plugin as a known conflict. It is designed to coexist with the keyboard, mouse,
  gamepad and Cheezecake's SE VR (their input is added to).

## FAQ

**Does it work on Windows?** No. Use ananace's Analog Grid Control instead.

**Does it work with Proton?** The plugin is for native Linux with Pulsar's Interim launcher and linux-compat. Its source has
no Proton handling.

**Do I need SDL or extra drivers?** No. It reads `/dev/input/event*` itself. The kernel must expose the device there.

**Does it work in VR?** Yes, with Cheezecake's SE VR: the joystick input is added to the hands' request.

**What can it bind?** Any joystick axis, button or hat direction (up to 14 axes, 128 buttons, 4 hats per device) to the ship
axes and actions listed under "Outputs".

**Where are my settings?** `~/.config/SpaceEngineers/Storage/LinuxHotas.cfg`. Delete it with the game closed to reset.

**How do I make a throttle lever work?** Bind the lever's axis to Throttle (lever). Use Invert if it is backwards. Bind a
button to Reverse Throttle for backward thrust.

**Why is the default deadzone 5 %?** That is the value the plugin starts each new bind with; change it per bind.

**Can I use two sticks at once?** Yes, every device is read at once. If two binds drive the same output, the one pushed
furthest wins.

**Does it support force feedback?** No.

**Can I fly the ship on foot controls or with a controller too?** The joystick input is added to the keyboard, mouse and
gamepad input, so they can be used together.

**Do scripts written for wolfe's AnalogThrottle work?** Yes, in the same line format, for blocks named `[AnalogThrottle]`.
Device names are the kernel's names.

**Why do my joysticks not fly after I sit down?** Either "Fly as soon as seated" is off and Toggle Active has not been
pressed, or there are no binds.

## Reporting a problem on GitHub

Open an issue at https://github.com/cheezecake-dev/CheezecakeLinuxHotas/issues and include:

- The plugin version (Pulsar's plugin list shows it; 0.5.1.0 at this release).
- The device name and its vendor and product ids (from `cat /proc/bus/input/devices` or `lsusb`).
- The Linux distribution and version.
- Whether the game runs natively through Pulsar's Interim launcher or under Proton.
- The lines of the game's log that contain `LinuxHotas`, especially `Joystick found`, and any `Cannot ...` or
  `stopped after an error` line.
- The plugin's settings file `~/.config/SpaceEngineers/Storage/LinuxHotas.cfg`, or the binds involved.
- What was expected and what happened, and whether in single player, on a hosted lobby or on someone else's server.

## Questions this guide doesn't answer

If a question isn't covered here, or it looks like a bug, open an issue at
https://github.com/cheezecake-dev/CheezecakeLinuxHotas/issues with the details listed under "Reporting a problem on
GitHub". For anything else, ask Cheezecake, the plugin's author, directly in the Pulsar Discord by mentioning them in the
message: a mention from the player is what notifies them.
