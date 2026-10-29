# Pluck

Pluck is a lightweight, self-hosted ephemeral file-sharing application built with a Zero-Knowledge security model.  

It consists of a self-hosted ASP.NET Core Web API and a feature-rich C# CLI client featuring client-side AES-256 encryption,
Spectre.Console UI formatting, terminal QR codes, download analytics, an interactive Admin TUI dashboard,
file upload/download progress tracking, and clipboard integration.

---

## Table of Contents

- [How Pluck Works](#how-pluck-works)
- [Installation](#installation)
    - [Linux & macOS](#linux--macos)
    - [Windows](#windows)
    - [Manual Download (GitHub Releases)](#manual-download-github-releases)
- [CLI Usage & Command Reference](#cli-usage--command-reference)
    - [pluck config](#pluck-config)
    - [pluck share](#pluck-share)
    - [pluck get](#pluck-get)
    - [pluck list](#pluck-list)
    - [pluck file](#pluck-file)
    - [pluck purge](#pluck-purge)
    - [pluck key-gen](#pluck-key-gen)
    - [pluck admin](#pluck-admin-admin-only)
    - [pluck users](#pluck-users-admin-only)
    - [pluck create-user](#pluck-create-user-admin-only)
    - [pluck revoke-user](#pluck-revoke-user-admin-only)
- [Self-Hosting Pluck API](#self-hosting-pluck-api)
    - [compose.yaml](#composeyaml)
    - [Environment Variables](#environment-variables)
    - [Starting the Server](#starting-the-server)
    - [Production Deployment (with TLS)](#production-deployment-with-tls)
- [License](#license)

---

## How Pluck Works

Pluck is designed for temporary file sharing with little maintenance and a **Zero-Knowledge security architecture**:

1. **Client-Side Encryption:** Every file or folder uploaded via `pluck share` is encrypted client-side using AES-256 before leaving your machine. If no custom password (`--pwd`) is provided, a 16-character encryption key is generated automatically and appended to the download URL fragment (`#<key>`). The server never receives or stores your plaintext files or encryption keys.
2. **Upload & Lifecycle Configuration:** Set an optional Time-To-Live (`--ttl` in hours, default 24 hours), maximum download count (`--downloads`), custom vanity token (`--token`), or a custom encryption password (`--pwd`).
3. **Instant Link & QR Generation:** The server returns the unique token and download link, which the CLI automatically copies to your clipboard and renders as a scannable terminal QR code.
4. **Automated Background Cleanup & Analytics:** The Pluck API runs an automated background cleanup worker every 10 minutes, while logging download counts and client/geographic analytics.
5. **Permanent File Purging:** A file is immediately rendered unavailable and permanently purged from disk storage and the database as soon as:

- Its Time-To-Live (TTL) expires, or
- Its remaining download limit reaches zero.

---

## Installation

### Linux & macOS

Run the official installation script in your terminal to automatically detect your system architecture and install the
`pluck` executable to `/usr/local/bin`:

```bash
curl -fsSL https://raw.githubusercontent.com/TruePadawan/Pluck/master/install.sh | sh
```

### Windows

Install via [Chocolatey](https://chocolatey.org/):

```cmd
choco install pluck-cli
```

### Manual Download (GitHub Releases)

Single-file standalone binaries are available on the [GitHub Releases](https://github.com/TruePadawan/Pluck/releases)
page. Download the appropriate binary for your system, rename it to `pluck` (or `pluck.exe`), and place it in your
system PATH:

- Linux (x64): `pluck-vX.Y.Z-linux-x64`
- Windows (x64): `pluck-vX.Y.Z-win-x64.exe`
- macOS Apple Silicon: `pluck-vX.Y.Z-osx-arm64`
- macOS Intel: `pluck-vX.Y.Z-osx-x64`

---

## CLI Usage & Command Reference

### `pluck config`

Links the Pluck CLI to a self-hosted Pluck API instance and saves credentials to `~/.pluck/config.json`.

```bash
pluck config --server <SERVER_URL> --key <API_KEY>
```

**Options:**

- `--server <url>` *(Required)*: The base URL of the Pluck API instance (e.g., `http://localhost:8080` or
  `https://pluck.example.com`).
- `--key <key>` *(Required)*: The API key used for authentication.

---

### `pluck share`

Encrypts and uploads a file or folder to the configured Pluck server with real-time transfer speed and progress tracking. Automatically copies the generated download URL to the clipboard and renders a scannable QR code in your terminal.

```bash
pluck share <filepath> [--ttl <hours>] [--downloads <count>] [--pwd <password>] [--token <token>]
```

**Arguments:**

- `<filepath>` *(Required)*: The path to the local file/folder to upload. Folders are automatically compressed into `.zip` archives before encryption and upload.

**Options:**

- `--ttl <hours>` *(Default: `24`)*: Time-to-live for the file in hours.
- `--downloads <count>` *(Optional)*: Maximum allowed downloads before the file automatically expires.
- `--pwd <password>` *(Optional)*: Custom password used to derive AES-256 encryption keys. If omitted, a cryptographically random 16-character key is generated and appended to the download URL fragment (`#<key>`).
- `--token <token>` *(Optional)*: Custom vanity token to use instead of a randomly generated token. The CLI checks availability with the server and prompts for an alternative if taken.

> [!WARNING]
> Because Pluck uses a Zero-Knowledge model, the server does not store encryption passwords or keys. If you lose the download URL fragment (`#<key>`) or forget your custom password, the file is permanently unrecoverable.

---

### `pluck get`

Downloads and decrypts a file or folder from a Pluck instance URL with real-time progress tracking. If the downloaded item is a folder archive, it is automatically extracted.

```bash
pluck get <url> [--save-dir <directory>] [--pwd <password>]
```

**Arguments:**

- `<url>` *(Required)*: The full download URL of the file to retrieve. If the URL contains an anchor fragment (e.g., `https://pluck.example.com/f/token#key`), the decryption key is automatically extracted and used.

**Options:**

- `--save-dir <dir>` *(Optional)*: The output directory to save the file/folder into. Defaults to the current working directory.
- `--pwd <password>` *(Optional)*: Password to decrypt the file with. If the file was encrypted with a custom password and this option is omitted, the CLI will securely prompt for the password interactively.

---

### `pluck list`

Lists active (unexpired) files on the connected Pluck instance in a styled table displaying tokens, file names, sizes, downloads left, total download counts, expiration timestamps, and download URLs.

```bash
pluck list [--name <username>]
```

**Options:**

- `--name <username>` *(Optional, Admin only)*: Filter listed files by owner username.

---

### `pluck file`

Displays detailed metadata (including file size, download metrics, expiration, and an inline QR code) for a specific file using its token, and copies the download URL to the clipboard. Can also display download analytics.

```bash
pluck file <token> [-a|--analytics]
```

**Arguments:**

- `<token>` *(Required)*: The unique token identifier of the file.

**Options:**

- `-a, --analytics` *(Optional)*: Fetches and displays download analytics instead of general file details, including total downloads, client distribution (bar chart), and geographic country distribution (bar chart).

---

### `pluck purge`

Deletes a file from the Pluck instance ahead of expiration.

```bash
pluck purge <token>
```

**Arguments:**

- `<token>` *(Required)*: The unique token identifier of the file to delete.

---

### `pluck key-gen`

Generates a random 32-character API key using GUID formatting and copies it directly to your system clipboard. Useful
for creating initial admin keys or new user credentials.

```bash
pluck key-gen
```

---

### `pluck admin` *(Admin Only)*

Launches an interactive Spectre.Console TUI Admin Dashboard for managing the Pluck instance directly from your terminal.

```bash
pluck admin
```

**Dashboard Modules:**

- **User Management**: Interactively browse registered users, create new user accounts (with automatic API key copying), or revoke/delete user accounts.
- **File Analytics**: Look up any file by its token to view total downloads, client type distribution charts, and country/geographic analytics.
- **Storage Graphs**: View instance-wide storage usage, active file counts, and a visual breakdown chart comparing active storage against expired/depleted storage.

---

### `pluck users` *(Admin Only)*

Lists all registered users on the Pluck instance.

```bash
pluck users
```

---

### `pluck create-user` *(Admin Only)*

Generates a new non-admin user account on the server, outputs the generated API key, and copies it to the clipboard.

```bash
pluck create-user <name>
```

**Arguments:**

- `<name>` *(Required)*: The username for the new account.

---

### `pluck revoke-user` *(Admin Only)*

Revokes and deletes a user account from the server. Prompts for confirmation before proceeding.

```bash
pluck revoke-user <name> [-f|--force]
```

**Arguments:**

- `<name>` *(Required)*: The username of the account to revoke.

**Options:**

- `-f, --force` *(Optional)*: Skips the interactive confirmation prompt.

---

## Self-Hosting Pluck API

Pluck API is packaged as a container image hosted on GitHub Container Registry (`ghcr.io/truepadawan/pluck-api`).

### `compose.yaml`

Create a `compose.yaml` file on your server:

```yaml
services:
  pluck.api:
    image: ghcr.io/truepadawan/pluck-api:latest
    container_name: pluck-api
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_HTTP_PORTS=8080
      - PluckApi__AdminKey=YOUR_SECURE_ADMIN_KEY
      - PluckApi__UploadDirectory=/app/pluck/uploads
      - ConnectionStrings__DefaultConnection=Data Source=/app/pluck/pluck.db;
    volumes:
      - ./pluck:/app/pluck
```

### Environment Variables

| Variable                               | Required | Description                                                                            |
|:---------------------------------------|:---------|:---------------------------------------------------------------------------------------|
| `PluckApi__AdminKey`                   | Yes      | The master API key for the initial Admin account.                                      |
| `PluckApi__UploadDirectory`            | Yes      | Directory path inside container where uploaded files are saved (`/app/pluck/uploads`). |
| `ConnectionStrings__DefaultConnection` | Yes      | SQLite connection string (`Data Source=/app/pluck/pluck.db;`).                         |
| `ASPNETCORE_HTTP_PORTS`                | No       | Internal HTTP port (Default: `8080`).                                                  |

### Starting the Server

```bash
docker compose up -d
```

The server will automatically initialize the SQLite database (`/app/pluck/pluck.db`), create the upload directory (
`/app/pluck/uploads`), and start the 10-minute background cleanup service.

Connect your CLI client to the self-hosted instance:

```bash
pluck config --server http://YOUR_SERVER_IP:8080 --key YOUR_SECURE_ADMIN_KEY
```

### Production Deployment (with TLS)

For production use, the Pluck API should be placed behind a reverse proxy that handles TLS termination, rate limiting,
and upload buffering. A ready-to-use Nginx configuration is included in the repository.

#### 1. Obtain TLS Certificates

Use [Let's Encrypt](https://letsencrypt.org/) with [certbot](https://certbot.eff.org/) to get free TLS certificates:

```bash
sudo certbot certonly --standalone -d YOUR_DOMAIN
```

Then copy the generated certificates into a `certs/` directory in the project root:

```bash
mkdir -p certs
sudo cp /etc/letsencrypt/live/YOUR_DOMAIN/fullchain.pem ./certs/
sudo cp /etc/letsencrypt/live/YOUR_DOMAIN/privkey.pem ./certs/
```

#### 2. Update the Nginx Config

Open `nginx/nginx.conf` and replace `server_name _` with your domain:

```nginx
server_name yourdomain.com;
```

#### 3. Start the Production Stack

```bash
docker compose -f compose.prod.yaml up -d
```

This starts both the Nginx reverse proxy and the Pluck API. The API is **not** exposed to the
host — only Nginx is accessible on ports 80 (HTTP → HTTPS redirect) and 443 (HTTPS).

#### 4. Connect the CLI

```bash
pluck config --server https://YOUR_DOMAIN --key YOUR_SECURE_ADMIN_KEY
```

#### What the Reverse Proxy Provides

- **TLS Termination**: Encrypts all traffic (API keys, passwords, file data) in transit using
  TLS 1.2/1.3. The API itself only handles plain HTTP on the isolated Docker network.
- **Rate Limiting**: The public download endpoint (`/f/`) is rate-limited to 5 requests/second
  per IP with a burst allowance of 10, preventing download abuse.
- **Upload Buffering**: Large file uploads (up to 10GB) are streamed directly to the API without
  Nginx buffering the entire file in memory.
- **HTTP → HTTPS Redirect**: All HTTP requests on port 80 are automatically redirected to HTTPS.

---

## License

Pluck uses component-specific open-source licenses:

- **Pluck API (`src/Pluck.Api`) & Shared Library (`src/Pluck.Shared`)**: Licensed under
  the [GNU Affero General Public License v3.0 (AGPL-3.0)](./LICENSE).
- **Pluck CLI (`src/Pluck.Cli`)**: Licensed under
  the [GNU General Public License v3.0 (GPL-3.0)](./src/Pluck.Cli/LICENSE).
