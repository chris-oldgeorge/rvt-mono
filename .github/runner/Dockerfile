FROM ubuntu:24.04

ARG RUNNER_VERSION=2.334.0
ARG RUNNER_SHA256=f44255bd3e80160eb25f71bc83d06ea025f6908748807a584687b3184759f7e4

RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
       ca-certificates curl git gosu jq libgssapi-krb5-2 libicu74 libkrb5-3 \
       liblttng-ust1t64 libssl3t64 postgresql-client tar gzip tzdata unzip xz-utils zlib1g \
    && rm -rf /var/lib/apt/lists/*

RUN useradd --create-home --uid 1001 --shell /bin/bash runner
WORKDIR /opt/actions-runner-dist
RUN curl -fsSLo actions-runner.tar.gz \
      --proto '=https' \
      --proto-redir '=https' \
      "https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/actions-runner-linux-arm64-${RUNNER_VERSION}.tar.gz" \
    && echo "${RUNNER_SHA256}  actions-runner.tar.gz" | sha256sum -c - \
    && tar -xzf actions-runner.tar.gz \
    && rm actions-runner.tar.gz

COPY --chmod=755 entrypoint.sh /usr/local/bin/runner-entrypoint
ENTRYPOINT ["/usr/local/bin/runner-entrypoint"]
