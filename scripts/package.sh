#!/bin/bash

if [ -z "$RELEASE_VERSION" ]; then
  echo "You must set the environment variable \$RELEASE_VERSION," 1>&2
  echo "and use semantic versioning." 1>&2
  exit 1
fi

# Fail on error.
set -e

# Go to the project's root directory.
cd "$(dirname "$0")"/..

# Log steps.
set -x

# Build a clean release.
rm -rf FarmCounter/bin/
./scripts/build.sh Release

# Make a generic zip.
rm -rf staging FarmCounter.zip
mkdir -p staging/plugins
# Stage mod & assets.
cp FarmCounter/bin/Release/FarmCounter.dll staging/plugins/
# Stage mod metadata.
cp publish/icon.png staging/
cp README.md staging/
cat publish/manifest.json \
    | jq ".version_number = \"$RELEASE_VERSION\"" \
    > staging/manifest.json
# Zip it.
(cd staging; zip -r9 ../FarmCounter.zip *)

# Stop logging.
set +x

# Double-check versioning.
manifest_version=$(cat staging/manifest.json | jq -r .version_number)
dll_version=$(monodis --assembly staging/plugins/FarmCounter.dll \
              | grep Version | cut -f 2 -d : | tr -d ' ')
if [[ "$manifest_version.0" != "$dll_version" ]]; then
  echo "Version mismatch!"
  echo "  Manifest version $manifest_version"
  echo "  DLL version $dll_version"
  exit 1
fi
