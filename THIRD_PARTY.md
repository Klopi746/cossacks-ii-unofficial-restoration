# Components, licenses and corresponding sources

The independent installer, launcher and local patch recipe are [MIT-licensed](LICENSE). Cossacks II belongs to its owners and is not distributed here. This is an unofficial, free community patch requiring a compatible original game. [Original game on GOG](https://www.gog.com/en/game/cossacks_ii_anthology).

| Component | Version / origin | Distribution and terms |
|---|---|---|
| DXVK x86 D3D8 / D3D9 | 3.1.1, [official release](https://github.com/doitsujin/dxvk/releases/tag/v3.1.1) | Unchanged DLLs embedded in setup; D3D8 is also installed under the game's `X3D8.DLL` name. zlib notice: [DXVK-LICENSE.txt](licenses/DXVK-LICENSE.txt). Authors include Philip Rebohle, Joshua Ashton, Robin Kertels and Jeffrey Ellison. |
| DxWnd host / hook | 2.06.16, ghotik / GHO and SFB7; [official build](https://sourceforge.net/projects/dxwnd/files/Latest%20build/v2_06_16_build.rar/download) and [matching source](https://sourceforge.net/projects/dxwnd/files/Sources/v2_06_16_src.rar/download) | Four unchanged selected runtime objects, with GPL-3.0-or-later text and corresponding sources. See the source collection below. |
| Disassembler DLL | Included with DxWnd; Oleh Yuschuk's 2.01 source and upstream VS2008 DLL project are present in the matching source package | GPL-3.0-or-later. Sources / build project under `Disasm201/` in our corresponding-source ZIP; [original author page](https://www.ollydbg.de/version2.html). |
| MinHook / HDE | 1.3.4 sources supplied by DxWnd, Tsuda Kageyu / Vyacheslav Patkov | BSD 2-clause; full notices in [MinHook-HDE-LICENSE.txt](licenses/MinHook-HDE-LICENSE.txt), source and supplied VC9 project in the ZIP. |
| Microsoft Detours | Upstream-supplied source ZIP identifies commit `ea6c4ae7f3f1b1772b8a7cda4199230b932f5a50` | MIT; [Detours-MIT.txt](licenses/Detours-MIT.txt). The matching DxWnd source archive contains the complete nested source ZIP, retained in our source collection. |
| Incorporated DxWnd source portions | Wine, S3TC, stb_dxt and mciplayer notices | Original notices remain with the corresponding source; [LGPL-2.1](licenses/LGPL-2.1.txt), [S3TC MIT](licenses/S3TC-MIT.txt), [stb_dxt BSD](licenses/stb_dxt-NOTICE.txt), [mciplayer notice](licenses/mciplayer-NOTICE.txt) accompany the binary package. |
| Roslyn compiler | Microsoft.Net.Compilers.Toolset 4.8.0 from [official NuGet](https://www.nuget.org/packages/Microsoft.Net.Compilers.Toolset/4.8.0) | MIT; a downloaded build tool, not redistributed in setup and not required on players' computers. |
| Windows / .NET Framework | Existing OS components | Used under Microsoft's terms; not redistributed. Native upstream build tools / SDKs / MFC are also not redistributed. |

## Source delivery

[DxWnd-2.06.16-corresponding-source.zip](third_party/DxWnd-2.06.16-corresponding-source.zip) is committed beside the pinned binaries, embedded in setup, installed under `.c2-restoration/sources/`, and offered as a separate asset on the same preview release page. It is available without a charge or an account request. The source ZIP contains:

- The matching upstream `host`, `dll` and `Include` trees, including resources used by the native projects.
- The upstream `Disasm201` source, export definition and Visual C++ 2008 DLL project.
- The supplied `minhook-1.3.4` source, HDE, build projects and license.
- The exact upstream-supplied `Detours/Detours-main.zip`, including its full MIT source and build files.
- GPL and additional license texts, build notes, and a SHA-256 inventory of the selected source files.

The source collection preserves original source bytes. Precompiled objects, caches, IDE outputs and unrelated optional helper programs were omitted. This avoids substituting prebuilt libraries for their sources. The matching original source archive is pinned by SHA-256 in [provenance](third_party/DxWnd-2.06.16-provenance.json). Its native project lists some stale unused files; a quoted-include check resolves the active local headers to the supplied trees, with remaining headers belonging to the Microsoft system development environment. The active resource paths were reviewed separately.

The source README describes the upstream Visual C++ 2008 / Win32 / MFC environment and building the supplied MinHook and Detours libraries. [The upstream contributor](https://sourceforge.net/p/dxwnd/discussion/general/thread/911dc12203/?page=6) documents compatibility compiler flags for Detours. The restoration build uses the unchanged official binaries; a bit-identical native rebuild has **not** been performed. Supplying corresponding source does not mean that every native compiler combination or every runtime feature was tested.

The installer invokes DxWnd as a separate program. Its MIT license does not replace the GPL / BSD / MIT terms of these components. Users retain those components' redistribution and source rights under their own licenses. No extra restriction on those rights is added by the installer.

## Integrity pins

- Original DXVK 3.1.1 archive: `40565b4a724aadc4433fa4e010b4b23916d9b1f1baeee64e17186db94f54e608`.
- Original DxWnd 2.06.16 build archive: `bad187d929b5ea17d2461bac8579246a28410d2e712ad1c32676b99155ea3da7`.
- Original matching DxWnd source archive: `8f4bc9a324660b240e5f6747b4a84532d7b51fbdd60f52ae1f07d6faa247d1fd`.

The four runtime hashes and the assembled corresponding-source ZIP hash are in the committed provenance JSON. The runtime also checks the four DxWnd objects before installation. The build verifies the committed sources and runtime objects, and separately checks the downloaded compiler / DXVK inputs. Installation does not use the network.

## Original-game boundary

No ready-made game EXE, complete original game function body, GOG installer, save, extracted game asset, game logo, music, torrent or unauthorized download link is included in setup. The patch recipe stores local copy/transform operations, numeric offsets and independently written helper bytes. Original instruction sequences and relocation data are read from the user's hash-verified EXE on that computer. A local recipe is not an automatic copyright clearance.

The documentation includes one user-provided gameplay screenshot, published with permission, and a resized JPEG of the same image. The game's artwork and trademarks remain with their respective owners; the screenshot is not covered by this repository's MIT code license. It documents a real diagnostic session, not an endorsement or a performance guarantee. See [image provenance](docs/images/README.md). The screenshot is not embedded in setup.

[GSC's fan guidelines](https://www.gsc-game.com/guidelines/) allow noncommercial unofficial mods requiring the original game, subject to the applicable EULA and law. This project makes no claim of endorsement or legal clearance for every jurisdiction. The MIT license grants no game or trademark rights. A concrete rights-holder clarification, if needed, is: “May we publish this free, unofficial local patch recipe for the identified GOG Battle for Europe executable, with no game bytes or standalone game executable, requiring the original and linking to its official purchase page?” No message has been sent to a rights holder.
