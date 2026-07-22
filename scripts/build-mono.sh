#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_feed="${RVT_PACKAGE_FEED_DIR:-${repo_root}/artifacts/packages}"
nuget_packages="${repo_root}/artifacts/nuget-packages"
validation_locks="${repo_root}/artifacts/validation-locks"
package_version="0.2.0-rc.1"

common_project="${repo_root}/libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj"
infrastructure_project="${repo_root}/libs/rvt-monitor-common/src/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj"
integration_testing_project="${repo_root}/libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj"
runtime_consumer_project="${repo_root}/libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj"
test_consumer_project="${repo_root}/libs/rvt-monitor-common/package-validation/TestConsumer/TestConsumer.csproj"
solution="${repo_root}/Rvt.Mono.slnx"

mkdir -p "${package_feed}" "${nuget_packages}" "${validation_locks}"

dotnet restore "${common_project}" --packages "${nuget_packages}"
dotnet restore "${infrastructure_project}" --packages "${nuget_packages}"
dotnet restore "${integration_testing_project}" --packages "${nuget_packages}"

dotnet pack "${common_project}" --no-restore --output "${package_feed}" -p:PackageVersion="${package_version}"
dotnet pack "${infrastructure_project}" --no-restore --output "${package_feed}" -p:PackageVersion="${package_version}"
dotnet pack "${integration_testing_project}" --no-restore --output "${package_feed}" -p:PackageVersion="${package_version}"

for package_id in \
  Rvt.Monitor.Common \
  Rvt.Monitor.Common.Infrastructure \
  Rvt.Monitor.IntegrationTesting; do
  package_artifact="${package_feed}/${package_id}.${package_version}.nupkg"
  if [[ ! -f "${package_artifact}" ]]; then
    printf 'Missing package artifact: %s\n' "${package_artifact}" >&2
    exit 1
  fi
done

validation_package_feed="${repo_root}/libs/rvt-monitor-common/artifacts/packages"
mkdir -p "$(dirname "${validation_package_feed}")"
if [[ ! -e "${validation_package_feed}" ]]; then
  ln -s "${package_feed}" "${validation_package_feed}"
fi

for package_id in \
  rvt.monitor.common \
  rvt.monitor.common.infrastructure \
  rvt.monitor.integrationtesting; do
  rm -rf "${nuget_packages:?}/${package_id}/${package_version}"
done

dotnet restore "${runtime_consumer_project}" --packages "${nuget_packages}" --force-evaluate -p:RestoreLockedMode=false -p:RvtUseArtifactValidationLocks=true
dotnet restore "${test_consumer_project}" --packages "${nuget_packages}" --force-evaluate -p:RestoreLockedMode=false -p:RvtUseArtifactValidationLocks=true
dotnet restore "${solution}" --packages "${nuget_packages}" --force-evaluate -p:RvtUseArtifactValidationLocks=true
dotnet build "${solution}" --no-restore --nologo
dotnet test "${solution}" --no-build --nologo
