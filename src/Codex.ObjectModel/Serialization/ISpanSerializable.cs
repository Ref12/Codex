// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


#nullable enable

namespace Codex.Utilities.Serialization
{
    public interface ISpanSerializable<TSelf>
        where TSelf : ISpanSerializable<TSelf>
    {
        static abstract TSelf Deserialize(ref SpanReader reader);

        void Serialize(ref SpanWriter writer);
    }
}
