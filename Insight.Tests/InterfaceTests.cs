﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Insight.Database;
using System.Data.Common;
using System.Data;
using System.Threading;

// since the interface and types are private, we have to let insight have access to them
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Insight.Database")]

namespace Insight.Tests
{
	#region Test Interfaces
	interface ITest1 : IDbConnection, IDbTransaction
	{
		// all execution modes
		void ExecuteSomething();
		void ExecuteSomethingWithParameters(int p, string q);
		int ExecuteSomethingScalar(int p);
		TestDataClasses.ParentTestData SingleObject();
		IList<int> QueryValue(int p);
		IList<TestDataClasses.ParentTestData> QueryObject();
		int ObjectAsParameter(TestDataClasses.ParentTestData data);
		IList<int> ObjectListAsParameter(IEnumerable<TestDataClasses.ParentTestData> data);
		Results<TestDataClasses.ParentTestData, int> QueryResults(int p);

		// same procs, asynchronously
		Task ExecuteSomethingAsync();
		Task ExecuteSomethingWithParametersAsync(int p, string q);
		Task<int> ExecuteSomethingScalarAsync(int p);
		Task<TestDataClasses.ParentTestData> SingleObjectAsync();
		Task<IList<int>> QueryValueAsync(int p);
		Task<IList<TestDataClasses.ParentTestData>> QueryObjectAsync();
		Task<int> ObjectAsParameterAsync(TestDataClasses.ParentTestData data);
		Task<IList<int>> ObjectListAsParameterAsync(IEnumerable<TestDataClasses.ParentTestData> data);
		Task<Results<TestDataClasses.ParentTestData, int>> QueryResultsAsync(int p);

		// inline overrides
		[Sql("SELECT X=CONVERT(varchar(128), @p)")]
		string InlineSql(int p);
		[Sql("ExecuteSomethingScalar", CommandType.StoredProcedure)]
		int InlineSqlProcOverride(int p);

		// graph override
		[DefaultGraph(typeof(Graph<TestDataClasses.ParentTestData, TestDataClasses.TestData>))]
		[Sql("QueryObject", CommandType.StoredProcedure)]
		TestDataClasses.ParentTestData QueryWithGraph();

		// graph override for results
		[DefaultGraph(
			typeof(Graph<TestDataClasses.ParentTestData, TestDataClasses.TestData>),
			null)]
		[Sql("QueryResults", CommandType.StoredProcedure)]
		Results<TestDataClasses.ParentTestData, int> QueryResultsWithGraph(int p);
	}

	interface ITestWithSpecialParameters
	{
		IList<TestDataClasses.ParentTestData> QueryObject(Type withGraph);
		Task<Results<TestDataClasses.ParentTestData, int>> QueryResultsAsync(Type[] withGraphs, int p);
		void ExecuteSomething(int? commandTimeout);
		Task ExecuteSomethingAsync(CancellationToken? cancellationToken);

		[Sql("ExecuteSomething")]
		void ExecuteSomethingWithTransaction(IDbTransaction transaction);
	}

	interface ITestInsertUpdate
	{
		void InsertTestData(TestDataClasses.TestData data);
		void UpdateTestData(TestDataClasses.TestData data);
		void InsertMultipleTestData(IEnumerable<TestDataClasses.TestData> data);
		void UpdateMultipleTestData(IEnumerable<TestDataClasses.TestData> data);

		Task InsertTestDataAsync(TestDataClasses.TestData data);
		Task UpdateTestDataAsync(TestDataClasses.TestData data);
		Task InsertMultipleTestDataAsync(IEnumerable<TestDataClasses.TestData> data);
		Task UpdateMultipleTestDataAsync(IEnumerable<TestDataClasses.TestData> data);
	}

	interface ITestOutputParameters
	{
		void ExecuteWithOutputParameter(out int p);
		int ExecuteScalarWithOutputParameter(out int p);
		IList<int> QueryWithOutputParameter(out int p);
		Results<TestDataClasses.ParentTestData, int> QueryResultsWithOutputParameter(out int p);
		void InsertWithOutputParameter(IEnumerable<TestDataClasses.TestData> data, out int p);
	}
	#endregion

	[TestFixture]
	public class InterfaceTests : BaseDbTest
	{
		#region Test Interface Is Generated
		[Test]
		public void InterfaceIsGenerated()
		{
			try
			{
				_connection.ExecuteSql("CREATE TYPE ObjectTable AS TABLE (ParentX [int])");

				using (var connection = _connectionStringBuilder.OpenWithTransaction())
				{
					// make sure that we can create an interface
					ITest1 i = connection.As<ITest1>();
					Assert.IsNotNull(i);

					// make sure that the wrapper is still a connection
					DbConnection c = i as DbConnection;
					Assert.IsNotNull(c);

					// create some procs to call
					connection.ExecuteSql("CREATE PROC ExecuteSomething AS SELECT NULL");
					connection.ExecuteSql("CREATE PROC ExecuteSomethingWithParameters @p int, @q [varchar](128) AS SELECT @p, @q");
					connection.ExecuteSql("CREATE PROC ExecuteSomethingScalar @p int AS SELECT @p");
					connection.ExecuteSql("CREATE PROC QueryValue @p int AS SELECT @p UNION ALL SELECT @p");
					connection.ExecuteSql("CREATE PROC QueryObject AS " + TestDataClasses.ParentTestData.Sql);
					connection.ExecuteSql("CREATE PROC SingleObject AS " + TestDataClasses.ParentTestData.Sql);
					connection.ExecuteSql("CREATE PROC ObjectAsParameter @ParentX [int] AS SELECT @ParentX");
					connection.ExecuteSql("CREATE PROC ObjectListAsParameter (@objects [ObjectTable] READONLY) AS SELECT ParentX FROM @objects");
					connection.ExecuteSql("CREATE PROC QueryResults @p int AS " + TestDataClasses.ParentTestData.Sql + " SELECT @p");
					
					// let's call us some methods
					i.ExecuteSomething();
					i.ExecuteSomethingWithParameters(5, "6");
					Assert.AreEqual(9, i.ExecuteSomethingScalar(9));
					i.SingleObject().Verify(false);
					Assert.AreEqual(2, i.QueryValue(9).Count());
					TestDataClasses.ParentTestData.Verify(i.QueryObject(), false);
					Assert.AreEqual(11, i.ObjectAsParameter(new TestDataClasses.ParentTestData() { ParentX = 11 }));
					Assert.AreEqual(11, i.ObjectListAsParameter(new[] { new TestDataClasses.ParentTestData() { ParentX = 11 } }).First());

					var results = i.QueryResults(7);
					TestDataClasses.ParentTestData.Verify(results.Set1, false);
					Assert.AreEqual(7, results.Set2.First());

					// let's call them asynchronously
					i.ExecuteSomethingAsync().Wait();
					i.ExecuteSomethingWithParametersAsync(5, "6").Wait();
					Assert.AreEqual(9, i.ExecuteSomethingScalarAsync(9).Result);
					i.SingleObjectAsync().Result.Verify(false);
					Assert.AreEqual(2, i.QueryValueAsync(9).Result.Count());
					TestDataClasses.ParentTestData.Verify(i.QueryObjectAsync().Result, false);
					Assert.AreEqual(11, i.ObjectAsParameterAsync(new TestDataClasses.ParentTestData() { ParentX = 11 }).Result);
					Assert.AreEqual(11, i.ObjectListAsParameterAsync(new[] { new TestDataClasses.ParentTestData() { ParentX = 11 } }).Result.First());

					results = i.QueryResultsAsync(7).Result;
					TestDataClasses.ParentTestData.Verify(results.Set1, false);
					Assert.AreEqual(7, results.Set2.First());

					// inline SQL!
					Assert.AreEqual("42", i.InlineSql(42));
					Assert.AreEqual(99, i.InlineSqlProcOverride(99));

					// graphs
					i.QueryWithGraph().Verify(true);
					i.QueryResultsWithGraph(87).Set1.First().Verify(true);
				}
			}
			finally
			{
				_connection.ExecuteSql("DROP TYPE ObjectTable");
			}
		}
		#endregion

		#region Test Interface Special Parameters
		[Test]
		public void InterfaceWithSpecialParametersIsGenerated()
		{
			using (var connection = _connectionStringBuilder.OpenWithTransaction())
			{
				// make sure that we can create an interface
				ITestWithSpecialParameters i = connection.As<ITestWithSpecialParameters>();
				Assert.IsNotNull(i);

				// create some procs to call
				connection.ExecuteSql("CREATE PROC QueryObject AS " + TestDataClasses.ParentTestData.Sql);
				connection.ExecuteSql("CREATE PROC QueryResults @p int AS " + TestDataClasses.ParentTestData.Sql + " SELECT @p");
				connection.ExecuteSql("CREATE PROC ExecuteSomething AS SELECT NULL");

				// let's call us some methods

				// commandTimeout
				i.ExecuteSomething(30);

				// withGraph
				TestDataClasses.ParentTestData.Verify(i.QueryObject(typeof(Graph<TestDataClasses.ParentTestData, TestDataClasses.TestData>)), true);

				// withGraphs
				Type[] graphs = new[] { typeof(Graph<TestDataClasses.ParentTestData, TestDataClasses.TestData>), null };
				var results = i.QueryResultsAsync(graphs, 7).Result;
				TestDataClasses.ParentTestData.Verify(results.Set1, true);
				Assert.AreEqual(7, results.Set2.First());

				// a cancelled cancellation token
				CancellationTokenSource cts = new CancellationTokenSource();
				cts.Cancel();
				Assert.Throws<AggregateException>(() => { i.ExecuteSomethingAsync(cts.Token).Wait(); });

				// override of the transaction
				// NOTE: if you use OpenWithTransaction, the transaction is propagated automatically, so you don't need to do this
				i.ExecuteSomethingWithTransaction(connection);
			}
		}
		#endregion

		#region Test Interface with Transaction
		[Test]
		public void ConnectionOpenedWithInterfaceAndTransaction()
		{
			using (var connection = _connectionStringBuilder.OpenWithTransactionAs<ITest1>())
			{
				connection.ExecuteSql("CREATE PROC ExecuteSomething AS SELECT NULL");
				connection.ExecuteSomething();
				connection.Rollback();
			}
		}
		#endregion

		#region Test Insert as TVP
		[Test]
		public void TestInsert()
		{
			try
			{
				_connection.ExecuteSql("CREATE TYPE InsertTestDataTVP AS TABLE (X [int], Z [int])");

				using (var connection = _connectionStringBuilder.OpenWithTransaction())
				{
					connection.ExecuteSql("CREATE TABLE InsertTestDataTable (X [int] identity (5, 1), Z [int])");
					connection.ExecuteSql("CREATE PROC InsertTestData @Z [int] AS INSERT INTO InsertTestDataTable (Z) OUTPUT inserted.X VALUES (@Z)");
					connection.ExecuteSql("CREATE PROC UpdateTestData @X [int], @Z [int] AS UPDATE InsertTestDataTable SET Z=@Z WHERE X=@X SELECT X=0");
					connection.ExecuteSql("CREATE PROC InsertMultipleTestData @data [InsertTestDataTVP] READONLY AS INSERT INTO InsertTestDataTable (Z) OUTPUT inserted.X SELECT Z FROM @data");
					connection.ExecuteSql("CREATE PROC UpdateMultipleTestData @data [InsertTestDataTVP] READONLY AS UPDATE InsertTestDataTable SET Z=data.Z FROM @data data WHERE data.X = InsertTestDataTable.X SELECT X=0 FROM @data");

					var i = connection.As<ITestInsertUpdate>();

					{
						// single insert
						TestDataClasses.TestData data = new TestDataClasses.TestData() { Z = 4 };
						i.InsertTestData(data);
						Assert.AreEqual(5, data.X, "ID should be returned");

						// single update
						i.UpdateTestData(data);
						Assert.AreEqual(0, data.X, "ID should be reset");

						// multiple insert
						var list = new[]
						{
							new TestDataClasses.TestData() { Z = 5 },
							new TestDataClasses.TestData() { Z = 6 }
						};
						i.InsertMultipleTestData(list);
						Assert.AreEqual(6, list[0].X, "ID should be returned");
						Assert.AreEqual(7, list[1].X, "ID should be returned");

						// multiple update
						i.UpdateMultipleTestData(list);
						Assert.AreEqual(0, list[0].X, "ID should be reset");
						Assert.AreEqual(0, list[1].X, "ID should be reset");
					}

					{
						// single insert
						TestDataClasses.TestData data = new TestDataClasses.TestData() { Z = 4 };
						i.InsertTestDataAsync(data).Wait();
						Assert.AreEqual(8, data.X, "ID should be returned");

						// single update
						i.UpdateTestDataAsync(data).Wait();
						Assert.AreEqual(0, data.X, "ID should be reset");

						// multiple insert
						var list = new[]
						{
							new TestDataClasses.TestData() { Z = 5 },
							new TestDataClasses.TestData() { Z = 6 }
						};
						i.InsertMultipleTestDataAsync(list).Wait();
						Assert.AreEqual(9, list[0].X, "ID should be returned");
						Assert.AreEqual(10, list[1].X, "ID should be returned");

						// multiple update
						i.UpdateMultipleTestDataAsync(list).Wait();
						Assert.AreEqual(0, list[0].X, "ID should be reset");
						Assert.AreEqual(0, list[1].X, "ID should be reset");
					}
				}
			}
			finally
			{
				_connection.ExecuteSql("DROP TYPE InsertTestDataTVP");
			}
		}
		#endregion

		#region Test Output Parameters
		[Test]
		public void TestOutputParameters()
		{
			try
			{
				_connection.ExecuteSql("CREATE TYPE InsertTestDataTVP AS TABLE (X [int], Z [int])");

				using (var connection = _connectionStringBuilder.OpenWithTransaction())
				{
					connection.ExecuteSql("CREATE TABLE InsertTestDataTable (X [int] identity (5, 1), Z [int])");
					connection.ExecuteSql("CREATE PROC ExecuteWithOutputParameter @p [int] = NULL OUTPUT AS SELECT @p=@p+1");
					connection.ExecuteSql("CREATE PROC ExecuteScalarWithOutputParameter @p [int] = NULL OUTPUT AS SELECT @p=@p+1 SELECT 7");
					connection.ExecuteSql("CREATE PROC QueryWithOutputParameter @p [int] = NULL OUTPUT AS SELECT @p=@p+1 SELECT 5");
					connection.ExecuteSql("CREATE PROC QueryResultsWithOutputParameter @p int OUTPUT AS " + TestDataClasses.ParentTestData.Sql + " SELECT @p=@p+1 SELECT @p");
					connection.ExecuteSql("CREATE PROC InsertWithOutputParameter @data [InsertTestDataTVP] READONLY, @p [int] OUTPUT AS INSERT INTO InsertTestDataTable (Z) OUTPUT inserted.X SELECT z FROM @data OUTPUT SELECT @p=@p+1");

					var i = connection.As<ITestOutputParameters>();

					// test execute with output parameter
					int original = 2;
					int p = original;
					i.ExecuteWithOutputParameter(out p);
					Assert.AreEqual(original + 1, p);

					// test executescalar with output parameter
					p = original;
					var scalar = i.ExecuteScalarWithOutputParameter(out p);
					Assert.AreEqual(original + 1, p);
					Assert.AreEqual(7, scalar);

					// test query with output parameters
					p = original;
					var results = i.QueryWithOutputParameter(out p);
					Assert.AreEqual(original + 1, p);
					Assert.AreEqual(1, results.Count);
					Assert.AreEqual(5, results[0]);

					// test query results with output parameters
					p = original;
					i.QueryResultsWithOutputParameter(out p);
					Assert.AreEqual(original + 1, p);

					// test insert with output parameters
					TestDataClasses.TestData data = new TestDataClasses.TestData() { Z = 4 };
					var list = new List<TestDataClasses.TestData>() { data };
					p = original;
					i.InsertWithOutputParameter(list, out p);
					Assert.AreEqual(original + 1, p);
				}
			}
			finally
			{
				_connection.ExecuteSql("DROP TYPE InsertTestDataTVP");
			}
		}
		#endregion
	}
}
