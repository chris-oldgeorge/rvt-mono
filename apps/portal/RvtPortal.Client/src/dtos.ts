// File summary: Exposes SPA API DTO types from the generated OpenAPI schema.
// Major updates:
// - 2026-06-26 pending Replaced hand-maintained API contract bodies with schema-derived aliases.
// - 2026-06-26 pending Added local schema-gap extensions for Help CMS, report guidelines, and newer detail fields.
// - 2026-06-26 pending Added disabled option metadata for unavailable select choices.
// - 2026-06-24 pending Added report generation, paged report-recipient, and report guideline DTOs.
// - 2026-06-10 pending Added Help CMS admin management DTOs.
// - 2026-06-08 pending Added unattached monitor removal DTOs.
// - 2026-06-09 pending Added legacy monitor-detail summary and picture-upload DTOs.
// - 2026-06-09 pending Added site monitor map coordinate DTO fields.
// - 2026-06-24 pending Added site customer-logo links for report branding.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import type { components } from './api/schema';

type Schema = components['schemas'];
type NonNullablePart<T> = Exclude<T, null | undefined>;
type ApiSchemaObject<T> =
  T extends readonly (infer Item)[]
    ? ApiSchemaObject<Item>[]
    : T extends Record<string, unknown>
      ? { [Key in keyof T]-?: ApiSchemaValue<T[Key]> }
      : NonNullablePart<T>;
type ApiSchemaValue<T> =
  NonNullablePart<T> extends readonly (infer Item)[]
    ? ApiSchemaObject<Item>[]
    : NonNullablePart<T> extends Record<string, unknown>
      ? ApiSchemaObject<NonNullablePart<T>>
      : NonNullablePart<T>;

export type ApiSchema<TKey extends keyof Schema> = ApiSchemaObject<Schema[TKey]>;
export type SortDirection = 'Ascending' | 'Descending';

export type PagedResponse<T> = {
  results: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
  searchText: string | null;
  sort: string | null;
  sortDir: SortDirection | string | null;
};

export type GetHealthResponse = ApiSchema<'GetHealthResponse'>;
export type SearchLookupResponse = ApiSchema<'SearchLookupResponse'>;
export type CompanyListItem = ApiSchema<'CompanyListItem'>;
export type CompanyDetailResponse = ApiSchema<'CompanyDetailResponse'>;
export type CompanyMutationRequest = ApiSchema<'CompanyMutationRequest'>;
export type EntityResponse<T> = {
  item?: T | null;
};
export type MutationResponse = ApiSchema<'MutationResponse'>;
export type OptionItem = ApiSchema<'OptionItem'>;
export type UserListItem = ApiSchema<'UserListItem'> & { id: string; email: string; role: string };
export type UserDetailResponse = ApiSchema<'UserDetailResponse'> & {
  id: string;
  email: string;
  role: string;
  availableRoles: OptionItem[];
  companies: OptionItem[];
};
export type UserMutationRequest = Omit<ApiSchema<'UserMutationRequest'>, 'name' | 'mobilePhone' | 'companyId' | 'companyRole'> & {
  name?: string | null;
  mobilePhone?: string | null;
  companyId?: string | null;
  companyRole?: string | null;
};
export type SiteUserAssignmentItem = ApiSchema<'SiteUserAssignmentItem'> & { id: string; email: string; role: string };
export type SiteAssignmentResponse = ApiSchema<'SiteAssignmentResponse'> & {
  availableUsers: UserListItem[];
  assignedUsers: SiteUserAssignmentItem[];
};
export type SiteUserMutationRequest = ApiSchema<'SiteUserMutationRequest'>;
export type AuthUser = ApiSchema<'AuthUserResponse'> & { roles: string[] };
export type AuthStateResponse = Omit<ApiSchema<'AuthStateResponse'>, 'isAuthenticated' | 'user'> & {
  isAuthenticated: boolean;
  user?: AuthUser | null;
};
export type LoginRequest = ApiSchema<'LoginRequest'>;
export type ForgotPasswordRequest = ApiSchema<'ForgotPasswordRequest'>;
export type ResetPasswordRequest = ApiSchema<'ResetPasswordRequest'>;
export type ConfirmEmailResponse = ApiSchema<'ConfirmEmailResponse'>;
export type SetInitialPasswordRequest = ApiSchema<'SetInitialPasswordRequest'>;
export type ChangePasswordRequest = ApiSchema<'ChangePasswordRequest'>;
export type ProfileResponse = ApiSchema<'ProfileResponse'>;
export type UpdateProfileRequest = ApiSchema<'UpdateProfileRequest'>;
export type MessageResponse = ApiSchema<'MessageResponse'>;

export type HelpOverviewResponse = {
  searchText: string;
  sections: HelpSectionResponse[];
};
export type HelpSectionResponse = {
  id: string;
  title: string;
  slug: string;
  sortOrder: number;
  articles: HelpArticleSummaryResponse[];
};
export type HelpArticleSummaryResponse = {
  id: string;
  title: string;
  slug: string;
  summary?: string | null;
  contentType: string;
  sectionTitle: string;
  sectionSlug: string;
  sectionSortOrder: number;
  sortOrder: number;
};
export type HelpArticleResponse = HelpArticleSummaryResponse & {
  body: string;
  isPublished: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  assets: HelpAssetResponse[];
};
export type HelpAdminOverviewResponse = {
  searchText: string;
  status: string;
  contentType: string;
  sections: HelpSectionResponse[];
  articles: HelpArticleResponse[];
};
export type HelpAssetResponse = {
  id: string;
  title: string;
  assetType: string;
  url: string;
  internalPath?: string | null;
  sortOrder: number;
};
export type HelpArticleMutationRequest = {
  sectionTitle: string;
  sectionSlug: string;
  title: string;
  slug: string;
  summary?: string | null;
  body: string;
  contentType: string;
  isPublished: boolean;
  sectionSortOrder: number;
  sortOrder: number;
  assets: HelpAssetMutationRequest[];
};
export type HelpAssetMutationRequest = {
  title: string;
  assetType: string;
  url: string;
  internalPath?: string | null;
  sortOrder: number;
};
export type HelpPublishRequest = {
  isPublished: boolean;
};

export type QueryCompaniesRequest = {
  searchText?: string;
  page?: number;
  pageSize?: number;
  sort?: string;
  sortDir?: SortDirection;
};
export type QueryCompaniesResponse = ApiSchema<'QueryCompaniesResponse'> & PagedResponse<CompanyListItem>;

export type QueryUsersRequest = QueryCompaniesRequest & {
  companyId?: string | null;
};
export type QueryUsersResponse = ApiSchema<'QueryUsersResponse'> & PagedResponse<UserListItem>;

export type QueryContractsRequest = QueryCompaniesRequest & {
  companyId?: string | null;
  siteId?: string | null;
};
export type ContractListItem = ApiSchema<'ContractListItem'>;
export type QueryContractsResponse = ApiSchema<'QueryContractsResponse'> & PagedResponse<ContractListItem>;
export type ContractDetailResponse = ApiSchema<'ContractDetailResponse'> & {
  companies: OptionItem[];
  sites: OptionItem[];
};
export type ContractMutationRequest = Omit<ApiSchema<'ContractMutationRequest'>, 'siteId' | 'offHireDate'> & {
  siteId?: string | null;
  offHireDate?: string | null;
};
export type ContractOptionsResponse = ApiSchema<'ContractOptionsResponse'>;

export type QuerySitesRequest = QueryCompaniesRequest & {
  companyId?: string | null;
  includeArchived?: boolean;
};
export type SiteListItem = ApiSchema<'SiteListItem'>;
export type QuerySitesResponse = ApiSchema<'QuerySitesResponse'> & PagedResponse<SiteListItem>;
export type SiteArchiveResponse = ApiSchema<'SiteArchiveResponse'>;
export type SiteMonitorItem = Omit<ApiSchema<'SiteMonitorItem'>, 'lat' | 'lng' | 'what3words'> & {
  lat?: number | null;
  lng?: number | null;
  what3words?: string | null;
};
export type SiteNotificationItem = ApiSchema<'SiteNotificationItem'>;
export type SiteOperatingHours = {
  dayOfWeek: number;
  dayName: string;
  startTime?: string | null;
  endTime?: string | null;
  isClosed: boolean;
};
export type SiteDetailResponse = Omit<
  ApiSchema<'SiteDetailResponse'>,
  'contractList' | 'monitors' | 'openNotifications' | 'archive' | 'companies' | 'availableContracts'
> & {
  customerLogoUrl?: string | null;
  operatingHours: SiteOperatingHours[];
  contractList: ContractListItem[];
  monitors: SiteMonitorItem[];
  openNotifications: SiteNotificationItem[];
  archive?: SiteArchiveResponse | null;
  companies: OptionItem[];
  availableContracts: OptionItem[];
};
export type SiteMutationRequest = Omit<
  ApiSchema<'SiteMutationRequest'>,
  | 'contractId'
  | 'addressLine1'
  | 'addressLine2'
  | 'postcode'
  | 'city'
  | 'county'
  | 'startTime'
  | 'endTime'
  | 'satStartTime'
  | 'satEndTime'
  | 'sunStartTime'
  | 'sunEndTime'
> & {
  contractId?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  postcode?: string | null;
  city?: string | null;
  county?: string | null;
  startTime?: string | null;
  endTime?: string | null;
  satStartTime?: string | null;
  satEndTime?: string | null;
  sunStartTime?: string | null;
  sunEndTime?: string | null;
  operatingHours?: SiteOperatingHours[] | null;
};
export type SiteOptionsResponse = ApiSchema<'SiteOptionsResponse'>;
export type QuerySiteMonitorsResponse = ApiSchema<'QuerySiteMonitorsResponse'> & PagedResponse<SiteMonitorItem>;
export type QuerySiteNotificationsResponse = ApiSchema<'QuerySiteNotificationsResponse'> & PagedResponse<SiteNotificationItem>;
export type SiteNotificationSettingsResponse = ApiSchema<'SiteNotificationSettingsResponse'>;
export type SiteNotificationSettingItem = ApiSchema<'SiteNotificationSettingItem'>;
export type SiteNotificationSettingMutationRequest = Omit<ApiSchema<'SiteNotificationSettingMutationRequest'>, 'startTime' | 'endTime'> & {
  startTime?: string | null;
  endTime?: string | null;
};

export type MonitorListState = 'all' | 'new' | 'not-in-use' | 'offline' | 'online' | 'installer';
export type QueryMonitorsRequest = QueryCompaniesRequest & {
  state?: MonitorListState;
  monitorType?: string | null;
};
export type MonitorListItem = ApiSchema<'MonitorListItem'> & {
  serialId: string;
  manufacturer: string;
  model: string;
  firmwareVersion: string;
  typeOfMonitor: string;
};
export type QueryMonitorsResponse = ApiSchema<'QueryMonitorsResponse'> & PagedResponse<MonitorListItem> & {
  state: MonitorListState;
};
export type MonitorRemovalImpactResponse = {
  deploymentCount: number;
  notificationCount: number;
  alertRuleCount: number;
  measurementTableCount: number;
  measurementRowCount: number;
  hasRelatedData: boolean;
};
export type UnattachedMonitorListItem = MonitorListItem & {
  hasRelatedData: boolean;
  willArchiveOnRemoval: boolean;
  impact: MonitorRemovalImpactResponse;
};
export type QueryUnattachedMonitorsResponse = PagedResponse<UnattachedMonitorListItem> & {
  canRemove: boolean;
};
export type MonitorAlertLevelItem = ApiSchema<'MonitorAlertLevelItem'>;
export type MonitorNotificationItem = ApiSchema<'MonitorNotificationItem'>;
export type MonitorMetricSummary = {
  label: string;
  field: string;
  value?: number | null;
  unit?: string | null;
  sampleTime?: string | null;
  detail?: string | null;
};
export type MonitorDeploymentSummary = {
  deploymentId: string;
  contractNumber?: string | null;
  siteName?: string | null;
  companyName?: string | null;
  onHireDate: string;
  offHireDate?: string | null;
  addedDate: string;
};
export type MonitorDetailResponse = ApiSchema<'MonitorDetailResponse'> & MonitorListItem & {
  monitorNotes: string;
  latestReading?: MonitorMetricSummary | null;
  latestAverage?: MonitorMetricSummary | null;
  latestBattery?: MonitorMetricSummary | null;
  deploymentSummary?: MonitorDeploymentSummary | null;
  alertLevels: MonitorAlertLevelItem[];
  recentNotifications: MonitorNotificationItem[];
};
export type MonitorMutationRequest = Omit<
  ApiSchema<'MonitorMutationRequest'>,
  'fleetNumber' | 'calibrationDate' | 'calibrationDue' | 'deploymentId' | 'lat' | 'lng' | 'location' | 'what3words'
> & {
  fleetNumber?: string | null;
  calibrationDate?: string | null;
  calibrationDue?: string | null;
  deploymentId?: string | null;
  lat?: number | null;
  lng?: number | null;
  location?: string | null;
  what3words?: string | null;
};
export type MonitorRemovalRequest = {
  reason?: string | null;
};
export type MonitorRemovalResponse = MutationResponse & {
  action: 'deleted' | 'archived';
  impact: MonitorRemovalImpactResponse;
};
export type FleetNumberMutationRequest = ApiSchema<'FleetNumberMutationRequest'>;
export type MonitorOptionsResponse = ApiSchema<'MonitorOptionsResponse'>;
export type MonitorAssignmentRequest = ApiSchema<'MonitorAssignmentRequest'>;
export type MonitorAssignmentContextResponse = ApiSchema<'MonitorAssignmentContextResponse'> & {
  contracts: OptionItem[];
  availableMonitors: MonitorListItem[];
  assignedMonitors: MonitorListItem[];
};
export type DefaultMonitorsResponse = ApiSchema<'DefaultMonitorsResponse'>;
export type InstallerDeploymentMutationRequest = ApiSchema<'InstallerDeploymentMutationRequest'>;
export type InstallerMonitorStatusResponse = ApiSchema<'InstallerMonitorStatusResponse'>;
export type What3WordsConvertResponse = ApiSchema<'What3WordsConvertResponse'>;

export type ReportListItem = ApiSchema<'ReportListItem'>;
export type QueryReportsResponse = ApiSchema<'QueryReportsResponse'> & PagedResponse<ReportListItem>;
export type QueryReportRulesRequest = QueryCompaniesRequest & {
  siteId?: string | null;
};
export type ReportRuleListItem = ApiSchema<'ReportRuleListItem'>;
export type QueryReportRulesResponse = ApiSchema<'QueryReportRulesResponse'> & PagedResponse<ReportRuleListItem>;
export type ReportAlertRuleGuidelineItem = {
  monitorType: string;
  title: string;
  summary?: string | null;
  body?: string | null;
  articleSlug?: string | null;
};
export type ReportRuleDetailResponse = ApiSchema<'ReportRuleDetailResponse'> & {
  sites: OptionItem[];
  frequencies: OptionItem[];
  daysOfWeek: OptionItem[];
  assignedUserCount: number;
  alertRuleGuidelines?: ReportAlertRuleGuidelineItem[];
};
export type ReportRuleOptionsResponse = ApiSchema<'ReportRuleOptionsResponse'> & {
  sites: OptionItem[];
  frequencies: OptionItem[];
  daysOfWeek: OptionItem[];
  alertRuleGuidelines?: ReportAlertRuleGuidelineItem[];
};
export type ReportRuleMutationRequest = Omit<ApiSchema<'ReportRuleMutationRequest'>, 'frequency' | 'dayOfWeek' | 'dayOfMonth' | 'reportName'> & {
  frequency: number;
  dayOfWeek?: number | null;
  dayOfMonth?: number | null;
  reportName?: string | null;
};
export type ReportGenerationRequestResponse = {
  id: string;
  reportRuleId: string;
  status: string;
  message: string;
  requestedAtUtc: string;
};
export type QueryReportRuleUsersResponse = PagedResponse<UserListItem> & {
  reportRuleId?: string;
  siteId?: string;
  siteName?: string;
  companyId?: string | null;
  companyName?: string | null;
  assignedUserCount?: number;
};
export type ReportUserAssignmentResponse = Omit<
  ApiSchema<'ReportUserAssignmentResponse'>,
  'availableUsers' | 'assignedUsers' | 'companyId' | 'companyName'
> & {
  companyId?: string | null;
  companyName?: string | null;
  availableUsers: UserListItem[];
  assignedUsers: UserListItem[];
};
export type ReportUserMutationRequest = ApiSchema<'ReportUserMutationRequest'>;

export type NotificationListState = 'all' | 'open' | 'cautions';
export type QueryNotificationsRequest = QueryCompaniesRequest & {
  state?: NotificationListState;
  monitorType?: string | null;
  alertType?: string | null;
  openAlerts?: boolean | null;
  siteId?: string | null;
};
export type NotificationListItem = ApiSchema<'NotificationListItem'> & {
  serialId: string;
  typeOfMonitor: string;
  alertType: string;
  alertField: string;
  limitName: string;
  alertStatus: string;
};
export type QueryNotificationsResponse = ApiSchema<'QueryNotificationsResponse'> & PagedResponse<NotificationListItem> & {
  state: NotificationListState;
};
export type NotificationDetailResponse = ApiSchema<'NotificationDetailResponse'> & NotificationListItem & {
  graphFromUtc: string;
  graphToUtc: string;
  relatedNotifications: NotificationListItem[];
  alertLevels: AlertLevelItem[];
};
export type NotificationCloseRequest = ApiSchema<'NotificationCloseRequest'>;
export type NotificationBatchCloseRequest = ApiSchema<'NotificationBatchCloseRequest'>;
export type NotificationBatchCloseResponse = ApiSchema<'NotificationBatchCloseResponse'>;

export type QueryAlertLevelsRequest = QueryCompaniesRequest & {
  monitorId: string;
};
export type AlertLevelItem = ApiSchema<'AlertLevelItem'>;
export type AlertLevelOptionsResponse = ApiSchema<'AlertLevelOptionsResponse'>;
export type QueryAlertLevelsResponse = ApiSchema<'QueryAlertLevelsResponse'> & PagedResponse<AlertLevelItem> & {
  monitorId: string;
  serialId: string;
  fleetNumber?: string | null;
  typeOfMonitor: string;
  canManage: boolean;
  options: AlertLevelOptionsResponse;
};
export type AlertLevelMutationRequest = ApiSchema<'AlertLevelMutationRequest'>;
export type VibrationAlertLevelMutationRequest = ApiSchema<'VibrationAlertLevelMutationRequest'>;
export type VibrationAlertLevelResponse = ApiSchema<'VibrationAlertLevelResponse'>;

export type DashboardMonitorCounts = ApiSchema<'DashboardMonitorCounts'>;
export type DashboardNotificationItem = ApiSchema<'DashboardNotificationItem'>;
export type DashboardSummaryResponse = ApiSchema<'DashboardSummaryResponse'> & {
  monitorCounts: DashboardMonitorCounts;
  sites: OptionItem[];
  calendarDeployments: OptionItem[];
  recentNotifications: DashboardNotificationItem[];
};
export type BreachesAlertsRequest = {
  date?: string | null;
  page?: number;
  pageSize?: number;
  sort?: string;
  sortDir?: SortDirection;
};
export type BreachesAlertsItem = ApiSchema<'BreachesAlertsItem'>;
export type BreachesAlertsResponse = ApiSchema<'BreachesAlertsResponse'> & PagedResponse<BreachesAlertsItem>;
export type MapMarkersRequest = {
  siteId?: string | null;
};
export type MapMonitorMarker = ApiSchema<'MapMonitorMarker'>;
export type MapMarkersResponse = ApiSchema<'MapMarkersResponse'> & {
  markers: MapMonitorMarker[];
};
export type CalendarMonthRequest = {
  deploymentId: string;
  year?: number;
  month?: number;
};
export type CalendarMonthDayItem = ApiSchema<'CalendarMonthDayItem'>;
export type CalendarMonthResponse = ApiSchema<'CalendarMonthResponse'> & {
  deployments: OptionItem[];
  days: CalendarMonthDayItem[];
};
export type CalendarDayRequest = {
  monitorId: string;
  year: number;
  month: number;
  day: number;
};
export type CalendarMeasurementItem = ApiSchema<'CalendarMeasurementItem'>;
export type CalendarDayResponse = ApiSchema<'CalendarDayResponse'> & {
  values: CalendarMeasurementItem[];
  alertLevels: AlertLevelItem[];
  notifications: DashboardNotificationItem[];
};
export type MonitorDataGridRequest = {
  filterOption?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  page?: number;
  pageSize?: number;
  sort?: string;
  sortDir?: SortDirection;
};
export type MonitorGraphRequest = {
  filterOption?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
};
export type TraceListRequest = {
  fromDate?: string | null;
  toDate?: string | null;
};
export type MonitorDataColumn = ApiSchema<'MonitorDataColumn'>;
export type MonitorDataRow = ApiSchema<'MonitorDataRow'>;
export type MonitorDataGridResponse = Omit<ApiSchema<'MonitorDataGridResponse'>, 'columns' | 'rows' | 'sortDir'> & PagedResponse<MonitorDataRow> & {
  columns: MonitorDataColumn[];
  rows: MonitorDataRow[];
  sortDir: SortDirection;
};
export type MonitorGraphPoint = ApiSchema<'MonitorGraphPoint'>;
export type MonitorGraphDataset = ApiSchema<'MonitorGraphDataset'> & {
  points: MonitorGraphPoint[];
};
export type MonitorGraphThreshold = ApiSchema<'MonitorGraphThreshold'>;
export type MonitorGraphResponse = ApiSchema<'MonitorGraphResponse'> & {
  filterOptions: OptionItem[];
  datasets: MonitorGraphDataset[];
  thresholds: MonitorGraphThreshold[];
};
export type TraceSummaryItem = ApiSchema<'TraceSummaryItem'>;
export type TraceListResponse = ApiSchema<'TraceListResponse'> & {
  traces: TraceSummaryItem[];
};
export type TraceSampleItem = ApiSchema<'TraceSampleItem'>;
export type TraceDetailResponse = ApiSchema<'TraceDetailResponse'> & {
  samples: TraceSampleItem[];
};
