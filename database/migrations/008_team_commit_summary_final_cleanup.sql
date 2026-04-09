-- Deprecation stage 4 for team_commit_summary:
-- Final cleanup to remove compatibility view and deprecated table.
--
-- Safe for mixed environments:
-- - If objects are already gone, this migration no-ops.
-- - Drops view first, then deprecated table.

DO $$
BEGIN
    -- Drop compatibility view from phase 2 when present.
    IF EXISTS (
        SELECT 1
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname = 'team_commit_summary'
          AND c.relkind = 'v'
    ) THEN
        DROP VIEW public.team_commit_summary;
    END IF;

    -- Drop deprecated table from phase 2 when present.
    IF EXISTS (
        SELECT 1
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname = 'team_commit_summary_deprecated'
          AND c.relkind = 'r'
    ) THEN
        DROP TABLE public.team_commit_summary_deprecated;
    END IF;

    -- Cleanup fallback for environments where legacy table still exists.
    IF EXISTS (
        SELECT 1
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname = 'team_commit_summary'
          AND c.relkind = 'r'
    ) THEN
        DROP TABLE public.team_commit_summary;
    END IF;
END $$;

