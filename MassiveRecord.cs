namespace MassiveRecord {
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;

    using Massive;

    public enum FilterType { BeforeSave, BeforeDelete }

    public class MassiveContextBase : DynamicModel {
        private IDictionary<FilterType, List<Action<dynamic>>> bound_filters = null;
        private IDictionary<String, Func<dynamic, bool>> bound_validators = null;

        public MassiveContextBase( MassiveRecord.DynamicTable.IReadableConfiguration config ) :
            this( config.ConnectionStringName ) {
            TableName = config.TableName;
            PrimaryKeyField = config.PrimaryKeyField;
            bound_filters = config.Filters;
            bound_validators = config.Validators;
        }
        public MassiveContextBase( string connectionStringName,
                                    IDictionary<FilterType, List<Action<dynamic>>> filters = null,
                                    IDictionary<String, Func<dynamic, bool>> validators = null ) :
            base( connectionStringName ) {
            bound_filters = filters ?? new Dictionary<FilterType, List<Action<dynamic>>> {
                    { FilterType.BeforeSave, new List<Action<dynamic>>() },
                    { FilterType.BeforeDelete, new List<Action<dynamic>>() }
                };
            bound_validators = validators ?? new Dictionary<String, Func<dynamic, bool>>();
        }

        public void RegisterFilter( FilterType type, Action<dynamic> filter ) {
            bound_filters[ type ].Add( filter );
        }

        public override bool BeforeSave( dynamic item ) {
            if( bound_filters.ContainsKey(FilterType.BeforeSave) )
                bound_filters[ FilterType.BeforeSave ].ForEach( a => a( item ) );
            return true;
        }

        public override bool BeforeDelete( dynamic item ) {
            if( bound_filters.ContainsKey( FilterType.BeforeDelete ) )
                bound_filters[ FilterType.BeforeDelete ].ForEach( a => a( item ) );
            return true;
        }

        public override void Validate( dynamic item ) {
            Errors = Errors.Concat( bound_validators.Where( v => v.Value(item) ).Select( v => v.Key )).ToList();
        }

        public override bool TryInvokeMember( System.Dynamic.InvokeMemberBinder binder, object[] args, out object result ) {
            result = TryDispatchToMassive(binder.Name, args);
            var handled = true;
            if( result == null ) {
                var binderName  = binder.Name.ToLower().Replace( "_", "" );
                var cols        = ToColumns( binderName );
                var where       = ToWhere( cols, args );
                if( binderName.StartsWith( "findby" ) ) {
                    result = All( where: where.ToString() );
                } else if( binderName.StartsWith( "findorcreateby" ) ) {
                    result = All( where: where.ToString() );
                    if( !result.ToDictionary().Any() )
                        result = Insert( SteveAustin( cols, args, args.Length == cols.Length ? null : args[ args.Length - 1 ] ) );
                }
                handled = base.TryInvokeMember( binder, args, out result );
            }
            return handled;
        }

        private object TryDispatchToMassive(string methodName, object[] args ) {
            if( typeof(DynamicModel).GetMethod( methodName ) != null )
                return typeof( DynamicModel ).InvokeMember( methodName, BindingFlags.InvokeMethod, null, this, args );
            return null;
        }

        private dynamic SteveAustin( string[] props, object[] args, object thing ) {
            var sixmill_man = new ExpandoObject();
            var steve = sixmill_man as IDictionary<string, object>;
            for( int i = 0; i < props.Length; i++ ) {
                steve.Add( props[ i ], args[ i ] );
            }
            if( thing != null )
                foreach( var pair in thing.ToDictionary() )
                    if( !steve.ContainsKey( pair.Key ) )
                        steve.Add( pair.Key, pair.Value );
            return sixmill_man;
        }

        private string[] ToColumns(string methodName) {
            ( new[] { "findby", "findorcreateby" } ).ToList().ForEach( s => methodName = methodName.Replace( s, "" ) );
            return Regex.Split( methodName, "and" );
        }

        private string ToWhere( string[] cols, object[] args ) {
            var where = new StringBuilder();
            for( int i = 0; i < cols.Length; i++ )
                where.AppendFormat( "{0} [{1}] = {2} ", i > 0 ? " and " : "", cols[ i ], ToSql( args[ i ] ) );
            return where.ToString();
        }

        private string ToSql( object p ) {
            if( p is String ) return String.Format( "'{0}'", (string)p );
            else if( p is DateTime ) return String.Format( "'{0}'", ( (DateTime)p ).ToString() );
            else return p.ToString();
        }
    }

    /// <summary>
    /// A class that extends Massive with awesomes
    /// </summary>
    public static class DynamicTable {
        private readonly static IDictionary<String, ISettings> settings = new Dictionary<String, ISettings>();

        // interfaces for our "mini fluent interface"
        public interface IWhenAskedFor {
            /// <summary>
            /// Sets a key word that can used when <see cref="DynamicTable.Create"/> is called.
            /// </summary>
            IUse WhenAskedFor( string table );
        }
        public interface IUse {
            /// <summary>
            /// A method for you to specify what settings to use when creating this DynamicTable
            /// </summary>
            ISettings Use( Action<ISettings> use );
        }
        public interface IReadableConfiguration {
            string TableName { get; }
            string ConnectionStringName { get; }
            string PrimaryKeyField { get; }
            IDictionary<FilterType, List<Action<dynamic>>> Filters { get; }
            IDictionary<String, Func<dynamic, bool>> Validators { get; }
        }

        public interface ISettings {
            /// <summary>
            /// What table should we use?
            /// </summary>
            ISettings Table( string name );

            /// <summary>
            /// What Connection String should we use?
            /// </summary>
            /// <remarks>Defaults to the first available connectionstring in the App.config/Web.config</remarks>
            ISettings ConnectionString( string name );

            /// <summary>
            /// What Primary Key should we use? (Default is "Id")
            /// </summary>
            ISettings PrimaryKey( string name );

            /// <summary>
            /// Pass a lambda to alter the item just before it is saved
            /// </summary>
            ISettings BeforeSave( Action<dynamic> filter );

            /// <summary>
            /// Pass a lambda to do some work just before the item is deleted
            /// </summary>
            ISettings BeforeDelete( Action<dynamic> filter );

            IDictionary<String, Func<dynamic, bool>> Validators { get; set; }
        }

        // configurator implements them all we just cast it around
        public class DynamicTableConfigurator : IWhenAskedFor, IUse, ISettings, IReadableConfiguration {
            private IDictionary<FilterType, List<Action<dynamic>>> filters = new Dictionary<FilterType, List<Action<dynamic>>>();

            public DynamicTableConfigurator() {
                Validators = new Dictionary<String, Func<dynamic, bool>>();
            }

            public IUse WhenAskedFor( string table ) { Type = table; return this; }

            public ISettings Use( Action<ISettings> use ) { use( this ); return this; }

            public string Type { get; private set; }
            public string TableName { get; private set; }
            public string ConnectionStringName { get; private set; }
            public string PrimaryKeyField { get; private set; }


            public ISettings Table( string name ) { TableName = name; return this; }
            public ISettings ConnectionString( string name ) { ConnectionStringName = name; return this; }
            public ISettings PrimaryKey( string name ) { PrimaryKeyField = name; return this; }

            public IDictionary<FilterType, List<Action<dynamic>>> Filters { get { return filters; } }
            public IDictionary<String, Func<dynamic, bool>> Validators { get; set; }

            public ISettings BeforeSave( Action<dynamic> filter ) { return AddFilter( FilterType.BeforeSave, filter ); }
            public ISettings BeforeDelete( Action<dynamic> filter ) { return AddFilter( FilterType.BeforeDelete, filter ); }

            private ISettings AddFilter( FilterType type, Action<dynamic> filter ) {
                if( filters.ContainsKey( type ) ) filters[ type ].Add( filter );
                else filters.Add( type, new List<Action<dynamic>> { filter } );
                return this;
            }
        }

        public static void Configure( Func<IWhenAskedFor, ISettings> config ) {
            var tableSettings = config( new DynamicTableConfigurator() );
            settings.Add( ( (DynamicTableConfigurator)tableSettings ).Type, tableSettings );
        }

        public static dynamic Create( string table, string connectionString = null, string primaryKey = "Id" ) {
            MassiveContextBase context = null;
            if( settings.ContainsKey( table ) )
                context = new MassiveContextBase( (DynamicTableConfigurator)settings[ table ] );
            return context ?? new MassiveContextBase( connectionString ) { TableName = table, PrimaryKeyField = primaryKey };
        }
    }
}
