-- Add a simple accumulated work-hours field on TASK.
-- This supports quick tracking now and can be migrated to a detailed worklog table later.

ALTER TABLE task
    ADD COLUMN IF NOT EXISTS work_hours INTEGER NOT NULL DEFAULT 0;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'task_work_hours_non_negative_chk'
    ) THEN
        ALTER TABLE task
            ADD CONSTRAINT task_work_hours_non_negative_chk
            CHECK (work_hours >= 0);
    END IF;
END $$;


