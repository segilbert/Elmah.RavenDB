using System;
using System.Collections;
using System.Configuration;
using System.Linq;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.Extensions;
using Raven.Client.Linq;

namespace Elmah.RavenDbErrorLog
{
    public class RavenDbErrorLog : ErrorLog
    {
        private readonly string _connectionString;

        private IDocumentStore _documentStore;

        public RavenDbErrorLog(IDictionary config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            var connectionString = GetConnectionString(config);

            //
            // If there is no connection string to use then throw an 
            // exception to abort construction.
            //

            if (connectionString.Length == 0)
                throw new ApplicationException("Connection string is missing for the RavenDB error log.");

            _connectionString = connectionString;

            //
            // Set the application name as this implementation provides
            // per-application isolation over a single store.
            //
            var appName = String.Empty;
            if (config["applicationName"] != null)
            {
                appName = (string)config["applicationName"];
            }

            ApplicationName = appName;

            InitDocumentStore();
        }

        private void InitDocumentStore()
        {
            _documentStore = new DocumentStore
            {
                ConnectionStringName = _connectionString
            };

            _documentStore.Conventions.DocumentKeyGenerator = c => Guid.NewGuid().ToString();

            _documentStore.Initialize();

            _documentStore.DatabaseCommands.EnsureDatabaseExists(ApplicationName);
        }

        //public RavenDbErrorLog(string connectionString)
        //{
        //    if (connectionString == null)
        //        throw new ArgumentNullException("connectionString");

        //    if (connectionString.Length == 0)
        //        throw new ArgumentException(null, "connectionString");

        //    _connectionString = connectionString;
        //    InitDocumentStore();
        //}

        public override string Log(Error error)
        {
            if (error == null)
            {
                throw new ArgumentNullException("error");
            }

            var errorDoc = new ErrorDocument
            {
                Error = error
            };

            using (var session = _documentStore.OpenSession(ApplicationName))
            {
                session.Store(errorDoc);
                session.SaveChanges();
            }

            return errorDoc.Id;
        }

        public override ErrorLogEntry GetError(string id)
        {
            ErrorDocument document;

            using (var session = _documentStore.OpenSession(ApplicationName))
            {
                document = session.Load<ErrorDocument>(id);
            }

            var result = new ErrorLogEntry(this, id, document.Error);

            return result;
        }

        public override int GetErrors(int pageIndex, int pageSize, IList errorEntryList)
        {
            using (var session = _documentStore.OpenSession(ApplicationName))
            {
                RavenQueryStatistics stats;

                var result = session.Query<ErrorDocument>()
                                    .Statistics(out stats)
                                    .Skip(pageSize * pageIndex)
                                    .Take(pageSize)
                                    .OrderByDescending(c => c.Error.Time);

                foreach (var errorDocument in result)
                {
                    errorEntryList.Add(new ErrorLogEntry(this, errorDocument.Id, errorDocument.Error));
                }

                return stats.TotalResults;
            }
        }

        public override string Name
        {
            get
            {
                return "RavenDB Error Log";
            }
        }

        private static string GetConnectionString(IDictionary config)
        {
            // From ELMAH source

            //
            // First look for a connection string name that can be 
            // subsequently indexed into the <connectionStrings> section of 
            // the configuration to get the actual connection string.
            //

            string connectionStringName = (string)config["connectionStringName"];

            if (!string.IsNullOrEmpty(connectionStringName))
            {
                return connectionStringName;


                // TODO implement explicit connection string
                ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings[connectionStringName];

                if (settings == null)
                    return string.Empty;

                return settings.ConnectionString ?? string.Empty;
            }

            //
            // Connection string name not found so see if a connection 
            // string was given directly.
            //

            var connectionString = (string)config["connectionString"];
            if (!string.IsNullOrEmpty(connectionString))
                return connectionString;

            //
            // As a last resort, check for another setting called 
            // connectionStringAppKey. The specifies the key in 
            // <appSettings> that contains the actual connection string to 
            // be used.
            //

            var connectionStringAppKey = (string)config["connectionStringAppKey"];
            return !string.IsNullOrEmpty(connectionStringAppKey)
                 ? ConfigurationManager.AppSettings[connectionStringAppKey]
                 : string.Empty;
        }
    }
}