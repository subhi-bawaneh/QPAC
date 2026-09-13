-- TIDP Data Reset Script
-- Safely deletes all TIDP-related data for a specific project WITHOUT touching other data
-- Respects foreign key constraints and includes before/after verification
--
-- USAGE:
--   sqlcmd -S server_name -U user -P password -d database_name -i reset-tidp-data.sql -v ProjectId='<guid>'
--   Or: sqlcmd ... -v ProjectId='00000000-0000-0000-0000-000000000000' -v CreateBackup=1
--
-- Before running:
--   1. Verify the ProjectId is correct (QPAC: 00000000-0000-0000-0000-000000000000)
--   2. Run this script with CreateBackup=1 to back up first
--   3. Review the "BEFORE:" counts
--   4. After reviewing, run again with CreateBackup=0 to execute the reset

-- =====================================================================
-- Configuration
-- =====================================================================

DECLARE @ProjectId UNIQUEIDENTIFIER = CONVERT(UNIQUEIDENTIFIER, '$(ProjectId)')
DECLARE @CreateBackup BIT = ISNULL($(CreateBackup), 1)
DECLARE @VerifyOnly BIT = ISNULL($(VerifyOnly), 0)

-- Validate ProjectId
IF @ProjectId IS NULL OR @ProjectId = '00000000-0000-0000-0000-000000000000'
BEGIN
    PRINT 'ERROR: ProjectId is required and cannot be null or empty GUID'
    PRINT 'Usage: -v ProjectId=''<real-guid-here>'''
    RETURN
END

PRINT '=========================================='
PRINT 'TIDP Data Reset Script'
PRINT '=========================================='
PRINT 'ProjectId: ' + CONVERT(VARCHAR(36), @ProjectId)
PRINT 'CreateBackup: ' + CONVERT(VARCHAR(1), @CreateBackup)
PRINT ''

-- =====================================================================
-- Backup (if requested)
-- =====================================================================

IF @CreateBackup = 1
BEGIN
    DECLARE @BackupPath NVARCHAR(MAX) = 'C:\Backups\DipDb_TIDP_Reset_' + CONVERT(VARCHAR, GETDATE(), 112) + '_' + CONVERT(VARCHAR, GETDATE(), 108) + '.bak'
    SET @BackupPath = REPLACE(@BackupPath, ':', '')
    SET @BackupPath = 'C:\Backups\DipDb_TIDP_Reset_' + FORMAT(GETDATE(), 'yyyyMMdd_HHmmss') + '.bak'

    PRINT 'Creating backup...'
    BACKUP DATABASE [DIP] TO DISK = @BackupPath WITH DESCRIPTION = 'TIDP Data Reset Backup'
    PRINT 'Backup created at: ' + @BackupPath
    PRINT ''
END

-- =====================================================================
-- Count rows BEFORE reset
-- =====================================================================

PRINT '=========================================='
PRINT 'ROW COUNTS BEFORE RESET:'
PRINT '=========================================='

DECLARE @CountBefore_TidpFiles INT
DECLARE @CountBefore_Documents INT
DECLARE @CountBefore_TidpFolderOwners INT
DECLARE @CountBefore_TidpFolderDisciplines INT
DECLARE @CountBefore_TidpFolderSyncs INT
DECLARE @CountBefore_ImportBatches_TIDP INT
DECLARE @CountBefore_Projects INT
DECLARE @CountBefore_Users INT

SELECT @CountBefore_TidpFiles = COUNT(*) FROM TidpFiles WHERE ProjectId = @ProjectId
SELECT @CountBefore_Documents = COUNT(*) FROM Documents WHERE ProjectId = @ProjectId
SELECT @CountBefore_TidpFolderOwners = COUNT(*) FROM TidpFolderOwners WHERE ProjectId = @ProjectId
SELECT @CountBefore_TidpFolderDisciplines = COUNT(*) FROM TidpFolderDisciplines WHERE ProjectId = @ProjectId
SELECT @CountBefore_TidpFolderSyncs = COUNT(*) FROM TidpFolderSyncs WHERE ProjectId = @ProjectId
SELECT @CountBefore_ImportBatches_TIDP = COUNT(*) FROM ImportBatches WHERE ProjectId = @ProjectId AND TidpFileId IS NOT NULL
SELECT @CountBefore_Projects = COUNT(*) FROM Projects
SELECT @CountBefore_Users = COUNT(*) FROM Users

PRINT 'TidpFiles: ' + CONVERT(VARCHAR, @CountBefore_TidpFiles)
PRINT 'Documents (TIDP rows): ' + CONVERT(VARCHAR, @CountBefore_Documents)
PRINT 'TidpFolderOwners: ' + CONVERT(VARCHAR, @CountBefore_TidpFolderOwners)
PRINT 'TidpFolderDisciplines: ' + CONVERT(VARCHAR, @CountBefore_TidpFolderDisciplines)
PRINT 'TidpFolderSyncs: ' + CONVERT(VARCHAR, @CountBefore_TidpFolderSyncs)
PRINT 'ImportBatches (TIDP): ' + CONVERT(VARCHAR, @CountBefore_ImportBatches_TIDP)
PRINT 'Projects (total, should not change): ' + CONVERT(VARCHAR, @CountBefore_Projects)
PRINT 'Users (total, should not change): ' + CONVERT(VARCHAR, @CountBefore_Users)
PRINT ''

IF @VerifyOnly = 1
BEGIN
    PRINT 'Verification only mode - no data deleted'
    RETURN
END

IF @CreateBackup = 1
BEGIN
    PRINT '=========================================='
    PRINT 'IMPORTANT: Review the counts above'
    PRINT '=========================================='
    PRINT 'To execute the reset, run this script again with: -v CreateBackup=0 -v VerifyOnly=0'
    PRINT 'This prevents accidental deletion.'
    PRINT ''
    RETURN
END

-- =====================================================================
-- Execute reset (FK-safe order)
-- =====================================================================

BEGIN TRANSACTION

PRINT '=========================================='
PRINT 'DELETING TIDP DATA...'
PRINT '=========================================='

-- 1. Delete sync operations
DELETE FROM TidpFolderSyncs WHERE ProjectId = @ProjectId
PRINT 'Deleted TidpFolderSyncs'

-- 2. Delete import batches for TIDP files (must be before deleting TidpFiles)
DELETE FROM ImportBatches WHERE ProjectId = @ProjectId AND TidpFileId IS NOT NULL
PRINT 'Deleted ImportBatches (TIDP source)'

-- 3. Delete document rows from TIDP files (must be before deleting TidpFiles)
DELETE FROM Documents WHERE ProjectId = @ProjectId AND TidpFileId IS NOT NULL
PRINT 'Deleted Documents (TIDP rows)'

-- 4. Delete TIDP files
DELETE FROM TidpFiles WHERE ProjectId = @ProjectId
PRINT 'Deleted TidpFiles'

-- 5. Delete discipline folders
DELETE FROM TidpFolderDisciplines WHERE ProjectId = @ProjectId
PRINT 'Deleted TidpFolderDisciplines'

-- 6. Delete owner folders
DELETE FROM TidpFolderOwners WHERE ProjectId = @ProjectId
PRINT 'Deleted TidpFolderOwners'

-- 7. Delete related audit logs (optional, keeps history minimal)
-- DELETE FROM AuditLogs WHERE ProjectId = @ProjectId AND EntityName LIKE 'Tidp%'
-- PRINT 'Deleted AuditLogs (TIDP operations)'

COMMIT TRANSACTION

PRINT ''
PRINT '=========================================='
PRINT 'ROW COUNTS AFTER RESET:'
PRINT '=========================================='

DECLARE @CountAfter_TidpFiles INT
DECLARE @CountAfter_Documents INT
DECLARE @CountAfter_TidpFolderOwners INT
DECLARE @CountAfter_TidpFolderDisciplines INT
DECLARE @CountAfter_TidpFolderSyncs INT
DECLARE @CountAfter_ImportBatches_TIDP INT
DECLARE @CountAfter_Projects INT
DECLARE @CountAfter_Users INT

SELECT @CountAfter_TidpFiles = COUNT(*) FROM TidpFiles WHERE ProjectId = @ProjectId
SELECT @CountAfter_Documents = COUNT(*) FROM Documents WHERE ProjectId = @ProjectId
SELECT @CountAfter_TidpFolderOwners = COUNT(*) FROM TidpFolderOwners WHERE ProjectId = @ProjectId
SELECT @CountAfter_TidpFolderDisciplines = COUNT(*) FROM TidpFolderDisciplines WHERE ProjectId = @ProjectId
SELECT @CountAfter_TidpFolderSyncs = COUNT(*) FROM TidpFolderSyncs WHERE ProjectId = @ProjectId
SELECT @CountAfter_ImportBatches_TIDP = COUNT(*) FROM ImportBatches WHERE ProjectId = @ProjectId AND TidpFileId IS NOT NULL
SELECT @CountAfter_Projects = COUNT(*) FROM Projects
SELECT @CountAfter_Users = COUNT(*) FROM Users

PRINT 'TidpFiles: ' + CONVERT(VARCHAR, @CountAfter_TidpFiles) + ' (was ' + CONVERT(VARCHAR, @CountBefore_TidpFiles) + ')'
PRINT 'Documents (TIDP rows): ' + CONVERT(VARCHAR, @CountAfter_Documents) + ' (was ' + CONVERT(VARCHAR, @CountBefore_Documents) + ')'
PRINT 'TidpFolderOwners: ' + CONVERT(VARCHAR, @CountAfter_TidpFolderOwners) + ' (was ' + CONVERT(VARCHAR, @CountBefore_TidpFolderOwners) + ')'
PRINT 'TidpFolderDisciplines: ' + CONVERT(VARCHAR, @CountAfter_TidpFolderDisciplines) + ' (was ' + CONVERT(VARCHAR, @CountBefore_TidpFolderDisciplines) + ')'
PRINT 'TidpFolderSyncs: ' + CONVERT(VARCHAR, @CountAfter_TidpFolderSyncs) + ' (was ' + CONVERT(VARCHAR, @CountBefore_TidpFolderSyncs) + ')'
PRINT 'ImportBatches (TIDP): ' + CONVERT(VARCHAR, @CountAfter_ImportBatches_TIDP) + ' (was ' + CONVERT(VARCHAR, @CountBefore_ImportBatches_TIDP) + ')'
PRINT 'Projects (should be unchanged): ' + CONVERT(VARCHAR, @CountAfter_Projects) + ' (was ' + CONVERT(VARCHAR, @CountBefore_Projects) + ')'
PRINT 'Users (should be unchanged): ' + CONVERT(VARCHAR, @CountAfter_Users) + ' (was ' + CONVERT(VARCHAR, @CountBefore_Users) + ')'
PRINT ''

-- =====================================================================
-- Verification
-- =====================================================================

PRINT '=========================================='
PRINT 'VERIFICATION:'
PRINT '=========================================='

IF @CountAfter_Projects <> @CountBefore_Projects
    PRINT 'WARNING: Projects count changed! Expected no change.'
ELSE
    PRINT 'OK: Projects unchanged'

IF @CountAfter_Users <> @CountBefore_Users
    PRINT 'WARNING: Users count changed! Expected no change.'
ELSE
    PRINT 'OK: Users unchanged'

IF @CountAfter_TidpFiles = 0 AND @CountAfter_Documents = 0 AND @CountAfter_TidpFolderOwners = 0
    PRINT 'OK: All TIDP data deleted'
ELSE
    PRINT 'WARNING: Some TIDP data still remains'

PRINT ''
PRINT 'Reset complete. You can now upload 02.TIDPs again from scratch.'
