using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LinuxHotas.Devices
{
    /// <summary>
    /// Every joystick-like device under /dev/input, opened as it appears and dropped when it is unplugged.
    /// Game thread only.
    /// </summary>
    public sealed class DeviceManager : IDisposable
    {
        private const string InputDirectory = "/dev/input";
        private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(2), RejectedRetry = TimeSpan.FromSeconds(30);

        private readonly List<JoystickDevice> devices = new List<JoystickDevice>();
        private readonly HashSet<string> rejected = new HashSet<string>();
        private DateTime nextScan = DateTime.MinValue, nextRejectedRetry = DateTime.MinValue;

        public IReadOnlyList<JoystickDevice> Devices => devices;

        /// <summary>Raised when a device is opened or dropped.</summary>
        public event Action<JoystickDevice, bool> Changed;

        public Action<string> Log = _ => { };

        /// <summary>Reads every device's pending events, and every couple of seconds looks for new ones.</summary>
        public void Update()
        {
            for (int i = devices.Count - 1; i >= 0; i--)
            {
                var device = devices[i];
                if (device.Poll())
                    continue;
                devices.RemoveAt(i);
                Log($"Joystick removed: '{device.Name}'");
                Changed?.Invoke(device, false);
            }

            DateTime now = DateTime.UtcNow;
            if (now < nextScan)
                return;
            nextScan = now + ScanInterval;
            if (now >= nextRejectedRetry)
            {
                rejected.Clear();
                nextRejectedRetry = now + RejectedRetry;
            }
            Scan();
        }

        public void Scan()
        {
            string[] paths;
            try
            {
                paths = Directory.GetFiles(InputDirectory, "event*");
            }
            catch (Exception e)
            {
                Log($"Cannot list {InputDirectory}: {e.Message}");
                return;
            }

            foreach (string path in paths.OrderBy(p => p.Length).ThenBy(p => p, StringComparer.Ordinal))
            {
                if (rejected.Contains(path) || devices.Any(d => d.Path == path))
                    continue;
                var device = JoystickDevice.TryOpen(path);
                if (device == null)
                {
                    rejected.Add(path);
                    continue;
                }

                // Two identical devices without serial numbers would share a key; number the later ones.
                string key = device.Key;
                for (int n = 2; devices.Any(d => d.Key == key); n++)
                    key = $"{device.Key} #{n}";
                device.Key = key;

                devices.Add(device);
                Log($"Joystick found: {device}");
                Changed?.Invoke(device, true);
            }
        }

        public void Dispose()
        {
            foreach (var device in devices)
                device.Dispose();
            devices.Clear();
        }
    }
}
