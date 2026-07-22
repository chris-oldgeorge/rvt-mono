# syntax=docker/dockerfile:1.10

FROM node:24-alpine@sha256:a0b9bf06e4e6193cf7a0f58816cc935ff8c2a908f81e6f1a95432d679c54fbfd AS build
WORKDIR /workspace/RvtPortal.Client

COPY RvtPortal.Client/package.json RvtPortal.Client/package-lock.json ./
RUN npm ci

COPY RvtPortal.Client/ ./
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.29-alpine@sha256:0c79d56aee561a1d81c63f00eee5fb5fe29279560cdc55e91425133104c7fbe6
COPY RvtPortal.Client/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /workspace/RvtPortal.Client/dist /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --retries=3 CMD wget -qO- http://127.0.0.1:8080/healthz || exit 1
