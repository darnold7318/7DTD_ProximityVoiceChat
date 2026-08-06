# R18 Release build verification

## Result

`ProximityPartyVoice.dll` compiled successfully in Release configuration from the R18 source.

```text
R18 RELEASE BUILD SUCCEEDED
OUTPUT=src/ProximityPartyVoice/bin/Release/net472/ProximityPartyVoice.dll
PDB=src/ProximityPartyVoice/bin/Release/net472/ProximityPartyVoice.pdb
REFERENCES=155
SOURCES=10
```

The generated installable mod folder is:

```text
Build/ProximityPartyVoice
```

## Compiler and target

- Compiler front end: Microsoft Roslyn `Microsoft.CodeAnalysis.CSharp` 5.6.0.0
- Optimization: Release
- Platform: Any CPU
- Output: classic managed PE/Mono-compatible .NET assembly
- Assembly identity: `ProximityPartyVoice, Version=1.0.15.0`
- Framework reference: `mscorlib, Version=4.0.0.0`

The build used the exact V3.1 b14 game assemblies supplied with this project plus Harmony. The `Assembly-CSharp.dll` used for both decompilation and compilation has SHA-256:

```text
b13862e30d8b28f42b83fe6a36bf074d155a6c43164e7b0797a6e4f77bd7dea3
```

## Output integrity

`Build/ProximityPartyVoice/ProximityPartyVoice.dll` SHA-256:

```text
8c2c207b74b3addf4a39e5bf19e63ebbc8c268751bc0bd696a9f6e752dd18f57
```

The compiled DLL was decompiled again with ILSpy. The resulting evidence under `evidence/ilspy/compiled/` confirms that the binary contains:

- the `PartyVoice.Update` Harmony prefix/postfix;
- the write to `platformPartyVoice.MuteSelf = false` for mod-requested transmission;
- typed `World.GetPlayers()` proximity enumeration;
- the R18 `1.0.15` startup identity.

## Scope of verification

This verifies the exact managed call path, source compilation, assembly target, packaged output, and compiled implementation. It does **not** replace an in-game two-client voice test. Runtime validation is still required to confirm microphone transmission and remote audibility in the game environment.
