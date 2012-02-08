# MassiveRecord will make you love Massive even more.

If you've used [Massive](http://github.com/robconery/massive), you know how awesome it is. But you've probably thought _"This still seems like a lot more code than I really want to have to write"_. I was thinking the same thing.

Goals

 * Eliminate the need to create a new class for each Massive "Table"
 * Add awesome functionality from ActiveRecord (ex: `FindBy...` and `Reload`)
 * Add registration for event hooks: BeforeSave, BeforeDelete
 * Add registration for validation hooks
 * Do all of this in as few lines of code as possible
 * And most importantly, make Massive that much easier and fun to use!

What if you could do something like this:

```csharp
    var contactsTable = DynamicTable.Create(
                                    "Person.Contact",   // table
                                    "AdventureWorks",   // connectionstring
                                    "ContactID" );      // primarykey

    var contacts = contactsTable.FindByFirstNameAndLastName( "Jay", "Adams" );
```

Pretty clean, right? Yeah I think so too. But that's not enough. What if we wanted to find a user by `Email` and `MiddleInitial`? No worries. MassiveRecord has your back.

```csharp
    var contacts = contactsTable.FindByEmailAndMiddleInitial( "test@test.com", "G" );
```

Okay... what if we wanted to have the `FirstName` capitalized every time we save a contact? Normally we'd have to override the BeforeSave method in our `Contacts` class. With MassiveRecord it takes 1 line of code:

```csharp
    DynamicTable.RegisterFilter( FilterType.BeforeSave, "Person.Contact",
                                           user => user.FirstName = user.FirstName.ToUpper() );
```

Yeah, it's *that* easy.


What if you wanted to specify a configuration that MassiveRecord should use everytime it creates a specific table? No problem, we even added a nifty mini fluent interface to help you out:

```csharp
    // in your Global.asax or some other startup class, include this code
    DynamicTable.Configure( c => c.WhenAskedFor("Users").Use( s => {
        s.ConnectionString = "Test";
        s.PrimaryKey = "ContactID";
        s.Table = "Person.Contact";
    }));

    // in your controller or other class file do the following
    var usersTable = DynamicTable.Create("Users");
    var users = usersTable.FindByEmail("myuser@test.com"); // BOOM!
```

## Installation

Just download the MassiveRecord.cs file along with [Massive](http://github.com/robconery/massive) and throw them into your project and you're done!
