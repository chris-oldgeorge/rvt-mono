/// <reference types="vite/client" />
interface ImportMetaEnv {
  readonly VITE_RVT_PORTAL_API_URL?: string;
  readonly VITE_RVT_PORTAL_ALLOWED_API_HOSTS?: string;
}
interface ImportMeta {
  readonly env: ImportMetaEnv;
}
