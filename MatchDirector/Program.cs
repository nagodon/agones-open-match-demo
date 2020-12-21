using Allocation;
using Google.Api.Gax.ResourceNames;
using Google.Cloud.SecretManager.V1;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using MatchDirector.Domain.Models;
using Newtonsoft.Json;
using OpenMatch;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using static Allocation.AllocationService;
using static OpenMatch.BackendService;

namespace MatchDirector
{
    class Program
    {
        private const string ProjectId = "REPLACE_PROJECT_ID"
        private const string BackendUrl = "http://om-backend.open-match.svc.cluster.local:50505";
        private const string MatchFunctionUrl = "demo-matchfunction.demo.svc.cluster.local";
        private const int MatchFunctionPort = 50502;
        private const int TaskDealyMillisecondsDelay = 5000;
        private const int ApiTimeout = 5;

        public static async Task Main(string[] args)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            SecretManagerServiceClient client = SecretManagerServiceClient.Create();
            var json = client.AccessSecretVersion(new SecretVersionName(ProjectId, "agones-allocator-info", "1")).Payload.Data.ToStringUtf8();
            var agonesAllocatorInfo = JsonConvert.DeserializeObject<AgonesAllocatorInfo>(json);

            while (true)
            {
                try {
                    List<Match> matches = await WaitingMatchAsync();
                    foreach (var match in matches)
                    {
                        await Task.Run(async () =>
                        {
                            var allocationResponse = await AllocateGameServerAsync(agonesAllocatorInfo);
                            await AssignTicketAsync(allocationResponse, match);
                        });
                    }
                } catch (Exception e) {
                    Console.WriteLine(e);
                } finally {
                    await Task.Delay(TaskDealyMillisecondsDelay);
                }
            }
        }

        private static FetchMatchesRequest GetFetchMatchesRequest()
        {
            var guid = Guid.NewGuid().ToString();
            var pool = new Pool();
            pool.Name = $"pool-{guid}";
            // フロントエンドリクエスト時の文字列を指定
            pool.TagPresentFilters.Add(new TagPresentFilter() { Tag = GameMode.Multi.ToString() });

            var config = new FunctionConfig
            {
                Host = MatchFunctionUrl,
                Port = MatchFunctionPort,
                Type = FunctionConfig.Types.Type.Grpc
            };

            var profile = new MatchProfile();
            profile.Name = "multi-100";
            profile.Pools.Add(pool);

            return new FetchMatchesRequest
            {
                Config = config,
                Profile = profile
            };
        }

        private static async Task<List<Match>> WaitingMatchAsync()
        {
            using var backendChannel = GrpcChannel.ForAddress(BackendUrl);
            var backendClient = new BackendServiceClient(backendChannel);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ApiTimeout));
            using var stream = backendClient.FetchMatches(GetFetchMatchesRequest());
            var matches = new List<Match>();

            while (await stream.ResponseStream.MoveNext(cts.Token))
            {
                matches.Add(stream.ResponseStream.Current.Match);
            }

            return matches;
        }

        private static async Task<AllocationResponse> AllocateGameServerAsync(AgonesAllocatorInfo agonesAllocatorInfo)
        {
            var creds = new SslCredentials(agonesAllocatorInfo.RawTlsCert(), new KeyCertificatePair(agonesAllocatorInfo.RawClientCert(), agonesAllocatorInfo.RawClientKey()));
            var channel = new Channel($"{agonesAllocatorInfo.Ip}:443", creds);
            var client = new AllocationService.AllocationServiceClient(channel);
            var allocationResponse = await client.AllocateAsync(new AllocationRequest {
                Namespace = "default",
                MultiClusterSetting = new Allocation.MultiClusterSetting { Enabled = false }
            });

            return allocationResponse;
        }

        private static async Task AssignTicketAsync(AllocationResponse allocationResponse, Match match)
        {
            var assignmentGroup = new AssignmentGroup();
            assignmentGroup.Assignment = new Assignment
            {
                Connection = $"{allocationResponse.Address}:{allocationResponse.Ports[0].Port}"
            };
            foreach (var ticket in match.Tickets) {
                assignmentGroup.TicketIds.Add(ticket.Id);
            }

            var assignTicketsRequest = new AssignTicketsRequest();
            assignTicketsRequest.Assignments.Add(assignmentGroup);

            using var backendChannel = GrpcChannel.ForAddress(BackendUrl);
            var backendClient = new BackendServiceClient(backendChannel);

            await backendClient.AssignTicketsAsync(assignTicketsRequest);
        }
    }
}
