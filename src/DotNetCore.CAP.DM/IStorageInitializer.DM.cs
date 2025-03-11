// Copyright (c) .NET Core Community. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dm;
using DotNetCore.CAP.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCore.CAP.DM
{
    public class DMStorageInitializer : IStorageInitializer
    {
        private readonly IOptions<DMOptions> _options;
        private readonly ILogger _logger;

        public DMStorageInitializer(
            ILogger<DMStorageInitializer> logger,
            IOptions<DMOptions> options)
        {
            _options = options;
            _logger = logger;
        }

        public virtual string GetPublishedTableName()
        {
            return $@"""{_options.Value.Schema}"".""published""";
        }

        public virtual string GetReceivedTableName()
        {
            return $@"""{_options.Value.Schema}"".""received""";
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;

            var sql = CreateDbTablesScript();
            await using (var connection = new DmConnection(_options.Value.ConnectionString))
                foreach (var item in sql)
                {
                    connection.ExecuteNonQuery(item);
                }
            _logger.LogDebug("Ensuring all create database tables script are applied.");
        }


        protected virtual List<string> CreateDbTablesScript()
        {
            List<string> result = new List<string>() {
               $@"
DECLARE
    SCHEMA_EXIST INT;
BEGIN
    SELECT COUNT(*) INTO SCHEMA_EXIST FROM SYSOBJECTS WHERE TYPE$ = 'SCH' AND NAME = '{_options.Value.Schema}';
    IF SCHEMA_EXIST = 0 THEN
        EXECUTE IMMEDIATE 'CREATE SCHEMA ""{_options.Value.Schema}""';
    END IF;
END;
",$@"
CREATE TABLE IF NOT EXISTS {GetPublishedTableName()} (
  ""Id"" BIGINT NOT NULL,
  ""Version"" VARCHAR(20) DEFAULT NULL,
  ""Name"" VARCHAR(400) NOT NULL,
  ""Group"" VARCHAR(200) DEFAULT NULL,
  ""Content"" CLOB,
  ""Retries"" INT DEFAULT NULL,
  ""Added"" DATETIME NOT NULL,
  ""ExpiresAt"" DATETIME DEFAULT NULL,
  ""StatusName"" VARCHAR(50) NOT NULL,
  PRIMARY KEY (""Id"")
);

",$@"
CREATE TABLE IF NOT EXISTS {GetReceivedTableName()} (
  ""Id"" BIGINT NOT NULL,
  ""Version"" VARCHAR(20) DEFAULT NULL,
  ""Name"" VARCHAR(200) NOT NULL,
  ""Content"" CLOB,
  ""Retries"" INT DEFAULT NULL,
  ""Added"" DATETIME NOT NULL,
  ""ExpiresAt"" DATETIME DEFAULT NULL,
  ""StatusName"" VARCHAR(40) NOT NULL,
  PRIMARY KEY (""Id"")
);
"
,$@"
DECLARE
  v_count INT;
BEGIN
  SELECT COUNT(*) INTO v_count 
  FROM USER_INDEXES 
  WHERE TABLE_NAME ='received' 
    AND INDEX_NAME = 'index_received_expiresat';

  IF v_count = 0 THEN
    EXECUTE IMMEDIATE 'CREATE INDEX ""index_received_expiresat"" ON {GetReceivedTableName()}(""ExpiresAt"")';
  END IF;
END;
",$@"
DECLARE
  v_count INT;
BEGIN
  SELECT COUNT(*) INTO v_count 
  FROM USER_INDEXES 
  WHERE TABLE_NAME = 'published' 
    AND INDEX_NAME ='index_published_expiresat';

  IF v_count = 0 THEN
    EXECUTE IMMEDIATE 'CREATE INDEX ""index_published_expiresat"" ON {GetPublishedTableName()}(""ExpiresAt"")';
  END IF;
END;
"

            };
           
            return result;
        }
    }
}