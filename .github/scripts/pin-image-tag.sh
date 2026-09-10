#!/usr/bin/env bash
# Set or print services.<name>.image.tag in a Mimir services YAML. Generic —
# should not need edits regardless of your services' names or count.
#   pin-image-tag.sh FILE SERVICE TAG
#   pin-image-tag.sh --print FILE SERVICE
set -euo pipefail

print_only=0
if [[ "${1:-}" == --print ]]; then
  print_only=1
  shift
fi

file=${1:?file}
service=${2:?service}
tag=${3:-}

python3 - "$print_only" "$file" "$service" "$tag" <<'PY'
import re
import sys
from pathlib import Path

print_only, path, service, tag = int(sys.argv[1]), sys.argv[2], sys.argv[3], sys.argv[4]
text = Path(path).read_text()
pat = rf'(  {re.escape(service)}:\n    image:\n      tag: )"([^"]+)"'
match = re.search(pat, text)
if not match:
    sys.exit(f"no image.tag under services.{service} in {path}")
if print_only:
    print(match.group(2))
    raise SystemExit(0)
if not tag:
    sys.exit("TAG is required")
Path(path).write_text(re.sub(pat, rf'\1"{tag}"', text, count=1))
print(f"Set services.{service} image tag in {path} to {tag}")
PY
