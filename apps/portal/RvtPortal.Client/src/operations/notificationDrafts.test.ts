import { describe, expect, it } from 'vitest';
import type { SiteNotificationSettingItem } from '../dtos';
import {
  notificationSettingDraft,
  withoutNotificationDraft,
} from './notificationDrafts';

const setting = {
  siteUserId: 'site-user-1',
  email: true,
  sms: false,
  startTime: '08:00',
  endTime: '18:00',
} as SiteNotificationSettingItem;

describe('notification draft ownership', () => {
  it('prefers a user override without copying every server setting', () => {
    const override = {
      email: false,
      sms: true,
      startTime: '09:00',
      endTime: '17:00',
    };

    expect(
      notificationSettingDraft(setting, { [setting.siteUserId]: override }),
    ).toEqual(override);
    expect(notificationSettingDraft(setting, {})).toEqual({
      email: true,
      sms: false,
      startTime: '08:00',
      endTime: '18:00',
    });
  });

  it('removes only the successfully saved override', () => {
    const overrides = {
      'site-user-1': { email: false, sms: true, startTime: '', endTime: '' },
      'site-user-2': { email: true, sms: true, startTime: '', endTime: '' },
    };

    expect(withoutNotificationDraft(overrides, 'site-user-1')).toEqual({
      'site-user-2': overrides['site-user-2'],
    });
  });
});
