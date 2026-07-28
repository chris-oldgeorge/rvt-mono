#!/usr/bin/env bash
set -euo pipefail

runner_dist_root="${RUNNER_DIST_ROOT:-/opt/actions-runner-dist}"
runner_home="${RUNNER_HOME:-/home/runner/actions-runner}"
runner_state="${RUNNER_STATE_ROOT:-/runner-state}"
runner_user="${RUNNER_USER:-runner}"
bootstrap_only="${RUNNER_BOOTSTRAP_ONLY:-false}"
registration_files=(.runner .credentials .credentials_rsaparams)
mkdir -p "${runner_home}" "${runner_state}"

if [[ ! -x "${runner_home}/bin/Runner.Listener" ]]; then
  cp -a "${runner_dist_root}/." "${runner_home}/"
fi
chown -R "${runner_user}" "${runner_home}" "${runner_state}"

cd "${runner_home}"
for registration_file in "${registration_files[@]}"; do
  if [[ -f "${runner_state}/${registration_file}" ]]; then
    ln -sfn "${runner_state}/${registration_file}" "${runner_home}/${registration_file}"
  fi
done

if [[ ! -f "${runner_state}/.runner" ]]; then
  : "${RUNNER_REGISTRATION_TOKEN:?Set a short-lived repository runner registration token for first start.}"
  gosu "${runner_user}" ./config.sh \
    --unattended \
    --url "${RUNNER_URL}" \
    --token "${RUNNER_REGISTRATION_TOKEN}" \
    --name "${RUNNER_NAME}" \
    --labels "${RUNNER_LABELS}" \
    --work _work \
    --replace

  for registration_file in "${registration_files[@]}"; do
    mv "${runner_home}/${registration_file}" "${runner_state}/${registration_file}"
    ln -s "${runner_state}/${registration_file}" "${runner_home}/${registration_file}"
  done
fi

if [[ "${bootstrap_only}" == "true" ]]; then
  unset RUNNER_REGISTRATION_TOKEN
  exit 0
fi

unset RUNNER_REGISTRATION_TOKEN
exec gosu "${runner_user}" ./run.sh
