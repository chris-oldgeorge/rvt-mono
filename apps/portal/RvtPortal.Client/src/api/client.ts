// File summary: Defines the browser API client and typed request helpers for the React SPA.
// Major updates:
// - 2026-06-26 pending Added AbortSignal support for high-churn list and lookup requests.
// - 2026-06-26 pending Restored generated OpenAPI schema facade as the client contract source.
// - 2026-06-24 pending Added report-rule manual generation and paged recipient query helpers.
// - 2026-06-10 pending Added Help CMS admin management API helpers.
// - 2026-06-08 pending Added unattached monitor removal API helpers.
// - 2026-06-09 pending Added monitor picture upload helper for legacy detail parity.
// - 2026-06-24 pending Added site customer-logo upload and delete helpers.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

import type {
  AlertLevelItem,
  AlertLevelMutationRequest,
  AlertLevelOptionsResponse,
  AuthStateResponse,
  BreachesAlertsRequest,
  BreachesAlertsResponse,
  CalendarDayRequest,
  CalendarDayResponse,
  CalendarMonthRequest,
  CalendarMonthResponse,
  ChangePasswordRequest,
  CompanyDetailResponse,
  ContractDetailResponse,
  ContractMutationRequest,
  ContractOptionsResponse,
  CompanyMutationRequest,
  ConfirmEmailResponse,
  EntityResponse,
  DefaultMonitorsResponse,
  FleetNumberMutationRequest,
  ForgotPasswordRequest,
  GetHealthResponse,
  HelpAdminOverviewResponse,
  HelpArticleMutationRequest,
  HelpArticleResponse,
  HelpOverviewResponse,
  HelpPublishRequest,
  DashboardSummaryResponse,
  InstallerDeploymentMutationRequest,
  InstallerMonitorStatusResponse,
  LoginRequest,
  MapMarkersRequest,
  MapMarkersResponse,
  MessageResponse,
  MonitorDataGridRequest,
  MonitorDataGridResponse,
  MonitorGraphRequest,
  MonitorGraphResponse,
  MonitorAssignmentContextResponse,
  MonitorAssignmentRequest,
  MonitorDetailResponse,
  MonitorRemovalImpactResponse,
  MonitorRemovalRequest,
  MonitorRemovalResponse,
  MonitorMutationRequest,
  MonitorOptionsResponse,
  MutationResponse,
  NotificationBatchCloseRequest,
  NotificationBatchCloseResponse,
  NotificationCloseRequest,
  NotificationDetailResponse,
  QueryAlertLevelsRequest,
  QueryAlertLevelsResponse,
  QueryCompaniesRequest,
  QueryContractsRequest,
  QueryMonitorsRequest,
  QueryNotificationsRequest,
  QueryNotificationsResponse,
  ProfileResponse,
  QueryCompaniesResponse,
  QueryContractsResponse,
  QueryMonitorsResponse,
  QueryUnattachedMonitorsResponse,
  QuerySiteMonitorsResponse,
  QuerySiteNotificationsResponse,
  QueryReportRulesRequest,
  QueryReportRulesResponse,
  QueryReportRuleUsersResponse,
  QueryReportsResponse,
  QuerySitesRequest,
  QuerySitesResponse,
  QueryUsersRequest,
  QueryUsersResponse,
  ReportGenerationRequestResponse,
  ReportListItem,
  ReportRuleDetailResponse,
  ReportRuleMutationRequest,
  ReportRuleOptionsResponse,
  ReportUserAssignmentResponse,
  ReportUserMutationRequest,
  ResetPasswordRequest,
  SearchLookupResponse,
  SiteDetailResponse,
  SiteMutationRequest,
  SiteNotificationSettingItem,
  SiteNotificationSettingMutationRequest,
  SiteNotificationSettingsResponse,
  SiteOptionsResponse,
  SetInitialPasswordRequest,
  SiteAssignmentResponse,
  SiteUserMutationRequest,
  TraceDetailResponse,
  TraceListRequest,
  TraceListResponse,
  UpdateProfileRequest,
  UserDetailResponse,
  UserMutationRequest,
  VibrationAlertLevelMutationRequest,
  VibrationAlertLevelResponse,
  What3WordsConvertResponse
} from './openApiClient';
const configuredApiBaseUrl = (import.meta.env.VITE_RVT_PORTAL_API_URL ?? '').trim();
const configuredAllowedApiHosts = (import.meta.env.VITE_RVT_PORTAL_ALLOWED_API_HOSTS ?? '').trim();
const browserOrigin = getBrowserOrigin();
const allowedApiHosts = buildAllowedApiHosts(configuredAllowedApiHosts, browserOrigin);
const apiBaseUrl = normalizeApiBaseUrl(configuredApiBaseUrl, browserOrigin, allowedApiHosts);
const apiUnavailableMessage =
  'Unable to reach the RVT Portal API. Start RvtPortal.Spa on http://localhost:5178, or set VITE_RVT_PORTAL_API_URL to the API origin.';
const unsafeApiUrlMessage =
  'Blocked an unsafe API request URL. Only relative /api/ paths whose host is in VITE_RVT_PORTAL_ALLOWED_API_HOSTS are allowed.';
const jsonHeaders = {
  Accept: 'application/json'
};
const sendJsonHeaders = {
  ...jsonHeaders,
  'Content-Type': 'application/json'
};
export class ApiError extends Error {
  readonly status: number;
  readonly correlationId?: string | null;

  constructor(message: string, status: number, correlationId?: string | null) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.correlationId = correlationId;
  }
}
export type ApiRequestOptions = {
  signal?: AbortSignal;
};
// Function summary: Evaluates unauthorized for the current decision point.
export function isUnauthorized(error: unknown) {
  return error instanceof ApiError && error.status === 401;
}
// Function summary: Evaluates forbidden for the current decision point.
export function isForbidden(error: unknown) {
  return error instanceof ApiError && error.status === 403;
}
// Function summary: Evaluates aborted browser requests so React effects can ignore stale loads.
export function isAbortError(error: unknown) {
  return typeof error === 'object' && error !== null && 'name' in error && error.name === 'AbortError';
}
async function getJson<T>(path: string, options: ApiRequestOptions = {}): Promise<T> {
  return requestJson<T>(path, { method: 'GET' }, options);
}
async function sendJson<T>(path: string, method: 'POST' | 'PUT' | 'DELETE', body?: unknown, options: ApiRequestOptions = {}): Promise<T> {
  return requestJson<T>(path, {
    method,
    headers: sendJsonHeaders,
    body: body === undefined ? undefined : JSON.stringify(body)
  }, options);
}
async function requestJson<T>(path: string, init: RequestInit, options: ApiRequestOptions = {}): Promise<T> {
  const url = apiUrl(path);
  let response: Response;
  try {
    response = await fetch(url, {
      headers: jsonHeaders,
      credentials: 'include',
      signal: options.signal,
      ...init
    });
  } catch (error) {
    if (isAbortError(error)) {
      throw error;
    }
    throw new Error(apiUnavailableMessage);
  }
  throwIfAborted(options.signal);
  if (!response.ok) {
    const problem = await readProblemDetails(response);
    throw new ApiError(problem.message || `Request failed with ${response.status}`, response.status, problem.correlationId);
  }
  const body = await response.json() as T;
  throwIfAborted(options.signal);
  return body;
}
// Function summary: Stops stale fetch responses when an AbortSignal was tripped after transport resolution.
function throwIfAborted(signal?: AbortSignal) {
  if (!signal?.aborted) {
    return;
  }

  if (typeof DOMException === 'function') {
    throw new DOMException('The operation was aborted.', 'AbortError');
  }

  const error = new Error('The operation was aborted.');
  error.name = 'AbortError';
  throw error;
}
// Function summary: Handles the API url workflow for this module.
function apiUrl(path: string) {
  const safePath = normalizeApiPath(path);
  const url = new URL(safePath, apiBaseUrl);
  if (!allowedApiHosts.has(url.host.toLowerCase())) {
    throw new Error(unsafeApiUrlMessage);
  }

  return url;
}

// Function summary: Handles the normalize API path workflow for this module.
function normalizeApiPath(path: string) {
  if (!path.startsWith('/api/') || path.startsWith('//') || path.includes('\\')) {
    throw new Error(unsafeApiUrlMessage);
  }

  const parsed = new URL(path, 'https://rvt.local');
  if (parsed.origin !== 'https://rvt.local' || !parsed.pathname.startsWith('/api/')) {
    throw new Error(unsafeApiUrlMessage);
  }

  if (parsed.pathname.split('/').some((segment) => segment === '.' || segment === '..')) {
    throw new Error(unsafeApiUrlMessage);
  }

  return `${parsed.pathname}${parsed.search}`;
}

// Function summary: Retrieves browser origin data for callers.
function getBrowserOrigin() {
  if (!globalThis.location?.origin) {
    return 'http://localhost';
  }

  return globalThis.location.origin;
}

// Function summary: Handles the normalize API base url workflow for this module.
function normalizeApiBaseUrl(value: string, fallbackOrigin: string, allowedHosts: ReadonlySet<string>) {
  const url = new URL(value || fallbackOrigin, fallbackOrigin);
  if (!['http:', 'https:'].includes(url.protocol) || !allowedHosts.has(url.host.toLowerCase())) {
    throw new Error(unsafeApiUrlMessage);
  }

  return url.origin;
}

// Function summary: Builds allowed API hosts data for callers.
function buildAllowedApiHosts(value: string, currentOrigin: string) {
  const hosts = new Set<string>([
    new URL(currentOrigin).host.toLowerCase(),
    'localhost:5178',
    'localhost:5179',
    '127.0.0.1:5178',
    '127.0.0.1:5179'
  ]);

  for (const entry of value.split(',')) {
    const host = normalizeAllowedApiHost(entry, currentOrigin);
    if (host) {
      hosts.add(host);
    }
  }

  return hosts;
}

// Function summary: Handles the normalize allowed API host workflow for this module.
function normalizeAllowedApiHost(value: string, currentOrigin: string) {
  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }

  const candidate = trimmed.includes('://') ? trimmed : `${new URL(currentOrigin).protocol}//${trimmed}`;
  return new URL(candidate).host.toLowerCase();
}
async function readProblemDetails(response: Response) {
  const correlationHeader = response.headers.get('x-correlation-id');
  const contentType = response.headers.get('content-type') ?? '';
  if (contentType.includes('application/problem+json') || contentType.includes('application/json')) {
    const body = await response.json().catch(() => null) as {
      detail?: string;
      title?: string;
      errors?: Record<string, string[]>;
      correlationId?: string;
      traceId?: string;
    } | null;
    const validationErrors = body?.errors ? Object.values(body.errors).flat().join(' ') : null;
    return {
      message: validationErrors || body?.detail || body?.title || null,
      correlationId: correlationHeader || body?.correlationId || body?.traceId || null
    };
  }
  return {
    message: await response.text().catch(() => null),
    correlationId: correlationHeader
  };
}
// Function summary: Retrieves health data for callers.
export function getHealth() {
  return getJson<GetHealthResponse>('/api/health');
}
// Function summary: Retrieves current auth data for callers.
export function getCurrentAuth() {
  return getJson<AuthStateResponse>('/api/auth/me');
}
// Function summary: Handles the login workflow for this module.
export function login(request: LoginRequest) {
  return sendJson<AuthStateResponse>('/api/auth/login', 'POST', request);
}
// Function summary: Handles the logout workflow for this module.
export function logout() {
  return sendJson<AuthStateResponse>('/api/auth/logout', 'POST');
}
// Function summary: Handles the forgot password workflow for this module.
export function forgotPassword(request: ForgotPasswordRequest) {
  return sendJson<MessageResponse>('/api/auth/forgot-password', 'POST', request);
}
// Function summary: Handles the reset password workflow for this module.
export function resetPassword(request: ResetPasswordRequest) {
  return sendJson<MessageResponse>('/api/auth/reset-password', 'POST', request);
}
// Function summary: Handles the confirm email workflow for this module.
export function confirmEmail(userId: string, code: string) {
  const params = new URLSearchParams({ userId, code });
  return getJson<ConfirmEmailResponse>(`/api/auth/confirm-email?${params.toString()}`);
}
// Function summary: Handles the set initial password workflow for this module.
export function setInitialPassword(request: SetInitialPasswordRequest) {
  return sendJson<AuthStateResponse>('/api/auth/confirm-email', 'POST', request);
}
// Function summary: Retrieves profile data for callers.
export function getProfile() {
  return getJson<ProfileResponse>('/api/auth/profile');
}
// Function summary: Updates profile data for the current workflow.
export function updateProfile(request: UpdateProfileRequest) {
  return sendJson<ProfileResponse>('/api/auth/profile', 'PUT', request);
}
// Function summary: Handles the change password workflow for this module.
export function changePassword(request: ChangePasswordRequest) {
  return sendJson<MessageResponse>('/api/auth/password', 'POST', request);
}
// Function summary: Handles the query companies workflow for this module.
export function queryCompanies(request: QueryCompaniesRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<QueryCompaniesResponse>(`/api/companies?${params.toString()}`, options);
}
// Function summary: Retrieves company data for callers.
export function getCompany(id: string) {
  return getJson<EntityResponse<CompanyDetailResponse>>(`/api/companies/${encodeURIComponent(id)}`);
}
// Function summary: Creates company data for the current workflow.
export function createCompany(request: CompanyMutationRequest) {
  return sendJson<EntityResponse<CompanyDetailResponse>>('/api/companies', 'POST', request);
}
// Function summary: Updates company data for the current workflow.
export function updateCompany(id: string, request: CompanyMutationRequest) {
  return sendJson<EntityResponse<CompanyDetailResponse>>(`/api/companies/${encodeURIComponent(id)}`, 'PUT', request);
}
// Function summary: Removes company data for the current workflow.
export function deleteCompany(id: string) {
  return sendJson<MutationResponse>(`/api/companies/${encodeURIComponent(id)}`, 'DELETE');
}
// Function summary: Handles the query users workflow for this module.
export function queryUsers(request: QueryUsersRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<QueryUsersResponse>(`/api/users?${params.toString()}`, options);
}
// Function summary: Retrieves user options data for callers.
export function getUserOptions() {
  return getJson<UserDetailResponse>('/api/users/options');
}
// Function summary: Retrieves user data for callers.
export function getUser(id: string) {
  return getJson<EntityResponse<UserDetailResponse>>(`/api/users/${encodeURIComponent(id)}`);
}
// Function summary: Creates user data for the current workflow.
export function createUser(request: UserMutationRequest) {
  return sendJson<EntityResponse<UserDetailResponse>>('/api/users', 'POST', request);
}
// Function summary: Updates user data for the current workflow.
export function updateUser(id: string, request: UserMutationRequest) {
  return sendJson<EntityResponse<UserDetailResponse>>(`/api/users/${encodeURIComponent(id)}`, 'PUT', request);
}
// Function summary: Handles the resend user confirmation workflow for this module.
export function resendUserConfirmation(id: string) {
  return sendJson<MessageResponse>(`/api/users/${encodeURIComponent(id)}/resend-confirmation`, 'POST');
}
// Function summary: Handles the send user reset password link workflow for this module.
export function sendUserResetPasswordLink(id: string) {
  return sendJson<MessageResponse>(`/api/users/${encodeURIComponent(id)}/reset-password-link`, 'POST');
}
// Function summary: Handles the disable user workflow for this module.
export function disableUser(id: string) {
  return sendJson<EntityResponse<UserDetailResponse>>(`/api/users/${encodeURIComponent(id)}/disable`, 'POST');
}
// Function summary: Handles the enable user workflow for this module.
export function enableUser(id: string) {
  return sendJson<EntityResponse<UserDetailResponse>>(`/api/users/${encodeURIComponent(id)}/enable`, 'POST');
}
// Function summary: Removes user data for the current workflow.
export function deleteUser(id: string) {
  return sendJson<MutationResponse>(`/api/users/${encodeURIComponent(id)}`, 'DELETE');
}
// Function summary: Retrieves site assignments data for callers.
export function getSiteAssignments(siteId: string) {
  return getJson<EntityResponse<SiteAssignmentResponse>>(`/api/users/site-assignments/${encodeURIComponent(siteId)}`);
}
// Function summary: Registers user to site for the current workflow.
export function addUserToSite(request: SiteUserMutationRequest) {
  return sendJson<EntityResponse<SiteAssignmentResponse>>('/api/users/site-assignments', 'POST', request);
}
// Function summary: Handles the set site contact user workflow for this module.
export function setSiteContactUser(request: SiteUserMutationRequest) {
  return sendJson<EntityResponse<SiteAssignmentResponse>>('/api/users/site-assignments/contact', 'POST', request);
}
// Function summary: Removes site contact user data for the current workflow.
export function removeSiteContactUser(request: SiteUserMutationRequest) {
  return sendJson<EntityResponse<SiteAssignmentResponse>>(
    `/api/users/site-assignments/contact/${encodeURIComponent(request.siteId)}/${encodeURIComponent(request.userId)}`,
    'DELETE'
  );
}
// Function summary: Removes user from site data for the current workflow.
export function removeUserFromSite(request: SiteUserMutationRequest) {
  return sendJson<EntityResponse<SiteAssignmentResponse>>(
    `/api/users/site-assignments/${encodeURIComponent(request.siteId)}/${encodeURIComponent(request.userId)}`,
    'DELETE'
  );
}
// Function summary: Handles the query contracts workflow for this module.
export function queryContracts(request: QueryContractsRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<QueryContractsResponse>(`/api/contracts?${params.toString()}`, options);
}
// Function summary: Retrieves contract options data for callers.
export function getContractOptions(companyId?: string | null) {
  const params = new URLSearchParams();
  if (companyId) {
    params.set('companyId', companyId);
  }
  return getJson<ContractOptionsResponse>(pathWithQuery('/api/contracts/options', params));
}
// Function summary: Retrieves contract data for callers.
export function getContract(id: string) {
  return getJson<EntityResponse<ContractDetailResponse>>(`/api/contracts/${encodeURIComponent(id)}`);
}
// Function summary: Creates contract data for the current workflow.
export function createContract(request: ContractMutationRequest) {
  return sendJson<EntityResponse<ContractDetailResponse>>('/api/contracts', 'POST', request);
}
// Function summary: Updates contract data for the current workflow.
export function updateContract(id: string, request: ContractMutationRequest) {
  return sendJson<EntityResponse<ContractDetailResponse>>(`/api/contracts/${encodeURIComponent(id)}`, 'PUT', request);
}
// Function summary: Removes contract data for the current workflow.
export function deleteContract(id: string) {
  return sendJson<MutationResponse>(`/api/contracts/${encodeURIComponent(id)}`, 'DELETE');
}
// Function summary: Handles the query sites workflow for this module.
export function querySites(request: QuerySitesRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<QuerySitesResponse>(`/api/sites?${params.toString()}`, options);
}
// Function summary: Retrieves site options data for callers.
export function getSiteOptions(companyId?: string | null) {
  const params = new URLSearchParams();
  if (companyId) {
    params.set('companyId', companyId);
  }
  return getJson<SiteOptionsResponse>(pathWithQuery('/api/sites/options', params));
}
// Function summary: Retrieves site data for callers.
export function getSite(id: string) {
  return getJson<EntityResponse<SiteDetailResponse>>(`/api/sites/${encodeURIComponent(id)}`);
}
// Function summary: Creates site data for the current workflow.
export function createSite(request: SiteMutationRequest) {
  return sendJson<EntityResponse<SiteDetailResponse>>('/api/sites', 'POST', request);
}
// Function summary: Updates site data for the current workflow.
export function updateSite(id: string, request: SiteMutationRequest) {
  return sendJson<EntityResponse<SiteDetailResponse>>(`/api/sites/${encodeURIComponent(id)}`, 'PUT', request);
}
// Function summary: Uploads a customer logo image for a site.
export function uploadSiteCustomerLogo(id: string, logo: File) {
  const body = new FormData();
  body.set('logo', logo);
  return requestJson<EntityResponse<SiteDetailResponse>>(`/api/sites/${encodeURIComponent(id)}/customer-logo`, {
    method: 'POST',
    body
  });
}
// Function summary: Deletes a customer logo image for a site.
export function deleteSiteCustomerLogo(id: string) {
  return sendJson<EntityResponse<SiteDetailResponse>>(`/api/sites/${encodeURIComponent(id)}/customer-logo`, 'DELETE');
}
// Function summary: Handles the archive site workflow for this module.
export function archiveSite(id: string) {
  return sendJson<EntityResponse<SiteDetailResponse>>(`/api/sites/${encodeURIComponent(id)}/archive`, 'POST');
}
// Function summary: Handles the query site monitors workflow for this module.
export function querySiteMonitors(siteId: string, request: QueryCompaniesRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<QuerySiteMonitorsResponse>(`/api/sites/${encodeURIComponent(siteId)}/monitors?${params.toString()}`, options);
}
// Function summary: Handles the query site open notifications workflow for this module.
export function querySiteOpenNotifications(siteId: string, request: QueryCompaniesRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<QuerySiteNotificationsResponse>(`/api/sites/${encodeURIComponent(siteId)}/notifications/open?${params.toString()}`, options);
}
// Function summary: Retrieves site notification settings data for callers.
export function getSiteNotificationSettings(siteId: string) {
  return getJson<SiteNotificationSettingsResponse>(`/api/sites/${encodeURIComponent(siteId)}/notification-settings`);
}
// Function summary: Updates site notification setting data for the current workflow.
export function updateSiteNotificationSetting(siteId: string, siteUserId: string, request: SiteNotificationSettingMutationRequest) {
  return sendJson<EntityResponse<SiteNotificationSettingItem>>(
    `/api/sites/${encodeURIComponent(siteId)}/notification-settings/${encodeURIComponent(siteUserId)}`,
    'PUT',
    request
  );
}
// Function summary: Retrieves published Help CMS sections and article summaries for callers.
export function queryHelp(searchText?: string | null, options: ApiRequestOptions = {}) {
  const params = new URLSearchParams();
  if (searchText) {
    params.set('searchText', searchText);
  }
  return getJson<HelpOverviewResponse>(pathWithQuery('/api/help', params), options);
}
// Function summary: Retrieves a Help CMS article by slug for callers.
export function getHelpArticle(slug: string, options: ApiRequestOptions = {}) {
  return getJson<EntityResponse<HelpArticleResponse>>(`/api/help/articles/${encodeURIComponent(slug)}`, options);
}
// Function summary: Retrieves all Help CMS content for admin management.
export function queryAdminHelp(request: { searchText?: string; status?: string; contentType?: string } = {}, options: ApiRequestOptions = {}) {
  return getJson<HelpAdminOverviewResponse>(pathWithQuery('/api/help/admin', toSearchParams(request)), options);
}
// Function summary: Retrieves a Help CMS article by id for admin editing.
export function getAdminHelpArticle(id: string) {
  return getJson<EntityResponse<HelpArticleResponse>>(`/api/help/admin/articles/${encodeURIComponent(id)}`);
}
// Function summary: Creates Help CMS article data for admin users.
export function createHelpArticle(request: HelpArticleMutationRequest) {
  return sendJson<EntityResponse<HelpArticleResponse>>('/api/help/articles', 'POST', request);
}
// Function summary: Updates Help CMS article data for admin users.
export function updateHelpArticle(id: string, request: HelpArticleMutationRequest) {
  return sendJson<EntityResponse<HelpArticleResponse>>(`/api/help/admin/articles/${encodeURIComponent(id)}`, 'PUT', request);
}
// Function summary: Publishes or unpublishes Help CMS article data for admin users.
export function setHelpArticlePublication(id: string, request: HelpPublishRequest) {
  return sendJson<EntityResponse<HelpArticleResponse>>(`/api/help/admin/articles/${encodeURIComponent(id)}/publication`, 'POST', request);
}
// Function summary: Removes Help CMS article data for admin users.
export function deleteHelpArticle(id: string) {
  return sendJson<MutationResponse>(`/api/help/admin/articles/${encodeURIComponent(id)}`, 'DELETE');
}
// Function summary: Handles the query monitors workflow for this module.
export function queryMonitors(request: QueryMonitorsRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<QueryMonitorsResponse>(`/api/monitors?${params.toString()}`, options);
}
// Function summary: Handles the query unattached monitors workflow for this module.
export function queryUnattachedMonitors(request: QueryMonitorsRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<QueryUnattachedMonitorsResponse>(`/api/monitors/unattached?${params.toString()}`, options);
}
// Function summary: Retrieves monitor options data for callers.
export function getMonitorOptions() {
  return getJson<MonitorOptionsResponse>('/api/monitors/options');
}
// Function summary: Retrieves monitor data for callers.
export function getMonitor(id: string) {
  return getJson<EntityResponse<MonitorDetailResponse>>(`/api/monitors/${encodeURIComponent(id)}`);
}
// Function summary: Retrieves monitor deployment data for callers.
export function getMonitorDeployment(deploymentId: string) {
  return getJson<EntityResponse<MonitorDetailResponse>>(`/api/monitors/deployments/${encodeURIComponent(deploymentId)}`);
}
// Function summary: Retrieves monitor removal impact data for callers.
export function getMonitorRemovalImpact(id: string) {
  return getJson<MonitorRemovalImpactResponse>(`/api/monitors/${encodeURIComponent(id)}/removal-impact`);
}
// Function summary: Updates monitor data for the current workflow.
export function updateMonitor(id: string, request: MonitorMutationRequest) {
  return sendJson<EntityResponse<MonitorDetailResponse>>(`/api/monitors/${encodeURIComponent(id)}`, 'PUT', request);
}
// Function summary: Uploads monitor deployment picture data for the current workflow.
export function uploadMonitorPicture(id: string, picture: File) {
  const body = new FormData();
  body.set('picture', picture);
  return requestJson<EntityResponse<MonitorDetailResponse>>(`/api/monitors/${encodeURIComponent(id)}/picture`, {
    method: 'POST',
    body
  });
}
// Function summary: Handles the set monitor fleet number workflow for this module.
export function setMonitorFleetNumber(id: string, request: FleetNumberMutationRequest) {
  return sendJson<EntityResponse<MonitorDetailResponse>>(`/api/monitors/${encodeURIComponent(id)}/fleet-number`, 'PUT', request);
}
// Function summary: Retrieves monitor assignment data for callers.
export function getMonitorAssignment(siteId: string, contractId?: string | null) {
  const params = new URLSearchParams({ siteId });
  if (contractId) {
    params.set('contractId', contractId);
  }
  return getJson<MonitorAssignmentContextResponse>(`/api/monitors/assignment?${params.toString()}`);
}
// Function summary: Registers monitor to contract for the current workflow.
export function addMonitorToContract(id: string, request: MonitorAssignmentRequest) {
  return sendJson<EntityResponse<MonitorDetailResponse>>(`/api/monitors/${encodeURIComponent(id)}/contract-assignment`, 'POST', request);
}
// Function summary: Removes monitor from contract data for the current workflow.
export function removeMonitorFromContract(id: string) {
  return sendJson<MutationResponse>(`/api/monitors/${encodeURIComponent(id)}/contract-assignment`, 'DELETE');
}
// Function summary: Removes or archives an unattached monitor through the admin workflow.
export function removeUnattachedMonitor(id: string, request: MonitorRemovalRequest) {
  return sendJson<MonitorRemovalResponse>(`/api/monitors/${encodeURIComponent(id)}/unattached`, 'DELETE', request);
}
// Function summary: Registers default monitor alert levels for the current workflow.
export function addDefaultMonitorAlertLevels() {
  return sendJson<DefaultMonitorsResponse>('/api/monitors/default-alert-levels', 'POST');
}
// Function summary: Handles the query installer monitors workflow for this module.
export function queryInstallerMonitors(request: QueryMonitorsRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<QueryMonitorsResponse>(`/api/installer/monitors?${params.toString()}`, options);
}
// Function summary: Retrieves installer monitor data for callers.
export function getInstallerMonitor(id: string) {
  return getJson<EntityResponse<MonitorDetailResponse>>(`/api/installer/monitors/${encodeURIComponent(id)}`);
}
// Function summary: Updates installer deployment data for the current workflow.
export function updateInstallerDeployment(deploymentId: string, request: InstallerDeploymentMutationRequest) {
  return sendJson<EntityResponse<MonitorDetailResponse>>(`/api/installer/deployments/${encodeURIComponent(deploymentId)}`, 'PUT', request);
}
// Function summary: Retrieves installer monitor status data for callers.
export function getInstallerMonitorStatus(id: string) {
  return getJson<InstallerMonitorStatusResponse>(`/api/installer/monitors/${encodeURIComponent(id)}/status`);
}
// Function summary: Handles the convert what3 words workflow for this module.
export function convertWhat3Words(what3words: string) {
  const params = new URLSearchParams({ what3words });
  return getJson<What3WordsConvertResponse>(`/api/installer/what3words/convert?${params.toString()}`);
}
// Function summary: Handles the query reports workflow for this module.
export function queryReports(request: QueryCompaniesRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<QueryReportsResponse>(`/api/reports?${params.toString()}`, options);
}
// Function summary: Retrieves report data for callers.
export function getReport(id: string) {
  return getJson<EntityResponse<ReportListItem>>(`/api/reports/${encodeURIComponent(id)}`);
}
// Function summary: Handles the query report rules workflow for this module.
export function queryReportRules(request: QueryReportRulesRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<QueryReportRulesResponse>(`/api/report-rules?${params.toString()}`, options);
}
// Function summary: Retrieves report rule options data for callers.
export function getReportRuleOptions() {
  return getJson<ReportRuleOptionsResponse>('/api/report-rules/options');
}
// Function summary: Retrieves report rule data for callers.
export function getReportRule(id: string) {
  return getJson<EntityResponse<ReportRuleDetailResponse>>(`/api/report-rules/${encodeURIComponent(id)}`);
}
// Function summary: Creates report rule data for the current workflow.
export function createReportRule(request: ReportRuleMutationRequest) {
  return sendJson<EntityResponse<ReportRuleDetailResponse>>('/api/report-rules', 'POST', request);
}
// Function summary: Updates report rule data for the current workflow.
export function updateReportRule(id: string, request: ReportRuleMutationRequest) {
  return sendJson<EntityResponse<ReportRuleDetailResponse>>(`/api/report-rules/${encodeURIComponent(id)}`, 'PUT', request);
}
// Function summary: Queues an immediate report generation request for a report rule.
export function requestReportRuleGeneration(id: string) {
  return sendJson<ReportGenerationRequestResponse>(`/api/report-rules/${encodeURIComponent(id)}/generation-requests`, 'POST');
}
// Function summary: Removes report rule data for the current workflow.
export function deleteReportRule(id: string) {
  return sendJson<MutationResponse>(`/api/report-rules/${encodeURIComponent(id)}`, 'DELETE');
}
// Function summary: Retrieves report rule users data for callers.
export function getReportRuleUsers(id: string) {
  return getJson<EntityResponse<ReportUserAssignmentResponse>>(`/api/report-rules/${encodeURIComponent(id)}/users`);
}
// Function summary: Queries users available for a report rule recipient assignment.
export function queryReportRuleAvailableUsers(id: string, request: QueryCompaniesRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<QueryReportRuleUsersResponse>(`/api/report-rules/${encodeURIComponent(id)}/available-users?${params.toString()}`, options);
}
// Function summary: Queries users already assigned to a report rule.
export function queryReportRuleAssignedUsers(id: string, request: QueryCompaniesRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<QueryReportRuleUsersResponse>(`/api/report-rules/${encodeURIComponent(id)}/assigned-users?${params.toString()}`, options);
}
// Function summary: Registers report rule user for the current workflow.
export function addReportRuleUser(id: string, request: ReportUserMutationRequest) {
  return sendJson<EntityResponse<ReportUserAssignmentResponse>>(`/api/report-rules/${encodeURIComponent(id)}/users`, 'POST', request);
}
// Function summary: Removes report rule user data for the current workflow.
export function removeReportRuleUser(id: string, userId: string) {
  return sendJson<EntityResponse<ReportUserAssignmentResponse>>(
    `/api/report-rules/${encodeURIComponent(id)}/users/${encodeURIComponent(userId)}`,
    'DELETE'
  );
}
// Function summary: Handles the query notifications workflow for this module.
export function queryNotifications(request: QueryNotificationsRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<QueryNotificationsResponse>(`/api/notifications?${params.toString()}`, options);
}
// Function summary: Retrieves notification data for callers.
export function getNotification(id: string) {
  return getJson<EntityResponse<NotificationDetailResponse>>(`/api/notifications/${encodeURIComponent(id)}`);
}
// Function summary: Handles the close notification workflow for this module.
export function closeNotification(id: string, request: NotificationCloseRequest) {
  return sendJson<EntityResponse<NotificationDetailResponse>>(`/api/notifications/${encodeURIComponent(id)}/close`, 'POST', request);
}
// Function summary: Handles the batch close notifications workflow for this module.
export function batchCloseNotifications(request: NotificationBatchCloseRequest) {
  return sendJson<NotificationBatchCloseResponse>('/api/notifications/batch-close', 'POST', request);
}
// Function summary: Handles the query alert levels workflow for this module.
export function queryAlertLevels(request: QueryAlertLevelsRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<QueryAlertLevelsResponse>(`/api/alert-levels?${params.toString()}`, options);
}
// Function summary: Retrieves alert level data for callers.
export function getAlertLevel(id: string) {
  return getJson<EntityResponse<AlertLevelItem>>(`/api/alert-levels/${encodeURIComponent(id)}`);
}
// Function summary: Retrieves alert level options data for callers.
export function getAlertLevelOptions(monitorId: string) {
  return getJson<AlertLevelOptionsResponse>(`/api/alert-levels/options?monitorId=${encodeURIComponent(monitorId)}`);
}
// Function summary: Creates alert level data for the current workflow.
export function createAlertLevel(request: AlertLevelMutationRequest) {
  return sendJson<EntityResponse<AlertLevelItem>>('/api/alert-levels', 'POST', request);
}
// Function summary: Updates alert level data for the current workflow.
export function updateAlertLevel(id: string, request: AlertLevelMutationRequest) {
  return sendJson<EntityResponse<AlertLevelItem>>(`/api/alert-levels/${encodeURIComponent(id)}`, 'PUT', request);
}
// Function summary: Updates vibration alert levels data for the current workflow.
export function updateVibrationAlertLevels(monitorId: string, request: VibrationAlertLevelMutationRequest) {
  return sendJson<VibrationAlertLevelResponse>(`/api/alert-levels/monitors/${encodeURIComponent(monitorId)}/vibration`, 'PUT', request);
}
// Function summary: Removes alert level data for the current workflow.
export function deleteAlertLevel(id: string) {
  return sendJson<MutationResponse>(`/api/alert-levels/${encodeURIComponent(id)}`, 'DELETE');
}
// Function summary: Retrieves dashboard summary data for callers.
export function getDashboardSummary(options: ApiRequestOptions = {}) {
  return getJson<DashboardSummaryResponse>('/api/dashboard/summary', options);
}
// Function summary: Handles the query breaches alerts workflow for this module.
export function queryBreachesAlerts(request: BreachesAlertsRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<BreachesAlertsResponse>(`/api/dashboard/breaches-alerts?${params.toString()}`, options);
}
// Function summary: Handles the query map markers workflow for this module.
export function queryMapMarkers(request: MapMarkersRequest | URLSearchParams = {}, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<MapMarkersResponse>(pathWithQuery('/api/dashboard/map-markers', params), options);
}
// Function summary: Retrieves calendar month data for callers.
export function getCalendarMonth(request: CalendarMonthRequest, options: ApiRequestOptions = {}) {
  const params = new URLSearchParams({ deploymentId: request.deploymentId });
  if (typeof request.year === 'number') {
    params.set('year', String(request.year));
  }
  if (typeof request.month === 'number') {
    params.set('month', String(request.month));
  }
  return getJson<CalendarMonthResponse>(`/api/dashboard/calendar/month?${params.toString()}`, options);
}
// Function summary: Retrieves calendar day data for callers.
export function getCalendarDay(request: CalendarDayRequest, options: ApiRequestOptions = {}) {
  const params = new URLSearchParams({
    monitorId: request.monitorId,
    year: String(request.year),
    month: String(request.month),
    day: String(request.day)
  });
  return getJson<CalendarDayResponse>(`/api/dashboard/calendar/day?${params.toString()}`, options);
}
// Function summary: Handles the query monitor data grid workflow for this module.
export function queryMonitorDataGrid(deploymentId: string, request: MonitorDataGridRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<MonitorDataGridResponse>(`/api/data/deployments/${encodeURIComponent(deploymentId)}/grid?${params.toString()}`, options);
}
// Function summary: Retrieves monitor graph data for callers.
export function getMonitorGraph(deploymentId: string, request: MonitorGraphRequest | URLSearchParams, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<MonitorGraphResponse>(`/api/data/deployments/${encodeURIComponent(deploymentId)}/graph?${params.toString()}`, options);
}
// Function summary: Handles the query monitor traces workflow for this module.
export function queryMonitorTraces(deploymentId: string, request: TraceListRequest | URLSearchParams = {}, options: ApiRequestOptions = {}) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return getJson<TraceListResponse>(`/api/data/deployments/${encodeURIComponent(deploymentId)}/traces?${params.toString()}`, options);
}
// Function summary: Retrieves monitor trace data for callers.
export function getMonitorTrace(deploymentId: string, traceId: string, options: ApiRequestOptions = {}) {
  return getJson<TraceDetailResponse>(
    `/api/data/deployments/${encodeURIComponent(deploymentId)}/traces/${encodeURIComponent(traceId)}`,
    options
  );
}
// Function summary: Handles the download monitor data csv workflow for this module.
export function downloadMonitorDataCsv(deploymentId: string, request: MonitorDataGridRequest | URLSearchParams) {
  const params = request instanceof URLSearchParams ? request : toSearchParams(request);
  return downloadFile(`/api/data/deployments/${encodeURIComponent(deploymentId)}/download?${params.toString()}`);
}
// Function summary: Handles the download monitor trace csv workflow for this module.
export function downloadMonitorTraceCsv(deploymentId: string, traceId: string) {
  return downloadFile(`/api/data/deployments/${encodeURIComponent(deploymentId)}/traces/${encodeURIComponent(traceId)}/download`);
}
// Function summary: Handles the search lookup workflow for this module.
export function searchLookup(
  kind: string,
  query: string,
  options: { take?: number; companyId?: string; includeAdmin?: boolean } = {},
  requestOptions: ApiRequestOptions = {}
) {
  const params = new URLSearchParams({ query, take: String(options.take ?? 8) });
  if (options.companyId) {
    params.set('companyId', options.companyId);
  }
  if (options.includeAdmin) {
    params.set('includeAdmin', 'true');
  }
  return getJson<SearchLookupResponse>(`/api/lookups/${encodeURIComponent(kind)}?${params.toString()}`, requestOptions);
}

export type DownloadedFile = {
  blob: Blob;
  fileName: string;
  contentType: string;
  correlationId?: string | null;
};

export async function downloadFile(path: string, init: RequestInit = {}): Promise<DownloadedFile> {
  const url = apiUrl(path);
  let response: Response;
  try {
    response = await fetch(url, {
      credentials: 'include',
      ...init
    });
  } catch {
    throw new Error(apiUnavailableMessage);
  }

  if (!response.ok) {
    const problem = await readProblemDetails(response);
    throw new ApiError(problem.message || `Download failed with ${response.status}`, response.status, problem.correlationId);
  }

  return {
    blob: await response.blob(),
    fileName: getFileName(response.headers.get('content-disposition')),
    contentType: response.headers.get('content-type') ?? 'application/octet-stream',
    correlationId: response.headers.get('x-correlation-id')
  };
}

const stringSearchParams = [
  'searchText',
  'sort',
  'sortDir',
  'companyId',
  'siteId',
  'state',
  'monitorType',
  'alertType',
  'monitorId',
  'date',
  'filterOption',
  'fromDate',
  'toDate'
] as const;
const numberSearchParams = ['page', 'pageSize'] as const;
const booleanSearchParams = ['openAlerts'] as const;

// Function summary: Handles the path with query workflow for this module.
function pathWithQuery(path: string, params: URLSearchParams) {
  const query = params.toString();
  return query ? `${path}?${query}` : path;
}

// Function summary: Handles the set string search param workflow for this module.
function setStringSearchParam(params: URLSearchParams, request: Record<string, unknown>, key: typeof stringSearchParams[number]) {
  const value = request[key];
  if (typeof value === 'string' && value) {
    params.set(key, value);
  }
}

// Function summary: Handles the set number search param workflow for this module.
function setNumberSearchParam(params: URLSearchParams, request: Record<string, unknown>, key: typeof numberSearchParams[number]) {
  const value = request[key];
  if (typeof value === 'number') {
    params.set(key, String(value));
  }
}

// Function summary: Handles the set boolean search param workflow for this module.
function setBooleanSearchParam(params: URLSearchParams, request: Record<string, unknown>, key: typeof booleanSearchParams[number]) {
  const value = request[key];
  if (typeof value === 'boolean') {
    params.set(key, String(value));
  }
}

// Function summary: Maps search params into the shape required by callers.
function toSearchParams(request: Record<string, unknown>) {
  const params = new URLSearchParams();

  stringSearchParams.forEach((key) => setStringSearchParam(params, request, key));
  numberSearchParams.forEach((key) => setNumberSearchParam(params, request, key));
  booleanSearchParams.forEach((key) => setBooleanSearchParam(params, request, key));

  if (request.includeArchived === true) {
    params.set('includeArchived', 'true');
  }

  return params;
}

// Function summary: Retrieves file name data for callers.
function getFileName(contentDisposition: string | null) {
  if (!contentDisposition) {
    return 'download';
  }

  const utf8FileName = /filename\*=UTF-8''([^;]+)/i.exec(contentDisposition)?.[1];
  if (utf8FileName) {
    return decodeURIComponent(utf8FileName);
  }

  return /filename="?([^";]+)"?/i.exec(contentDisposition)?.[1] ?? 'download';
}
