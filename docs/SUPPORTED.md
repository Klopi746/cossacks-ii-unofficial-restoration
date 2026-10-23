# Supported build fingerprints

Version 0.1.1-preview accepts the same measured **GOG Battle for Europe** build as 0.1.0. Identification uses file contents, not the folder name or the PE resource version. A different language/data package sharing these files has not thereby passed gameplay testing.

| Original file | SHA-256 |
|---|---|
| `engine.exe`, 23,515,840 bytes | `DB52B8A6926C63EF2B7BBF921C01B8F95A4ECD9F2B63BDA18F0EDD18E45F7517` |
| `X3D8.DLL` | `5D5B49A0366D5C98CE05F854B402169D8FC7DDF00A67393F810BB21436BEE7F5` |

The verified installation's menu reported `1.3G`. The PE resource reports `2,0,0,0`; that alone is not a reliable game-patch identifier. The local GOG metadata said English. No generic claim is made for every GOG release, localized pack, repack or modded data set.

Patched output: 24,567,808 bytes, SHA-256 `EC5B4E1EB1410E66223AD86C5F69B97B2646552A5B2AEDE2B4072111D85C9605`. The installer builds this output locally. That file is never a repository or release asset.

An already patched file is accepted only with this installer's own matching installed manifest. A patched file without that manifest is refused, preventing an unsafe second application or an assumed backup history.

Required settings files must also have the expected unique ASCII resolution keys. Missing / unexpected keys, read-only outputs, reparse points, hard-linked files and an active target game are rejected. Unknown original `X3D8.DLL` files are rejected rather than overwritten. Recovery also validates the manifest's owned-file names, hashes, backup mapping and dimensions; corrupt ownership records are refused before restoration writes.

Not supported: Napoleonic Wars, Steam, other engine hashes, AI / balance mods, other replacement engines, multiplayer, Windows on ARM, Wine. Windows 10 22H2 and Windows 11 are the intended desktop targets; this preview's local file and UI tests ran on Windows 11. GPU / driver compatibility still needs manual testing on each system.
