// OsuTetherReceiver — receives touch over USB tethering (RNDIS) and injects an absolute mouse.
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

class Program
{
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inst, int cb);

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public int type; public MOUSEINPUT mi; public int pad0, pad1; }
    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint data, flags, time; public IntPtr extra; }

    const uint LEFTDOWN = 0x0002, LEFTUP = 0x0004, ABSOLUTE = 0x8000, MOVE = 0x0001;

    static void Mouse(int x, int y, uint flags, int W, int H)
    {
        if (OperatingSystem.IsWindows())
            SendInput(1, new[] { new INPUT { type = 0, mi = new MOUSEINPUT { dx = x * 65535 / W, dy = y * 65535 / H, flags = flags | ABSOLUTE | MOVE } } }, Marshal.SizeOf<INPUT>());
        else
        {
            LinuxUinput.Move(x * 65535 / W, y * 65535 / H);
            if ((flags & LEFTDOWN) != 0) LinuxUinput.Button(true);
            if ((flags & LEFTUP) != 0) LinuxUinput.Button(false);
        }
    }

    static string? FindRndisIp()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            string name = nic.Name.ToLowerInvariant() + nic.Description.ToLowerInvariant();
            if (name.Contains("remote ndis") || name.Contains("apple mobile device") || name.Contains("ethernet adapter") && name.Contains("apple"))
            {
                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        return addr.Address.ToString();
            }
        }
        return null;
    }

    static async Task Main()
    {
        int W, H;
        if (OperatingSystem.IsWindows()) { W = GetSystemMetrics(0); H = GetSystemMetrics(1); }
        else
        {
            W = int.TryParse(Environment.GetEnvironmentVariable("TAB_W"), out var vw) ? vw : 1920;
            H = int.TryParse(Environment.GetEnvironmentVariable("TAB_H"), out var vh) ? vh : 1080;
            Console.WriteLine($"TAB_W/TAB_H unset — assuming {W}x{H} (set env vars for your real display size)");
            LinuxUinput.Init();
        }
        var ip = OperatingSystem.IsWindows() ? FindRndisIp() : System.String.Join(",", System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()).Where(a => a.AddressFamily == AddressFamily.InterNetwork));
        Console.WriteLine($"local IPs: {ip}  (type the RNDIS/ipheth one into the iPhone app)");
        var pc = new UdpClient(4242);

        int iw = 393, ih = 852; bool down = false; int lx = W / 2, ly = H / 2;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        DateTime last = DateTime.MinValue; int frames = 0;

        // watchdog: release click if stream stalls (UDP may drop the UP packet)
        var wd = new Thread(() =>
        {
            while (true)
            {
                Thread.Sleep(150);
                if (down && (DateTime.UtcNow - last).TotalMilliseconds > 300)
                { Mouse(lx, ly, LEFTUP, W, H); down = false; }
            }
        }) { IsBackground = true };
        wd.Start();

        Console.WriteLine("listening on UDP 4242…");
        while (true)
        {
            var r = await pc.ReceiveAsync();
            last = DateTime.UtcNow;
            var line = Encoding.ASCII.GetString(r.Buffer).Trim();
            var p = line.Split(' ');
            switch (p[0])
            {
                case "W": iw = int.Parse(p[1]); ih = int.Parse(p[2]); Console.WriteLine($"device {iw}x{ih}"); break;
                case "D":
                    {
                        int sx = (int)(double.Parse(p[1]) / iw * W);
                        int sy = (int)(double.Parse(p[2]) / ih * H);
                        if (!down) { Mouse(sx, sy, LEFTDOWN, W, H); down = true; }
                        Mouse(sx, sy, 0, W, H);
                        lx = sx; ly = sy; frames++;
                        if (sw.ElapsedMilliseconds >= 1000) { Console.WriteLine($"{frames} pkt/s"); frames = 0; sw.Restart(); }
                        break;
                    }
                case "U": if (down) { Mouse(lx, ly, LEFTUP, W, H); down = false; } break;
            }
        }
    }
}
