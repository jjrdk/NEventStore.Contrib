NEventStore
======================================================================

NEventStore is a persistence library used to abstract different storage implementations
when using event sourcing as storage mechanism. This library is developed with a specific focus on [DDD](http://en.wikipedia.org/wiki/Domain-driven_design)/[CQRS](http://cqrsinfo.com) applications.

Please see the [documentation](https://github.com/NEventStore/NEventStore/wiki) to get started and for more information.

This Contribution package for NEVentStore includes Firebird as supported database.

Next is a simple code snippet showing how to use the extension:

    IStoreEvents eventStore = Wireup.Init()
                              .LogToOutputWindow()
                              .UsingFirebirdPersistence("myconnectionstringName")
                              .WithDialect(new FirebirdSqlDialect())
                              .UsingJsonSerialization()
                                  .Compress()
                                  .EncryptWith(EncryptionKey).Build();


##### Developed with:

[![Resharper](http://neventstore.org/images/logo_resharper_small.gif)](http://www.jetbrains.com/resharper/)
[![TeamCity](http://neventstore.org/images/logo_teamcity_small.gif)](http://www.jetbrains.com/teamcity/)
[![dotCover](http://neventstore.org/images/logo_dotcover_small.gif)](http://www.jetbrains.com/dotcover/)
[![dotTrace](http://neventstore.org/images/logo_dottrace_small.gif)](http://www.jetbrains.com/dottrace/)
