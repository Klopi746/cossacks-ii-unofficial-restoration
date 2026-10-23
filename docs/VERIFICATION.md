# Preview verification record

## 0.1.1-preview — 23 October 2026

This release changes maintenance and file-safety code, not the formation recipe or the resulting game EXE. The checked setup SHA-256 is `88E3C1ED014E7B5B1579456EEB49C5782228D5C51F2876E43D7775919C63CBD9`. The release's `SHA256SUMS.txt` identifies the downloadable files.

### Automated checks

- Installer fixtures use real supported engine/helper/settings files and synthetic user data. They cover ordinary installation, repeated installation, removal by the installed maintenance EXE, reinstall after removal, preservation of the archived history, settings/engine conflicts and original/edited/foreign shortcuts.
- Negative cases reject unsupported originals, read-only outputs, corrupt backup bytes, unknown/duplicate manifest names, redirected backup paths, invalid digests, null file lists, report collisions, reports inside the game and unsupported dry-run combinations. Rejection checks compare file contents before and after.
- Controlled failures include a late sharing violation, an NTFS deny-write/delete ACL and terminating only the test installer after persisted write intent. Recovery restores the original fixture files. No test disables supported-file or memory checks.
- Quiet recovery verifies original read-only state, byte preservation, retention of new nonempty logs, unrelated attributes and idempotence. A hard-linked log is refused before changing the external alias's bytes/attributes. Create-new reports and path containment are tested separately.
- A complete isolated game copy contains all 6,533 original files. Installation, unchanged reinstall, self-removal, reinstall and recovery after a later settings edit preserve original hashes/attributes, saved-game data and the edited settings in conflicts. The old user installation and manual-test copy are not used as mutation targets.
- The final external setup accepts a genuine 0.1.0 manifest for removal, then passes its own install/self-removal/reinstall/removal cycle. The original 0.1.0 release asset remains unchanged.
- The recipe still produces `EC5B4E1EB1410E66223AD86C5F69B97B2646552A5B2AEDE2B4072111D85C9605`. Static PE checks and all 26 isolated x86 scenarios/comparisons pass. They test synthetic state and stubbed services without invoking the Windows game loader.
- Real WinForms navigation, consent, install and completion events pass in the off-screen harness. English and Russian wizard pages were rendered and inspected. Windows PowerShell 5.1 and PowerShell 7 builds match byte-for-byte. Public CI repeats the build and game-free checks.

Tests ran on Windows 11 Pro, build 26300. The agent did not start the game or DxWnd, switch display modes, change GPU settings, install drivers or weaken Windows protection. A Microsoft Defender custom scan of the setup returned no matching detections; this is a check of that file, not a general safety guarantee.

### Manual session identified

The user reported a successful diagnostic run and supplied the [screenshot](images/README.md). The matching launcher record starts at **2026-10-23 14:36:38 UTC**, identifies diagnostic mode and a 2560×1440 launch configuration, and belongs to the installed **0.1.0-preview C# launcher**. Its renderer log identifies DXVK 3.1.1, NVIDIA GeForce RTX 4070 Ti and driver 610.88.0. The game EXE matches the pinned patched hash.

The available writable game log has 1,425 bytes, with ordinary renderer/production records from 17:36:39 to 17:37:49 local time and barracks completion. It contains no repeated `no>=MaxOf`/`MaxType` warning. It does contain missing-texture, missing-dialog and render-state warnings; it is not a warning-free log. The screenshot shows about 59.7 FPS at one moment. Neither the log interval nor that frame establishes the total session duration or sustained performance.

Version 0.1.1's maintenance changes have automated evidence. A new gameplay session with 0.1.1 was not run as part of this work. Raw diagnostic files and local paths stay private; a sanitized summary is in `verification/manual-session-2026-10-23.json`.

### Remaining acceptance

Continue with a longer diagnostic battle, roads and village capture, then save → exit → relaunch → load, Alt+Tab and normal exit. Check normal-mode log-attribute recovery during an actual game session. Additional nations, long sessions, save compatibility, multiplayer, other GPUs/drivers, other Windows configurations, Wine, Windows on ARM and real gameplay from Unicode paths remain unverified.

The source audit found file-preservation defects and they were fixed; it did not establish a separate cross-privilege security exploit in the local `asInvoker` model. The installer is not a security sandbox against hostile concurrent writers. The package remains a preview.

## Historical 0.1.0 record, before the manual session

Date: 23 October 2026. Scope: the first independent installer for the supported original GOG Battle for Europe files. No game process was started for this release preparation. No Windows display mode, GPU setting, security setting or driver was changed.

### Confirmed locally for 0.1.0

- The C# recipe produces exactly the previously checked 24,567,808-byte patched EXE, SHA-256 `EC5B4E1EB1410E66223AD86C5F69B97B2646552A5B2AEDE2B4072111D85C9605`. An already patched input is rejected.
- The PE verifier passes: 46 changed bytes within the allowed original-prefix ranges, unchanged existing sections / entry point / imports / resources / stack reserve, valid checksum, original relocation coverage retained, 15 added relocations, and rebase checks.
- All 26 isolated x86 test scenarios / comparisons pass: 10 catalogue bounds cases, four original/new controls, seven further consumer scenarios and five UI scenarios. Canaries, row 127, center 15, sentinel handling, registers, stack, frees and allocation failure are checked. These use synthetic game state and stubbed services; no Windows game loader is involved.
- The reduced real-file installer fixtures pass installation, unchanged reinstall, removal, reinstall after removal, unsupported EXE rejection, read-only refusal, a late sharing violation with automatic rollback, and repeat recovery.
- User saves / player sentinels and new user files survive. Later settings and EXE edits are copied into conflicts before restoring originals. A corrupt original backup stops removal before changing current files. Existing and edited shortcuts are preserved; a name collision creates a numbered shortcut in an isolated test desktop.
- File operations pass with spaces and Cyrillic. Silent play requests are refused. Dry-run normal / diagnostic requests validate without starting processes or changing installed files. The test environment also forbids game launch.
- Actual WinForms Next → folder validation → components / consent → Install → completion events pass in an off-screen harness. English and Russian pages were rendered and visually inspected.
- The new C# quiet-log recovery passes nine isolated assertions: original writable/read-only state, unchanged bytes, journal completion and idempotence, deletion of an own empty new log, retention of non-empty evidence, unrelated attributes and a missing log. No game process is needed for these checks.
- Repeated builds match within one environment and between Windows PowerShell 5.1 and PowerShell 7 after using a deterministic ZIP STORE writer. The final release checksum is in `SHA256SUMS.txt`.

Machine-readable public summaries are in `verification/`; raw local logs and game-bearing fixtures are excluded. The public CI has no proprietary game files: it builds, compares a repeat build and rejects a synthetic unsupported EXE. Its dependency probe verifies the bundled DxWnd objects and source availability without starting native components.

### Component and source verification for 0.1.0

All four unchanged DxWnd runtime objects match the official build archive. The complete matching upstream source archive was inventoried, including its nested Detours source ZIP, MinHook 1.3.4 and Disasm201 DLL source / project. The committed corresponding-source collection preserves the required source trees and notices, excludes compiled objects / unrelated optional tools, and records file hashes. It is embedded in setup and provided separately with the release. Active local quoted includes resolve within the selected source trees; remaining header dependencies are Microsoft system development headers. A native bit-identical DxWnd rebuild has not been performed.

DXVK's unchanged x86 DLLs and zlib notice are embedded. Our own code and recipe are MIT. Installation is offline, with no SourceForge dependency, additional runtime download or security-setting change. See [THIRD_PARTY.md](../THIRD_PARTY.md) for exact sources, hashes and source delivery.

### Earlier manual observations before 0.1.0

On 21 October the user reported that the same patched game EXE worked in a short session. With writable game logging, ordinary records and barracks completion were present and repeated `no>=MaxOf` warnings were absent. A later short menu session confirmed output at 2560×1440 and the user accepted stretched full-width menu behavior. These observations belong to the earlier launch scripts, not to a new gameplay test of this C# setup / launcher.

### Checklist recorded before the 23 October diagnostic session

1. Install into a separate complete original game copy, close setup, and start through its new shortcut.
2. Check full-width menu, mouse position and clicks, resolution choice and primary-monitor placement.
3. Run `Restoration-Diagnostics.cmd`, start a skirmish, build a barracks, create formations, play 5–10 minutes, use roads and capture a village.
4. Confirm ordinary new log entries without repeated formation-capacity warnings; observe FPS and game speed.
5. Save, exit, relaunch and load. Check Alt+Tab, return to the battle and normal exit. Verify quiet-log attribute recovery in normal mode.
6. Try additional nations and longer sessions before relying on the preview. If a failure occurs, keep `.c2-restoration/sessions/`, the writable game log and the exact hashes; inspect them before changing settings.

Historical status before the later diagnostic session: actual loader / gameplay for the new launcher, long battles, all nations, save compatibility, multiplayer, other GPUs / drivers, other operating systems and real game launch from every Unicode path were unverified. The current evidence and remaining checks are at the top of this document; passing file tests does not prove gameplay scenarios.
