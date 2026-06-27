# QMSofts Shell — build the Vite SPA, serve static via nginx.
#
# Build args let Railway inject the VITE_* values at BUILD time (Vite inlines
# env vars into the bundle; they are not read at runtime). Set these as
# build-time variables in the Railway service, OR rely on Railway passing
# service variables through to the build.
#
# Railway: set service Root Directory to shell/qmsofts-shell and use this Dockerfile.

FROM node:22-alpine AS build
WORKDIR /app

# Build-time public config (Vite inlines VITE_* at build).
ARG VITE_IDENTITY_URL
ARG VITE_PARAKH_URL
ARG VITE_ERES_URL
ENV VITE_IDENTITY_URL=$VITE_IDENTITY_URL \
    VITE_PARAKH_URL=$VITE_PARAKH_URL \
    VITE_ERES_URL=$VITE_ERES_URL

COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS runtime
# Template is rendered at start so ${PORT} from Railway is honored.
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

# nginx:alpine's entrypoint runs envsubst over /etc/nginx/templates/*.template
# into /etc/nginx/conf.d/. Restrict substitution to ${PORT} only, so nginx's
# own $uri/$host variables in the template are left intact.
ENV PORT=8080
ENV NGINX_ENVSUBST_FILTER='PORT'
EXPOSE 8080
