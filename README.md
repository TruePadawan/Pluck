# Pluck

Pluck is a lightweight, self-hosted ephemeral file-sharing application.  
It consists of a self-hosted ASP.NET Core Web API and a feature-rich C# CLI client with Spectre.Console UI formatting,
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
    - [pluck key-gen](#pluck-key-gen)
    - [pluck create-user](#pluck-create-user-admin-only)
    - [pluck revoke-user](#pluck-revoke-user-admin-only)
- [Self-Hosting Pluck API](#self-hosting-pluck-api)
    - [compose.yaml](#composeyaml)
    - [Environment Variables](#environment-variables)
    - [Starting the Server](#starting-the-server)
- [License](#license)

---

## How Pluck Works

Pluck is designed for temporary file sharing with automatic lifecycle management and zero maintenance:

1. **Upload & Configure Expiry:** When sharing a file via `pluck share`, you set an optional Time-To-Live (`--ttl` in
   hours, default 24 hours), an optional maximum download count (`--downloads`) or an optional password (`--pwd`)
   to secure the file.
2. **Instant Link Generation:** The server generates a unique download token and link, which the CLI automatically
   copies to your system clipboard.
3. **Automated Background Cleanup:** The Pluck API runs an automated background cleanup worker every 10 minutes.
4. **Permanent File Purging:** A file is immediately rendered unavailable and permanently purged from both disk storage
   and the database as soon as:

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

Install natively via Windows Package Manager (WinGet):

```cmd
winget install pluck.cli
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

Uploads a file or folder to the configured Pluck server with real-time transfer speed and progress tracking.
Automatically copies the generated download URL to the clipboard.

```bash
pluck share <filepath> [--ttl <hours>] [--downloads <count>] [--pwd <password>]
```

**Arguments:**

- `<filepath>` *(Required)*: The path to the local file/folder to upload.

**Options:**

- `--ttl <hours>` *(Default: `24`)*: Time-to-live for the file in hours.
- `--downloads <count>` *(Optional)*: Maximum allowed downloads before the file automatically expires.
- `--pwd <password>` *(Optional)*: Password to secure the file with.

---

### `pluck get`

Downloads a file or folder from a Pluck instance URL with real-time progress tracking.

```bash
pluck get <url> [--save-dir <directory>] [--pwd <password>]
```

**Arguments:**

- `<url>` *(Required)*: The full download URL of the file to retrieve.

**Options:**

- `--save-dir <dir>` *(Optional)*: The output directory to save the file/folder into. Defaults to the current working
  directory.
- `--pwd <password>` *(Optional)*: Password to decrypt the file with.

---

### `pluck list`

Lists active (unexpired) files on the connected Pluck instance.

```bash
pluck list [--name <username>]
```

**Options:**

- `--name <username>` *(Optional, Admin only)*: Filter listed files by owner username.

---

### `pluck file`

Displays detailed metadata for a specific file using its token, and copies the download URL to the clipboard.

```bash
pluck file <token>
```

**Arguments:**

- `<token>` *(Required)*: The unique token identifier of the file.

---

### `pluck key-gen`

Generates a random 32-character API key using GUID formatting and copies it directly to your system clipboard. Useful
for creating initial admin keys or new user credentials.

```bash
pluck key-gen
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
pluck revoke-user <name> [--force]
```

**Arguments:**

- `<name>` *(Required)*: The username of the account to revoke.

**Options:**

- `--force` *(Optional)*: Skips the interactive confirmation prompt.

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

## License

Pluck uses component-specific open-source licenses:

- **Pluck API (`src/Pluck.Api`) & Shared Library (`src/Pluck.Shared`)**: Licensed under
  the [GNU Affero General Public License v3.0 (AGPL-3.0)](./LICENSE).
- **Pluck CLI (`src/Pluck.Cli`)**: Licensed under
  the [GNU General Public License v3.0 (GPL-3.0)](./src/Pluck.Cli/LICENSE).
