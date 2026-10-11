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
    Installation.cs FileSafety.cs ConsumerInput.cs ConfigLocation.cs
    WindowLayout.cs UiText.cs DeviceFilter.cs
)
setup_sources=(
    Setup.cs Installation.cs FileSafety.cs ConsumerInput.cs ConfigLocation.cs
    WindowLayout.cs UiText.cs DeviceFilter.cs RemoteConfig.cs ActionModel.cs
    AppIcon.cs ActionEditor.cs SettingsForm.cs
)
compiler_options=(
    -nologo -sdk:4.5 -warnaserror:8001 -target:winexe -platform:x64 -optimize+
    -win32res:dist/app-native.res
    -resource:assets/app-icon.ico,app-icon.ico
    -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Web.Extensions.dll
)

mkdir -p dist
python3 build-win32-resources.py assets/app-icon.ico assets/app.manifest dist/app-native.res
mcs "${compiler_options[@]}" -out:dist/RockTekRemote.exe "${app_sources[@]}"
mcs "${compiler_options[@]}" -out:dist/RockTekRemoteSetup.exe \
    -resource:dist/RockTekRemote.exe,RockTekRemote.exe "${setup_sources[@]}"

# The portable package contains only the application, with no user settings.
(
    cd dist
    rm -f -- RockTekRemote-portable.zip
    zip -X RockTekRemote-portable.zip RockTekRemote.exe
    sha256sum RockTekRemote.exe RockTekRemoteSetup.exe RockTekRemote-portable.zip > SHA256SUMS
    sha256sum --check SHA256SUMS
)
