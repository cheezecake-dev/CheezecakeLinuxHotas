using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LinuxHotas.Devices
{
    /// <summary>
    /// The kernel's evdev interface (linux/input.h): libc open/read/ioctl on /dev/input/event*.
    /// No SDL, so nothing here touches linux-compat's SDL thread.
    /// </summary>
    internal static unsafe class Evdev
    {
        private const string Libc = "libc.so.6";

        public const int O_RDONLY = 0, O_NONBLOCK = 0x800, O_CLOEXEC = 0x80000;
        public const int EAGAIN = 11, ENODEV = 19, EINTR = 4;

        public const int EV_SYN = 0, EV_KEY = 1, EV_ABS = 3;
        public const int SYN_REPORT = 0, SYN_DROPPED = 3;

        public const int ABS_X = 0x00, ABS_Y = 0x01, ABS_Z = 0x02, ABS_RX = 0x03, ABS_RY = 0x04, ABS_RZ = 0x05;
        public const int ABS_THROTTLE = 0x06, ABS_RUDDER = 0x07, ABS_WHEEL = 0x08, ABS_GAS = 0x09, ABS_BRAKE = 0x0a;
        public const int ABS_HAT0X = 0x10, ABS_HAT3Y = 0x17;
        public const int ABS_MT_SLOT = 0x2f, ABS_CNT = 0x40;

        public const int BTN_MISC = 0x100, BTN_JOYSTICK = 0x120, BTN_GAMEPAD = 0x130, BTN_DIGI = 0x140, BTN_TOUCH = 0x14a;
        public const int BTN_TRIGGER_HAPPY = 0x2c0, KEY_CNT = 0x300;

        /// <summary>struct input_event on 64-bit Linux: a timeval, then type, code and value.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct InputEvent
        {
            public long Seconds, Microseconds;
            public ushort Type, Code;
            public int Value;
        }

        /// <summary>struct input_absinfo.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct AbsInfo
        {
            public int Value, Minimum, Maximum, Fuzz, Flat, Resolution;
        }

        /// <summary>struct input_id.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct InputId
        {
            public ushort BusType, Vendor, Product, Version;
        }

        [DllImport(Libc, SetLastError = true)]
        private static extern int open(string path, int flags);

        [DllImport(Libc, SetLastError = true)]
        public static extern int close(int fd);

        [DllImport(Libc, SetLastError = true)]
        private static extern IntPtr read(int fd, void* buffer, UIntPtr count);

        [DllImport(Libc, SetLastError = true)]
        private static extern int ioctl(int fd, ulong request, void* arg);

        // _IOC(dir, 'E', nr, size) with dir 2 = read.
        private static ulong Ior(int nr, int size) => (2UL << 30) | ((ulong)(uint)size << 16) | ((ulong)'E' << 8) | (uint)nr;

        public static int Open(string path, out int errno)
        {
            int fd = open(path, O_RDONLY | O_NONBLOCK | O_CLOEXEC);
            errno = fd < 0 ? Marshal.GetLastWin32Error() : 0;
            return fd;
        }

        /// <summary>Reads up to <paramref name="events"/>.Length events without blocking. Returns the count, 0 when
        /// there is nothing, and -errno on an error (-ENODEV once the device is unplugged).</summary>
        public static int Read(int fd, InputEvent[] events)
        {
            fixed (InputEvent* p = events)
            {
                long n = (long)read(fd, p, (UIntPtr)(events.Length * sizeof(InputEvent)));
                if (n < 0)
                {
                    int errno = Marshal.GetLastWin32Error();
                    return errno == EAGAIN || errno == EINTR ? 0 : -errno;
                }
                return (int)(n / sizeof(InputEvent));
            }
        }

        public static string Name(int fd) => String(fd, 0x06);
        public static string Phys(int fd) => String(fd, 0x07);
        public static string Uniq(int fd) => String(fd, 0x08);

        private static string String(int fd, int nr)
        {
            byte* buffer = stackalloc byte[256];
            int n = ioctl(fd, Ior(nr, 256), buffer);
            if (n <= 0)
                return "";
            int length = 0;
            while (length < n && buffer[length] != 0)
                length++;
            return Encoding.UTF8.GetString(buffer, length);
        }

        public static InputId Id(int fd)
        {
            InputId id;
            ioctl(fd, Ior(0x02, sizeof(InputId)), &id);
            return id;
        }

        /// <summary>EVIOCGBIT: which codes of event type <paramref name="type"/> the device has (type 0: which types).</summary>
        public static byte[] Bits(int fd, int type, int count)
        {
            var bits = new byte[(count + 7) / 8];
            fixed (byte* p = bits)
                ioctl(fd, Ior(0x20 + type, bits.Length), p);
            return bits;
        }

        public const int INPUT_PROP_POINTER = 0, INPUT_PROP_DIRECT = 1, INPUT_PROP_ACCELEROMETER = 6, INPUT_PROP_CNT = 0x20;

        /// <summary>EVIOCGPROP: the device's INPUT_PROP_* bits.</summary>
        public static byte[] Props(int fd)
        {
            var bits = new byte[INPUT_PROP_CNT / 8];
            fixed (byte* p = bits)
                ioctl(fd, Ior(0x09, bits.Length), p);
            return bits;
        }

        /// <summary>EVIOCGKEY: which keys and buttons are held right now.</summary>
        public static byte[] Keys(int fd)
        {
            var bits = new byte[KEY_CNT / 8];
            fixed (byte* p = bits)
                ioctl(fd, Ior(0x18, bits.Length), p);
            return bits;
        }

        public static bool Abs(int fd, int code, out AbsInfo info)
        {
            AbsInfo result;
            bool ok = ioctl(fd, Ior(0x40 + code, sizeof(AbsInfo)), &result) >= 0;
            info = result;
            return ok;
        }

        public static bool Has(byte[] bits, int code) => code / 8 < bits.Length && (bits[code / 8] & (1 << (code % 8))) != 0;
    }
}
