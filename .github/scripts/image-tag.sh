#!/usr/bin/env bash
# Print the GHCR image tag: yy.mm.dd-HH.MM-shortsha. Adjust TZ to your team's.
# Example: 26.08.28-14.32-ef9bb12
# Time of day is a sequence hint (no GHCR/API lookup needed); the SHA keeps it unique.
#   image-tag.sh [SHA]
set -euo pipefail

sha=${1:-${GITHUB_SHA:?GITHUB_SHA is required}}
echo "$(TZ=Europe/Copenhagen date +%y.%m.%d-%H.%M)-${sha:0:7}"
