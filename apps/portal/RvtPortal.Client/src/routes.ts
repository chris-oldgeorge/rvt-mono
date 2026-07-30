// File summary: Defines SPA route tables, role constants, and navigation helpers shared by the app entry and shell.
// Major updates:
// - 2026-07-30 pending Extracted from App.tsx so routing data has one home outside the entry component.

import {
  Activity,
  BarChart3,
  Bell,
  Building2,
  CalendarDays,
  FileText,
  Gauge,
  HelpCircle,
  Map as MapIcon,
  MapPinned,
  UserRound,
  UsersRound,
  type LucideIcon,
} from 'lucide-react';
import type { AuthUser } from './dtos';

export const roleNames = {
  masterAdmin: 'RVTMasterAdmin',
  admin: 'RVTAdmin',
  installer: 'RVTInstaller',
  companyUser: 'CompanyUser',
} as const;

export const adminRoles = [roleNames.masterAdmin, roleNames.admin];

export type PublicRoute = 'login' | 'forgot-password' | 'reset-password' | 'confirm-email' | 'privacy';
export type ProtectedRoute =
  | 'dashboard'
  | 'maps'
  | 'calendar'
  | 'data'
  | 'sites'
  | 'contracts'
  | 'monitors'
  | 'notifications'
  | 'reports'
  | 'admin-help'
  | 'help'
  | 'companies'
  | 'users'
  | 'profile'
  | 'access-denied'
  | 'not-found';
export type AppRoute = PublicRoute | ProtectedRoute;

export type NavigationItem = {
  name: string;
  path: string;
  route: ProtectedRoute;
  icon: LucideIcon;
  state: string;
  roles?: string[];
  group?: 'primary' | 'admin' | 'secondary' | 'account';
};

const navigationItems: NavigationItem[] = [
  { name: 'Home', path: '/', route: 'dashboard', icon: Activity, state: 'Protected', group: 'primary' },
  {
    name: 'Maps',
    path: '/maps',
    route: 'maps',
    icon: MapIcon,
    state: 'Migrated',
    group: 'secondary',
    roles: [...adminRoles, roleNames.companyUser],
  },
  {
    name: 'Calendar',
    path: '/calendar',
    route: 'calendar',
    icon: CalendarDays,
    state: 'Migrated',
    group: 'secondary',
    roles: [...adminRoles, roleNames.companyUser],
  },
  {
    name: 'Data',
    path: '/data',
    route: 'data',
    icon: BarChart3,
    state: 'Migrated',
    group: 'secondary',
    roles: [...adminRoles, roleNames.companyUser],
  },
  {
    name: 'Sites',
    path: '/sites',
    route: 'sites',
    icon: MapPinned,
    state: 'Migrated',
    group: 'primary',
    roles: [...adminRoles, roleNames.companyUser],
  },
  {
    name: 'Contracts',
    path: '/contracts',
    route: 'contracts',
    icon: FileText,
    state: 'Admin only',
    group: 'admin',
    roles: adminRoles,
  },
  {
    name: 'Monitors',
    path: '/monitors',
    route: 'monitors',
    icon: Gauge,
    state: 'Migrated',
    group: 'primary',
    roles: [...adminRoles, roleNames.companyUser, roleNames.installer],
  },
  {
    name: 'Notifications',
    path: '/notifications',
    route: 'notifications',
    icon: Bell,
    state: 'Migrated',
    group: 'secondary',
    roles: [...adminRoles, roleNames.companyUser],
  },
  {
    name: 'Reports',
    path: '/reports',
    route: 'reports',
    icon: FileText,
    state: 'Migrated',
    group: 'admin',
    roles: adminRoles,
  },
  {
    name: 'Help/FAQ',
    path: '/admin/help',
    route: 'admin-help',
    icon: HelpCircle,
    state: 'Admin only',
    group: 'admin',
    roles: adminRoles,
  },
  {
    name: 'Help',
    path: '/help',
    route: 'help',
    icon: HelpCircle,
    state: 'Migrated',
    group: 'secondary',
    roles: [...adminRoles, roleNames.companyUser],
  },
  {
    name: 'Companies',
    path: '/companies',
    route: 'companies',
    icon: Building2,
    state: 'Admin only',
    group: 'admin',
    roles: adminRoles,
  },
  {
    name: 'Users',
    path: '/users',
    route: 'users',
    icon: UsersRound,
    state: 'Admin only',
    group: 'admin',
    roles: adminRoles,
  },
  { name: 'Account', path: '/profile', route: 'profile', icon: UserRound, state: 'Self service', group: 'account' },
];

const exactRoutes: Readonly<Record<string, AppRoute>> = {
  '/': 'dashboard',
  '/forgot-password': 'forgot-password',
  '/reset-password': 'reset-password',
  '/confirm-email': 'confirm-email',
  '/privacy': 'privacy',
  '/login': 'login',
  '/profile': 'profile',
  '/access-denied': 'access-denied',
};
const prefixRoutes: ReadonlyArray<readonly [string, ProtectedRoute]> = [
  ['/admin/help', 'admin-help'],
  ['/maps', 'maps'],
  ['/calendar', 'calendar'],
  ['/data', 'data'],
  ['/sites', 'sites'],
  ['/contracts', 'contracts'],
  ['/monitors', 'monitors'],
  ['/notifications', 'notifications'],
  ['/reports', 'reports'],
  ['/help', 'help'],
  ['/companies', 'companies'],
  ['/users', 'users'],
];

// Function summary: Retrieves route from location data for callers.
export function getRouteFromLocation(): AppRoute {
  const path = globalThis.location.pathname.toLowerCase();
  return exactRoutes[path] ?? prefixRoutes.find(([prefix]) => path.startsWith(prefix))?.[1] ?? 'not-found';
}

// Function summary: Navigates the SPA to the requested route.
export function navigate(path: string) {
  globalThis.history.pushState(null, '', path);
  globalThis.dispatchEvent(new globalThis.PopStateEvent('popstate'));
}

// Function summary: Retrieves user roles data for callers.
export function getUserRoles(user?: AuthUser | null) {
  return user?.roles ?? [];
}

// Function summary: Evaluates any role for the current decision point.
export function hasAnyRole(user: AuthUser | null | undefined, roles?: string[]) {
  if (!roles || roles.length === 0) {
    return true;
  }
  const userRoles = getUserRoles(user);
  return roles.some((role) => userRoles.includes(role));
}

// Function summary: Handles the visible navigation workflow for this module.
export function visibleNavigation(user: AuthUser | null | undefined) {
  return navigationItems.filter((item) => hasAnyRole(user, item.roles));
}

// Function summary: Retrieves navigation group data for the legacy-compatible shell menu.
export function navigationGroup(items: NavigationItem[], group: NonNullable<NavigationItem['group']>) {
  return items.filter((item) => item.group === group);
}

// Function summary: Evaluates access route for the current decision point.
export function canAccessRoute(route: ProtectedRoute, user: AuthUser | null | undefined) {
  if (route === 'access-denied' || route === 'not-found') {
    return true;
  }
  const navigationItem = navigationItems.find((item) => item.route === route);
  return navigationItem ? hasAnyRole(user, navigationItem.roles) : false;
}

// Function summary: Handles the route path workflow for this module.
export function routePath(route: ProtectedRoute) {
  if (route === 'dashboard') {
    return '/';
  }
  return `/${route}`;
}

// Function summary: Handles the current location path workflow for this module.
export function currentLocationPath() {
  const pathname = globalThis.location.pathname.startsWith('/') ? globalThis.location.pathname : '/';
  const search = globalThis.location.search.startsWith('?') ? globalThis.location.search : '';
  return `${pathname}${search}`;
}
