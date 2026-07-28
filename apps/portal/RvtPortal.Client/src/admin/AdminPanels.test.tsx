import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type {
  CompanyListItem,
  PagedResponse,
  SearchLookupResponse,
  UserListItem,
} from '../dtos';
import { CompaniesPanel, UsersPanel } from './AdminPanels';

const api = vi.hoisted(() => ({
  queryCompanies: vi.fn(),
  queryUsers: vi.fn(),
  searchLookup: vi.fn(),
}));

vi.mock('../api/client', async () => ({
  ...(await vi.importActual('../api/client')),
  ...api,
}));

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((next) => {
    resolve = next;
  });
  return { promise, resolve };
}

function companyResponse(name: string): PagedResponse<CompanyListItem> {
  return {
    results: [{ id: name, companyName: name, userCount: 1, sites: '1', contracts: '1' }],
    total: 1,
    page: 1,
    pageSize: 10,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
  };
}

function userResponse(email: string, companyName: string): PagedResponse<UserListItem> {
  return {
    results: [{
      id: email,
      name: email,
      companyName,
      email,
      phoneNumber: '',
      role: 'CompanyUser',
      siteCount: 0,
      emailConfirmed: true,
      isDisabled: false,
    }],
    total: 1,
    page: 1,
    pageSize: 10,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
  };
}

function lookup(query: string, results: string[]): SearchLookupResponse {
  return { kind: 'companies', query, take: 8, results };
}

describe('AdminPanels request ownership', () => {
  beforeEach(() => {
    api.queryCompanies.mockReset();
    api.queryUsers.mockReset();
    api.searchLookup.mockReset();
  });

  it('does not recycle completed company rows when the search returns to an earlier value', async () => {
    const beta = deferred<PagedResponse<CompanyListItem>>();
    const alphaAgain = deferred<PagedResponse<CompanyListItem>>();
    api.queryCompanies
      .mockResolvedValueOnce(companyResponse('Old Alpha Company'))
      .mockReturnValueOnce(beta.promise)
      .mockReturnValueOnce(alphaAgain.promise);
    api.searchLookup.mockResolvedValue(lookup('alpha', []));

    render(
      <CompaniesPanel
        locationPath="/companies?q=alpha"
        onNavigate={vi.fn()}
        onRequestError={vi.fn()}
      />,
    );

    expect(await screen.findByText('Old Alpha Company')).toBeInTheDocument();
    const search = screen.getByPlaceholderText('Search companies');
    fireEvent.change(search, { target: { value: 'beta' } });
    await waitFor(() => expect(api.queryCompanies).toHaveBeenCalledTimes(2));
    fireEvent.change(search, { target: { value: 'alpha' } });
    await waitFor(() => expect(api.queryCompanies).toHaveBeenCalledTimes(3));

    expect(screen.getByText('Loading data...')).toBeInTheDocument();
    expect(screen.queryByText('Old Alpha Company')).not.toBeInTheDocument();

    await act(async () => alphaAgain.resolve(companyResponse('Fresh Alpha Company')));
    expect(await screen.findByText('Fresh Alpha Company')).toBeInTheDocument();
    await act(async () => beta.resolve(companyResponse('Late Beta Company')));
    expect(screen.getByText('Fresh Alpha Company')).toBeInTheDocument();
  });

  it('does not recycle company suggestions when a query executes again', async () => {
    const beta = deferred<SearchLookupResponse>();
    const alphaAgain = deferred<SearchLookupResponse>();
    api.queryCompanies.mockResolvedValue(companyResponse('Company row'));
    api.searchLookup
      .mockResolvedValueOnce(lookup('alpha', ['Old Alpha Suggestion']))
      .mockReturnValueOnce(beta.promise)
      .mockReturnValueOnce(alphaAgain.promise);

    render(
      <CompaniesPanel
        locationPath="/companies?q=alpha"
        onNavigate={vi.fn()}
        onRequestError={vi.fn()}
      />,
    );

    expect(await screen.findByRole('button', { name: 'Old Alpha Suggestion' })).toBeInTheDocument();
    const search = screen.getByPlaceholderText('Search companies');
    fireEvent.change(search, { target: { value: 'beta' } });
    await waitFor(() => expect(api.searchLookup).toHaveBeenCalledTimes(2));
    fireEvent.change(search, { target: { value: 'alpha' } });
    await waitFor(() => expect(api.searchLookup).toHaveBeenCalledTimes(3));

    expect(screen.queryByRole('button', { name: 'Old Alpha Suggestion' })).not.toBeInTheDocument();
    await act(async () => alphaAgain.resolve(lookup('alpha', ['Fresh Alpha Suggestion'])));
    expect(await screen.findByRole('button', { name: 'Fresh Alpha Suggestion' })).toBeInTheDocument();
    await act(async () => beta.resolve(lookup('beta', ['Late Beta Suggestion'])));
    expect(screen.getByRole('button', { name: 'Fresh Alpha Suggestion' })).toBeInTheDocument();
  });

  it('owns user rows and suggestions by execution and company scope', async () => {
    const companyBRows = deferred<PagedResponse<UserListItem>>();
    const companyAAgainRows = deferred<PagedResponse<UserListItem>>();
    const companyBSuggestions = deferred<SearchLookupResponse>();
    const companyAAgainSuggestions = deferred<SearchLookupResponse>();
    api.queryUsers
      .mockResolvedValueOnce(userResponse('old-a@rvt.test', 'Company A'))
      .mockReturnValueOnce(companyBRows.promise)
      .mockReturnValueOnce(companyAAgainRows.promise);
    api.searchLookup
      .mockResolvedValueOnce(lookup('alpha', ['Old Company A User']))
      .mockReturnValueOnce(companyBSuggestions.promise)
      .mockReturnValueOnce(companyAAgainSuggestions.promise);

    const view = render(
      <UsersPanel
        locationPath="/users?companyId=company-a&companyName=Company%20A&q=alpha"
        onNavigate={vi.fn()}
        onRequestError={vi.fn()}
      />,
    );

    expect(await screen.findAllByText('old-a@rvt.test')).toHaveLength(2);
    expect(await screen.findByRole('button', { name: 'Old Company A User' })).toBeInTheDocument();

    view.rerender(
      <UsersPanel
        locationPath="/users?companyId=company-b&companyName=Company%20B&q=alpha"
        onNavigate={vi.fn()}
        onRequestError={vi.fn()}
      />,
    );
    await waitFor(() => expect(api.queryUsers).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(api.searchLookup).toHaveBeenCalledTimes(2));
    expect(screen.queryByRole('button', { name: 'Old Company A User' })).not.toBeInTheDocument();

    view.rerender(
      <UsersPanel
        locationPath="/users?companyId=company-a&companyName=Company%20A&q=alpha"
        onNavigate={vi.fn()}
        onRequestError={vi.fn()}
      />,
    );
    await waitFor(() => expect(api.queryUsers).toHaveBeenCalledTimes(3));
    await waitFor(() => expect(api.searchLookup).toHaveBeenCalledTimes(3));
    expect(screen.getByText('Loading data...')).toBeInTheDocument();
    expect(screen.queryAllByText('old-a@rvt.test')).toHaveLength(0);
    expect(screen.queryByRole('button', { name: 'Old Company A User' })).not.toBeInTheDocument();

    await act(async () => {
      companyAAgainRows.resolve(userResponse('fresh-a@rvt.test', 'Company A'));
      companyAAgainSuggestions.resolve(lookup('alpha', ['Fresh Company A User']));
    });
    expect(await screen.findAllByText('fresh-a@rvt.test')).toHaveLength(2);
    expect(await screen.findByRole('button', { name: 'Fresh Company A User' })).toBeInTheDocument();

    await act(async () => {
      companyBRows.resolve(userResponse('late-b@rvt.test', 'Company B'));
      companyBSuggestions.resolve(lookup('alpha', ['Late Company B User']));
    });
    expect(screen.getAllByText('fresh-a@rvt.test')).toHaveLength(2);
    expect(screen.getByRole('button', { name: 'Fresh Company A User' })).toBeInTheDocument();
  });
});
