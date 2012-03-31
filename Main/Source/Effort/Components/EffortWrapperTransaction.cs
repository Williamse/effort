﻿#region License

// Copyright (c) 2011 Effort Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EFProviderWrapperToolkit;
using System.Data.Common;

namespace Effort.Components
{
    public class EffortWrapperTransaction : DbTransaction
    {
        private EffortWrapperConnection connection;
        private System.Data.IsolationLevel isolationLevel;

        private System.Transactions.CommittableTransaction transaction;

        public EffortWrapperTransaction(EffortWrapperConnection connection, System.Data.IsolationLevel isolationLevel) 
        {
            if (System.Transactions.Transaction.Current != null)
            {
                throw new InvalidOperationException("Ambient transaction is already set");
            }

            this.connection = connection;
            this.isolationLevel = isolationLevel;

            // Initialize new ambient transaction
            System.Transactions.TransactionOptions options = new System.Transactions.TransactionOptions();
            options.IsolationLevel = TranslateIsolationLevel(isolationLevel);
            options.Timeout = new TimeSpan(0, 0, connection.ConnectionTimeout);

            this.transaction = new System.Transactions.CommittableTransaction(options);

            // If the mode of the connection is accelerator, the underlying connection has to be enlisted to this transaction
            if (connection.ProviderMode == ProviderModes.DatabaseAccelerator)
            {
                // Open the physical database connection, if it has not been yet
                if (connection.WrappedConnection.State != System.Data.ConnectionState.Open)
                {
                    connection.WrappedConnection.Open();
                }

                // Enlist the physical database connection to the ambient transaction
                connection.WrappedConnection.EnlistTransaction(transaction);
            }

            if (NMemory.Transactions.Transaction.Current != null)
            {
                throw new InvalidOperationException();
            }

            // Create an NMemory transaction
            NMemory.Transactions.Transaction.EnlistOnExternal(this.transaction);
        }

        public override void Commit()
        {
            transaction.Commit();
        }

        protected override DbConnection DbConnection
        {
            get { return this.connection; }
        }

        public override System.Data.IsolationLevel IsolationLevel
        {
            get { return this.isolationLevel; }
        }

        public override void Rollback()
        {
            transaction.Rollback();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.transaction.Dispose();
                // this.mmdbTransaction.Dispose();
            }

            base.Dispose(disposing);
        }

        private System.Transactions.IsolationLevel TranslateIsolationLevel(System.Data.IsolationLevel isolationLevel)
        {
            switch (isolationLevel)
            {
                case System.Data.IsolationLevel.Chaos:
                    return System.Transactions.IsolationLevel.Chaos;
                case System.Data.IsolationLevel.ReadCommitted:
                    return System.Transactions.IsolationLevel.ReadCommitted;
                case System.Data.IsolationLevel.ReadUncommitted:
                    return System.Transactions.IsolationLevel.ReadUncommitted;
                case System.Data.IsolationLevel.RepeatableRead:
                    return System.Transactions.IsolationLevel.RepeatableRead;
                case System.Data.IsolationLevel.Serializable:
                    return System.Transactions.IsolationLevel.Serializable;
                case System.Data.IsolationLevel.Snapshot:
                    return System.Transactions.IsolationLevel.Snapshot;
                case System.Data.IsolationLevel.Unspecified:
                    return System.Transactions.IsolationLevel.Unspecified;
                default:
                    throw new ArgumentException("Unknown isolation level.", "isolationLevel");
            }
        }
    }
}
