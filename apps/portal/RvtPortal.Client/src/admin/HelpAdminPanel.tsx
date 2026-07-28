// File summary: Renders Help/FAQ CMS administration tools for RVT admin users.
// Major updates:
// - 2026-07-28 Added immutable asset-row identity, persisted asset IDs, and deterministic focus restoration.
// - 2026-06-29 pending Moved Help CMS slug generation to a tested linear helper for Sonar reliability.
// - 2026-06-26 pending Added cancellation for Help admin list requests.
// - 2026-06-25 pending Sorted Help content type filters with locale-aware comparison.
// - 2026-06-26 pending Split slug trimming regex into linear replacements for Sonar reliability.
// - 2026-06-10 pending Added admin Help/FAQ management page with create, edit, publish, and delete workflows.

import { BookOpen, Edit3, EyeOff, Plus, Save, Search, Trash2 } from 'lucide-react';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import {
  createHelpArticle,
  deleteHelpArticle,
  isAbortError,
  queryAdminHelp,
  setHelpArticlePublication,
  updateHelpArticle,
} from '../api/client';
import { ConfirmDialog, FormField, Notice, SubmitButton } from '../components/FormControls';
import type {
  HelpAdminOverviewResponse,
  HelpArticleMutationRequest,
  HelpArticleResponse,
  HelpAssetMutationRequest,
} from '../dtos';
import { slugify } from './HelpAdminSlug';

type HelpAdminPanelProps = Readonly<{
  onNavigate: (path: string) => void;
  onRequestError: (error: unknown) => void;
}>;

type HelpAssetFormRow = HelpAssetMutationRequest & {
  clientKey: string;
};

type HelpArticleForm = Omit<HelpArticleMutationRequest, 'assets'> & {
  assets: HelpAssetFormRow[];
};

type PendingFocus =
  | { kind: 'article'; id: string }
  | { kind: 'publication'; id: string }
  | { kind: 'asset'; clientKey: string }
  | { kind: 'add-asset' }
  | { kind: 'new-article' };

const emptyArticleForm: HelpArticleForm = {
  sectionTitle: 'General',
  sectionSlug: 'general',
  title: '',
  slug: '',
  summary: '',
  body: '',
  contentType: 'FAQ',
  isPublished: false,
  sectionSortOrder: 1,
  sortOrder: 1,
  assets: [],
};

// Function summary: Renders the HelpAdminPanel React component and wires Help CMS management behavior.
export function HelpAdminPanel({ onNavigate, onRequestError }: HelpAdminPanelProps) {
  const [overview, setOverview] = useState<HelpAdminOverviewResponse | null>(null);
  const [selectedArticle, setSelectedArticle] = useState<HelpArticleResponse | null>(null);
  const [form, setForm] = useState<HelpArticleForm>(emptyArticleForm);
  const [searchText, setSearchText] = useState('');
  const [status, setStatus] = useState('All');
  const [contentType, setContentType] = useState('All');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [completedRequestKey, setCompletedRequestKey] = useState<string | null>(null);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [deleteCandidate, setDeleteCandidate] = useState<HelpArticleResponse | null>(null);
  const [pendingFocus, setPendingFocus] = useState<PendingFocus | null>(null);
  const articleEditRefs = useRef(new Map<string, HTMLButtonElement>());
  const articlePublicationRefs = useRef(new Map<string, HTMLButtonElement>());
  const assetTitleRefs = useRef(new Map<string, HTMLInputElement>());
  const addAssetButtonRef = useRef<HTMLButtonElement>(null);
  const newArticleButtonRef = useRef<HTMLButtonElement>(null);

  const contentTypes = useMemo(() => {
    const values = new Set(['FAQ', 'Article', 'Document', 'Video', 'Definition']);
    overview?.articles.forEach((article) => values.add(article.contentType));
    return [
      'All',
      ...Array.from(values)
        .filter(Boolean)
        .sort((left, right) => left.localeCompare(right)),
    ];
  }, [overview]);
  const query = useMemo(() => ({ searchText, status, contentType }), [contentType, searchText, status]);
  const requestKey = JSON.stringify(query);
  const isLoading = isRefreshing || completedRequestKey !== requestKey;

  // Function summary: Loads Help CMS admin article data.
  const loadArticles = useCallback(
    async () => {
      setIsRefreshing(true);
      try {
        const response = await queryAdminHelp(query);
        setOverview(response);
        setError(null);
        setCompletedRequestKey(requestKey);
        return response;
      } catch (err) {
        if (!isAbortError(err)) {
          setError((err as Error).message);
          onRequestError(err);
          setCompletedRequestKey(requestKey);
        }
        return null;
      } finally {
        setIsRefreshing(false);
      }
    },
    [onRequestError, query, requestKey],
  );

  useEffect(() => {
    const controller = new AbortController();
    queryAdminHelp(query, { signal: controller.signal })
      .then((response) => {
        if (controller.signal.aborted) {
          return;
        }
        setOverview(response);
        setError(null);
        setCompletedRequestKey(requestKey);
      })
      .catch((err: Error) => {
        if (controller.signal.aborted || isAbortError(err)) {
          return;
        }
        setError(err.message);
        onRequestError(err);
        setCompletedRequestKey(requestKey);
      });
    return () => controller.abort();
  }, [onRequestError, query, requestKey]);

  useEffect(() => {
    if (!pendingFocus) {
      return;
    }

    const target =
      pendingFocus.kind === 'article'
        ? articleEditRefs.current.get(pendingFocus.id)
        : pendingFocus.kind === 'publication'
          ? articlePublicationRefs.current.get(pendingFocus.id)
          : pendingFocus.kind === 'asset'
            ? assetTitleRefs.current.get(pendingFocus.clientKey)
            : pendingFocus.kind === 'add-asset'
              ? addAssetButtonRef.current
              : newArticleButtonRef.current;
    if (target) {
      target.focus();
      setPendingFocus(null);
    }
  }, [form.assets, overview, pendingFocus]);

  // Function summary: Selects a Help CMS article for editing.
  function editArticle(article: HelpArticleResponse) {
    setSelectedArticle(article);
    setForm(articleToForm(article));
    setNotice(null);
  }

  // Function summary: Resets the Help CMS article form for new content.
  function startNewArticle() {
    setSelectedArticle(null);
    setForm({ ...emptyArticleForm, assets: [] });
    setNotice(null);
  }

  // Function summary: Saves a Help CMS article from the management form.
  async function saveArticle(event: FormEvent) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    setNotice(null);
    try {
      const mutation = formToMutation(form);
      const saved = selectedArticle
        ? await updateHelpArticle(selectedArticle.id, mutation)
        : await createHelpArticle(mutation);
      setSelectedArticle(saved.item ?? null);
      setForm(saved.item ? articleToForm(saved.item) : form);
      setNotice(selectedArticle ? 'Help article updated.' : 'Help article created.');
      if (saved.item) {
        setPendingFocus({ kind: 'article', id: saved.item.id });
      }
      void loadArticles();
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    } finally {
      setIsSaving(false);
    }
  }

  // Function summary: Publishes or unpublishes a Help CMS article.
  async function togglePublication(article: HelpArticleResponse) {
    try {
      const articlesBeforeRefresh = overview?.articles ?? [];
      const changedIndex = articlesBeforeRefresh.findIndex((item) => item.id === article.id);
      const response = await setHelpArticlePublication(article.id, { isPublished: !article.isPublished });
      if (selectedArticle?.id === article.id && response.item) {
        setSelectedArticle(response.item);
        setForm(articleToForm(response.item));
      }
      setNotice(response.item?.isPublished ? 'Help article published.' : 'Help article moved to draft.');
      const refreshedOverview = await loadArticles();
      if (!refreshedOverview) {
        return;
      }

      const refreshedIds = new Set(refreshedOverview.articles.map((item) => item.id));
      if (refreshedIds.has(article.id)) {
        setPendingFocus({ kind: 'publication', id: article.id });
        return;
      }

      const nextArticle = articlesBeforeRefresh.slice(changedIndex + 1).find((item) => refreshedIds.has(item.id));
      const previousArticle = articlesBeforeRefresh
        .slice(0, Math.max(changedIndex, 0))
        .reverse()
        .find((item) => refreshedIds.has(item.id));
      const fallbackArticle = nextArticle ?? previousArticle ?? refreshedOverview.articles[0];
      setPendingFocus(fallbackArticle ? { kind: 'publication', id: fallbackArticle.id } : { kind: 'new-article' });
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    }
  }

  // Function summary: Deletes a Help CMS article after confirmation.
  async function confirmDeleteArticle() {
    if (!deleteCandidate) {
      return;
    }

    try {
      const articles = overview?.articles ?? [];
      const deletedIndex = articles.findIndex((article) => article.id === deleteCandidate.id);
      const nextArticle = deletedIndex >= 0 ? (articles[deletedIndex + 1] ?? articles[deletedIndex - 1]) : undefined;
      await deleteHelpArticle(deleteCandidate.id);
      if (selectedArticle?.id === deleteCandidate.id) {
        startNewArticle();
      }
      setDeleteCandidate(null);
      setNotice('Help article deleted.');
      setPendingFocus(nextArticle ? { kind: 'article', id: nextArticle.id } : { kind: 'new-article' });
      void loadArticles();
    } catch (err) {
      setError((err as Error).message);
      onRequestError(err);
    }
  }

  // Function summary: Updates one Help CMS linked asset row in the form.
  function updateAsset(index: number, nextAsset: HelpAssetFormRow) {
    setForm((current) => ({
      ...current,
      assets: current.assets.map((asset, assetIndex) => (assetIndex === index ? nextAsset : asset)),
    }));
  }

  // Function summary: Adds one linked asset row to the Help CMS form.
  function addAsset() {
    const clientKey = newAssetClientKey();
    setForm((current) => ({
      ...current,
      assets: [
        ...current.assets,
        {
          clientKey,
          title: '',
          assetType: 'Document',
          url: '',
          sortOrder: current.assets.length + 1,
        },
      ],
    }));
    setPendingFocus({ kind: 'asset', clientKey });
  }

  // Function summary: Removes one linked asset row from the Help CMS form.
  function removeAsset(index: number) {
    const nextAsset = form.assets[index + 1] ?? form.assets[index - 1];
    setForm((current) => ({
      ...current,
      assets: current.assets.filter((_, assetIndex) => assetIndex !== index),
    }));
    setPendingFocus(nextAsset ? { kind: 'asset', clientKey: nextAsset.clientKey } : { kind: 'add-asset' });
  }

  return (
    <section className="admin-help-grid">
      <section className="panel">
        <div className="panel-heading">
          <div>
            <p>Administration</p>
            <h2>Help/FAQ Management</h2>
          </div>
          <button className="secondary-button" type="button" onClick={startNewArticle} ref={newArticleButtonRef}>
            <Plus size={17} aria-hidden="true" />
            <span>New FAQ</span>
          </button>
        </div>
        <div className="admin-help-filters">
          <label className="search-box">
            <Search size={18} aria-hidden="true" />
            <input
              value={searchText}
              onChange={(event) => setSearchText(event.target.value)}
              placeholder="Search help content"
            />
          </label>
          <label className="form-field compact-field">
            <span>Status</span>
            <select value={status} onChange={(event) => setStatus(event.target.value)}>
              <option value="All">All</option>
              <option value="Published">Published</option>
              <option value="Draft">Draft</option>
            </select>
          </label>
          <label className="form-field compact-field">
            <span>Type</span>
            <select value={contentType} onChange={(event) => setContentType(event.target.value)}>
              {contentTypes.map((item) => (
                <option value={item} key={item}>
                  {item}
                </option>
              ))}
            </select>
          </label>
        </div>
        {notice && <Notice tone="success" message={notice} />}
        {error && <Notice tone="error" message={error} />}
        {isLoading && <p className="muted-text">Loading help articles...</p>}
        <div className="admin-help-list">
          {overview?.articles.map((article) => (
            <article
              className={selectedArticle?.id === article.id ? 'help-admin-card selected' : 'help-admin-card'}
              key={article.id}
            >
              <div>
                <span className={article.isPublished ? 'status-chip success' : 'status-chip neutral'}>
                  {article.isPublished ? 'Published' : 'Draft'}
                </span>
                <strong>{article.title}</strong>
                <p>
                  {article.sectionTitle} / {article.contentType}
                </p>
              </div>
              <div className="row-actions">
                <button
                  className="icon-button"
                  type="button"
                  onClick={() => editArticle(article)}
                  aria-label={`Edit ${article.title}`}
                  title="Edit"
                  ref={(element) => {
                    if (element) {
                      articleEditRefs.current.set(article.id, element);
                    } else {
                      articleEditRefs.current.delete(article.id);
                    }
                  }}
                >
                  <Edit3 size={16} aria-hidden="true" />
                </button>
                <button
                  className="icon-button"
                  type="button"
                  onClick={() => togglePublication(article)}
                  aria-label={article.isPublished ? `Unpublish ${article.title}` : `Publish ${article.title}`}
                  title={article.isPublished ? 'Unpublish' : 'Publish'}
                  ref={(element) => {
                    if (element) {
                      articlePublicationRefs.current.set(article.id, element);
                    } else {
                      articlePublicationRefs.current.delete(article.id);
                    }
                  }}
                >
                  {article.isPublished ? (
                    <EyeOff size={16} aria-hidden="true" />
                  ) : (
                    <BookOpen size={16} aria-hidden="true" />
                  )}
                </button>
                <button
                  className="icon-button"
                  type="button"
                  onClick={() => onNavigate(`/help/${article.slug}`)}
                  aria-label={`Preview ${article.title}`}
                  title="Preview"
                  disabled={!article.isPublished}
                >
                  <BookOpen size={16} aria-hidden="true" />
                </button>
                <button
                  className="icon-button danger"
                  type="button"
                  onClick={() => setDeleteCandidate(article)}
                  aria-label={`Delete ${article.title}`}
                  title="Delete"
                >
                  <Trash2 size={16} aria-hidden="true" />
                </button>
              </div>
            </article>
          ))}
          {overview?.articles.length === 0 && <p className="muted-text">No help articles match the current filters.</p>}
        </div>
      </section>

      <section className="panel">
        <div className="panel-heading">
          <div>
            <p>{selectedArticle ? 'Edit content' : 'Create content'}</p>
            <h2>{selectedArticle?.title || 'New FAQ'}</h2>
          </div>
          <span className={form.isPublished ? 'status-chip success' : 'status-chip neutral'}>
            {form.isPublished ? 'Published' : 'Draft'}
          </span>
        </div>
        <form className="form-grid compact-form help-admin-form" onSubmit={saveArticle}>
          <FormField label="Section">
            <input
              value={form.sectionTitle}
              onChange={(event) =>
                setForm({ ...form, sectionTitle: event.target.value, sectionSlug: slugify(event.target.value) })
              }
            />
          </FormField>
          <FormField label="Section slug">
            <input
              value={form.sectionSlug}
              onChange={(event) => setForm({ ...form, sectionSlug: event.target.value })}
            />
          </FormField>
          <FormField label="Title">
            <input
              value={form.title}
              onChange={(event) =>
                setForm({
                  ...form,
                  title: event.target.value,
                  slug: selectedArticle ? form.slug : slugify(event.target.value),
                })
              }
            />
          </FormField>
          <FormField label="Slug">
            <input value={form.slug} onChange={(event) => setForm({ ...form, slug: event.target.value })} />
          </FormField>
          <FormField label="Type">
            <select
              value={form.contentType}
              onChange={(event) => setForm({ ...form, contentType: event.target.value })}
            >
              <option value="FAQ">FAQ</option>
              <option value="Article">Article</option>
              <option value="Document">Document</option>
              <option value="Video">Video</option>
              <option value="Definition">Definition</option>
            </select>
          </FormField>
          <FormField label="Summary">
            <input value={form.summary ?? ''} onChange={(event) => setForm({ ...form, summary: event.target.value })} />
          </FormField>
          <FormField label="Body">
            <textarea value={form.body} onChange={(event) => setForm({ ...form, body: event.target.value })} rows={7} />
          </FormField>
          <div className="form-row">
            <FormField label="Section order">
              <input
                value={form.sectionSortOrder}
                onChange={(event) => setForm({ ...form, sectionSortOrder: Number(event.target.value) || 0 })}
                type="number"
                min="0"
              />
            </FormField>
            <FormField label="Article order">
              <input
                value={form.sortOrder}
                onChange={(event) => setForm({ ...form, sortOrder: Number(event.target.value) || 0 })}
                type="number"
                min="0"
              />
            </FormField>
          </div>
          <label className="checkbox-field">
            <input
              type="checkbox"
              checked={form.isPublished}
              onChange={(event) => setForm({ ...form, isPublished: event.target.checked })}
            />
            <span>Publish this content</span>
          </label>
          <div className="help-asset-editor">
            <div className="panel-heading compact-heading">
              <div>
                <p>Resources</p>
                <h3>Linked assets</h3>
              </div>
              <button className="secondary-button" type="button" onClick={addAsset} ref={addAssetButtonRef}>
                <Plus size={16} aria-hidden="true" />
                <span>Add Asset</span>
              </button>
            </div>
            {form.assets.map((asset, index) => (
              <div className="asset-row" key={asset.clientKey}>
                <input
                  value={asset.title}
                  onChange={(event) => updateAsset(index, { ...asset, title: event.target.value })}
                  placeholder="Title"
                  ref={(element) => {
                    if (element) {
                      assetTitleRefs.current.set(asset.clientKey, element);
                    } else {
                      assetTitleRefs.current.delete(asset.clientKey);
                    }
                  }}
                />
                <select
                  value={asset.assetType}
                  onChange={(event) => updateAsset(index, { ...asset, assetType: event.target.value })}
                >
                  <option value="Document">Document</option>
                  <option value="Video">Video</option>
                  <option value="Link">Link</option>
                </select>
                <input
                  value={asset.url}
                  onChange={(event) => updateAsset(index, { ...asset, url: event.target.value })}
                  placeholder="URL"
                />
                <button
                  className="icon-button danger"
                  type="button"
                  onClick={() => removeAsset(index)}
                  aria-label="Remove asset"
                >
                  <Trash2 size={16} aria-hidden="true" />
                </button>
              </div>
            ))}
            {form.assets.length === 0 && <p className="muted-text">No linked assets.</p>}
          </div>
          <SubmitButton
            icon={<Save size={17} aria-hidden="true" />}
            isSubmitting={isSaving}
            idleLabel={selectedArticle ? 'Save FAQ' : 'Create FAQ'}
          />
        </form>
      </section>
      <ConfirmDialog
        open={deleteCandidate !== null}
        title="Delete help article"
        message={`Delete ${deleteCandidate?.title ?? 'this help article'}? This removes the linked Help CMS entry and its asset metadata.`}
        confirmLabel="Delete"
        onCancel={() => setDeleteCandidate(null)}
        onConfirm={confirmDeleteArticle}
      />
    </section>
  );
}

// Function summary: Converts a Help CMS response into the mutation form shape.
function articleToForm(article: HelpArticleResponse): HelpArticleForm {
  return {
    sectionTitle: article.sectionTitle,
    sectionSlug: article.sectionSlug,
    title: article.title,
    slug: article.slug,
    summary: article.summary ?? '',
    body: article.body,
    contentType: article.contentType,
    isPublished: article.isPublished,
    sectionSortOrder: article.sectionSortOrder,
    sortOrder: article.sortOrder,
    assets: article.assets.map((asset) => ({
      id: asset.id,
      clientKey: `persisted:${asset.id}`,
      title: asset.title,
      assetType: asset.assetType,
      url: asset.url,
      sortOrder: asset.sortOrder,
    })),
  };
}

function formToMutation(form: HelpArticleForm): HelpArticleMutationRequest {
  return {
    ...form,
    assets: form.assets.map(({ clientKey: _clientKey, ...asset }) => asset),
  };
}

function newAssetClientKey() {
  return `new:${globalThis.crypto.randomUUID()}`;
}
