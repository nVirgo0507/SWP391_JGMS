# Jira Integration Testing Guide

> **Last updated:** February 23, 2026
>
> This guide walks through testing the JGMS Jira integration end-to-end, covering prerequisites, API endpoints, expected responses, and troubleshooting.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Authentication](#2-authentication)
3. [API Endpoints Overview](#3-api-endpoints-overview)
4. [Step-by-Step Testing](#4-step-by-step-testing)
   - [Step 1: Login & Obtain JWT Token](#step-1-login--obtain-jwt-token)
   - [Step 2: Configure Jira Integration (Admin)](#step-2-configure-jira-integration-admin)
   - [Step 3: Test Jira Connection](#step-3-test-jira-connection)
   - [Step 4: Sync Issues from Jira](#step-4-sync-issues-from-jira)
   - [Step 5: View Synced Issues](#step-5-view-synced-issues)
   - [Step 6: View Single Issue Details](#step-6-view-single-issue-details)
   - [Step 7: Get Sync Status](#step-7-get-sync-status)
   - [Step 8: Update Integration Config](#step-8-update-integration-config)
   - [Step 9: Get All Integrations (Admin)](#step-9-get-all-integrations-admin)
   - [Step 10: Delete Integration](#step-10-delete-integration)
5. [Role-Based Access Control Testing](#5-role-based-access-control-testing)
6. [Common Errors & Troubleshooting](#6-common-errors--troubleshooting)
7. [Jira API Token Setup](#7-jira-api-token-setup)
8. [Sample Test Data](#8-sample-test-data)

---

## 1. Prerequisites

Before testing the Jira integration, ensure the following:

| Requirement                  | Details                                                            |
|------------------------------|--------------------------------------------------------------------|
| **JGMS Backend Running**     | The API server must be running (default: `http://localhost:5000`)   |
| **PostgreSQL Database**      | Database initialized with `init.sql`                               |
| **Jira Cloud Account**       | An Atlassian Jira Cloud instance (e.g., `https://yourteam.atlassian.net`) |
| **Jira API Token**           | Generated from [Atlassian API Tokens](https://id.atlassian.com/manage-profile/security/api-tokens) |
| **Jira Project**             | An existing Jira project with a project key (e.g., `SWP391`)      |
| **Admin Account in JGMS**    | A user with `admin` role in the system                             |
| **HTTP Client**              | Postman, curl, or the `.http` files included in the project        |

---

## 2. Authentication

All Jira integration endpoints require a JWT Bearer token. Include it in every request:

```
Authorization: Bearer <your-jwt-token>
```

---

## 3. API Endpoints Overview

| Method   | Endpoint                                         | Role Required          | Description                          |
|----------|--------------------------------------------------|------------------------|--------------------------------------|
| `POST`   | `/api/jira/projects/{projectId}/integration`     | Admin                  | Configure Jira integration           |
| `GET`    | `/api/jira/projects/{projectId}/integration`     | Admin, Lecturer, Member| Get integration config               |
| `PUT`    | `/api/jira/projects/{projectId}/integration`     | Admin                  | Update integration config            |
| `DELETE` | `/api/jira/projects/{projectId}/integration`     | Admin                  | Delete integration                   |
| `GET`    | `/api/jira/projects/{projectId}/integration/test`| Admin                  | Test Jira connection                 |
| `GET`    | `/api/jira/integrations`                         | Admin                  | List all integrations                |
| `POST`   | `/api/jira/projects/{projectId}/sync`            | Admin, Team Leader     | Sync issues from Jira                |
| `GET`    | `/api/jira/projects/{projectId}/sync-status`     | Admin, Lecturer, Member| Get sync status                      |
| `GET`    | `/api/jira/projects/{projectId}/issues`          | Admin, Lecturer, Member| Get synced issues (role-filtered)    |
| `GET`    | `/api/jira/issues/{issueKey}`                    | Admin, Lecturer, Member| Get single issue details             |

---

## 4. Step-by-Step Testing

### Step 1: Login & Obtain JWT Token

Login as an **admin** user to get a JWT token.

**Request:**
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "admin@fpt.edu.vn",
  "password": "your-admin-password"
}
```

**Expected Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6...",
  "user": {
    "userId": 1,
    "email": "admin@fpt.edu.vn",
    "role": "admin"
  }
}
```

> 💡 Save the `token` value — you'll use it as `Bearer <token>` in all subsequent requests.

---

### Step 2: Configure Jira Integration (Admin)

Set up the Jira integration for a specific JGMS project.

**Request:**
```http
POST /api/jira/projects/1/integration
Authorization: Bearer <admin-jwt-token>
Content-Type: application/json

{
  "jiraUrl": "https://yourteam.atlassian.net",
  "jiraEmail": "admin@fpt.edu.vn",
  "apiToken": "ATATT3xFfGN0xxxxxxxxxxxxxxxxxxxx",
  "projectKey": "SWP391"
}
```

**Expected Response (200 OK):**
```json
{
  "integrationId": 1,
  "projectId": 1,
  "projectName": "SWP391 Project",
  "jiraUrl": "https://yourteam.atlassian.net",
  "jiraEmail": "admin@fpt.edu.vn",
  "projectKey": "SWP391",
  "lastSync": null,
  "syncStatus": "pending",
  "createdAt": "2026-02-23T06:00:00Z",
  "updatedAt": "2026-02-23T06:00:00Z"
}
```

**Validation Points:**
- ✅ `syncStatus` should be `"pending"` (no sync has occurred yet)
- ✅ `lastSync` should be `null`
- ✅ The system validates the Jira URL, email, and API token before saving

**Error Cases to Test:**
| Scenario | Expected Status | Expected Message |
|----------|----------------|-----------------|
| Invalid Jira URL | 400 | `"Failed to connect to Jira. Please check your credentials and URL."` |
| Invalid API token | 400 | `"Failed to connect to Jira. Please check your credentials and URL."` |
| Non-existent project key | 400 | `"Failed to get Jira project: ..."` |
| Integration already exists | 400 | `"Jira integration already exists for this project. Use update endpoint instead."` |
| Non-admin user | 401 | `"Only administrators can configure Jira integration"` |
| Invalid project ID | 400 | `"Project not found"` |

---

### Step 3: Test Jira Connection

Verify the stored Jira credentials are still valid.

**Request:**
```http
GET /api/jira/projects/1/integration/test
Authorization: Bearer <admin-jwt-token>
```

**Expected Response (200 OK):**
```json
{
  "isConnected": true,
  "message": "Successfully connected to Jira project: SWP391 Project",
  "jiraProjectName": "SWP391 Project",
  "jiraProjectKey": "SWP391",
  "testedAt": "2026-02-23T06:05:00Z"
}
```

**Validation Points:**
- ✅ `isConnected` should be `true`
- ✅ `jiraProjectName` and `jiraProjectKey` should match your Jira project

---

### Step 4: Sync Issues from Jira

Pull all issues from the Jira project into the JGMS database.

**Request:**
```http
POST /api/jira/projects/1/sync
Authorization: Bearer <admin-jwt-token>
```

**Expected Response (200 OK) — Successful Sync:**
```json
{
  "totalIssues": 15,
  "newIssues": 15,
  "updatedIssues": 0,
  "failedIssues": 0,
  "syncTime": "2026-02-23T06:10:00Z",
  "status": "success",
  "errors": [],
  "warnings": []
}
```

**Expected Response — Subsequent Sync (updates existing issues):**
```json
{
  "totalIssues": 16,
  "newIssues": 1,
  "updatedIssues": 15,
  "failedIssues": 0,
  "syncTime": "2026-02-23T07:00:00Z",
  "status": "success",
  "errors": [],
  "warnings": []
}
```

**Validation Points:**
- ✅ `status` should be `"success"`
- ✅ `totalIssues` should match the number of issues in your Jira project
- ✅ First sync: `newIssues` should equal `totalIssues`, `updatedIssues` should be `0`
- ✅ Subsequent syncs: `updatedIssues` should be > 0, `newIssues` only for truly new issues
- ✅ `errors` array should be empty on a successful sync
- ✅ `failedIssues` should be `0`

**Error Cases to Test:**
| Scenario | Expected Status | Expected Message |
|----------|----------------|-----------------|
| No integration configured | 400 | `"Jira integration not configured for this project"` |
| Non-admin, non-leader user | 401 | `"Only administrators or project leaders can sync issues"` |

---

### Step 5: View Synced Issues

Retrieve all synced issues for a project. Results are filtered by user role.

**Request:**
```http
GET /api/jira/projects/1/issues
Authorization: Bearer <jwt-token>
```

**Expected Response (200 OK):**
```json
[
  {
    "jiraIssueId": 1,
    "issueKey": "SWP391-1",
    "jiraId": "10001",
    "issueType": "Story",
    "summary": "User login functionality",
    "description": "Implement user login with JWT authentication",
    "priority": "high",
    "status": "In Progress",
    "assigneeJiraId": "5f1b2c3d4e5f6a7b8c9d0e1f",
    "assigneeName": null,
    "createdDate": "2026-02-01T10:00:00Z",
    "updatedDate": "2026-02-20T15:30:00Z",
    "lastSynced": "2026-02-23T06:10:00Z"
  },
  {
    "jiraIssueId": 2,
    "issueKey": "SWP391-2",
    "jiraId": "10002",
    "issueType": "Task",
    "summary": "Set up database schema",
    "description": "Create PostgreSQL tables for the application",
    "priority": "medium",
    "status": "Done",
    "assigneeJiraId": null,
    "assigneeName": null,
    "createdDate": "2026-02-01T11:00:00Z",
    "updatedDate": "2026-02-15T09:00:00Z",
    "lastSynced": "2026-02-23T06:10:00Z"
  }
]
```

**Role-Based Filtering:**
| Role         | Issues Returned                                  |
|--------------|--------------------------------------------------|
| Admin        | All issues in the project                        |
| Lecturer     | All issues in the project (read-only)            |
| Team Leader  | All issues in their project                      |
| Student      | Only issues assigned to them (by `assigneeJiraId`) |

---

### Step 6: View Single Issue Details

Get details for a specific issue by its Jira issue key.

**Request:**
```http
GET /api/jira/issues/SWP391-1
Authorization: Bearer <jwt-token>
```

**Expected Response (200 OK):**
```json
{
  "jiraIssueId": 1,
  "issueKey": "SWP391-1",
  "jiraId": "10001",
  "issueType": "Story",
  "summary": "User login functionality",
  "description": "Implement user login with JWT authentication",
  "priority": "high",
  "status": "In Progress",
  "assigneeJiraId": "5f1b2c3d4e5f6a7b8c9d0e1f",
  "assigneeName": null,
  "createdDate": "2026-02-01T10:00:00Z",
  "updatedDate": "2026-02-20T15:30:00Z",
  "lastSynced": "2026-02-23T06:10:00Z"
}
```

---

### Step 7: Get Sync Status

Check the current synchronization status for a project.

**Request:**
```http
GET /api/jira/projects/1/sync-status
Authorization: Bearer <jwt-token>
```

**Expected Response (200 OK):**
```json
{
  "totalIssues": 15,
  "newIssues": 0,
  "updatedIssues": 0,
  "failedIssues": 0,
  "syncTime": "2026-02-23T06:10:00Z",
  "status": "success",
  "errors": [],
  "warnings": []
}
```

**Possible `status` Values:**
| Status    | Description                          |
|-----------|--------------------------------------|
| `pending` | Integration configured, never synced |
| `syncing` | Sync currently in progress           |
| `success` | Last sync completed successfully     |
| `failed`  | Last sync failed                     |

---

### Step 8: Update Integration Config

Update an existing Jira integration configuration.

**Request:**
```http
PUT /api/jira/projects/1/integration
Authorization: Bearer <admin-jwt-token>
Content-Type: application/json

{
  "jiraUrl": "https://yourteam.atlassian.net",
  "jiraEmail": "newemail@fpt.edu.vn",
  "apiToken": "ATATT3xFfGN0_NEW_TOKEN_xxxxxxxx",
  "projectKey": "SWP391"
}
```

**Expected Response (200 OK):**
```json
{
  "integrationId": 1,
  "projectId": 1,
  "projectName": "SWP391 Project",
  "jiraUrl": "https://yourteam.atlassian.net",
  "jiraEmail": "newemail@fpt.edu.vn",
  "projectKey": "SWP391",
  "lastSync": "2026-02-23T06:10:00Z",
  "syncStatus": "success",
  "createdAt": "2026-02-23T06:00:00Z",
  "updatedAt": "2026-02-23T07:00:00Z"
}
```

---

### Step 9: Get All Integrations (Admin)

List all Jira integrations across all projects.

**Request:**
```http
GET /api/jira/integrations
Authorization: Bearer <admin-jwt-token>
```

**Expected Response (200 OK):**
```json
[
  {
    "integrationId": 1,
    "projectId": 1,
    "projectName": "SWP391 Project",
    "jiraUrl": "https://yourteam.atlassian.net",
    "jiraEmail": "admin@fpt.edu.vn",
    "projectKey": "SWP391",
    "lastSync": "2026-02-23T06:10:00Z",
    "syncStatus": "success",
    "createdAt": "2026-02-23T06:00:00Z",
    "updatedAt": "2026-02-23T06:10:00Z"
  }
]
```

---

### Step 10: Delete Integration

Remove a Jira integration from a project.

**Request:**
```http
DELETE /api/jira/projects/1/integration
Authorization: Bearer <admin-jwt-token>
```

**Expected Response (200 OK):**
```json
{
  "message": "Jira integration deleted successfully"
}
```

---

## 5. Role-Based Access Control Testing

Test each endpoint with different user roles to verify access control:

### Test Matrix

| Endpoint                              | Admin | Lecturer | Team Leader | Student (Member) | Unauthorized User |
|---------------------------------------|:-----:|:--------:|:-----------:|:----------------:|:-----------------:|
| `POST .../integration` (configure)    |  ✅   |   ❌ 401  |   ❌ 401     |     ❌ 401        |      ❌ 401        |
| `GET .../integration` (view config)   |  ✅   |   ✅     |   ✅         |     ✅            |      ❌ 401        |
| `PUT .../integration` (update)        |  ✅   |   ❌ 401  |   ❌ 401     |     ❌ 401        |      ❌ 401        |
| `DELETE .../integration` (delete)     |  ✅   |   ❌ 401  |   ❌ 401     |     ❌ 401        |      ❌ 401        |
| `GET .../integration/test`            |  ✅   |   ❌ 401  |   ❌ 401     |     ❌ 401        |      ❌ 401        |
| `GET /integrations` (list all)        |  ✅   |   ❌ 401  |   ❌ 401     |     ❌ 401        |      ❌ 401        |
| `POST .../sync`                       |  ✅   |   ❌ 401  |   ✅         |     ❌ 401        |      ❌ 401        |
| `GET .../sync-status`                 |  ✅   |   ✅     |   ✅         |     ✅            |      ❌ 401        |
| `GET .../issues` (list)               |  ✅ All|   ✅ All  |   ✅ All     |     ✅ Assigned    |      ❌ 401        |
| `GET /issues/{key}` (detail)          |  ✅   |   ✅     |   ✅         |     ✅ If assigned |      ❌ 401        |

### RBAC Test Procedure

1. **Create test users** for each role: `admin`, `lecturer`, `student` (leader), `student` (member)
2. **Login as each user** and obtain their JWT tokens
3. **Call each endpoint** with each user's token
4. **Verify** that:
   - Authorized users get `200 OK` with correct data
   - Unauthorized users get `401 Unauthorized` with an appropriate message
   - Students only see issues assigned to them (by matching `assigneeJiraId` to `user.JiraAccountId`)

---

## 6. Common Errors & Troubleshooting

### Error: Jira API Removed (410 Gone)

```json
{
  "status": "failed",
  "errors": [
    "Sync failed: Failed to get Jira issues: Gone - {\"errorMessages\":[\"The requested API has been removed. Please migrate to the /rest/api/3/search/jql API.\"]}"
  ]
}
```

**Cause:** Atlassian removed the old `GET /rest/api/3/search` endpoint.

**Fix:** The codebase has been updated to use `POST /rest/api/3/search/jql`. Make sure you are running the latest version of the backend. Rebuild and redeploy:

```bash
dotnet build
dotnet run --project backend/JGMS
```

---

### Error: Failed to Connect to Jira

```json
{
  "message": "Failed to connect to Jira. Please check your credentials and URL."
}
```

**Possible Causes:**
- ❌ Incorrect Jira URL (must be `https://yourteam.atlassian.net`, no trailing slash)
- ❌ Incorrect email address
- ❌ Invalid or expired API token
- ❌ Network connectivity issues

**Fix:** Verify your Jira URL, email, and API token. Regenerate the token if needed (see [Section 7](#7-jira-api-token-setup)).

---

### Error: Failed to Get Jira Project

```json
{
  "message": "Failed to get Jira project: NotFound - ..."
}
```

**Possible Causes:**
- ❌ Project key doesn't exist in Jira
- ❌ The API token user doesn't have access to the project

**Fix:** Verify the project key in Jira (e.g., `SWP391`). Project keys are uppercase and visible in Jira under **Project Settings > Details**.

---

### Error: Integration Already Exists

```json
{
  "message": "Jira integration already exists for this project. Use update endpoint instead."
}
```

**Fix:** Use `PUT /api/jira/projects/{projectId}/integration` instead of `POST` to update the existing configuration.

---

### Error: Unauthorized Access

```json
{
  "message": "Only administrators can configure Jira integration"
}
```

**Fix:** Ensure you are logged in with the correct role. Configuration endpoints require `admin` role.

---

### Error: Priority Parse Failure During Sync

If issues fail during sync with messages like `Failed to sync issue SWP391-X: ...`, it may be due to a custom Jira priority name that doesn't map to the `JiraPriority` enum.

**Fix:** Check the Jira issue priorities and ensure they match the values in the `JiraPriority` enum (`highest`, `high`, `medium`, `low`, `lowest`).

---

## 7. Jira API Token Setup

### Generate a Jira API Token

1. Go to [https://id.atlassian.com/manage-profile/security/api-tokens](https://id.atlassian.com/manage-profile/security/api-tokens)
2. Click **"Create API token"**
3. Enter a label (e.g., `JGMS Integration`)
4. Click **"Create"**
5. **Copy the token immediately** — it won't be shown again

### Required Jira Permissions

The account associated with the API token needs:

- **Browse Projects** — to view project details and issues
- **Create Issues** — if creating issues from JGMS
- **Edit Issues** — if updating issues from JGMS
- **Transition Issues** — if changing issue statuses from JGMS

> 💡 **Tip:** Using a Jira project admin account ensures all permissions are available.

### Jira URL Format

| ✅ Correct                              | ❌ Incorrect                                          |
|-----------------------------------------|------------------------------------------------------|
| `https://yourteam.atlassian.net`        | `https://yourteam.atlassian.net/`  (trailing slash)  |
| `https://yourteam.atlassian.net`        | `http://yourteam.atlassian.net`  (must be HTTPS)     |
| `https://yourteam.atlassian.net`        | `yourteam.atlassian.net`  (missing protocol)         |

---

## 8. Sample Test Data

### Minimal Configuration Payload

```json
{
  "jiraUrl": "https://yourteam.atlassian.net",
  "jiraEmail": "your-email@fpt.edu.vn",
  "apiToken": "ATATT3xFfGN0xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "projectKey": "SWP391"
}
```

### Validation Rules

| Field         | Validation                                              |
|---------------|---------------------------------------------------------|
| `jiraUrl`     | Required, must be a valid URL                           |
| `jiraEmail`   | Required, must be a valid email address                 |
| `apiToken`    | Required, minimum 10 characters                         |
| `projectKey`  | Required, uppercase letters/numbers/underscores (`^[A-Z][A-Z0-9_]*$`) |

### Test Scenarios Checklist

- [ ] **Happy Path:** Configure → Test → Sync → View Issues → View Single Issue
- [ ] **Invalid Credentials:** Configure with wrong token/email/URL
- [ ] **Non-existent Project:** Configure with invalid project key
- [ ] **Duplicate Integration:** Configure same project twice (should fail)
- [ ] **Update Integration:** Update credentials via PUT
- [ ] **Delete Integration:** Remove integration and verify issues are no longer accessible
- [ ] **Admin Access:** All admin-only endpoints with admin token
- [ ] **Team Leader Sync:** Sync endpoint with team leader token
- [ ] **Student Filtered View:** Student sees only their assigned issues
- [ ] **Lecturer Read-Only:** Lecturer can view but cannot configure or sync
- [ ] **Unauthorized Access:** Each endpoint with wrong role returns 401
- [ ] **No JWT Token:** All endpoints without Authorization header return 401
- [ ] **Expired Token:** Request with expired JWT returns 401
- [ ] **Re-sync After Changes:** Modify issues in Jira, re-sync, verify updates

---

> **Note:** The Jira integration uses Basic Authentication (email + API token) to communicate with Atlassian's REST API v3. API tokens are encrypted at rest using ASP.NET Core Data Protection before being stored in the database.

