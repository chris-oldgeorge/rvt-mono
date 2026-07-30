// File summary: Renders the site create/edit form with operating hours and customer-logo management.
// Major updates:
// - 2026-07-30 pending Extracted from ContractSitePanels.tsx during the contracts/sites split.

import { Image as ImageIcon, Plus, Save, Trash2, Upload } from 'lucide-react';
import { useEffect, useState } from 'react';
import type { SyntheticEvent } from 'react';
import {
  createSite,
  deleteSiteCustomerLogo,
  getSite,
  getSiteOptions,
  updateSite,
  uploadSiteCustomerLogo,
} from '../api/client';
import { FormField, Notice, SubmitButton } from '../components/FormControls';
import { currentRoutePath, returnToOr, withReturnTo } from '../navigation';
import { normalizeOperatingHours, siteOperatingDays } from './panelShared';
import type { OperationsRouteProps } from './panelShared';
import type { SiteMutationRequest, SiteOperatingHours, SiteOptionsResponse } from '../dtos';

type SiteFormState = {
  siteName: string;
  companyId: string;
  contractId: string;
  addressLine1: string;
  addressLine2: string;
  postcode: string;
  city: string;
  county: string;
  operatingHours: SiteOperatingHours[];
};

// Function summary: Renders the SiteFormPanel React component and wires its local UI behavior.
export function SiteFormPanel({
  siteId,
  locationPath,
  onNavigate,
  onRequestError,
}: OperationsRouteProps & Readonly<{ siteId?: string }>) {
  const isEdit = Boolean(siteId);
  const backPath = returnToOr(locationPath, siteId ? `/sites/${siteId}` : '/sites');
  const formPath = currentRoutePath(locationPath);
  const [form, setForm] = useState<SiteFormState>({
    siteName: '',
    companyId: '',
    contractId: '',
    addressLine1: '',
    addressLine2: '',
    postcode: '',
    city: '',
    county: '',
    operatingHours: siteOperatingDays,
  });
  const [options, setOptions] = useState<SiteOptionsResponse>({ companies: [], contracts: [] });
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [customerLogoUrl, setCustomerLogoUrl] = useState<string | null>(null);
  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [logoStatus, setLogoStatus] = useState<string | null>(null);
  const [logoError, setLogoError] = useState<string | null>(null);
  const [isLogoSubmitting, setIsLogoSubmitting] = useState(false);
  const [logoInputKey, setLogoInputKey] = useState(0);
  const canSelectContract = !isEdit && Boolean(form.companyId);
  useEffect(() => {
    getSiteOptions()
      .then(setOptions)
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [onRequestError]);
  useEffect(() => {
    if (!siteId) {
      return;
    }
    getSite(siteId)
      .then((response) => {
        const item = response.item;
        if (item) {
          setForm({
            siteName: item.siteName,
            companyId: item.companyId ?? '',
            contractId: '',
            addressLine1: item.addressLine1 ?? '',
            addressLine2: item.addressLine2 ?? '',
            postcode: item.postcode ?? '',
            city: item.city ?? '',
            county: item.county ?? '',
            operatingHours: normalizeOperatingHours(item.operatingHours, item),
          });
          setCustomerLogoUrl(item.customerLogoUrl ?? null);
          setOptions({ companies: item.companies, contracts: item.availableContracts });
        }
      })
      .catch((err: Error) => {
        setError(err.message);
        onRequestError(err);
      });
  }, [onRequestError, siteId]);
  async function handleCompanyChange(companyId: string) {
    setForm((current) => ({ ...current, companyId, contractId: '' }));
    try {
      setOptions(await getSiteOptions(companyId || undefined));
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    }
  }
  function updateOperatingHours(dayOfWeek: number, patch: Partial<SiteOperatingHours>) {
    setForm((current) => ({
      ...current,
      operatingHours: current.operatingHours.map((hours) =>
        hours.dayOfWeek === dayOfWeek ? { ...hours, ...patch } : hours,
      ),
    }));
  }
  async function handleSubmit(event: SyntheticEvent) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);
    setStatus(null);
    try {
      const payload: SiteMutationRequest = {
        siteName: form.siteName,
        companyId: form.companyId,
        contractId: isEdit ? null : form.contractId || null,
        addressLine1: form.addressLine1 || null,
        addressLine2: form.addressLine2 || null,
        postcode: form.postcode || null,
        city: form.city || null,
        county: form.county || null,
        startTime: form.operatingHours.find((hours) => hours.dayOfWeek === 1)?.startTime || null,
        endTime: form.operatingHours.find((hours) => hours.dayOfWeek === 1)?.endTime || null,
        satStartTime: form.operatingHours.find((hours) => hours.dayOfWeek === 6)?.startTime || null,
        satEndTime: form.operatingHours.find((hours) => hours.dayOfWeek === 6)?.endTime || null,
        sunStartTime: form.operatingHours.find((hours) => hours.dayOfWeek === 7)?.startTime || null,
        sunEndTime: form.operatingHours.find((hours) => hours.dayOfWeek === 7)?.endTime || null,
        operatingHours: form.operatingHours,
      };
      const response = isEdit && siteId ? await updateSite(siteId, payload) : await createSite(payload);
      const saved = response.item;
      setStatus(isEdit ? 'Site updated.' : 'Site created.');
      if (saved?.id) {
        onNavigate(`/sites/${saved.id}`);
      }
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsSubmitting(false);
    }
  }
  function handleAddContract() {
    const query = form.companyId ? `?companyId=${encodeURIComponent(form.companyId)}` : '';
    onNavigate(withReturnTo(`/contracts/new${query}`, formPath));
  }
  async function handleUploadLogo() {
    if (!siteId || !logoFile) {
      setLogoError('Choose a customer logo image first.');
      return;
    }

    setIsLogoSubmitting(true);
    setLogoError(null);
    setLogoStatus(null);
    try {
      const response = await uploadSiteCustomerLogo(siteId, logoFile);
      setCustomerLogoUrl(response.item?.customerLogoUrl ?? null);
      setLogoFile(null);
      setLogoInputKey((current) => current + 1);
      setLogoStatus('Customer logo updated.');
    } catch (err) {
      setLogoError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsLogoSubmitting(false);
    }
  }
  async function handleDeleteLogo() {
    if (!siteId) {
      return;
    }

    setIsLogoSubmitting(true);
    setLogoError(null);
    setLogoStatus(null);
    try {
      const response = await deleteSiteCustomerLogo(siteId);
      setCustomerLogoUrl(response.item?.customerLogoUrl ?? null);
      setLogoFile(null);
      setLogoInputKey((current) => current + 1);
      setLogoStatus('Customer logo removed.');
    } catch (err) {
      setLogoError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsLogoSubmitting(false);
    }
  }
  return (
    <section className="panel narrow-panel">
      <div className="panel-heading">
        <div>
          <p>Site</p>
          <h2>{isEdit ? 'Edit Site' : 'Add Site'}</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate(backPath)}>
          Back
        </button>
      </div>
      <form className="form-grid compact-form" onSubmit={handleSubmit}>
        <FormField label="Site Name">
          <input
            value={form.siteName}
            onChange={(event) => setForm({ ...form, siteName: event.target.value })}
            maxLength={100}
          />
        </FormField>
        <FormField label="Company">
          <select
            value={form.companyId}
            onChange={(event) => handleCompanyChange(event.target.value)}
            disabled={isEdit}
          >
            <option value="">Select a Company</option>
            {options.companies.map((company) => (
              <option value={company.value} key={company.value}>
                {company.label}
              </option>
            ))}
          </select>
        </FormField>
        {!isEdit && (
          <div className="form-action-row">
            <button
              className="secondary-button inline"
              type="button"
              onClick={() => onNavigate(withReturnTo('/companies/new', formPath))}
            >
              <Plus size={16} aria-hidden="true" />
              <span>Add Company</span>
            </button>
          </div>
        )}
        {!isEdit && (
          <FormField label="Contract">
            <select
              value={form.contractId}
              onChange={(event) => setForm({ ...form, contractId: event.target.value })}
              disabled={!canSelectContract}
            >
              <option value="">Select a Contract</option>
              {options.contracts.map((contract) => (
                <option value={contract.value} key={contract.value}>
                  {contract.label}
                </option>
              ))}
            </select>
          </FormField>
        )}
        {!isEdit && (
          <div className="form-action-row">
            <button
              className="secondary-button inline"
              type="button"
              onClick={handleAddContract}
              disabled={!canSelectContract}
            >
              <Plus size={16} aria-hidden="true" />
              <span>Add Contract</span>
            </button>
          </div>
        )}
        {isEdit && (
          <section className="customer-logo-section" aria-label="Customer logo">
            <div className="section-heading">
              <ImageIcon size={18} aria-hidden="true" />
              <h3>Customer Logo</h3>
            </div>
            {customerLogoUrl ? (
              <img className="customer-logo-preview" src={customerLogoUrl} alt="Customer logo" />
            ) : (
              <p className="muted-text">No customer logo set.</p>
            )}
            <FormField label="Customer logo image">
              <input
                key={logoInputKey}
                accept="image/png,image/jpeg,image/webp"
                type="file"
                onChange={(event) => setLogoFile(event.target.files?.[0] ?? null)}
              />
            </FormField>
            <div className="customer-logo-actions">
              <button
                className="secondary-button"
                type="button"
                onClick={handleUploadLogo}
                disabled={isLogoSubmitting || !logoFile}
              >
                <Upload size={16} aria-hidden="true" />
                <span>Upload Logo</span>
              </button>
              <button
                className="secondary-button danger"
                type="button"
                onClick={handleDeleteLogo}
                disabled={isLogoSubmitting || !customerLogoUrl}
              >
                <Trash2 size={16} aria-hidden="true" />
                <span>Delete Logo</span>
              </button>
            </div>
            {logoStatus && <Notice tone="success" message={logoStatus} />}
            {logoError && <Notice tone="error" message={logoError} />}
          </section>
        )}
        <FormField label="Address Line 1">
          <input
            value={form.addressLine1}
            onChange={(event) => setForm({ ...form, addressLine1: event.target.value })}
            maxLength={100}
          />
        </FormField>
        <FormField label="Address Line 2">
          <input
            value={form.addressLine2}
            onChange={(event) => setForm({ ...form, addressLine2: event.target.value })}
            maxLength={100}
          />
        </FormField>
        <FormField label="Postcode">
          <input
            value={form.postcode}
            onChange={(event) => setForm({ ...form, postcode: event.target.value })}
            maxLength={20}
          />
        </FormField>
        <FormField label="City">
          <input
            value={form.city}
            onChange={(event) => setForm({ ...form, city: event.target.value })}
            maxLength={100}
          />
        </FormField>
        <FormField label="County">
          <input
            value={form.county}
            onChange={(event) => setForm({ ...form, county: event.target.value })}
            maxLength={100}
          />
        </FormField>
        <div className="time-grid daily-hours-grid">
          {form.operatingHours.map((hours) => (
            <div className="daily-hours-row" key={hours.dayOfWeek}>
              <span className="daily-hours-label">{hours.dayName}</span>
              <label className="checkbox-row">
                <input
                  checked={hours.isClosed}
                  onChange={(event) => updateOperatingHours(hours.dayOfWeek, { isClosed: event.target.checked })}
                  type="checkbox"
                />
                <span>{hours.dayName} Closed</span>
              </label>
              <FormField label={`${hours.dayName} Start`}>
                <input
                  value={hours.startTime ?? ''}
                  onChange={(event) => updateOperatingHours(hours.dayOfWeek, { startTime: event.target.value })}
                  type="time"
                  disabled={hours.isClosed}
                />
              </FormField>
              <FormField label={`${hours.dayName} End`}>
                <input
                  value={hours.endTime ?? ''}
                  onChange={(event) => updateOperatingHours(hours.dayOfWeek, { endTime: event.target.value })}
                  type="time"
                  disabled={hours.isClosed}
                />
              </FormField>
            </div>
          ))}
        </div>
        {status && <Notice tone="success" message={status} />}
        {error && <Notice tone="error" message={error} />}
        <SubmitButton
          icon={<Save size={17} aria-hidden="true" />}
          isSubmitting={isSubmitting}
          idleLabel={isEdit ? 'Update Site' : 'Create Site'}
        />
      </form>
    </section>
  );
}
