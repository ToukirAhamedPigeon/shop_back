// src/Modules/UserTable/Api/Controllers/UserTableCombinationController.cs
using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.DTOs;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Services.Authorization;
using System;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Api.Controllers
{
    [ApiController]
    [Route("api/user-table-combination")]
    public class UserTableCombinationController : ControllerBase
    {
        private readonly IUserTableCombinationService _service;

        public UserTableCombinationController(IUserTableCombinationService service)
        {
            _service = service;
        }

        [HttpGet]
        [HasPermissionAny("read-admin-dashboard")]
        public async Task<IActionResult> Get([FromQuery] string tableId, [FromQuery] Guid userId)
        {
            // Console.WriteLine("tableId: " + tableId);
            // Console.WriteLine("userId: " + userId);
            if (string.IsNullOrEmpty(tableId) || userId == Guid.Empty)
                return BadRequest("Missing tableId or userId");

            var result = await _service.GetByTableAndUserAsync(tableId, userId);
            return Ok(new { showColumnCombinations = result.ShowColumnCombinations });
        }

        [HttpPut]
        [HasPermissionAny("read-admin-dashboard")]
        public async Task<IActionResult> Put([FromBody] UserTableCombinationDTO dto)
        {
            if (dto.UserId == Guid.Empty || string.IsNullOrEmpty(dto.TableId))
                return BadRequest("Invalid userId or tableId");

            await _service.SaveOrUpdateAsync(dto.UserId, dto);
            return Ok(new { success = true });
        }
    }
}
