#!/usr/bin/env bash
# Point test's services.SERVICE at TAG and push to main. Retries if two service
# workflows (e.g. gateway and web) pin the same file at once. Generic — the
# target path is the fixed Mimir manifest convention, no edits needed.
#   pin-test-and-push.sh SERVICE TAG
set -euo pipefail

service=${1:?service}
tag=${2:?tag}
file=deploy/mimir/services.test.yaml
pin=$(cd "$(dirname "$0")" && pwd)/pin-image-tag.sh
# Keep this run's script; git reset --hard may drop it from the tree.
cp "$pin" /tmp/pin-image-tag.sh

git config user.name github-actions[bot]
git config user.email github-actions[bot]@users.noreply.github.com

for attempt in 1 2 3 4 5; do
  git fetch origin main
  git reset --hard origin/main
  bash /tmp/pin-image-tag.sh "$file" "$service" "$tag"
  git add "$file"
  if git diff --staged --quiet; then
    echo "already pinned to ${tag}"
    exit 0
  fi
  git commit -m "chore: pin test ${service} to ${tag} [skip ci]"
  if git push origin HEAD:main; then
    exit 0
  fi
  echo "push conflict on attempt ${attempt}; retrying"
  sleep $((attempt * 2))
done
echo "failed to pin test after retries" >&2
exit 1
