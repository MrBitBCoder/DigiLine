# -------------------------------
# BUILD STAGE
# -------------------------------
FROM node:22-alpine AS build

WORKDIR /app

ARG VITE_API_BASE_URL
ARG VITE_SIGNALR_URL
ARG VITE_PUBLIC_SIGNALR_URL

ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_SIGNALR_URL=$VITE_SIGNALR_URL
ENV VITE_PUBLIC_SIGNALR_URL=$VITE_PUBLIC_SIGNALR_URL

COPY package*.json ./

RUN npm ci

COPY . .

RUN npm run build


# -------------------------------
# RUNTIME STAGE
# -------------------------------
FROM nginx:alpine

COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]