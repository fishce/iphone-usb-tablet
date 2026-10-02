// OsuUsbReceiver — receives touch stream over forwarded USB port and injects an absolute mouse.
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

class Program
{
    const int MOUSEEVENTF_MOVE = 0x0001;
    const int MOUSEEVENTF_LEFTDOWN = 0x0002;
    const int MOUSEEVENTF_LEFTUP = 0x0004;
    const int MOUSEEVENTF_ABSOLUTE = 0x8000;

    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int cb);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT
    {
        public int type; public MOUSEINPUT mi; // padded union — fine for mouse only
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr extra;
        }
    }

    static void Mouse(int x, int y, uint flags, int screenW, int screenH)
    {
        INPUT[] input = new INPUT[1];
        input[0].type = 0;
        input[0].mi = new INPUT.MOUSEINPUT
        {
            dx = x * 65535 / screenW,
            dy = y * 65535 / screenH,
            dwFlags = flags | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE
        };
        SendInput(1, input, Marshal.SizeOf<INPUT>());
    }

    static async Task Main(string[] args)
    {
        int port = 5050;
        int iw = 393, ih = 852; // iPhone logical touch area (overridden by header)
        bool down = false;
        int W = GetSystemMetrics(0), H = GetSystemMetrics(1);
        int lastSX = W / 2, lastSY = H / 2;

        Console.WriteLine($"OsuUsbReceiver — connecting to 127.0.0.1:{port} (iproxy must be running)…");
        TcpClient? client = null;
        while (client == null)
        {
            try { client = new TcpClient(); await client.ConnectAsync("127.0.0.1", port); }
            catch { Console.WriteLine("…waiting for iproxy/otouchd — start both, then this retries"); await Task.Delay(2000); }
        }
        Console.WriteLine("connected. stream:");

        var reader = new StreamReader(client.GetStream(), Encoding.ASCII);
        string? line;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            var p = line.Split(' ');
            switch (p[0])
            {
                case "W": // header: device logical size
                    iw = int.Parse(p[1]); ih = int.Parse(p[2]);
                    Console.WriteLine($"device size: {iw}x{ih}");
                    break;
                case "D":
                    {
                        double x = double.Parse(p[1]);
                        double y = double.Parse(p[2]);
                        int sx = (int)(x / iw * W);
                        int sy = (int)(y / ih * H);
                        if (!down) { Mouse(sx, sy, MOUSEEVENTF_LEFTDOWN, W, H); down = true; }
                        Mouse(sx, sy, 0, W, H);
                        lastSX = sx; lastSY = sy;
                        break;
                    }
                case "U":
                    if (down) { Mouse(lastSX, lastSY, MOUSEEVENTF_LEFTUP, W, H); down = false; }
                    break;
            }
        }
        Console.WriteLine("disconnected.");
    }
}
