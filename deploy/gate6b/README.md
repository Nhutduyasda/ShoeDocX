# Isolated staging

Use `.github/scripts/gate6b_staging.py` only on a disposable Linux CI runner with
Docker, .NET 10.0.402, Node 22, OpenSSL, Playwright and openpyxl. Run from the parent
of `feature` and `baseline` checkouts as the workflow does. This executes real
migrations and destroys the explicitly named synthetic Compose project in cleanup.
Never point it at an existing database or operator data. No cloud credentials needed.

The script generates a private CA, SQL and ingress certificates, encrypted PFX,
random synthetic credentials and a protected runtime JSON file in runner temp.
It builds the ordinary application images, adds only the public test CA to the API
trust store, starts SQL with enforced TLS, migrates both contexts through a separate
account, then starts the actual production entrypoint under the runtime login.
Only `127.0.0.1:8443` is published. No public pilot URL exists.

This package is not yet approved for general host execution or production. See the
Gate 6B report for actual execution status and prerequisites. SQL Developer licensing
is for nonproduction only. The private CA is ephemeral test trust, not public PKI.
