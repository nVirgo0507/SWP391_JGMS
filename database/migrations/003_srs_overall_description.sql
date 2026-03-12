-- Migration 003: Add Overall Description sub-section columns to SRS_DOCUMENT
-- Allows students to provide their own content for each sub-section of
-- Section 3 (Overall Description) in the generated SRS document.
-- If not provided, the system auto-generates generic fallback content.

ALTER TABLE SRS_DOCUMENT
    ADD COLUMN IF NOT EXISTS product_perspective      TEXT DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS user_classes             TEXT DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS operating_environment    TEXT DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS assumptions_dependencies TEXT DEFAULT NULL;

