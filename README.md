# ![Logo](/assets/icon_32.png) Universal Tracker Markers

> A SteamVR utility to display markers relative to any tracked device (including Lighthouses).

![screenshot with tracked devices and markers](/assets/github/screenshot6.png)

## Features

- Attach any PNG or other image to a tracked device
	- Change opacity, tint, size, offset, etc
- Proximity-based opacity, i.e. fade out markers that are far away from a controller or the HMD
- OSC control to hide and show markers

## Installation

Head to the [Releases](https://github.com/jangxx/UniversalTrackerMarkers/releases) page and download the latest version. Unpack it somewhere and run `UniversalTrackerMarkers.exe`.

## Usage

Opening the program for the first time will greet you with this interface.

![screenshot with no markers](/assets/github/screenshot1.png)

After clicking on `Add marker` and selecting the marker on the left, you will see the actual marker settings UI

![screenshot marker settings](/assets/github/screenshot2.png)

In order to see the marker you will need to at least pick a device to attach it to and an image to use as a marker.
All other settings are optional.

Right clicking a marker in the list on the left opens a context menu to delete and duplicate an existing marker.

![marker context menu](/assets/github/screenshot5.png)


## Marker settings

From top to bottom, these are the marker settings and their functions.

- `Marker enabled`: Hides or shows the marker. If this is ticked off, the marker will never show up, even when shown with OSC.
- `Marker name`: Only used in the list on the left for organization.
- `Attached to device`: Select the tracked device to attach the marker to here. You can pick any tracked device SteamVR sees, including Lighthouses.
- `Overlay opacity`: Set the maximum opacity of the marker. It can be less opaque if the proximity features are enabled further down.
- `Overlay color`: Change the tint color of the marker. If the image is white this will set the color of the marker directly, otherwise it will make the image look tinted.

---

- `Overlay width`: Changes the size of the marker
- `X, Y, Z axis`: Changed the offset of the marker. The coordinate system is determined by the tracked device itself and may not always be intuitive.
- `X, Y, Z rotation`: Rotation of the marker, _after_ the position offset has been applied.
- `Reset transform`: Resets all transform values back to 0.
- `Set transform relative to device`: This button allows you to automatically set the marker transform so that the marker position matches another device. This it probably most useful when trying to create a marker relative to a fixed device like a Lighthouse.

---

- `Proximity features enabled`: Fade the marker in and out depending on the distance between it and either the HMD or a controller.
- `Proximity device`: Select either `HMD`, `LeftHand`, `RightHand` or `AnyHand` with the last option always picking the device closer to the marker.
- `Full opacity distance`: If the chosen device is closer than this distance, the marker will be shown at full opacity, i.e. whatever is set up as the `Overlay opacity` above.
- `Zero opacity distance`: If the chosen device is farther away than this distance, the marker will have 0 opacity, i.e. be fully invisible.

---

- `OSC control enabled`: Allow showing and hiding this marker by sending `true` or `false` to the specified address.
- `OSC Address`: An address like `/path/to/parameter` that you want to send a boolean value to.
- `Start hidden when OSC is used`: If this is checked, the marker will be hidden by default, and only shown if the OSC packet is received. If it's not checked, the marker is visible by default. _Start hidden_ refers to either program startup or the `Marker enabled` checkbox being checked.

## Other settings

### OSC settings

![OSC settings](/assets/github/screenshot3.png)

In this section you can configure the OSC server. Settings are applied as soon as you tab out of one of the input fields or when the `OSC server enabled` checkbox is checked and unchecked.

### File menu

- `Save config`: Saves the current config to a config file in `%localappdata%\jangxx\Universal Tracker Markers`. The program will also warn you if you're exiting while having unsaved changes.
- `Refresh devices`: Refreshes the list of connected SteamVR devices. Normally this is not neccessary and happens automatically as they connect and disconnect.

### View menu

- `Start minimized`: If this is checked, the program will start in minimized state. Probably the most useful if used in combination with enabling SteamVRs automatic startup feature.
- `Minimize to tray`: If this is checked, minimizing the program will hide it to the system tray so it doesn't clutter up your taskbar.
- `Show serial numbers on devices`: Add overlays to all devices showing their serial numbers in VR view. This should simplify the process of picking a device from the dropdown menus.

![serial numbers shown on devices](/assets/github/screenshot4.png)