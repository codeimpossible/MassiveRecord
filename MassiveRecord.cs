namespace MassiveRecord {
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;

    using Massive;
    
    public class MassiveContextBase : DynamicModel {
        private List<Action<dynamic>> before_save_filters = null;

        public MassiveContextBase ( string connectionStringName, List<Action<dynamic>> filters = null ) :
            base( connectionStringName ) {
            before_save_filters = filters ?? new List<Action<dynamic>>();
        }

        public void RegisterBeforeSaveFilter ( Action<dynamic> filter ) {
            before_save_filters.Add( filter );
        }

        public override bool BeforeSave ( dynamic item ) {
            before_save_filters.ForEach( a => a( item ) );
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
        private readonly static IDictionary<String, List<Action<dynamic>>> before_save_filters =
            new Dictionary<String, List<Action<dynamic>>>();

        public static dynamic Create<MODEL> ( string connectionString = null, string primaryKey = "Id" ) {
            var table = typeof( MODEL ).Name;
            return Create( table, connectionString, primaryKey );
        }

        public static dynamic Create ( string table, string connectionString = null, string primaryKey = "Id" ) {
            var filters = before_save_filters.ContainsKey( table ) ? before_save_filters[table] : null;
            return new MassiveContextBase( connectionString, filters ) {
                TableName = table,
                PrimaryKeyField = primaryKey
            };
        }

        public static void RegisterBeforeSaveFilter<TMODEL> ( Action<dynamic> filter ) {
            var type = typeof( TMODEL ).Name;
            RegisterBeforeSaveFilter( type, filter );
        }

        public static void RegisterBeforeSaveFilter ( string tableName, Action<dynamic> filter ) {
            if( before_save_filters.ContainsKey( tableName ) ) before_save_filters[tableName].Add( filter );
            else before_save_filters.Add( tableName, new List<Action<dynamic>> { filter } );
        }
    }
}