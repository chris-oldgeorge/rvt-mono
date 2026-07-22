#!/usr/bin/env bash
set -euo pipefail

pushd RvtPortal.Client >/dev/null
npm ci
npm run lint
npm run test:run
npm run build
popd >/dev/null

test -f RvtPortal.Client/dist/index.html
