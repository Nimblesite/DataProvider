#!/usr/bin/env bash
# Implements [CI-CLINICAL-LOCAL]: build and test the ClinicalCoding reference app
# (https://github.com/Nimblesite/ClinicalCoding) against this checkout instead of
# the published NuGet packages and dotnet tools.
#
# Usage: make clinical [CLINICAL_DIR=../ClinicalCoding] [CLINICAL_TARGETS="lint test build"]
#
# 1. Packs every DataProvider package + tool into artifacts/local-feed with a
#    unique 0.0.0-local.<timestamp> version (unique so the NuGet cache never
#    serves a stale build).
# 2. Points ClinicalCoding at that feed for the run only: a Directory.Build.rsp
#    overrides DataProviderVersion and adds the feed as a restore source, and the
#    local tool manifest is bumped to the same version. Both are restored on exit
#    so the ClinicalCoding working tree is left untouched.
# 3. Runs the requested ClinicalCoding make targets.
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
clinical_dir=$(cd "${CLINICAL_DIR:-$root/../ClinicalCoding}" && pwd)
targets=${CLINICAL_TARGETS:-lint test build}
feed="$root/artifacts/local-feed"
version="0.0.0-local.$(date -u +%Y%m%d%H%M%S)"
manifest="$clinical_dir/.config/dotnet-tools.json"
rsp="$clinical_dir/Directory.Build.rsp"

if [ -e "$rsp" ]; then
  echo "FAIL: $rsp already exists; refusing to overwrite it" >&2
  exit 1
fi

echo "==> Packing DataProvider $version into $feed"
rm -rf "$feed"
dotnet pack "$root/DataProvider.sln" --configuration Release -p:Version="$version" --output "$feed"

cp "$manifest" "$manifest.local-backup"
restore_clinical() {
  mv "$manifest.local-backup" "$manifest"
  rm -f "$rsp"
}
trap restore_clinical EXIT

printf -- '-p:DataProviderVersion=%s\n-p:RestoreAdditionalProjectSources=%s\n' \
  "$version" "$feed" >"$rsp"

cd "$clinical_dir"
for tool in dataprovidermigrate dataprovider lql; do
  dotnet tool update --local "$tool" --version "$version" --add-source "$feed"
done

echo "==> Running ClinicalCoding targets against local DataProvider: $targets"
read -r -a target_list <<<"$targets"
make "${target_list[@]}"
