using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LinuxHotas.Devices;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using IngameUpdateType = Sandbox.ModAPI.Ingame.UpdateType;

namespace LinuxHotas.Scripts
{
    /// <summary>
    /// wolfe's format (wolfe-labs/SE-AnalogThrottlePlugin), supported under his name so scripts written for his plugin run
    /// unchanged: every change since the last update, one per line as <c>device:axis:value</c>, run as the argument of each
    /// programmable block named <c>[AnalogThrottle]</c> on the seat's grid. Axes are X Y Z RX RY RZ and S0 S1 (sliders)
    /// at 0..65535, P0..P3 the hats as DirectInput angles (hundredths of a degree, 65535 centred), B0..B127 the buttons.
    /// As in his plugin, a pressed button sends 0 and a released one 65535. The device is named as the kernel names it,
    /// which is usually, not always, the name Windows gives it. Written from his plugin's source, not copied.
    /// </summary>
    internal static class WolfeScripts
    {
        public const string Tag = "[AnalogThrottle]";
        private static readonly string[] AxisNames = { "X", "Y", "Z", "RX", "RY", "RZ" };

        private sealed class Last
        {
            public readonly int[] Axes = new int[JoystickDevice.AxisCount];
            public readonly bool[] Buttons = new bool[JoystickDevice.MaxButtons];
            public readonly int[] Hats = new int[JoystickDevice.MaxHats];
        }

        private static readonly Dictionary<JoystickDevice, Last> last = new Dictionary<JoystickDevice, Last>();
        private static readonly List<IMyProgrammableBlock> blocks = new List<IMyProgrammableBlock>();
        private static readonly StringBuilder message = new StringBuilder();
        private static MyCubeGrid blocksOf;
        private static int tick;

        /// <summary>Once a frame on the game thread.</summary>
        public static void Update(IReadOnlyList<JoystickDevice> devices, Mapping.Config config)
        {
            tick++;
            foreach (var gone in last.Keys.Where(d => !devices.Contains(d)).ToList())
                last.Remove(gone);

            // A device's state when it first appears is the baseline, as his plugin takes it when the session starts.
            foreach (var device in devices)
                if (!last.ContainsKey(device))
                    last[device] = Snapshot(device, new Last());

            if (!config.WolfeScripts || MySession.Static == null)
                return;
            if (!MySession.Static.IsServer && tick % Math.Max(1, config.MultiplayerScriptEveryTicks) != 0)
                return;

            message.Clear();
            foreach (var device in devices)
                Changes(device, last[device]);

            var seat = MySession.Static.ControlledEntity as MyShipController;
            if (message.Length == 0 || seat == null || seat.CubeGrid == null)
                return;

            if (blocksOf != seat.CubeGrid || tick % 60 == 0)
                FindBlocks(seat.CubeGrid);
            string argument = message.ToString();
            foreach (var block in blocks)
            {
                try
                {
                    block.Run(argument, IngameUpdateType.Mod);
                }
                catch (Exception e)
                {
                    Plugin.Log($"[AnalogThrottle] '{block.CustomName}' failed to run: {e.Message}");
                }
            }
        }

        private static void FindBlocks(MyCubeGrid grid)
        {
            blocksOf = grid;
            blocks.Clear();
            foreach (var block in grid.GetFatBlocks())
                if (block is IMyProgrammableBlock pb && pb.CustomName != null && pb.CustomName.Contains(Tag))
                    blocks.Add(pb);
        }

        private static void Changes(JoystickDevice device, Last was)
        {
            for (int axis = 0; axis < JoystickDevice.AxisCount; axis++)
            {
                if (device.Axes[axis] == was.Axes[axis])
                    continue;
                string name = axis < AxisNames.Length ? AxisNames[axis] : "S" + (axis - (int)DeviceAxis.Slider0);
                Line(device, name, device.Axes[axis]);
            }
            for (int hat = 0; hat < JoystickDevice.MaxHats; hat++)
                if (device.Hats[hat] != was.Hats[hat])
                    Line(device, "P" + hat, device.Hats[hat] < 0 ? ushort.MaxValue : device.Hats[hat]);
            for (int button = 0; button < JoystickDevice.MaxButtons; button++)
                if (device.Buttons[button] != was.Buttons[button])
                    Line(device, "B" + button, device.Buttons[button] ? ushort.MinValue : ushort.MaxValue);
            Snapshot(device, was);
        }

        private static void Line(JoystickDevice device, string axis, int value)
        {
            if (message.Length > 0)
                message.Append('\n');
            // His scripts split each line on ':' and the message on '\n', so neither may appear in the name.
            message.Append(device.Name.Replace(':', ' ').Replace('\n', ' ')).Append(':').Append(axis).Append(':').Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static Last Snapshot(JoystickDevice device, Last into)
        {
            Array.Copy(device.Axes, into.Axes, into.Axes.Length);
            Array.Copy(device.Buttons, into.Buttons, into.Buttons.Length);
            Array.Copy(device.Hats, into.Hats, into.Hats.Length);
            return into;
        }
    }
}
