#!/usr/bin/env bash
set -euo pipefail

cd -- "$(dirname -- "${BASH_SOURCE[0]}")"
for tool in mcs python3 zip sha256sum; do
    command -v "$tool" >/dev/null || { printf 'Required tool not found: %s\n' "$tool" >&2; exit 1; }
done

# Keep these source lists and resources in sync with build.ps1.
app_sources=(
    RockTekRemote.cs HidConsumerReader.cs InputDiagnostics.cs ActionExecutor.cs RemoteMapping.cs HoldRepeater.cs
    RemoteConfig.cs ActionModel.cs AppIcon.cs ActionEditor.cs SettingsForm.cs
    NativeBackSupport.cs Installation.cs FileSafety.cs ConsumerInput.cs ConfigLocation.cs
    WindowLayout.cs UiText.cs DeviceFilter.cs PlayerPayloads.cs
)
setup_sources=(
    Setup.cs NativeBackSupport.cs Installation.cs FileSafety.cs ConsumerInput.cs ConfigLocation.cs
    WindowLayout.cs UiText.cs DeviceFilter.cs RemoteConfig.cs ActionModel.cs PlayerPayloads.cs
    AppIcon.cs ActionEditor.cs SettingsForm.cs
)
compiler_options=(
    -nologo -sdk:4.5 -warnaserror:8001 -target:winexe -platform:x64 -optimize+
    -win32res:dist/app-native.res
    -resource:assets/app-icon.ico,app-icon.ico
    -resource:native/timedhook.dll,timedhook.dll -resource:native/service-runner.exe,service-runner.exe
    -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Web.Extensions.dll
)

mkdir -p dist
python3 native/build.py
python3 build-win32-resources.py assets/app-icon.ico assets/app.manifest dist/app-native.res
mcs "${compiler_options[@]}" -out:dist/RockTekRemote.exe "${app_sources[@]}"
python3 prepare-framework-references.py
helper_options=(-nologo -sdk:4.5 -warnaserror:8001 -target:winexe -platform:x64 -optimize+ -win32res:dist/app-native.res -r:System.Windows.Forms.dll)
mcs "${helper_options[@]}" -out:dist/CloudMusPlayPause.exe -r:dist/RockTekRemote.exe \
    -r:.build/net48/UIAutomationClient.dll -r:.build/net48/UIAutomationTypes.dll -r:.build/net48/WindowsBase.dll \
    CloudMusPlayPause.cs CloudMusOptions.cs PlayerTarget.cs PlayerClose.cs
mcs "${helper_options[@]}" -out:dist/MurglarRemote.exe -r:System.Management.dll MurglarRemote.cs PlayerTarget.cs PlayerClose.cs
mkdir -p dist/profiles
cp profiles/CloudMus.json profiles/Murglar.json dist/profiles/
mcs "${compiler_options[@]}" -out:dist/RockTekRemoteSetup.exe \
    -resource:dist/RockTekRemote.exe,RockTekRemote.exe \
    -resource:dist/CloudMusPlayPause.exe,CloudMusPlayPause.exe -resource:dist/MurglarRemote.exe,MurglarRemote.exe \
    -resource:profiles/CloudMus.json,profiles.CloudMus.json -resource:profiles/Murglar.json,profiles.Murglar.json "${setup_sources[@]}"

# The portable package contains application, bridges and neutral ready-to-use profiles.
(
    cd dist
    rm -f -- RockTekRemote-portable.zip
    zip -X RockTekRemote-portable.zip RockTekRemote.exe CloudMusPlayPause.exe MurglarRemote.exe profiles/CloudMus.json profiles/Murglar.json
    sha256sum RockTekRemote.exe RockTekRemoteSetup.exe CloudMusPlayPause.exe MurglarRemote.exe profiles/CloudMus.json profiles/Murglar.json RockTekRemote-portable.zip > SHA256SUMS
    sha256sum --check SHA256SUMS
)
