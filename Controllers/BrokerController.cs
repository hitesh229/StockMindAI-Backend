using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockMindAI.API.DTOs;
using StockMindAI.API.Services;

namespace StockMindAI.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/portfolio/broker")]
    public class BrokerController : ControllerBase
    {
        private readonly IBrokerService _brokerService;

        public BrokerController(IBrokerService brokerService)
        {
            _brokerService = brokerService;
        }

        [HttpPost("sync")]
        public async Task<IActionResult> SyncHoldings([FromBody] BrokerSyncRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserIdFromToken();
            if (userId == 0)
            {
                return Unauthorized("Invalid user identification claim.");
            }

            try
            {
                var holdings = await _brokerService.SyncBrokerHoldingsAsync(userId, request.Broker, request.AuthCode);
                return Ok(new
                {
                    Message = $"Successfully synced portfolio with {request.Broker}.",
                    Count = holdings.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred during broker synchronization: {ex.Message}");
            }
        }

        private int GetUserIdFromToken()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 0;
        }
    }
}
