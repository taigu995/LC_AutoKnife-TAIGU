#!/bin/bash
set -e

echo "Building AutoKnife mod..."

# Clean old build
rm -f AutoKnife.dll

# Compile - reference BepInEx, Harmony, UnityEngine.CoreModule (for MonoBehaviour, Time), and InputLegacyModule (for Input)
mcs -target:library -out:AutoKnife.dll \
  -r:libs/BepInEx.dll \
  -r:libs/0Harmony.dll \
  -r:/workspace/projects/assets/Managed/Managed/UnityEngine.CoreModule.dll \
  -r:/workspace/projects/assets/Managed/Managed/UnityEngine.InputLegacyModule.dll \
  -r:/workspace/projects/assets/Managed/Managed/netstandard.dll \
  src/AutoKnife.cs

echo "Build successful: AutoKnife.dll"
ls -lh AutoKnife.dll
