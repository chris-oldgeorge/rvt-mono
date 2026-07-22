// File summary: Renders the user-facing Help/FAQ page backed by the Help CMS API.
// Major updates:
// - 2026-06-26 pending Added cancellation for Help overview and article requests.
// - 2026-06-08 pending Added Help page overview, article detail, search, and asset links.
// - 2026-06-25 pending Validated admin-authored asset URLs against unsafe schemes before rendering links.

import { BookOpen, FileText, Search, Video } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { getHelpArticle, isAbortError, queryHelp } from '../api/client';
import { Notice } from '../components/FormControls';
import type { HelpArticleResponse, HelpOverviewResponse } from '../dtos';
import { safeHref } from '../safeUrl';

type HelpPanelProps = Readonly<{
  locationPath: string;
  onNavigate: (path: string) => void;
  onRequestError: (error: unknown) => void;
}>;

// Function summary: Renders the HelpPanel React component and wires its local UI behavior.
export function HelpPanel({ locationPath, onNavigate, onRequestError }: HelpPanelProps) {
  const slug = useMemo(() => parseHelpSlug(locationPath), [locationPath]);
  return slug
    ? <HelpArticlePanel slug={slug} onNavigate={onNavigate} onRequestError={onRequestError} />
    : <HelpOverviewPanel onNavigate={onNavigate} onRequestError={onRequestError} />;
}

// Function summary: Renders the Help CMS overview and search experience.
function HelpOverviewPanel({ onNavigate, onRequestError }: Omit<HelpPanelProps, 'locationPath'>) {
  const [searchText, setSearchText] = useState('');
  const [overview, setOverview] = useState<HelpOverviewResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    setIsLoading(true);
    queryHelp(searchText, { signal: controller.signal })
      .then((response) => {
        setOverview(response);
        setError(null);
      })
      .catch((err: Error) => {
        if (isAbortError(err)) {
          return;
        }
        setError(err.message);
        onRequestError(err);
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });
    return () => controller.abort();
  }, [onRequestError, searchText]);

  return (
    <section className="panel help-panel">
      <div className="panel-heading">
        <div>
          <p>Support</p>
          <h2>Help</h2>
        </div>
        <BookOpen size={22} aria-hidden="true" />
      </div>
      <label className="search-field">
        <Search size={17} aria-hidden="true" />
        <input value={searchText} onChange={(event) => setSearchText(event.target.value)} placeholder="Search help" />
      </label>
      {error && <Notice tone="error" message={error} />}
      {isLoading && <p className="muted-text">Loading help...</p>}
      <div className="help-section-list">
        {overview?.sections.map((section) => (
          <section className="help-section" key={section.id}>
            <h3>{section.title}</h3>
            <div className="help-article-list">
              {section.articles.map((article) => (
                <article className="help-article-card" key={article.id}>
                  <span className="status-chip neutral">{article.contentType}</span>
                  <strong>{article.title}</strong>
                  {article.summary && <p>{article.summary}</p>}
                  <a href={`/help/${article.slug}`} onClick={(event) => {
                    event.preventDefault();
                    onNavigate(`/help/${article.slug}`);
                  }}>
                    Open article
                  </a>
                </article>
              ))}
            </div>
          </section>
        ))}
        {overview?.sections.length === 0 && <p className="muted-text">No help content matches this search.</p>}
      </div>
    </section>
  );
}

// Function summary: Renders a Help CMS article and linked assets.
function HelpArticlePanel({ slug, onNavigate, onRequestError }: Omit<HelpPanelProps, 'locationPath'> & Readonly<{ slug: string }>) {
  const [article, setArticle] = useState<HelpArticleResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    setIsLoading(true);
    getHelpArticle(slug, { signal: controller.signal })
      .then((response) => {
        setArticle(response.item ?? null);
        setError(null);
      })
      .catch((err: Error) => {
        if (isAbortError(err)) {
          return;
        }
        setError(err.message);
        onRequestError(err);
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });
    return () => controller.abort();
  }, [onRequestError, slug]);

  return (
    <section className="panel help-panel">
      <div className="panel-heading">
        <div>
          <p>{article?.sectionTitle ?? 'Help'}</p>
          <h2>{article?.title ?? 'Loading article'}</h2>
        </div>
        <button className="secondary-button" type="button" onClick={() => onNavigate('/help')}>Back</button>
      </div>
      {error && <Notice tone="error" message={error} />}
      {isLoading && <p className="muted-text">Loading article...</p>}
      {article && (
        <div className="help-article-detail">
          {article.summary && <p className="lead-text">{article.summary}</p>}
          <p>{article.body}</p>
          {article.assets.length > 0 && (
            <div className="help-assets">
              <h3>Resources</h3>
              {article.assets.map((asset) => {
                const href = safeHref(asset.url);
                if (!href) {
                  return null;
                }
                const isExternal = !asset.url.startsWith('/');
                return (
                  <a href={href} key={asset.id} target={isExternal ? '_blank' : undefined} rel={isExternal ? 'noreferrer' : undefined}>
                    {asset.assetType.toLowerCase() === 'video' ? <Video size={17} aria-hidden="true" /> : <FileText size={17} aria-hidden="true" />}
                    <span>{asset.title}</span>
                  </a>
                );
              })}
            </div>
          )}
        </div>
      )}
    </section>
  );
}

// Function summary: Retrieves the Help article slug from the current route.
function parseHelpSlug(locationPath: string) {
  const path = new URL(locationPath, 'https://rvt.local').pathname;
  const match = /^\/help\/([^/]+)$/i.exec(path);
  return match ? decodeURIComponent(match[1]) : null;
}
