# Database Migrations

When you change the database schema, do **two things**:

1. **Update `init.sql`** — so new/fresh databases get the correct schema from the start
2. **Add a numbered migration** in this folder — so existing databases can be updated

## How to run a migration on your Render database

```bash
# Option 1: Using Render's shell (Dashboard → Database → Shell tab)
# Paste the SQL directly

# Option 2: Using psql with the external connection string
psql "postgresql://admin:PASSWORD@HOST:5432/jgms" -f database/migrations/001_fix_api_token_length.sql
```

## Migration history

| # | File | Description | Applied |
|---|------|-------------|---------|
| 001 | `001_fix_api_token_length.sql` | api_token VARCHAR(255) → TEXT | 2026-02-28 |

## If you need a completely fresh database on Render

1. Go to Render Dashboard → your database
2. Delete the database
3. Re-create it (the `render.yaml` blueprint will use `init.sql` for fresh setup)
4. Or manually run: `psql $DATABASE_URL -f database/init.sql`

