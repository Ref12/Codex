using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
using System.Text.Json.Serialization;

namespace Codex.ObjectModel
{
    [GeneratorExclude]
    public interface IEntity<TImpl> : ISerializableEntity, ICreate<TImpl>
    {
        static abstract ISingletonDescriptor Descriptor { get; }

    }

    public interface ICreate<TImpl>
    { 
        static abstract TImpl Create();
    }

    public interface IBaseEntity<TImpl, in TBase> : IEntity<TImpl>
        where TImpl : TBase
    { 
        TImpl CreateClone(TBase self, bool shallow = false);
    }

    public interface IEntity<TImpl, TBase> : IBaseEntity<TImpl, TBase>
        where TImpl : TBase, IEntity<TImpl, TBase>
    {
        static abstract DescriptorBase<TImpl, TBase> GetDescriptor();

        static virtual TImpl Create(TBase value, bool shallow = false)
        {
            var result = TImpl.Create();
            var descriptor = TImpl.GetDescriptor();
            descriptor.CopyFrom(result, value, shallow);
            return result;
        }

        TImpl IBaseEntity<TImpl, TBase>.CreateClone(TBase self, bool shallow)
        {
            return TImpl.Create(self, shallow: shallow);
        }
    }

    public interface IEntity<TImpl, TBase, TDescriptor> : IEntity<TImpl, TBase>
        where TImpl : TBase, IEntity<TImpl, TBase, TDescriptor>
        where TDescriptor : SingletonDescriptorBase<TImpl, TBase, TDescriptor>, ISingletonDescriptor<TDescriptor>, ICreate<TDescriptor>
    {
        static DescriptorBase<TImpl, TBase> IEntity<TImpl, TBase>.GetDescriptor() => TDescriptor.Instance;

        static ISingletonDescriptor IEntity<TImpl>.Descriptor => TDescriptor.Instance;
    }

    public class EntityBase : ISerializableEntity, IJsonOnSerializing, IJsonOnDeserialized
    {
        public bool? IsRequired { get; set; }

        public ISearchEntity RootEntity { get; set; }

        public EntityBase()
        {
            Initialize();
        }

        /// <summary>
        /// Mark the entity as required so it is added to entities to be indexed
        /// </summary>
        public void MarkRequired()
        {
            IsRequired = true;
        }

        protected virtual void Initialize()
        {
        }

        protected virtual void OnSerializingCore()
        {
        }

        protected virtual void OnDeserializedCore()
        {
        }

        void ISerializableEntity.OnSerializing()
        {
            OnSerializingCore();
        }

        void ISerializableEntity.OnDeserialized()
        {
            OnDeserializedCore();
        }

        void IJsonOnSerializing.OnSerializing()
        {
            OnSerializingCore();
        }

        void IJsonOnDeserialized.OnDeserialized()
        {
            OnDeserializedCore();
        }
    }

    [Placeholder.Todo("Ideally we wouldn't have logic which is only trigger as a side of (de)serialization.")]
    public interface ISerializableEntity
    {
        void OnDeserialized();

        void OnSerializing();
    }
}
