# Borderlands Online Xbox Controller Mod

Standalone Xbox/XInput controls for the compatible Windows Borderlands Online client. Includes gameplay controls, a cursor for menus and inventory, item dragging, scrolling, an on-screen keyboard, rebinding, adjustable sensitivity and deadzones, and shot vibration.

**[Download XboxControllerMod-1.0.zip](https://github.com/jason11040/borderlands-online-xbox-controller-mod/releases/download/v1.0.0/XboxControllerMod-1.0.zip)** · **[Short text tutorial](TUTORIAL.txt)** · [Full controls](CONTROLS.md)

This first release is a **preview**: automated checks pass, but Xbox hardware and individual menus still need an in-game check. It targets the shipped Windows, 32-bit Unity 4.6 client. Movement retains the game's eight directions; camera and cursor input are analog. Menus use a controller-driven cursor and existing keyboard labels, with additional controller hints.

## Quick install

1. Download the ZIP from the link above. Right-click it, select **Properties → Unblock** if shown, then extract it into its own folder.
2. Close the game and connect your Xbox controller.
3. Open the extracted folder in Terminal and run this command, replacing the path with the folder containing **BOL.exe**:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -GameDirectory "C:\Path\To\Game"
   ```

4. Launch the game and release all controller buttons, triggers and sticks to activate it.
5. Press **F10**, or hold **View/Back + Menu/Start** for half a second, to change settings. Click **Save** to keep changes.

The Creative mod is not required. Existing mod hooks are preserved. Keep the extracted mod folder: it contains the installation record, uninstaller and backups.

## Default controls

| Input | Gameplay | Menus |
| --- | --- | --- |
| Left / right stick | Move / look | Cursor / precise cursor |
| RT / LT | Shoot / aim | Scroll down / up |
| A | Jump | Click; hold to drag |
| B | Crouch | Back |
| X | Interact with a target, otherwise reload | Item context menu |
| Y | Change weapon | On-screen keyboard |
| LB / RB | Skill / grenade | Hold Shift / next text field |
| Stick clicks | Sprint / melee | — |
| D-pad | Weapon slots | Precise cursor; keyboard selection |
| View/Back / Menu/Start | Inventory / pause or back, on release | Inventory / back |

To type, click a text field and press **Y**. Select a key with the D-pad or left stick; **A** types, **X** deletes, **LB** toggles Shift, **Start** submits, and **B/Y** closes the keyboard. The settings panel's **Game menus** tab opens skills, quests, maps, chat and other menus.

## Uninstall

Close the game and run this from the extracted mod folder:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\uninstall.ps1
```

Removal preserves other mod hooks and controller configuration. If the installation record is missing, supply the same `-GameDirectory` path used during installation.

## Source and checks

Downloads are ready to install; compiling is optional. The source is in `src`, the assembly patcher is in `tools`, and the tests use simulated inputs without sending real keyboard or mouse events.

```powershell
.\build.ps1 -GameDirectory 'C:\Path\To\Game'
.\test.ps1 -GameDirectory 'C:\Path\To\Game' -OriginalAssembly 'C:\Path\To\Unmodified\Assembly-CSharp.dll'
```

Building uses Windows .NET Framework 4 and the Mono.Cecil DLL bundled in `dist` (or its local NuGet cache). Testing requires a local compatible game assembly; no game binaries are distributed here. Supply an unmodified assembly to validate installation without other mods. When the game is closed, `tests\InstallerSmoke.ps1` checks installation and removal against a separate test fixture after `test.ps1` passes.

Checks cover input edges, independent triggers, deadzones, controller reconnects, focus loss, text and menu routing, paused menu cursor movement, UI click shielding, native input layouts, configuration persistence, installer/reinstaller/uninstaller behavior, and preservation of unrelated methods.

The release contains only the standalone mod, installer tools and documentation. [Mono.Cecil](https://github.com/jbevain/cecil) 0.11.6 is included under its [MIT license](Mono.Cecil.LICENSE.txt).
