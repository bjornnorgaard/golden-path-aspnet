#!/usr/bin/env bash
# Copy each given service's image tag from the test manifest onto
# production. Never rebuilds — only moves already-tested tags forward.
#   copy-test-tags-to-production.sh SERVICE [SERVICE...]
set -euo pipefail

services=("$@")
if [[ ${#services[@]} -eq 0 ]]; then
  echo "usage: $0 SERVICE [SERVICE...]" >&2
  exit 1
fi

here=$(cd "$(dirname "$0")" && pwd)
pin=$here/pin-image-tag.sh
test_file=deploy/mimir/services.test.yaml
prod_file=deploy/mimir/services.prod.yaml

git config user.name github-actions[bot]
git config user.email github-actions[bot]@users.noreply.github.com

for service in "${services[@]}"; do
  tag=$(bash "$pin" --print "$test_file" "$service")
  bash "$pin" "$prod_file" "$service" "$tag"
done

git add "$prod_file"
if git diff --staged --quiet; then
  echo "production already matches test"
  exit 0
fi
git commit -m "chore: pin production to current test tags [skip ci]"
git push origin HEAD:main
