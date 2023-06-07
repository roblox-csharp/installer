#!/bin/sudo bash

snap_directory="/snap"
snapd_directory="/var/lib/snapd/snap"

# Check if /snap directory exists
if [ -d "$snap_directory" ]; then
  # Remove /snap directory
  rm -rf "$snap_directory"
fi

# Create symbolic link from /snap to /var/lib/snapd/snap
ln -s "$snapd_directory" "$snap_directory"
