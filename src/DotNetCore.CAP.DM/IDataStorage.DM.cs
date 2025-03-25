// Copyright (c) .NET Core Community. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dm;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Messages;
using DotNetCore.CAP.Monitoring;
using DotNetCore.CAP.Persistence;
using DotNetCore.CAP.Serialization;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;


namespace DotNetCore.CAP.DM
{
    public class DMDataStorage : IDataStorage
    {
        private readonly IOptions<DMOptions> _options;
        private readonly IOptions<CapOptions> _capOptions;
        private readonly IStorageInitializer _initializer;
        private readonly ISerializer _serializer;
        private readonly string _pubName;
        private readonly string _recName;

        public DMDataStorage(
            IOptions<DMOptions> options,
            IOptions<CapOptions> capOptions,
            IStorageInitializer initializer,
            ISerializer serializer)
        {
            _options = options;
            _capOptions = capOptions;
            _initializer = initializer;
            _serializer = serializer;
            _pubName = initializer.GetPublishedTableName();
            _recName = initializer.GetReceivedTableName();
        }

        public async Task ChangePublishStateAsync(MediumMessage message, StatusName state) =>
            await ChangeMessageStateAsync(_pubName, message, state);

        public async Task ChangeReceiveStateAsync(MediumMessage message, StatusName state) =>
            await ChangeMessageStateAsync(_recName, message, state);

        public MediumMessage StoreMessage(string name, Message content, object dbTransaction = null)
        {
            var sql = $@"INSERT INTO {_pubName}(""Id"",""Version"",""Name"",""Content"",""Retries"",""Added"",""ExpiresAt"",""StatusName"")" +
                      $" VALUES(:Id,'{_options.Value.Version}',:Name,:Content,:Retries,:Added,:ExpiresAt,:StatusName);";

            var message = new MediumMessage
            {
                DbId = content.GetId(),
                Origin = content,
                Content = _serializer.Serialize(content),
                Added = DateTime.Now,
                ExpiresAt = null,
                Retries = 0
            };

            object[] sqlParams =
            {
                new DmParameter(":Id", message.DbId),
                new DmParameter(":Name", name),
                new DmParameter(":Content", message.Content),
                new DmParameter(":Retries", message.Retries),
                new DmParameter(":Added", message.Added),
                new DmParameter(":ExpiresAt", message.ExpiresAt.HasValue ? (object)message.ExpiresAt.Value : DBNull.Value),
                new DmParameter(":StatusName", nameof(StatusName.Scheduled)),
            };

            if (dbTransaction == null)
            {
                using var connection = new DmConnection(_options.Value.ConnectionString);
                connection.ExecuteNonQuery(sql, sqlParams: sqlParams);
            }
            else
            {
                var dbTrans = dbTransaction as IDbTransaction;
                if (dbTrans == null && dbTransaction is IDbContextTransaction dbContextTrans)
                {
                    dbTrans = dbContextTrans.GetDbTransaction();
                }

                var conn = dbTrans?.Connection;
                conn.ExecuteNonQuery(sql, dbTrans, sqlParams);
            }

            return message;
        }

        public void StoreReceivedExceptionMessage(string name, string group, string content)
        {
            object[] sqlParams =
            {
                new DmParameter(":Id", SnowflakeId.Default().NextId().ToString()),
                new DmParameter(":Name", name),
                new DmParameter(":Group", group),
                new DmParameter(":Content", content),
                new DmParameter(":Retries", _capOptions.Value.FailedRetryCount),
                new DmParameter(":Added", DateTime.Now),
                new DmParameter(":ExpiresAt", DateTime.Now.AddDays(15)),
                new DmParameter(":StatusName", nameof(StatusName.Failed))
            };

            StoreReceivedMessage(sqlParams);
        }

        public MediumMessage StoreReceivedMessage(string name, string group, Message message)
        {
            var mdMessage = new MediumMessage
            {
                DbId = SnowflakeId.Default().NextId().ToString(),
                Origin = message,
                Added = DateTime.Now,
                ExpiresAt = null,
                Retries = 0
            };

            object[] sqlParams =
            {
                new DmParameter(":Id", mdMessage.DbId),
                new DmParameter(":Name", name),
                new DmParameter(":Group", group),
                new DmParameter(":Content", _serializer.Serialize(mdMessage.Origin)),
                new DmParameter(":Retries", mdMessage.Retries),
                new DmParameter(":Added", mdMessage.Added),
                new DmParameter(":ExpiresAt", mdMessage.ExpiresAt.HasValue ? (object) mdMessage.ExpiresAt.Value : DBNull.Value),
                new DmParameter(":StatusName", nameof(StatusName.Scheduled))
            };

            StoreReceivedMessage(sqlParams);
            return mdMessage;
        }

        public async Task<int> DeleteExpiresAsync(string table, DateTime timeout, int batchCount = 1000, CancellationToken token = default)
        {
            await using var connection = new DmConnection(_options.Value.ConnectionString);
            return connection.ExecuteNonQuery(
                $@"DELETE FROM {table} WHERE ""ExpiresAt"" < :timeout limit :batchCount;", null,
                new DmParameter(":timeout", timeout), new DmParameter(":batchCount", batchCount));
        }

        public async Task<IEnumerable<MediumMessage>> GetPublishedMessagesOfNeedRetry() =>
            await GetMessagesOfNeedRetryAsync(_pubName);

        public async Task<IEnumerable<MediumMessage>> GetReceivedMessagesOfNeedRetry() =>
            await GetMessagesOfNeedRetryAsync(_recName);

        public IMonitoringApi GetMonitoringApi()
        {
            return new DMMonitoringApi(_options, _initializer);
        }

        private async Task ChangeMessageStateAsync(string tableName, MediumMessage message, StatusName state)
        {
            var sql =
                $@"UPDATE {tableName} SET ""Content""=:Content,""Retries""=:Retries,""ExpiresAt""=:ExpiresAt,""StatusName""=:StatusName WHERE ""Id""=:Id;";

            object[] sqlParams =
            {
                new DmParameter(":Id", message.DbId),
                new DmParameter(":Content", _serializer.Serialize(message.Origin)),
                new DmParameter(":Retries", message.Retries),
                new DmParameter(":ExpiresAt", message.ExpiresAt),
                new DmParameter(":StatusName", state.ToString("G"))
            };

            await using var connection = new DmConnection(_options.Value.ConnectionString);
            connection.ExecuteNonQuery(sql, sqlParams: sqlParams);
        }

        private void StoreReceivedMessage(object[] sqlParams)
        {
            var sql = $@"INSERT INTO {_recName}(""Id"",""Version"",""Name"",""Group"",""Content"",""Retries"",""Added"",""ExpiresAt"",""StatusName"") " +
                      $"VALUES(:Id,'{_options.Value.Version}',:Name,:Group,:Content,:Retries,:Added,:ExpiresAt,:StatusName);";

            using var connection = new DmConnection(_options.Value.ConnectionString);
            connection.ExecuteNonQuery(sql, sqlParams: sqlParams);
        }

        private async Task<IEnumerable<MediumMessage>> GetMessagesOfNeedRetryAsync(string tableName)
        {
            var fourMinAgo = DateTime.Now.AddMinutes(-4).ToString("O");
            var sql =
                $@"SELECT ""Id"",""Content"",""Retries"",""Added"" FROM {tableName} WHERE ""Retries""<{_capOptions.Value.FailedRetryCount} " +
                $@"AND ""Version""='{_capOptions.Value.Version}' AND ""Added""<'{fourMinAgo}' AND (""StatusName"" = '{StatusName.Failed}' OR ""StatusName"" = '{StatusName.Scheduled}') LIMIT 200;";

            await using var connection = new DmConnection(_options.Value.ConnectionString);
            var result = connection.ExecuteReader(sql, reader =>
            {
                var messages = new List<MediumMessage>();
                while (reader.Read())
                {
                    messages.Add(new MediumMessage
                    {
                        DbId = reader.GetInt64(0).ToString(),
                        Origin = _serializer.Deserialize(reader.GetString(1)),
                        Retries = reader.GetInt32(2),
                        Added = reader.GetDateTime(3)
                    });
                }

                return messages;
            });

            return result;
        }
    }
}
