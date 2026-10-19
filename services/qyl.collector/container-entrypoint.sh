#!/bin/sh
set -eu

chown qyl:qyl /data
exec setpriv --reuid=qyl --regid=qyl --init-groups /app/qyl.collector "$@"
