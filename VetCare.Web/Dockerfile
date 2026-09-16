# O Vite gera arquivos estáticos; a imagem final é apenas um nginx servindo-os.
FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .

# A URL da API entra no bundle em tempo de build, que é como o Vite trata as
# variáveis VITE_*.
ARG VITE_API_URL=http://localhost:5265
ENV VITE_API_URL=$VITE_API_URL

RUN npm run build

FROM nginx:alpine AS final

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

# O endereço vai literal em IPv4: o nginx escuta apenas em 0.0.0.0 e o wget do
# busybox tentaria ::1 primeiro se usássemos "localhost".
HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
    CMD wget --quiet --spider http://127.0.0.1/ || exit 1
