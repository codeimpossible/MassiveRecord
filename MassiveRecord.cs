namespace MassiveRecord {
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;

    using Massive;

    public enum FilterType { BeforeSave, BeforeDelete }

    public class MassiveContextBase : DynamicModel {
        private IDictionary<FilterType, List<Action<dynamic>>> bound_filters = null;

        public MassiveContextBase ( string connectionStringName,
                                    IDictionary<FilterType, List<Action<dynamic>>> filters = null ) :
            base( connectionStringName ) {
            bound_filters = filters ?? new Dictionary<FilterType, List<Action<dynamic>>> {
                    { FilterType.BeforeSave, new List<Action<dynamic>>() },
                    { FilterType.BeforeDelete, new List<Action<dynamic>>() }
                };
        }

        public void RegisterFilter ( FilterType type, Action<dynamic> filter ) {
            bound_filters[type].Add( filter );
        }

        public override bool BeforeSave ( dynamic item ) {
            bound_filters[FilterType.BeforeSave].ForEach( a => a( item ) );
            return true;
        }

        public override bool BeforeDelete ( dynamic item ) {
            bound_filters[FilterType.BeforeDelete].ForEach( a => a( item ) );
            return true;
        }

        public override bool TryInvokeMember ( System.Dynamic.InvokeMemberBinder binder, object[] args, out object result ) {
            if( binder.Name.ToLower().StartsWith( "findby" ) ) {
                var where = new StringBuilder();
                var method = binder.Name.ToLower().Replace( "findby", "" );
                var methodColumns = Regex.Split( method, "and" );

                for( int i = 0; i < methodColumns.Length; i++ )
                    where.AppendFormat( "{0} [{1}] = {2} ", i > 0 ? " and " : "", methodColumns[i], ToSql( args[i] ) );
                result = All( where: where.ToString() );
                return true;
            }

            var type = GetType();
            if( type.GetMethod( binder.Name ) != null ) {
                result = type.InvokeMember( binder.Name, BindingFlags.Default | BindingFlags.InvokeMethod, null, this, args );
                return true;
            }

            return base.TryInvokeMember( binder, args, out result );
        }

        private string ToSql ( object p ) {
            if( p is String ) return String.Format( "'{0}'", (string)p );
            else if( p is DateTime ) return String.Format( "'{0}'", ( (DateTime)p ).ToString() );
            else return p.ToString();
        }
    }

    public static class DynamicTable {
        private readonly static IDictionary<String, IDictionary<FilterType, List<Action<dynamic>>>> filters =
            new Dictionary<String, IDictionary<FilterType, List<Action<dynamic>>>>();

        private readonly static IDictionary<String, ISettings> settings = new Dictionary<String, ISettings>();

        // interfaces for our "mini DSL"
        public interface IWhenAskedFor {
            IUse WhenAskedFor ( string table );
        }
        public interface IUse {
            ISettings Use ( Action<ISettings> use );
        }
        public interface ISettings {
            string Table { get; set; }
            string ConnectionString { get; set; }
            string PrimaryKey { get; set; }
        }

        // configurator implements them all we just cast it around
        public class DynamicTableConfigurator : IWhenAskedFor, IUse, ISettings {
            public IUse WhenAskedFor ( string table ) { return this; }

            public ISettings Use ( Action<ISettings> use ) {
                use(this);
                return this;
            }

            public string Table { get; set; }
            public string ConnectionString { get; set; }
            public string PrimaryKey { get; set; }
        }

        public static void Configure ( Func<IWhenAskedFor, ISettings> config ) {
            var tableSettings = config(new DynamicTableConfigurator());
            settings.Add( tableSettings.Table, tableSettings );
        }

        public static dynamic Create ( string table, string connectionString = null, string primaryKey = "Id" ) {
            var table_filters = filters.ContainsKey( table ) ? filters[table] : null;
            return new MassiveContextBase( connectionString, table_filters ) {
                TableName = table,
                PrimaryKeyField = primaryKey
            };
        }

        public static void RegisterFilter ( FilterType type, string tableName, Action<dynamic> filter ) {
            if( filters.ContainsKey( tableName ) ) {
                if( filters[tableName].ContainsKey( type ) ) {
                    filters[tableName][type].Add( filter );
                } else filters[tableName].Add( type, new List<Action<dynamic>>() );
            } else filters.Add( tableName, new Dictionary<FilterType, List<Action<dynamic>>> { { type, new List<Action<dynamic>> { filter } } } );
        }
    }
}
