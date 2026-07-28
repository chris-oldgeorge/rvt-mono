import type {
  SiteNotificationSettingItem,
  SiteNotificationSettingMutationRequest,
} from '../dtos';

export type NotificationDraftOverrides = Record<
  string,
  SiteNotificationSettingMutationRequest
>;

export function notificationSettingDraft(
  setting: SiteNotificationSettingItem,
  overrides: NotificationDraftOverrides,
): SiteNotificationSettingMutationRequest {
  return overrides[setting.siteUserId] ?? {
    email: setting.email,
    sms: setting.sms,
    startTime: setting.startTime ?? '',
    endTime: setting.endTime ?? '',
  };
}

export function withoutNotificationDraft(
  overrides: NotificationDraftOverrides,
  siteUserId: string,
): NotificationDraftOverrides {
  return Object.fromEntries(
    Object.entries(overrides).filter(([key]) => key !== siteUserId),
  );
}
