# Email domain lists

| File | Source | License | Effect |
|---|---|---|---|
| `disposable-email-domains.txt` | https://github.com/disposable-email-domains/disposable-email-domains (`disposable_email_blocklist.conf`) | CC0 1.0 | Always blocked |
| `spam-domains.txt` | https://github.com/tsirolnik/spam-domains-list (`spamdomains.txt`) | MIT | Blocked unless listed in `trusted-email-providers.txt` |
| `trusted-email-providers.txt` | HalfQR | Project license | Overrides `spam-domains.txt` only |

A domain is blocked if it, or any parent domain, appears in a blocking list. For example, `inbox.mailinator.com` is blocked by `mailinator.com`.

Refresh with `.helpers/update-email-blocklists.sh`.
