# ---- Build Stage ----
FROM node:22-alpine3.20 AS builder

WORKDIR /app

RUN npm install -g pnpm@9

COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

COPY . .
RUN pnpm run build

# ---- Runtime Stage ----
FROM nginx:1.31-alpine AS runtime

COPY --from=builder /app/dist /usr/share/nginx/html
COPY nginx.spa.conf /etc/nginx/conf.d/default.conf

# nginx:alpine ships with an `nginx` user; ensure it owns writable paths,
# then drop to it so the container runs as non-root.
RUN chown -R nginx:nginx /usr/share/nginx/html /var/cache/nginx /var/log/nginx /etc/nginx/conf.d \
    && touch /var/run/nginx.pid \
    && chown nginx:nginx /var/run/nginx.pid

USER nginx

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --quiet --spider http://localhost:8080/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
