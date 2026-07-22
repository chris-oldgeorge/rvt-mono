#!/usr/bin/env bash
set -euo pipefail

image="${1:-rvt-cloud-refactor-web:local}"
container="rvt-cloud-refactor-web-$RANDOM"

docker build --file RvtPortal.Client/Dockerfile --tag "$image" .
docker run --detach --rm --name "$container" --publish 127.0.0.1:18080:8080 "$image" >/dev/null
trap 'docker rm --force "$container" >/dev/null 2>&1 || true' EXIT

for attempt in {1..20}; do
    if curl --fail --silent http://127.0.0.1:18080/healthz >/dev/null; then
        break
    fi
    sleep 1
done

curl --fail --silent http://127.0.0.1:18080/healthz | grep -Fx 'ok'
curl --fail --silent http://127.0.0.1:18080/nonexistent-route | grep --fixed-strings '<div id="root"></div>'
docker image inspect --format '{{.Config.User}}' "$image" | grep -Fx '101'
