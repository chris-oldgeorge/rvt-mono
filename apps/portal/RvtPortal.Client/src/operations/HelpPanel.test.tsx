import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { HelpArticleResponse, HelpOverviewResponse } from '../dtos';
import { HelpPanel } from './HelpPanel';

const api = vi.hoisted(() => ({
  getHelpArticle: vi.fn(),
  queryHelp: vi.fn(),
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

function overview(title: string, searchText: string): HelpOverviewResponse {
  const slug = title.toLowerCase().replaceAll(' ', '-');
  return {
    searchText,
    sections: [{
      id: slug,
      title: 'General',
      slug: 'general',
      sortOrder: 1,
      articles: [{
        id: slug,
        title,
        slug,
        summary: `${title} summary`,
        contentType: 'FAQ',
        sectionTitle: 'General',
        sectionSlug: 'general',
        sectionSortOrder: 1,
        sortOrder: 1,
      }],
    }],
  };
}

function article(title: string, slug: string): { item: HelpArticleResponse } {
  return {
    item: {
      id: slug,
      title,
      slug,
      summary: `${title} summary`,
      body: `${title} body`,
      contentType: 'FAQ',
      sectionTitle: 'General',
      sectionSlug: 'general',
      sectionSortOrder: 1,
      sortOrder: 1,
      isPublished: true,
      createdAtUtc: '2026-07-28T08:00:00Z',
      updatedAtUtc: '2026-07-28T08:00:00Z',
      assets: [],
    },
  };
}

describe('HelpPanel request ownership', () => {
  beforeEach(() => {
    api.getHelpArticle.mockReset();
    api.queryHelp.mockReset();
  });

  it('does not recycle an earlier overview completion when search returns to the same text', async () => {
    const beta = deferred<HelpOverviewResponse>();
    const alphaAgain = deferred<HelpOverviewResponse>();
    api.queryHelp
      .mockResolvedValueOnce(overview('Initial Help', ''))
      .mockResolvedValueOnce(overview('Old Alpha Help', 'alpha'))
      .mockReturnValueOnce(beta.promise)
      .mockReturnValueOnce(alphaAgain.promise);

    render(<HelpPanel locationPath="/help" onNavigate={vi.fn()} onRequestError={vi.fn()} />);

    const search = await screen.findByPlaceholderText('Search help');
    fireEvent.change(search, { target: { value: 'alpha' } });
    expect(await screen.findByText('Old Alpha Help')).toBeInTheDocument();
    fireEvent.change(search, { target: { value: 'beta' } });
    await waitFor(() => expect(api.queryHelp).toHaveBeenCalledTimes(3));
    fireEvent.change(search, { target: { value: 'alpha' } });
    await waitFor(() => expect(api.queryHelp).toHaveBeenCalledTimes(4));

    expect(screen.getByText('Loading help...')).toBeInTheDocument();
    expect(screen.queryByText('Old Alpha Help')).not.toBeInTheDocument();

    await act(async () => alphaAgain.resolve(overview('Fresh Alpha Help', 'alpha')));
    expect(await screen.findByText('Fresh Alpha Help')).toBeInTheDocument();
    await act(async () => beta.resolve(overview('Late Beta Help', 'beta')));
    expect(screen.getByText('Fresh Alpha Help')).toBeInTheDocument();
  });

  it('does not recycle an earlier article completion when navigation returns to the same slug', async () => {
    const beta = deferred<{ item: HelpArticleResponse }>();
    const alphaAgain = deferred<{ item: HelpArticleResponse }>();
    api.getHelpArticle
      .mockResolvedValueOnce(article('Old Alpha Article', 'alpha'))
      .mockReturnValueOnce(beta.promise)
      .mockReturnValueOnce(alphaAgain.promise);

    const view = render(
      <HelpPanel locationPath="/help/alpha" onNavigate={vi.fn()} onRequestError={vi.fn()} />,
    );

    expect(await screen.findByRole('heading', { name: 'Old Alpha Article' })).toBeInTheDocument();
    view.rerender(<HelpPanel locationPath="/help/beta" onNavigate={vi.fn()} onRequestError={vi.fn()} />);
    await waitFor(() => expect(api.getHelpArticle).toHaveBeenCalledTimes(2));
    view.rerender(<HelpPanel locationPath="/help/alpha" onNavigate={vi.fn()} onRequestError={vi.fn()} />);
    await waitFor(() => expect(api.getHelpArticle).toHaveBeenCalledTimes(3));

    expect(screen.getByText('Loading article...')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Old Alpha Article' })).not.toBeInTheDocument();

    await act(async () => alphaAgain.resolve(article('Fresh Alpha Article', 'alpha')));
    expect(await screen.findByRole('heading', { name: 'Fresh Alpha Article' })).toBeInTheDocument();
    await act(async () => beta.resolve(article('Late Beta Article', 'beta')));
    expect(screen.getByRole('heading', { name: 'Fresh Alpha Article' })).toBeInTheDocument();
  });
});
