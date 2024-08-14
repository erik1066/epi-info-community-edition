using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Text;
using System.Data.Common;
using Epi.Data.PostgreSQL.Forms;
using System.IO;
using Npgsql;
using Epi;
using System.Windows.Forms;

namespace Epi.Data.PostgreSQL
{
    /// <summary>
    /// PostgreSQLDBFactory - Database Factory for PostgreSQL Databases
    /// </summary>
    public class PostgreSQLDBFactory : IDbDriverFactory
    {

        #region Connection string on different OS 
        //Windows
        //   "Persist Security Info=False;database=myDB;server=myHost;Connect Timeout=30;user id=myUser; pwd=myPass";
        //Linux with MONO: filepath is all of the below statement
        //   "database=myDB;server=/var/lib/mysql/mysql.sock;user id=myUser; pwd=myPass";
        #endregion

        private NpgsqlConnectionStringBuilder mySQLConnBuild = new NpgsqlConnectionStringBuilder();

        public bool ArePrerequisitesMet()
        {
            return true;
        }

        public string PrerequisiteMessage
        {
            get { return string.Empty; }
        }

        #region IDbDriverFactory Members

        /// <summary>
        /// Create a database
        /// </summary>
        /// <param name="dbInfo">DbDriverInfo</param>
        public void CreatePhysicalDatabase(DbDriverInfo dbInfo)
        {
            NpgsqlConnectionStringBuilder masterBuilder = new NpgsqlConnectionStringBuilder(dbInfo.DBCnnStringBuilder.ToString());
            NpgsqlConnectionStringBuilder tempBuilder = new NpgsqlConnectionStringBuilder(dbInfo.DBCnnStringBuilder.ToString());
            
            //tempBuilder = dbInfo.DBCnnStringBuilder as MySqlConnectionStringBuilder;
            //The "test" database is installed by default with PostgreSQL.  System needs to login to this database to create a new database.
            tempBuilder.Database = "postgres"; 
            
            NpgsqlConnection masterConnection = new NpgsqlConnection(tempBuilder.ToString());
            
            try
            {
                NpgsqlCommand command = masterConnection.CreateCommand();
                if(dbInfo.DBName != null)
                {
                    string databaseName = dbInfo.DBName;
                    command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
                }
                
                masterConnection.Open();
                //Logger.Log(command.CommandText);
                command.ExecuteNonQuery();

                //reset database to new database for correct storage of meta tables
                tempBuilder.Database = dbInfo.DBName;
            }
            catch (Exception ex)
            {
                throw new System.ApplicationException("Could not create new PostgreSQL Database", ex);//(Epi.SharedStrings.CAN_NOT_CREATE_NEW_MYSQL, ex); 
            }
            finally
            {
                masterConnection.Close();
            }
        }

        /// <summary>
        /// Create an instance of database object
        /// </summary>
        /// <param name="connectionStringBuilder">A connection string builder which contains the connection string</param>
        /// <returns>IDbDriver instance</returns>
        public IDbDriver CreateDatabaseObject(System.Data.Common.DbConnectionStringBuilder connectionStringBuilder)
        {
            IDbDriver instance = new PostgreSQLDatabase();
            instance.ConnectionString = connectionStringBuilder.ConnectionString;
            
            return instance;
        }

        /// <summary>
        /// Create a database with a name that has already been established
        /// </summary>
        /// <param name="configDatabaseKey">Name of the database</param>
        /// <returns>IDbDriver instance</returns>
        public IDbDriver CreateDatabaseObjectByConfiguredName(string configDatabaseKey)
        {
            //may not use since PHIN is .MDB
            IDbDriver instance = null;
            Configuration config = Configuration.GetNewInstance();
            DataRow[] result = config.DatabaseConnections.Select("Name='" + configDatabaseKey + "'");
            if (result.Length == 1)
            {
                Epi.DataSets.Config.DatabaseRow dbConnection = (Epi.DataSets.Config.DatabaseRow)result[0];
                NpgsqlConnectionStringBuilder mySqlConnectionBuilder = new NpgsqlConnectionStringBuilder(dbConnection.ConnectionString);
                instance = CreateDatabaseObject(mySqlConnectionBuilder);
            }
            else
            {
                throw new GeneralException("Database name is not configured.");
            }

            return instance;
        }

        /// <summary>
        /// Launch GUI for connection string for an existing database  Throws NotSupportedException if there is 
        /// no GUI associated with the current environment.
        /// </summary>
        /// <returns>IConnectionStringGui</returns>
        public IConnectionStringGui GetConnectionStringGuiForExistingDb()
        {
            if (Configuration.Environment == ExecutionEnvironment.WindowsApplication)
            {
                return new ExistingConnectionStringDialog();
            }
            else
            {
                throw new NotSupportedException("No GUI associated with current environment.");
            }
        }

        /// <summary>
        /// Launch GUI for connection string for a new database  Throws NotSupportedException if there is 
        /// no GUI associated with the current environment.
        /// </summary>
        /// <returns>IConnectionStringGui</returns>
        public IConnectionStringGui GetConnectionStringGuiForNewDb()
        {
            
            if (Configuration.Environment == ExecutionEnvironment.WindowsApplication)
            {
                return new NonExistingConnectionStringDialog();
               
            }
            else
            {
                throw new NotSupportedException("No GUI associated with current environment.");
            }
        }

        /// <summary>
        /// Get a new connection, given a fileName
        /// </summary>
        /// <param name="fileName">Name of the file to become the connectionString</param>
        /// <returns>System.Data.Common.DbConnectionStringBuilder</returns>
        public System.Data.Common.DbConnectionStringBuilder RequestNewConnection(string fileName)
        {
            DbConnectionStringBuilder dbStringBuilder = new DbConnectionStringBuilder(false);
            dbStringBuilder.ConnectionString = fileName;

            return dbStringBuilder;
        }

        /// <summary>
        /// Get the default connection
        /// </summary>
        /// <param name="databaseName">Name of the database to get the default connection from</param>
        /// <returns></returns>
        public System.Data.Common.DbConnectionStringBuilder RequestDefaultConnection(string databaseName, string projectName = "")
        {
            DbConnectionStringBuilder dbStringBuilder = new DbConnectionStringBuilder(false);
            dbStringBuilder.ConnectionString = PostgreSQLDatabase.BuildDefaultConnectionString(databaseName);
            return dbStringBuilder;
        }

        /// <summary>
        /// Default PostgreSQL ConnectionString request.
        /// </summary>
        /// <param name="database">Data store.</param>
        /// <param name="server">Server location of database.</param>
        /// <param name="user">User account login Id.</param>
        /// <param name="password">User account password.</param>
        /// <returns>Strongly typed connection string builder.</returns>
        public System.Data.Common.DbConnectionStringBuilder RequestDefaultConnection(string database, string server, int port, string user, string password)
        {
            //mySQLConnBuild.PersistSecurityInfo = false;
            mySQLConnBuild.Database = database;
            mySQLConnBuild.Host = server;
            mySQLConnBuild.Port = port;
            mySQLConnBuild.Username = user;
            mySQLConnBuild.Password = password;

            return (mySQLConnBuild as DbConnectionStringBuilder); 
        }

        public bool CanClaimConnectionString(string connectionString)
        {
            string conn = connectionString.ToLowerInvariant();
            if (conn.Contains("host") && conn.Contains("syncnotification"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        public string ConvertFileStringToConnectionString(string fileString)
        {
            return fileString;
        }

        public string GetCreateFromDataTableSQL(string tableName, DataTable table)
        {
            string sql = $"CREATE TABLE {tableName} (\n";
            bool HasUniqueKey = false;
            
            // columns
            foreach (DataColumn column in table.Columns)
            {
                if (column.ColumnName.Equals("uniquekey", StringComparison.OrdinalIgnoreCase))
                {
                    HasUniqueKey = true;
                    sql += column.ColumnName + " SERIAL PRIMARY KEY,\n";
                }
                else
                {
                    if (!column.ColumnName.Contains("."))
                    {
                        sql += column.ColumnName + " " + SQLGetType(column) + ",\n";
                    }
                }
            }

            if (HasUniqueKey)
            {
                sql = sql + "PRIMARY KEY (uniquekey)\n";
            }
            else
            {
                sql = sql.TrimEnd(new char[] { ',', '\n' });
            }

            sql = sql + "\n);";

            return sql;
        }

        private string SQLGetType(DataColumn column)
        {
            return SQLGetType(column.DataType, column.MaxLength, 10, 2);
        }

        public string SQLGetType(object type, int columnSize, int numericPrecision, int numericScale)
        {
            switch (type.ToString())
            {
                case "System.Boolean":
                    return "boolean";
                case "System.Byte":
                    return "smallint"; // There is no direct byte type in PostgreSQL, smallint is the closest.
                case "System.Int16":
                    return "smallint";
                case "System.Int32":
                    return "integer";
                case "System.Int64":
                    return "bigint";
                case "System.Single":
                    return "real";
                case "System.Double":
                    return "double precision";
                case "System.Decimal":
                    return "numeric";
                case "System.DateTime":
                    return "timestamp without time zone"; // or "timestamp with time zone"
                case "System.String":
                    return "text"; // or you could use varchar(n), where n is the length limit
                case "System.Guid":
                    return "uuid";
                default:
                    throw new Exception(type.ToString() + " not implemented.");
            }
        }
    }
}
