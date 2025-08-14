using System.Reflection;
using System.Runtime.Serialization;
using Codex.ObjectModel;
using MessagePack;
using MessagePack.Formatters;

namespace Codex.Utilities.Serialization;
public static partial class MessagePacker
{
    //public class ContractResolver : IFormatterResolver
    //{
    //    public IMessagePackFormatter<T> GetFormatter<T>(ObjectStage stage)
    //    {
    //        var type = typeof(T);
    //        if (type.GetCustomAttribute<DataContractAttribute>() != null)
    //        {

    //        }
    //    }

    //    public IMessagePackFormatter<T> GetContractFormatter<T>(ObjectStage stage)
    //    {
    //        var type = typeof(T).GetTypeInfo();

    //    }

    //    public IReadOnlyDictionary<string, MemberInfo> GetMembers()
    //    {

    //    }

    //    private class ContractDescriptor<T> : DescriptorBase<T, T>
    //    {
    //        public ContractDescriptor() 
    //            : base(-1)
    //        {
    //        }
    //    }
    //}
}

