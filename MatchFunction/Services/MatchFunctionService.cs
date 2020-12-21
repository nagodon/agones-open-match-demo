using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc;
using OpenMatch;
using System.Collections.Generic;
using System.Linq;
using System.Net.Cache;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using System.Threading;
using System;
using grpc = global::Grpc.Core;
using static OpenMatch.MatchFunction;
using static OpenMatch.QueryService;

namespace MatchFunction.Services
{
    public struct PoolResult
    {
        public string Error { get; set; }
        public List<Ticket> Tickets { get; set; }
        public string Name { get; set; }
    }

    public class MatchFunctionService : MatchFunctionBase
    {
        private const string QueryUrl = "http://om-query.open-match.svc.cluster.local:50503";
        private const string MatchFunctionName = "multi";
        private const int MatchingCount = 2;
        private const int ApiTimeout = 5;

        public async override Task Run(RunRequest request, grpc::IServerStreamWriter<global::OpenMatch.RunResponse> responseStream, grpc::ServerCallContext context)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            var poolMap = await GetPoolMapAsync(request);

            if (0 == poolMap.Count) {
                return;
            }

            var matches = MakeMatches(request, poolMap);
            foreach (var match in matches) {
                await responseStream.WriteAsync(new RunResponse { Proposal = match });
            }
        }

        private async Task<Dictionary<string, PoolResult>> GetPoolMapAsync(RunRequest request)
        {
            using var channel = GrpcChannel.ForAddress(QueryUrl);
            var client = new QueryServiceClient(channel);
            var poolMap = new Dictionary<string, PoolResult>();

            foreach (var pool in request.Profile.Pools)
            {
                var queryTicketRequest = new QueryTicketsRequest() { Pool = pool };
                using var stream = client.QueryTickets(queryTicketRequest);
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ApiTimeout));
                var poolTickets = new List<Ticket>();

                while (await stream.ResponseStream.MoveNext(cts.Token))
                {
                    poolTickets.AddRange(stream.ResponseStream.Current.Tickets);
                }

                if (0 == poolTickets.Count) {
                    continue;
                }

                var result = new PoolResult();
                result.Name = pool.Name;
                result.Tickets = poolTickets.ToList();

                poolMap.Add(pool.Name, result);
            }

            return poolMap;
        }

        private List<Match> MakeMatches(RunRequest request, Dictionary<string, PoolResult> poolMap)
        {
            var matches = new List<Match>();
            while (true) {
                var insufficientTickets = false;
                var matchTickets = new List<Ticket>();

                foreach (var key in poolMap.Keys) {
                    var pool = poolMap[key];
                    var tickets = pool.Tickets.ToList();
                    if (tickets.Count < MatchingCount) {
                        insufficientTickets = true;
                        break;
                    }

                    for (var i = 0; i < MatchingCount; i++) {
                        var ticket = tickets[i];
                        matchTickets.Add(ticket);
                        pool.Tickets.Remove(ticket);
                    }
                }

                if (insufficientTickets) {
                    break;
                }

                var match = new Match();
                match.MatchId = $"profile-{request.Profile.Name}-{Guid.NewGuid().ToString()}";
                match.MatchProfile = request.Profile.Name;
                match.MatchFunction = MatchFunctionName;
                match.Tickets.AddRange(matchTickets);
                matches.Add(match);
            }

            return matches;
        }
    }
}
