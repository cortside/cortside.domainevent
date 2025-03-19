using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Asp.Versioning;
using Cortside.AspNetCore.Common.Paging;
using Cortside.Common.Logging;
using Cortside.DomainEvent.EntityFramework;
using Cortside.DomainEvent.EntityFramework.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cortside.DomainEvent.Mvc.Controllers {
    /// <summary>
    /// Outbox controller
    /// </summary>
    [ApiVersionNeutral]
    [Route("api/outbox/messages")]  // TODO: need to make this configurable
    [ApiController]
    [Produces("application/json")]
    public class OutboxController<T> : ControllerBase where T : DbContext {
        private readonly ILogger<OutboxController<T>> logger;
        private readonly T db;
        private readonly OutboxHostedServiceConfiguration configuration;

        /// <summary>
        /// OutboxController
        /// </summary>
        public OutboxController(ILogger<OutboxController<T>> logger, T databaseContext, OutboxHostedServiceConfiguration configuration) {
            this.logger = logger;
            db = databaseContext;
            this.configuration = configuration;
        }

        /// <summary>
        /// Get failed messages
        /// </summary>
        [HttpGet("")]
        [Authorize("GetOutboxMessages")]
        [ProducesResponseType(typeof(PagedList<Outbox>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMessagesAsync([FromQuery] int pageNumber, int pageSize) {
            var messages = db.Set<Outbox>().Where(x => x.Status == OutboxStatus.Failed);

            var result = new PagedList<Outbox> {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = await messages.CountAsync().ConfigureAwait(false),
                Items = [],
            };
            result.Items = await messages.OrderBy(x => x.MessageId).Skip(result.PageSize * result.PageNumber).Take(result.PageSize).ToListAsync();

            return Ok(result);
        }

        /// <summary>
        /// Reset message attempts and status
        /// </summary>
        /// <param name="id"></param>
        /// <param name="input"></param>
        [HttpPost("{id}/reset")]
        [Authorize("ResetOutboxMessage")]
        [ProducesResponseType(typeof(Outbox), StatusCodes.Status200OK)]
        public async Task<IActionResult> ResetMessageAsync(string id) {
            using (logger.PushProperty("MessageId", id)) {
                var message = await db.Set<Outbox>().FirstOrDefaultAsync(x => x.MessageId == id);
                if (message == null) {
                    return NotFound();
                }

                message.Status = OutboxStatus.Queued;
                var attempts = configuration.Overrides?.FirstOrDefault(x => x.EventType == message.EventType)?.MaximumPublishCount ?? configuration.MaximumPublishCount;
                message.RemainingAttempts = attempts;
                await db.SaveChangesAsync();

                return Ok(message);
            }
        }

        /// <summary>
        /// Delete message
        /// </summary>
        /// <param name="resourceId"></param>
        [HttpDelete("{id}")]
        [Authorize("DeleteOutboxMessage")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> CancelOrderAsync(string id) {
            using (logger.PushProperty("MessageId", id)) {
                var message = await db.Set<Outbox>().FirstOrDefaultAsync(x => x.MessageId == id);
                if (message == null) {
                    return NotFound();
                }

                db.Remove(message);
                await db.SaveChangesAsync();

                return StatusCode((int)HttpStatusCode.NoContent);
            }
        }
    }
}
