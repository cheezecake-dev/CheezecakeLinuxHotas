using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace LinuxHotas.Mapping
{
    /// <summary>A device's binds, found again by its <see cref="Devices.JoystickDevice.Key"/>.</summary>
    public sealed class DeviceBinds
    {
        [XmlAttribute] public string Key { get; set; } = "";
        [XmlElement("Bind")] public List<Bind> Binds { get; set; } = new List<Bind>();
    }

    /// <summary>The settings file, Storage/LinuxHotas.cfg in the game's user data folder.</summary>
    [XmlRoot("LinuxHotas")]
    public sealed class Config
    {
        /// <summary>Whether the joysticks fly a grid as soon as the pilot sits down (Toggle Active switches it).</summary>
        public bool ActiveByDefault { get; set; } = true;

        /// <summary>Send axis and button changes to programmable blocks named [AnalogThrottle], in wolfe's format.</summary>
        public bool WolfeScripts { get; set; } = true;

        /// <summary>In multiplayer, send script updates on one tick in this many (wolfe's plugin used 3).</summary>
        public int MultiplayerScriptEveryTicks { get; set; } = 3;

        [XmlArray("Devices"), XmlArrayItem("Device")]
        public List<DeviceBinds> Devices { get; set; } = new List<DeviceBinds>();

        public DeviceBinds For(string key, bool create)
        {
            var binds = Devices.FirstOrDefault(d => d.Key == key);
            if (binds == null && create)
                Devices.Add(binds = new DeviceBinds { Key = key });
            return binds;
        }

        public static Config Load(string path, Action<string> log)
        {
            try
            {
                if (File.Exists(path))
                {
                    using (var reader = File.OpenText(path))
                    {
                        var config = (Config)new XmlSerializer(typeof(Config)).Deserialize(reader);
                        foreach (var device in config.Devices)
                            foreach (var bind in device.Binds.Where(b => !b.IsValid))
                                log($"Ignoring the bind '{bind}' for '{device.Key}': unknown input or output");
                        return config;
                    }
                }
            }
            catch (Exception e)
            {
                string aside = $"{path}.unreadable-{DateTime.Now:yyyyMMdd-HHmmss}";
                log($"Cannot read {path} ({e.Message}); moved it to {aside} and started from defaults");
                try { File.Move(path, aside); } catch { }
            }
            return new Config();
        }

        public void Save(string path, Action<string> log)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                foreach (var device in Devices)
                    device.Binds.RemoveAll(b => string.IsNullOrEmpty(b.Input) || string.IsNullOrEmpty(b.Output));
                Devices.RemoveAll(d => d.Binds.Count == 0);
                string temp = path + ".tmp";
                using (var writer = File.CreateText(temp))
                    new XmlSerializer(typeof(Config)).Serialize(writer, this);
                File.Copy(temp, path, true);
                File.Delete(temp);
            }
            catch (Exception e)
            {
                log($"Cannot save {path}: {e.Message}");
            }
        }
    }
}
