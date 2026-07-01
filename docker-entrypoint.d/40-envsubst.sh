#!/bin/sh
set -eu

envsubst '${SUPABASE_URL} ${SUPABASE_ANON_KEY} ${APP_ENV} ${APP_VERSION}' \
  < /usr/share/nginx/html/env.template.js \
  > /usr/share/nginx/html/env.js
