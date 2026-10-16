using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LinuxHotas.Devices;
using LinuxHotas.Mapping;
using Sandbox;
using Sandbox.Graphics.GUI;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace LinuxHotas.Gui
{
    /// <summary>
    /// The plugin's settings, opened from Pulsar's plugin list: per device, a table of binds with their live values, an
    /// editor for the selected one (Detect picks the input by moving it), and the two switches. Saved on close. Written
    /// with ananace's settings, device and bind dialogs as the reference, folded into one screen.
    /// </summary>
    internal sealed class ConfigScreen : MyGuiScreenBase
    {
        private const float Left = -0.43f, Width = 0.86f;
        private const long ButtonKeys = 1000, HatKeys = 2000, ActionKeys = 100;
        private static readonly TimeSpan DetectTime = TimeSpan.FromSeconds(10);

        private MyGuiControlCombobox deviceChoice, inputChoice, outputChoice;
        private MyGuiControlTable bindTable;
        private MyGuiControlCheckbox invertBox;
        private MyGuiControlSlider deadzoneSlider, curveSlider;
        private MyGuiControlLabel deviceLabel, statusLabel;

        private readonly List<string> deviceKeys = new List<string>();
        private readonly List<MyGuiControlTable.Cell> liveCells = new List<MyGuiControlTable.Cell>();
        private string deviceKey;
        private Bind selected;
        private bool loading;

        private DateTime detectUntil;
        private Dictionary<JoystickDevice, (int[] Axes, bool[] Buttons, int[] Hats)> detectFrom;

        public ConfigScreen()
            : base(new Vector2(0.5f, 0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR, new Vector2(0.92f, 0.9f),
                backgroundTransition: MySandboxGame.Config.UIBkOpacity, guiTransition: MySandboxGame.Config.UIOpacity)
        {
            EnabledBackgroundFade = true;
            m_closeOnEsc = true;
            CloseButtonEnabled = true;
            CanHideOthers = true;
            Closed += (_, __) => Plugin.SaveConfig();
        }

        public override string GetFriendlyName() => "LinuxHotasConfigScreen";

        public override void LoadContent()
        {
            base.LoadContent();
            RecreateControls(true);
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            AddCaption("Linux HOTAS");
            var config = Plugin.Config;

            float y = -0.36f;
            Controls.Add(new MyGuiControlLabel(new Vector2(Left, y), text: "Device"));
            deviceChoice = new MyGuiControlCombobox(new Vector2(Left + 0.1f, y), new Vector2(0.45f, 0.04f), originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
            deviceChoice.ItemSelected += () => SelectDevice(deviceKeys[(int)deviceChoice.GetSelectedKey()]);
            Controls.Add(deviceChoice);
            deviceLabel = new MyGuiControlLabel(new Vector2(Left + 0.57f, y), text: "", textScale: 0.7f);
            Controls.Add(deviceLabel);

            y += 0.055f;
            AddCheckbox(new Vector2(Left, y), "Fly as soon as seated", config.ActiveByDefault, v => config.ActiveByDefault = v,
                "Off: the joysticks wait for the Toggle Active action each time you sit down.");
            AddCheckbox(new Vector2(Left + 0.43f, y), "Send to [AnalogThrottle] scripts", config.WolfeScripts, v => config.WolfeScripts = v,
                "Axis and button changes go to programmable blocks named [AnalogThrottle] on your grid, in the format of wolfe's AnalogThrottle plugin.");

            y += 0.045f;
            bindTable = new MyGuiControlTable
            {
                Position = new Vector2(Left, y),
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                Size = new Vector2(Width, 0.3f),
                ColumnsCount = 4,
                VisibleRowsCount = 8,
            };
            bindTable.SetCustomColumnWidths(new[] { 0.2f, 0.35f, 0.3f, 0.15f });
            bindTable.SetColumnName(0, new StringBuilder("Input"));
            bindTable.SetColumnName(1, new StringBuilder("Does"));
            bindTable.SetColumnName(2, new StringBuilder("Options"));
            bindTable.SetColumnName(3, new StringBuilder("Now"));
            bindTable.ItemSelected += (_, __) => SelectBind(bindTable.SelectedRow?.UserData as Bind);
            Controls.Add(bindTable);

            y = 0.13f;
            Controls.Add(new MyGuiControlLabel(new Vector2(Left, y), text: "Input"));
            inputChoice = new MyGuiControlCombobox(new Vector2(Left + 0.1f, y), new Vector2(0.18f, 0.04f), originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
            inputChoice.ItemSelected += () => Edit(b => b.Input = InputOf(inputChoice.GetSelectedKey()));
            Controls.Add(inputChoice);
            Controls.Add(Button(new Vector2(Left + 0.29f, y), "Detect", StartDetect, "Then move an axis, press a button or push a hat on any device.", MyGuiControlButtonStyleEnum.Small));
            Controls.Add(new MyGuiControlLabel(new Vector2(Left + 0.45f, y), text: "Does"));
            outputChoice = new MyGuiControlCombobox(new Vector2(Left + 0.53f, y), new Vector2(0.33f, 0.04f), openAreaItemsCount: 12, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
            foreach (GameAxis axis in Enum.GetValues(typeof(GameAxis)))
                outputChoice.AddItem((long)axis, Words(axis.ToString()) + (Mapping.Controls.IsCentred(axis) ? " (stick)" : " (lever)"), sort: false);
            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
                outputChoice.AddItem(ActionKeys + (long)action, Words(action.ToString()), sort: false);
            outputChoice.ItemSelected += () => Edit(b => b.Output = OutputOf(outputChoice.GetSelectedKey()));
            Controls.Add(outputChoice);

            y += 0.06f;
            invertBox = AddCheckbox(new Vector2(Left, y), "Invert", false, v => Edit(b => b.Invert = v), "Reverse the axis.");
            Controls.Add(new MyGuiControlLabel(new Vector2(Left + 0.17f, y), text: "Deadzone"));
            deadzoneSlider = new MyGuiControlSlider(new Vector2(Left + 0.28f, y), 0, 50, 0.2f, Bind.DefaultDeadzone * 100, labelText: "{0}%", labelDecimalPlaces: 0, labelSpaceWidth: 0.05f, showLabel: true,
                originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, toolTip: "Sticks: ignored around the centre. Levers: half at each end, so the stops read exactly 0 and full.");
            deadzoneSlider.ValueChanged += s => Edit(b => b.Deadzone = s.Value / 100);
            Controls.Add(deadzoneSlider);
            Controls.Add(new MyGuiControlLabel(new Vector2(Left + 0.56f, y), text: "Curve"));
            curveSlider = new MyGuiControlSlider(new Vector2(Left + 0.64f, y), 0, 1, 0.16f, 0, labelText: "{0}", labelDecimalPlaces: 2, labelSpaceWidth: 0.05f, showLabel: true,
                originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, toolTip: "0 is linear; 1 is gentle near rest and quick at full travel.");
            curveSlider.ValueChanged += s => Edit(b => b.Curve = s.Value);
            Controls.Add(curveSlider);

            y += 0.08f;
            Controls.Add(Button(new Vector2(Left + 0.1f, y), "Add bind", AddBind, "A new bind on this device."));
            Controls.Add(Button(new Vector2(Left + 0.35f, y), "Remove bind", RemoveBind, "Remove the selected bind."));
            Controls.Add(Button(new Vector2(Left + 0.76f, y), "Close", () => CloseScreen(), "Save and close."));

            statusLabel = new MyGuiControlLabel(new Vector2(Left, y + 0.06f), text: "", textScale: 0.7f);
            Controls.Add(statusLabel);

            FillDevices();
        }

        private MyGuiControlCheckbox AddCheckbox(Vector2 at, string text, bool value, Action<bool> changed, string tip)
        {
            var box = new MyGuiControlCheckbox(at, toolTip: tip, isChecked: value, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
            box.IsCheckedChanged += b => changed(b.IsChecked);
            Controls.Add(box);
            Controls.Add(new MyGuiControlLabel(at + new Vector2(0.035f, 0), text: text));
            return box;
        }

        private static MyGuiControlButton Button(Vector2 at, string text, Action clicked, string tip, MyGuiControlButtonStyleEnum style = MyGuiControlButtonStyleEnum.Default) =>
            new MyGuiControlButton(at, style, text: new StringBuilder(text), toolTip: tip, onButtonClick: _ => clicked());

        private void FillDevices()
        {
            deviceKeys.Clear();
            deviceKeys.AddRange(Plugin.Devices.Devices.Select(d => d.Key));
            deviceKeys.AddRange(Plugin.Config.Devices.Select(d => d.Key).Where(k => !deviceKeys.Contains(k)));
            deviceChoice.ClearItems();
            for (int i = 0; i < deviceKeys.Count; i++)
                deviceChoice.AddItem(i, deviceKeys[i] + (Device(deviceKeys[i]) == null ? " (not connected)" : ""), sort: false);

            if (deviceKeys.Count == 0)
            {
                deviceKey = null;
                deviceLabel.Text = "No joysticks found";
                FillBinds();
                return;
            }
            int index = Math.Max(0, deviceKeys.IndexOf(deviceKey));
            deviceChoice.SelectItemByKey(index, false);
            SelectDevice(deviceKeys[index]);
        }

        private static JoystickDevice Device(string key) => Plugin.Devices.Devices.FirstOrDefault(d => d.Key == key);

        private void SelectDevice(string key)
        {
            deviceKey = key;
            var device = Device(key);
            deviceLabel.Text = device == null ? "not connected" : $"{device.HasAxis.Count(h => h)} axes, {device.ButtonCount} buttons, {device.HatCount} hats";

            inputChoice.ClearItems();
            for (int axis = 0; axis < JoystickDevice.AxisCount; axis++)
                if (device == null ? axis < 8 : device.HasAxis[axis])
                    inputChoice.AddItem(axis, ((DeviceAxis)axis).ToString(), sort: false);
            for (int button = 0; button < (device?.ButtonCount ?? 32); button++)
                inputChoice.AddItem(ButtonKeys + button, $"Button {button + 1}", sort: false);
            for (int hat = 0; hat < (device?.HatCount ?? 1); hat++)
                foreach (HatDirection direction in Enum.GetValues(typeof(HatDirection)))
                    inputChoice.AddItem(HatKeys + hat * 4 + (int)direction, $"Hat {hat + 1} {direction}", sort: false);

            FillBinds();
        }

        private void FillBinds(Bind select = null)
        {
            bindTable.Clear();
            liveCells.Clear();
            var binds = deviceKey == null ? null : Plugin.Config.For(deviceKey, false);
            foreach (var bind in binds?.Binds ?? new List<Bind>())
            {
                var row = new MyGuiControlTable.Row(bind);
                row.AddCell(new MyGuiControlTable.Cell(InputText(bind)));
                row.AddCell(new MyGuiControlTable.Cell(OutputText(bind), textColor: bind.IsValid ? (Color?)null : Color.OrangeRed));
                row.AddCell(new MyGuiControlTable.Cell(OptionsText(bind)));
                // The table copies a cell's text when it is made, so the live value is written to the cell itself.
                var live = new MyGuiControlTable.Cell(new StringBuilder());
                liveCells.Add(live);
                row.AddCell(live);
                bindTable.Add(row);
                if (bind == select)
                    bindTable.SelectedRowIndex = bindTable.RowsCount - 1;
            }
            SelectBind(select);
        }

        private void SelectBind(Bind bind)
        {
            selected = bind;
            loading = true;
            try
            {
                if (bind == null)
                    return;
                long input = KeyOf(bind);
                if (input >= 0)
                    inputChoice.SelectItemByKey(input, false);
                if (bind.OutputAxis is GameAxis axis)
                    outputChoice.SelectItemByKey((long)axis, false);
                else if (bind.OutputAction is GameAction action)
                    outputChoice.SelectItemByKey(ActionKeys + (long)action, false);
                invertBox.IsChecked = bind.Invert;
                deadzoneSlider.Value = bind.Deadzone * 100;
                curveSlider.Value = bind.Curve;
            }
            finally
            {
                loading = false;
            }
        }

        /// <summary>Applies an editor change to the selected bind and redraws the table.</summary>
        private void Edit(Action<Bind> change)
        {
            if (loading || selected == null)
                return;
            change(selected);
            FillBinds(selected);
        }

        private void AddBind()
        {
            if (deviceKey == null)
            {
                statusLabel.Text = "Connect a joystick first.";
                return;
            }
            var device = Device(deviceKey);
            int axis = device == null ? 0 : Array.IndexOf(device.HasAxis, true);
            var bind = axis >= 0
                ? new Bind { Input = Bind.AxisInput((DeviceAxis)axis), Output = nameof(GameAxis.Pitch) }
                : new Bind { Input = Bind.ButtonInput(0), Output = nameof(GameAction.FirePrimary) };
            Plugin.Config.For(deviceKey, true).Binds.Add(bind);
            FillBinds(bind);
            statusLabel.Text = "Added; choose its input (or Detect) and what it does.";
        }

        private void RemoveBind()
        {
            if (selected == null || deviceKey == null)
                return;
            Plugin.Config.For(deviceKey, false)?.Binds.Remove(selected);
            FillBinds();
        }

        private void StartDetect()
        {
            detectFrom = Plugin.Devices.Devices.ToDictionary(d => d, d => ((int[])d.Axes.Clone(), (bool[])d.Buttons.Clone(), (int[])d.Hats.Clone()));
            detectUntil = DateTime.UtcNow + DetectTime;
            statusLabel.Text = "Move an axis, press a button or push a hat...";
        }

        public override bool Update(bool hasFocus)
        {
            if (detectFrom != null)
                Detect();

            var device = deviceKey == null ? null : Device(deviceKey);
            var binds = deviceKey == null ? null : Plugin.Config.For(deviceKey, false);
            for (int i = 0; i < liveCells.Count && binds != null && i < binds.Binds.Count; i++)
            {
                liveCells[i].Text.Clear();
                if (device != null && binds.Binds[i].IsValid)
                    liveCells[i].Text.Append(binds.Binds[i].Read(device).ToString("0.00"));
            }
            return base.Update(hasFocus);
        }

        private void Detect()
        {
            if (DateTime.UtcNow > detectUntil)
            {
                detectFrom = null;
                statusLabel.Text = "Nothing moved.";
                return;
            }
            foreach (var pair in detectFrom)
            {
                var device = pair.Key;
                string input = null;
                for (int b = 0; b < device.ButtonCount && input == null; b++)
                    if (device.Buttons[b] && !pair.Value.Buttons[b])
                        input = Bind.ButtonInput(b);
                for (int h = 0; h < device.HatCount && input == null; h++)
                    if (device.Hats[h] >= 0 && device.Hats[h] != pair.Value.Hats[h])
                        foreach (HatDirection direction in Enum.GetValues(typeof(HatDirection)))
                            if (input == null && JoystickDevice.HatPressed(device.Hats[h], direction))
                                input = Bind.HatInput(h, direction);
                for (int a = 0; a < JoystickDevice.AxisCount && input == null; a++)
                    if (device.HasAxis[a] && Math.Abs(device.Axes[a] - pair.Value.Axes[a]) > 20000)
                        input = Bind.AxisInput((DeviceAxis)a);
                if (input == null)
                    continue;

                detectFrom = null;
                if (device.Key != deviceKey)
                {
                    deviceKey = device.Key;
                    FillDevices();
                }
                if (selected == null)
                    AddBind();
                Edit(b => b.Input = input);
                statusLabel.Text = $"Detected {InputText(selected)} on {device.Name}.";
                return;
            }
        }

        private static long KeyOf(Bind bind)
        {
            if (bind.InputAxis is DeviceAxis axis)
                return (long)axis;
            if (bind.InputButton is int button)
                return ButtonKeys + button;
            if (bind.InputHat is (int hat, HatDirection direction))
                return HatKeys + hat * 4 + (int)direction;
            return -1;
        }

        private static string InputOf(long key)
        {
            if (key >= HatKeys)
                return Bind.HatInput((int)(key - HatKeys) / 4, (HatDirection)((key - HatKeys) % 4));
            if (key >= ButtonKeys)
                return Bind.ButtonInput((int)(key - ButtonKeys));
            return Bind.AxisInput((DeviceAxis)key);
        }

        private static string OutputOf(long key) =>
            key >= ActionKeys ? ((GameAction)(key - ActionKeys)).ToString() : ((GameAxis)key).ToString();

        private static string InputText(Bind bind)
        {
            if (bind.InputButton is int button)
                return $"Button {button + 1}";
            if (bind.InputHat is (int hat, HatDirection direction))
                return $"Hat {hat + 1} {direction}";
            return bind.Input;
        }

        private static string OutputText(Bind bind) => Words(bind.Output);

        private static string OptionsText(Bind bind)
        {
            var parts = new List<string>();
            if (bind.Invert)
                parts.Add("inverted");
            if (bind.InputAxis.HasValue)
            {
                parts.Add($"deadzone {bind.Deadzone * 100:0}%");
                if (bind.Curve != 0)
                    parts.Add($"curve {bind.Curve:0.00}");
            }
            return string.Join(", ", parts);
        }

        /// <summary>"ToolbarSlot1" reads "Toolbar Slot 1".</summary>
        private static string Words(string name)
        {
            var text = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && (char.IsUpper(c) || (char.IsDigit(c) && !char.IsDigit(name[i - 1]))))
                    text.Append(' ');
                text.Append(c);
            }
            return text.ToString();
        }
    }
}
