
Scaffold-DbContext 'Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;Persist Security Info=False;User ID=<user>;Password=<value>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;' Microsoft.EntityFrameworkCore.SqlServer -ContextDir Context -Context RVTSearchContext -OutputDir EntityModels/Models -f -table UserSearch, CompanySearch, SiteSearch, SiteUserSearch, ContractSearch, MonitorSearch, MonitorUserSearch,UsersForSiteSearch, MyAtmDustLevels, AdminDashboardData,MyAtmDustLevel8hourAvg, AirQnoiseLevels, AirQnoiseLevel1hourAvg, AirQnoiseLevel1dayAvg, CustomerDashboardMonitorData, CustomerDashboardNotificationData, OmnidotsPeakLevels

Scaffold-DbContext 'Server=<local-server>;Database=<database>;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True' Microsoft.EntityFrameworkCore.SqlServer -ContextDir Context -Context RVTSearchContext -OutputDir EntityModels/Models -f -table AdminDashboardData, CompanySearch, ContractSearch, CustomerDashboardMonitorData, CustomerDashboardNotificationData, MonitorCurrentSearch, MonitorSearch, MonitorUserSearch, MyAtmDustLevels, MyAtmDustLevel8hourAvg, NoiseLevel15minAvg, NoiseLevel1dayAvg, NoiseLevel1hourAvg, NoiseLevelSiteAvg, NotificationSearch, NotificationUserSearch, OmnidotsMonitorStatus, OmnidotsPeakLevels, OmnidotsPeakLevel1dayPeak, OmnidotsSensors, OmnidotsTraces, OmnidotsTracesIndex, ReportRules, ReportRuleSearch, ReportRuleUserSearch, ReportSearch, ReportUsers, ReportUserSearch, SiteSearch, SiteUserSearch, SvantekMonitorStatus, UserSearch, UsersForReportSearch, UsersForSiteSearch,OmnidotsPeakLevel20min,OmnidotsPeakLevel15min,OmnidotsPeakLevel5min,OmnidotsPeakLevel1min


         if (!optionsBuilder.IsConfigured)
        {
            IConfigurationBuilder configurationBuilder = new ConfigurationBuilder().SetBasePath(Environment.CurrentDirectory).AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var configurationRoot = configurationBuilder.Build();
            optionsBuilder.UseSqlServer(configurationRoot.GetConnectionString("DefaultConnection"));
        }

 
