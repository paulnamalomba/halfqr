FROM node:22-bookworm-slim AS deps
WORKDIR /app

COPY webapp/package.json webapp/package-lock.json ./
RUN npm ci

FROM node:22-bookworm-slim AS builder
WORKDIR /app

COPY --from=deps /app/node_modules ./node_modules
COPY webapp/ ./

ARG NEXT_PUBLIC_HAVEQR_API_BASE_URL=https://haveqr-api-demo.computemore.com
ENV NEXT_PUBLIC_HAVEQR_API_BASE_URL=$NEXT_PUBLIC_HAVEQR_API_BASE_URL

RUN npm run build

FROM node:22-bookworm-slim AS runner
WORKDIR /app

ENV NODE_ENV=production
ENV PORT=3000
ENV HOSTNAME=0.0.0.0

ARG NEXT_PUBLIC_HAVEQR_API_BASE_URL=https://haveqr-api-demo.computemore.com
ENV NEXT_PUBLIC_HAVEQR_API_BASE_URL=$NEXT_PUBLIC_HAVEQR_API_BASE_URL

COPY --from=builder /app/.next/standalone ./
COPY --from=builder /app/.next/static ./.next/static
COPY --from=builder /app/public ./public

EXPOSE 3000

CMD ["node", "server.js"]
