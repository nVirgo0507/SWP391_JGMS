# Deployment Guide — SWP391 JGMS API

---

## Option A: One-Click Deploy with Render Blueprint (Easiest)

This creates **both** the database and the API automatically.

1. Push your code to GitHub
2. Go to: **https://render.com/deploy** → paste your repo URL
3. Render reads the `render.yaml` file and creates everything for you:
   - ✅ PostgreSQL database (`jgms-db`)
   - ✅ Docker web service (`swp391-jgms-api`)
   - ✅ JWT key (auto-generated)
4. **The only thing you set manually** is the database connection string (see Step 5 below)
5. After both services are created, go to your **database dashboard** and copy the **Internal Database URL**, then:
   - Go to your **web service** → **Environment** → find `ConnectionStrings__DefaultConnection`
   - Convert the URL and paste it (see [How to Convert the Database URL](#-how-to-convert-the-database-url) below)
6. **Initialize the database** (see [How to Set Up the Database](#how-to-set-up-the-database) below)
7. Done! Your API is at: `https://swp391-jgms-api.onrender.com/swagger`

---

## Option B: Manual Step-by-Step Deploy to Render

### Step 1: Create the Database

1. Go to [render.com](https://render.com) → Sign up (use GitHub login)
2. Dashboard → **New** → **PostgreSQL**
3. Fill in:
   - **Name:** `jgms-db`
   - **Region:** Singapore
   - **Plan:** Free
4. Click **Create Database** → wait ~30 seconds

### Step 2: Set Up the Database

See [How to Set Up the Database](#how-to-set-up-the-database) below.

### Step 3: Deploy the API

1. Dashboard → **New** → **Web Service**
2. Connect your **GitHub repository**
3. Fill in:
   - **Name:** `swp391-jgms-api`
   - **Region:** Singapore
   - **Runtime:** Docker
   - **Plan:** Free

4. Add these **Environment Variables** (click "Add Environment Variable" for each row):

   | Key | What to put |
   |-----|-------------|
   | `ConnectionStrings__DefaultConnection` | Your converted database URL — see [below](#-how-to-convert-the-database-url) |
   | `Jwt__Key` | Any random string 32+ characters — see [below](#-how-to-create-jwt-key) |
   | `Jwt__Issuer` | `SWP391_JGMS` ← copy exactly |
   | `Jwt__Audience` | `SWP391_JGMS_USERS` ← copy exactly |
   | `Jwt__ExpireMinutes` | `120` ← copy exactly |

5. Click **Deploy Web Service**
6. API is at: `https://swp391-jgms-api.onrender.com/swagger`

---

## How to Set Up the Database

You only need to do this **once** — it creates all tables, enums, triggers, and sample users.

Pick **one** of these methods:

### Database Option 1: Run the Helper Script (Windows — Easiest)

If you have PostgreSQL installed on your PC:

1. Open Render dashboard → click your database → copy the **External Database URL**
2. Open a terminal in your project folder and run:
   ```cmd
   cd database\scripts
   setup-remote-db.bat "postgres://admin:YOUR_PASS@dpg-YOUR_HOST.singapore-postgres.render.com/jgms"
   ```
3. ✅ Done! It runs `init.sql` automatically.

> 💡 Don't have PostgreSQL? Install it: `winget install PostgreSQL.PostgreSQL` or download from [postgresql.org](https://www.postgresql.org/download/windows/)

### Database Option 2: Use DBeaver (GUI — No Command Line)

1. Download [DBeaver](https://dbeaver.io/download/) (free)
2. In Render dashboard, click your database → **Info** tab → find the **External Database URL**:
   ```
   postgres://admin:aB3xY9kLmN7pQ2@dpg-abc123.singapore-postgres.render.com/jgms
   ```
3. In DBeaver: **Database** → **New Database Connection** → **PostgreSQL**
4. Fill in the fields from your URL:
   - **Host:** `dpg-abc123.singapore-postgres.render.com`
   - **Port:** `5432`
   - **Database:** `jgms`
   - **Username:** `admin`
   - **Password:** `aB3xY9kLmN7pQ2`
5. Click **Test Connection** → should say "Connected"
6. Click **Finish**
7. Open an **SQL Editor** (right-click database → SQL Editor → New SQL Script)
8. Open the file `database/init.sql` from your project → **Select All** → **Copy** → **Paste** into DBeaver
9. Click the ▶️ **Execute** button (or press `Ctrl+Enter`)
10. ✅ Done! All tables and sample data are created.

### Database Option 3: Use `psql` from Command Line

If you have PostgreSQL installed locally:
```bash
# Replace with YOUR External Database URL from Render
psql "postgres://admin:aB3xY9kLmN7pQ2@dpg-abc123.singapore-postgres.render.com/jgms" -f database/init.sql
```

### Database Option 4: Use Render Shell (no install needed, but slow)

1. In Render dashboard, click your database → **Shell** tab
2. You'll get a `psql` prompt. Paste the entire contents of `database/init.sql` and press Enter.

> ⚠️ The Render shell can be slow with large pastes (init.sql is 500+ lines). If it hangs, use one of the other options instead.

### What does init.sql create?

| What | Details |
|------|---------|
| **Tables** | 15+ tables — users, groups, projects, tasks, requirements, SRS docs, commits, etc. |
| **Enums** | PostgreSQL enums — user_role, task_status, priority_level, etc. |
| **Triggers** | Auto-update `updated_at` timestamps on every row change |
| **Indexes** | All necessary indexes for performance |
| **Sample data** | 1 admin + 3 lecturers + 15 students (all with password `Password123`) |

> 💡 **You only need to run init.sql once.** After that, the app manages the data through the API.

---

## 🔑 How to Convert the Database URL

Render gives you a URL like this:
```
postgres://admin:aB3xY9kLmN7pQ2@dpg-abc123def456.singapore-postgres.render.com/jgms
         ─────  ───────────────  ──────────────────────────────────────────────  ────
         user   password         host                                           database
```

Convert it to this format for the environment variable:
```
Host=dpg-abc123def456.singapore-postgres.render.com;Port=5432;Database=jgms;Username=admin;Password=aB3xY9kLmN7pQ2
```

**Just rearrange the parts — don't change any values:**

| From the Render URL | Put it here |
|---------------------|-------------|
| `admin` (between `://` and `:`) | `Username=admin` |
| `aB3xY9kLmN7pQ2` (between `:` and `@`) | `Password=aB3xY9kLmN7pQ2` |
| `dpg-abc123...render.com` (between `@` and `/`) | `Host=dpg-abc123...render.com` |
| `jgms` (after the last `/`) | `Database=jgms` |
| *(always 5432)* | `Port=5432` |

> 📋 Use **Internal Database URL** if the API and DB are both on Render (faster).
> Use **External Database URL** if connecting from your local machine or DBeaver.

---

## 🔑 How to Create JWT Key

This is just a **secret password** for signing login tokens. Make up any random string 32+ characters.

**Examples (pick one or make your own):**
```
MySwp391ProjectSuperSecretKey2026!@#
```
```
jgms-api-jwt-secret-key-swp391-2026
```

Or go to [randomkeygen.com](https://randomkeygen.com/) and copy any long key.

> 🚫 Do **NOT** literally type `YOUR_STRONG_SECRET_KEY_AT_LEAST_32_CHARS` — that's a placeholder!

---

## How to Redeploy After Changes

Just **push to GitHub**. Render auto-deploys on every push to your default branch.

```bash
git add .
git commit -m "fix: update something"
git push origin main
```

Render rebuilds automatically (~2-3 minutes).

---

## Important Notes

### Free Tier Limitations
- **Spin-down:** The API sleeps after 15 minutes of inactivity. First request after sleep takes ~30 seconds. Fine for dev/testing.
- **Database:** Free Postgres expires after 90 days. You can recreate it or upgrade ($7/month).
- **750 hours/month free** — more than enough for a university project.

### For Production (if needed later)
- Upgrade to Starter plan ($7/month) — no spin-down
- Use a proper JWT secret key (not the dev one)
- Set `AllowedOrigins` in appsettings to your actual frontend URL

---

## Alternative: Deploy with Docker on a VPS

If you have a VPS (DigitalOcean, Oracle Cloud free tier, etc.):

```bash
# On the server
git clone <your-repo-url>
cd SWP
docker-compose up --build -d
```

This starts **both** the database and API automatically. No manual database setup needed — `docker-compose.yml` runs `init.sql` on first start.

Your API runs at `http://server-ip:8080/swagger`

---

## Local Development (unchanged)

For local development, everything works the same:
```bash
# Start just the database (runs init.sql automatically on first start)
docker-compose up db -d

# Run the API from IDE or CLI
cd backend/JGMS
dotnet run
```

API at: `http://localhost:5284/swagger`

> 💡 Docker Compose mounts `database/init.sql` and runs it when the database container starts for the first time. If you need to reset the database, run:
> ```bash
> docker-compose down -v   # -v deletes the database volume
> docker-compose up db -d  # recreates fresh database with init.sql
> ```

---

## Quick Reference — Test Accounts

After running `init.sql`, these accounts are available (all password: **`Password123`**):

| Email | Role | Notes |
|-------|------|-------|
| `admin@swp391.edu.vn` | Admin | Full system access |
| `nguyenvana@fpt.edu.vn` | Lecturer | Can view assigned groups |
| `tranthib@fpt.edu.vn` | Lecturer | |
| `phamvanc@fpt.edu.vn` | Lecturer | |
| `levand@student.fpt.edu.vn` | Student | Group 1 |
| `ngothie@student.fpt.edu.vn` | Student | Group 1 |
| *(+ 13 more students)* | Student | See `init.sql` for full list |

Login via: `POST /api/auth/login` with `{ "email": "...", "password": "Password123" }`

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| API returns 500 on first request | Database not initialized — run `init.sql` |
| "relation does not exist" errors | Enum types missing — run `init.sql` (not just EnsureCreated) |
| API takes 30s to respond | Normal on free tier — Render is waking up the service |
| Can't connect to database from DBeaver | Use **External** Database URL (not Internal) |
| `psql` command not found | Install PostgreSQL: `winget install PostgreSQL.PostgreSQL` |
| Database expired after 90 days | Recreate on Render (free) and re-run `init.sql` |

