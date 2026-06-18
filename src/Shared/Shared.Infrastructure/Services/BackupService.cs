// src/Shared/Shared.Infrastructure/Services/BackupService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Npgsql;
using shop_back.src.Shared.Application.DTOs.Backups;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Infrastructure.Helpers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class BackupService : IBackupService
    {
        private readonly IBackupRepository _backupRepository;
        private readonly IBackupScheduleRepository _scheduleRepository;
        private readonly IStorageDestinationRepository _storageRepository;
        private readonly IBackupLogRepository _logRepository;
        private readonly IConfiguration _configuration;
        private readonly IGoogleDriveService _googleDriveService;
        private readonly string _connectionString;

        public BackupService(
            IBackupRepository backupRepository,
            IBackupScheduleRepository scheduleRepository,
            IStorageDestinationRepository storageRepository,
            IBackupLogRepository logRepository,
            IConfiguration configuration,
            IGoogleDriveService googleDriveService)
        {
            _backupRepository = backupRepository;
            _scheduleRepository = scheduleRepository;
            _storageRepository = storageRepository;
            _logRepository = logRepository;
            _configuration = configuration;
            _googleDriveService = googleDriveService;
            
            // Try multiple ways to get connection string
            _connectionString = GetConnectionString();
            
            if (string.IsNullOrEmpty(_connectionString))
            {
                Console.WriteLine("⚠️ WARNING: Connection string is empty!");
            }
            else
            {
                var logConn = _connectionString;
                if (logConn.Contains("Password="))
                {
                    var passwordMatch = System.Text.RegularExpressions.Regex.Match(logConn, @"Password=([^;]+)");
                    if (passwordMatch.Success)
                    {
                        logConn = logConn.Replace(passwordMatch.Value, "Password=*****");
                    }
                }
                Console.WriteLine($"🔗 Connection string loaded: {logConn}");
            }
        }

        private string GetConnectionString()
        {
            // Try multiple sources - handle null values safely
            var sources = new List<string?>();
            sources.Add(_configuration.GetConnectionString("DefaultConnection"));
            sources.Add(Environment.GetEnvironmentVariable("DefaultConnection"));
            sources.Add(_configuration["DefaultConnection"]);
            
            // Also try to get from DotNetEnv directly
            try
            {
                var envConn = DotNetEnv.Env.GetString("DefaultConnection");
                sources.Add(envConn);
            }
            catch { }

            // Return the first non-null, non-empty value
            return sources.FirstOrDefault(s => !string.IsNullOrEmpty(s)) ?? string.Empty;
        }

        public async Task<BackupDto> CreateBackupAsync(CreateBackupRequest request, Guid? userId = null)
        {
            var backupName = request?.Name ?? $"Backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
            var fileName = $"{backupName}.sql";
            
            var backupPath = await CreateDatabaseBackupPureCSharpAsync(fileName);
            
            if (string.IsNullOrEmpty(backupPath))
                throw new Exception("Failed to create database backup");

            var fileInfo = new FileInfo(backupPath);
            var checksum = ComputeFileChecksum(backupPath);

            // Get all active storage destinations
            var destinations = await _storageRepository.GetActiveDestinationsAsync();
            
            Console.WriteLine($"📊 Found {destinations?.Count() ?? 0} storage destinations");
            
            // If no destinations exist, create a default local one
            if (destinations == null || !destinations.Any())
            {
                Console.WriteLine("⚠️ No storage destinations found. Creating default local storage.");
                
                var defaultDestination = new StorageDestination
                {
                    Type = "Local",
                    Name = "Local Storage",
                    Config = new Dictionary<string, object> { { "path", "backups" } },
                    Priority = 1,
                    IsActive = true,
                    IsPrimary = true,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                await _storageRepository.AddAsync(defaultDestination);
                await _storageRepository.SaveChangesAsync();
                
                destinations = new List<StorageDestination> { defaultDestination };
            }

            // Upload to ALL destinations
            var storagePaths = new List<string>();
            var backupId = 0L;
            
            // Create backup record first
            var backup = new Backup
            {
                Name = backupName,
                FileName = fileName,
                FilePath = backupPath,
                FileSize = fileInfo.Length,
                StorageType = "Local", // Default
                StoragePath = backupPath, // Default
                Checksum = checksum,
                Status = "InProgress",
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _backupRepository.AddAsync(backup);
            await _backupRepository.SaveChangesAsync();
            backupId = backup.Id;

            // Upload to each destination
            foreach (var dest in destinations)
            {
                try
                {
                    Console.WriteLine($"📤 Uploading to {dest.Type}...");
                    var storagePath = await UploadToStorageAsync(backupPath, dest, fileName);
                    storagePaths.Add(storagePath);
                    
                    // Update backup with the storage info
                    backup.StoragePath = string.Join(",", storagePaths);
                    backup.StorageType = dest.Type;
                    await _backupRepository.UpdateAsync(backup);
                    
                    // Log successful upload
                    await _logRepository.AddAsync(new BackupLog
                    {
                        BackupId = backupId,
                        Action = "Upload",
                        Status = "Success",
                        Message = $"Uploaded to {dest.Type}: {storagePath}",
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow
                    });
                    
                    Console.WriteLine($"✅ Successfully uploaded to {dest.Type}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to upload to {dest.Type}: {ex.Message}");
                    
                    // Log failed upload
                    await _logRepository.AddAsync(new BackupLog
                    {
                        BackupId = backupId,
                        Action = "Upload",
                        Status = "Failed",
                        Message = $"Failed to upload to {dest.Type}: {ex.Message}",
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            
            // Update backup status
            backup.Status = "Success";
            backup.UpdatedAt = DateTime.UtcNow;
            await _backupRepository.UpdateAsync(backup);
            await _backupRepository.SaveChangesAsync();
            
            // Log the backup creation
            await _logRepository.AddAsync(new BackupLog
            {
                BackupId = backupId,
                Action = "Create",
                Status = "Success",
                Message = $"Backup created: {backupName}",
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            });
            await _logRepository.SaveChangesAsync();

            return MapToDto(backup);
        }

        public async Task<bool> TestRemoteUploadAsync()
        {
            try
            {
                var testContent = "test content";
                var bytes = Encoding.UTF8.GetBytes(testContent);
                var stream = new MemoryStream(bytes);
                var formFile = new FormFile(stream, 0, bytes.Length, "file", "test.txt")
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "text/plain"
                };
                
                var result = await FileHelper.SaveFileAsync(formFile, "backups", processImage: false);
                Console.WriteLine($"Test upload result: {result}");
                return !string.IsNullOrEmpty(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test upload failed: {ex.Message}");
                return false;
            }
        }

        private async Task<string> CreateDatabaseBackupPureCSharpAsync(string fileName)
        {
            var tempPath = Path.GetTempPath();
            var backupPath = Path.Combine(tempPath, fileName);

            try
            {
                Console.WriteLine($"🔄 Creating backup using pure C# (no pg_dump dependency)");
                Console.WriteLine($"📁 Backup path: {backupPath}");

                if (string.IsNullOrEmpty(_connectionString))
                {
                    throw new Exception("Connection string is empty. Cannot connect to database.");
                }

                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                Console.WriteLine($"✅ Connected to database successfully");

                var tables = new List<string>();
                using var tableCmd = new NpgsqlCommand(@"
                    SELECT tablename FROM pg_tables 
                    WHERE schemaname = 'public' 
                    AND tablename NOT LIKE 'pg_%' 
                    AND tablename NOT LIKE 'sql_%'", connection);
                
                using var reader = await tableCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }
                await reader.CloseAsync();

                using var dbCmd = new NpgsqlCommand("SELECT current_database()", connection);
                var dbName = await dbCmd.ExecuteScalarAsync() as string ?? "shop_db";

                Console.WriteLine($"📊 Found {tables.Count} tables in database '{dbName}'");

                using var writer = new StreamWriter(backupPath, false, Encoding.UTF8);
                
                await writer.WriteLineAsync($"-- Database Backup Generated by ShopSphere");
                await writer.WriteLineAsync($"-- Database: {dbName}");
                await writer.WriteLineAsync($"-- Generated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                await writer.WriteLineAsync("-- =============================================");
                await writer.WriteLineAsync();

                var totalRows = 0;
                foreach (var table in tables)
                {
                    Console.WriteLine($"🔄 Processing table: {table}");

                    using var schemaCmd = new NpgsqlCommand($@"
                        SELECT column_name, data_type, is_nullable, column_default
                        FROM information_schema.columns 
                        WHERE table_schema = 'public' AND table_name = '{table}'
                        ORDER BY ordinal_position", connection);
                    
                    using var schemaReader = await schemaCmd.ExecuteReaderAsync();
                    var columns = new List<string>();
                    var columnDefs = new List<string>();
                    
                    while (await schemaReader.ReadAsync())
                    {
                        var colName = schemaReader.GetString(0);
                        columns.Add(colName);
                        var colDef = $"\"{colName}\" {schemaReader.GetString(1)}";
                        if (schemaReader.GetString(2) == "NO")
                            colDef += " NOT NULL";
                        if (!schemaReader.IsDBNull(3))
                            colDef += $" DEFAULT {schemaReader.GetString(3)}";
                        columnDefs.Add(colDef);
                    }
                    await schemaReader.CloseAsync();

                    await writer.WriteLineAsync($"DROP TABLE IF EXISTS \"{table}\" CASCADE;");
                    await writer.WriteLineAsync($"CREATE TABLE \"{table}\" (");
                    await writer.WriteLineAsync($"  {string.Join(",\n  ", columnDefs)}");
                    await writer.WriteLineAsync(");\n");

                    using var dataCmd = new NpgsqlCommand($"SELECT * FROM \"{table}\"", connection);
                    using var dataReader = await dataCmd.ExecuteReaderAsync();
                    
                    var rowCount = 0;
                    while (await dataReader.ReadAsync())
                    {
                        var values = new List<string>();
                        for (int i = 0; i < dataReader.FieldCount; i++)
                        {
                            if (dataReader.IsDBNull(i))
                            {
                                values.Add("NULL");
                            }
                            else
                            {
                                var value = dataReader.GetValue(i);
                                var stringValue = value?.ToString() ?? string.Empty;
                                stringValue = stringValue.Replace("'", "''");
                                
                                // Handle different data types
                                if (value is DateTime dateTime)
                                {
                                    stringValue = dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
                                }
                                else if (value is bool boolVal)
                                {
                                    stringValue = boolVal ? "true" : "false";
                                }
                                else if (value is Guid guidVal)
                                {
                                    stringValue = guidVal.ToString();
                                }
                                else if (value is byte[] byteArray)
                                {
                                    stringValue = Convert.ToBase64String(byteArray);
                                }
                                
                                values.Add($"'{stringValue}'");
                            }
                        }
                        
                        if (rowCount == 0)
                        {
                            await writer.WriteLineAsync($"INSERT INTO \"{table}\" ({string.Join(", ", columns.Select(c => $"\"{c}\""))}) VALUES");
                        }
                        
                        await writer.WriteAsync($"  ({string.Join(", ", values)})");
                        rowCount++;
                        totalRows++;
                        
                        if (!await dataReader.ReadAsync())
                        {
                            await writer.WriteLineAsync(";");
                            break;
                        }
                        else
                        {
                            await writer.WriteLineAsync(",");
                            continue;
                        }
                    }
                    await dataReader.CloseAsync();

                    if (rowCount > 0)
                    {
                        await writer.WriteLineAsync();
                    }
                    Console.WriteLine($"  ✅ Table '{table}': {rowCount} rows exported");
                }

                await writer.WriteLineAsync("-- =============================================");
                await writer.WriteLineAsync($"-- Backup Completed Successfully");
                await writer.WriteLineAsync($"-- Total Tables: {tables.Count}, Total Rows: {totalRows}");
                await writer.FlushAsync();

                var fileSize = new FileInfo(backupPath).Length;
                Console.WriteLine($"✅ Backup created successfully: {backupPath} ({fileSize} bytes)");
                Console.WriteLine($"📊 Total Tables: {tables.Count}, Total Rows: {totalRows}");
                return backupPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Database backup failed: {ex.Message}");
                if (File.Exists(backupPath))
                {
                    try { File.Delete(backupPath); } catch { }
                }
                throw;
            }
        }

        private async Task<string> UploadToStorageAsync(string filePath, StorageDestination destination, string fileName)
        {
            try
            {
                Console.WriteLine($"📤 Uploading to {destination.Type}: {destination.Name}");
                Console.WriteLine($"📤 Destination Config: {JsonSerializer.Serialize(destination.Config)}");
                Console.WriteLine($"📤 File: {filePath}, Size: {new FileInfo(filePath).Length} bytes");
                
                switch (destination.Type.ToLower())
                {
                    case "local":
                        var fileBytes = await File.ReadAllBytesAsync(filePath);
                        return await UploadToLocalAsync(fileBytes, fileName);
                        
                    case "googledrive":
                        // Upload to Google Drive
                        Console.WriteLine($"📤 Uploading to Google Drive...");
                        try
                        {
                            var driveResult = await _googleDriveService.UploadFileAsync(filePath, fileName, "backups");
                            Console.WriteLine($"✅ Uploaded to Google Drive: {driveResult}");
                            return driveResult;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Google Drive upload failed: {ex.Message}");
                            throw;
                        }
                        
                    case "remoteserver":
                        // Upload to Remote Server (cPanel)
                        Console.WriteLine($"📤 Uploading to Remote Server...");
                        try
                        {
                            var bytes = await File.ReadAllBytesAsync(filePath);
                            var remoteResult = await UploadToRemoteServerAsync(bytes, fileName);
                            Console.WriteLine($"✅ Uploaded to Remote Server: {remoteResult}");
                            return remoteResult;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Remote Server upload failed: {ex.Message}");
                            throw;
                        }
                        
                    default:
                        throw new Exception($"Unsupported storage type: {destination.Type}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to upload to {destination.Type}: {ex.Message}");
                throw new Exception($"Failed to upload to {destination.Type}: {ex.Message}");
            }
        }

        private async Task<string> UploadToLocalAsync(byte[] fileBytes, string fileName)
        {
            var backupDir = Path.Combine(Directory.GetCurrentDirectory(), "backups");
            Directory.CreateDirectory(backupDir);
            
            var filePath = Path.Combine(backupDir, fileName);
            await File.WriteAllBytesAsync(filePath, fileBytes);
            
            return filePath;
        }

        private async Task<string> UploadToRemoteServerAsync(byte[] fileBytes, string fileName)
        {
            try
            {
                // Use RemoteFileHelper to upload to cPanel
                // Create a fake IFormFile to use with RemoteFileHelper
                var stream = new MemoryStream(fileBytes);
                var formFile = new FormFile(stream, 0, fileBytes.Length, "file", fileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "application/sql"
                };
                
                // Use your existing RemoteFileHelper
                var result = await FileHelper.SaveFileAsync(formFile, "backups", processImage: false);
                
                if (string.IsNullOrEmpty(result))
                    throw new Exception("Failed to upload to remote server");
                
                Console.WriteLine($"✅ Uploaded to remote server: {result}");
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to upload to remote server: {ex.Message}");
            }
        }

        private string ComputeFileChecksum(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public async Task CleanupOldBackupsAsync(int retentionDays, Guid? userId = null)
        {
            var oldBackups = await _backupRepository.GetOldBackupsAsync(retentionDays);
            
            foreach (var backup in oldBackups)
            {
                try
                {
                    await DeleteFromStorageAsync(backup);
                    await _backupRepository.DeleteAsync(backup.Id);
                    
                    await _logRepository.AddAsync(new BackupLog
                    {
                        BackupId = backup.Id,
                        Action = "Cleanup",
                        Status = "Success",
                        Message = $"Deleted backup older than {retentionDays} days",
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    await _logRepository.AddAsync(new BackupLog
                    {
                        BackupId = backup.Id,
                        Action = "Cleanup",
                        Status = "Failed",
                        Message = $"Failed to delete backup: {ex.Message}",
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            
            await _logRepository.SaveChangesAsync();
        }

        private async Task DeleteFromStorageAsync(Backup backup)
        {
            switch (backup.StorageType.ToLower())
            {
                case "local":
                    if (File.Exists(backup.FilePath))
                        File.Delete(backup.FilePath);
                    break;
                case "googledrive":
                    await _googleDriveService.DeleteFileAsync(backup.StoragePath);
                    break;
                case "remoteserver":
                    await FileHelper.DeleteFileAsync(backup.StoragePath);
                    break;
                default:
                    throw new Exception($"Unsupported storage type: {backup.StorageType}");
            }
        }

        public async Task<BackupDto?> GetBackupByIdAsync(long id)
        {
            var backup = await _backupRepository.GetByIdAsync(id);
            return backup != null ? MapToDto(backup) : null;
        }

        public async Task<(IEnumerable<BackupDto> Items, int TotalCount, int GrandTotalCount)> GetBackupsAsync(BackupFilterRequest request)
        {
            var (items, totalCount, grandTotalCount) = await _backupRepository.GetFilteredAsync(request);
            return (items.Select(MapToDto), totalCount, grandTotalCount);
        }

        public async Task DeleteBackupAsync(long id, Guid? userId = null)
        {
            var backup = await _backupRepository.GetByIdAsync(id);
            if (backup == null)
                throw new Exception("Backup not found");

            await DeleteFromStorageAsync(backup);
            await _backupRepository.DeletePermanentlyAsync(id);
            
            await _logRepository.AddAsync(new BackupLog
            {
                BackupId = id,
                Action = "Delete",
                Status = "Success",
                Message = "Backup permanently deleted",
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            });
            
            await _logRepository.SaveChangesAsync();
        }

        public async Task<string> DownloadBackupAsync(long id)
        {
            var backup = await _backupRepository.GetByIdAsync(id);
            if (backup == null)
                throw new Exception("Backup not found");

            return backup.FilePath;
        }

        public async Task RestoreBackupAsync(long id, Guid? userId = null)
        {
            var backup = await _backupRepository.GetByIdAsync(id);
            if (backup == null)
                throw new Exception("Backup not found");

            string backupFile;
            if (backup.StorageType != "Local")
            {
                backupFile = await DownloadFromStorageAsync(backup);
            }
            else
            {
                backupFile = backup.FilePath;
            }

            if (!File.Exists(backupFile))
                throw new Exception("Backup file not found");

            await RestoreDatabaseAsync(backupFile);

            await _logRepository.AddAsync(new BackupLog
            {
                BackupId = id,
                Action = "Restore",
                Status = "Success",
                Message = $"Database restored from backup: {backup.Name}",
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            });
            await _logRepository.SaveChangesAsync();
        }

        private async Task<string> DownloadFromStorageAsync(Backup backup)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), backup.FileName);
            
            switch (backup.StorageType.ToLower())
            {
                case "googledrive":
                    await _googleDriveService.DownloadFileAsync(backup.StoragePath, tempPath);
                    break;
                case "remoteserver":
                    using (var client = new HttpClient())
                    {
                        var bytes = await client.GetByteArrayAsync(backup.StoragePath);
                        await File.WriteAllBytesAsync(tempPath, bytes);
                    }
                    break;
                default:
                    throw new Exception($"Unsupported storage type: {backup.StorageType}");
            }

            return tempPath;
        }

        private async Task RestoreDatabaseAsync(string backupFile)
        {
            try
            {
                Console.WriteLine($"🔄 Restoring database from: {backupFile}");
                
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                
                var sqlContent = await File.ReadAllTextAsync(backupFile);
                
                using var command = new NpgsqlCommand(sqlContent, connection);
                command.CommandTimeout = 300;
                await command.ExecuteNonQueryAsync();
                
                Console.WriteLine($"✅ Database restored successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Database restore failed: {ex.Message}");
                throw;
            }
        }

        public async Task<BackupStatisticsDto> GetStatisticsAsync()
        {
            return await _backupRepository.GetStatisticsAsync();
        }

        // Schedule management methods
        public async Task<BackupScheduleDto> CreateScheduleAsync(BackupScheduleDto schedule, Guid? userId = null)
        {
            var entity = new BackupSchedule
            {
                Name = schedule.Name,
                CronExpression = schedule.CronExpression,
                IsActive = schedule.IsActive,
                RetentionDays = schedule.RetentionDays,
                StorageDestinations = schedule.StorageDestinations,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _scheduleRepository.AddAsync(entity);
            await _scheduleRepository.SaveChangesAsync();

            return MapToScheduleDto(entity);
        }

        public async Task<BackupScheduleDto> UpdateScheduleAsync(long id, BackupScheduleDto schedule, Guid? userId = null)
        {
            var entity = await _scheduleRepository.GetByIdAsync(id);
            if (entity == null)
                throw new Exception("Schedule not found");

            entity.Name = schedule.Name;
            entity.CronExpression = schedule.CronExpression;
            entity.IsActive = schedule.IsActive;
            entity.RetentionDays = schedule.RetentionDays;
            entity.StorageDestinations = schedule.StorageDestinations;
            entity.UpdatedAt = DateTime.UtcNow;

            await _scheduleRepository.UpdateAsync(entity);
            await _scheduleRepository.SaveChangesAsync();

            return MapToScheduleDto(entity);
        }

        public async Task DeleteScheduleAsync(long id, Guid? userId = null)
        {
            await _scheduleRepository.DeleteAsync(id);
            await _scheduleRepository.SaveChangesAsync();
        }

        public async Task<List<BackupScheduleDto>> GetSchedulesAsync()
        {
            var schedules = await _scheduleRepository.GetActiveSchedulesAsync();
            return schedules.Select(MapToScheduleDto).ToList();
        }

        // Storage destination methods
        public async Task<StorageDestinationDto> CreateStorageDestinationAsync(StorageDestinationDto destination, Guid? userId = null)
        {
            var entity = new StorageDestination
            {
                Type = destination.Type,
                Name = destination.Name,
                Config = destination.Config,
                Priority = destination.Priority,
                IsActive = destination.IsActive,
                IsPrimary = destination.IsPrimary,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (entity.IsPrimary)
            {
                var existing = await _storageRepository.GetActiveDestinationsAsync();
                foreach (var e in existing)
                {
                    if (e.Id != entity.Id)
                    {
                        e.IsPrimary = false;
                        await _storageRepository.UpdateAsync(e);
                    }
                }
            }

            await _storageRepository.AddAsync(entity);
            await _storageRepository.SaveChangesAsync();

            return MapToStorageDto(entity);
        }

        public async Task<StorageDestinationDto> UpdateStorageDestinationAsync(long id, StorageDestinationDto destination, Guid? userId = null)
        {
            var entity = await _storageRepository.GetByIdAsync(id);
            if (entity == null)
                throw new Exception("Storage destination not found");

            entity.Type = destination.Type;
            entity.Name = destination.Name;
            entity.Config = destination.Config;
            entity.Priority = destination.Priority;
            entity.IsActive = destination.IsActive;
            entity.IsPrimary = destination.IsPrimary;
            entity.UpdatedAt = DateTime.UtcNow;

            if (entity.IsPrimary)
            {
                var existing = await _storageRepository.GetActiveDestinationsAsync();
                foreach (var e in existing)
                {
                    if (e.Id != entity.Id)
                    {
                        e.IsPrimary = false;
                        await _storageRepository.UpdateAsync(e);
                    }
                }
            }

            await _storageRepository.UpdateAsync(entity);
            await _storageRepository.SaveChangesAsync();

            return MapToStorageDto(entity);
        }

        public async Task DeleteStorageDestinationAsync(long id, Guid? userId = null)
        {
            await _storageRepository.DeleteAsync(id);
            await _storageRepository.SaveChangesAsync();
        }

        public async Task<List<StorageDestinationDto>> GetStorageDestinationsAsync()
        {
            var destinations = await _storageRepository.GetActiveDestinationsAsync();
            return destinations.Select(MapToStorageDto).ToList();
        }

        public async Task<bool> TestStorageConnectionAsync(long id)
        {
            var destination = await _storageRepository.GetByIdAsync(id);
            if (destination == null)
                throw new Exception("Storage destination not found");

            switch (destination.Type.ToLower())
            {
                case "local":
                    return true;
                case "googledrive":
                    return await _googleDriveService.TestConnectionAsync();
                case "remoteserver":
                    return await TestRemoteServerConnectionAsync(destination.Config);
                default:
                    throw new Exception($"Unsupported storage type: {destination.Type}");
            }
        }

        private async Task<bool> TestRemoteServerConnectionAsync(Dictionary<string, object> config)
        {
            try
            {
                var host = config.GetValueOrDefault("host")?.ToString();
                if (string.IsNullOrEmpty(host))
                    return false;
                
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var response = await client.GetAsync($"http://{host}/");
                
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // Mapping methods
        private BackupDto MapToDto(Backup backup)
        {
            return new BackupDto
            {
                Id = backup.Id,
                Name = backup.Name,
                FileName = backup.FileName,
                FilePath = backup.FilePath,
                FileSize = backup.FileSize,
                StorageType = backup.StorageType,
                StoragePath = backup.StoragePath,
                Checksum = backup.Checksum,
                Status = backup.Status,
                IsDeleted = backup.IsDeleted,
                DeletedAt = backup.DeletedAt,
                CreatedByName = backup.CreatedByUser?.Name,
                CreatedAt = backup.CreatedAt,
                UpdatedAt = backup.UpdatedAt
            };
        }

        private BackupScheduleDto MapToScheduleDto(BackupSchedule schedule)
        {
            return new BackupScheduleDto
            {
                Id = schedule.Id,
                Name = schedule.Name,
                CronExpression = schedule.CronExpression,
                IsActive = schedule.IsActive,
                RetentionDays = schedule.RetentionDays,
                StorageDestinations = schedule.StorageDestinations,
                LastRunAt = schedule.LastRunAt,
                NextRunAt = schedule.NextRunAt,
                CreatedByName = schedule.CreatedByUser?.Name,
                CreatedAt = schedule.CreatedAt,
                UpdatedAt = schedule.UpdatedAt
            };
        }

        private StorageDestinationDto MapToStorageDto(StorageDestination destination)
        {
            return new StorageDestinationDto
            {
                Id = destination.Id,
                Type = destination.Type,
                Name = destination.Name,
                Config = destination.Config,
                Priority = destination.Priority,
                IsActive = destination.IsActive,
                IsPrimary = destination.IsPrimary,
                CreatedByName = destination.CreatedByUser?.Name,
                CreatedAt = destination.CreatedAt,
                UpdatedAt = destination.UpdatedAt
            };
        }
    }

    internal class RemoteUploadResponse
    {
        public bool Success { get; set; }
        public string? Url { get; set; }
        public string? Error { get; set; }
    }
}