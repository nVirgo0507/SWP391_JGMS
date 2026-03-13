# Database Migrations

When you change the database schema, do **two things**:

1. **Update `init.sql`** — so fresh databases get the correct schema from the start
2. **Add a numbered migration** in this folder — so existing databases are updated automatically on the next deploy

## How it works

`MigrationRunner` runs automatically on every app startup. It:
- Creates a `schema_migrations` table in the DB to track what's been applied
- Scans this folder for `*.sql` files in alphabetical order
- Skips files already recorded in `schema_migrations`
- Runs each new file in a transaction — rolls back on failure

**You never need to run migrations manually again.** Just add the file and redeploy.

---

## Local testing with Docker

### First time / fresh start
```bash
docker-compose up --build
```
This builds the API image and starts both the DB and API containers.  
The DB will be initialized from `init.sql` automatically (fresh volume).

### After changing code (no schema changes)
```bash
docker-compose up --build
```
Rebuilds the API image and restarts. The existing DB volume is kept — `MigrationRunner` runs but finds nothing new to apply.

### After adding a new migration file
```bash
docker-compose up --build
```
Same command. On startup, `MigrationRunner` detects the new `.sql` file and applies it automatically.

### Wipe everything and start completely fresh
```bash
docker-compose down -v
docker-compose up --build
```
`-v` removes the `postgres_data` volume so the DB is recreated from `init.sql` from scratch.  
Use this when you want to reset all data locally.

### Useful commands
```bash
# View live logs
docker-compose logs -f

# View only API logs
docker-compose logs -f api

# Stop containers (keep data)
docker-compose down

# Open a psql shell into the local DB
docker exec -it jgms_db_container psql -U admin -d JGMS

# Check which migrations have been applied
docker exec -it jgms_db_container psql -U admin -d JGMS -c "SELECT * FROM schema_migrations;"
```

---

## Deploying to Render

Just push to your repo — Render rebuilds the Docker image and `MigrationRunner` applies any new migrations automatically on startup. No manual steps needed.

### Running a migration manually on Render (if needed)
```bash
# Render Dashboard → Database → Shell tab, paste the SQL directly
# Or via psql:
psql "postgresql://admin:PASSWORD@HOST:5432/jgms" -f database/migrations/002_softdelete_group_members.sql
```

---

## Migration history

| # | File | Description | Applied |
|---|------|-------------|---------|
| 001 | `001_fix_api_token_length.sql` | api_token VARCHAR(255) → TEXT | 2026-02-28 |
| 002 | `002_softdelete_group_members.sql` | Add `left_at` to `group_member` for soft-delete audit trail | 2026-03-02 |

---

## Adding a new migration (workflow)

1. Make your schema change in `init.sql`
2. Add `NNN_description.sql` in this folder with just the `ALTER`/`CREATE` statements
3. Run `docker-compose up --build` locally to verify it applies cleanly
4. Push — Render picks it up automatically on the next deploy
