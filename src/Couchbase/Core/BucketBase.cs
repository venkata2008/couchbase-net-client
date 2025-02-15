using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Core.Logging;
using Couchbase.Core.Sharding;
using Couchbase.Management;
using Couchbase.Services.Views;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core
{
    internal abstract class BucketBase : IBucket
    {
        internal const string DefaultScope = "_default";
        private static readonly ILogger Log = LogManager.CreateLogger<BucketBase>();
        protected readonly ConcurrentDictionary<IPEndPoint, ClusterNode> BucketNodes = new ConcurrentDictionary<IPEndPoint, ClusterNode>();
        protected readonly ConcurrentDictionary<string, IScope> Scopes = new ConcurrentDictionary<string, IScope>();

        protected BucketConfig BucketConfig;
        protected Manifest Manifest;
        protected IKeyMapper KeyMapper;
        protected Couchbase.Configuration Configuration;
        protected bool SupportsCollections;
        protected ConfigContext CouchbaseContext;
        protected bool Disposed;

        public BucketType BucketType { get; protected set; }

        public string Name { get; protected set; }

        public abstract Task<IScope> this[string name] { get; }

        public Task<ICollection> DefaultCollectionAsync()
        {
            return Task.FromResult(Scopes[DefaultScope][CouchbaseCollection.DefaultCollection]);
        }

        public abstract Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName,
            ViewOptions options = default);

        public abstract IViewManager ViewIndexes { get; }

        protected abstract void LoadManifest();

        protected async Task LoadClusterMap(IEnumerable<NodeAdapter> adapters)
        {
            foreach (var nodeAdapter in adapters)
            {
                var endPoint = nodeAdapter.GetIpEndPoint();
                if (BucketNodes.TryGetValue(endPoint, out ClusterNode bootstrapNode))
                {
                    bootstrapNode.NodesAdapter = nodeAdapter;
                    bootstrapNode.BuildServiceUris();
                    continue; //bootstrap node is skipped because it already went through these steps
                }

                var connection = endPoint.GetConnection();
                await connection.Authenticate(Configuration, Name).ConfigureAwait(false);
                await connection.SelectBucket(Name).ConfigureAwait(false);

                //one error map per node
                var errorMap = await connection.GetErrorMap().ConfigureAwait(false);
                var supportedFeatures = await connection.Hello().ConfigureAwait(false);

                var clusterNode = new ClusterNode
                {
                    Connection = connection,
                    ErrorMap = errorMap,
                    EndPoint = endPoint,
                    ServerFeatures = supportedFeatures,
                    Configuration = Configuration,
                    NodesAdapter = nodeAdapter
                };
                clusterNode.BuildServiceUris();
                SupportsCollections = clusterNode.Supports(ServerFeatures.Collections);
                BucketNodes.AddOrUpdate(endPoint, clusterNode, (ep, node) => clusterNode);
                Configuration.GlobalNodes.Add(clusterNode);
            }
        }

        protected void Prune(BucketConfig newConfig)
        {
            var removed = BucketNodes.Where(x =>
                !newConfig.NodesExt.Any(y => x.Key.Equals(y.GetIpEndPoint(Configuration))));

            foreach (var valuePair in removed)
            {
                if (!BucketNodes.TryRemove(valuePair.Key, out var clusterNode)) continue;
                if (Configuration.GlobalNodes.TryTake(out clusterNode))
                {
                    clusterNode.Dispose();
                }
            }
        }

        protected async Task CheckConnection(ClusterNode clusterNode)
        {
            //TODO temp fix for recreating dead connections - in future use CP to manage them
            var connection = clusterNode.Connection;
            if (connection.IsDead)
            {
                //recreate the connection its been closed and disposed
                connection = clusterNode.EndPoint.GetConnection();
                clusterNode.ServerFeatures = await connection.Hello().ConfigureAwait(false);
                clusterNode.ErrorMap = await connection.GetErrorMap().ConfigureAwait(false);
                await connection.Authenticate(Configuration, Name).ConfigureAwait(false);
                await connection.SelectBucket(Name).ConfigureAwait(false);
                clusterNode.Connection = connection;
            }
        }

        internal abstract Task Send(IOperation op, TaskCompletionSource<IMemoryOwner<byte>> tcs);
        internal abstract Task Bootstrap(params ClusterNode[] bootstrapNodes);
        internal abstract void ConfigUpdated(object sender, BucketConfigEventArgs e);

        public virtual void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            BucketNodes.Clear();

            if (Configuration.GlobalNodes.TryTake(out var clusterNode))
            {
                if (clusterNode.Owner == this)
                {
                    clusterNode.Dispose();
                }
            }
        }
    }
}
