                                                                                                                                                                    @echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
REM ============================================
REM Import data from dump file to stockdb2
REM Will not affect stockdb database
REM ============================================
echo Starting import script...
echo Current directory: %CD%
echo.

REM Database connection configuration (target database)
set TARGET_HOST=localhost
set TARGET_PORT=8532
set TARGET_DB=stockdb2
set TARGET_USER=postgres
set TARGET_PASSWORD=cd123321

REM Find tool paths
echo Checking for required tools...
set PSQL_PATH=
set PGRESTORE_PATH=
for %%p in (
   "F:\dsfr\mqq\tools\bin\psql.exe"
) do (
    if exist %%p (
        set PSQL_PATH=%%p
        echo Found psql: %%p
        goto :found_psql
    )
)
where psql >nul 2>&1
if !errorlevel!==0 (
    set PSQL_PATH=psql
    echo Found psql in PATH
)

:found_psql
if "!PSQL_PATH!"=="" (
    echo [ERROR] psql.exe not found
    exit /b 1
)

for %%p in (
   "F:\dsfr\mqq\tools\bin\pg_restore.exe"
   "C:\Program Files\PostgreSQL\16\bin\pg_restore.exe"
   "C:\Program Files\PostgreSQL\15\bin\pg_restore.exe"
) do (
    if exist %%p (
        set PGRESTORE_PATH=%%p
        echo Found pg_restore: %%p
        goto :found_pgrestore
    )
)
where pg_restore >nul 2>&1
if !errorlevel!==0 (
    set PGRESTORE_PATH=pg_restore
    echo Found pg_restore in PATH
)

:found_pgrestore
if "!PGRESTORE_PATH!"=="" (
    echo [ERROR] pg_restore.exe not found
    exit /b 1
)
echo Tools check completed.
echo.

REM Get dump file path (from command line argument or use default)
echo Looking for dump file...
set DUMP_FILE=%1
if "%DUMP_FILE%"=="" (
    REM If no argument provided, find the latest dump file (prefer files without Chinese characters in name)
    set DUMP_FILE=
    REM First, find files without Chinese characters in name (format: stockdb_full_YYYYMMDD_HHMMSS.dump)
    for /f "delims=" %%f in ('dir /b /o-d "%CD%\backups\stockdb_full_*_*.dump" 2^>nul') do (
        set "DUMP_FILE=!CD!\backups\%%f"
        echo Found dump file: !DUMP_FILE!
        goto :found_dump
    )
    REM If not found, search for all dump files
    for /f "delims=" %%f in ('dir /b /o-d "%CD%\backups\*.dump" 2^>nul') do (
        set "DUMP_FILE=!CD!\backups\%%f"
        echo Found dump file: !DUMP_FILE!
        goto :found_dump
    )
    :found_dump
    if "!DUMP_FILE!"=="" (
        echo [ERROR] No dump file found in backups directory
        echo Please specify dump file path or place dump file in backups folder
        exit /b 1
    )
) else (
    echo Using specified dump file: !DUMP_FILE!
)

REM File already found by dir command, skip additional check
REM If file path is empty, report error
if "!DUMP_FILE!"=="" (
    echo [ERROR] Dump file path is empty
    exit /b 1
)
echo Dump file ready: !DUMP_FILE!
echo.

echo ============================================
echo Importing data from dump file to stockdb2
echo ============================================
echo Target database: !TARGET_DB!@!TARGET_HOST!:!TARGET_PORT!
echo Dump file: !DUMP_FILE!
echo.
echo [Important Notes]
echo   - Data will be imported to: stockdb2 database
echo   - Will not affect stockdb database
echo   - Existing data in stockdb2 may be overwritten
echo   - Table structure will be created before import
echo ============================================
echo.
set /p CONFIRM="Confirm import to stockdb2 database? (Y/N, default N): "
if /i not "!CONFIRM!"=="Y" (
    echo Operation cancelled
    exit /b 0
)
echo.

REM Set password environment variable
set PGPASSWORD=!TARGET_PASSWORD!

REM Check if target database exists
echo Step 1 of 4: Checking if target database exists...
"!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d postgres -c "SELECT 1 FROM pg_database WHERE datname='!TARGET_DB!';" | find "1" >nul
if !errorlevel! neq 0 (
    echo Database !TARGET_DB! does not exist, creating...
    "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d postgres -c "CREATE DATABASE !TARGET_DB! ENCODING 'UTF8';"
    if !errorlevel! neq 0 (
        echo [ERROR] Failed to create database !TARGET_DB!
        goto :end
    )
    echo Database created successfully!
) else (
    echo Database !TARGET_DB! already exists
)
echo.

REM Create table structure
echo Step 2/4: Creating table structure...
REM Prefer SQL file without Chinese comments
if exist create_all_tables_no_comments.sql (
    set SQL_FILE=create_all_tables_no_comments.sql
    echo Using create_all_tables_no_comments.sql
    goto :sql_file_found
)
if exist create_all_tables.sql (
    set SQL_FILE=create_all_tables.sql
    echo Using create_all_tables.sql
    goto :sql_file_found
)
echo ERROR: SQL file not found
echo Please ensure create_all_tables.sql or create_all_tables_no_comments.sql exists
goto :end

:sql_file_found
echo Creating tables...
REM Set client encoding to UTF8
set PGCLIENTENCODING=UTF8
REM Execute SQL file to create tables
"!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -f "!SQL_FILE!" >nul 2>&1
set CREATE_RESULT=!errorlevel!
set PGCLIENTENCODING=

if !CREATE_RESULT! neq 0 (
    echo [WARNING] Some errors may have occurred, checking if tables were created...
) else (
    echo SQL file executed successfully!
)

REM Verify if table was created successfully
echo Verifying table structure...
"!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -t -A -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name='stock_daily_data';" >temp_table_check.txt 2>&1
find /i "1" temp_table_check.txt >nul
set TABLE_EXISTS=!errorlevel!
del temp_table_check.txt >nul 2>&1
if !TABLE_EXISTS! neq 0 (
    echo [ERROR] Table stock_daily_data creation failed
    echo Please check the SQL file encoding
    goto :end
)
echo Table structure verified successfully!
echo.

REM Ask if existing data should be cleared
echo Step 3 of 4: Preparing to import data...
set /p CLEAR_DATA="Clear existing data in stockdb2? This will truncate all tables. (Y/N, default N): "
if /i "!CLEAR_DATA!"=="Y" (
    echo Clearing existing data from all tables...
    set PGPASSWORD=!TARGET_PASSWORD!
    echo Step 1: Truncating all tables...
    "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -c "TRUNCATE TABLE public.stock_daily_data, public.stock_exrights_data, public.stock_info, public.stock_realtime_data, public.data_receive_log, public.adjustment_task CASCADE;"
    if !errorlevel! neq 0 (
        echo [WARNING] Batch truncate failed, trying individual truncate...
        "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -c "TRUNCATE TABLE public.stock_daily_data CASCADE;"
        "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -c "TRUNCATE TABLE public.stock_exrights_data CASCADE;"
        "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -c "TRUNCATE TABLE public.stock_info CASCADE;"
        "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -c "TRUNCATE TABLE public.stock_realtime_data CASCADE;"
        "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -c "TRUNCATE TABLE public.data_receive_log CASCADE;"
        "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -c "TRUNCATE TABLE public.adjustment_task CASCADE;"
    )
    echo Step 2: Resetting all sequences to start from 1...
    "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -c "ALTER SEQUENCE IF EXISTS stock_daily_data_id_seq RESTART WITH 1; ALTER SEQUENCE IF EXISTS stock_exrights_data_id_seq RESTART WITH 1; ALTER SEQUENCE IF EXISTS stock_realtime_data_id_seq RESTART WITH 1; ALTER SEQUENCE IF EXISTS data_receive_log_id_seq RESTART WITH 1; ALTER SEQUENCE IF EXISTS adjustment_task_id_seq RESTART WITH 1;"
    echo Step 3: Verifying data is cleared...
    "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -t -A -c "SELECT 'stock_daily_data: ' || COUNT(*) FROM stock_daily_data UNION ALL SELECT 'stock_info: ' || COUNT(*) FROM stock_info UNION ALL SELECT 'stock_exrights_data: ' || COUNT(*) FROM stock_exrights_data;"
    echo All tables cleared and sequences reset successfully
    echo.
)

REM Check and fix table structure mismatch (add missing columns if needed)
echo Checking table structure compatibility...
set PGPASSWORD=!TARGET_PASSWORD!
echo Checking if stock_realtime_data has close_price column...
"!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -t -A -c "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='stock_realtime_data' AND column_name='close_price';" >column_check.txt 2>&1
findstr /i "1" column_check.txt >nul
if !errorlevel! neq 0 (
    echo [INFO] close_price column not found, adding it to match dump file structure...
    "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -c "ALTER TABLE public.stock_realtime_data ADD COLUMN IF NOT EXISTS close_price NUMERIC(10, 3);"
    if !errorlevel! equ 0 (
        echo close_price column added successfully
    ) else (
        echo [WARNING] Failed to add close_price column, will continue anyway
    )
) else (
    echo close_price column already exists
)
del column_check.txt >nul 2>&1
echo.

REM Drop all indexes before import to speed up data import
echo Dropping all indexes before import...
if exist "drop_all_indexes.sql" (
    set PGPASSWORD=!TARGET_PASSWORD!
    "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -f drop_all_indexes.sql 2>&1
    if !errorlevel! equ 0 (
        echo All indexes dropped successfully
    ) else (
        echo [WARNING] Some errors occurred while dropping indexes, will continue import
    )
) else (
    echo [INFO] drop_all_indexes.sql not found, skipping index drop step
)
echo.

REM Import data
echo Step 4 of 4: Importing data...
echo.

REM First try to restore data only (without table structure, to avoid sequence, constraint, index conflicts)
echo Restoring data (data only, no table structure)...
echo Checking if table exists...
set PGPASSWORD=!TARGET_PASSWORD!
"!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -t -A -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name='stock_daily_data';" >table_check.txt 2>&1
findstr /i "1" table_check.txt >nul
if !errorlevel! neq 0 (
    echo ERROR: Table stock_daily_data does not exist!
    echo Please create table structure first
    del table_check.txt >nul 2>&1
    set RESTORE_RESULT=1
    goto :cleanup_log
)
del table_check.txt >nul 2>&1
echo Table exists, starting data restore...
echo This may take several minutes depending on data size...
echo Please wait...
echo.
echo Note: Restoring all data (will filter to stock_daily_data if needed)
echo.
set PGPASSWORD=!TARGET_PASSWORD!
"!PGRESTORE_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! --no-owner --no-privileges --data-only --verbose --no-tablespaces "!DUMP_FILE!" >restore_output.log 2>&1
set RESTORE_RESULT=!errorlevel!
echo.
echo Restore command finished with exit code: !RESTORE_RESULT!
if !RESTORE_RESULT! equ 0 goto restore_success
echo Restore failed with error code: !RESTORE_RESULT!
if exist restore_output.log (
    echo Error details:
    type restore_output.log
)
goto try_full_restore

:restore_success
echo Restore command completed
if exist restore_output.log (
    echo Checking restore log...
    findstr /i "stock_daily_data" restore_output.log >nul
    if !errorlevel! equ 0 (
        echo OK: Data processing found for stock_daily_data table
    ) else (
        echo WARNING: No data processing found for stock_daily_data table in log
    )
    findstr /i "error" restore_output.log >nul
    if !errorlevel! equ 0 (
        echo WARNING: Errors found in restore log
        findstr /i "error" restore_output.log
    )
    findstr /i "failed" restore_output.log >nul
    if !errorlevel! equ 0 (
        echo WARNING: Failures found in restore log
        findstr /i "failed" restore_output.log
    )
    echo.
    echo Note: Full restore log saved in restore_output.log for review
)
goto cleanup_log

:try_full_restore
if exist restore_output.log del restore_output.log
REM If table restore failed, try to restore entire database data
echo Table restore failed, trying to restore entire database data...
set PGPASSWORD=!TARGET_PASSWORD!
"!PGRESTORE_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! --no-owner --no-privileges --data-only "!DUMP_FILE!" >restore_output.log 2>&1
set RESTORE_RESULT=!errorlevel!
if !RESTORE_RESULT! equ 0 goto restore_success
echo Restore failed with error code: !RESTORE_RESULT!
if exist restore_output.log (
    echo Error details:
    type restore_output.log
)
REM If data-only restore failed, try full restore
        echo Data-only restore failed, trying full restore (will delete existing objects)...
        echo WARNING: This operation will delete existing table structures, sequences, constraints and indexes!
        set PGPASSWORD=!TARGET_PASSWORD!
        "!PGRESTORE_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! --clean --if-exists --no-owner --no-privileges --no-tablespaces "!DUMP_FILE!" >restore_output.log 2>&1
set RESTORE_RESULT=!errorlevel!
if !RESTORE_RESULT! equ 0 goto restore_success
echo Restore failed with error code: !RESTORE_RESULT!
if exist restore_output.log (
    echo Error details:
    type restore_output.log
)

:cleanup_log
REM Keep log file for debugging - will be checked later
set PGPASSWORD=

if !RESTORE_RESULT! equ 0 (
    echo.
    echo ============================================
    echo Import successful!
    echo ============================================
    
    REM Recreate all indexes after successful import
    echo.
    echo Recreating all indexes...
    if exist "recreate_all_indexes.sql" (
        set PGPASSWORD=!TARGET_PASSWORD!
        "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -f recreate_all_indexes.sql 2>&1
        if !errorlevel! equ 0 (
            echo All indexes recreated successfully
        ) else (
            echo [WARNING] Some errors occurred while recreating indexes
        )
    ) else (
        echo [INFO] recreate_all_indexes.sql not found, skipping index recreation
    )
    
    REM Recreate all constraints (unique constraints, foreign keys) after successful import
    echo.
    echo Recreating all constraints...
    if exist "recreate_all_constraints.sql" (
        set PGPASSWORD=!TARGET_PASSWORD!
        "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -f recreate_all_constraints.sql 2>&1
        if !errorlevel! equ 0 (
            echo All constraints recreated successfully
        ) else (
            echo [WARNING] Some errors occurred while recreating constraints
        )
    ) else (
        echo [INFO] recreate_all_constraints.sql not found, skipping constraint recreation
    )
    
    REM Display data statistics after restore
    echo.
    echo Data statistics:
    set PGPASSWORD=!TARGET_PASSWORD!
    "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -c "SELECT COUNT(*) as total_records FROM public.stock_daily_data;"
    
    REM Check if data count is 0
    echo.
    set RECORD_COUNT=
    set PGPASSWORD=!TARGET_PASSWORD!
    "!PSQL_PATH!" -h !TARGET_HOST! -p !TARGET_PORT! -U !TARGET_USER! -d !TARGET_DB! -t -A -c "SELECT COUNT(*) FROM public.stock_daily_data;" >temp_count.txt 2>&1
    for /f "tokens=1" %%a in (temp_count.txt) do (
        set RECORD_COUNT=%%a
        goto :check_count
    )
    :check_count
    if exist temp_count.txt del temp_count.txt
    if "!RECORD_COUNT!"=="0" (
        echo.
        echo [WARNING] Imported data count is 0
        echo.
        echo Possible reasons:
        echo   1. Dump file is schema-only backup (no data)
        echo   2. Data in dump file was cleared
        echo   3. Table name or schema mismatch
        echo   4. Data restore failed silently
        echo.
        echo Checking dump file contents:
        set PGPASSWORD=!TARGET_PASSWORD!
        "!PGRESTORE_PATH!" -l "!DUMP_FILE!" 2>nul | findstr /i "stock_daily_data"
        echo.
        if exist restore_output.log (
            echo Full restore log for debugging:
            type restore_output.log
        )
    )
) else (
    echo.
    echo ============================================
    echo Import failed!
    echo ============================================
    echo Please check:
    echo   1. Is dump file complete?
    echo   2. Is database connection normal?
    echo   3. Does table structure match?
    echo   4. Check error messages above
    echo.
)

:end
REM Clear password environment variable
set PGPASSWORD=

REM Clean up temporary files
if exist restore_output.log del restore_output.log
if exist temp_count.txt del temp_count.txt
if exist table_check.txt del table_check.txt

echo.
echo ============================================
echo Operation completed!
echo Note: stockdb database was not modified
echo ============================================
endlocal
exit /b 0
