using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
    internal class CalendarConfigFunctions : RequestHandler
    {

        private ILogger<CalendarConfigFunctions> _logger;


		public CalendarConfigFunctions(ILogger<CalendarConfigFunctions> logger)
        {
            _logger = logger;
        }

        [Allow(Role.Admin)]
        [Function("CreateCalendar")]
        public static async Task<IActionResult> RunCreateCalendar(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "calendar/create/")] HttpRequest req)
        {
            Calendar calendar = JsonConvert.DeserializeObject<Calendar>(
                await new StreamReader(req.Body).ReadToEndAsync());

            return Ok(
                () => (new DatabaseRepo<Calendar>()).Create(calendar),
                (p) => new
                {
                    calendar = p
                });
        }


        [Allow(Role.Admin)]
        [Function("DeleteCalendar")]
        public static async Task<IActionResult> RunDeleteCalendar(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "calendar/{id}/")] HttpRequest req, string id)
        {
            return Ok(
                () => (new DatabaseRepo<Calendar>()).Delete(id),
                (p) => new
                {
                    deletedCalendar = p
                });
        }


        [Allow(Role.Admin)]
        [Function("GetCalendar")]
        public static async Task<IActionResult> RunGetCalendar(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "calendar/{id}/")] HttpRequest req, string id)
        {
            return Ok(
                () => (new DatabaseRepo<Calendar>()).Get(id),
                (p) => new
                {
                    calendar = p
                });
        }


        [Allow(Role.Admin)]
        [Function("UpdateCalendar")]
        public static async Task<IActionResult> RunUpdateCalendar(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "calendar/{id}/update/")] HttpRequest req, string id)
        {
            Calendar calendar = JsonConvert.DeserializeObject<Calendar>(
                await new StreamReader(req.Body).ReadToEndAsync());
            calendar.Id = id;

            return Ok(
                () => (new DatabaseRepo<Calendar>()).Update(calendar),
                (p) => new
                {
                    oldCalendar = ((Tuple<Calendar, Calendar>)p).Item1,
                    newCalendar = ((Tuple<Calendar, Calendar>)p).Item2,
                });
        }
    }
}
