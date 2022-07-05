#!/bin/bash

# Written for me (Joey), and working on Ubuntu 20.04 LTS.  I make no promises
# that it will work for you.  See .github/workflows/ for repeatable
# instructions to install necessary .NET tools.

set -e

if [ "$1" == "" ]; then
  BUILD_TYPE=Debug
else
  BUILD_TYPE="$1"
fi

if [ "$2" == "" ]; then
  GAME_NAME="Valheim"
else
  GAME_NAME="$2"
fi

PLUGINS_PATH=~/.local/share/Steam/steamapps/common/"$GAME_NAME"/BepInEx/plugins

cd "$(dirname "$0")"/..

cp FarmCounter/bin/$BUILD_TYPE/FarmCounter.dll "$PLUGINS_PATH"/

echo "Installed $BUILD_TYPE build."
