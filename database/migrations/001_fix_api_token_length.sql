-- Migration 001: Change api_token columns from VARCHAR(255) to TEXT
-- to accommodate encrypted tokens from DataProtector (which are much longer than 255 chars)
--
-- Applied: 2026-02-28 on Render production DB
-- Run manually: psql $DATABASE_URL -f database/migrations/001_fix_api_token_length.sql

ALTER TABLE jira_integration ALTER COLUMN api_token TYPE TEXT;
ALTER TABLE github_integration ALTER COLUMN api_token TYPE TEXT;


