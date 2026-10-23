# Local formation-capacity patch

The compatible game declares 92 unique officer formation types in the observed data but the shared catalogue previously had capacity for 64. A bounds warning was emitted repeatedly during a tested barracks scenario. The guard was doing useful work: removing the warning or only changing the capacity constant would not make the consumers safe.

This patch preserves that guard and changes the two identified consumer layouts together:

- The UI's six heap buffers use 128-row dimensions consistently.
- The second consumer's large fixed stack-local tables move to a separate heap block of 563,412 bytes. Allocation failure returns safely; exit paths release the block. Limits, offsets and four row-stride shifts agree on capacity 128.
- Both paths skip unused `0xFFFF` rows before looking up the corresponding game object.
- The common collector, overflow warning and capacity check remain. Input with 129 rows still exercises the warning at capacity 128.

The output adds a `.bfix` read/execute PE section, preserves the existing sections and their flags, updates the PE checksum and relocation directory, retains the original relocations and adds 15 fixups. Entry point, imports, resources, image base, stack reserve and DLL characteristics remain as checked by the static verifier. Forty-six bytes in the original file prefix differ within the explicitly allowed PE-header and redirect ranges.

## What is distributed

`src/patch-plan.json` is a 361-block copy/transform recipe. It requires 1,832 original instruction bytes and 1,044,068 bytes of original relocation data from the user's exact input. Seventy-four literal code bytes are independently written helper/prologue/epilogue/guard code. The recipe also contains numeric PE metadata, relative redirects, padding and new relocation entries. It contains no complete original function body or original game file.

`EnginePatch.Apply` verifies the original file hash before reading the recipe, validates all copy ranges, constructs the output in memory, and verifies its full SHA-256 before the installer can replace anything. It refuses an already patched EXE as an input. The installer separately preserves the original and a transaction manifest before replacement.

The recipe reconstructs exactly `EC5B4E1EB1410E66223AD86C5F69B97B2646552A5B2AEDE2B4072111D85C9605`, the same output used in the successful short diagnostic session through the 0.1.0 C# launcher on 23 October. Version 0.1.1 does not change this recipe or the resulting game EXE. Neither that short session nor x86 emulation proves every nation, every formation definition, game service, save format or long-running battle.

## Evidence scope

The isolated tests execute selected actual x86 routines in an emulator with synthetic data and stubbed external services: 10 catalogue bounds cases, four original/new control comparisons, seven additional expanded-table / multi-center / allocation-failure scenarios, and five UI cases. They verify canaries, last row / last center, register and stack preservation, sentinel handling, and allocation/free behavior. The PE verifier also tests rebasing by `0x01000000`.

See [VERIFICATION.md](VERIFICATION.md) for release checks and the remaining manual checklist. The [licensing record](../THIRD_PARTY.md) separates this independent recipe from original game rights; a local recipe is not an automatic copyright clearance.
