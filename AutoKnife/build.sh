#!/bin/bash
# AutoKnife Build Script - Using actual game DLLs
set -e

echo "=== Building AutoKnife v1.0.6 (Pure Reflection + Game DLLs) ==="

# Clean previous build
rm -f AutoKnife.dll

# Build stubs (BepInEx references UnityEngine, Harmony is standalone)
echo "Building stubs..."
mcs -target:library -out:libs/BepInEx.dll \
  -r:/workspace/projects/assets/Managed/Managed/UnityEngine.CoreModule.dll \
  -r:/workspace/projects/assets/Managed/Managed/netstandard.dll \
  stubs/BepInExStub.cs
mcs -target:library -out:libs/0Harmony.dll stubs/HarmonyStub.cs

# Build the mod using actual game DLLs for Unity types
# This eliminates all version mismatch issues
echo "Building AutoKnife.dll..."
mcs -target:library -out:AutoKnife.dll \
  -r:libs/BepInEx.dll \
  -r:libs/0Harmony.dll \
  -r:/workspace/projects/assets/Managed/Managed/UnityEngine.CoreModule.dll \
  -r:/workspace/projects/assets/Managed/Managed/Unity.InputSystem.dll \
  -r:/workspace/projects/assets/Managed/Managed/Assembly-CSharp.dll \
  -r:/workspace/projects/assets/Managed/Managed/netstandard.dll \
  src/AutoKnife.cs

echo "Build complete: AutoKnife.dll ($(stat -c%s AutoKnife.dll) bytes)"
