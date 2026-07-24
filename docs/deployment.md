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
| `InstanceOperator__Name` | *(empty)* | Display name shown in the footer as "Hosted by …". Hidden if empty. |
| `InstanceOperator__Url` | *(empty)* | Optional URL linked from the operator name in the footer. |
| `InstanceOperator__Notice` | *(empty)* | Optional plain-text footer notice. Shown without a link. |
| `Localization__EnabledLanguages` | `en` | Comma-separated language codes shown in the Profile language selector (e.g. `en,hu`). See [Language / UI Localisation](#language--ui-localisation). |

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

> **Note:** FIDO2/passkeys and PWA installation require a [secure context](https://developer.mozilla.org/en-US/docs/Web/Security/Secure_Contexts). On a local network without HTTPS, most browsers only allow `localhost` as a secure context. If you access the app from another device (e.g. your phone to a Raspberry Pi), you will need HTTPS — either via a reverse proxy (see below) or a local certificate.

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

### 5. Configure application settings

Set all required and optional app settings via `az webapp config appsettings set`:

```sh
az webapp config appsettings set \
  --name <app-name> \
  --resource-group <resource-group> \
  --settings \
    Fido2__ServerDomain=<app-name>.azurewebsites.net \
    Fido2__Origins=https://<app-name>.azurewebsites.net \
    InstanceOperator__Name="The Smith Family" \
    InstanceOperator__Url="https://smith.example.com" \
    InstanceOperator__Notice="Running on Azure App Service"
```

> Azure App Service provides a free TLS certificate for `*.azurewebsites.net` automatically. FIDO2/passkeys **require HTTPS** — this is handled for you.

If you configure a custom domain, update `Fido2__ServerDomain` and `Fido2__Origins` to match it.

#### Available settings

| Setting | Required | Description |
|---|---|---|
| `Fido2__ServerDomain` | ✅ | Domain used for passkey registration/authentication. Must match the browser's address bar. |
| `Fido2__Origins` | ✅ | Comma-separated list of allowed WebAuthn origins (full URL with scheme). Must match exactly. |
| `InstanceOperator__Name` | — | Display name of the person or household running this instance. Shown in the app footer. Leave empty to hide. |
| `InstanceOperator__Url` | — | Optional URL linked from the operator name in the footer (e.g. a personal site). Ignored if `Name` is empty. |
| `InstanceOperator__Notice` | — | Optional plain-text footer notice. Shown in the app footer without a link. |
| `DataDirectory` | — | Path inside the container where `chores.db` is stored. Defaults to `/data`. |
| `Localization__EnabledLanguages` | — | Comma-separated language codes to show in the Profile language selector (e.g. `en,hu`). Defaults to `en`. See [Language / UI Localisation](#language--ui-localisation). |

### 6. Deploy updates

```sh
docker build -t <your-registry-name>.azurecr.io/chores:latest .
docker push <your-registry-name>.azurecr.io/chores:latest

az webapp restart --name <app-name> --resource-group <resource-group>
```

---

## Language / UI Localisation

The UI language is controlled by a combination of an environment variable (which languages are available) and a per-user cookie (which language that user has selected). The backend, database values, and API responses remain English-only.

### Enabling languages

Set `Localization:EnabledLanguages` to a comma-separated list of language codes. Only languages whose code appears in this list will be shown in the language selector on the Profile page. When exactly one language is enabled the selector is hidden.

| Setting | Default | Description |
|---|---|---|
| `Localization:EnabledLanguages` | `en` | Comma-separated list of BCP 47-style language codes to make available in the UI (e.g. `en,hu`). |

**`appsettings.Development.json`** (local development — already included in the repo):

```json
{
  "Localization": {
    "EnabledLanguages": "en,hu"
  }
}
```

**`appsettings.json`** (self-hosted without Docker):

```json
{
  "Localization": {
    "EnabledLanguages": "en,hu"
  }
}
```

**Docker Compose** (environment variable — double underscore separator):

```yaml
environment:
  - Localization__EnabledLanguages=en,hu
```

**Azure App Service** (app setting — double underscore separator):

```sh
az webapp config appsettings set \
  --name <app-name> \
  --resource-group <resource-group> \
  --settings Localization__EnabledLanguages="en,hu"
```

> **Note:** JSON config files use a nested object with a colon path (`Localization` → `EnabledLanguages`), while environment variables use a double-underscore separator (`Localization__EnabledLanguages`). Both map to the same setting.

### Built-in languages

The following languages are bundled with the application and are ready to enable without any extra files:

| Code | Language |
|---|---|
| `en` | English |
| `de` | Deutsch (German) |
| `es` | Español (Spanish) |
| `hu` | Magyar (Hungarian) |

English (`en`) is always the fallback: if a translation key is missing in the active language it is looked up in English, and if it is missing there too the key itself is returned.

### Adding a custom language (without redeployment)

Place a UTF-8 JSON file in `{DataDirectory}/languages/` (e.g. `/data/languages/fr.json`). The file must contain at minimum the `_code` and `_name` keys:

```json
{
  "_code": "fr",
  "_name": "Français",
  "nav.myChores": "Mes tâches",
  "nav.chores": "Tâches"
}
```

Keys not present in the custom file fall back to the English built-in values, so a partial translation is perfectly valid. You can also place a JSON file for an already built-in language (e.g. `en.json`) to override individual keys without replacing the full file — uploaded entries take precedence over built-in ones.

After adding or modifying files in the `languages/` directory, restart the container so the singleton `TranslationStore` reloads the files.

Then add the new code to `Localization:EnabledLanguages` and restart.

---

## Scaling considerations

Chores is designed for household-scale use. SQLite works well for this purpose.

If you scale the App Service to more than one instance, multiple instances cannot safely share a single SQLite file over Azure Files. In that case, migrate the EF Core provider from SQLite to **Azure Database for PostgreSQL Flexible Server** — only `Program.cs` and the NuGet packages need to change; all models, migrations, and pages remain the same.
