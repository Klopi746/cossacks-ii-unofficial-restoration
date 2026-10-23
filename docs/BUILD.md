# Reproduce the setup build

Use Windows with .NET Framework 4.8+, Windows PowerShell 5.1 or PowerShell 7, built-in `tar.exe`, and Internet access for the pinned build inputs. These are **developer** requirements. Players do not need Python, PowerShell 7, Roslyn, an SDK or Visual Studio.

From the repository root:

```powershell
powershell.exe -NoProfile -File .\build\Build.ps1
```

The script downloads Microsoft.Net.Compilers.Toolset **4.8.0** from official NuGet and DXVK **3.1.1** from the official GitHub release, verifies their archive hashes and selected x86 DLLs, and verifies the committed DxWnd binaries / corresponding-source ZIP. It creates a payload with fixed timestamps and ZIP STORE entries, then invokes Roslyn with deterministic compilation and source path mapping. The payload contains all player runtime components, notices and corresponding DxWnd source. No game file is read during a build. Installation uses no network.

Output:

```text
artifacts/CossacksII-Unofficial-Restoration-0.1.0-preview-setup.exe
artifacts/SHA256SUMS.txt
artifacts/build-manifest.json
artifacts/DxWnd-2.06.16-corresponding-source.zip
```

Build inputs are cached under ignored `vendor/`; outputs are ignored under `artifacts/`. Do not commit either directory. Compiler archive SHA-256: `37333F4F1E2CE55E621355D6DA651DC23D4CB5F94A8F76B9478816E87F110AD9`. DXVK archive SHA-256: `40565B4A724AADC4433FA4E010B4B23916D9B1F1BAEEE64E17186DB94F54E608`. DLL pins and license texts are in the script and `licenses/`.

Run the build twice and compare the setup hash. The workflow repeats this check on `windows-2022`, then tests rejection of a synthetic unsupported file and uploads the setup/checksum/manifest. It never has access to proprietary game data and never starts a game. Cross-machine reproducibility is reported separately from the same-machine repeat check in the verification record.

## Optional local file tests with your own original

These tests need the supported original game folder as a read-only input and a **new, separate** work directory. They create reduced fixtures containing the real engine/helper/settings and synthetic user data. They preserve the input and do not start the game or DxWnd. A process-local environment guard forbids play, the desktop is redirected to an isolated test folder, and silent play requests are tested for rejection.

```powershell
.\tests\Test-Installer.ps1 -OriginalGame 'C:\Games\BfE-original' -WorkDirectory 'C:\Tests\BfE-install-check'
.\tests\Test-Recipe.ps1 -OriginalEngine 'C:\Games\BfE-original\engine.exe' -OutputDirectory 'C:\Tests\BfE-recipe-check'
```

Use paths suitable for your computer. Do not publish the work directory, generated patched EXE, local manifests, original files or private logs. Test cases cover installation, identical reinstall, Unicode / spaces, shortcut collision, unsupported inputs, read-only refusal, write failure and rollback, recovery, conflict preservation, backup corruption and user-data retention.

`tests/WizardSmoke.cs` is an optional harness for the actual WinForms Next / consent / Install / completion events. Compile it against the setup assembly and Windows Forms, place a copy of that assembly next to the harness, and pass a fresh reduced test fixture as its argument. The harness itself forbids game launch and disables shortcut creation. `tests/DependencySmoke.cs` verifies the embedded DxWnd objects and corresponding-source availability without starting any native component.

## Optional static PE and isolated memory checks

Developer-only Python dependencies: `pefile`, `capstone`, `unicorn`. Use a virtual environment; none is needed on players' computers. The scripts read both game files as bytes; they never load either as a Windows process. The x86 emulator replaces external game/OS services with synthetic fixtures and hooks.

```powershell
python .\tests\Test-PE.py --original 'C:\Games\BfE-original\engine.exe' --candidate 'C:\Tests\BfE-recipe-check\engine-patched.exe' --report 'C:\Tests\pe-result.json'
python .\tests\Test-MemoryContracts.py --original 'C:\Games\BfE-original\engine.exe' --candidate 'C:\Tests\BfE-recipe-check\engine-patched.exe' --report 'C:\Tests\memory-result.json'
```

Memory contracts and fixture addresses are locked to the supported hashes. These checks do not establish actual gameplay behavior, frame rate, save/load compatibility, OS loader acceptance or behavior on another GPU.

## Noninteractive maintenance

```powershell
Start-Process .\artifacts\CossacksII-Unofficial-Restoration-0.1.0-preview-setup.exe -ArgumentList '--silent --install --no-shortcut --game "C:\Games\Battle for Europe" --resolution 2560x1440 --report "C:\Tests\install.json"' -Wait
```

Actions: `--validate`, `--install`, `--uninstall`. `--game` specifies the target; `--resolution WIDTHxHEIGHT` is used for install; `--report` writes the result; `--no-shortcut` suppresses the desktop link. Exit code 0 means success, 1 means an explained refusal/failure. No action defaults to the GUI wizard. `--silent` never starts the game; play/diagnostic requests are refused unless accompanied by `--dry-run`, which validates the launch plan without starting processes or touching the log. `C2_RESTORATION_FORBID_GAME_LAUNCH=1` is an additional test guard. No testing switch bypasses supported-file checks.
