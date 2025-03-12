# DotNetCore.CAP.DM

CAP is a library based on .Net standard, which is a solution to deal with distributed transactions, has the function of EventBus, it is lightweight, easy to use, and efficient.

In the process of building an SOA or MicroService system, we usually need to use the event to integrate each service. In the process, simple use of message queue does not guarantee reliability. CAP adopts local message table program integrated with the current database to solve exceptions that may occur in the process of the distributed system calling each other. It can ensure that the event messages are not lost in any case.

You can also use CAP as an EventBus. CAP provides a simpler way to implement event publishing and subscriptions. You do not need to inherit or implement any interface during subscription and sending process.

DotNetCore.CAP.DM provides support for CAP with DM (Dameng) database integration.

This project primarily addresses a current need to migrate to the DM database, implemented using a lower version of CAP. A subsequent merge request will be submitted to the CAP project.

## Install
```
Install-Package DotNetCore.CAP.DM
```




