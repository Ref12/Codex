// --------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation.  All rights reserved.
//
// --------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace Codex.Utilities
{
    /// <summary>
    /// Allows an IDisposable unsetting async local
    /// </summary>
    [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
    public struct AsyncLocalScope<T> : IDisposable
    {
        private readonly AsyncLocal<T> _local;
        private readonly T _exitValue;

        public AsyncLocalScope(AsyncLocal<T> local, T exitValue = default)
        {
            this._local = local;
            _exitValue = exitValue;
        }

        /// <summary>
        /// IDispoaable.Dispose()
        /// </summary>
        public void Dispose()
        {
            if (_local == null) return;
            _local.Value = _exitValue;
        }

        /// <summary>
        /// Whether this async local scope is valid (and not the default value)
        /// </summary>
        public bool IsValid
        {
            get { return _local != null; }
        }
    }
}