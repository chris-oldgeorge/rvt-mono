# RVT Portal Client

React/Vite SPA client for the RVT Portal migration.

This project is intentionally split from `RvtPortal.Spa` as of Phase 3.5. The ASP.NET Core host remains the API and static-file publish host during migration, while this folder owns the TypeScript, Vite, Vitest, Playwright, and OpenAPI client artifacts.

## Common Commands

```powershell
npm ci
npm run dev:vs
npm run lint
npm run build
npm run test:run
npm run test:e2e
npm run openapi
```

Visual Studio F5 for `RvtPortal.Spa` still launches this client through ASP.NET Core SPA Proxy using `npm run dev:vs`.

The client is intentionally a plain npm/Vite project in Phase 3.5. A Visual Studio `.esproj` wrapper can be added later once the VM image has confirmed JavaScript project SDK support.

## API Contract Boundary

`src/api/schema.d.ts` is generated from the backend OpenAPI document with `npm run openapi`. `src/api/client.ts` must import request and response contracts through `src/api/openApiClient.ts`, not directly from `src/dtos.ts`; `src/api/client.test.ts` contains a guardrail for that boundary.

## API Request Safety

Client-side API calls must go through `src/api/client.ts`. Do not pass browser-controlled values directly to `fetch` or build request URLs outside the shared client.

Allowed API origins are controlled by `VITE_RVT_PORTAL_ALLOWED_API_HOSTS` as a comma-separated list of origins or host:port values. Same-origin requests and the standard local API ports `5178`/`5179` are allowed by default for development. `VITE_RVT_PORTAL_API_URL` must point to one of the allowed origins when it is set.
