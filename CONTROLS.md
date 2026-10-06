# Standalone Xbox controller mod

Independent client input mod for the shipped Windows, 32-bit Unity 4.6 game. It has its own `ControllerSupport.dll`, source, config, patcher, installer, uninstaller and tests. It does not call or reference CreativeInventory and does not require an offline server or change server permissions. It can be installed over the current Creative mod or onto an otherwise unmodified compatible client.

## Install

Close the game. The server can stay running. Run `install.ps1` in PowerShell:

```powershell
& .\ControllerMod\install.ps1
# On another installation or from an extracted release:
& .\install.ps1 -GameDirectory 'C:\path\to\game'
```

The installer backs up the current game assembly, verifies the new hooks before replacing it, and preserves existing mod hooks. Launch the game with an Xbox/XInput controller connected. Automatic selection keeps the same controller until disconnected. A specific slot can be selected in settings. Release buttons, triggers and sticks after connection, switching modes or returning from another application; this prevents held controls from firing or clicking unexpectedly.

**F10** or **hold Back + Start for half a second** opens controller settings. Use the cursor to adjust sensitivity, deadzones, trigger threshold, cursor speed, inversion, hints, vibration and controller slot. Changes take effect immediately; Save persists them. Bindings are editable here, including chords; click an action, release controls, then press its new button(s). Back cancels binding capture. Set None to disable a binding. Restore defaults affects controller configuration only.

The mod creates `ControllerSupport.ini` and `ControllerSupport.log` beside its installed DLL in `BOL_Data\Managed`. Settings can also be edited as text and reloaded from the panel without restarting.

Look speed is in degrees per second (default 180), with a separate aim multiplier. It compensates the game's mouse sensitivity, zoom sensitivity and mouse inversion so controller settings remain independent. The menu cursor uses elapsed real time and continues working when simulation time is paused. If controller support is disabled in settings, F10 still lets you reopen the panel and re-enable it.

## Gameplay defaults

| Input | Action |
| --- | --- |
| Left stick | Move in the game's eight directions |
| Right stick | Analog camera look |
| RT / LT | Fire / aim |
| A / B | Jump / crouch |
| X | Interact when the game has an available target, otherwise reload |
| Y | Cycle weapon |
| LB / RB | Skill / grenade |
| Left / right stick click | Sprint / melee |
| D-pad up/right/down/left | Weapon slots 1/2/3/4 |
| Back / Start (release) | Inventory / pause or back |
| Back + Start (hold) | Controller settings |

The settings **Game menus** page opens inventory, skills, quests, map, friends, guild, group, shop, achievements, scoreboard and chat through the game's existing action handlers. Additional actions can be rebound there. Keyboard and mouse remain usable.

## Menus, equipment and text

All existing menus use a controller-driven cursor, including login, character selection, inventory, skills, shops, maps and dialogs. This keeps the original Flash UI and item interactions.

| Input | Menu action |
| --- | --- |
| Left stick | Cursor |
| Right stick / D-pad | Precise cursor |
| A (hold) | Left click / hold and drag items or sliders |
| X | Right click / item context menu |
| B / Start | Back through the game's existing menu handler |
| LT / RT | Scroll up / down |
| LB (hold) | Shift modifier, including inventory shortcuts |
| RB | Tab to next text field (LB+RB = Shift+Tab) |
| Y | On-screen keyboard |

Click a text field first, then press Y. Use the D-pad or left stick to select a key, A to type, X to backspace, LB to toggle uppercase/symbols, RB to advance fields, and Start to enter/submit. B or Y closes the keyboard without submitting. The keyboard supports English letters, digits, punctuation, space, backspace and tab, without showing or logging the text you type.

Cursor and text events are sent through Windows input only while this game's window is foreground. Focus loss, disconnect, mode switches, capture and shutdown release any mouse buttons/modifiers held by the mod and stop vibration. Native XInput deadzones are adjustable; idle sticks do not generate movement. Vibration is tied to the local player's native shot event when firing with the controller.

While controller settings or the keyboard is open, the mod disables the underlying Flash camera's mouse/touch input and filters its extra mouse/scroll handlers. Closing the panel restores the original flags after the closing frame; cached mouse-down state is reset to prevent a delayed click. The focused text field and keyboard input remain available for typing.

## Scope and validation

Camera look, triggers and cursor use analog input. Movement uses the game's existing directional flags and full movement speed; variable walking speed would require coordinated changes to the client/server movement protocol. Menus use the virtual cursor, rather than rebuilding the Flash widgets with directional focus. Existing keyboard button labels remain; the mod provides its own controller hints. No aim assistance is added. Existing game restrictions, including where firing is allowed, still apply.

The source compiles against the shipped Unity/.NET assemblies. Automated checks cover stick normalization, trigger independence, action edges, binding persistence, focus/disconnect/reconnect suppression, input ABI sizes, installation hooks, idempotent reinstallation, removal and preservation of unrelated methods/mod hooks. Hardware gameplay and every individual menu need an in-game check with the Xbox controller.

## Remove and rebuild

Close the game and run `uninstall.ps1` (supply `-GameDirectory` if needed). Removal edits only controller hooks in the current assembly, preserving other mods installed later. Configuration and backups are retained.

`build.ps1` uses the Windows .NET Framework compiler and Mono.Cecil 0.11.6 from the local NuGet cache. It writes a portable compiled package to `dist`; consumers only need the built package, not the Creative source or build tools. When rebuilding other assembly patches, re-run this controller installer afterward so its hooks remain present.

Native API contracts: [XInput controller state](https://learn.microsoft.com/en-us/windows/win32/api/xinput/nf-xinput-xinputgetstate), [Windows SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput), and [keyboard input/Unicode](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput).
