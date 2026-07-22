# Phase 12 MVC To SPA Parity Matrix

Date: 2026-05-24

This matrix is the cutover classification ledger for the MVC application surface. Every public MVC controller action discovered by the Phase 12 readiness test and every Razor view under `RvtDemo/Views` is listed here with a final migration decision.

Classification values:

- Migrated: function is implemented in the SPA/API path.
- Replaced: MVC-specific shell/partial/error infrastructure has an SPA/API equivalent.
- Retired: route/view is intentionally unavailable at cutover, normally because it was demo/test/debug-only.
- Deferred: item remains outside the cutover artifact by approved exception.

## Controller Actions

| Classification | MVC action | SPA/API outcome |
|---|---|---|
| Migrated | `AccountController.ConfirmEmail` | Phase 1 AuthController and account/profile React flows cover login, logout, reset, confirmation, profile, and password management. |
| Migrated | `AccountController.Index` | Phase 1 AuthController and account/profile React flows cover login, logout, reset, confirmation, profile, and password management. |
| Migrated | `AccountController.Manage` | Phase 1 AuthController and account/profile React flows cover login, logout, reset, confirmation, profile, and password management. |
| Migrated | `AccountController.Profile` | Phase 1 AuthController and account/profile React flows cover login, logout, reset, confirmation, profile, and password management. |
| Migrated | `AccountController.ResetPassword` | Phase 1 AuthController and account/profile React flows cover login, logout, reset, confirmation, profile, and password management. |
| Migrated | `AccountController.ResetPasswordConfirmation` | Phase 1 AuthController and account/profile React flows cover login, logout, reset, confirmation, profile, and password management. |
| Migrated | `AlertLevelsController.Add` | Phase 6 alert-level APIs and React monitor alert-level panels cover list/create/edit/delete/vibration thresholds. |
| Migrated | `AlertLevelsController.Delete` | Phase 6 alert-level APIs and React monitor alert-level panels cover list/create/edit/delete/vibration thresholds. |
| Migrated | `AlertLevelsController.Edit` | Phase 6 alert-level APIs and React monitor alert-level panels cover list/create/edit/delete/vibration thresholds. |
| Migrated | `AlertLevelsController.EditDust` | Phase 6 alert-level APIs and React monitor alert-level panels cover list/create/edit/delete/vibration thresholds. |
| Migrated | `AlertLevelsController.EditNoise` | Phase 6 alert-level APIs and React monitor alert-level panels cover list/create/edit/delete/vibration thresholds. |
| Migrated | `AlertLevelsController.EditVibration` | Phase 6 alert-level APIs and React monitor alert-level panels cover list/create/edit/delete/vibration thresholds. |
| Migrated | `AlertLevelsController.ListPartial` | Phase 6 alert-level APIs and React monitor alert-level panels cover list/create/edit/delete/vibration thresholds. |
| Migrated | `AlertLevelsController.PageAndSortAlertLevels` | Phase 6 alert-level APIs and React monitor alert-level panels cover list/create/edit/delete/vibration thresholds. |
| Migrated | `CalendarController.DayView` | Phase 8 dashboard calendar APIs and React calendar route cover month/day views. |
| Migrated | `CalendarController.Index` | Phase 8 dashboard calendar APIs and React calendar route cover month/day views. |
| Migrated | `CalendarController.Month` | Phase 8 dashboard calendar APIs and React calendar route cover month/day views. |
| Retired | `ClassController.Index` | Development/test utility route is not exposed in the SPA host cutover surface. |
| Migrated | `CompanyController.AddConfirmed` | Phase 3 CompaniesController and React company admin panels cover list/detail/create/edit/delete. |
| Migrated | `CompanyController.Delete` | Phase 3 CompaniesController and React company admin panels cover list/detail/create/edit/delete. |
| Migrated | `CompanyController.DeleteConfirmed` | Phase 3 CompaniesController and React company admin panels cover list/detail/create/edit/delete. |
| Migrated | `CompanyController.Edit` | Phase 3 CompaniesController and React company admin panels cover list/detail/create/edit/delete. |
| Migrated | `CompanyController.Index` | Phase 3 CompaniesController and React company admin panels cover list/detail/create/edit/delete. |
| Migrated | `CompanyController.Index2` | Phase 3 CompaniesController and React company admin panels cover list/detail/create/edit/delete. |
| Migrated | `CompanyController.PageAndSort` | Phase 3 CompaniesController and React company admin panels cover list/detail/create/edit/delete. |
| Migrated | `CompanyController.View` | Phase 3 CompaniesController and React company admin panels cover list/detail/create/edit/delete. |
| Migrated | `ContractController.Add` | Phase 4 contracts APIs and React operations panels cover list/detail/create/edit/delete. |
| Migrated | `ContractController.AddConfirmed` | Phase 4 contracts APIs and React operations panels cover list/detail/create/edit/delete. |
| Migrated | `ContractController.Delete` | Phase 4 contracts APIs and React operations panels cover list/detail/create/edit/delete. |
| Migrated | `ContractController.DeleteConfirmed` | Phase 4 contracts APIs and React operations panels cover list/detail/create/edit/delete. |
| Migrated | `ContractController.Edit` | Phase 4 contracts APIs and React operations panels cover list/detail/create/edit/delete. |
| Migrated | `ContractController.Index` | Phase 4 contracts APIs and React operations panels cover list/detail/create/edit/delete. |
| Migrated | `ContractController.Index2` | Phase 4 contracts APIs and React operations panels cover list/detail/create/edit/delete. |
| Migrated | `ContractController.PageAndSort` | Phase 4 contracts APIs and React operations panels cover list/detail/create/edit/delete. |
| Migrated | `ContractController.View` | Phase 4 contracts APIs and React operations panels cover list/detail/create/edit/delete. |
| Migrated | `DataApiController.CompaniesSearch` | Phase 2 lookup API and module-specific option endpoints replace MVC autocomplete/select JSON actions. |
| Migrated | `DataApiController.ContractsForCompanySelect` | Phase 2 lookup API and module-specific option endpoints replace MVC autocomplete/select JSON actions. |
| Migrated | `DataApiController.ContractsSearch` | Phase 2 lookup API and module-specific option endpoints replace MVC autocomplete/select JSON actions. |
| Migrated | `DataApiController.MonitorAvaialbleSearch` | Phase 2 lookup API and module-specific option endpoints replace MVC autocomplete/select JSON actions. |
| Migrated | `DataApiController.MonitorSearch` | Phase 2 lookup API and module-specific option endpoints replace MVC autocomplete/select JSON actions. |
| Migrated | `DataApiController.MonitorsNewSearch` | Phase 2 lookup API and module-specific option endpoints replace MVC autocomplete/select JSON actions. |
| Migrated | `DataApiController.MonitorsNotDeployed` | Phase 2 lookup API and module-specific option endpoints replace MVC autocomplete/select JSON actions. |
| Migrated | `DataApiController.MonitorsOfflineSearch` | Phase 2 lookup API and module-specific option endpoints replace MVC autocomplete/select JSON actions. |
| Migrated | `DataApiController.MonitorsOnlineSearch` | Phase 2 lookup API and module-specific option endpoints replace MVC autocomplete/select JSON actions. |
| Migrated | `DataApiController.SitesForCompanySelect` | Phase 2 lookup API and module-specific option endpoints replace MVC autocomplete/select JSON actions. |
| Migrated | `DataApiController.SitesSearch` | Phase 2 lookup API and module-specific option endpoints replace MVC autocomplete/select JSON actions. |
| Migrated | `DataApiController.UserSearch` | Phase 2 lookup API and module-specific option endpoints replace MVC autocomplete/select JSON actions. |
| Migrated | `DataApiController.UsersForCompany` | Phase 2 lookup API and module-specific option endpoints replace MVC autocomplete/select JSON actions. |
| Retired | `DemoController.AddSite` | Demo-only MVC route is folded into migrated dashboard/site/monitor/installer/data pages or returns safe 404 from the SPA host. |
| Retired | `DemoController.Alert` | Demo-only MVC route is folded into migrated dashboard/site/monitor/installer/data pages or returns safe 404 from the SPA host. |
| Retired | `DemoController.Dash` | Demo-only MVC route is folded into migrated dashboard/site/monitor/installer/data pages or returns safe 404 from the SPA host. |
| Retired | `DemoController.DashCust` | Demo-only MVC route is folded into migrated dashboard/site/monitor/installer/data pages or returns safe 404 from the SPA host. |
| Retired | `DemoController.Index` | Demo-only MVC route is folded into migrated dashboard/site/monitor/installer/data pages or returns safe 404 from the SPA host. |
| Retired | `DemoController.InstallSearch` | Demo-only MVC route is folded into migrated dashboard/site/monitor/installer/data pages or returns safe 404 from the SPA host. |
| Retired | `DemoController.InstallStatus` | Demo-only MVC route is folded into migrated dashboard/site/monitor/installer/data pages or returns safe 404 from the SPA host. |
| Retired | `DemoController.MonitorDust` | Demo-only MVC route is folded into migrated dashboard/site/monitor/installer/data pages or returns safe 404 from the SPA host. |
| Retired | `DemoController.Monitors` | Demo-only MVC route is folded into migrated dashboard/site/monitor/installer/data pages or returns safe 404 from the SPA host. |
| Retired | `DemoController.NewMonitors` | Demo-only MVC route is folded into migrated dashboard/site/monitor/installer/data pages or returns safe 404 from the SPA host. |
| Retired | `DemoController.Site` | Demo-only MVC route is folded into migrated dashboard/site/monitor/installer/data pages or returns safe 404 from the SPA host. |
| Retired | `DemoController.Sites` | Demo-only MVC route is folded into migrated dashboard/site/monitor/installer/data pages or returns safe 404 from the SPA host. |
| Replaced | `ErrorController.Index` | Replaced by `/error`, API ProblemDetails, SPA not-found screens, and AppErrorBoundary. |
| Migrated | `HomeController.Error` | Role dashboards, installer monitor list, paging, breaches, maps, and alerts are implemented in Phase 8 SPA/API routes. |
| Migrated | `HomeController.Index` | Role dashboards, installer monitor list, paging, breaches, maps, and alerts are implemented in Phase 8 SPA/API routes. |
| Migrated | `HomeController.IndexInstaller` | Role dashboards, installer monitor list, paging, breaches, maps, and alerts are implemented in Phase 8 SPA/API routes. |
| Migrated | `HomeController.PageAndSort` | Role dashboards, installer monitor list, paging, breaches, maps, and alerts are implemented in Phase 8 SPA/API routes. |
| Migrated | `HomeController.PageAndSortInstallerMonitor` | Role dashboards, installer monitor list, paging, breaches, maps, and alerts are implemented in Phase 8 SPA/API routes. |
| Migrated | `HomeController.Privacy` | Public `/privacy` React route retains the required static content. |
| Retired | `HomeController.Reset` | Legacy debug/reset utility route returns safe 404 from the SPA host. |
| Retired | `HomeController.exception` | Legacy debug/reset utility route returns safe 404 from the SPA host. |
| Migrated | `InstallerController.Convert` | Phase 5 InstallerApiController and React installer panels cover deployment edit, status, and what3words conversion. |
| Migrated | `InstallerController.Edit` | Phase 5 InstallerApiController and React installer panels cover deployment edit, status, and what3words conversion. |
| Migrated | `InstallerController.Index` | Phase 5 InstallerApiController and React installer panels cover deployment edit, status, and what3words conversion. |
| Migrated | `InstallerController.MonitorStatus` | Phase 5 InstallerApiController and React installer panels cover deployment edit, status, and what3words conversion. |
| Migrated | `LoginController.ForgotPassword` | Phase 1 AuthController and account/profile React flows cover login, logout, reset, confirmation, profile, and password management. |
| Migrated | `LoginController.ForgotPasswordConfirmation` | Phase 1 AuthController and account/profile React flows cover login, logout, reset, confirmation, profile, and password management. |
| Migrated | `LoginController.Index` | Phase 1 AuthController and account/profile React flows cover login, logout, reset, confirmation, profile, and password management. |
| Migrated | `LoginController.Index2` | Phase 1 AuthController and account/profile React flows cover login, logout, reset, confirmation, profile, and password management. |
| Migrated | `LoginController.Logout` | Phase 1 AuthController and account/profile React flows cover login, logout, reset, confirmation, profile, and password management. |
| Migrated | `MapController.MapForSite` | Phase 8 dashboard map-marker API and React `/maps` route cover user/site map behavior. |
| Migrated | `MapController.MapForUser` | Phase 8 dashboard map-marker API and React `/maps` route cover user/site map behavior. |
| Migrated | `MasterAdminController.GetBreachesAndAlertsData` | Phase 8 dashboard summary and breaches/alerts APIs cover master-admin dashboard data. |
| Migrated | `MasterAdminController.Index` | Phase 8 dashboard summary and breaches/alerts APIs cover master-admin dashboard data. |
| Migrated | `MonitorController.AddFleetNr` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.AddToContract` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.ContractAssigned` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.DefaultMonitors` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.Edit` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.Graph` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.GraphView` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.Index` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.Index2` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.NewList` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.NotInUse` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.OffLine` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.OnLine` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.PageAndSort` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.PageAndSortEdit` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.PageAndSortNewList` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.PageAndSortNotInUse` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.PageAndSortOffline` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.PageAndSortOnLine` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.RemoveFromContract` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.SiteEdit` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.Traces` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.View` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `MonitorController.ViewDeploy` | Phase 5 monitor/installer APIs and Phase 9 graph/trace/data APIs cover monitor inventory, deployment, assignment, graph, trace, and default monitor flows. |
| Migrated | `NotificationController.BatchClose` | Phase 6 notification APIs and React panels cover open/caution/all lists, detail, close, and batch close. |
| Migrated | `NotificationController.Cautions` | Phase 6 notification APIs and React panels cover open/caution/all lists, detail, close, and batch close. |
| Migrated | `NotificationController.CloseNote` | Phase 6 notification APIs and React panels cover open/caution/all lists, detail, close, and batch close. |
| Migrated | `NotificationController.Index` | Phase 6 notification APIs and React panels cover open/caution/all lists, detail, close, and batch close. |
| Migrated | `NotificationController.OpenNotifications` | Phase 6 notification APIs and React panels cover open/caution/all lists, detail, close, and batch close. |
| Migrated | `NotificationController.PageAndSortAllNotifications` | Phase 6 notification APIs and React panels cover open/caution/all lists, detail, close, and batch close. |
| Migrated | `NotificationController.PageAndSortCautions` | Phase 6 notification APIs and React panels cover open/caution/all lists, detail, close, and batch close. |
| Migrated | `NotificationController.PageAndSortOpenNotifications` | Phase 6 notification APIs and React panels cover open/caution/all lists, detail, close, and batch close. |
| Migrated | `NotificationController.SearchCautions` | Phase 6 notification APIs and React panels cover open/caution/all lists, detail, close, and batch close. |
| Migrated | `NotificationController.SearchOpenNotificationSite` | Phase 6 notification APIs and React panels cover open/caution/all lists, detail, close, and batch close. |
| Migrated | `NotificationController.SearchOpenNotifications` | Phase 6 notification APIs and React panels cover open/caution/all lists, detail, close, and batch close. |
| Migrated | `NotificationController.View` | Phase 6 notification APIs and React panels cover open/caution/all lists, detail, close, and batch close. |
| Migrated | `NotificationController._CloseNoteMany` | Phase 6 notification APIs and React panels cover open/caution/all lists, detail, close, and batch close. |
| Migrated | `ReportController.AddUser` | Phase 7 report/report-rule APIs and React panels cover generated reports, rule CRUD, DB-side paging, manual generation request acceptance, and paged recipient grids. |
| Migrated | `ReportController.DeleteRule` | Phase 7 report/report-rule APIs and React panels cover generated reports, rule CRUD, DB-side paging, manual generation request acceptance, and paged recipient grids. |
| Migrated | `ReportController.EditRule` | Phase 7 report/report-rule APIs and React panels cover generated reports, rule CRUD, DB-side paging, manual generation request acceptance, and paged recipient grids. |
| Migrated | `ReportController.Index` | Phase 7 report/report-rule APIs and React panels cover generated reports, rule CRUD, DB-side paging, manual generation request acceptance, and paged recipient grids. |
| Migrated | `ReportController.PageAndSort` | Phase 7 report/report-rule APIs and React panels cover generated reports, rule CRUD, DB-side paging, manual generation request acceptance, and paged recipient grids. |
| Migrated | `ReportController.PageAndSortRules` | Phase 7 report/report-rule APIs and React panels cover generated reports, rule CRUD, DB-side paging, manual generation request acceptance, and paged recipient grids. |
| Migrated | `ReportController.PageAndSortUsers` | Phase 7 report/report-rule APIs and React panels cover generated reports, rule CRUD, DB-side paging, manual generation request acceptance, and paged recipient grids. |
| Migrated | `ReportController.RemoveUser` | Phase 7 report/report-rule APIs and React panels cover generated reports, rule CRUD, DB-side paging, manual generation request acceptance, and paged recipient grids. |
| Migrated | `ReportController.Rules` | Phase 7 report/report-rule APIs and React panels cover generated reports, rule CRUD, DB-side paging, manual generation request acceptance, and paged recipient grids. |
| Migrated | `ReportController.Users` | Phase 7 report/report-rule APIs and React panels cover generated reports, rule CRUD, DB-side paging, manual generation request acceptance, and paged recipient grids. |
| Migrated | `ReportController.UsersAssigned` | Phase 7 report/report-rule APIs and React panels cover generated reports, rule CRUD, DB-side paging, manual generation request acceptance, and paged recipient grids. |
| Migrated | `SiteController.Add` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Migrated | `SiteController.Archive` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Migrated | `SiteController.DataGrid` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Migrated | `SiteController.DataView` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Migrated | `SiteController.DownloadDataGrid` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Migrated | `SiteController.Edit` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Migrated | `SiteController.Index` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Migrated | `SiteController.Index2` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Migrated | `SiteController.NotificationSettings` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Migrated | `SiteController.PageAndSort` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Migrated | `SiteController.PageAndSortDataGrid` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Migrated | `SiteController.PageAndSortMonitors` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Migrated | `SiteController.PageAndSortNS` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Migrated | `SiteController.PageAndSortOpenNotifications` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Migrated | `SiteController.SearchOpenNotifications` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Migrated | `SiteController.UpdateSiteNotification` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Migrated | `SiteController.View` | Phase 4 site APIs and Phase 9 data APIs cover site CRUD/archive/detail/notifications/data/download settings. |
| Retired | `TestController.Blob` | Development/test utility route is not exposed in the SPA host cutover surface. |
| Retired | `TestController.Exception` | Development/test utility route is not exposed in the SPA host cutover surface. |
| Retired | `TestController.Index` | Development/test utility route is not exposed in the SPA host cutover surface. |
| Retired | `TestController.MsgTest` | Development/test utility route is not exposed in the SPA host cutover surface. |
| Retired | `TestController.SendTest` | Development/test utility route is not exposed in the SPA host cutover surface. |
| Retired | `TestController.SendTestAjax` | Development/test utility route is not exposed in the SPA host cutover surface. |
| Migrated | `UserController.AddSiteUser` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.AddToSite` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.AddUserConfirmed` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.Delete` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.DeleteConfirmed` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.Disable` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.DisableConfirmed` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.EditUser` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.Enable` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.EnableConfirmed` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.Index` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.Index2` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.IndexComp` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.PageAndSort` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.PageAndSortEdit` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.ReSendLink` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.ReSendLinkAjax` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.RemoveFromSite` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.RemoveSiteContactUser` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.ResetPasswordLink` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.ResetPasswordLinkAjax` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.SetSiteContactUser` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.SiteAssigned` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.SiteEdit` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.SiteEdit2` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |
| Migrated | `UserController.ViewUser` | Phase 3 user admin APIs and React panels cover list/detail/edit/invite/reset/status/delete/site assignment. |

## Razor Views

| Classification | MVC view | SPA/API outcome |
|---|---|---|
| Migrated | `RvtDemo/Views/Account/ConfirmEmail.cshtml` | Migrated to Phase 1 auth/account React flows and AuthController endpoints. |
| Migrated | `RvtDemo/Views/Account/Manage.cshtml` | Migrated to Phase 1 auth/account React flows and AuthController endpoints. |
| Migrated | `RvtDemo/Views/Account/Profile.cshtml` | Migrated to Phase 1 auth/account React flows and AuthController endpoints. |
| Migrated | `RvtDemo/Views/Account/ResetPassword.cshtml` | Migrated to Phase 1 auth/account React flows and AuthController endpoints. |
| Migrated | `RvtDemo/Views/Account/ResetPasswordConfirmation.cshtml` | Migrated to Phase 1 auth/account React flows and AuthController endpoints. |
| Migrated | `RvtDemo/Views/Account/_Manage.cshtml` | Migrated to Phase 1 auth/account React flows and AuthController endpoints. |
| Migrated | `RvtDemo/Views/AlertLevels/EditDust.cshtml` | Migrated to Phase 6 alert-level React panels. |
| Migrated | `RvtDemo/Views/AlertLevels/EditNoise.cshtml` | Migrated to Phase 6 alert-level React panels. |
| Migrated | `RvtDemo/Views/AlertLevels/EditVibration.cshtml` | Migrated to Phase 6 alert-level React panels. |
| Migrated | `RvtDemo/Views/AlertLevels/Index.cshtml` | Migrated to Phase 6 alert-level React panels. |
| Migrated | `RvtDemo/Views/AlertLevels/ViewVibration.cshtml` | Migrated to Phase 6 alert-level React panels. |
| Migrated | `RvtDemo/Views/AlertLevels/_PartialLevelList.cshtml` | Migrated to Phase 6 alert-level React panels. |
| Migrated | `RvtDemo/Views/AlertLevels/_list.cshtml` | Migrated to Phase 6 alert-level React panels. |
| Migrated | `RvtDemo/Views/Calendar/Index.cshtml` | Migrated to Phase 8 calendar React route. |
| Migrated | `RvtDemo/Views/Calendar/_DayView.cshtml` | Migrated to Phase 8 calendar React route. |
| Migrated | `RvtDemo/Views/Calendar/_Month.cshtml` | Migrated to Phase 8 calendar React route. |
| Migrated | `RvtDemo/Views/Company/Delete.cshtml` | Migrated to Phase 3 company admin React panels. |
| Migrated | `RvtDemo/Views/Company/Edit.cshtml` | Migrated to Phase 3 company admin React panels. |
| Migrated | `RvtDemo/Views/Company/Index.cshtml` | Migrated to Phase 3 company admin React panels. |
| Migrated | `RvtDemo/Views/Company/View.cshtml` | Migrated to Phase 3 company admin React panels. |
| Migrated | `RvtDemo/Views/Company/_Add.cshtml` | Migrated to Phase 3 company admin React panels. |
| Migrated | `RvtDemo/Views/Company/_PartialCompanyList.cshtml` | Migrated to Phase 3 company admin React panels. |
| Migrated | `RvtDemo/Views/Contract/Add.cshtml` | Migrated to Phase 4 contract React panels. |
| Migrated | `RvtDemo/Views/Contract/Delete.cshtml` | Migrated to Phase 4 contract React panels. |
| Migrated | `RvtDemo/Views/Contract/Edit.cshtml` | Migrated to Phase 4 contract React panels. |
| Migrated | `RvtDemo/Views/Contract/Index.cshtml` | Migrated to Phase 4 contract React panels. |
| Migrated | `RvtDemo/Views/Contract/View.cshtml` | Migrated to Phase 4 contract React panels. |
| Migrated | `RvtDemo/Views/Contract/_Add.cshtml` | Migrated to Phase 4 contract React panels. |
| Migrated | `RvtDemo/Views/Contract/_PartialContractList.cshtml` | Migrated to Phase 4 contract React panels. |
| Retired | `RvtDemo/Views/Demo/AddSite.cshtml` | Demo/test view not part of production cutover; corresponding routes are safely retired. |
| Retired | `RvtDemo/Views/Demo/Alert.cshtml` | Demo/test view not part of production cutover; corresponding routes are safely retired. |
| Retired | `RvtDemo/Views/Demo/Dash.cshtml` | Demo/test view not part of production cutover; corresponding routes are safely retired. |
| Retired | `RvtDemo/Views/Demo/DashCust.cshtml` | Demo/test view not part of production cutover; corresponding routes are safely retired. |
| Retired | `RvtDemo/Views/Demo/InstallSearch.cshtml` | Demo/test view not part of production cutover; corresponding routes are safely retired. |
| Retired | `RvtDemo/Views/Demo/InstallStatus.cshtml` | Demo/test view not part of production cutover; corresponding routes are safely retired. |
| Retired | `RvtDemo/Views/Demo/MonitorDust.cshtml` | Demo/test view not part of production cutover; corresponding routes are safely retired. |
| Retired | `RvtDemo/Views/Demo/Monitors.cshtml` | Demo/test view not part of production cutover; corresponding routes are safely retired. |
| Retired | `RvtDemo/Views/Demo/NewMonitors.cshtml` | Demo/test view not part of production cutover; corresponding routes are safely retired. |
| Retired | `RvtDemo/Views/Demo/Site.cshtml` | Demo/test view not part of production cutover; corresponding routes are safely retired. |
| Retired | `RvtDemo/Views/Demo/Sites.cshtml` | Demo/test view not part of production cutover; corresponding routes are safely retired. |
| Replaced | `RvtDemo/Views/Error/Index.cshtml` | Replaced by `/error`, SPA not-found, and `AppErrorBoundary`. |
| Migrated | `RvtDemo/Views/Home/Index.cshtml` | Dashboard and role-specific home content migrated to Phase 8 React dashboard routes. |
| Migrated | `RvtDemo/Views/Home/IndexAdmin.cshtml` | Dashboard and role-specific home content migrated to Phase 8 React dashboard routes. |
| Migrated | `RvtDemo/Views/Home/IndexInstaller.cshtml` | Dashboard and role-specific home content migrated to Phase 8 React dashboard routes. |
| Migrated | `RvtDemo/Views/Home/Privacy.cshtml` | Migrated to public `/privacy` React route. |
| Migrated | `RvtDemo/Views/Installer/_checkStatusPartial.cshtml` | Migrated to Phase 5 installer React panels. |
| Migrated | `RvtDemo/Views/Installer/_editPartial.cshtml` | Migrated to Phase 5 installer React panels. |
| Migrated | `RvtDemo/Views/Installer/edit.cshtml` | Migrated to Phase 5 installer React panels. |
| Migrated | `RvtDemo/Views/Login/ForgotPasswordConfirmation.cshtml` | Migrated to Phase 1 auth/account React flows and AuthController endpoints. |
| Migrated | `RvtDemo/Views/Login/Index.cshtml` | Migrated to Phase 1 auth/account React flows and AuthController endpoints. |
| Migrated | `RvtDemo/Views/Login/forgotpassword.cshtml` | Migrated to Phase 1 auth/account React flows and AuthController endpoints. |
| Migrated | `RvtDemo/Views/MasterAdmin/Index.cshtml` | Migrated to Phase 8 dashboard breach/alert panels. |
| Migrated | `RvtDemo/Views/MasterAdmin/_BreachesAlertsPartial.cshtml` | Migrated to Phase 8 dashboard breach/alert panels. |
| Migrated | `RvtDemo/Views/Monitor/Edit.cshtml` | Migrated to Phase 5 monitor React panels and Phase 9 graph/trace/data views. |
| Migrated | `RvtDemo/Views/Monitor/Index.cshtml` | Migrated to Phase 5 monitor React panels and Phase 9 graph/trace/data views. |
| Migrated | `RvtDemo/Views/Monitor/NewList.cshtml` | Migrated to Phase 5 monitor React panels and Phase 9 graph/trace/data views. |
| Migrated | `RvtDemo/Views/Monitor/NotInUse.cshtml` | Migrated to Phase 5 monitor React panels and Phase 9 graph/trace/data views. |
| Migrated | `RvtDemo/Views/Monitor/OffLine.cshtml` | Migrated to Phase 5 monitor React panels and Phase 9 graph/trace/data views. |
| Migrated | `RvtDemo/Views/Monitor/OnLine.cshtml` | Migrated to Phase 5 monitor React panels and Phase 9 graph/trace/data views. |
| Migrated | `RvtDemo/Views/Monitor/SiteEdit.cshtml` | Migrated to Phase 5 monitor React panels and Phase 9 graph/trace/data views. |
| Migrated | `RvtDemo/Views/Monitor/View.cshtml` | Migrated to Phase 5 monitor React panels and Phase 9 graph/trace/data views. |
| Migrated | `RvtDemo/Views/Monitor/_AddFleetNr.cshtml` | Migrated to Phase 5 monitor React panels and Phase 9 graph/trace/data views. |
| Migrated | `RvtDemo/Views/Monitor/_Notes.cshtml` | Migrated to Phase 5 monitor React panels and Phase 9 graph/trace/data views. |
| Migrated | `RvtDemo/Views/Monitor/_PartialMonitorAssigendList.cshtml` | Migrated to Phase 5 monitor React panels and Phase 9 graph/trace/data views. |
| Migrated | `RvtDemo/Views/Monitor/_PartialMonitorEditList.cshtml` | Migrated to Phase 5 monitor React panels and Phase 9 graph/trace/data views. |
| Migrated | `RvtDemo/Views/Monitor/_PartialMonitorNewList.cshtml` | Migrated to Phase 5 monitor React panels and Phase 9 graph/trace/data views. |
| Migrated | `RvtDemo/Views/Monitor/_PartialMonitorNotAssignedList.cshtml` | Migrated to Phase 5 monitor React panels and Phase 9 graph/trace/data views. |
| Migrated | `RvtDemo/Views/Monitor/_Traces.cshtml` | Migrated to Phase 5 monitor React panels and Phase 9 graph/trace/data views. |
| Migrated | `RvtDemo/Views/Notification/Cautions.cshtml` | Migrated to Phase 6 notification React panels. |
| Migrated | `RvtDemo/Views/Notification/OpenNotifications.cshtml` | Migrated to Phase 6 notification React panels. |
| Migrated | `RvtDemo/Views/Notification/View.cshtml` | Migrated to Phase 6 notification React panels. |
| Migrated | `RvtDemo/Views/Notification/_CloseNote.cshtml` | Migrated to Phase 6 notification React panels. |
| Migrated | `RvtDemo/Views/Notification/_CloseNoteMany.cshtml` | Migrated to Phase 6 notification React panels. |
| Migrated | `RvtDemo/Views/Notification/_PartialNotificationsList.cshtml` | Migrated to Phase 6 notification React panels. |
| Migrated | `RvtDemo/Views/Report/EditRule.cshtml` | Migrated to Phase 7 report React panels; upgraded with guided setup steps, manual generation request action, and paged recipient grids. |
| Migrated | `RvtDemo/Views/Report/Index.cshtml` | Migrated to Phase 7 report React panels; upgraded with guided setup steps, manual generation request action, and paged recipient grids. |
| Migrated | `RvtDemo/Views/Report/Rules.cshtml` | Migrated to Phase 7 report React panels; upgraded with guided setup steps, manual generation request action, and paged recipient grids. |
| Migrated | `RvtDemo/Views/Report/Users.cshtml` | Migrated to Phase 7 report React panels; upgraded with guided setup steps, manual generation request action, and paged recipient grids. |
| Migrated | `RvtDemo/Views/Report/_PartialReportRulesList.cshtml` | Migrated to Phase 7 report React panels; upgraded with guided setup steps, manual generation request action, and paged recipient grids. |
| Migrated | `RvtDemo/Views/Report/_PartialReportUserAssignedList.cshtml` | Migrated to Phase 7 report React panels; upgraded with guided setup steps, manual generation request action, and paged recipient grids. |
| Migrated | `RvtDemo/Views/Report/_PartialReportUserEditList.cshtml` | Migrated to Phase 7 report React panels; upgraded with guided setup steps, manual generation request action, and paged recipient grids. |
| Migrated | `RvtDemo/Views/Report/_PartialReportsList.cshtml` | Migrated to Phase 7 report React panels; upgraded with guided setup steps, manual generation request action, and paged recipient grids. |
| Replaced | `RvtDemo/Views/Shared/Error.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Replaced | `RvtDemo/Views/Shared/Monitor/_AlertLevels.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Replaced | `RvtDemo/Views/Shared/Monitor/_MonDetails.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Replaced | `RvtDemo/Views/Shared/Monitor/_PartialMonitorList.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Replaced | `RvtDemo/Views/Shared/Notification/_PartialNotificationsList.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Replaced | `RvtDemo/Views/Shared/Site/_PartialSiteList.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Replaced | `RvtDemo/Views/Shared/Site/_PartialSiteListNS.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Replaced | `RvtDemo/Views/Shared/_Layout.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Replaced | `RvtDemo/Views/Shared/_LoginLayout.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Replaced | `RvtDemo/Views/Shared/_LoginPartial.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Replaced | `RvtDemo/Views/Shared/_PartialData.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Replaced | `RvtDemo/Views/Shared/_PartialDataGrid.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Replaced | `RvtDemo/Views/Shared/_PartialGraph.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Replaced | `RvtDemo/Views/Shared/_PartialMap.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Replaced | `RvtDemo/Views/Shared/_TopNavBar.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Replaced | `RvtDemo/Views/Shared/_ValidationScriptsPartial.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Migrated | `RvtDemo/Views/Site/Add.cshtml` | Migrated to Phase 4 site React panels and Phase 9 data views. |
| Migrated | `RvtDemo/Views/Site/Archive.cshtml` | Migrated to Phase 4 site React panels and Phase 9 data views. |
| Migrated | `RvtDemo/Views/Site/Archived.cshtml` | Migrated to Phase 4 site React panels and Phase 9 data views. |
| Migrated | `RvtDemo/Views/Site/Delete.cshtml` | Migrated to Phase 4 site React panels and Phase 9 data views. |
| Migrated | `RvtDemo/Views/Site/Edit.cshtml` | Migrated to Phase 4 site React panels and Phase 9 data views. |
| Migrated | `RvtDemo/Views/Site/Index.cshtml` | Migrated to Phase 4 site React panels and Phase 9 data views. |
| Migrated | `RvtDemo/Views/Site/NotificationSettings.cshtml` | Migrated to Phase 4 site React panels and Phase 9 data views. |
| Migrated | `RvtDemo/Views/Site/View.cshtml` | Migrated to Phase 4 site React panels and Phase 9 data views. |
| Retired | `RvtDemo/Views/Test/Blob.cshtml` | Demo/test view not part of production cutover; corresponding routes are safely retired. |
| Retired | `RvtDemo/Views/Test/HTML.cshtml` | Demo/test view not part of production cutover; corresponding routes are safely retired. |
| Retired | `RvtDemo/Views/Test/SendTest.cshtml` | Demo/test view not part of production cutover; corresponding routes are safely retired. |
| Retired | `RvtDemo/Views/Test/Status.cshtml` | Demo/test view not part of production cutover; corresponding routes are safely retired. |
| Migrated | `RvtDemo/Views/User/AddUserConfirmed.cshtml` | Migrated to Phase 3 user admin and assignment React panels. |
| Migrated | `RvtDemo/Views/User/Delete.cshtml` | Migrated to Phase 3 user admin and assignment React panels. |
| Migrated | `RvtDemo/Views/User/Disable.cshtml` | Migrated to Phase 3 user admin and assignment React panels. |
| Migrated | `RvtDemo/Views/User/EditUser.cshtml` | Migrated to Phase 3 user admin and assignment React panels. |
| Migrated | `RvtDemo/Views/User/Enable.cshtml` | Migrated to Phase 3 user admin and assignment React panels. |
| Migrated | `RvtDemo/Views/User/Index.cshtml` | Migrated to Phase 3 user admin and assignment React panels. |
| Migrated | `RvtDemo/Views/User/ReSendLink.cshtml` | Migrated to Phase 3 user admin and assignment React panels. |
| Migrated | `RvtDemo/Views/User/ResetPasswordLink.cshtml` | Migrated to Phase 3 user admin and assignment React panels. |
| Migrated | `RvtDemo/Views/User/SiteEdit.cshtml` | Migrated to Phase 3 user admin and assignment React panels. |
| Migrated | `RvtDemo/Views/User/ViewUser.cshtml` | Migrated to Phase 3 user admin and assignment React panels. |
| Migrated | `RvtDemo/Views/User/_PartialUserAssigendList.cshtml` | Migrated to Phase 3 user admin and assignment React panels. |
| Migrated | `RvtDemo/Views/User/_PartialUserEditList.cshtml` | Migrated to Phase 3 user admin and assignment React panels. |
| Migrated | `RvtDemo/Views/User/_PartialUserList.cshtml` | Migrated to Phase 3 user admin and assignment React panels. |
| Replaced | `RvtDemo/Views/_ViewImports.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |
| Replaced | `RvtDemo/Views/_ViewStart.cshtml` | MVC layout/partial infrastructure replaced by React shell, shared components, API ProblemDetails, and SPA routing. |

## Cutover Summary

| Classification | Count | Notes |
|---|---:|---|
| Migrated actions | 158 | Covered by Phases 1-11 and verified again in Phase 12. |
| Migrated or replaced views | 110 | Covered by Phases 1-11 and verified again in Phase 12. |
| Retired demo/test/debug surface | 36 | Retired routes are intentionally blocked by the SPA host or omitted from production navigation. |
| Deferred | 0 | No MVC action or view is deferred at Phase 12 closure. |
