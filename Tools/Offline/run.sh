#!/bin/sh
set -eu
cd "$(dirname "$0")/../.."
UNITY_SCRIPTING=/Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app/Contents/Resources/Scripting
mkdir -p /tmp/ratf-tests
JSON_DLL=$(find Library/PackageCache -path "*/com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll" -print -quit)
cp "$JSON_DLL" /tmp/ratf-tests/Newtonsoft.Json.dll
"$UNITY_SCRIPTING/MonoBleedingEdge/bin/mono" "$UNITY_SCRIPTING/MonoBleedingEdge/lib/mono/4.5/csc.exe" -r:"$JSON_DLL" -r:"$UNITY_SCRIPTING/MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll" -nologo -out:/tmp/ratf-tests/offline.exe Assets/Game/Domain/*.cs Assets/Game/Application/*.cs Assets/Game/Infrastructure/PlanJson.cs Assets/Game/Infrastructure/HttpCommanders.cs Assets/Game/Tests/EditMode/RuleCases.cs Assets/Game/Tests/EditMode/ProviderCases.cs Tools/Offline/Runner.cs
"$UNITY_SCRIPTING/MonoBleedingEdge/bin/mono" /tmp/ratf-tests/offline.exe
