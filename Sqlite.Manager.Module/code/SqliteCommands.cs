﻿using System;
using Kooboo.Sites.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kooboo.Lib.Helper;

namespace Sqlite.Menager.Module.code
{
    internal class SqliteCommands
    {
        private const string IndexNameFormat = "idx_{0}_{1}";
        private const string UniqueNameFormat = "idx_uniq_{0}_{1}";
        public const string KoobooSystemTable = "_kooboo_sys_setting";
        public const string DefaultIdSchema =
            "[{\"Name\":\"_id\",\"IsPrimaryKey\":true,\"IsIndex\":true,\"IsUnique\":true,\"DataType\":\"TEXT\",\"IsSystem\":true}]";

        public static string ListTables()
        {
            return "SELECT name FROM sqlite_master WHERE type='table' and name not like '\\_%' ESCAPE '\\';";
        }

        public static string TableExists(string name)
        {
            return $"SELECT name FROM sqlite_master WHERE type='table' and name='{name}' LIMIT 1";
        }

        public static string CreateSystemTable()
        {
            var columns = new[]
            {
                GetDefaultIdColumn(),
                new DbTableColumn { Name = "table_name", IsIndex = true, IsUnique = true,  DataType = "TEXT" },
                new DbTableColumn { Name = "schema", DataType = "TEXT" },
            };
            return CreateTableInternal(KoobooSystemTable, columns);
        }

        public static string CreateTable(string table)
        {
            return CreateTableInternal(table, new[] { GetDefaultIdColumn() });
        }

        public static string DeleteTables(IEnumerable<string> tables)
        {
            return string.Join("", tables.Select(table => $"DROP TABLE {table};"));
        }

        public static string GetSchema(string table)
        {
            return $"SELECT schema FROM {KoobooSystemTable} WHERE table_name='{table}' LIMIT 1;";
        }

        public static string UpdateSchema(string table, IEnumerable<DbTableColumn> columns, out object param)
        {
            param = new
            {
                tableName = table,
                schema = JsonHelper.Serialize(columns),
            };

            return $"UPDATE {KoobooSystemTable} SET schema = @schema WHERE table_name = @tableName";
        }

        public static string UpdateColumn(string table, List<DbTableColumn> originalColumns, List<DbTableColumn> columns)
        {
            if (columns.All(x => x.Name != Kooboo.IndexedDB.Dynamic.Constants.DefaultIdFieldName))
            {
                columns.Insert(0, GetDefaultIdColumn());
            }

            var sb = new StringBuilder();
            sb.AppendLine("BEGIN TRANSACTION;");
            var oldTable = $"_old_{table}_{DateTime.Now:yyyyMMddHHmmss}";

            // drop index
            var indexes = originalColumns.Where(x => !x.IsPrimaryKey && (x.IsIndex || x.IsUnique)).ToArray();
            foreach (var column in indexes)
            {
                var format = column.IsUnique ? UniqueNameFormat : IndexNameFormat;
                sb.AppendLine($"DROP INDEX {string.Format(format, table, column.Name)};");
            }

            // rename table
            sb.AppendLine($"ALTER TABLE {table} RENAME TO {oldTable};");
            // create new table
            sb.AppendLine(CreateTableInternal(table, columns));
            // copy data
            var intersect = originalColumns.Select(x => x.Name).Intersect(columns.Select(x => x.Name)).ToArray();
            if (intersect.Any())
            {
                var cols = string.Join("\",\"", intersect);
                sb.AppendLine($"INSERT INTO {table} (\"{cols}\") SELECT \"{cols}\" FROM {oldTable};");
            }

            // drop old table
            sb.AppendLine($"DROP TABLE {oldTable};");

            sb.AppendLine("END TRANSACTION;");
            return sb.ToString();
        }

        private static DbTableColumn GetDefaultIdColumn()
        {
            return new DbTableColumn
            {
                Name = "_id",
                IsPrimaryKey = true,
                IsIndex = true,
                IsUnique = true,
                DataType = "TEXT",
                IsSystem = true
            };
        }

        private static string GenerateColumnDefine(DbTableColumn column)
        {
            string dataType;
            switch (column.DataType.ToLower())
            {
                case "number":
                    dataType = "REAL";
                    break;
                case "bool":
                    dataType = "INTEGER";
                    break;
                case "string":
                case "datetime":
                default:
                    dataType = "TEXT";
                    break;
            }

            var length = column.Length > 0 ? $"({column.Length})" : "";
            return $"\"{column.Name}\" {dataType}{length}";
        }

        private static string CreateTableInternal(string table, IEnumerable<DbTableColumn> columns)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE \"{table}\"(");

            var primaryKeys = new List<string>();
            var indexes = new List<string>();
            var uniques = new List<string>();

            foreach (var column in columns)
            {
                if (column.IsPrimaryKey)
                {
                    primaryKeys.Add($"\"{column.Name}\"");
                }
                else if (column.IsUnique)
                {
                    uniques.Add(column.Name);
                }
                else if (column.IsIndex)
                {
                    indexes.Add(column.Name);
                }

                sb.AppendLine(GenerateColumnDefine(column) + ",");
            }

            if (primaryKeys.Any())
            {
                var primaryKeyString = string.Join(", ", primaryKeys);
                sb.AppendLine($"PRIMARY KEY ({primaryKeyString})");
            }

            sb.AppendLine(");");

            foreach (var columnName in indexes)
            {
                sb.AppendLine($"CREATE INDEX {string.Format(IndexNameFormat, table, columnName)} ON {table}({columnName});");
            }

            foreach (var columnName in uniques)
            {
                sb.AppendLine($"CREATE UNIQUE INDEX {string.Format(UniqueNameFormat, table, columnName)} ON {table}({columnName});");
            }

            return sb.ToString();
        }
    }
}
