-- Migration 002: Soft-delete support for group_member
-- Adds left_at column to track when a student left/was removed from a group.
-- Drops the unique(group_id, user_id) constraint so students can re-join a group
-- after having left (historical rows are kept for audit trail).
-- A new partial unique index ensures only one ACTIVE membership per student per group.

-- 1. Add the left_at column
ALTER TABLE GROUP_MEMBER ADD COLUMN left_at TIMESTAMP DEFAULT NULL;

-- 2. Drop the old unique constraint
ALTER TABLE GROUP_MEMBER DROP CONSTRAINT unique_group_member;

-- 3. Add a partial unique index: only one active (left_at IS NULL) membership per student per group
CREATE UNIQUE INDEX unique_active_group_member
    ON GROUP_MEMBER (group_id, user_id)
    WHERE left_at IS NULL;

-- 4. Clean up orphaned active memberships from previously soft-deleted groups
--    (set left_at to now so they're treated as historical, not active)
UPDATE GROUP_MEMBER
SET left_at = NOW()
WHERE left_at IS NULL
  AND group_id IN (
      SELECT group_id FROM STUDENT_GROUP WHERE status = 'inactive'
  );

