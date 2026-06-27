-- Migration: Add Atlassian OAuth fields to JiraIntegration and USER tables
-- For PostgreSQL Database

-- 1. Add OAuth fields to "USER" table (Note: USER is a reserved keyword in Postgres, must be quoted)
ALTER TABLE "USER"
    ADD COLUMN atlassian_access_token TEXT NULL,
ADD COLUMN atlassian_refresh_token TEXT NULL,
ADD COLUMN atlassian_token_expires_at TIMESTAMP WITHOUT TIME ZONE NULL;

-- 2. Add OAuth fields to jira_integration table
ALTER TABLE jira_integration
    ADD COLUMN access_token TEXT NULL,
ADD COLUMN refresh_token TEXT NULL,
ADD COLUMN cloud_id VARCHAR(255) NULL,
ADD COLUMN token_expires_at TIMESTAMP WITHOUT TIME ZONE NULL;

-- 3. (Optional) You can make the existing jira_email column nullable if OAuth replaces it entirely
-- ALTER TABLE jira_integration ALTER COLUMN jira_email DROP NOT NULL;

-- 4. (Optional) You can drop or clear the existing basic auth api_token if migrating purely to OAuth
-- ALTER TABLE jira_integration DROP COLUMN api_token;
