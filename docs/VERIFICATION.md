# Preview verification record

Date: 23 October 2026. Scope: the first independent installer for the supported original GOG Battle for Europe files. No game process was started for this release preparation. No Windows display mode, GPU setting, security setting or driver was changed.

## Confirmed locally

- The C# recipe produces exactly the previously checked 24,567,808-byte patched EXE, SHA-256 `EC5B4E1EB1410E66223AD86C5F69B97B2646552A5B2AEDE2B4072111D85C9605`. An already patched input is rejected.
- The PE verifier passes: 46 changed bytes within the allowed original-prefix ranges, unchanged existing sections / entry point / imports / resources / stack reserve, valid checksum, original relocation coverage retained, 15 added relocations, and rebase checks.
- All 26 isolated x86 test scenarios / comparisons pass: 10 catalogue bounds cases, four original/new controls, seven further consumer scenarios and five UI scenarios. Canaries, row 127, center 15, sentinel handling, registers, stack, frees and allocation failure are checked. These use synthetic game state and stubbed services; no Windows game loader is involved.
- The reduced real-file installer fixtures pass installation, unchanged reinstall, removal, reinstall after removal, unsupported EXE rejection, read-only refusal, a late sharing violation with automatic rollback, and repeat recovery.
- User saves / player sentinels and new user files survive. Later settings and EXE edits are copied into conflicts before restoring originals. A corrupt original backup stops removal before changing current files. Existing and edited shortcuts are preserved; a name collision creates a numbered shortcut in an isolated test desktop.
- File operations pass with spaces and Cyrillic. Silent play requests are refused. Dry-run normal / diagnostic requests validate without starting processes or changing installed files. The test environment also forbids game launch.
- Actual WinForms Next → folder validation → components / consent → Install → completion events pass in an off-screen harness. English and Russian pages were rendered and visually inspected.
- Repeated builds match within one environment and between Windows PowerShell 5.1 and PowerShell 7 after using a deterministic ZIP STORE writer. The final release checksum is in `SHA256SUMS.txt`.

Machine-readable public summaries are in `verification/`; raw local logs and game-bearing fixtures are excluded. The public CI has no proprietary game files: it builds, compares a repeat build and rejects a synthetic unsupported EXE. Its dependency probe verifies the bundled DxWnd objects and source availability without starting native components.

## Component and source verification

All four unchanged DxWnd runtime objects match the official build archive. The complete matching upstream source archive was inventoried, including its nested Detours source ZIP, MinHook 1.3.4 and Disasm201 DLL source / project. The committed corresponding-source collection preserves the required source trees and notices, excludes compiled objects / unrelated optional tools, and records file hashes. It is embedded in setup and provided separately with the release. Active local quoted includes resolve within the selected source trees; remaining header dependencies are Microsoft system development headers. A native bit-identical DxWnd rebuild has not been performed.

DXVK's unchanged x86 DLLs and zlib notice are embedded. Our own code and recipe are MIT. Installation is offline, with no SourceForge dependency, additional runtime download or security-setting change. See [THIRD_PARTY.md](../THIRD_PARTY.md) for exact sources, hashes and source delivery.

## Earlier manual observations

On 21 October the user reported that the same patched game EXE worked in a short session. With writable game logging, ordinary records and barracks completion were present and repeated `no>=MaxOf` warnings were absent. A later short menu session confirmed output at 2560×1440 and the user accepted stretched full-width menu behavior. These observations belong to the earlier launch scripts, not to a new gameplay test of this C# setup / launcher.

## Still requires manual acceptance

1. Install into a separate complete original game copy, close setup, and start through its new shortcut.
2. Check full-width menu, mouse position and clicks, resolution choice and primary-monitor placement.
3. Run `Restoration-Diagnostics.cmd`, start a skirmish, build a barracks, create formations, play 5–10 minutes, use roads and capture a village.
4. Confirm ordinary new log entries without repeated formation-capacity warnings; observe FPS and game speed.
5. Save, exit, relaunch and load. Check Alt+Tab, return to the battle and normal exit. Verify quiet-log attribute recovery in normal mode.
6. Try additional nations and longer sessions before relying on the preview. If a failure occurs, keep `.c2-restoration/sessions/`, the writable game log and the exact hashes; inspect them before changing settings.

Unverified: actual loader / gameplay for this new launcher, long battles, all nations, save compatibility, multiplayer, other GPUs / drivers, other operating systems and real game launch from every Unicode path. Do not describe passing file tests as passing these scenarios.
