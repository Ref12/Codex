using Lucene.Net.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Directory = Lucene.Net.Store.Directory;

namespace Codex.Lucene.Search
{
    public class ValidatingDirectory : BaseDirectory
    {
        public Directory Dir1 { get; init; }
        public Directory Dir2 { get; init; }

        public ValidatingDirectory(Directory dir1, Directory dir2)
        {
            Dir1 = dir1;
            Dir2 = dir2;
        }

        public override IndexOutput CreateOutput(string name, IOContext context)
        {
            return Validate(d => d.CreateOutput(name, context));
        }

        public override void DeleteFile(string name)
        {
            Validate(d => d.DeleteFile(name));
        }

        public override bool FileExists(string name)
        {
            return Validate(d => d.FileExists(name));
        }

        public override long FileLength(string name)
        {
            return Validate(d => d.FileLength(name));
        }

        public override string[] ListAll()
        {
            return Validate(d => d.ListAll(), SetEquals);
        }

        private bool SetEquals(string[] arg1, string[] arg2)
        {
            if (arg1.Length != arg2.Length)
            {
                return false;
            }

            var set1 = new HashSet<string>(arg1.Select(a => a.Replace("\\", "/")), StringComparer.OrdinalIgnoreCase);
            var set2 = new HashSet<string>(arg2.Select(a => a.Replace("\\", "/")), StringComparer.OrdinalIgnoreCase);

            return set1.IsSubsetOf(set2) && set2.IsSubsetOf(set1);
        }

        public override IndexInput OpenInput(string name, IOContext context)
        {
            return Validate(d => d.OpenInput(name, context), (r1, r2) => true, resultSelector: (r1, r2) => new ValidatingInput(name) { Dir1 = r1, Dir2 = r2 });
        }

        public override void Sync(ICollection<string> names)
        {
        }

        protected override void Dispose(bool disposing)
        {
        }

        public TResult Validate<TResult>(Func<Directory, TResult> func, Func<TResult, TResult, bool> equals = null, Func<TResult, TResult, TResult> resultSelector = null)
        {
            return Validator.Validate(Dir1, Dir2, func, equals, resultSelector);
        }

        public void Validate(Action<Directory> func)
        {
            Validator.Validate(Dir1, Dir2, d => { func(d); return false; });
        }
    }

    public class ValidatingInput : BufferedIndexInput
    {
        public IndexInput Dir1 { get; init; }
        public IndexInput Dir2 { get; init; }

        private bool IsClone;

        public ValidatingInput(string resourceDescription) 
            : base(resourceDescription)
        {
        }

        public override long Length
        {
            get
            {
                return Validate(d => d.Length);
            }
        }

        protected override void ReadInternal(byte[] b, int offset, int length)
        {
            Validate(d =>
            {
                d.ReadBytes(b, offset, length);
                return (b, offset, length);
            }, (a, b) => ReadEquals(a, b));
        }

        protected override void SeekInternal(long pos)
        {
            ValidateAction(d => d.Seek(pos));
        }

        //public override long GetFilePointer()
        //{
        //    return Validate(d => d.GetFilePointer());
        //}

        //public override byte ReadByte()
        //{
        //    return Validate(d => d.ReadByte());
        //}

        //public override void ReadBytes(byte[] b, int offset, int len)
        //{
        //    Validate(d =>
        //    {
        //        d.ReadBytes(b, offset, len);
        //        return (b, offset, len);
        //    }, (a, b) => ReadEquals(a, b));
        //}

        private bool ReadEquals((byte[] b, int offset, int len) r1, (byte[] b, int offset, int len) r2)
        {
            if (!r1.b.AsSpan(r1.offset, r1.len).SequenceEqual(r2.b.AsSpan(r2.offset, r2.len)))
            {
                return false;
            }

            return true;
        }

        //public override void Seek(long pos)
        //{
        //    ValidateAction(d => d.Seek(pos));
        //}

        public TResult Validate<TResult>(Func<IndexInput, TResult> func, Func<TResult, TResult, bool> equals = null, Func<TResult, TResult, TResult> resultSelector = null)
        {
            return Validator.Validate(Dir1, Dir2, func, equals, resultSelector);
        }

        public void ValidateAction(Action<IndexInput> func)
        {
            Validator.Validate(Dir1, Dir2, d => { func(d); return false; });
        }

        public override object Clone()
        {
            var clone = (ValidatingInput)base.Clone();
            clone.IsClone = true;
            return clone;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsClone)
            {
                ValidateAction(i => i.Dispose());
            }
        }
    }

    public record Validator<T>(T Instance1, T Instance2)
    {
        public TResult Validate<TResult>(Func<T, TResult> func, Func<TResult, TResult, bool> equals = null, Func<TResult, TResult, TResult> resultSelector = null)
        {
            return Validator.Validate(Instance1, Instance2, func, equals, resultSelector);
        }

        public void ValidateAction(Action<T> func)
        {
            Validator.Validate(Instance1, Instance2, d => { func(d); return false; });
        }
    }


    internal static class Validator
    {
        public static Validator<T> Create<T>(T t1, T t2)
        {
            return new Validator<T>(t1, t2);
        }

        public static TResult Validate<TInput, TResult>(TInput t1, TInput t2, Func<TInput, TResult> func, Func<TResult, TResult, bool> equals = null, Func<TResult, TResult, TResult> resultSelector = null)
        {
            equals ??= EqualityComparer<TResult>.Default.Equals;
            resultSelector ??= (r1, r2) => r1;

            var r1 = Run(t1, func);
            var r2 = Run(t2, func);

            if (!r1.Equals(r2, equals))
            {
                throw new Exception($"Results are not equal {r1.Value} != {r2.Value}");
            }

            if (r1.HasException)
            {
                throw r1.Exception;
            }

            return resultSelector(r1.Value, r2.Value);
        }

        public static Result<TResult> Run<TInput, TResult>(TInput t, Func<TInput, TResult> func)
        {
            try
            {
                var result = func(t);
                return new Result<TResult>() { Value = result };
            }
            catch (Exception ex)
            {
                return new Result<TResult>() { Exception = ex };
            }
        }

        public record struct Result<T>
        {
            public T Value { get; init; }
            public Exception Exception { get; init; }

            public bool HasException => Exception != null;

            public bool Equals(Result<T> other, Func<T, T, bool> equals)
            {
                if (HasException != other.HasException)
                {
                    return false;
                }

                if (HasException)
                {
                    return Exception.GetType() == other.Exception.GetType();
                }

                return equals(Value, other.Value);
            }
        }

    }
}
