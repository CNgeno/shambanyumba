#!/bin/bash
# Server-side deployment handler for ShambaNyumba
set -e

ARCHIVE_PATH=$1
TARGET_DIR="/home/builder/SmartEstate/publish"
BACKUP_DIR="/home/builder/SmartEstate/backups"
TIMESTAMP=$(date +"%Y%m%d-%H%M%S")
SERVICE_NAME="smartestate" # Make sure this matches your exact systemd service name

if [ -z "$ARCHIVE_PATH" ] || [ ! -f "$ARCHIVE_PATH" ]; then
    echo "ERROR: Missing archive payload package."
    exit 1
fi

echo "Creating safe roll-back point backup..."
mkdir -p "$BACKUP_DIR/$TIMESTAMP"
cp -r "$TARGET_DIR/." "$BACKUP_DIR/$TIMESTAMP/"

# 🌟 CRITICAL FIX: Stop the application process first to free the locked files
echo "Stopping application service daemon to free binaries..."
sudo systemctl stop "$SERVICE_NAME"

echo "Extracting updated delta files..."
tar -xzf "$ARCHIVE_PATH" -C "$TARGET_DIR"

# 🌟 CRITICAL FIX: Spin the application process back up now that files are replaced
echo "Starting application service daemon..."
sudo systemctl start "$SERVICE_NAME"

echo "Running system automated health check..."
sleep 15 # Give the app and database migrations enough time to finish starting up
HEALTH_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/health || echo "FAILED")

if [ "$HEALTH_STATUS" -eq 200 ]; then
    echo "SUCCESS: Deployment online and healthy."
    exit 0
else
    echo "CRITICAL: Health check returned code $HEALTH_STATUS. Initiating rollback..."
    # If it fails, stop it again to safely swap the old working version back in
    sudo systemctl stop "$SERVICE_NAME" || true
    cp -r "$BACKUP_DIR/$TIMESTAMP/." "$TARGET_DIR/"
    sudo systemctl start "$SERVICE_NAME"
    echo "ROLLBACK COMPLETE: Previous software version restored."
    exit 1
fi
