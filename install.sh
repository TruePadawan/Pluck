#!/bin/sh
set -e

REPO="TruePadawan/Pluck"
INSTALL_DIR="/usr/local/bin"

# Detect OS
OS="$(uname -s)"
case "$OS" in
  Linux)  PLATFORM="linux" ;;
  Darwin) PLATFORM="osx" ;;
  *)      echo "Error: Unsupported OS '$OS'. Pluck CLI install script supports Linux and macOS."; exit 1 ;;
esac

# Detect Architecture
ARCH="$(uname -m)"
case "$ARCH" in
  x86_64|amd64) ARCH="x64" ;;
  arm64|aarch64) ARCH="arm64" ;;
  *)            echo "Error: Unsupported architecture '$ARCH'."; exit 1 ;;
esac

if [ "$PLATFORM" = "linux" ] && [ "$ARCH" = "arm64" ]; then
  echo "Error: Linux ARM64 builds are not currently published. Use x86_64/amd64 Linux or macOS."
  exit 1
fi

echo "Detecting latest Pluck CLI release from GitHub..."

# Fetch the latest release tag prefixed with 'cli-v'
LATEST_TAG=$(curl -s "https://api.github.com/repos/${REPO}/releases" | \
  grep -o '"tag_name": *"cli-v[^"]*"' | \
  head -n 1 | \
  sed 's/"tag_name": *"cli-v\(.*\)"/\1/')

if [ -z "$LATEST_TAG" ]; then
  echo "Error: Could not find any published Pluck CLI releases on GitHub."
  exit 1
fi

VERSION="v${LATEST_TAG}"
ASSET_NAME="pluck-${VERSION}-${PLATFORM}-${ARCH}"
DOWNLOAD_URL="https://github.com/${REPO}/releases/download/cli-${VERSION}/${ASSET_NAME}"

echo "Downloading Pluck CLI ${VERSION} (${PLATFORM}-${ARCH})..."
TMP_DIR=$(mktemp -d)
TMP_FILE="${TMP_DIR}/pluck"

if ! curl -fsSL "$DOWNLOAD_URL" -o "$TMP_FILE"; then
  echo "Error: Failed to download ${ASSET_NAME} from ${DOWNLOAD_URL}."
  rm -rf "$TMP_DIR"
  exit 1
fi

chmod +x "$TMP_FILE"

# Ensure install directory exists
if [ ! -d "$INSTALL_DIR" ]; then
  if [ -w "$(dirname "$INSTALL_DIR")" ]; then
    mkdir -p "$INSTALL_DIR"
  else
    sudo mkdir -p "$INSTALL_DIR"
  fi
fi

echo "Installing executable to ${INSTALL_DIR}/pluck..."
if [ -w "$INSTALL_DIR" ]; then
  mv "$TMP_FILE" "${INSTALL_DIR}/pluck"
else
  sudo mv "$TMP_FILE" "${INSTALL_DIR}/pluck"
fi

rm -rf "$TMP_DIR"

echo ""
echo "✓ Pluck CLI ${VERSION} installed successfully!"
echo "Run 'pluck --help' or 'pluck config' to get started."
