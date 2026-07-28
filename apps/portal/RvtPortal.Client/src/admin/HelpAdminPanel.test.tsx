// File summary: Covers stable identity, focus restoration, and canonical Help Admin mutations.
// Major updates:
// - 2026-07-28 Added focused Help Admin workflow regressions.

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { HelpAdminOverviewResponse, HelpArticleResponse } from '../dtos';
import { HelpAdminPanel } from './HelpAdminPanel';

const api = vi.hoisted(() => ({
  createHelpArticle: vi.fn(),
  deleteHelpArticle: vi.fn(),
  queryAdminHelp: vi.fn(),
  setHelpArticlePublication: vi.fn(),
  updateHelpArticle: vi.fn(),
}));

vi.mock('../api/client', () => ({
  ...api,
  isAbortError: () => false,
}));

const existingArticle: HelpArticleResponse = {
  id: 'article-1',
  title: 'Dust FAQ',
  slug: 'dust-faq',
  summary: 'Dust summary',
  body: 'Dust body',
  contentType: 'FAQ',
  sectionTitle: 'Data',
  sectionSlug: 'data',
  sectionSortOrder: 1,
  sortOrder: 1,
  isPublished: false,
  createdAtUtc: '2026-07-28T08:00:00Z',
  updatedAtUtc: '2026-07-28T08:00:00Z',
  assets: [
    {
      id: 'asset-1',
      title: 'Dust monitoring guide',
      assetType: 'Document',
      url: '/help-assets/dust.pdf',
      internalPath: '/help-assets/dust.pdf',
      sortOrder: 1,
    },
  ],
};

const secondArticle: HelpArticleResponse = {
  ...existingArticle,
  id: 'article-2',
  title: 'Noise FAQ',
  slug: 'noise-faq',
  assets: [],
};

function overview(articles: HelpArticleResponse[]): HelpAdminOverviewResponse {
  return {
    searchText: '',
    status: 'All',
    contentType: 'All',
    sections: [],
    articles,
  };
}

function renderHelpAdmin() {
  return render(<HelpAdminPanel onNavigate={vi.fn()} onRequestError={vi.fn()} />);
}

describe('HelpAdminPanel', () => {
  beforeEach(() => {
    api.createHelpArticle.mockReset();
    api.deleteHelpArticle.mockReset();
    api.queryAdminHelp.mockReset();
    api.setHelpArticlePublication.mockReset();
    api.updateHelpArticle.mockReset();
    api.queryAdminHelp.mockResolvedValue(overview([existingArticle]));
    api.deleteHelpArticle.mockResolvedValue({
      id: existingArticle.id,
      message: 'Help article removed.',
    });
    api.setHelpArticlePublication.mockResolvedValue({
      item: { ...existingArticle, isPublished: true },
    });
    api.updateHelpArticle.mockImplementation(async (_id, request) => ({
      item: {
        ...existingArticle,
        ...request,
        assets: existingArticle.assets,
      },
    }));
  });

  it('keeps an asset row mounted while its editable title changes', async () => {
    renderHelpAdmin();
    fireEvent.click(await screen.findByRole('button', { name: 'Edit Dust FAQ' }));
    const title = await screen.findByDisplayValue('Dust monitoring guide');
    title.focus();
    fireEvent.change(title, { target: { value: 'Updated guide' } });

    expect(screen.getByDisplayValue('Updated guide')).toHaveFocus();
  });

  it('submits persisted asset IDs without internal or client-only metadata', async () => {
    renderHelpAdmin();
    fireEvent.click(await screen.findByRole('button', { name: 'Edit Dust FAQ' }));
    fireEvent.click(screen.getByRole('button', { name: 'Save FAQ' }));

    await waitFor(() => expect(api.updateHelpArticle).toHaveBeenCalled());
    const request = api.updateHelpArticle.mock.calls[0][1];
    expect(request.assets).toEqual([
      {
        id: 'asset-1',
        title: 'Dust monitoring guide',
        assetType: 'Document',
        url: '/help-assets/dust.pdf',
        sortOrder: 1,
      },
    ]);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Edit Dust FAQ' })).toHaveFocus());
  });

  it('focuses new asset rows and a deterministic fallback after removal', async () => {
    renderHelpAdmin();
    fireEvent.click(await screen.findByRole('button', { name: 'Edit Dust FAQ' }));
    fireEvent.click(screen.getByRole('button', { name: 'Add Asset' }));

    const emptyTitle = (await screen.findAllByPlaceholderText('Title')).at(-1);
    expect(emptyTitle).toBeDefined();
    expect(emptyTitle).toHaveFocus();
    fireEvent.click(screen.getAllByRole('button', { name: 'Remove asset' })[1]);

    expect(screen.getByDisplayValue('Dust monitoring guide')).toHaveFocus();
  });

  it('focuses the saved article edit action after create', async () => {
    const createdArticle = {
      ...existingArticle,
      id: 'article-2',
      title: 'New FAQ',
      slug: 'new-faq',
      assets: [],
    };
    api.createHelpArticle.mockImplementation(async () => {
      api.queryAdminHelp.mockResolvedValue(overview([existingArticle, createdArticle]));
      return { item: createdArticle };
    });
    renderHelpAdmin();
    fireEvent.click(await screen.findByRole('button', { name: 'New FAQ' }));
    fireEvent.change(screen.getByLabelText('Title'), {
      target: { value: 'New FAQ' },
    });
    fireEvent.change(screen.getByLabelText('Body'), {
      target: { value: 'New body' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Create FAQ' }));

    expect(await screen.findByRole('button', { name: 'Edit New FAQ' })).toHaveFocus();
  });

  it('waits for the refreshed overview before focusing the changed publication action', async () => {
    let resolveRefresh!: (value: HelpAdminOverviewResponse) => void;
    api.queryAdminHelp.mockResolvedValueOnce(overview([existingArticle])).mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveRefresh = resolve;
        }),
    );
    renderHelpAdmin();
    const publish = await screen.findByRole('button', {
      name: 'Publish Dust FAQ',
    });
    publish.focus();
    fireEvent.click(publish);

    await waitFor(() => expect(api.queryAdminHelp).toHaveBeenCalledTimes(2));
    expect(publish).toHaveFocus();
    await act(async () => resolveRefresh(overview([{ ...existingArticle, isPublished: true }])));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Unpublish Dust FAQ' })).toHaveFocus());
  });

  it('keeps newer filter results when an older mutation refresh resolves last', async () => {
    let resolveMutationRefresh!: (value: HelpAdminOverviewResponse) => void;
    let resolveDraftFilter!: (value: HelpAdminOverviewResponse) => void;
    api.queryAdminHelp
      .mockResolvedValueOnce(overview([existingArticle]))
      .mockImplementationOnce(
        () => new Promise((resolve) => {
          resolveMutationRefresh = resolve;
        }),
      )
      .mockImplementationOnce(
        () => new Promise((resolve) => {
          resolveDraftFilter = resolve;
        }),
      );
    renderHelpAdmin();

    fireEvent.click(await screen.findByRole('button', { name: 'Publish Dust FAQ' }));
    await waitFor(() => expect(api.queryAdminHelp).toHaveBeenCalledTimes(2));
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'Draft' } });
    await waitFor(() => expect(api.queryAdminHelp).toHaveBeenCalledTimes(3));

    await act(async () => resolveDraftFilter(overview([secondArticle])));
    expect(await screen.findByRole('button', { name: 'Publish Noise FAQ' })).toBeInTheDocument();
    await act(async () => resolveMutationRefresh(overview([existingArticle])));

    expect(screen.getByRole('button', { name: 'Publish Noise FAQ' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Publish Dust FAQ' })).not.toBeInTheDocument();
  });

  it('focuses the next publication action when the changed article leaves the status filter', async () => {
    api.queryAdminHelp
      .mockResolvedValueOnce(overview([existingArticle, secondArticle]))
      .mockResolvedValueOnce(overview([existingArticle, secondArticle]))
      .mockResolvedValueOnce(overview([secondArticle]));
    renderHelpAdmin();
    fireEvent.change(await screen.findByLabelText('Status'), {
      target: { value: 'Draft' },
    });
    await waitFor(() => expect(api.queryAdminHelp).toHaveBeenCalledTimes(2));
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Publish Dust FAQ',
      }),
    );

    await waitFor(() => expect(screen.getByRole('button', { name: 'Publish Noise FAQ' })).toHaveFocus());
  });

  it('focuses New FAQ when the changed article is the last match for the status filter', async () => {
    api.queryAdminHelp
      .mockResolvedValueOnce(overview([existingArticle]))
      .mockResolvedValueOnce(overview([existingArticle]))
      .mockResolvedValueOnce(overview([]));
    renderHelpAdmin();
    fireEvent.change(await screen.findByLabelText('Status'), {
      target: { value: 'Draft' },
    });
    await waitFor(() => expect(api.queryAdminHelp).toHaveBeenCalledTimes(2));
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Publish Dust FAQ',
      }),
    );

    await waitFor(() => expect(screen.getByRole('button', { name: 'New FAQ' })).toHaveFocus());
  });

  it('focuses the next article after deletion', async () => {
    let articles = [existingArticle, secondArticle];
    api.queryAdminHelp.mockImplementation(async () => overview(articles));
    api.deleteHelpArticle.mockImplementation(async (id) => {
      articles = articles.filter((article) => article.id !== id);
      return { id, message: 'Help article removed.' };
    });
    renderHelpAdmin();
    fireEvent.click(
      await screen.findByRole('button', {
        name: 'Delete Dust FAQ',
      }),
    );
    fireEvent.click(screen.getByRole('button', { name: 'Delete' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Edit Noise FAQ' })).toHaveFocus());
  });

  it('focuses the previous article or New FAQ after deletion', async () => {
    let articles = [existingArticle, secondArticle];
    api.queryAdminHelp.mockImplementation(async () => overview(articles));
    api.deleteHelpArticle.mockImplementation(async (id) => {
      articles = articles.filter((article) => article.id !== id);
      return { id, message: 'Help article removed.' };
    });
    const view = renderHelpAdmin();
    fireEvent.click(
      await screen.findByRole('button', {
        name: 'Delete Noise FAQ',
      }),
    );
    fireEvent.click(screen.getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Edit Dust FAQ' })).toHaveFocus());

    view.unmount();
    articles = [existingArticle];
    renderHelpAdmin();
    fireEvent.click(
      await screen.findByRole('button', {
        name: 'Delete Dust FAQ',
      }),
    );
    fireEvent.click(screen.getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'New FAQ' })).toHaveFocus());
  });
});
