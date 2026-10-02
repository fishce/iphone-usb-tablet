// Linux backend: creates /dev/uinput virtual tablet (absolute ABS_X/ABS_Y + BTN_LEFT)
// so libinput/osu! sees a tablet device. Needs read/write on /dev/uinput
// (run as root, or: sudo usermod -aG input $USER + udev rule — see README).
using System.Runtime.InteropServices;

static class LinuxUinput
{
    const int EV_SYN = 0, EV_KEY = 1, EV_ABS = 3;
    const int SYN_REPORT = 0, BTN_LEFT = 0x110;
    const int ABS_X = 0, ABS_Y = 1;

    static int fd = -1;

    public static void Init()
    {
        fd = libc.open("/dev/uinput", libc.O_WRONLY | libc.O_NONBLOCK, 0);
        if (fd < 0) throw new Exception("cannot open /dev/uinput — needs root or udev rule");
        ioctl(fd, UI_SET_EVBIT, EV_SYN); ioctl(fd, UI_SET_EVBIT, EV_KEY); ioctl(fd, UI_SET_EVBIT, EV_ABS);
        ioctl(fd, UI_SET_KEYBIT, BTN_LEFT);
        ioctl(fd, UI_SET_ABSBIT, ABS_X); ioctl(fd, UI_SET_ABSBIT, ABS_Y);

        byte[] dev = new byte[1112];
        System.Text.Encoding.ASCII.GetBytes("iphone-tab").CopyTo(dev, 0);
        BitConverter.GetBytes((ushort)3).CopyTo(dev, 80);     // bustype BUS_USB
        BitConverter.GetBytes((ushort)0x05ac).CopyTo(dev, 82); // vendor Apple, fun
        BitConverter.GetBytes((ushort)0x0100).CopyTo(dev, 84);
        BitConverter.GetBytes((ushort)1).CopyTo(dev, 86);
        // absmax for ABS_X (idx0), ABS_Y (idx1) at offset 80+8+64*4*2 (after absmax? layout: absmax[64] at 88)
        int off = 88;
        // absmax[0]=ABS_X, absmax[1]=ABS_Y -> offset 88, 92
        BitConverter.GetBytes(65535).CopyTo(dev, off);
        BitConverter.GetBytes(65535).CopyTo(dev, off + 4);
        libc.write(fd, dev, dev.Length);
        ioctl(fd, UI_DEV_CREATE, 0);
        Thread.Sleep(300);
    }

    static void ioctl(int f, int req, int val) { if (libc.ioctl(f, req, val) < 0) throw new Exception($"ioctl {req}"); }

    const int UI_SET_EVBIT = 0x40045564, UI_SET_KEYBIT = 0x40045565, UI_SET_ABSBIT = 0x40045567, UI_DEV_CREATE = 0x5501;

    static void SendEvent(ushort type, ushort code, int value)
    {
        byte[] ev = new byte[24];
        long t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        BitConverter.GetBytes(t).CopyTo(ev, 0);
        BitConverter.GetBytes((ushort)type).CopyTo(ev, 16);
        BitConverter.GetBytes(code).CopyTo(ev, 18);
        BitConverter.GetBytes(value).CopyTo(ev, 20);
        libc.write(fd, ev, ev.Length);
    }

    static void Sync() => SendEvent(EV_SYN, SYN_REPORT, 0);

    public static void Move(int x65535, int y65535) { SendEvent(EV_ABS, ABS_X, x65535); SendEvent(EV_ABS, ABS_Y, y65535); Sync(); }
    public static void Button(bool down) { SendEvent(EV_KEY, BTN_LEFT, down ? 1 : 0); Sync(); }

    static class libc
    {
        public const int O_WRONLY = 1, O_NONBLOCK = 0x800;
        [DllImport("libc")] public static extern int open(string path, int flags, int mode);
        [DllImport("libc")] public static extern int write(int fd, byte[] buf, int count);
        [DllImport("libc")] public static extern int ioctl(int fd, int request, int val);
        [DllImport("libc")] public static extern int close(int fd);
    }
}
