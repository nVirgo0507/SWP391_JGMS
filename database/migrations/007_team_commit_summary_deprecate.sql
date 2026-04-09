-- Deprecation stage 2 for team_commit_summary:
-- 1) Rename the legacy table to team_commit_summary_deprecated
-- 2) Keep backward read compatibility via a view named team_commit_summary
--
-- Notes:
-- - The compatibility view is read-only by default.
-- - This migration is defensive/idempotent for manual reruns.

DO $$
DECLARE
    has_old_table BOOLEAN;
    has_new_table BOOLEAN;
BEGIN
    SELECT EXISTS (
        SELECT 1
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname = 'team_commit_summary'
          AND c.relkind = 'r'
    ) INTO has_old_table;

    SELECT EXISTS (
        SELECT 1
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname = 'team_commit_summary_deprecated'
          AND c.relkind = 'r'
    ) INTO has_new_table;

    -- Rename base table once.
    IF has_old_table AND NOT has_new_table THEN
        ALTER TABLE public.team_commit_summary RENAME TO team_commit_summary_deprecated;
    END IF;

    -- Optional rename for sequence readability.
    IF to_regclass('public.team_commit_summary_summary_id_seq') IS NOT NULL
       AND to_regclass('public.team_commit_summary_deprecated_summary_id_seq') IS NULL THEN
        ALTER SEQUENCE public.team_commit_summary_summary_id_seq
            RENAME TO team_commit_summary_deprecated_summary_id_seq;
    END IF;

    -- Optional rename for PK/index names (clarity only).
    IF to_regclass('public.team_commit_summary_pkey') IS NOT NULL
       AND to_regclass('public.team_commit_summary_deprecated_pkey') IS NULL THEN
        ALTER INDEX public.team_commit_summary_pkey
            RENAME TO team_commit_summary_deprecated_pkey;
    END IF;

    IF to_regclass('public.idx_team_summary_project') IS NOT NULL
       AND to_regclass('public.idx_team_summary_project_deprecated') IS NULL THEN
        ALTER INDEX public.idx_team_summary_project
            RENAME TO idx_team_summary_project_deprecated;
    END IF;

    IF to_regclass('public.idx_team_summary_date') IS NOT NULL
       AND to_regclass('public.idx_team_summary_date_deprecated') IS NULL THEN
        ALTER INDEX public.idx_team_summary_date
            RENAME TO idx_team_summary_date_deprecated;
    END IF;

    IF to_regclass('public.unique_project_date') IS NOT NULL
       AND to_regclass('public.unique_project_date_deprecated') IS NULL THEN
        ALTER INDEX public.unique_project_date
            RENAME TO unique_project_date_deprecated;
    END IF;

    -- Create compatibility view only when name is currently free.
    IF NOT EXISTS (
        SELECT 1
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname = 'team_commit_summary'
    )
    AND EXISTS (
        SELECT 1
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname = 'team_commit_summary_deprecated'
          AND c.relkind = 'r'
    ) THEN
        EXECUTE $view$
            CREATE VIEW public.team_commit_summary AS
            SELECT
                summary_id,
                project_id,
                summary_date,
                total_commits,
                total_additions,
                total_deletions,
                active_contributors,
                summary_data,
                created_at
            FROM public.team_commit_summary_deprecated
        $view$;
    END IF;
END $$;

