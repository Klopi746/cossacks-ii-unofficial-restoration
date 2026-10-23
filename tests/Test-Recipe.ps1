[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OriginalEngine,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if(Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new output directory.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$setup=Join-Path $projectRoot 'artifacts\CossacksII-Unofficial-Restoration-0.1.1-preview-setup.exe'
$source=@'
using System;
using System.IO;
using CossacksRestoration;
class Probe {
 static int Main(string[] args) {
  byte[] original=File.ReadAllBytes(args[0]);
  byte[] patched=EnginePatch.Apply(original,File.ReadAllText(args[1]));
  if(Hashing.Bytes(patched)!=EnginePatch.PatchedHash) return 1;
  bool rejected=false; try { EnginePatch.Apply(patched,File.ReadAllText(args[1])); } catch(InvalidDataException) { rejected=true; }
  if(!rejected) return 2;
  File.WriteAllBytes(args[2],patched);
  Console.WriteLine("PASS: expected input/output hashes; already-patched EXE rejected; no PE process created."); return 0;
 }
}
'@
$probeSource=Join-Path $OutputDirectory 'Probe.cs'
[IO.File]::WriteAllText($probeSource,$source)
$compiler=Join-Path $projectRoot 'vendor\compiler-4.8.0\tasks\net472\csc.exe'
$probe=Join-Path $OutputDirectory 'Probe.exe'
& $compiler /nologo /target:exe ("/out:"+$probe) ("/r:"+$setup) $probeSource
if($LASTEXITCODE -ne 0) { throw 'Probe compile failed.' }
Copy-Item -LiteralPath $setup -Destination (Join-Path $OutputDirectory ([IO.Path]::GetFileName($setup)))
& $probe $OriginalEngine (Join-Path $projectRoot 'src\patch-plan.json') (Join-Path $OutputDirectory 'engine-patched.exe')
if($LASTEXITCODE -ne 0) { throw 'Patch recipe contract failed.' }
