#!/usr/bin/env bash
# Refreshes the sign-in email domain blocklists used by HalfQR.Identity.
set -euo pipefail

target="$(cd "$(dirname "$0")/.." && pwd)/microservices/HalfQR.Identity/Resources"

curl -fsSL -o "$target/disposable-email-domains.txt" \
  https://raw.githubusercontent.com/disposable-email-domains/disposable-email-domains/main/disposable_email_blocklist.conf
curl -fsSL -o "$target/spam-domains.txt" \
  https://raw.githubusercontent.com/tsirolnik/spam-domains-list/master/spamdomains.txt

wc -l "$target/disposable-email-domains.txt" "$target/spam-domains.txt"
