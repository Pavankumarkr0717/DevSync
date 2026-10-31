FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
# postinstall links test dependencies; it is a no-op here because tests are not in this context.
COPY scripts/ scripts/
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build

# Static files plus a same-origin reverse proxy to the API.
FROM nginx:alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
