-- Deprecation stage 1 for team_commit_summary:
-- Make legacy rows non-blocking for project deletion by cascading project deletes.

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint c
        JOIN pg_class t ON t.oid = c.conrelid
        WHERE c.conname = 'team_commit_summary_project_id_fkey'
          AND t.relname = 'team_commit_summary'
    ) THEN
        ALTER TABLE team_commit_summary
            DROP CONSTRAINT team_commit_summary_project_id_fkey;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = 'team_commit_summary'
    ) THEN
        ALTER TABLE team_commit_summary
            ADD CONSTRAINT team_commit_summary_project_id_fkey
            FOREIGN KEY (project_id)
            REFERENCES project(project_id)
            ON DELETE CASCADE;
    END IF;
END $$;

