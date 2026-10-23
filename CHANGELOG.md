# Changes

## 0.1.1-preview — 23 October 2026

This release repairs installer and recovery behavior. The patched game EXE, formation recipe, DXVK 3.1.1 and DxWnd 2.06.16 are unchanged.

- **Reinstall after self-removal:** the maintenance EXE retained by Windows is now treated as part of archived history, not as an original file at its old path. The old sequence failed while backing up that moved file.
- **CLI dry-run:** `--install --dry-run` and `--uninstall --dry-run` now refuse the request before changes. Previously those combinations performed the operation. `--dry-run` is supported only for play/diagnostic plan validation; `--validate` checks installation compatibility.
- **Report files:** `--report` requires a new path outside the game folder. Existing files are never overwritten, including by error reports or a competing writer.
- **Recovery ownership:** manifests with unknown or duplicate file names, invalid hashes or redirected backup paths are rejected. Shortcut cleanup requires the expected desktop name, unchanged bytes and the expected target/arguments. Foreign records are retained.
- **Linked files:** existing reparse points and hard-linked files are rejected before file/attribute operations. This prevents a hard-linked log from changing the attributes of another path to the same file. Concurrent hostile filesystem replacement is not a supported security boundary.
- **Process-exit race:** process inspection tolerates a process that exits during inspection. Quiet-log recovery is centralized in uninstall; an unused stored log hash was removed.
- **Evidence and documentation:** the user's successful diagnostic session is identified as a 0.1.0 C# launcher session. Its original screenshot is retained, and its limits are stated. Windows/Vulkan requirements, upgrade steps, link restrictions and ownership checks are documented.

The tests cover self-removal and reinstall, corrupt journals, report collisions, linked paths, a denied-write ACL, a sharing violation and forced termination after write intent. Full-copy preservation and isolated x86 checks are reported in [VERIFICATION.md](docs/VERIFICATION.md).

Upgrade with the new external setup: remove/recover the previous installation, then install again. Keep the recovery history. The old 0.1.0 release assets remain unchanged.

## 0.1.0-preview — 23 October 2026

First public offline installer, with English/Russian wizard, the locally applied formation-table recipe, bundled DXVK/DxWnd, desktop shortcut, normal/diagnostic launch and conflict-preserving recovery. Historical machine-readable checks remain in `verification/`.
