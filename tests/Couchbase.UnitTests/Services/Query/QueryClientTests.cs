using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DataMapping;
using Couchbase.Core.IO.Serializers;
using Couchbase.Services;
using Couchbase.Services.Query;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Moq;
using Moq.Protected;
using Xunit;

namespace Couchbase.UnitTests.Services.Query
{
    public class QueryClientTests
    {
        [Theory]
        [InlineData("query-badrequest-error-response-400.json", HttpStatusCode.BadRequest, typeof(QueryException))]
        [InlineData("query-n1ql-error-response-400.json", HttpStatusCode.BadRequest, typeof(QueryException))]
        [InlineData("query-notfound-response-404.json", HttpStatusCode.NotFound, typeof(QueryException))]
        [InlineData("query-service-error-response-503.json", HttpStatusCode.ServiceUnavailable, typeof(QueryException))]
        [InlineData("query-timeout-response-200.json", HttpStatusCode.OK, typeof(QueryException))]
        [InlineData("query-unsupported-error-405.json", HttpStatusCode.MethodNotAllowed, typeof(QueryException))]
        public async Task Test(string file, HttpStatusCode httpStatusCode, Type errorType)
        {
            using (var response = ResourceHelper.ReadResourceAsStream(@"Documents\Query\" + file))
            {
                var buffer = new byte[response.Length];
                response.Read(buffer, 0, buffer.Length);

                var handlerMock = new Mock<HttpMessageHandler>();
                handlerMock.Protected().Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()).ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = httpStatusCode,
                    Content = new ByteArrayContent(buffer)
                });

                var httpClient = new HttpClient(handlerMock.Object)
                {
                    BaseAddress = new Uri("http://localhost:8091")
                };
                var config = new Configuration().WithBucket("default").WithServers("http://localhost:8901");
                var clusterNode = new ClusterNode
                {
                    Configuration = config,
                    EndPoint = new Uri("http://localhost:8091").GetIpEndPoint(8091, false),
                    NodesAdapter = new NodeAdapter(new Node {Hostname = "127.0.0.1"},
                        new NodesExt {Hostname = "127.0.0.1", Services = new Couchbase.Core.Configuration.Server.Services
                        {
                            N1Ql = 8093
                        }}, new BucketConfig())
                };
                clusterNode.BuildServiceUris();

                config.GlobalNodes = new ConcurrentBag<ClusterNode> {clusterNode};

                var client = new QueryClient(httpClient, new JsonDataMapper(new DefaultSerializer()), config);

                try
                {
                    await client.QueryAsync<DynamicAttribute>("SELECT * FROM `default`", new QueryOptions());
                }
                catch (Exception e)
                {
                    Assert.True(e.GetType() == errorType);
                }
            }
        }

        [Fact]
        public void EnhancedPreparedStatements_defaults_to_false()
        {
            var client = new QueryClient(new Configuration());
            Assert.False(client.EnhancedPreparedStatementsEnabled);
        }

        [Fact]
        public void EnhancedPreparedStatements_is_set_to_true_if_enabled_in_cluster_caps()
        {
            var client = new QueryClient(new Configuration());
            Assert.False(client.EnhancedPreparedStatementsEnabled);

            var clusterCapabilities = new ClusterCapabilities();
            clusterCapabilities.Capabilities = new Dictionary<string, IEnumerable<string>>
            {
                {
                    ServiceType.Query.GetDescription(),
                    new List<string> {ClusterCapabilityFeatures.EnhancedPreparedStatements.GetDescription()}
                }
            };

            client.UpdateClusterCapabilities(clusterCapabilities);
            Assert.True(client.EnhancedPreparedStatementsEnabled);
        }
    }
}
