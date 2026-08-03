# Deploy en Hostinger VPS

Este proyecto queda listo para desplegar en un VPS Linux de Hostinger usando Docker Compose.

## Estructura usada

- `Ventagram.Web`: web principal
- `Ventagram.ChatService`: servicio de chat
- `docker-compose.hostinger.yml`: stack de produccion
- `.env.hostinger.example`: variables de entorno para copiar a `.env`

## Requisitos

- VPS con Docker y Docker Compose
- Puertos abiertos:
  - `8080` para la web
  - `8081` para chat
- Git instalado en el VPS si vas a clonar el repo

## Despliegue por SSH

1. Conectate al VPS:

```bash
ssh root@TU_IP
```

2. Instala Git si hace falta:

```bash
apt update
apt install -y git
```

3. Clona el repo:

```bash
git clone TU_REPO_GIT ventagram
cd ventagram
```

4. Crea el archivo `.env`:

```bash
cp .env.hostinger.example .env
nano .env
```

5. Completa como minimo estas variables:

- `MYSQL_ROOT_PASSWORD`
- `VENTAGRAM_WEB_BASE_URL`
- `VENTAGRAM_WEB_BASE_URL_ALT`
- `VENTAGRAM_CHAT_BASE_URL`
- `SMTP_HOST`
- `SMTP_USER`
- `SMTP_PASSWORD`
- `SMTP_FROM_EMAIL`
- `MAP_STYLE_URL` o `MAP_TILES_URL_TEMPLATE`
- `R2_ACCESS_KEY_ID`
- `R2_SECRET_ACCESS_KEY`
- `R2_BUCKET`
- `R2_PUBLIC_BASE_URL`

Opcionales para búsqueda de direcciones:

- `MAP_GEOCODING_SEARCH_URL_TEMPLATE`
- `MAP_REVERSE_GEOCODING_URL_TEMPLATE`
- `SMTP_CONTACT_RECIPIENT` si quieres que el formulario de contacto llegue a otra casilla distinta del remitente general

6. Levanta el stack:

```bash
docker compose -f docker-compose.hostinger.yml up -d --build
```

7. Verifica estado:

```bash
docker compose -f docker-compose.hostinger.yml ps
docker compose -f docker-compose.hostinger.yml logs -f ventagram-web
docker compose -f docker-compose.hostinger.yml logs -f ventagram-chat
```

## Actualizar una nueva version

```bash
cd ~/ventagram
git pull
docker compose -f docker-compose.hostinger.yml up -d --build
```

## URLs iniciales

Sin proxy inverso:

- Web: `http://TU_IP:8080`
- Chat: `http://TU_IP:8081`

## Dominio

Si despues quieres usar dominio:

- `app.tudominio.com` -> puerto `8080`
- `chat.tudominio.com` -> puerto `8081`

En ese caso actualiza en `.env`:

- `VENTAGRAM_WEB_BASE_URL=https://app.tudominio.com`
- `VENTAGRAM_WEB_BASE_URL_ALT=https://www.app.tudominio.com` o repite la misma URL
- `VENTAGRAM_CHAT_BASE_URL=https://chat.tudominio.com`
- `VENTAGRAM_COOKIE_DOMAIN=.tudominio.com`

## Notas

- MySQL queda solo dentro de la red Docker. No se publica al exterior.
- `DataProtection` queda en un volumen compartido entre web y chat para que las cookies sigan siendo validas.
- Si usas HTTPS por proxy externo, conviene apuntar las variables `*_BASE_URL` a las URLs finales con `https`.
- El chat crea `ventagram_chat` automaticamente si no existe.
