-- Migration 001: Change api_token columns from VARCHAR(255) to TEXT
-- to accommodate encrypted tokens from DataProtector (which are much longer than 255 chars)
--
-- Applied: 2026-02-28 on Render production DB
-- Run manually: psql $DATABASE_URL -f database/migrations/001_fix_api_token_length.sql

DO $$
BEGIN
	IF EXISTS (
		SELECT 1
		FROM information_schema.columns
		WHERE table_schema = 'public'
		  AND table_name = 'jira_integration'
		  AND column_name = 'api_token'
		  AND data_type <> 'text'
	) THEN
		ALTER TABLE jira_integration ALTER COLUMN api_token TYPE TEXT;
	END IF;

	IF EXISTS (
		SELECT 1
		FROM information_schema.columns
		WHERE table_schema = 'public'
		  AND table_name = 'github_integration'
		  AND column_name = 'api_token'
		  AND data_type <> 'text'
	) THEN
		ALTER TABLE github_integration ALTER COLUMN api_token TYPE TEXT;
	END IF;
END $$;


