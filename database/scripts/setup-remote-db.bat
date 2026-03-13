@echo off
REM ============================================================================
REM Setup Remote Database — Run init.sql on your Render PostgreSQL
REM ============================================================================
REM
REM USAGE:
REM   setup-remote-db.bat "YOUR_EXTERNAL_DATABASE_URL"
REM
REM EXAMPLE:
REM   setup-remote-db.bat "postgres://admin:aB3xY9@dpg-abc123.singapore-postgres.render.com/jgms"
REM
REM PREREQUISITES:
REM   - PostgreSQL must be installed (for psql command)
REM   - Download from: https://www.postgresql.org/download/windows/
REM   - Or install via: winget install PostgreSQL.PostgreSQL
REM ============================================================================

if "%~1"=="" (
    echo.
    echo ERROR: Please provide your Render External Database URL
    echo.
    echo USAGE:
    echo   setup-remote-db.bat "postgres://USER:PASS@HOST/DBNAME"
    echo.
    echo HOW TO FIND IT:
    echo   1. Go to render.com dashboard
    echo   2. Click your database ^(jgms-db^)
    echo   3. Scroll to "Connections" section
    echo   4. Copy the "External Database URL"
    echo.
    exit /b 1
)

echo.
echo ============================================
echo  Setting up JGMS database...
echo ============================================
echo.

REM Navigate to the project root (two levels up from this script)
pushd "%~dp0\..\"

echo Running init.sql on remote database...
echo.

psql "%~1" -f init.sql

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ============================================
    echo  SUCCESS! Database is ready.
    echo ============================================
    echo.
    echo  Tables, enums, triggers, and sample data
    echo  have been created.
    echo.
    echo  Test accounts (all password: Password123):
    echo    - admin@swp391.edu.vn     ^(Admin^)
    echo    - nguyenvana@fpt.edu.vn   ^(Lecturer^)
    echo    - levand@student.fpt.edu.vn ^(Student^)
    echo.
) else (
    echo.
    echo ============================================
    echo  FAILED! Check the error messages above.
    echo ============================================
    echo.
    echo  Common issues:
    echo    - psql not found: Install PostgreSQL
    echo    - Connection refused: Check the URL
    echo    - Permission denied: Check username/password
    echo.
)

popd
pause

