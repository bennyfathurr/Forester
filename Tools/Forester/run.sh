#!/bin/sh
set -eu
cd "$(dirname "$0")/../.."
FORESTER_SCRIPTING=/Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app/Contents/Resources/Scripting
mkdir -p /tmp/forester-tests
"$FORESTER_SCRIPTING/MonoBleedingEdge/bin/mono" "$FORESTER_SCRIPTING/MonoBleedingEdge/lib/mono/4.5/csc.exe" -nologo -out:/tmp/forester-tests/offline.exe Assets/Forester/Domain/*.cs Assets/Forester/Application/*.cs Assets/Forester/Tests/EditMode/RuleCases.cs Tools/Forester/Runner.cs
"$FORESTER_SCRIPTING/MonoBleedingEdge/bin/mono" /tmp/forester-tests/offline.exe
