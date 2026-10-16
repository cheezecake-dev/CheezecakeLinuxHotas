using System;
using System.IO;
using HarmonyLib;
using LinuxHotas.Devices;
using LinuxHotas.Flight;
using LinuxHotas.Mapping;
using VRage.FileSystem;
using VRage.Plugins;
using VRage.Utils;

namespace LinuxHotas
{
    /// <summary>
    /// Game plugin entry point: applies the Harmony patches once, then each frame reads every joystick and hands the
    /// result to the seat being flown.
    /// </summary>
    public partial class Plugin : IPlugin
    {
        private const string HarmonyId = "LinuxHotas";

        public static Plugin Instance { get; private set; }
        public static DeviceManager Devices { get; } = new DeviceManager { Log = Log };
        public static Config Config { get; private set; } = new Config();
        public static string ConfigPath => Path.Combine(MyFileSystem.UserDataPath, "Storage", "LinuxHotas.cfg");

        private bool failed;

        public static void Log(string message) => MyLog.Default.WriteLine("LinuxHotas: " + message);

        public void Init(object gameInstance)
        {
            Instance = this;
            Config = Config.Load(ConfigPath, Log);
            ShipControl.Init();
            new Harmony(HarmonyId).PatchAll(typeof(Plugin).Assembly);
            Devices.Scan();
            Log($"loaded; {Devices.Devices.Count} joystick(s), binds for {Config.Devices.Count} device(s)");
        }

        /// <summary>Called by the game once a frame, on the game thread.</summary>
        public void Update()
        {
            if (failed)
                return;
            try
            {
                Devices.Update();
                ShipControl.Input.Update(Devices.Devices, Config);
                ShipControl.Update(Config);
                Scripts.WolfeScripts.Update(Devices.Devices, Config);
                TestAidsUpdate();
            }
            catch (Exception e)
            {
                // One failure a frame would flood the log; stop instead, and say so.
                failed = true;
                Log($"stopped after an error: {e}");
            }
        }

        /// <summary>The test aids' drive file (LinuxHotas.TestAids.xml builds include them); nothing otherwise.</summary>
        partial void TestAidsUpdate();

        /// <summary>Pulsar's plugin list calls this for the plugin's settings.</summary>
        public void OpenConfigDialog() => Sandbox.Graphics.GUI.MyGuiSandbox.AddScreen(new Gui.ConfigScreen());

        public static void SaveConfig() => Config.Save(ConfigPath, Log);

        public void Dispose()
        {
            Devices.Dispose();
        }
    }
}
