# Deployment Guide

Chores is a single-container application. It uses SQLite as its database, so no separate database service is needed.

---

## Docker (local / Raspberry Pi)

### Prerequisites

- Docker and Docker Compose installed on the host

### Run

```sh
docker compose up -d
```

The app listens on **port 8080**. Open `http://<host-ip>:8080` in your browser.

The SQLite database is stored in a named Docker volume (`chores-data`) and persists across container restarts and upgrades.

### Upgrade

```sh
docker compose pull
docker compose up -d
```

### Configuration

Set environment variables in `docker-compose.yml` or via `-e` flags:

| Variable | Default | Description |
|---|---|---|
| `DataDirectory` | `/data` | Path inside the container where `chores.db` is stored |
| `Fido2__ServerDomain` | `localhost` | Domain used for passkey registration/authentication |
| `Fido2__Origins` | `https://localhost:5001` | Comma-separated list of allowed WebAuthn origins |

Example `docker-compose.yml` override for a Raspberry Pi on your local network:

```yaml
services:
  chores:
    build: .
    ports:
      - "8080:8080"
    volumes:
      - chores-data:/data
    environment:
      - Fido2__ServerDomain=192.168.1.100
      - Fido2__Origins=http://192.168.1.100:8080
    restart: unless-stopped

volumes:
  chores-data:
```

> **Note:** FIDO2/passkeys require a [secure context](https://developer.mozilla.org/en-US/docs/Web/Security/Secure_Contexts). On a local network without HTTPS, most browsers only allow `localhost` as a secure context. If you access the app from another device (e.g. your phone to a Raspberry Pi), you will need HTTPS — either via a reverse proxy (see below) or a local certificate.

### Reverse proxy with HTTPS (optional)

To enable HTTPS on a local network, place a reverse proxy such as [Caddy](https://caddyserver.com/) or [nginx](https://nginx.org/) in front of the container. Example with Caddy and a local domain:

```
your-pi.local {
    reverse_proxy chores:8080
}
```

---

## Azure App Service

### Prerequisites

- Azure CLI installed and logged in (`az login`)
- Docker installed locally
- An Azure Container Registry (ACR) or Docker Hub account

### 1. Build and push the image

```sh
# Log in to ACR
az acr login --name <your-registry-name>

# Build and push
docker build -t <your-registry-name>.azurecr.io/chores:latest .
docker push <your-registry-name>.azurecr.io/chores:latest
```

### 2. Create a Storage Account and File Share

SQLite needs a persistent volume. Azure App Service's local filesystem is ephemeral — use Azure Files to persist the database.

```sh
az storage account create \
  --name <storage-account-name> \
  --resource-group <resource-group> \
  --sku Standard_LRS

az storage share create \
  --name chores-data \
  --account-name <storage-account-name>
```

### 3. Create the App Service Plan and Web App

```sh
az appservice plan create \
  --name chores-plan \
  --resource-group <resource-group> \
  --is-linux \
  --sku B1

az webapp create \
  --name <app-name> \
  --resource-group <resource-group> \
  --plan chores-plan \
  --deployment-container-image-name <your-registry-name>.azurecr.io/chores:latest
```

### 4. Mount Azure Files as persistent storage

```sh
# Get the storage account key
STORAGE_KEY=$(az storage account keys list \
  --account-name <storage-account-name> \
  --resource-group <resource-group> \
  --query "[0].value" -o tsv)

# Mount the file share to /data inside the container
az webapp config storage-account add \
  --name <app-name> \
  --resource-group <resource-group> \
  --custom-id chores-data \
  --storage-type AzureFiles \
  --share-name chores-data \
  --account-name <storage-account-name> \
  --access-key "$STORAGE_KEY" \
  --mount-path /data
```

### 5. Configure FIDO2 settings

FIDO2/passkeys are bound to a specific domain and origin. Set these to match your App Service URL:

```sh
az webapp config appsettings set \
  --name <app-name> \
  --resource-group <resource-group> \
  --settings \
    Fido2__ServerDomain=<app-name>.azurewebsites.net \
    Fido2__Origins=https://<app-name>.azurewebsites.net
```

> Azure App Service provides a free TLS certificate for `*.azurewebsites.net` automatically. FIDO2/passkeys **require HTTPS** — this is handled for you.

If you configure a custom domain, update both settings to match it.

### 6. Deploy updates

```sh
docker build -t <your-registry-name>.azurecr.io/chores:latest .
docker push <your-registry-name>.azurecr.io/chores:latest

az webapp restart --name <app-name> --resource-group <resource-group>
```

---

## Scaling considerations

Chores is designed for household-scale use. SQLite works well for this purpose.

If you scale the App Service to more than one instance, multiple instances cannot safely share a single SQLite file over Azure Files. In that case, migrate the EF Core provider from SQLite to **Azure Database for PostgreSQL Flexible Server** — only `Program.cs` and the NuGet packages need to change; all models, migrations, and pages remain the same.
