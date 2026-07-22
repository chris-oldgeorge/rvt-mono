# RVT Common Extraction Baseline

- Approved short commit: `8739750`
- Approved full commit: `87397504dfbdc72b7910d6043b004792e947aeb9`
- SDK: `10.0.203`
- Common path drift: none
- Common-related project references: 22
- Build: passed with zero errors
- IntegrationTesting tests: passed (6/6)
- Common tests: passed (426/426)
- Infrastructure tests: passed (64/64)
- AirQ tests: passed (118/118)
- MyATM tests: passed (194/194)
- Omnidots tests: passed (399/399)
- Svantek tests: passed (124/124)
- Reporting tests: passed (75/75)
- Credential or connection value persisted: no

## Common-related project-reference inventory

```text
airqmonitor/AirQMonitor/AirQMonitor.csproj:16:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.Common\Rvt.Monitor.Common.csproj" />
airqmonitor/AirQMonitor/AirQMonitor.csproj:17:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.Common.Infrastructure\Rvt.Monitor.Common.Infrastructure.csproj" />
airqmonitor/AirQMonitorTests/AirQMonitorTests.csproj:26:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.IntegrationTesting\Rvt.Monitor.IntegrationTesting.csproj" />
myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj:16:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.Common\Rvt.Monitor.Common.csproj" />
myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj:17:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.Common.Infrastructure\Rvt.Monitor.Common.Infrastructure.csproj" />
myatmmonitor/MyAtmMonitorTests/MyAtmMonitorTests.csproj:26:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.IntegrationTesting\Rvt.Monitor.IntegrationTesting.csproj" />
omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj:16:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.Common\Rvt.Monitor.Common.csproj" />
omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj:17:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.Common.Infrastructure\Rvt.Monitor.Common.Infrastructure.csproj" />
omnidotsmonitor/OmnidotsMonitorTests/OmnidotsMonitorTests.csproj:25:		<ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.IntegrationTesting\Rvt.Monitor.IntegrationTesting.csproj" />
reportingmonitor/ReportingMonitor/ReportingMonitor.csproj:21:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.Common\Rvt.Monitor.Common.csproj" />
reportingmonitor/ReportingMonitor/ReportingMonitor.csproj:22:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.Common.Infrastructure\Rvt.Monitor.Common.Infrastructure.csproj" />
reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj:26:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.Common\Rvt.Monitor.Common.csproj" />
reportingmonitor/ReportingMonitorTests/ReportingMonitorTests.csproj:27:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.IntegrationTesting\Rvt.Monitor.IntegrationTesting.csproj" />
reportingmonitor/Rvt.Reporting.Messaging/Rvt.Reporting.Messaging.csproj:5:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.Common\Rvt.Monitor.Common.csproj" />
reportingmonitor/Rvt.Reporting.Storage/Rvt.Reporting.Storage.csproj:5:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.Common\Rvt.Monitor.Common.csproj" />
rvt-monitor-common/Rvt.Monitor.Common.Infrastructure/Rvt.Monitor.Common.Infrastructure.csproj:13:    <ProjectReference Include="..\Rvt.Monitor.Common\Rvt.Monitor.Common.csproj" />
rvt-monitor-common/Rvt.Monitor.Common.InfrastructureTests/Rvt.Monitor.Common.InfrastructureTests.csproj:16:    <ProjectReference Include="..\Rvt.Monitor.Common.Infrastructure\Rvt.Monitor.Common.Infrastructure.csproj" />
rvt-monitor-common/Rvt.Monitor.CommonTests/Rvt.Monitor.CommonTests.csproj:22:    <ProjectReference Include="..\Rvt.Monitor.Common\Rvt.Monitor.Common.csproj" />
rvt-monitor-common/Rvt.Monitor.IntegrationTesting.Tests/Rvt.Monitor.IntegrationTesting.Tests.csproj:16:    <ProjectReference Include="..\\Rvt.Monitor.IntegrationTesting\\Rvt.Monitor.IntegrationTesting.csproj" />
svantekmonitor/SvantekMonitor/SvantekMonitor.csproj:17:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.Common\Rvt.Monitor.Common.csproj" />
svantekmonitor/SvantekMonitor/SvantekMonitor.csproj:18:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.Common.Infrastructure\Rvt.Monitor.Common.Infrastructure.csproj" />
svantekmonitor/SvantekMonitorTests/SvantekMonitorTests.csproj:25:    <ProjectReference Include="..\..\rvt-monitor-common\Rvt.Monitor.IntegrationTesting\Rvt.Monitor.IntegrationTesting.csproj" />
```
