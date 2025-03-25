// Copyright (c) .NET Core Community. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

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
            connection.ExecuteNonQuery(sql);
            _logger.LogDebug("Ensuring all create database tables script are applied.");
        }


        private string GetOnlyTableNameString(string schemaTableName)
        {
            if (string.IsNullOrWhiteSpace(schemaTableName))
            {
                return "";
            }
            return schemaTableName.Replace("\"", "").Replace($"{_options.Value.Schema}.", "");
        }
        protected virtual string CreateDbTablesScript()
        {
            var batchSql =
                $@"
DECLARE
    v_count INT;
BEGIN
    SELECT COUNT(*) INTO v_count FROM SYSOBJECTS WHERE TYPE$ = 'SCH' AND NAME = '{_options.Value.Schema}';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE SCHEMA ""{_options.Value.Schema}""';
    END IF; 
    SELECT COUNT(*)  
    INTO v_count  
    FROM USER_TABLES  
    WHERE TABLE_NAME = '{GetOnlyTableNameString(GetReceivedTableName())}';  
    IF v_count = 0 THEN  
        EXECUTE IMMEDIATE 'CREATE TABLE {GetReceivedTableName()} (  
              ""Id"" BIGINT NOT NULL,
              ""Version"" VARCHAR(20) DEFAULT NULL,
              ""Name"" VARCHAR(400) NOT NULL,
              ""Group"" VARCHAR(200) DEFAULT NULL,
              ""Content"" CLOB,
              ""Retries"" INT DEFAULT NULL,
              ""Added"" DATETIME NOT NULL,
              ""ExpiresAt"" DATETIME DEFAULT NULL,
              ""StatusName"" VARCHAR(50) NOT NULL,
              CONSTRAINT ""PK_{GetOnlyTableNameString(GetReceivedTableName())}"" PRIMARY KEY (""Id"")
        )';  
    END IF; 
    SELECT COUNT(*)  
    INTO v_count  
    FROM USER_TABLES  
    WHERE TABLE_NAME = '{GetOnlyTableNameString(GetPublishedTableName())}';  
    IF v_count = 0 THEN  
        EXECUTE IMMEDIATE 'CREATE TABLE {GetPublishedTableName()} (  
          ""Id"" BIGINT NOT NULL,
          ""Version"" VARCHAR(20) DEFAULT NULL,
          ""Name"" VARCHAR(200) NOT NULL,
          ""Content"" CLOB,
          ""Retries"" INT DEFAULT NULL,
          ""Added"" DATETIME NOT NULL,
          ""ExpiresAt"" DATETIME DEFAULT NULL,
          ""StatusName"" VARCHAR(40) NOT NULL,
          CONSTRAINT ""PK_{GetOnlyTableNameString(GetPublishedTableName())}"" PRIMARY KEY (""Id"")  
        )';  
    END IF;
  IF NOT EXISTS (
    SELECT 1 FROM USER_INDEXES 
    WHERE TABLE_NAME ='{GetOnlyTableNameString(GetReceivedTableName())}' 
    AND INDEX_NAME = 'IX_{GetOnlyTableNameString(GetReceivedTableName())}_Version_ExpiresAt_StatusName'
  ) THEN
    EXECUTE IMMEDIATE 'CREATE INDEX ""IX_{GetOnlyTableNameString(GetReceivedTableName())}_Version_ExpiresAt_StatusName"" ON  {GetReceivedTableName()} (""Version"", ""ExpiresAt"", ""StatusName"")';
  END IF;  
  IF NOT EXISTS (
    SELECT 1 FROM USER_INDEXES 
    WHERE TABLE_NAME ='{GetOnlyTableNameString(GetReceivedTableName())}'
    AND INDEX_NAME = 'IX_{GetOnlyTableNameString(GetReceivedTableName())}_ExpiresAt_StatusName'
  ) THEN
    EXECUTE IMMEDIATE 'CREATE INDEX ""IX_{GetOnlyTableNameString(GetReceivedTableName())}_ExpiresAt_StatusName"" ON  {GetReceivedTableName()} (""ExpiresAt"", ""StatusName"")';
  END IF;
  IF NOT EXISTS (
    SELECT 1 FROM USER_INDEXES 
    WHERE TABLE_NAME ='{GetOnlyTableNameString(GetPublishedTableName())}' 
    AND INDEX_NAME = 'IX_{GetOnlyTableNameString(GetPublishedTableName())}_Version_ExpiresAt_StatusName'
  ) THEN
    EXECUTE IMMEDIATE 'CREATE INDEX ""IX_{GetOnlyTableNameString(GetPublishedTableName())}_Version_ExpiresAt_StatusName"" ON  {GetPublishedTableName()} (""Version"", ""ExpiresAt"", ""StatusName"")';
  END IF;
  IF NOT EXISTS (
    SELECT 1 FROM USER_INDEXES 
    WHERE TABLE_NAME ='{GetOnlyTableNameString(GetPublishedTableName())}' 
    AND INDEX_NAME = 'IX_{GetOnlyTableNameString(GetPublishedTableName())}_ExpiresAt_StatusName'
  ) THEN
    EXECUTE IMMEDIATE 'CREATE INDEX ""IX_{GetOnlyTableNameString(GetPublishedTableName())}_ExpiresAt_StatusName"" ON  {GetPublishedTableName()} (""ExpiresAt"", ""StatusName"")';
  END IF;
END;
    ";
            return batchSql;
        }

    }
}