using System;
using System.Collections.Generic;

namespace LinuxHotas.Devices
{
    /// <summary>A device's axes in DirectInput's layout, so bindings and wolfe's script format read the same as on
    /// Windows. Absolute axes past the six named ones become sliders, in kernel code order.</summary>
    public enum DeviceAxis
    {
        X, Y, Z, RX, RY, RZ,
        Slider0, Slider1, Slider2, Slider3, Slider4, Slider5, Slider6, Slider7,
    }

    public enum HatDirection { Up, Right, Down, Left }

    /// <summary>
    /// One joystick, throttle, pedal set or pad, read from its /dev/input/event node. Shared with other readers
    /// (the game's own SDL joystick included): evdev hands every open file the same events.
    /// Poll on the game thread; never blocks.
    /// </summary>
    public sealed class JoystickDevice : IDisposable
    {
        public const int AxisCount = 14, MaxButtons = 128, MaxHats = 4;

        public string Path { get; }
        public string Name { get; }
        /// <summary>Stable across replugs and reboots: name plus USB serial when the device reports one. Identical devices
        /// without one are told apart by order found (" #2", " #3"), which is not stable.</summary>
        public string Key { get; internal set; }
        public bool Connected => fd >= 0;

        /// <summary>0..65535, centre 32768, as DirectInput reports. Unsupported axes sit at the centre.</summary>
        public readonly int[] Axes = new int[AxisCount];
        public readonly bool[] HasAxis = new bool[AxisCount];
        public readonly bool[] Buttons = new bool[MaxButtons];
        /// <summary>DirectInput POV: hundredths of a degree clockwise from up, -1 when centred.</summary>
        public readonly int[] Hats = { -1, -1, -1, -1 };
        public int ButtonCount { get; private set; }
        public int HatCount { get; private set; }

        private int fd;
        private readonly int[] axisOfCode = new int[Evdev.ABS_CNT];
        private readonly Evdev.AbsInfo[] absInfo = new Evdev.AbsInfo[Evdev.ABS_CNT];
        private readonly int[] buttonOfCode = new int[Evdev.KEY_CNT];
        private readonly int[] hatX = new int[MaxHats], hatY = new int[MaxHats];
        private readonly Evdev.InputEvent[] events = new Evdev.InputEvent[64];
        private bool dropped;

        private JoystickDevice(string path, int fd, string name)
        {
            Path = path;
            this.fd = fd;
            Name = name;
        }

        /// <summary>Opens <paramref name="path"/> if it is a joystick-like device; null if it is not, or cannot be read.</summary>
        public static JoystickDevice TryOpen(string path)
        {
            int fd = Evdev.Open(path, out _);
            if (fd < 0)
                return null;

            byte[] types = Evdev.Bits(fd, 0, 32);
            byte[] abs = Evdev.Bits(fd, Evdev.EV_ABS, Evdev.ABS_CNT);
            byte[] keys = Evdev.Bits(fd, Evdev.EV_KEY, Evdev.KEY_CNT);
            byte[] props = Evdev.Props(fd);
            if (!IsJoystick(types, abs, keys, props))
            {
                Evdev.close(fd);
                return null;
            }

            string name = Evdev.Name(fd);
            var device = new JoystickDevice(path, fd, string.IsNullOrEmpty(name) ? path : name);
            string uniq = Evdev.Uniq(fd);
            device.Key = string.IsNullOrEmpty(uniq) ? device.Name : $"{device.Name} [{uniq}]";
            device.Map(abs, keys);
            device.Resync();
            return device;
        }

        /// <summary>Roughly udev's ID_INPUT_JOYSTICK: joystick or pad buttons, or flight axes, and not a touchpad,
        /// tablet or a pad's motion sensor.</summary>
        private static bool IsJoystick(byte[] types, byte[] abs, byte[] keys, byte[] props)
        {
            if (!Evdev.Has(types, Evdev.EV_ABS))
                return false;
            if (Evdev.Has(props, Evdev.INPUT_PROP_ACCELEROMETER) || Evdev.Has(props, Evdev.INPUT_PROP_POINTER) || Evdev.Has(props, Evdev.INPUT_PROP_DIRECT))
                return false;
            if (Evdev.Has(abs, Evdev.ABS_MT_SLOT) || Evdev.Has(keys, Evdev.BTN_TOUCH))
                return false;

            for (int code = Evdev.BTN_JOYSTICK; code < Evdev.BTN_DIGI; code++)
                if (Evdev.Has(keys, code))
                    return true;
            for (int code = Evdev.BTN_TRIGGER_HAPPY; code < Evdev.BTN_TRIGGER_HAPPY + 40; code++)
                if (Evdev.Has(keys, code))
                    return true;
            for (int code = Evdev.ABS_RX; code <= Evdev.ABS_BRAKE; code++)
                if (Evdev.Has(abs, code))
                    return true;
            return false;
        }

        private void Map(byte[] abs, byte[] keys)
        {
            for (int i = 0; i < axisOfCode.Length; i++)
                axisOfCode[i] = -1;
            for (int i = 0; i < AxisCount; i++)
                Axes[i] = 32768;

            int slider = (int)DeviceAxis.Slider0;
            for (int code = 0; code < Evdev.ABS_CNT; code++)
            {
                if (!Evdev.Has(abs, code) || (code >= Evdev.ABS_HAT0X && code <= Evdev.ABS_HAT3Y) || code >= Evdev.ABS_MT_SLOT)
                    continue;
                int axis = code <= Evdev.ABS_RZ ? code : slider < AxisCount ? slider++ : -1;
                if (axis < 0)
                    continue;
                axisOfCode[code] = axis;
                HasAxis[axis] = true;
            }

            for (int hat = 0; hat < MaxHats; hat++)
                if (Evdev.Has(abs, Evdev.ABS_HAT0X + hat * 2) || Evdev.Has(abs, Evdev.ABS_HAT0X + hat * 2 + 1))
                    HatCount = hat + 1;

            // SDL's order: joystick buttons and everything above them first, then the codes below.
            for (int i = 0; i < buttonOfCode.Length; i++)
                buttonOfCode[i] = -1;
            int button = 0;
            for (int code = Evdev.BTN_JOYSTICK; code < Evdev.KEY_CNT && button < MaxButtons; code++)
                if (Evdev.Has(keys, code))
                    buttonOfCode[code] = button++;
            for (int code = 0; code < Evdev.BTN_JOYSTICK && button < MaxButtons; code++)
                if (Evdev.Has(keys, code))
                    buttonOfCode[code] = button++;
            ButtonCount = button;
        }

        /// <summary>Reads the full current state through ioctls: at open, and after the kernel dropped events.</summary>
        private void Resync()
        {
            for (int code = 0; code < Evdev.ABS_CNT; code++)
            {
                bool hat = code >= Evdev.ABS_HAT0X && code <= Evdev.ABS_HAT3Y;
                if ((axisOfCode[code] < 0 && !hat) || !Evdev.Abs(fd, code, out var info))
                    continue;
                absInfo[code] = info;
                Apply(Evdev.EV_ABS, code, info.Value);
            }

            byte[] held = Evdev.Keys(fd);
            for (int code = 0; code < Evdev.KEY_CNT; code++)
                if (buttonOfCode[code] >= 0)
                    Buttons[buttonOfCode[code]] = Evdev.Has(held, code);
        }

        /// <summary>Drains pending events. False once the device is gone (unplugged); it is closed then.</summary>
        public bool Poll()
        {
            if (fd < 0)
                return false;

            while (true)
            {
                int n = Evdev.Read(fd, events);
                if (n < 0)
                {
                    Dispose();
                    return false;
                }
                if (n == 0)
                    return true;

                for (int i = 0; i < n; i++)
                {
                    ref var e = ref events[i];
                    if (e.Type == Evdev.EV_SYN)
                    {
                        if (e.Code == Evdev.SYN_DROPPED)
                            dropped = true;
                        else if (e.Code == Evdev.SYN_REPORT && dropped)
                        {
                            dropped = false;
                            Resync();
                        }
                    }
                    else if (!dropped)
                        Apply(e.Type, e.Code, e.Value);
                }
            }
        }

        private void Apply(int type, int code, int value)
        {
            if (type == Evdev.EV_KEY)
            {
                if (code < buttonOfCode.Length && buttonOfCode[code] >= 0)
                    Buttons[buttonOfCode[code]] = value != 0;
                return;
            }
            if (type != Evdev.EV_ABS || code >= Evdev.ABS_CNT)
                return;

            if (code >= Evdev.ABS_HAT0X && code <= Evdev.ABS_HAT3Y)
            {
                int hat = (code - Evdev.ABS_HAT0X) / 2;
                if (((code - Evdev.ABS_HAT0X) & 1) == 0)
                    hatX[hat] = Math.Sign(value);
                else
                    hatY[hat] = Math.Sign(value);
                Hats[hat] = Pov(hatX[hat], hatY[hat]);
                return;
            }

            int axis = axisOfCode[code];
            if (axis < 0)
                return;
            var info = absInfo[code];
            long range = (long)info.Maximum - info.Minimum;
            Axes[axis] = range <= 0 ? 32768 : (int)Math.Max(0, Math.Min(65535, ((long)value - info.Minimum) * 65535 / range));
        }

        /// <summary>Hat x (-1 left, 1 right) and y (-1 up, 1 down) as a DirectInput POV angle.</summary>
        private static int Pov(int x, int y)
        {
            if (x == 0 && y == 0)
                return -1;
            double degrees = Math.Atan2(x, -y) * 180 / Math.PI;
            return (int)Math.Round((degrees + 360) % 360 * 100);
        }

        public static bool HatPressed(int pov, HatDirection direction)
        {
            if (pov < 0)
                return false;
            switch (direction)
            {
                case HatDirection.Up: return pov >= 31500 || pov <= 4500;
                case HatDirection.Right: return pov >= 4500 && pov <= 13500;
                case HatDirection.Down: return pov >= 13500 && pov <= 22500;
                case HatDirection.Left: return pov >= 22500 && pov <= 31500;
            }
            return false;
        }

        public override string ToString() =>
            $"'{Name}' ({Path}; axes {string.Join(" ", AxisNames())}; {ButtonCount} buttons; {HatCount} hats)";

        private IEnumerable<string> AxisNames()
        {
            for (int i = 0; i < AxisCount; i++)
                if (HasAxis[i])
                    yield return ((DeviceAxis)i).ToString();
        }

        public void Dispose()
        {
            if (fd >= 0)
                Evdev.close(fd);
            fd = -1;
        }
    }
}
