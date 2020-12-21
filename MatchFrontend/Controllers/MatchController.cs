using Grpc.Net.Client;
using MatchFrontend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenMatch;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using static OpenMatch.FrontendService;

namespace MatchFrontend.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MatchController : ControllerBase
    {
        private const string FrontendUrl = "http://om-frontend.open-match.svc.cluster.local:50504";

        public MatchController()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        }

        [HttpPost]
        [Route("request")]
        public async Task<ActionResult<MatchResponse>> MatchRequest(MatchRequest request)
        {
            var matchResponse = new MatchResponse();

            try
            {
                using var channel = GrpcChannel.ForAddress(FrontendUrl);
                var client =  new FrontendServiceClient(channel);

                var searchFields = new SearchFields();
                searchFields.Tags.Add(request.GameMode.ToString());
                var ticket = new Ticket();
                ticket.SearchFields = searchFields;

                CreateTicketRequest createTicketRequest = new CreateTicketRequest();
                createTicketRequest.Ticket = ticket;

                var response = await client.CreateTicketAsync(createTicketRequest);
                matchResponse.TicketId = response.Id;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return matchResponse;
        }

        [HttpPost]
        [Route("polling")]
        public async Task<ActionResult<PollingResponse>> MatchPolling(PollingRequest request)
        {
            var pollingResponse = new PollingResponse();

            try
            {
                using var channel = GrpcChannel.ForAddress(FrontendUrl);
                var client =  new FrontendServiceClient(channel);

                var registeredTicket = await client.GetTicketAsync(new GetTicketRequest { TicketId = request.TicketId });
                pollingResponse.TicketId = request.TicketId;

                if (registeredTicket.Assignment == null) {
                    return pollingResponse;
                }

                pollingResponse.Connection = registeredTicket.Assignment.Connection;

                await client.DeleteTicketAsync(new DeleteTicketRequest { TicketId = request.TicketId });
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return pollingResponse;
        }
    }
}
