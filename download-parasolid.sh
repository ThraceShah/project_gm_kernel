#!/bin/bash
# Download Parasolid SDK from ECS storage
# Usage: ./download-parasolid.sh

set -e

ECS_HOST="aliyun_ecs"
ECS_FILE="/root/git-lfs-storage/parasolid.tar.gz"
LOCAL_DIR="third_party"

echo "Downloading Parasolid SDK from ECS..."

# Download tar.gz from ECS
scp "$ECS_HOST:$ECS_FILE" parasolid.tar.gz

# Extract to third_party
mkdir -p "$LOCAL_DIR"
tar -xzf parasolid.tar.gz -C "$LOCAL_DIR" --preserve-permissions

# Cleanup
rm parasolid.tar.gz

echo "Done! Parasolid SDK extracted to $LOCAL_DIR/parasolid/"
echo "Size: $(du -sh $LOCAL_DIR/parasolid | cut -f1)"