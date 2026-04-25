// src/Shared/API/Controllers/UserController.cs

using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs.Users;
using shop_back.src.Shared.Application.DTOs.Common;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using shop_back.src.Shared.Application.Exceptions;

namespace shop_back.src.Shared.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _service;
        private readonly IMailVerificationService _mailVerificationService;

        public UserController(IUserService service, IMailVerificationService mailVerificationService)
        {
            _service = service;
            _mailVerificationService = mailVerificationService;
        }

        [Authorize]
        [HttpPost]
        [HasPermissionAny("read-admin-users")]
        public async Task<IActionResult> GetUsers([FromBody] UserFilterRequest request)
        {
            var result = await _service.GetUsersAsync(request);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("{id}")]
        [HasPermissionAny("read-admin-users")]
        public async Task<IActionResult> GetUser(Guid id)
        {
            var user = await _service.GetUserAsync(id);
            if (user == null) return NotFound();

            return Ok(user);
        }

        [Authorize]
        [HttpPost("create")]
        [HasPermissionAny("create-admin-users")]
        public async Task<IActionResult> Create([FromForm] CreateUserRequest request)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var result = await _service.CreateUserAsync(request, currentUserId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            var result = await _mailVerificationService.VerifyTokenAsync(token);
            if (!result.Success) return BadRequest(result.Message);

            return Ok(result.Message);
        }

        [Authorize]
        [HasPermissionAny("create-admin-users")]
        [HttpPost("{id}/resend-verification")]
        public async Task<IActionResult> ResendVerification(Guid id)
        {
            var result = await _mailVerificationService.ResendVerificationAsync(id);

            if (!result.Success)
                return BadRequest(result.Message);

            return Ok(result.Message);
        }

        [Authorize]
        [HasPermissionAny("update-admin-users")]
        [HttpPost("{id}/regenerate-qr")]
        public async Task<IActionResult> RegenerateQr(Guid id)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            var user = await _service.RegenerateQrAsync(id, currentUserId);

            if (user == null) return NotFound(new { message = "User not found" });

            return Ok(new { qrCode = user.QRCode, message = "QR Code regenerated successfully" });
        }

        [Authorize]
        [HttpGet("{id}/edit")]
        [HasPermissionAny("update-admin-users")]
        public async Task<IActionResult> GetUserForEdit(Guid id)
        {
            var user = await _service.GetUserForEditAsync(id);
            if (user == null) return NotFound();

            return Ok(user);
        }

        [Authorize]
        [HttpPut("{id}")]
        [HasPermissionAny("update-admin-users")]
        public async Task<IActionResult> Update(Guid id, [FromForm] UpdateUserRequest request)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            var result = await _service.UpdateUserAsync(id, request, currentUserId);

            if (result.Success)
            {
                return Ok(new { success = true, message = result.Message });
            }
            
            return BadRequest(new { success = false, message = result.Message });
        }

        [Authorize]
        [HttpGet("profile")]
        [HasPermissionAny("read-admin-profile")]
        public async Task<IActionResult> GetProfile()
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            if (string.IsNullOrEmpty(currentUserId) || !Guid.TryParse(currentUserId, out var userId))
                return Unauthorized();

            var user = await _service.GetProfileAsync(userId);
            if (user == null) return NotFound();

            return Ok(user);
        }

        [Authorize]
        [HttpPut("profile")]
        [HasPermissionAny("update-admin-profile")]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileRequest request)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            if (string.IsNullOrEmpty(currentUserId) || !Guid.TryParse(currentUserId, out var userId))
                return Unauthorized();

            var result = await _service.UpdateProfileAsync(userId, request);

            return result.Success ? Ok(result) : BadRequest(result.Message);
        }

        [Authorize]
        [HttpDelete("{id}")]
        [HasPermissionAny("delete-admin-users")]
        public async Task<IActionResult> DeleteUser(Guid id, [FromQuery] bool permanent = false)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            try
            {
                var result = await _service.DeleteUserAsync(id, permanent, currentUserId);
                
                if (!result.Success)
                    return BadRequest(new { message = result.Message });
                
                return Ok(new { 
                    message = result.Message, 
                    deleteType = result.DeleteType 
                });
            }
            catch (ForeignKeyConstraintException ex)
            {
                return BadRequest(new { 
                    message = ex.Message,
                    blockingTables = ex.BlockingTables
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("{id}/restore")]
        [HasPermissionAny("restore-admin-users")]
        public async Task<IActionResult> RestoreUser(Guid id)
        {
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            var result = await _service.RestoreUserAsync(id, currentUserId);
            
            return result.Success ? Ok(new { message = result.Message }) : BadRequest(new { message = result.Message });
        }

        [Authorize]
        [HttpGet("{id}/delete-info")]
        [HasPermissionAny("delete-admin-users")]
        public async Task<IActionResult> GetDeleteInfo(Guid id)
        {
            var result = await _service.CheckDeleteEligibilityAsync(id);
            
            if (!result.Success)
                return NotFound(new { message = result.Message });
            
            return Ok(new { 
                canBePermanent = result.CanBePermanent,
                message = result.Message,
                hasRelatedRecords = result.HasRelatedRecords,
                hasVerifiedEmail = result.HasVerifiedEmail,
                relatedRecordsDetails = result.RelatedRecordsDetails
            });
        }

        [Authorize]
        [HttpPost("bulk-delete")]
        [HasPermissionAny("delete-admin-users")]
        public async Task<IActionResult> BulkDelete([FromBody] BulkOperationRequest request)
        {
            // Validate and convert IDs
            var (isValid, invalidIds) = request.ValidateIds();
            
            if (!isValid)
            {
                return BadRequest(new 
                { 
                    success = false, 
                    message = $"Invalid GUID format for IDs: {string.Join(", ", invalidIds)}" 
                });
            }
            
            var guids = request.GetGuids();
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            var result = await _service.BulkDeleteAsync(guids, request.Permanent, currentUserId);
            
            if (!result.Success)
                return BadRequest(result);
            
            return Ok(result);
        }

        [Authorize]
        [HttpPost("bulk-restore")]
        [HasPermissionAny("restore-admin-users")]
        public async Task<IActionResult> BulkRestore([FromBody] BulkOperationRequest request)
        {
            // Validate and convert IDs
            var (isValid, invalidIds) = request.ValidateIds();
            
            if (!isValid)
            {
                return BadRequest(new 
                { 
                    success = false, 
                    message = $"Invalid GUID format for IDs: {string.Join(", ", invalidIds)}" 
                });
            }
            
            var guids = request.GetGuids();
            var currentUserId = User?.FindFirst("UserId")?.Value 
                                ?? User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            
            var result = await _service.BulkRestoreAsync(guids, currentUserId);
            
            if (!result.Success)
                return BadRequest(result);
            
            return Ok(result);
        }
    }
}