# iPhone → osu! tablet over USB TETHERING (no jailbreak)

No raw network: traffic stays on the USB cable via iPhone's RNDIS-over-USB adapter.

## Windows
```powershell
cd tethered
dotnet run
```
→ prints your PC's tethering IP (e.g. `192.168.137.1` / `172.20.10.x`), then
listens on UDP 4242 and injects an absolute mouse (left button held while touching).

## iPhone
1. Settings → Personal Hotspot → **Allow Others to Join** (ON) — plug USB in.
2. Create Xcode project (iOS SwiftUI App, min iOS 15), drop in the three files
   from `tethered/ios/`, run on your iPhone.
3. Type the IP from step above, tap Start.
4. osu! → mouse sensitivity `1.0`.

## Notes
- Works at up to 120 Hz (ProMotion iPhones); non-Pro devices cap at 60 Hz.
- Watchdog auto-releases the mouse button if packets stop (dropped UP).
- If RNDIS IP isn't found: install iTunes/Apple Devices, unplug/replug, trust dialog.
- Stream format matches `../README.md`: `W`, `D x y p`, `U`.

## Linux (your osu! box)
1. Tether shows up as `enx…`/`ipheth` (`ip link`). Your PC IP is typically
   `172.20.10.2`; find it with `ip addr` and put that in the iPhone app.
2. `dotnet run` (same project — auto-switches to /dev/uinput).
3. Grant access: `sudo chmod a+rw /dev/uinput`, or add a udev rule + `input` group.
4. Screen size: set `TAB_W`/`TAB_H` env vars (definition: your monitor size),
   e.g. `TAB_W=1920 TAB_H=1080 dotnet run`.
5. The virtual device appears as `iphone-tab` (absolute X/Y + left button); osu!
   sees it as a tablet/mouse — set osu! mouse sensitivity to `1.0`.
6. No OpenTabletDriver needed (that's the point of this path). If you still have
   OTD owning a real tablet, disable its profile while using this.
