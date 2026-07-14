#!/bin/bash
# AutoKnife Build Script
# Compiles the merged Lethal Company V81 mod from source
# Requires: mono-mcs (apt-get install mono-mcs)

set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

echo "=== AutoKnife Build ==="
echo "[1/3] Compiling stub assemblies..."

mcs -target:library -out:libs/UnityEngine.CoreModule.dll stubs/UnityEngineStub.cs
mcs -target:library -out:libs/Unity.InputSystem.dll -r:libs/UnityEngine.CoreModule.dll stubs/InputSystemStub.cs
mcs -target:library -out:libs/0Harmony.dll stubs/HarmonyStub.cs
mcs -target:library -out:libs/BepInEx.dll -r:libs/UnityEngine.CoreModule.dll stubs/BepInExStub.cs
mcs -target:library -out:libs/Assembly-CSharp.dll -r:libs/UnityEngine.CoreModule.dll -r:libs/Unity.InputSystem.dll stubs/AssemblyCSharpStub.cs

echo "[2/3] Compiling AutoKnife.dll..."

mcs -target:library -out:AutoKnife.dll \
  -r:libs/BepInEx.dll \
  -r:libs/0Harmony.dll \
  -r:libs/UnityEngine.CoreModule.dll \
  -r:libs/Unity.InputSystem.dll \
  -r:libs/Assembly-CSharp.dll \
  src/AutoKnife.cs

echo "[3/3] Verifying..."
strings -e l AutoKnife.dll | grep "TAIGU"
echo ""
echo "=== Build complete: AutoKnife.dll ==="
ls -la AutoKnife.dll
