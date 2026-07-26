#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

bash scripts/verify-postgresql-only.sh .

package_feed="${RVT_PACKAGE_FEED_DIR:-${repo_root}/artifacts/packages}"
nuget_packages="${repo_root}/artifacts/nuget-packages"
validation_locks="${repo_root}/artifacts/validation-locks"
package_version="0.2.0-rc.1"

common_project="${repo_root}/libs/rvt-monitor-common/src/Rvt.Monitor.Common/Rvt.Monitor.Common.csproj"
communication_abstractions_project="${repo_root}/libs/rvt-monitor-common/src/Rvt.Communication.Abstractions/Rvt.Communication.Abstractions.csproj"
communication_project="${repo_root}/libs/rvt-monitor-common/src/Rvt.Communication/Rvt.Communication.csproj"
graph_mail_project="${repo_root}/libs/rvt-monitor-common/src/Rvt.Communication.MicrosoftGraphMail/Rvt.Communication.MicrosoftGraphMail.csproj"
sendgrid_mail_project="${repo_root}/libs/rvt-monitor-common/src/Rvt.Communication.SendGridMail/Rvt.Communication.SendGridMail.csproj"
transmit_sms_project="${repo_root}/libs/rvt-monitor-common/src/Rvt.Communication.TransmitSms/Rvt.Communication.TransmitSms.csproj"
integration_testing_project="${repo_root}/libs/rvt-monitor-common/testing/Rvt.Monitor.IntegrationTesting/Rvt.Monitor.IntegrationTesting.csproj"
runtime_consumer_project="${repo_root}/libs/rvt-monitor-common/package-validation/RuntimeConsumer/RuntimeConsumer.csproj"
test_consumer_project="${repo_root}/libs/rvt-monitor-common/package-validation/TestConsumer/TestConsumer.csproj"
solution="${repo_root}/Rvt.Mono.slnx"

mkdir -p "${package_feed}" "${nuget_packages}" "${validation_locks}"

dotnet restore "${common_project}" --packages "${nuget_packages}"
dotnet restore "${communication_abstractions_project}" --packages "${nuget_packages}"
dotnet restore "${communication_project}" --packages "${nuget_packages}"
dotnet restore "${graph_mail_project}" --packages "${nuget_packages}"
dotnet restore "${sendgrid_mail_project}" --packages "${nuget_packages}"
dotnet restore "${transmit_sms_project}" --packages "${nuget_packages}"
dotnet restore "${integration_testing_project}" --packages "${nuget_packages}"

dotnet pack "${common_project}" --no-restore --output "${package_feed}" -p:PackageVersion="${package_version}"
dotnet pack "${communication_abstractions_project}" --no-restore --output "${package_feed}" -p:PackageVersion="${package_version}"
dotnet pack "${communication_project}" --no-restore --output "${package_feed}" -p:PackageVersion="${package_version}"
dotnet pack "${graph_mail_project}" --no-restore --output "${package_feed}" -p:PackageVersion="${package_version}"
dotnet pack "${sendgrid_mail_project}" --no-restore --output "${package_feed}" -p:PackageVersion="${package_version}"
dotnet pack "${transmit_sms_project}" --no-restore --output "${package_feed}" -p:PackageVersion="${package_version}"
dotnet pack "${integration_testing_project}" --no-restore --output "${package_feed}" -p:PackageVersion="${package_version}"

for package_id in \
  Rvt.Monitor.Common \
  Rvt.Communication.Abstractions \
  Rvt.Communication \
  Rvt.Communication.MicrosoftGraphMail \
  Rvt.Communication.SendGridMail \
  Rvt.Communication.TransmitSms \
  Rvt.Monitor.IntegrationTesting; do
  package_artifact="${package_feed}/${package_id}.${package_version}.nupkg"
  if [[ ! -f "${package_artifact}" ]]; then
    printf 'Missing package artifact: %s\n' "${package_artifact}" >&2
    exit 1
  fi
done

validation_package_feed="${repo_root}/libs/rvt-monitor-common/artifacts/packages"
mkdir -p "$(dirname "${validation_package_feed}")"
rm -rf "${validation_package_feed}"
ln -s "${package_feed}" "${validation_package_feed}"

for package_id in \
  rvt.communication.abstractions \
  rvt.communication \
  rvt.communication.microsoftgraphmail \
  rvt.communication.sendgridmail \
  rvt.communication.transmitsms \
  rvt.monitor.common \
  rvt.monitor.integrationtesting; do
  rm -rf "${nuget_packages:?}/${package_id}/${package_version}"
done

dotnet restore "${runtime_consumer_project}" --packages "${nuget_packages}" --force-evaluate -p:RestoreLockedMode=false -p:RvtUseArtifactValidationLocks=true
dotnet restore "${test_consumer_project}" --packages "${nuget_packages}" --force-evaluate -p:RestoreLockedMode=false -p:RvtUseArtifactValidationLocks=true
dotnet restore "${solution}" --packages "${nuget_packages}" --force-evaluate -p:RvtUseArtifactValidationLocks=true
dotnet build "${solution}" --no-restore --nologo
dotnet test "${solution}" --no-build --nologo
