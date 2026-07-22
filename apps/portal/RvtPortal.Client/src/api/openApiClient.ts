// File summary: Provides the SPA API contract boundary backed by the generated OpenAPI schema.
// Major updates:
// - 2026-06-26 pending Pointed contract sentinels at generated schema keys after DTO facade conversion.
// - 2026-06-26 pending Restored the OpenAPI facade so client.ts no longer imports hand-maintained DTOs directly.

import type { components } from './schema';

export type * from '../dtos';

type Schema = components['schemas'];
type Assert<T extends true> = T;
type HasKey<T, TKey extends PropertyKey> = TKey extends keyof T ? true : false;

export type OpenApiClientContractSentinels = [
  Assert<HasKey<Schema['AlertLevelMutationRequest'], 'monitorId'>>,
  Assert<HasKey<Schema['AlertLevelMutationRequest'], 'alertField'>>,
  Assert<HasKey<Schema['AlertLevelMutationRequest'], 'alertType'>>,
  Assert<HasKey<Schema['CompanyMutationRequest'], 'companyName'>>,
  Assert<Schema['SiteUserMutationRequest'] extends { siteId: string; userId: string } ? true : false>,
  Assert<Schema['VibrationAlertLevelMutationRequest'] extends { alertLevel: number; cautionLevel: number } ? true : false>,
  Assert<HasKey<Schema['LoginRequest'], 'email'>>,
  Assert<HasKey<Schema['ReportUserMutationRequest'], 'userId'>>
];
