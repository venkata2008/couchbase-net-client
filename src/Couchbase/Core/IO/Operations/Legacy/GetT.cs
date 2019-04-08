using System;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Core.IO.Operations.Legacy
{
    internal class GetT<T> : MutationOperationBase<T>
    {
        public override void WriteExtras(OperationBuilder builder)
        {
            Span<byte> extras = stackalloc byte[4];
            Converter.FromUInt32(Expires, extras);
            builder.Write(extras);
        }

        public override byte[] CreateBody()
        {
            return Array.Empty<byte>();
        }

        public override OpCode OpCode => OpCode.GAT;

        public override IOperation Clone()
        {
            var cloned = new GetT<T>
            {
                Key = Key,
                Content = Content,
                Transcoder = Transcoder,
                VBucketId = VBucketId,
                Opaque = Opaque,
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                Expires = Expires,
                LastConfigRevisionTried = LastConfigRevisionTried,
                BucketName = BucketName,
                ErrorCode = ErrorCode
            };
            return cloned;
        }

        public override bool RequiresKey => true;
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
