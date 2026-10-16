using System;
using System.Xml.Serialization;
using LinuxHotas.Devices;

namespace LinuxHotas.Mapping
{
    /// <summary>
    /// One input on one device to one output: an axis to a <see cref="GameAxis"/>, or a button or hat direction to a
    /// <see cref="GameAction"/>. Stored as text so the config file reads and edits by hand:
    /// <c>&lt;Bind Input="Y" Output="Pitch" /&gt;</c>, <c>&lt;Bind Input="Button1" Output="FirePrimary" /&gt;</c>,
    /// <c>&lt;Bind Input="Hat1Up" Output="NextToolbarSlot" /&gt;</c>. Buttons and hats count from 1, as the game shows them.
    /// </summary>
    public sealed class Bind
    {
        public const float DefaultDeadzone = 0.05f;

        [XmlAttribute] public string Input { get; set; } = "";
        [XmlAttribute] public string Output { get; set; } = "";
        [XmlAttribute] public bool Invert { get; set; }
        [XmlAttribute] public float Deadzone { get; set; } = DefaultDeadzone;
        /// <summary>0 is linear, 1 is x³: a·x³ + (1 − a)·x, applied to the distance from rest.</summary>
        [XmlAttribute] public float Curve { get; set; }

        public bool ShouldSerializeInvert() => Invert;
        public bool ShouldSerializeDeadzone() => InputAxis.HasValue && Deadzone != DefaultDeadzone;
        public bool ShouldSerializeCurve() => InputAxis.HasValue && Curve != 0;

        [XmlIgnore] public DeviceAxis? InputAxis => ParseAxis(Input);
        [XmlIgnore] public int? InputButton => ParseButton(Input);
        [XmlIgnore] public (int Hat, HatDirection Direction)? InputHat => ParseHat(Input);
        [XmlIgnore] public GameAxis? OutputAxis => Enum.TryParse(Output, out GameAxis a) ? a : (GameAxis?)null;
        [XmlIgnore] public GameAction? OutputAction => Enum.TryParse(Output, out GameAction a) ? a : (GameAction?)null;

        /// <summary>An axis feeds a game axis, or crosses three quarters of its travel to press an action.</summary>
        [XmlIgnore] public bool IsValid => (InputAxis.HasValue || InputButton.HasValue || InputHat.HasValue) && (OutputAxis.HasValue || OutputAction.HasValue);

        public static string AxisInput(DeviceAxis axis) => axis.ToString();
        public static string ButtonInput(int index) => $"Button{index + 1}";
        public static string HatInput(int hat, HatDirection direction) => $"Hat{hat + 1}{direction}";

        // Enum.TryParse also takes numbers ("-1", "+20", " 20"), which would index past the device's axes.
        private static DeviceAxis? ParseAxis(string input) =>
            !string.IsNullOrEmpty(input) && !char.IsDigit(input[0]) && Enum.TryParse(input, out DeviceAxis axis) && Enum.IsDefined(typeof(DeviceAxis), axis) ? axis : (DeviceAxis?)null;

        private static int? ParseButton(string input) =>
            input != null && input.StartsWith("Button") && int.TryParse(input.Substring(6), out int n) && n >= 1 && n <= JoystickDevice.MaxButtons ? n - 1 : (int?)null;

        private static (int, HatDirection)? ParseHat(string input)
        {
            if (input == null || !input.StartsWith("Hat") || input.Length < 5 || !char.IsDigit(input[3]))
                return null;
            int hat = input[3] - '1';
            return hat >= 0 && hat < JoystickDevice.MaxHats && Enum.TryParse(input.Substring(4), out HatDirection direction) ? (hat, direction) : ((int, HatDirection)?)null;
        }

        /// <summary>The input's value on <paramref name="device"/>: -1..1 for a centred output, 0..1 otherwise (buttons 0 or 1).</summary>
        public float Read(JoystickDevice device)
        {
            if (InputButton is int button)
                return device.Buttons[button] ? 1 : 0;
            if (InputHat is (int hat, HatDirection direction))
                return JoystickDevice.HatPressed(device.Hats[hat], direction) ? 1 : 0;
            if (!(InputAxis is DeviceAxis axis))
                return 0;

            float raw = device.Axes[(int)axis] / 65535f;
            if (Invert)
                raw = 1 - raw;
            bool centred = OutputAxis is GameAxis output && Controls.IsCentred(output);
            return centred ? Shape(raw * 2 - 1) : Lever(raw);
        }

        /// <summary>-1..1 with the deadzone cut out of the middle, then the curve.</summary>
        private float Shape(float value)
        {
            float size = Math.Abs(value);
            float dz = Math.Max(0, Math.Min(0.99f, Deadzone));
            size = size <= dz ? 0 : Math.Min(1, (size - dz) / (1 - dz));
            return Math.Sign(value) * CurveOf(size);
        }

        /// <summary>0..1 with half the deadzone cut from each end, so a lever reads exactly 0 and 1 at its stops.</summary>
        private float Lever(float value)
        {
            float dz = Math.Max(0, Math.Min(0.99f, Deadzone)) / 2;
            value = value <= dz ? 0 : value >= 1 - dz ? 1 : (value - dz) / (1 - 2 * dz);
            return CurveOf(value);
        }

        private float CurveOf(float size)
        {
            float a = Math.Max(0, Math.Min(1, Curve));
            return a * size * size * size + (1 - a) * size;
        }

        public Bind Clone() => (Bind)MemberwiseClone();

        public override string ToString() => $"{Input} => {Output}";
    }
}
