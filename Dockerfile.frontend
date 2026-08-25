FROM node:22.18.0-alpine3.22 AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.27.5-alpine
WORKDIR /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY docker-entrypoint.d/40-envsubst.sh /docker-entrypoint.d/40-envsubst.sh
COPY --from=build /app/dist/ ./
COPY public/env.template.js ./env.template.js
USER root
RUN chmod +x /docker-entrypoint.d/40-envsubst.sh && chown -R nginx:nginx /usr/share/nginx/html
USER nginx
EXPOSE 8080
