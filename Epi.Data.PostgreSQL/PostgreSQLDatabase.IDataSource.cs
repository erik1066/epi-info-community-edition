﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EpiInfo.Plugin;

namespace Epi.Data.PostgreSQL
{
    public partial class PostgreSQLDatabase : IDataSource 
    {
        /// <summary>
        /// executes sql against the datasource and will return a datatablereader of the results
        /// </summary>
        /// <param name="pSQL"></param>
        /// <returns></returns>
        public System.Data.IDataReader GetDataTableReader(string pSQL)
        {
            System.Data.IDataReader result = null;
            try
            {
                result = (System.Data.IDataReader)this.ExecuteReader(this.CreateQuery(pSQL));
            }
            catch (Exception e)
            {
                Logger.Log("Error PostgreSQLDatabase.IDataSource.GetDataTableReader:\n" + e.ToString());
            }
            return result;
        }


        /// <summary>
        /// Executes sql against the datasource will return false if sql statement fails
        /// </summary>
        /// <param name="pSQL"></param>
        /// <returns></returns>
        public bool ExecuteSQL(string pSQL)
        {
            bool result = false;
            try
            {
                this.ExecuteNonQuery(this.CreateQuery(pSQL));
                result = true;
            }
            catch (Exception e)
            {
                Logger.Log("Error PostgreSQLDatabase.IDataSource.Execute:\n" + e.ToString());
            }
            return result;
        }


        
        /// <summary>
        /// executes sql against the datasource and will return a single result object
        /// </summary>
        /// <param name="pSQL"></param>
        /// <returns></returns>
        public object GetScalar(string pSQL)
        {
            object result = null;
            
            try
            {
                System.Data.IDataReader ReaderResult = (System.Data.IDataReader)this.ExecuteReader(this.CreateQuery(pSQL));
                while (ReaderResult.Read())
                {
                    result = ReaderResult[0];
                    break;
                }
            }
            catch (Exception e)
            {
                Logger.Log("Error PostgreSQLDatabase.IDataSource.GetDataTableReader:\n" + e.ToString());
            }
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pFileString"></param>
        /// <param name="pTableName"></param>
        /// <param name="pIsConnectionString"></param>
        /// <returns></returns>
        public override bool CheckDatabaseTableExistance(string pFileString, string pTableName, bool pIsConnectionString = false)
        {
            System.Data.Common.DbConnection Conn = null;
            //System.Data.Common.DbDataAdapter Adapter = null;
            System.Data.DataTable DataTable = new System.Data.DataTable();
            bool result = false;

            string connString = pFileString;

            string[] restrictions = new string[] { null, null, pTableName };

            if (DataSource != null)
            {
                IDbDriver driver = this;
                driver.ConnectionString = connString;
                Conn = (System.Data.Common.DbConnection)driver.GetConnection();
                try
                {
                    Conn.Open();

                    DataTable = Conn.GetSchema("Tables", restrictions);

                    if (DataTable.Rows.Count == 0)
                    {
                        //Table does not exist
                        result = false;
                    }

                    else
                    {
                        //Table exists
                        result = true;
                    }
                }
                catch (Exception ex)
                {
                    // do nothing
                }
                finally
                {
                    Conn.Close();
                }
            }

            return result;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pFileString"></param>
        /// <returns></returns>
        public override bool CreateDataBase(string pFileString)
        {
            throw new NotImplementedException("Method NOT implemented");
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pFileString"></param>
        /// <param name="pTableName"></param>
        /// <param name="pIsConnectionString"></param>
        /// <returns></returns>
        public override bool CheckDatabaseExistance(string pFileString, string pTableName, bool pIsConnectionString = false)
        {
            bool result = false;

            System.Data.Common.DbConnection Conn = null;

            string connString = pFileString;

            string[] restrictions = new string[] { null, null, pTableName };

            if (DataSource != null)
            {
                IDbDriver driver = this;
                driver.ConnectionString = connString;
                Conn = (System.Data.Common.DbConnection)driver.GetConnection();
                try
                {
                    Conn.Open();
                    
                    System.Data.Common.DbCommand cmd = Conn.CreateCommand();
                    string sql = "SELECT 1 FROM pg_database WHERE datname = @databaseName;";
                    System.Data.Common.DbParameter parameter = cmd.CreateParameter();
                    parameter.ParameterName = "@databaseName";
                    parameter.Value = driver.DbName;
                    cmd.Parameters.Add(parameter);

                    object ret = cmd.ExecuteScalar();
                    if (ret != null)
                    {
                        int databaseID = (int)ret;
                        result = databaseID > 0;
                    }
                }
                catch (Exception ex)
                {
                    // do nothing
                }
                finally
                {
                    Conn.Close();
                }
            }

            return result;
        }
    }
}
