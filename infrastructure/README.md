# Local infrastructure

The canonical Compose file lives at the repository root. PostgreSQL 17 and Redis 7.4 have persistent named volumes, health checks and loopback-only host ports (5432 and 6379 by default).

Use a private local `.env` based on `.env.example`. The database password is required; there is no hard-coded production credential. Redis is unauthenticated on loopback for local development only. Applications run on the host in this phase.

`docker compose down` preserves data. Removing volumes destroys data. Do not expose this Compose setup publicly; production topology, TLS, secrets management, backups, retention and image digest pinning are separate deployment work.
