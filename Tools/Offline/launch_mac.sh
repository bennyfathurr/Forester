#!/bin/sh
set -eu
cd "$(dirname "$0")/../.."
open -a "$(pwd)/Builds/RageAgainstFactory.app" --args -screen-fullscreen 0 -screen-width 1280 -screen-height 900
