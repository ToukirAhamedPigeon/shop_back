// src/Shared/Shared.API/Controllers/BackupController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.Backups;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Services.Authorization;
using System.IdentityModel.Tokens.Jwt;

namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BackupController : ControllerBase
    {
        private readonly IBackupService _backupService;

        public BackupController(IBackupService backupService)
        {
            _backupService = backupService;
        }

        private Guid? GetCurrentUserId()
        {
            var userId = User?.FindFirst("UserId")?.Value 
                         ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(userId, out var parsed) ? parsed : null;
        }

        [HttpGet("statistics")]
        [HasPermissionAny("read-admin-backups")]
        public async Task<IActionResult> GetStatistics()
        {
            var stats = await _backupService.GetStatisticsAsync();
            return Ok(stats);
        }

        [HttpPost]
        [HasPermissionAny("read-admin-backups")]
        public async Task<IActionResult> GetBackups([FromBody] BackupFilterRequest request)
        {
            var (items, totalCount, grandTotalCount) = await _backupService.GetBackupsAsync(request);
            return Ok(new { backups = items, totalCount, grandTotalCount });
        }

        [HttpGet("{id}")]
        [HasPermissionAny("read-admin-backups")]
        public async Task<IActionResult> GetBackup(long id)
        {
            var backup = await _backupService.GetBackupByIdAsync(id);
            if (backup == null)
                return NotFound(new { message = "Backup not found" });
            return Ok(backup);
        }

        [HttpPost("create")]
        [HasPermissionAny("create-admin-backups")]
        public async Task<IActionResult> CreateBackup([FromBody] CreateBackupRequest request)
        {
            var userId = GetCurrentUserId();
            var backup = await _backupService.CreateBackupAsync(request, userId);
            return Ok(new { success = true, backup });
        }

        [HttpDelete("{id}")]
        [HasPermissionAny("delete-admin-backups")]
        public async Task<IActionResult> DeleteBackup(long id)
        {
            var userId = GetCurrentUserId();
            await _backupService.DeleteBackupAsync(id, userId);
            return Ok(new { success = true });
        }

        [HttpGet("{id}/download")]
        [HasPermissionAny("read-admin-backups")]
        public async Task<IActionResult> DownloadBackup(long id)
        {
            var filePath = await _backupService.DownloadBackupAsync(id);
            var backup = await _backupService.GetBackupByIdAsync(id);
            
            if (!System.IO.File.Exists(filePath))
                return NotFound(new { message = "Backup file not found" });

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, "application/octet-stream", backup?.FileName ?? "backup.sql");
        }

        [HttpPost("{id}/restore")]
        [HasPermissionAny("restore-admin-backups")]
        public async Task<IActionResult> RestoreBackup(long id)
        {
            var userId = GetCurrentUserId();
            await _backupService.RestoreBackupAsync(id, userId);
            return Ok(new { success = true, message = "Backup restored successfully" });
        }

        [HttpPost("cleanup")]
        [HasPermissionAny("delete-admin-backups")]
        public async Task<IActionResult> CleanupOldBackups([FromBody] BackupCleanupRequest request)
        {
            var userId = GetCurrentUserId();
            await _backupService.CleanupOldBackupsAsync(request.RetentionDays, userId);
            return Ok(new { success = true, message = $"Cleaned up backups older than {request.RetentionDays} days" });
        }

        [HttpGet("schedule")]
        [HasPermissionAny("read-admin-backups")]
        public async Task<IActionResult> GetSchedules()
        {
            var schedules = await _backupService.GetSchedulesAsync();
            return Ok(new { schedules });
        }

        [HttpPost("schedule")]
        [HasPermissionAny("create-admin-backups")]
        public async Task<IActionResult> CreateSchedule([FromBody] BackupScheduleDto request)
        {
            var userId = GetCurrentUserId();
            var schedule = await _backupService.CreateScheduleAsync(request, userId);
            return Ok(new { success = true, schedule });
        }

        [HttpPut("schedule/{id}")]
        [HasPermissionAny("update-admin-backups")]
        public async Task<IActionResult> UpdateSchedule(long id, [FromBody] BackupScheduleDto request)
        {
            var userId = GetCurrentUserId();
            var schedule = await _backupService.UpdateScheduleAsync(id, request, userId);
            return Ok(new { success = true, schedule });
        }

        [HttpDelete("schedule/{id}")]
        [HasPermissionAny("delete-admin-backups")]
        public async Task<IActionResult> DeleteSchedule(long id)
        {
            var userId = GetCurrentUserId();
            await _backupService.DeleteScheduleAsync(id, userId);
            return Ok(new { success = true });
        }

        [HttpGet("storage")]
        [HasPermissionAny("read-admin-backups")]
        public async Task<IActionResult> GetStorageDestinations()
        {
            var destinations = await _backupService.GetStorageDestinationsAsync();
            return Ok(new { destinations });
        }

        [HttpPost("storage")]
        [HasPermissionAny("create-admin-backups")]
        public async Task<IActionResult> CreateStorageDestination([FromBody] StorageDestinationDto request)
        {
            var userId = GetCurrentUserId();
            var destination = await _backupService.CreateStorageDestinationAsync(request, userId);
            return Ok(new { success = true, destination });
        }

        [HttpPut("storage/{id}")]
        [HasPermissionAny("update-admin-backups")]
        public async Task<IActionResult> UpdateStorageDestination(long id, [FromBody] StorageDestinationDto request)
        {
            var userId = GetCurrentUserId();
            var destination = await _backupService.UpdateStorageDestinationAsync(id, request, userId);
            return Ok(new { success = true, destination });
        }

        [HttpDelete("storage/{id}")]
        [HasPermissionAny("delete-admin-backups")]
        public async Task<IActionResult> DeleteStorageDestination(long id)
        {
            var userId = GetCurrentUserId();
            await _backupService.DeleteStorageDestinationAsync(id, userId);
            return Ok(new { success = true });
        }

        [HttpPost("storage/{id}/test")]
        [HasPermissionAny("update-admin-backups")]
        public async Task<IActionResult> TestStorageConnection(long id)
        {
            var success = await _backupService.TestStorageConnectionAsync(id);
            return Ok(new { success, message = success ? "Connection successful" : "Connection failed" });
        }
    }
}