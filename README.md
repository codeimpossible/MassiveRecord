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

Pretty clean, right? Yeah I think so too. But that's not enough, what if we wanted to have the `FirstName` capitalized every time we save a contact? Normally we'd have to override the BeforeSave method in our `Contacts` class. With MassiveRecord it takes 1 line of code:

```csharp
    DynamicTable.RegisterBeforeSaveFilter( "Person.Contact", 
                                           user => user.FirstName = user.FirstName.ToUpper() );
```

Yeah, it's *that* easy.


## Installation

Just download the MassiveRecord.cs file along with [Massive](http://github.com/robconery/massive) and throw them into your project and you're done!
