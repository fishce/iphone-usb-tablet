# iPhone USB Touch Tablet for osu! (jailbreak route, raw USB only)

**No Wi-Fi, no hotspot, no Ethernet adapter** — the iPhone talks to a root daemon
(`otouchd`) that streams binary touch lines over a raw USB channel. Windows gets
them through `iproxy` (libimobiledevice over USB) and injects an absolute mouse.

```
iPhone touch screen  →  IOHIDEventSystem (otouchd, port 5050)
      │ raw USB (usbmuxd)
      ▼
iproxy.exe (Windows)  →  127.0.0.1:5050
      ▼
OsuUsbReceiver  →  SendInput (absolute mouse, left button while touching)
      ▼
osu!
```

## 0. Prerequisites
- Jailbroken iPhone (palera1n / Dopamine) + OpenSSH or Terminal app
- iTunes or Apple Devices app installed on Windows (pairing + usbmuxd)
- `iproxy.exe` — from libimobiledevice-win32 / MSYS2:
  `pacman -S mingw-w64-ucrt-x86_64-libimobiledevice-utils` → use its `iproxy.exe`
- .NET 10 SDK on Windows

## 1. Build & run `otouchd` on the iPhone
```sh
# on the iPhone (Terminal or SSH in as root)
clang otouchd.m -framework Foundation -framework IOKit -o /var/jb/usr/local/bin/otouchd
chmod +x /var/jb/usr/local/bin/otouchd
otouchd --debug        # first run: check it prints raw touch fields
```
- First, run `--debug`: touch the screen, see `TOUCH raw: x=… y=…` values.
  If they're all 0, the `kIOHIDEventFieldTouchAbsoluteX/Y` constants in
  `otouchd.m` don't match your iOS version — pull the real values from your
  device's `IOHIDEvent.h` (iOSOpenDev headers) and rebuild.
- Set `W` header line to your device's logical resolution (e.g. `W 393 852`).

## 2. On Windows
```powershell
# forward USB port 5050 -> local 5050 (keep running)
iproxy 5050 :5050

cd windows
dotnet run
```
You should see `connected. stream:` and your taps moving the cursor in osu!.

## Protocol (lines, ASCII)
| line              | meaning                          |
| ----------------- | -------------------------------- |
| `W 393 852`       | device logical touch area (sent on connect) |
| `D <x> <y> <p>`   | touch down/move at (x,y), pressure |
| `U`               | touch up (left button released)  |

## Caveats
- Private IOHID touch APIs are undocumented — field constants may need a small fix
  per iOS version (debug mode will show you what's happening).
- This is **not** a real HID tablet, so osu! sees a mouse: set osu! mouse
  sensitivity to `1.0` and use `RemoveLimiter=false` only if needed.
- No pressure/tilt goes to osu! through this path (osu! doesn't use them).
- Raw touch rate follows the screen (≤120 Hz ProMotion, ≤60 Hz other iPhones).
