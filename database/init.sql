-- ============================================================================
-- SWP391 Project Management System - PostgreSQL Database Setup
-- ============================================================================

-- Create database and user
-- Note: Run these commands as postgres superuser first
-- CREATE DATABASE swp391_db;
-- CREATE USER admin WITH PASSWORD '123456';
-- GRANT ALL PRIVILEGES ON DATABASE swp391_db TO admin;

-- Connect to the database
-- \c swp391_db

-- Grant schema privileges
GRANT ALL ON SCHEMA public TO admin;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO admin;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO admin;

-- ============================================================================
-- CORE ENTITIES - User Management
-- ============================================================================

CREATE TYPE user_role AS ENUM ('admin', 'lecturer', 'student');
CREATE TYPE user_status AS ENUM ('active', 'inactive');

CREATE TABLE "USER" (
    user_id SERIAL PRIMARY KEY,
    email VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(100) NOT NULL,
    role user_role NOT NULL,

    -- Student-specific fields
    student_code VARCHAR(50) UNIQUE,
    github_username VARCHAR(100),
    jira_account_id VARCHAR(100),

    -- Lecturer-specific fields
    phone VARCHAR(20),
    status user_status DEFAULT 'active',

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_user_role ON "USER"(role);
CREATE INDEX idx_user_email ON "USER"(email);
CREATE INDEX idx_user_student_code ON "USER"(student_code);
CREATE INDEX idx_user_github_username ON "USER"(github_username);

COMMENT ON TABLE "USER" IS 'All users: Admin, Lecturer, Student';

-- ============================================================================
-- Student Groups
-- ============================================================================

CREATE TABLE STUDENT_GROUP (
    group_id SERIAL PRIMARY KEY,
    group_code VARCHAR(50) UNIQUE NOT NULL,
    group_name VARCHAR(200) NOT NULL,
    lecturer_id INTEGER NOT NULL REFERENCES "USER"(user_id),
    leader_id INTEGER REFERENCES "USER"(user_id),
    status user_status DEFAULT 'active',

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_group_lecturer ON STUDENT_GROUP(lecturer_id);
CREATE INDEX idx_group_leader ON STUDENT_GROUP(leader_id);

COMMENT ON TABLE STUDENT_GROUP IS 'Admin: manage student groups, assign lecturers to groups';

-- ============================================================================
-- Group Membership
-- ============================================================================

CREATE TABLE GROUP_MEMBER (
    membership_id SERIAL PRIMARY KEY,
    group_id INTEGER NOT NULL REFERENCES STUDENT_GROUP(group_id),
    user_id INTEGER NOT NULL REFERENCES "USER"(user_id),
    is_leader BOOLEAN DEFAULT FALSE,
    joined_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT unique_group_member UNIQUE (group_id, user_id)
);

CREATE INDEX idx_group_member_group ON GROUP_MEMBER(group_id);
CREATE INDEX idx_group_member_user ON GROUP_MEMBER(user_id);

COMMENT ON TABLE GROUP_MEMBER IS 'Lecturer: manage students in assigned groups';

-- ============================================================================
-- PROJECT & INTEGRATION
-- ============================================================================

CREATE TYPE project_status AS ENUM ('active', 'completed');

CREATE TABLE PROJECT (
    project_id SERIAL PRIMARY KEY,
    group_id INTEGER UNIQUE NOT NULL REFERENCES STUDENT_GROUP(group_id),
    project_name VARCHAR(200) NOT NULL,
    description TEXT,
    start_date DATE,
    end_date DATE,
    status project_status DEFAULT 'active',

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_project_group ON PROJECT(group_id);

COMMENT ON TABLE PROJECT IS 'One project per group';

-- ============================================================================
-- Jira Integration
-- ============================================================================

CREATE TYPE sync_status AS ENUM ('pending', 'syncing', 'success', 'failed');

CREATE TABLE JIRA_INTEGRATION (
    integration_id SERIAL PRIMARY KEY,
    project_id INTEGER UNIQUE NOT NULL REFERENCES PROJECT(project_id),
    jira_url VARCHAR(255) NOT NULL,
    api_token VARCHAR(255) NOT NULL,
    jira_email VARCHAR(100) NOT NULL,
    project_key VARCHAR(50) NOT NULL,
    last_sync TIMESTAMP,
    sync_status sync_status DEFAULT 'pending',

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_jira_project ON JIRA_INTEGRATION(project_id);

COMMENT ON TABLE JIRA_INTEGRATION IS 'Admin: configure Jira integration';
COMMENT ON COLUMN JIRA_INTEGRATION.api_token IS 'Encrypted token';

-- ============================================================================
-- GitHub Integration
-- ============================================================================

CREATE TABLE GITHUB_INTEGRATION (
    integration_id SERIAL PRIMARY KEY,
    project_id INTEGER UNIQUE NOT NULL REFERENCES PROJECT(project_id),
    repo_url VARCHAR(255) NOT NULL,
    api_token VARCHAR(255) NOT NULL,
    repo_owner VARCHAR(100) NOT NULL,
    repo_name VARCHAR(100) NOT NULL,
    last_sync TIMESTAMP,
    sync_status sync_status DEFAULT 'pending',

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_github_project ON GITHUB_INTEGRATION(project_id);

COMMENT ON TABLE GITHUB_INTEGRATION IS 'Admin: configure GitHub integration';
COMMENT ON COLUMN GITHUB_INTEGRATION.api_token IS 'Encrypted token';

-- ============================================================================
-- JIRA SYNC - Data from Jira
-- ============================================================================

CREATE TYPE jira_priority AS ENUM ('highest', 'high', 'medium', 'low', 'lowest');

CREATE TABLE JIRA_ISSUE (
    jira_issue_id SERIAL PRIMARY KEY,
    project_id INTEGER NOT NULL REFERENCES PROJECT(project_id),
    issue_key VARCHAR(50) UNIQUE NOT NULL,
    jira_id VARCHAR(100) UNIQUE NOT NULL,
    issue_type VARCHAR(50) NOT NULL,
    summary VARCHAR(255) NOT NULL,
    description TEXT,
    priority jira_priority,
    status VARCHAR(50) NOT NULL,
    assignee_jira_id VARCHAR(100),
    created_date TIMESTAMP,
    updated_date TIMESTAMP,
    last_synced TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_jira_issue_project ON JIRA_ISSUE(project_id);
CREATE INDEX idx_jira_issue_key ON JIRA_ISSUE(issue_key);
CREATE INDEX idx_jira_issue_type ON JIRA_ISSUE(issue_type);
CREATE INDEX idx_jira_issue_status ON JIRA_ISSUE(status);

COMMENT ON TABLE JIRA_ISSUE IS 'Raw issues synced from Jira - source for requirements management';

-- ============================================================================
-- REQUIREMENTS & TASKS
-- ============================================================================

CREATE TYPE requirement_type AS ENUM ('functional', 'non-functional');
CREATE TYPE priority_level AS ENUM ('high', 'medium', 'low');

CREATE TABLE REQUIREMENT (
    requirement_id SERIAL PRIMARY KEY,
    project_id INTEGER NOT NULL REFERENCES PROJECT(project_id),
    jira_issue_id INTEGER UNIQUE REFERENCES JIRA_ISSUE(jira_issue_id),
    requirement_code VARCHAR(50) UNIQUE NOT NULL,
    title VARCHAR(255) NOT NULL,
    description TEXT,
    requirement_type requirement_type NOT NULL,
    priority priority_level DEFAULT 'medium',
    created_by INTEGER NOT NULL REFERENCES "USER"(user_id),

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_requirement_project ON REQUIREMENT(project_id);
CREATE INDEX idx_requirement_jira ON REQUIREMENT(jira_issue_id);
CREATE INDEX idx_requirement_type ON REQUIREMENT(requirement_type);

COMMENT ON TABLE REQUIREMENT IS 'Team Leader: manage group requirements (synced from Jira) | Lecturer: view requirements';

-- ============================================================================
-- Tasks
-- ============================================================================

CREATE TYPE task_status AS ENUM ('todo', 'in_progress', 'done');

CREATE TABLE TASK (
    task_id SERIAL PRIMARY KEY,
    requirement_id INTEGER REFERENCES REQUIREMENT(requirement_id),
    jira_issue_id INTEGER UNIQUE REFERENCES JIRA_ISSUE(jira_issue_id),
    assigned_to INTEGER REFERENCES "USER"(user_id),
    title VARCHAR(255) NOT NULL,
    description TEXT,
    status task_status DEFAULT 'todo',
    priority priority_level DEFAULT 'medium',
    due_date DATE,
    completed_at TIMESTAMP,

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_task_requirement ON TASK(requirement_id);
CREATE INDEX idx_task_assigned ON TASK(assigned_to);
CREATE INDEX idx_task_status ON TASK(status);

COMMENT ON TABLE TASK IS 'Team Leader: assign tasks to members, monitor task progress | Team Member: view assigned tasks, update task status | Lecturer: view tasks';

-- ============================================================================
-- GITHUB SYNC - Commit data from GitHub
-- ============================================================================

CREATE TABLE GITHUB_COMMIT (
    github_commit_id SERIAL PRIMARY KEY,
    project_id INTEGER NOT NULL REFERENCES PROJECT(project_id),
    commit_sha VARCHAR(255) UNIQUE NOT NULL,
    author_username VARCHAR(100) NOT NULL,
    author_email VARCHAR(100),
    commit_message TEXT,
    additions INTEGER DEFAULT 0,
    deletions INTEGER DEFAULT 0,
    changed_files INTEGER DEFAULT 0,
    commit_date TIMESTAMP NOT NULL,
    branch_name VARCHAR(100),
    last_synced TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_github_commit_project ON GITHUB_COMMIT(project_id);
CREATE INDEX idx_github_commit_sha ON GITHUB_COMMIT(commit_sha);
CREATE INDEX idx_github_commit_author ON GITHUB_COMMIT(author_username);
CREATE INDEX idx_github_commit_date ON GITHUB_COMMIT(commit_date);

COMMENT ON TABLE GITHUB_COMMIT IS 'Raw commits synced from GitHub API';

-- ============================================================================
-- Commits linked to users
-- ============================================================================

CREATE TABLE COMMIT (
    commit_id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES "USER"(user_id),
    github_commit_id INTEGER UNIQUE NOT NULL REFERENCES GITHUB_COMMIT(github_commit_id),
    project_id INTEGER NOT NULL REFERENCES PROJECT(project_id),
    commit_message TEXT,
    additions INTEGER DEFAULT 0,
    deletions INTEGER DEFAULT 0,
    changed_files INTEGER DEFAULT 0,
    commit_date TIMESTAMP NOT NULL,

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_commit_user ON COMMIT(user_id);
CREATE INDEX idx_commit_project ON COMMIT(project_id);
CREATE INDEX idx_commit_date ON COMMIT(commit_date);

COMMENT ON TABLE COMMIT IS 'Commits linked to students (matched by github_username) - base data for all commit reports';

-- ============================================================================
-- DOCUMENT GENERATION - SRS from requirements
-- ============================================================================

CREATE TYPE document_status AS ENUM ('draft', 'published');

CREATE TABLE SRS_DOCUMENT (
    document_id SERIAL PRIMARY KEY,
    project_id INTEGER NOT NULL REFERENCES PROJECT(project_id),
    version VARCHAR(50) NOT NULL,
    document_title VARCHAR(255) NOT NULL,
    introduction TEXT,
    scope TEXT,
    file_path VARCHAR(255),
    status document_status DEFAULT 'draft',
    generated_by INTEGER NOT NULL REFERENCES "USER"(user_id),
    generated_at TIMESTAMP,

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_srs_project ON SRS_DOCUMENT(project_id);
CREATE INDEX idx_srs_version ON SRS_DOCUMENT(version);

COMMENT ON TABLE SRS_DOCUMENT IS 'Header record for the SRS. Content is built by joining with SRS_INCLUDED_REQUIREMENT';

-- ============================================================================
-- SRS Requirements Bridge Table
-- ============================================================================

CREATE TABLE SRS_INCLUDED_REQUIREMENT (
    id SERIAL PRIMARY KEY,
    document_id INTEGER NOT NULL REFERENCES SRS_DOCUMENT(document_id),
    requirement_id INTEGER NOT NULL REFERENCES REQUIREMENT(requirement_id),
    section_number VARCHAR(20),

    -- Snapshots to preserve history
    snapshot_title VARCHAR(255),
    snapshot_description TEXT,

    CONSTRAINT unique_doc_req UNIQUE (document_id, requirement_id)
);

CREATE INDEX idx_srs_included_doc ON SRS_INCLUDED_REQUIREMENT(document_id);
CREATE INDEX idx_srs_included_req ON SRS_INCLUDED_REQUIREMENT(requirement_id);

COMMENT ON TABLE SRS_INCLUDED_REQUIREMENT IS 'Links specific requirements to an SRS version. Ensures traceability from Jira -> Req -> SRS.';
COMMENT ON COLUMN SRS_INCLUDED_REQUIREMENT.section_number IS 'e.g. 1.1, 2.0 - Order in document';
COMMENT ON COLUMN SRS_INCLUDED_REQUIREMENT.snapshot_title IS 'Title at time of generation';
COMMENT ON COLUMN SRS_INCLUDED_REQUIREMENT.snapshot_description IS 'Description at time of generation';

-- ============================================================================
-- REPORTS - Generated and stored for viewing
-- ============================================================================

CREATE TYPE report_type AS ENUM ('task_assignment', 'task_completion', 'weekly', 'sprint');

CREATE TABLE PROGRESS_REPORT (
    report_id SERIAL PRIMARY KEY,
    project_id INTEGER NOT NULL REFERENCES PROJECT(project_id),
    report_type report_type NOT NULL,
    report_period_start DATE,
    report_period_end DATE,
    report_data JSONB NOT NULL,
    summary TEXT,
    file_path VARCHAR(255),
    generated_by INTEGER NOT NULL REFERENCES "USER"(user_id),
    generated_at TIMESTAMP NOT NULL,

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_progress_project ON PROGRESS_REPORT(project_id);
CREATE INDEX idx_progress_type ON PROGRESS_REPORT(report_type);
CREATE INDEX idx_progress_generated ON PROGRESS_REPORT(generated_at);

COMMENT ON TABLE PROGRESS_REPORT IS 'Problem 2: Tổng hợp báo cáo phân công và thực hiện công việc | Lecturer: view project progress reports';

-- ============================================================================
-- Commit Statistics
-- ============================================================================

CREATE TABLE COMMIT_STATISTICS (
    stat_id SERIAL PRIMARY KEY,
    project_id INTEGER NOT NULL REFERENCES PROJECT(project_id),
    user_id INTEGER NOT NULL REFERENCES "USER"(user_id),
    period_start DATE NOT NULL,
    period_end DATE NOT NULL,
    total_commits INTEGER DEFAULT 0,
    total_additions INTEGER DEFAULT 0,
    total_deletions INTEGER DEFAULT 0,
    total_changed_files INTEGER DEFAULT 0,
    commit_frequency DECIMAL DEFAULT 0,
    avg_commit_size INTEGER DEFAULT 0,

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT unique_stat_period UNIQUE (project_id, user_id, period_start, period_end)
);

CREATE INDEX idx_commit_stats_project ON COMMIT_STATISTICS(project_id);
CREATE INDEX idx_commit_stats_user ON COMMIT_STATISTICS(user_id);
CREATE INDEX idx_commit_stats_period ON COMMIT_STATISTICS(period_start);

COMMENT ON TABLE COMMIT_STATISTICS IS 'Problem 3: Báo cáo đánh giá tần suất và chất lượng các lần commit | Lecturer: view GitHub commit statistics | Team Member: view personal commit statistics';

-- ============================================================================
-- Team Commit Summary
-- ============================================================================

CREATE TABLE TEAM_COMMIT_SUMMARY (
    summary_id SERIAL PRIMARY KEY,
    project_id INTEGER NOT NULL REFERENCES PROJECT(project_id),
    summary_date DATE NOT NULL,
    total_commits INTEGER DEFAULT 0,
    total_additions INTEGER DEFAULT 0,
    total_deletions INTEGER DEFAULT 0,
    active_contributors INTEGER DEFAULT 0,
    summary_data JSONB,

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT unique_project_date UNIQUE (project_id, summary_date)
);

CREATE INDEX idx_team_summary_project ON TEAM_COMMIT_SUMMARY(project_id);
CREATE INDEX idx_team_summary_date ON TEAM_COMMIT_SUMMARY(summary_date);

COMMENT ON TABLE TEAM_COMMIT_SUMMARY IS 'Team Leader: view team commit summaries (aggregated from COMMIT_STATISTICS)';

-- ============================================================================
-- Personal Task Statistics
-- ============================================================================

CREATE TABLE PERSONAL_TASK_STATISTICS (
    stat_id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES "USER"(user_id),
    project_id INTEGER NOT NULL REFERENCES PROJECT(project_id),
    total_tasks INTEGER DEFAULT 0,
    completed_tasks INTEGER DEFAULT 0,
    in_progress_tasks INTEGER DEFAULT 0,
    overdue_tasks INTEGER DEFAULT 0,
    completion_rate DECIMAL DEFAULT 0,
    last_calculated TIMESTAMP,

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT unique_user_project UNIQUE (user_id, project_id)
);

CREATE INDEX idx_personal_stats_user ON PERSONAL_TASK_STATISTICS(user_id);
CREATE INDEX idx_personal_stats_project ON PERSONAL_TASK_STATISTICS(project_id);

COMMENT ON TABLE PERSONAL_TASK_STATISTICS IS 'Team Member: view personal task statistics';

-- ============================================================================
-- TRIGGERS - Auto-update timestamps
-- ============================================================================

CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

CREATE TRIGGER update_user_updated_at BEFORE UPDATE ON "USER"
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_student_group_updated_at BEFORE UPDATE ON STUDENT_GROUP
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_project_updated_at BEFORE UPDATE ON PROJECT
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_jira_integration_updated_at BEFORE UPDATE ON JIRA_INTEGRATION
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_github_integration_updated_at BEFORE UPDATE ON GITHUB_INTEGRATION
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_requirement_updated_at BEFORE UPDATE ON REQUIREMENT
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_task_updated_at BEFORE UPDATE ON TASK
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_srs_document_updated_at BEFORE UPDATE ON SRS_DOCUMENT
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_commit_statistics_updated_at BEFORE UPDATE ON COMMIT_STATISTICS
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_personal_task_statistics_updated_at BEFORE UPDATE ON PERSONAL_TASK_STATISTICS
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- ============================================================================
-- Grant permissions to admin user
-- ============================================================================

GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO admin;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO admin;

-- ============================================================================
-- Sample Data (Optional - for testing)
-- ============================================================================

-- Insert a default admin user (password: Password_1 - hashed with Microsoft.AspNetCore.Identity.PasswordHasher)
-- ============================================================================
-- SEED DATA - Example Users (Vietnamese)
-- ============================================================================
-- Note: All passwords are "Password123" for testing purposes
-- You should change these in production
-- Hash generated using Microsoft.Extensions.Identity.Core PasswordHasher (V2 format)

-- Admin User
INSERT INTO "USER" (email, password_hash, full_name, phone, role, status) VALUES
('admin@swp391.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Quản Trị Viên Hệ Thống', '+84901234567', 'admin', 'active');

-- Lecturers (Giảng viên)
INSERT INTO "USER" (email, password_hash, full_name, phone, role, status) VALUES
('nguyenvana@fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Nguyễn Văn An', '+84912345678', 'lecturer', 'active'),
('tranthib@fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Trần Thị Bình', '+84923456789', 'lecturer', 'active'),
('phamvanc@fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Phạm Văn Cường', '+84934567890', 'lecturer', 'active');

-- Students (Sinh viên)
INSERT INTO "USER" (email, password_hash, full_name, phone, student_code, github_username, jira_account_id, role, status) VALUES
-- Group 1 students
('levand@student.fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Lê Văn Dũng', '+84945678901', 'SE171234', 'levandung', 'levand@student.fpt.edu.vn', 'student', 'active'),
('ngothie@student.fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Ngô Thị Em', '+84956789012', 'SE171235', 'ngothiem', 'ngothie@student.fpt.edu.vn', 'student', 'active'),
('hoangvanf@student.fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Hoàng Văn Phong', '+84967890123', 'SE171236', 'hoangvanphong', 'hoangvanf@student.fpt.edu.vn', 'student', 'active'),
('vuthig@student.fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Vũ Thị Giang', '+84978901234', 'SE171237', 'vuthigiang', 'vuthig@student.fpt.edu.vn', 'student', 'active'),
('doanvanh@student.fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Đoàn Văn Hùng', '+84989012345', 'SE171238', 'doanvanhung', 'doanvanh@student.fpt.edu.vn', 'student', 'active'),

-- Group 2 students
('buithii@student.fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Bùi Thị Lan', '+84990123456', 'SE171239', 'buithilan', 'buithii@student.fpt.edu.vn', 'student', 'active'),
('dangvank@student.fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Đặng Văn Khoa', '+84901234567', 'SE171240', 'dangvankhoa', 'dangvank@student.fpt.edu.vn', 'student', 'active'),
('lyvanh@student.fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Lý Văn Minh', '+84912345678', 'SE171241', 'lyvanminh', 'lyvanh@student.fpt.edu.vn', 'student', 'active'),
('macthim@student.fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Mạc Thị Nga', '+84923456789', 'SE171242', 'macthinga', 'macthim@student.fpt.edu.vn', 'student', 'active'),
('phanvano@student.fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Phan Văn Ông', '+84934567890', 'SE171243', 'phanvanong', 'phanvano@student.fpt.edu.vn', 'student', 'active'),

-- Group 3 students
('tathibp@student.fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Tạ Thị Phương', '+84945678901', 'SE171244', 'tathiphuong', 'tathibp@student.fpt.edu.vn', 'student', 'active'),
('quachvanq@student.fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Quách Văn Quang', '+84956789012', 'SE171245', 'quachvanquang', 'quachvanq@student.fpt.edu.vn', 'student', 'active'),
('dinhthir@student.fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Đinh Thị Rung', '+84967890123', 'SE171246', 'dinhthirung', 'dinhthir@student.fpt.edu.vn', 'student', 'active'),
('dovans@student.fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Đỗ Văn Sơn', '+84978901234', 'SE171247', 'dovanson', 'dovans@student.fpt.edu.vn', 'student', 'active'),
('rangthibt@student.fpt.edu.vn', 'AQAAAAIAAYagAAAAEHJyvwi6uCt5eNUkyBCVR1I27D5YadrZS7/GwMT5+fvW1JfREzPV5x0lUnTajw5I3A==', 'Rạng Thị Thanh', '+84989012345', 'SE171248', 'rangthithanh', 'rangthibt@student.fpt.edu.vn', 'student', 'active');

COMMENT ON DATABASE JGMS IS 'SWP391 Project Management System - Supporting Tool for Requirements and Project Progress Management';

-- ============================================================================
-- END OF SCRIPT
-- ============================================================================
