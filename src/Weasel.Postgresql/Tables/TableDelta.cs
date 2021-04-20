using System;
using System.Collections.Generic;
using System.Linq;
using Baseline;

namespace Weasel.Postgresql.Tables
{
    public class Change<T>
    {
        public Change(T expected, T actual)
        {
            Expected = expected;
            Actual = actual;
        }

        public T Expected { get; }
        public T Actual { get; }
    }
    
    public class ItemDelta<T> where T: INamed
    {
        private readonly List<Change<T>> _different = new List<Change<T>>();
        private readonly List<T> _matched = new List<T>();
        private readonly List<T> _extras = new List<T>();
        private readonly List<T> _missing = new List<T>();

        public bool HasChanges()
        {
            return _different.Any() || _extras.Any() || _missing.Any();
        }

        public IReadOnlyList<Change<T>> Different => _different;

        public IReadOnlyList<T> Matched => _matched;

        public IReadOnlyList<T> Extras => _extras;

        public IReadOnlyList<T> Missing => _missing;
        
        public ItemDelta(IEnumerable<T> expectedItems, IEnumerable<T> actualItems, Func<T, T, bool> comparison = null)
        {
            comparison ??= (expected, actual) => expected.Equals(actual);
            var expecteds = expectedItems.ToDictionary(x => x.Name);

            foreach (var actual in actualItems)
            {
                if (expecteds.TryGetValue(actual.Name, out var expected))
                {
                    if (comparison(expected, actual))
                    {
                        _matched.Add(actual);
                    }
                    else
                    {
                        _different.Add(new Change<T>(expected, actual));
                    }
                }
                else
                {
                    _extras.Add(actual);
                }
            }

            var actuals = actualItems.ToDictionary(x => x.Name);
            _missing.AddRange(expectedItems.Where(x => !actuals.ContainsKey(x.Name)));
        }
    }    
    
    public class TableDelta
    {
        private readonly DbObjectName _tableName;

        public TableDelta(Table expected, Table actual)
        {
            Columns = new ItemDelta<TableColumn>(expected.Columns, actual.Columns);
            Indexes = new ItemDelta<IIndexDefinition>(expected.Indexes, actual.Indexes,
                (e, a) => ActualIndex.Matches(e, a, expected));
            
            _tableName = expected.Identifier;

            compareForeignKeys(expected, actual);
        }
        
        public ItemDelta<TableColumn> Columns { get; }
        public ItemDelta<IIndexDefinition> Indexes { get; }

        private void compareForeignKeys(Table expected, Table actual)
        {
            // var schemaName = expected.Identifier.Schema;
            // var tableName = expected.Identifier.Name;
            //
            // // Locate FKs that exist, but aren't defined
            // var obsoleteFkeys = actual.ActualForeignKeys.Where(afk => expected.ForeignKeys.All(fk => fk.KeyName != afk.Name));
            // foreach (var fkey in obsoleteFkeys)
            // {
            //     ForeignKeyMissing.Add($"ALTER TABLE {schemaName}.{tableName} DROP CONSTRAINT {fkey.Name};");
            //     ForeignKeyMissingRollbacks.Add($"ALTER TABLE {schemaName}.{tableName} ADD CONSTRAINT {fkey.Name} {fkey.DDL};");
            // }
            //
            // // Detect changes
            // foreach (var fkey in expected.ForeignKeys)
            // {
            //     var actualFkey = actual.ActualForeignKeys.SingleOrDefault(afk => afk.Name == fkey.KeyName);
            //     if (actualFkey != null && fkey.CascadeDeletes != actualFkey.DoesCascadeDeletes())
            //     {
            //         // The fkey cascading has changed, drop and re-create the key
            //         ForeignKeyChanges.Add($"ALTER TABLE {schemaName}.{tableName} DROP CONSTRAINT {actualFkey.Name}; {fkey.ToDDL()};");
            //         ForeignKeyRollbacks.Add($"ALTER TABLE {schemaName}.{tableName} DROP CONSTRAINT {fkey.KeyName}; ALTER TABLE {schemaName}.{tableName} ADD CONSTRAINT {actualFkey.Name} {actualFkey.DDL};");
            //     }
            //     else if (actualFkey == null)// The foreign key is missing
            //     {
            //         ForeignKeyChanges.Add(fkey.ToDDL());
            //         ForeignKeyRollbacks.Add($"ALTER TABLE {schemaName}.{tableName} DROP CONSTRAINT {fkey.KeyName};");
            //     }
            // }
        }

        public readonly IList<string> ForeignKeyMissing = new List<string>();
        public readonly IList<string> ForeignKeyMissingRollbacks = new List<string>();

        public readonly IList<string> ForeignKeyChanges = new List<string>();
        public readonly IList<string> ForeignKeyRollbacks = new List<string>();

        public readonly IList<string> AlteredColumnTypes = new List<string>();
        public readonly IList<string> AlteredColumnTypeRollbacks = new List<string>();

        public bool Matches
        {
            get
            {
                if (Columns.HasChanges()) return false;

                if (Indexes.HasChanges()) return false;

                if (ForeignKeyChanges.Any())
                    return false;

                return true;
            }
        }

        public override string ToString()
        {
            return $"TableDiff for {_tableName}";
        }

    }
}