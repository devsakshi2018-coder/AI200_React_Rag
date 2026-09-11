# Stage 1: Build
FROM node:20-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
# Vite env variables build-time par bake ho jaate hain
ARG VITE_INFERENCE_API_URL
ENV VITE_INFERENCE_API_URL=$VITE_INFERENCE_API_URL
RUN npm run build

# Stage 2: Serve via nginx
FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]